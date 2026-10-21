using System;
using Framework;
using UnityEngine;

[Serializable]
public class SpawnRow : ITableRow
{
    public int Index;
    public ESpawnKind Kind;
    public int RefId;
    public Vector3 Position;
    public int Count;
    public float Radius;
    public float Respawn;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>맵 배치 (몬스터/자원/상인/시작 지점).</summary>
[CreateAssetMenu(menuName = "Game/Tables/SpawnTable")]
public class SpawnTable : TableAsset<SpawnRow>
{
}
