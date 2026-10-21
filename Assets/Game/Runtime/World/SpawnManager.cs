using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// SpawnTable 기반 월드 배치. 프리팹은 Addressables 비동기 로드 후 생성한다.
/// 로드 도중 씬이 내려가면 CancellationToken 으로 중단하고, 결과를 쓰지 않는다 (Issues.md "비동기 로드 레이스").
/// </summary>
public class SpawnManager : IManager
{
    private struct PendingRespawn
    {
        public SpawnRow Row;
        public float Remaining;
    }

    private Transform Root;
    private readonly List<PendingRespawn> Respawns = new List<PendingRespawn>();
    private readonly CancellationTokenSource Cancel = new CancellationTokenSource();

    void IManager.Initialize()
    {
        Root = new GameObject("[World]").transform;
    }

    void IManager.UnInitialize()
    {
        Cancel.Cancel();
        Cancel.Dispose();
        Respawns.Clear();
        ResourceNode.ClearAll();
        if (Root != null)
        {
            Object.Destroy(Root.gameObject);
        }
    }

    void IManager.Update(float InDeltaTime)
    {
        for (int i = Respawns.Count - 1; i >= 0; --i)
        {
            var pending = Respawns[i];
            pending.Remaining -= InDeltaTime;
            if (pending.Remaining > 0.0f)
            {
                Respawns[i] = pending;
                continue;
            }

            Respawns.RemoveAt(i);
            spawnMonsterAsync(pending.Row.RefId, randomPoint(pending.Row), pending.Row).Forget();
        }
    }

    public Vector3 GetPlayerStart()
    {
        var rows = Tables.Root.Spawn.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            if (rows[i].Kind == ESpawnKind.PlayerStart)
            {
                return CombatUtil.SnapToGround(rows[i].Position);
            }
        }

        var context = SceneContext.Current;
        return context != null && context.PlayerStart != null ? context.PlayerStart.position : Vector3.zero;
    }

    public async UniTask SpawnWorldAsync()
    {
        var rows = Tables.Root.Spawn.All;
        var tasks = new List<UniTask>();
        for (int i = 0; i < rows.Count; ++i)
        {
            var row = rows[i];
            var random = new System.Random(row.Index);
            for (int n = 0; n < Mathf.Max(1, row.Count); ++n)
            {
                Vector3 position = randomPoint(row, random);
                switch (row.Kind)
                {
                    case ESpawnKind.Monster:
                        tasks.Add(spawnMonsterAsync(row.RefId, position, row));
                        break;
                    case ESpawnKind.Resource:
                        tasks.Add(spawnResourceAsync(row.RefId, position, (float)random.NextDouble() * 360.0f));
                        break;
                    case ESpawnKind.Merchant:
                        tasks.Add(spawnMerchantAsync(row.RefId, position));
                        break;
                }
            }
        }

        await UniTask.WhenAll(tasks);
    }

    /// <summary>치트/디버그용 즉시 스폰.</summary>
    public void SpawnMonster(int InCharacterId, Vector3 InPosition)
    {
        spawnMonsterAsync(InCharacterId, InPosition, null).Forget();
    }

    private async UniTask spawnMonsterAsync(int InCharacterId, Vector3 InPosition, SpawnRow InRow)
    {
        CharacterRow character;
        if (Tables.Root.Character.TryGet(InCharacterId, out character) == false)
        {
            return;
        }

        var prefab = await Managers.Get<AssetLoader>().LoadAsync<GameObject>(Tables.Address(character.Prefab), null, Cancel.Token);
        if (prefab == null || Root == null || Cancel.IsCancellationRequested == true)
        {
            return;
        }

        Vector3 position = snapToNavMesh(InPosition);
        var instance = Object.Instantiate(prefab, position, Quaternion.Euler(0.0f, Random.Range(0.0f, 360.0f), 0.0f), Root);
        var monster = instance.GetComponent<MonsterCharacter>();
        if (monster == null)
        {
            Log.Error($"Prefab for character {InCharacterId} has no MonsterCharacter");
            Object.Destroy(instance);
            return;
        }

        monster.InitializeMonster(character, position, InRow != null ? InRow.Index : 0);
        if (InRow != null && InRow.Respawn > 0.0f)
        {
            monster.OnDied += (victim, killer) => Respawns.Add(new PendingRespawn { Row = InRow, Remaining = InRow.Respawn });
        }
    }

    private async UniTask spawnResourceAsync(int InNodeId, Vector3 InPosition, float InYaw)
    {
        ResourceNodeRow node;
        if (Tables.Root.ResourceNode.TryGet(InNodeId, out node) == false)
        {
            return;
        }

        var prefab = await Managers.Get<AssetLoader>().LoadAsync<GameObject>(Tables.Address(node.Prefab), null, Cancel.Token);
        if (prefab == null || Root == null || Cancel.IsCancellationRequested == true)
        {
            return;
        }

        var instance = Object.Instantiate(prefab, CombatUtil.SnapToGround(InPosition), Quaternion.Euler(0.0f, InYaw, 0.0f), Root);
        instance.transform.localScale *= node.Scale > 0.0f ? node.Scale : 1.0f;
        ComponentUtil.GetOrAdd<ResourceNode>(instance).Initialize(node);
    }

    private async UniTask spawnMerchantAsync(int InCharacterId, Vector3 InPosition)
    {
        CharacterRow character;
        if (Tables.Root.Character.TryGet(InCharacterId, out character) == false)
        {
            return;
        }

        var prefab = await Managers.Get<AssetLoader>().LoadAsync<GameObject>(Tables.Address(character.Prefab), null, Cancel.Token);
        if (prefab == null || Root == null || Cancel.IsCancellationRequested == true)
        {
            return;
        }

        var instance = Object.Instantiate(prefab, CombatUtil.SnapToGround(InPosition), Quaternion.Euler(0.0f, 180.0f, 0.0f), Root);
        ComponentUtil.GetOrAdd<Merchant>(instance);
    }

    private static Vector3 snapToNavMesh(Vector3 InPosition)
    {
        Vector3 ground = CombatUtil.SnapToGround(InPosition);
        NavMeshHit hit;
        if (NavMesh.SamplePosition(ground, out hit, 6.0f, NavMesh.AllAreas) == true)
        {
            return hit.position;
        }

        return ground;
    }

    private static Vector3 randomPoint(SpawnRow InRow)
    {
        Vector2 offset = Random.insideUnitCircle * InRow.Radius;
        return InRow.Position + new Vector3(offset.x, 0.0f, offset.y);
    }

    private static Vector3 randomPoint(SpawnRow InRow, System.Random InRandom)
    {
        double angle = InRandom.NextDouble() * System.Math.PI * 2.0;
        double radius = System.Math.Sqrt(InRandom.NextDouble()) * InRow.Radius;
        return InRow.Position + new Vector3((float)(System.Math.Cos(angle) * radius), 0.0f, (float)(System.Math.Sin(angle) * radius));
    }
}
