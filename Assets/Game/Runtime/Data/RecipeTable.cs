using System;
using Framework;
using UnityEngine;

[Serializable]
public class RecipeRow : ITableRow
{
    public int Index;
    public EStation Station;
    public int Result;
    public int ResultCount;
    public int[] Inputs;
    public int[] InputCounts;
    public float CraftTime;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>제작법.</summary>
[CreateAssetMenu(menuName = "Game/Tables/RecipeTable")]
public class RecipeTable : TableAsset<RecipeRow>
{
}
