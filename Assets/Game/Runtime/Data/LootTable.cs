using System;
using Framework;
using UnityEngine;

[Serializable]
public class LootRow : ITableRow
{
    public int Index;
    public int[] Items;
    public int[] Min;
    public int[] Max;
    public float[] Chance;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>드랍 테이블.</summary>
[CreateAssetMenu(menuName = "Game/Tables/LootTable")]
public class LootTable : TableAsset<LootRow>
{
}
