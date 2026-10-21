using System;
using Framework;
using UnityEngine;

[Serializable]
public class StructureRow : ITableRow
{
    public int Index;
    public int NameId;
    public EStructureType Type;
    public int Prefab;
    public int Icon;
    public int[] Costs;
    public int[] CostCounts;
    public Vector2 Footprint;
    public bool RequiresHeart;
    public EStation Station;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>건축물.</summary>
[CreateAssetMenu(menuName = "Game/Tables/StructureTable")]
public class StructureTable : TableAsset<StructureRow>
{
}
