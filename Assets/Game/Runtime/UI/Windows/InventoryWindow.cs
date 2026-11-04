using System.Collections.Generic;
using System.Text;
using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 + 장비 + 능력치 창.
///  - 클릭: 칸의 바인딩이 처리 (인벤토리 칸 = 장착 / 소비 아이템 사용, 장비 칸 = 벗기)
///  - 드래그 앤 드롭 (2026-11-01): 칸을 끌면 아이콘(DragIcon)이 마우스를 따라오고, 다른 칸에 놓으면 끌어온 칸.DropOnto(놓은 칸) —
///    인벤토리 칸끼리 옮기기 · 합치기 · 바꾸기, 인벤토리 → 장비 칸 장착(그 부위 장비만), 장비 → 인벤토리 칸 벗기. 규칙은 ItemSlotBindings.cs.
/// </summary>
public class InventoryWindow : UIWindow, IItemSlotDragHost
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
    /// <summary>끄는 동안 마우스를 따라오는 아이콘 (레이캐스트를 막지 않음).</summary>
    public Image DragIcon;

    private readonly List<ItemSlotView> Slots = new List<ItemSlotView>();
    private readonly List<ItemSlotView> EquipSlots = new List<ItemSlotView>();
    private ItemSlotView DragSource;

    /// <summary>칸 수는 ConstTable.InventorySize 라 프리팹에 고정할 수 없다 → 슬롯 프리팹을 그 수만큼 띄운다.</summary>
    protected override void OnInitialize()
    {
        var grid = new UIItemList<ItemSlotView>(SlotPrefab, Grid);
        int size = Tables.ConstInt(EConst.InventorySize);
        for (int i = 0; i < size; ++i)
        {
            Slots.Add(addSlot(grid, $"Slot{i}", new InventorySlotBinding(i)));
        }

        var equip = new UIItemList<ItemSlotView>(SlotPrefab, EquipRoot);
        for (int i = 0; i < (int)EEquipSlot.Count; ++i)
        {
            EquipSlots.Add(addSlot(equip, $"Equip{i}", new EquipSlotBinding((EEquipSlot)i)));
        }

        DragIcon.raycastTarget = false;
        DragIcon.gameObject.SetActive(false);
    }

    private ItemSlotView addSlot(UIItemList<ItemSlotView> InList, string InName, ItemSlotBinding InBinding)
    {
        var slot = InList.Add();
        slot.name = InName;
        slot.Binding = InBinding;
        slot.Host = this;
        slot.Button.onClick.AddListener(() => onSlotClicked(slot));
        slot.Set(new ItemStack());
        return slot;
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
        cancelDrag();
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

        for (int i = 0; i < Slots.Count; ++i)
        {
            Slots[i].Set(Slots[i].Binding.GetStack(player));
        }

        for (int i = 0; i < EquipSlots.Count; ++i)
        {
            EquipSlots[i].Set(EquipSlots[i].Binding.GetStack(player));
        }

        var builder = new StringBuilder();
        for (int i = 0; i < ShownStats.Length; ++i)
        {
            builder.AppendLine($"{Tables.Text(UIUtil.AttributeTextId(ShownStats[i]))}  <color=#F0D080>{player.Attributes.Get(ShownStats[i]):0.#}</color>");
        }

        Stats.text = builder.ToString();
    }

    private void onSlotClicked(ItemSlotView InSlot)
    {
        var player = PlayerCharacter.Local;
        if (player == null)
        {
            return;
        }

        showTooltip(InSlot.Stack);
        InSlot.Binding.Activate(player);
    }

    private void showTooltip(ItemStack InStack)
    {
        ItemRow row;
        if (InStack.IsEmpty == true || Tables.Root.Item.TryGet(InStack.ItemId, out row) == false)
        {
            Tooltip.text = string.Empty;
            return;
        }

        Tooltip.text = $"<b>{Tables.Text(row.NameId)}</b>  ({Tables.Text(EText.GearLevel)} {row.GearLevel:0})\n{Tables.Text(row.DescId)}";
    }

    // ------------------------------------------------------------------ drag & drop

    void IItemSlotDragHost.OnSlotBeginDrag(ItemSlotView InSlot, PointerEventData InEvent)
    {
        DragSource = InSlot;
        InSlot.SetDragging(true);
        DragIcon.sprite = InSlot.Icon.sprite;
        DragIcon.color = InSlot.Icon.sprite != null ? new Color(1.0f, 1.0f, 1.0f, 0.85f) : new Color(0.6f, 0.6f, 0.6f, 0.85f);
        DragIcon.gameObject.SetActive(true);
        DragIcon.transform.SetAsLastSibling();
        followPointer(InEvent);
        showTooltip(InSlot.Stack);
    }

    void IItemSlotDragHost.OnSlotDrag(ItemSlotView InSlot, PointerEventData InEvent)
    {
        if (DragSource == InSlot)
        {
            followPointer(InEvent);
        }
    }

    /// <summary>OnDrop(놓은 칸) 다음에 불린다 — 칸 밖에 놓았으면 아무 일도 없이 끝난다.</summary>
    void IItemSlotDragHost.OnSlotEndDrag(ItemSlotView InSlot, PointerEventData InEvent)
    {
        cancelDrag();
    }

    void IItemSlotDragHost.OnSlotDrop(ItemSlotView InTarget, PointerEventData InEvent)
    {
        var player = PlayerCharacter.Local;
        if (DragSource == null || DragSource == InTarget || player == null)
        {
            return;
        }

        DragSource.Binding.DropOnto(InTarget.Binding, player);
    }

    private void followPointer(PointerEventData InEvent)
    {
        Vector2 local;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, InEvent.position, InEvent.pressEventCamera, out local) == true)
        {
            DragIcon.rectTransform.anchoredPosition = local;
        }
    }

    private void cancelDrag()
    {
        if (DragSource != null)
        {
            DragSource.SetDragging(false);
            DragSource = null;
        }

        if (DragIcon != null)
        {
            DragIcon.gameObject.SetActive(false);
        }
    }
}
