using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>드래그 앤 드롭을 처리하는 창 (InventoryWindow). 칸은 이벤트만 넘기고 판단은 창 · 바인딩이 한다.</summary>
public interface IItemSlotDragHost
{
    void OnSlotBeginDrag(ItemSlotView InSlot, PointerEventData InEvent);
    void OnSlotDrag(ItemSlotView InSlot, PointerEventData InEvent);
    void OnSlotEndDrag(ItemSlotView InSlot, PointerEventData InEvent);
    void OnSlotDrop(ItemSlotView InTarget, PointerEventData InEvent);
}

/// <summary>
/// 아이템 슬롯 1칸 (아이콘 + 개수 + 버튼). Binding 이 이 칸이 무엇(인벤토리 칸 / 장비 부위)인지 알고, Host 가 드래그 앤 드롭을 처리한다.
/// </summary>
public class ItemSlotView : Framework.UIBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public Image Icon;
    public TextMeshProUGUI Count;
    public Button Button;
    public ItemStack Stack { get; private set; }

    /// <summary>이 칸이 가리키는 것 (InventorySlotBinding / EquipSlotBinding).</summary>
    public ItemSlotBinding Binding { get; set; }

    /// <summary>드래그 앤 드롭을 받을 창. 없으면 드래그하지 않는다.</summary>
    public IItemSlotDragHost Host { get; set; }

    public void Set(ItemStack InStack)
    {
        Stack = InStack;
        if (InStack.IsEmpty == true)
        {
            Icon.enabled = false;
            Count.text = string.Empty;
            return;
        }

        ItemRow row;
        if (Tables.Root.Item.TryGet(InStack.ItemId, out row) == false)
        {
            Icon.enabled = false;
            return;
        }

        var sprite = UIUtil.LoadSprite(row.Icon);
        Icon.enabled = true;
        Icon.sprite = sprite;
        Icon.preserveAspect = true;
        Icon.color = sprite != null ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1.0f);
        Count.text = InStack.Count > 1 ? InStack.Count.ToString() : string.Empty;
    }

    /// <summary>끌고 있는 동안 원래 칸 아이콘을 흐리게.</summary>
    public void SetDragging(bool InDragging)
    {
        var color = Icon.color;
        color.a = InDragging == true ? 0.35f : 1.0f;
        Icon.color = color;
    }

    public void OnBeginDrag(PointerEventData InEvent)
    {
        if (Host != null && Stack.IsEmpty == false)
        {
            Host.OnSlotBeginDrag(this, InEvent);
        }
    }

    public void OnDrag(PointerEventData InEvent)
    {
        if (Host != null)
        {
            Host.OnSlotDrag(this, InEvent);
        }
    }

    public void OnEndDrag(PointerEventData InEvent)
    {
        if (Host != null)
        {
            Host.OnSlotEndDrag(this, InEvent);
        }
    }

    public void OnDrop(PointerEventData InEvent)
    {
        if (Host != null)
        {
            Host.OnSlotDrop(this, InEvent);
        }
    }
}
