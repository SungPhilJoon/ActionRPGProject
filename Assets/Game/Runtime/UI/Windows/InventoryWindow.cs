using System.Collections.Generic;
using System.Text;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>
/// 인벤토리 + 장비 + 능력치 창. 슬롯 클릭 = 장착 / 소비 아이템 사용.
/// </summary>
public class InventoryWindow : UIWindow
{
    private static readonly EAttribute[] ShownStats =
    {
        EAttribute.MaxHealth, EAttribute.PhysicalPower, EAttribute.SpellPower, EAttribute.PhysicalResist,
        EAttribute.SpellResist, EAttribute.MoveSpeed, EAttribute.AttackSpeed, EAttribute.CritChance,
        EAttribute.CritDamage, EAttribute.LifeLeech, EAttribute.SunResist, EAttribute.ResourceYield, EAttribute.Level,
    };

    public RectTransform Grid;
    public RectTransform EquipRoot;
    public ItemSlotView SlotPrefab;
    public TextMeshProUGUI Stats;
    public TextMeshProUGUI Tooltip;

    private readonly List<ItemSlotView> Slots = new List<ItemSlotView>();
    private readonly List<ItemSlotView> EquipSlots = new List<ItemSlotView>();

    /// <summary>칸 수는 ConstTable.InventorySize 라 프리팹에 고정할 수 없다 → 슬롯 프리팹을 그 수만큼 띄운다.</summary>
    protected override void OnInitialize()
    {
        var grid = new UIItemList<ItemSlotView>(SlotPrefab, Grid);
        int size = Tables.ConstInt(EConst.InventorySize);
        for (int i = 0; i < size; ++i)
        {
            int index = i;
            var slot = grid.Add();
            slot.name = $"Slot{i}";
            slot.Button.onClick.AddListener(() => onSlotClicked(index));
            slot.Set(new ItemStack());
            Slots.Add(slot);
        }

        var equip = new UIItemList<ItemSlotView>(SlotPrefab, EquipRoot);
        for (int i = 0; i < (int)EEquipSlot.Count; ++i)
        {
            var slotType = (EEquipSlot)i;
            var slot = equip.Add();
            slot.name = $"Equip{i}";
            slot.Button.onClick.AddListener(() => onEquipClicked(slotType));
            slot.Set(new ItemStack());
            EquipSlots.Add(slot);
        }
    }

    private void OnEnable()
    {
        EventBus<InventoryChangedEvent>.Subscribe(onInventoryChanged);
        EventBus<EquipmentChangedEvent>.Subscribe(onEquipmentChanged);
    }

    private void OnDisable()
    {
        EventBus<InventoryChangedEvent>.Unsubscribe(onInventoryChanged);
        EventBus<EquipmentChangedEvent>.Unsubscribe(onEquipmentChanged);
    }

    protected override void OnOpen()
    {
        refresh();
    }

    private void onInventoryChanged(InventoryChangedEvent InEvent)
    {
        refresh();
    }

    private void onEquipmentChanged(EquipmentChangedEvent InEvent)
    {
        refresh();
    }

    private void refresh()
    {
        var player = PlayerCharacter.Local;
        if (player == null || Slots.Count == 0)
        {
            return;
        }

        var stacks = player.Inventory.Slots;
        for (int i = 0; i < Slots.Count && i < stacks.Length; ++i)
        {
            Slots[i].Set(stacks[i]);
        }

        for (int i = 0; i < EquipSlots.Count; ++i)
        {
            int itemId = player.Equipment.GetEquipped((EEquipSlot)i);
            EquipSlots[i].Set(new ItemStack { ItemId = itemId, Count = itemId != 0 ? 1 : 0 });
        }

        var builder = new StringBuilder();
        for (int i = 0; i < ShownStats.Length; ++i)
        {
            builder.AppendLine($"{ShownStats[i]}  <color=#F0D080>{player.Attributes.Get(ShownStats[i]):0.#}</color>");
        }

        Stats.text = builder.ToString();
    }

    private void onSlotClicked(int InIndex)
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        var stack = player.Inventory.Slots[InIndex];
        ItemRow row;
        if (stack.IsEmpty == true || Tables.Root.Item.TryGet(stack.ItemId, out row) == false)
        {
            Tooltip.text = string.Empty;
            return;
        }

        Tooltip.text = $"<b>{Tables.Text(row.NameId)}</b>  ({Tables.Text(EText.GearLevel)} {row.GearLevel:0})\n{Tables.Text(row.DescId)}";
        if (row.EquipSlot != EEquipSlot.None)
        {
            player.Equipment.EquipFromInventory(InIndex);
            return;
        }

        if (row.Type == EItemType.Consumable)
        {
            for (int i = 0; i < row.Effects.Length; ++i)
            {
                player.ApplyEffect(row.Effects[i], player, 0);
            }

            player.Inventory.Remove(row.Index, 1);
        }
    }

    private void onEquipClicked(EEquipSlot InSlot)
    {
        var player = PlayerCharacter.Local;
        if (player != null)
        {
            player.Equipment.Unequip(InSlot);
        }
    }
}
