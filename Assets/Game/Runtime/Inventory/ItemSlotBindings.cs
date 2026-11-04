using Framework;

/// <summary>
/// 인벤토리 창의 칸 하나가 무엇을 가리키는지 (2026-11-01 드래그 앤 드롭). 칸 종류마다 한 클래스:
///  - InventorySlotBinding: 인벤토리 N 번 칸 / EquipSlotBinding: 장비 부위
/// 드롭은 더블 디스패치 — 끌어온 칸.DropOnto(놓은 칸) → 놓은 칸.ReceiveFrom{Inventory|Equipment}(끌어온 칸 정보).
/// 그래서 "인벤토리 → 인벤토리 = 옮기기 · 합치기 · 바꾸기, 인벤토리 → 장비 = 장착, 장비 → 인벤토리 = 그 칸으로 벗기" 를 if / switch 없이 고른다.
/// </summary>
public abstract class ItemSlotBinding
{
    public abstract ItemStack GetStack(PlayerCharacter InPlayer);

    /// <summary>클릭 (사용 · 장착 · 벗기).</summary>
    public abstract void Activate(PlayerCharacter InPlayer);

    /// <summary>이 칸을 끌어다 InTarget 에 놓음. 실제 처리는 InTarget 이 끌어온 칸 종류를 보고 한다.</summary>
    public abstract bool DropOnto(ItemSlotBinding InTarget, PlayerCharacter InPlayer);

    public virtual bool ReceiveFromInventory(int InFromSlot, PlayerCharacter InPlayer)
    {
        return false;
    }

    public virtual bool ReceiveFromEquipment(EEquipSlot InFromSlot, PlayerCharacter InPlayer)
    {
        return false;
    }
}

public sealed class InventorySlotBinding : ItemSlotBinding
{
    public readonly int Index;

    public InventorySlotBinding(int InIndex)
    {
        Index = InIndex;
    }

    public override ItemStack GetStack(PlayerCharacter InPlayer)
    {
        return InPlayer.Inventory.Slots[Index];
    }

    /// <summary>아이템 종류별 사용 (ItemUse — 장비면 장착, 소비 아이템이면 사용).</summary>
    public override void Activate(PlayerCharacter InPlayer)
    {
        var stack = GetStack(InPlayer);
        ItemRow row;
        if (stack.IsEmpty == true || Tables.Root.Item.TryGet(stack.ItemId, out row) == false)
        {
            return;
        }

        var use = ItemUse.Of(row.Type);
        if (use != null)
        {
            use.Use(InPlayer, row, Index);
        }
    }

    public override bool DropOnto(ItemSlotBinding InTarget, PlayerCharacter InPlayer)
    {
        return InTarget.ReceiveFromInventory(Index, InPlayer);
    }

    /// <summary>인벤토리 → 인벤토리: 옮기기 · 합치기 · 바꾸기.</summary>
    public override bool ReceiveFromInventory(int InFromSlot, PlayerCharacter InPlayer)
    {
        return InPlayer.Inventory.Move(InFromSlot, Index);
    }

    /// <summary>장비 → 인벤토리: 이 칸으로 벗기 (같은 부위 장비가 있으면 교체).</summary>
    public override bool ReceiveFromEquipment(EEquipSlot InFromSlot, PlayerCharacter InPlayer)
    {
        return InPlayer.Equipment.UnequipTo(InFromSlot, Index);
    }
}

public sealed class EquipSlotBinding : ItemSlotBinding
{
    public readonly EEquipSlot Slot;

    public EquipSlotBinding(EEquipSlot InSlot)
    {
        Slot = InSlot;
    }

    public override ItemStack GetStack(PlayerCharacter InPlayer)
    {
        int itemId = InPlayer.Equipment.GetEquipped(Slot);
        return new ItemStack { ItemId = itemId, Count = itemId != 0 ? 1 : 0 };
    }

    public override void Activate(PlayerCharacter InPlayer)
    {
        InPlayer.Equipment.Unequip(Slot);
    }

    public override bool DropOnto(ItemSlotBinding InTarget, PlayerCharacter InPlayer)
    {
        return InTarget.ReceiveFromEquipment(Slot, InPlayer);
    }

    /// <summary>인벤토리 → 장비: 이 부위 장비일 때만 장착.</summary>
    public override bool ReceiveFromInventory(int InFromSlot, PlayerCharacter InPlayer)
    {
        var stack = InPlayer.Inventory.Slots[InFromSlot];
        ItemRow row;
        bool fits = stack.IsEmpty == false && Tables.Root.Item.TryGet(stack.ItemId, out row) == true && row.EquipSlot == Slot;
        return fits == true && InPlayer.Equipment.EquipFromInventory(InFromSlot) == true;
    }
}

/// <summary>인벤토리에서 아이템을 클릭했을 때 (EItemType 마다 한 클래스). 자원은 등록하지 않아 아무 일도 없다.</summary>
public abstract class ItemUse
{
    public static ItemUse Of(EItemType InType)
    {
        return KindRegistry<ItemUse>.Get((int)InType);
    }

    public abstract void Use(PlayerCharacter InPlayer, ItemRow InRow, int InInventorySlot);
}

[Kind(EItemType.Weapon)]
[Kind(EItemType.Armor)]
public sealed class EquipItemUse : ItemUse
{
    public override void Use(PlayerCharacter InPlayer, ItemRow InRow, int InInventorySlot)
    {
        InPlayer.Equipment.EquipFromInventory(InInventorySlot);
    }
}

/// <summary>소비 아이템: ItemTable.Effects 를 자신에게 적용하고 1개 소모.</summary>
[Kind(EItemType.Consumable)]
public sealed class ConsumeItemUse : ItemUse
{
    public override void Use(PlayerCharacter InPlayer, ItemRow InRow, int InInventorySlot)
    {
        for (int i = 0; i < InRow.Effects.Length; ++i)
        {
            InPlayer.ApplyEffect(InRow.Effects[i], InPlayer, 0);
        }

        InPlayer.Inventory.Remove(InRow.Index, 1);
    }
}
