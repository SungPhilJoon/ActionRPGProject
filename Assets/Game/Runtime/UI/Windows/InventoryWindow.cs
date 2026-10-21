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

    private readonly List<ItemSlotView> Slots = new List<ItemSlotView>();
    private readonly List<ItemSlotView> EquipSlots = new List<ItemSlotView>();
    private TextMeshProUGUI Stats;
    private TextMeshProUGUI Tooltip;

    protected override void Build()
    {
        var frame = UIFactory.CreateWindowFrame(Rect, "Frame");
        UIFactory.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 20.0f), new Vector2(1100.0f, 640.0f));

        var title = UIFactory.CreateText(frame, "Title", Tables.Text(EText.Inventory), 30.0f, TextAlignmentOptions.Left);
        UIFactory.Place(title, new Vector2(0.0f, 1.0f), new Vector2(24.0f, -12.0f), new Vector2(400.0f, 44.0f));

        var close = UIFactory.CreateButton(frame, "Close", Tables.Text(EText.Close), 18.0f, Close);
        UIFactory.Place(close, new Vector2(1.0f, 1.0f), new Vector2(-16.0f, -14.0f), new Vector2(110.0f, 38.0f));

        var grid = UIFactory.CreateRect(frame, "Grid");
        UIFactory.Place(grid, new Vector2(0.0f, 1.0f), new Vector2(16.0f, -64.0f), new Vector2(560.0f, 460.0f));
        UIFactory.AddGrid(grid, new Vector2(80.0f, 80.0f), new Vector2(6.0f, 6.0f), 6);

        int size = Tables.ConstInt(EConst.InventorySize);
        for (int i = 0; i < size; ++i)
        {
            int index = i;
            var slot = UIUtil.CreateItemSlot(grid, $"Slot{i}");
            slot.Button.onClick.AddListener(() => onSlotClicked(index));
            Slots.Add(slot);
        }

        var equipTitle = UIFactory.CreateText(frame, "EquipTitle", Tables.Text(EText.Equipment), 22.0f, TextAlignmentOptions.Left);
        UIFactory.Place(equipTitle, new Vector2(0.0f, 1.0f), new Vector2(600.0f, -64.0f), new Vector2(200.0f, 30.0f));

        var equipRoot = UIFactory.CreateRect(frame, "Equip");
        UIFactory.Place(equipRoot, new Vector2(0.0f, 1.0f), new Vector2(592.0f, -96.0f), new Vector2(110.0f, 460.0f));
        UIFactory.AddGrid(equipRoot, new Vector2(80.0f, 80.0f), new Vector2(6.0f, 6.0f), 1);
        for (int i = 0; i < (int)EEquipSlot.Count; ++i)
        {
            var slotType = (EEquipSlot)i;
            var slot = UIUtil.CreateItemSlot(equipRoot, $"Equip{i}");
            slot.Button.onClick.AddListener(() => onEquipClicked(slotType));
            EquipSlots.Add(slot);
        }

        Stats = UIFactory.CreateText(frame, "Stats", string.Empty, 17.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(Stats, new Vector2(0.0f, 1.0f), new Vector2(720.0f, -64.0f), new Vector2(360.0f, 470.0f));

        Tooltip = UIFactory.CreateText(frame, "Tooltip", string.Empty, 18.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(Tooltip, new Vector2(0.0f, 0.0f), new Vector2(24.0f, 16.0f), new Vector2(1050.0f, 90.0f));
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
