using System;
using Framework;
using UnityEngine;

[Serializable]
public class ItemRow : ITableRow
{
    public int Index;
    public int NameId;
    public int DescId;
    public int Icon;
    public EItemType Type;
    public int MaxStack;
    public float GearLevel;
    public EEquipSlot EquipSlot;
    public int Weapon;
    public int[] Effects;
    public int DropModel;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>아이템. Icon 은 AssetTable 인덱스 → 어떤 아이콘을 노출할지 데이터로 결정.</summary>
[CreateAssetMenu(menuName = "Game/Tables/ItemTable")]
public class ItemTable : TableAsset<ItemRow>
{
}
