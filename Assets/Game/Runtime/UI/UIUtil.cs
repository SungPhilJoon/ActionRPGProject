using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>게임 UI 공통 조립 함수 (아이템 슬롯, 아이콘, 키 라벨 등).</summary>
public static class UIUtil
{
    public static readonly Color SlotColor = new Color(0.12f, 0.1f, 0.12f, 0.95f);
    public static readonly Color HealthColor = new Color(0.75f, 0.1f, 0.12f, 1.0f);
    public static readonly Color ShieldColor = new Color(0.55f, 0.75f, 0.95f, 1.0f);
    public static readonly Color BloodColor = new Color(0.55f, 0.0f, 0.08f, 1.0f);
    public static readonly Color BarBackColor = new Color(0.0f, 0.0f, 0.0f, 0.6f);
    public static readonly Color GoldColor = new Color(0.95f, 0.8f, 0.4f, 1.0f);

    public static Sprite LoadSprite(int InAssetId)
    {
        string address = Tables.Address(InAssetId);
        if (string.IsNullOrEmpty(address) == true)
        {
            return null;
        }

        return Managers.Get<AssetLoader>().LoadSync<Sprite>(address);
    }

    public static string ItemName(int InItemId)
    {
        ItemRow row;
        if (Tables.Root.Item.TryGet(InItemId, out row) == false)
        {
            return string.Empty;
        }

        return Tables.Text(row.NameId);
    }

    /// <summary>"&lt;Keyboard&gt;/q" → "Q". 화면 표시용으로만 쓴다.</summary>
    public static string KeyLabel(EInputAction InAction)
    {
        InputRow row;
        if (Tables.Root.Input.TryGet((int)InAction, out row) == false || string.IsNullOrEmpty(row.Binding1) == true)
        {
            return string.Empty;
        }

        string path = row.Binding1;
        int slash = path.LastIndexOf('/');
        string key = slash >= 0 ? path.Substring(slash + 1) : path;
        return key.Length <= 2 ? key.ToUpperInvariant() : char.ToUpperInvariant(key[0]) + key.Substring(1);
    }

    public static ItemSlotView CreateItemSlot(Transform InParent, string InName)
    {
        var rect = UIFactory.CreatePanel(InParent, InName, SlotColor);
        var slot = rect.gameObject.AddComponent<ItemSlotView>();
        slot.Icon = UIFactory.CreateImage(rect, "Icon", Color.white);
        UIFactory.Stretch(slot.Icon.rectTransform, 6.0f);
        slot.Count = UIFactory.CreateText(rect, "Count", string.Empty, 16.0f, TextAlignmentOptions.BottomRight);
        UIFactory.Stretch(slot.Count.rectTransform, 4.0f);
        slot.Button = rect.gameObject.AddComponent<Button>();
        slot.Button.targetGraphic = rect.GetComponent<Image>();
        slot.Set(new ItemStack());
        return slot;
    }

    public static string CostText(Inventory InInventory, int[] InItems, int[] InCounts)
    {
        var builder = new System.Text.StringBuilder();
        for (int i = 0; i < InItems.Length; ++i)
        {
            int need = InCounts != null && i < InCounts.Length ? InCounts[i] : 1;
            int owned = InInventory != null ? InInventory.Count(InItems[i]) : 0;
            string color = owned >= need ? "#D8D0C0" : "#E05050";
            builder.Append($"<color={color}>{ItemName(InItems[i])} {owned}/{need}</color>  ");
        }

        return builder.ToString();
    }
}

public static class CanvasUtil
{
    /// <summary>스크린 좌표 변환에 넘길 카메라. Overlay 캔버스면 null, Camera/World 캔버스면 캔버스 카메라.</summary>
    public static Camera GetEventCamera(RectTransform InRect)
    {
        var canvas = InRect.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }
}
