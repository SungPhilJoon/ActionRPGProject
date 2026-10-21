using System;
using Framework;
using UnityEngine;

[Serializable]
public struct ItemStack
{
    public int ItemId;
    public int Count;

    public bool IsEmpty
    {
        get { return ItemId == 0 || Count <= 0; }
    }
}

/// <summary>슬롯 기반 인벤토리. 아이템은 ItemTable 인덱스(int)로만 다룬다.</summary>
public class Inventory : MonoBehaviour
{
    public ItemStack[] Slots { get; private set; }

    public void Initialize(int InSize)
    {
        Slots = new ItemStack[InSize];
    }

    public int Size
    {
        get { return Slots.Length; }
    }

    /// <summary>추가하고 넣지 못한 개수를 돌려준다.</summary>
    public int Add(int InItemId, int InCount)
    {
        ItemRow row;
        if (InCount <= 0 || Tables.Root.Item.TryGet(InItemId, out row) == false)
        {
            return InCount;
        }

        int maxStack = Mathf.Max(1, row.MaxStack);
        int remaining = InCount;

        for (int i = 0; i < Slots.Length && remaining > 0; ++i)
        {
            if (Slots[i].ItemId != InItemId || Slots[i].Count >= maxStack)
            {
                continue;
            }

            int add = Mathf.Min(maxStack - Slots[i].Count, remaining);
            Slots[i].Count += add;
            remaining -= add;
        }

        for (int i = 0; i < Slots.Length && remaining > 0; ++i)
        {
            if (Slots[i].IsEmpty == false)
            {
                continue;
            }

            int add = Mathf.Min(maxStack, remaining);
            Slots[i].ItemId = InItemId;
            Slots[i].Count = add;
            remaining -= add;
        }

        notifyChanged();
        if (remaining > 0)
        {
            EventBus<NotifyEvent>.Publish(new NotifyEvent { TextId = (int)EText.InventoryFull, Color = Color.red });
        }

        return remaining;
    }

    public int Count(int InItemId)
    {
        int total = 0;
        for (int i = 0; i < Slots.Length; ++i)
        {
            if (Slots[i].ItemId == InItemId)
            {
                total += Slots[i].Count;
            }
        }

        return total;
    }

    public bool Has(int[] InItems, int[] InCounts)
    {
        if (InItems == null)
        {
            return true;
        }

        for (int i = 0; i < InItems.Length; ++i)
        {
            int need = InCounts != null && i < InCounts.Length ? InCounts[i] : 1;
            if (Count(InItems[i]) < need)
            {
                return false;
            }
        }

        return true;
    }

    public bool Remove(int InItemId, int InCount)
    {
        if (Count(InItemId) < InCount)
        {
            return false;
        }

        int remaining = InCount;
        for (int i = Slots.Length - 1; i >= 0 && remaining > 0; --i)
        {
            if (Slots[i].ItemId != InItemId)
            {
                continue;
            }

            int take = Mathf.Min(Slots[i].Count, remaining);
            Slots[i].Count -= take;
            remaining -= take;
            if (Slots[i].Count <= 0)
            {
                Slots[i] = new ItemStack();
            }
        }

        notifyChanged();
        return true;
    }

    public bool Remove(int[] InItems, int[] InCounts)
    {
        if (Has(InItems, InCounts) == false)
        {
            return false;
        }

        for (int i = 0; i < InItems.Length; ++i)
        {
            int need = InCounts != null && i < InCounts.Length ? InCounts[i] : 1;
            Remove(InItems[i], need);
        }

        return true;
    }

    public void TakeSlot(int InSlot)
    {
        Slots[InSlot] = new ItemStack();
        notifyChanged();
    }

    private void notifyChanged()
    {
        EventBus<InventoryChangedEvent>.Publish(new InventoryChangedEvent { Inventory = this });
    }
}
