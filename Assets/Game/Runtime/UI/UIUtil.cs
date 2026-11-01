using Framework;
using UnityEngine;

/// <summary>게임 UI 공통 함수 (아이콘 로드, 키 라벨, 비용 문구, 색).</summary>
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

    /// <summary>대상 정보창에 보여 줄 상태이상 태그와 문구 (같은 순서).</summary>
    private static readonly ETag[] StatusTags =
    {
        ETag.State_Debuff_Stun_Frozen, ETag.State_Debuff_Stun, ETag.State_Debuff_Slow, ETag.State_Debuff_Ignite, ETag.State_Debuff_Chill,
        ETag.State_Debuff_Static, ETag.State_Debuff_Condemn, ETag.State_Debuff_Weaken, ETag.State_Debuff_Leech, ETag.State_Debuff_Bleed,
        ETag.State_Immaterial, ETag.State_Buff_Guard, ETag.State_Buff_Empower, ETag.State_Buff_Haste,
    };

    private static readonly EText[] StatusTexts =
    {
        EText.StatusFreeze, EText.StatusStun, EText.StatusSlow, EText.StatusIgnite, EText.StatusChill,
        EText.StatusStatic, EText.StatusCondemn, EText.StatusWeaken, EText.StatusLeech, EText.StatusBleed,
        EText.StatusImmaterial, EText.StatusGuard, EText.StatusEmpower, EText.StatusHaste,
    };

    /// <summary>InActor 에 걸린 상태이상 이름을 " · " 로 이어 붙인다 (빙결이면 기절은 생략 — 빙결이 기절의 자식 태그).</summary>
    public static void AppendStatuses(Actor InActor, System.Text.StringBuilder OutText)
    {
        bool frozen = false;
        for (int i = 0; i < StatusTags.Length; ++i)
        {
            var tag = StatusTags[i];
            if (InActor.HasTag((int)tag) == false || (tag == ETag.State_Debuff_Stun && frozen == true))
            {
                continue;
            }

            frozen |= tag == ETag.State_Debuff_Stun_Frozen;
            OutText.Append(OutText.Length > 0 ? "  · " : string.Empty);
            OutText.Append("<color=#F2CC66>").Append(Tables.Text(StatusTexts[i])).Append("</color>");
        }
    }

    /// <summary>능력치 이름 문구 = StringTable 9000 + EAttribute (인벤토리 능력치 목록).</summary>
    public const int AttributeTextBase = 9000;

    public static int AttributeTextId(EAttribute InAttribute)
    {
        return AttributeTextBase + (int)InAttribute;
    }

    public static EText QuestChapterText(EQuestChapter InChapter)
    {
        switch (InChapter)
        {
            case EQuestChapter.Hunt:
                return EText.QuestChapterHunt;
            case EQuestChapter.Boss:
                return EText.QuestChapterBoss;
            default:
                return EText.QuestChapterTutorial;
        }
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
