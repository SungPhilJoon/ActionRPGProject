using System;
using Framework;
using UnityEngine;

[Serializable]
public class ShopRow : ITableRow
{
    public int Index;
    public int Item;
    public int Count;
    public int Price;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>상점. 프로젝트 규칙상 모든 Price = 0 (무료).</summary>
[CreateAssetMenu(menuName = "Game/Tables/ShopTable")]
public class ShopTable : TableAsset<ShopRow>
{
}
