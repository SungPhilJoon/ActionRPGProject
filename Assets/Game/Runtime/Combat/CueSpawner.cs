using System.Collections.Generic;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// CueTable 기반 GameplayCue 처리기. CueId → AssetTable 프리팹을 풀에서 꺼내 재생한다.
/// 지속형 큐(OnActive/OnRemove)는 (대상, 큐) 쌍으로 인스턴스를 추적해 이펙트가 빠질 때 회수한다.
/// </summary>
public class CueSpawner : IGameplayCueHandler
{
    private static readonly Dictionary<long, GameObject> Persistent = new Dictionary<long, GameObject>();

    public static void Spawn(int InCueId, Vector3 InPosition, Vector3 InDirection)
    {
        spawnInternal(InCueId, InPosition, InDirection, null);
    }

    void IGameplayCueHandler.OnExecute(GameplayCueParams InParams)
    {
        // 이펙트 실행 큐는 CueTable 의 Attach 여부에 따라 대상에 붙인다.
        spawnInternal(CueId, InParams.Location, InParams.Direction, InParams.Target != null ? InParams.Target.transform : null);
    }

    void IGameplayCueHandler.OnActive(GameplayCueParams InParams)
    {
        if (InParams.Target == null)
        {
            return;
        }

        long key = makeKey(InParams.Target, CueId);
        if (Persistent.ContainsKey(key) == true)
        {
            return;
        }

        var instance = spawnInternal(CueId, InParams.Target.transform.position, InParams.Direction, InParams.Target.transform);
        if (instance != null)
        {
            Persistent[key] = instance;
        }
    }

    void IGameplayCueHandler.OnRemove(GameplayCueParams InParams)
    {
        if (InParams.Target == null)
        {
            return;
        }

        long key = makeKey(InParams.Target, CueId);
        GameObject instance;
        if (Persistent.TryGetValue(key, out instance) == false)
        {
            return;
        }

        Persistent.Remove(key);
        if (instance != null)
        {
            Managers.Get<GameObjectPool>().Despawn(instance);
        }
    }

    /// <summary>
    /// GameplayCueManager 가 핸들러에 CueId 를 넘기지 않으므로, 큐마다 이 클래스의 인스턴스를 하나씩 등록해 CueId 를 기억한다.
    /// </summary>
    private int CueId;

    public CueSpawner(int InCueId)
    {
        CueId = InCueId;
    }

    public static void RegisterAll()
    {
        Persistent.Clear();
        var rows = Tables.Root.Cue.All;
        for (int i = 0; i < rows.Count; ++i)
        {
            GameplayCueManager.Register(rows[i].Index, new CueSpawner(rows[i].Index));
        }
    }

    private static GameObject spawnInternal(int InCueId, Vector3 InPosition, Vector3 InDirection, Transform InAttachTarget)
    {
        CueRow row;
        if (InCueId == 0 || Tables.Root.Cue.TryGet(InCueId, out row) == false)
        {
            return null;
        }

        var prefab = Managers.Get<AssetLoader>().LoadSync<GameObject>(Tables.Address(row.Asset));
        if (prefab == null)
        {
            return null;
        }

        Quaternion rotation = InDirection.sqrMagnitude > 0.001f ? Quaternion.LookRotation(MathUtil.Flat(InDirection) + Vector3.forward * 0.0001f) : Quaternion.identity;
        var instance = Managers.Get<GameObjectPool>().Spawn(prefab, InPosition, rotation);
        instance.transform.localScale = Vector3.one * (row.Scale > 0.0f ? row.Scale : 1.0f);
        if (row.Attach == true && InAttachTarget != null)
        {
            instance.transform.SetParent(InAttachTarget, true);
            instance.transform.localPosition = Vector3.up * 1.0f;
        }

        if (row.Lifetime > 0.0f)
        {
            ComponentUtil.GetOrAdd<TimedDespawn>(instance).Begin(row.Lifetime);
        }

        return instance;
    }

    private static long makeKey(AbilitySystemComponent InTarget, int InCueId)
    {
        return ((long)InTarget.GetInstanceID() << 32) | (uint)InCueId;
    }
}
