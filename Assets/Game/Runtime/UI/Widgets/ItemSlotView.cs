using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>아이템 슬롯 1칸 (아이콘 + 개수 + 버튼).</summary>
public class ItemSlotView : MonoBehaviour
{
    public Image Icon;
    public TextMeshProUGUI Count;
    public Button Button;
    public ItemStack Stack { get; private set; }

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
}
