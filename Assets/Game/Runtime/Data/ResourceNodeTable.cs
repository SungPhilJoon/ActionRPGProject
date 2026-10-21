using System;
using Framework;
using UnityEngine;

[Serializable]
public class ResourceNodeRow : ITableRow
{
    public int Index;
    public int NameId;
    public int Prefab;
    public float Health;
    public EHarvestType Harvest;
    public int DropItem;
    public int DropMin;
    public int DropMax;
    public float Respawn;
    public float Scale;
    /// <summary>채집할 때 재생할 AnimationTable Index.</summary>
    public int Anim;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>채집 노드 (나무/바위/풀).</summary>
[CreateAssetMenu(menuName = "Game/Tables/ResourceNodeTable")]
public class ResourceNodeTable : TableAsset<ResourceNodeRow>
{
}
