using Framework;
using Framework.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지도 한 장 (미니맵 · 월드맵 공용). 레이아웃은 프리팹(UIPrefabBuilder.Map), 이 컴포넌트는 매 프레임 위치만 맞춘다.
///  - 지도 그림: 에디터에서 위에서 내려다본 월드를 구운 텍스처 (Game > World > Bake World Map → Content/UI/Map/WorldMap.png)
///  - 월드 범위: SceneContext.Terrain 의 위치 · 크기 (지형 = 월드)
///  - ViewRadius 가 0 보다 크면 플레이어 주변 반경만 (미니맵), 0 이면 월드 전체를 맞춰 넣는다 (월드맵)
///  - RotateWithCamera: 카메라가 보는 방향이 위가 되도록 지도를 돌린다 (쿼터뷰 카메라가 45° 돌아 있어서 미니맵은 화면과 방향을 맞춘다)
/// 표식 종류별 스프라이트 · 색 · 크기는 EMapMarker 순서의 배열 (프리팹에서 바꾼다).
/// </summary>
public class MapView : Framework.UIBehaviour
{
    [Header("Layout")]
    /// <summary>회전 · 이동되는 지도 그림의 부모 (Viewport 가운데 기준).</summary>
    public RectTransform Content;
    public RawImage Map;
    /// <summary>표식 · 지역 이름의 부모 (회전하지 않는다).</summary>
    public RectTransform MarkerRoot;
    /// <summary>표식 복제 원본 (비활성 자식).</summary>
    public Image MarkerTemplate;
    /// <summary>지역 이름 복제 원본 (비활성 자식). 없으면 이름을 그리지 않는다.</summary>
    public TextMeshProUGUI LabelTemplate;

    [Header("View")]
    public float ViewRadius;
    public bool RotateWithCamera;
    public bool ShowMonsters;
    public bool ShowLabels;
    /// <summary>화면 밖 퀘스트 표식을 가장자리에 붙여 보여 준다 (미니맵). 붙은 동안은 대상 쪽을 가리키는 화살표가 된다.</summary>
    public bool ClampQuestToEdge;

    [Header("Quest Guide")]
    /// <summary>플레이어 → 퀘스트 장소를 잇는 선 (Image, MarkerRoot 자식). 없으면 선을 그리지 않는다.</summary>
    public Image GuideLine;
    public float GuideLineWidth = 3.0f;
    /// <summary>가장자리에 붙은 퀘스트 표식에 쓰는 화살표 (위를 가리키는 스프라이트).</summary>
    public Sprite GuideArrow;

    [Header("Markers (EMapMarker 순서)")]
    public Sprite[] MarkerSprites = new Sprite[(int)EMapMarker.Count];
    public Color[] MarkerColors = new Color[(int)EMapMarker.Count];
    public float[] MarkerSizes = new float[(int)EMapMarker.Count];

    /// <summary>지역 이름을 지역 위치보다 조금 아래에 써서 그 자리의 표식(보스 · 플레이어)을 가리지 않게 한다.</summary>
    private const float LabelOffsetY = -18.0f;

    private UIItemList<Image> Markers;
    private UIItemList<TextMeshProUGUI> Labels;
    private Vector2 WorldOrigin;
    private Vector2 WorldSize;
    private bool HasWorld;
    private float Scale;
    private Vector2 Center;
    private float Rotation;

    protected override void OnUnityAwake()
    {
        base.OnUnityAwake();
        Markers = new UIItemList<Image>(MarkerTemplate, MarkerRoot);
        if (LabelTemplate != null)
        {
            Labels = new UIItemList<TextMeshProUGUI>(LabelTemplate, MarkerRoot);
        }
    }

    /// <summary>플레이어 · 카메라 위치에 맞춰 지도와 표식을 다시 놓는다 (창의 Update 에서 매 프레임).</summary>
    public void Refresh()
    {
        if (resolveWorld() == false)
        {
            return;
        }

        var player = PlayerCharacter.Local;
        Vector2 size = Rect.rect.size;
        if (ViewRadius > 0.0f)
        {
            Scale = Mathf.Min(size.x, size.y) * 0.5f / ViewRadius;
            Center = player != null ? toXZ(player.transform.position) : WorldOrigin + WorldSize * 0.5f;
        }
        else
        {
            Scale = Mathf.Min(size.x / WorldSize.x, size.y / WorldSize.y);
            Center = WorldOrigin + WorldSize * 0.5f;
        }

        var context = SceneContext.Current;
        Rotation = RotateWithCamera == true && context != null && context.Camera != null ? context.Camera.YawDegrees : 0.0f;

        Map.rectTransform.sizeDelta = WorldSize * Scale;
        Map.rectTransform.anchoredPosition = (WorldOrigin + WorldSize * 0.5f - Center) * Scale;
        Content.localRotation = Quaternion.Euler(0.0f, 0.0f, Rotation);

        Markers.Clear();
        addActors(player);
        addFixedMarkers();
        addQuest(player);
        if (player != null)
        {
            // 플레이어 화살표는 맨 위에 그린다 (Add 순서 = 형제 순서).
            var arrow = place(EMapMarker.Player, player.transform.position, false);
            if (arrow != null)
            {
                arrow.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, Rotation - player.transform.eulerAngles.y);
            }
        }

        refreshLabels();
    }

    /// <summary>월드 좌표 → 이 지도(MarkerRoot) 의 로컬 좌표.</summary>
    public Vector2 WorldToMap(Vector3 InWorld)
    {
        Vector2 offset = (toXZ(InWorld) - Center) * Scale;
        return Quaternion.Euler(0.0f, 0.0f, Rotation) * offset;
    }

    // ------------------------------------------------------------------ markers

    private void addActors(PlayerCharacter InPlayer)
    {
        var registry = Managers.Get<ActorRegistry>();
        if (registry == null)
        {
            return;
        }

        var actors = registry.All;
        for (int i = 0; i < actors.Count; ++i)
        {
            var actor = actors[i];
            if (actor == null || actor.IsDead == true || actor.IsPlayer == true || actor.Team == ETeam.Neutral)
            {
                continue;
            }

            bool isBoss = actor.Row != null && actor.Row.IsVBlood == true;
            if (isBoss == false && ShowMonsters == false)
            {
                continue;
            }

            place(isBoss == true ? EMapMarker.Boss : EMapMarker.Monster, actor.transform.position, false);
        }
    }

    private void addFixedMarkers()
    {
        var entries = MapMarkers.All;
        for (int i = 0; i < entries.Count; ++i)
        {
            if (entries[i].Target != null)
            {
                place(entries[i].Kind, entries[i].Target.position, false);
            }
        }
    }

    /// <summary>퀘스트 가이드: 갈 곳 표식 + 플레이어에서 그곳까지 선. 미니맵 밖이면 가장자리 화살표.</summary>
    private void addQuest(PlayerCharacter InPlayer)
    {
        var quests = Managers.Get<QuestManager>();
        Vector3 position = Vector3.zero;
        bool guided = InPlayer != null && quests != null && quests.TryGetGuidePosition(InPlayer.transform.position, out position) == true;
        if (guided == false)
        {
            setGuideLine(false, Vector2.zero, Vector2.zero);
            return;
        }

        Vector2 from = WorldToMap(InPlayer.transform.position);
        Vector2 to = WorldToMap(position);
        var marker = place(EMapMarker.Quest, position, ClampQuestToEdge);
        bool clamped = marker != null && (marker.rectTransform.anchoredPosition - to).sqrMagnitude > 1.0f;
        if (clamped == true && GuideArrow != null)
        {
            Vector2 direction = to - from;
            marker.sprite = GuideArrow;
            marker.rectTransform.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90.0f);
        }

        setGuideLine(true, from, marker != null ? marker.rectTransform.anchoredPosition : to);
    }

    private void setGuideLine(bool InShow, Vector2 InFrom, Vector2 InTo)
    {
        if (GuideLine == null)
        {
            return;
        }

        Vector2 delta = InTo - InFrom;
        bool show = InShow == true && delta.sqrMagnitude > 4.0f;
        if (GuideLine.gameObject.activeSelf != show)
        {
            GuideLine.gameObject.SetActive(show);
        }

        if (show == false)
        {
            return;
        }

        var rect = GuideLine.rectTransform;
        rect.anchoredPosition = (InFrom + InTo) * 0.5f;
        rect.sizeDelta = new Vector2(delta.magnitude, GuideLineWidth);
        rect.localRotation = Quaternion.Euler(0.0f, 0.0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        // 표식 · 이름보다 아래.
        rect.SetAsFirstSibling();
    }

    /// <summary>표식 하나를 놓는다. 지도 밖이면 InClamp 가 아니면 그리지 않는다.</summary>
    private Image place(EMapMarker InKind, Vector3 InWorld, bool InClamp)
    {
        int kind = (int)InKind;
        Vector2 local = WorldToMap(InWorld);
        Vector2 half = Rect.rect.size * 0.5f;
        float margin = MarkerSizes[kind] * 0.5f;
        bool inside = Mathf.Abs(local.x) <= half.x - margin && Mathf.Abs(local.y) <= half.y - margin;
        if (inside == false)
        {
            if (InClamp == false)
            {
                return null;
            }

            local.x = Mathf.Clamp(local.x, -half.x + margin, half.x - margin);
            local.y = Mathf.Clamp(local.y, -half.y + margin, half.y - margin);
        }

        var marker = Markers.Add();
        marker.sprite = MarkerSprites[kind];
        marker.color = MarkerColors[kind];
        marker.rectTransform.sizeDelta = new Vector2(MarkerSizes[kind], MarkerSizes[kind]);
        marker.rectTransform.anchoredPosition = local;
        marker.rectTransform.localRotation = Quaternion.identity;
        return marker;
    }

    private void refreshLabels()
    {
        if (Labels == null)
        {
            return;
        }

        if (ShowLabels == false || Tables.Root.MapLabel == null)
        {
            Labels.Clear();
            return;
        }

        var rows = Tables.Root.MapLabel.All;
        Labels.SetCount(rows.Count);
        for (int i = 0; i < rows.Count; ++i)
        {
            var label = Labels[i];
            label.text = Tables.Text(rows[i].NameId);
            label.rectTransform.anchoredPosition = WorldToMap(new Vector3(rows[i].Position.x, 0.0f, rows[i].Position.y)) + new Vector2(0.0f, LabelOffsetY);
            // 표식보다 아래에 그린다 (표식은 Add 때 맨 뒤 형제로 간다).
            label.rectTransform.SetAsFirstSibling();
        }
    }

    // ------------------------------------------------------------------ world

    private bool resolveWorld()
    {
        if (HasWorld == true)
        {
            return true;
        }

        var context = SceneContext.Current;
        if (context == null || context.Terrain == null || context.Terrain.terrainData == null)
        {
            return false;
        }

        Vector3 origin = context.Terrain.transform.position;
        Vector3 size = context.Terrain.terrainData.size;
        WorldOrigin = new Vector2(origin.x, origin.z);
        WorldSize = new Vector2(size.x, size.z);
        HasWorld = true;
        return true;
    }

    private static Vector2 toXZ(Vector3 InWorld)
    {
        return new Vector2(InWorld.x, InWorld.z);
    }
}
