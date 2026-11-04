using Cysharp.Threading.Tasks;
using Framework;
using UnityEngine;

/// <summary>SpawnTable 행 하나를 월드에 놓는다 (ESpawnKind 마다 한 클래스, 2026-11-01 switch → 다형성).</summary>
public abstract class SpawnHandler
{
    /// <summary>플레이어 시작 지점인가 (SpawnManager.GetPlayerStart).</summary>
    public virtual bool IsPlayerStart
    {
        get { return false; }
    }

    public abstract UniTask Spawn(SpawnManager InManager, SpawnRow InRow, Vector3 InPosition, System.Random InRandom);
}

[Kind(ESpawnKind.Monster)]
public sealed class MonsterSpawnHandler : SpawnHandler
{
    public override UniTask Spawn(SpawnManager InManager, SpawnRow InRow, Vector3 InPosition, System.Random InRandom)
    {
        return InManager.SpawnMonsterAsync(InRow.RefId, InPosition, InRow);
    }
}

/// <summary>채집 노드: 방향은 무작위.</summary>
[Kind(ESpawnKind.Resource)]
public sealed class ResourceSpawnHandler : SpawnHandler
{
    public override UniTask Spawn(SpawnManager InManager, SpawnRow InRow, Vector3 InPosition, System.Random InRandom)
    {
        return InManager.SpawnResourceAsync(InRow.RefId, InPosition, (float)InRandom.NextDouble() * 360.0f);
    }
}

[Kind(ESpawnKind.Merchant)]
public sealed class MerchantSpawnHandler : SpawnHandler
{
    public override UniTask Spawn(SpawnManager InManager, SpawnRow InRow, Vector3 InPosition, System.Random InRandom)
    {
        return InManager.SpawnMerchantAsync(InRow.RefId, InPosition);
    }
}

/// <summary>플레이어 시작 지점: 아무것도 놓지 않고 위치만 알려 준다.</summary>
[Kind(ESpawnKind.PlayerStart)]
public sealed class PlayerStartSpawnHandler : SpawnHandler
{
    public override bool IsPlayerStart
    {
        get { return true; }
    }

    public override UniTask Spawn(SpawnManager InManager, SpawnRow InRow, Vector3 InPosition, System.Random InRandom)
    {
        return UniTask.CompletedTask;
    }
}
