using Framework.Editor;
using Framework.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI 프리팹 빌더 (Game > Setup > 3b. Build UI Prefabs).
/// 예전에는 각 창이 런타임 Build() 에서 UIFactory 로 자식을 만들고 필드에 연결했다. 이제 그 레이아웃을 여기서 한 번 조립해
/// 프리팹(Content/UI, Addressables 주소 = UI/...)으로 저장하고, 자식 참조는 프리팹에 직렬화한다. 런타임은 로드 → Instantiate 만 한다.
///  - 스킨: UISkinSetup.GetOrCreate() 의 스프라이트 · 틴트 (UIFactory.Skin)
///  - 고정 문구: UIText(TextId) — 인스펙터에서 TextId 만 바꾸면 된다
///  - 버튼: 영구 리스너 (인스펙터 OnClick 에 보임)
/// 이미 있는 프리팹은 덮어쓴다 (프리팹을 손으로 고쳤다면 다시 굽기 전에 코드에도 반영할 것 — UI.md 3절).
/// </summary>
public static partial class UIPrefabBuilder
{
    public const string Folder = EditorPaths.Content + "/UI";

    [MenuItem("Game/Setup/3b. Build UI Prefabs", priority = 3)]
    public static void BuildAll()
    {
        EditorAssetUtil.EnsureFolder(Folder + "/Windows");
        EditorAssetUtil.EnsureFolder(Folder + "/Widgets");
        EditorAssetUtil.EnsureFolder(Folder + "/Layers");

        bool bound = Tables.IsReady;
        if (bound == false)
        {
            Tables.Bind(AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset));
        }

        UIFactory.Skin = UISkinSetup.GetOrCreate();
        try
        {
            buildCanvas();
            var row = buildListRow();
            var slot = buildItemSlot();
            var healthBar = buildWorldHealthBar();
            var number = buildDamageNumber();

            buildWorldHealthBarLayer(healthBar);
            buildDamageNumberLayer(number);
            buildHud();
            buildInventory(slot);
            buildList<CraftWindow>(row);
            buildList<BuildWindow>(row);
            buildList<ShopWindow>(row);
            buildBalanceOverlay();
            var markers = buildMapMarkerSprites();
            buildMinimap(markers);
            buildWorldMap(markers);
            buildCheatConsole();
            buildTitle();
        }
        finally
        {
            UIFactory.Skin = null;
            if (bound == false)
            {
                Tables.Bind(null);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[UI] prefabs built: " + Folder);
    }

    // ------------------------------------------------------------------ helpers

    private static T save<T>(RectTransform InRoot, string InRelativePath) where T : Component
    {
        string path = $"{Folder}/{InRelativePath}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(InRoot.gameObject, path);
        Object.DestroyImmediate(InRoot.gameObject);
        return prefab.GetComponent<T>();
    }

    private static T createWindow<T>(out RectTransform OutRoot) where T : Component
    {
        OutRoot = UIFactory.CreateRoot(typeof(T).Name);
        return OutRoot.gameObject.AddComponent<T>();
    }

    /// <summary>StringTable 고정 문구 텍스트 (UIText 부착, 미리보기 문구 = 현재 테이블 값).</summary>
    private static TextMeshProUGUI label(Transform InParent, string InName, EText InText, float InSize, TextAlignmentOptions InAlign)
    {
        var text = UIFactory.CreateText(InParent, InName, Tables.Text(InText), InSize, InAlign);
        text.gameObject.AddComponent<UIText>().TextId = (int)InText;
        return text;
    }

    private static Button button(Transform InParent, string InName, EText InText, float InSize, UnityEngine.Events.UnityAction InOnClick)
    {
        var result = UIFactory.CreateButton(InParent, InName, Tables.Text(InText), InSize, InOnClick);
        var text = result.GetComponentInChildren<TextMeshProUGUI>();
        text.gameObject.AddComponent<UIText>().TextId = (int)InText;
        return result;
    }

    private static TextMeshProUGUI title(Transform InParent, EText InText, float InWidth)
    {
        var text = label(InParent, "Title", InText, 30.0f, TextAlignmentOptions.Left);
        text.color = UIFactory.Skin != null ? UIFactory.Skin.TitleColor : Color.white;
        text.fontStyle = FontStyles.Bold;
        UIFactory.Place(text, new Vector2(0.0f, 1.0f), new Vector2(28.0f, -16.0f), new Vector2(InWidth, 44.0f));
        return text;
    }

    private static Button closeButton(Transform InFrame, UIWindow InWindow)
    {
        var close = button(InFrame, "Close", EText.Close, 18.0f, InWindow.Close);
        UIFactory.Place(close, new Vector2(1.0f, 1.0f), new Vector2(-18.0f, -16.0f), new Vector2(120.0f, 40.0f));
        return close;
    }

    // ------------------------------------------------------------------ canvas / widgets

    private static void buildCanvas()
    {
        var canvas = UIFactory.CreateCanvas("LayerCanvas", 0);
        save<Canvas>(canvas.GetComponent<RectTransform>(), "LayerCanvas");
    }

    private static ListRowView buildListRow()
    {
        var root = UIFactory.CreateInset(null, "ListRow", UIUtil.SlotColor);
        root.sizeDelta = new Vector2(820.0f, 84.0f);
        UIFactory.SetPreferred(root, 0.0f, 84.0f);
        var view = root.gameObject.AddComponent<ListRowView>();

        var iconBack = UIFactory.CreateSlot(root, "IconBack", UIUtil.SlotColor);
        UIFactory.Place(iconBack, new Vector2(0.0f, 0.5f), new Vector2(10.0f, 0.0f), new Vector2(70.0f, 70.0f));
        view.Icon = UIFactory.CreateImage(iconBack, "Icon", Color.white);
        UIFactory.Stretch(view.Icon.rectTransform, 8.0f);

        view.Title = UIFactory.CreateText(root, "Title", "Title", 22.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(view.Title, new Vector2(0.0f, 1.0f), new Vector2(94.0f, -8.0f), new Vector2(560.0f, 30.0f));
        view.Detail = UIFactory.CreateText(root, "Detail", "Detail", 16.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(view.Detail, new Vector2(0.0f, 1.0f), new Vector2(94.0f, -40.0f), new Vector2(560.0f, 42.0f));

        view.Button = UIFactory.CreateButton(root, "Action", "Action", 18.0f, null);
        UIFactory.Place(view.Button, new Vector2(1.0f, 0.5f), new Vector2(-12.0f, 0.0f), new Vector2(136.0f, 48.0f));
        view.ButtonLabel = view.Button.GetComponentInChildren<TextMeshProUGUI>();
        return save<ListRowView>(root, "Widgets/ListRow");
    }

    private static ItemSlotView buildItemSlot()
    {
        var root = UIFactory.CreateSlot(null, "ItemSlot", UIUtil.SlotColor);
        root.sizeDelta = new Vector2(80.0f, 80.0f);
        var slot = root.gameObject.AddComponent<ItemSlotView>();
        slot.Icon = UIFactory.CreateImage(root, "Icon", Color.white);
        UIFactory.Stretch(slot.Icon.rectTransform, 9.0f);
        slot.Icon.enabled = false;
        slot.Count = UIFactory.CreateText(root, "Count", string.Empty, 16.0f, TextAlignmentOptions.BottomRight);
        UIFactory.Stretch(slot.Count.rectTransform, 6.0f);
        slot.Button = root.gameObject.AddComponent<Button>();
        slot.Button.targetGraphic = root.GetComponent<Image>();
        return save<ItemSlotView>(root, "Widgets/ItemSlot");
    }

    private static WorldHealthBarView buildWorldHealthBar()
    {
        var root = UIFactory.CreateRect(null, "WorldHealthBar");
        root.sizeDelta = new Vector2(120.0f, 16.0f);
        var view = root.gameObject.AddComponent<WorldHealthBarView>();
        view.Bar = UIFactory.CreateBar(root, "Bar", new Color(0.8f, 0.12f, 0.12f, 1.0f), UIUtil.BarBackColor);
        UIFactory.Stretch(view.Bar.Rect, 0.0f);
        view.Bar.Label.text = string.Empty;
        foreach (var graphic in view.Bar.GetComponentsInChildren<Graphic>())
        {
            graphic.raycastTarget = false;
        }

        // 레벨 배지: 바 왼쪽 어두운 칸 + 숫자.
        var badge = UIFactory.CreateSlot(root, "LevelBadge", new Color(0.0f, 0.0f, 0.0f, 0.75f));
        badge.anchorMin = new Vector2(0.0f, 0.5f);
        badge.anchorMax = new Vector2(0.0f, 0.5f);
        badge.pivot = new Vector2(1.0f, 0.5f);
        badge.anchoredPosition = new Vector2(-2.0f, 0.0f);
        badge.sizeDelta = new Vector2(30.0f, 24.0f);
        badge.GetComponent<Image>().raycastTarget = false;
        view.Level = UIFactory.CreateText(badge, "Level", "1", 18.0f, TextAlignmentOptions.Center);
        UIFactory.Stretch(view.Level.rectTransform, 0.0f);
        view.Level.textWrappingMode = TextWrappingModes.NoWrap;
        view.Level.fontStyle = FontStyles.Bold;
        return save<WorldHealthBarView>(root, "Widgets/WorldHealthBar");
    }

    private static TextMeshProUGUI buildDamageNumber()
    {
        var text = UIFactory.CreateText(null, "DamageNumber", "123", 26.0f, TextAlignmentOptions.Center);
        text.fontStyle = FontStyles.Bold;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.sizeDelta = new Vector2(200.0f, 50.0f);
        text.outlineWidth = 0.15f;
        text.outlineColor = new Color32(20, 0, 0, 255);
        return save<TextMeshProUGUI>(text.rectTransform, "Widgets/DamageNumber");
    }

    private static void buildWorldHealthBarLayer(WorldHealthBarView InBar)
    {
        var root = UIFactory.CreateRoot("WorldHealthBarLayer");
        root.gameObject.AddComponent<WorldHealthBarLayer>().BarPrefab = InBar;
        save<WorldHealthBarLayer>(root, "Layers/WorldHealthBarLayer");
    }

    private static void buildDamageNumberLayer(TextMeshProUGUI InNumber)
    {
        var root = UIFactory.CreateRoot("DamageNumberLayer");
        root.gameObject.AddComponent<DamageNumberLayer>().NumberPrefab = InNumber;
        save<DamageNumberLayer>(root, "Layers/DamageNumberLayer");
    }

    // ------------------------------------------------------------------ windows

    private static void buildList<T>(ListRowView InRow) where T : ListWindow
    {
        RectTransform root;
        var window = createWindow<T>(out root);
        var frame = UIFactory.CreateWindowFrame(root, "Frame");
        UIFactory.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 20.0f), new Vector2(880.0f, 640.0f));

        window.Title = title(frame, (EText)window.TitleTextId, 600.0f);
        closeButton(frame, window);

        var list = UIFactory.CreateInset(frame, "ListBack", new Color(0.0f, 0.0f, 0.0f, 0.35f));
        UIFactory.Place(list, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 20.0f), new Vector2(840.0f, 548.0f));
        var viewport = UIFactory.CreateRect(list, "Viewport");
        UIFactory.Stretch(viewport, 8.0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        viewport.gameObject.AddComponent<Image>().color = new Color(0.0f, 0.0f, 0.0f, 0.01f);

        var content = UIFactory.CreateRect(viewport, "Content");
        content.anchorMin = new Vector2(0.0f, 1.0f);
        content.anchorMax = new Vector2(1.0f, 1.0f);
        content.pivot = new Vector2(0.5f, 1.0f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;
        UIFactory.AddVertical(content, 6.0f, 4);
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.horizontal = false;
        scroll.viewport = viewport;
        scroll.scrollSensitivity = 30.0f;

        window.Content = content;
        window.RowPrefab = InRow;
        save<T>(root, "Windows/" + typeof(T).Name);
    }

    private static void buildInventory(ItemSlotView InSlot)
    {
        RectTransform root;
        var window = createWindow<InventoryWindow>(out root);
        var frame = UIFactory.CreateWindowFrame(root, "Frame");
        UIFactory.Place(frame, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 20.0f), new Vector2(1120.0f, 660.0f));
        title(frame, EText.Inventory, 400.0f);
        closeButton(frame, window);

        var gridBack = UIFactory.CreateInset(frame, "GridBack", new Color(0.0f, 0.0f, 0.0f, 0.35f));
        UIFactory.Place(gridBack, new Vector2(0.0f, 1.0f), new Vector2(20.0f, -70.0f), new Vector2(570.0f, 470.0f));
        window.Grid = UIFactory.CreateRect(gridBack, "Grid");
        UIFactory.Stretch(window.Grid, 0.0f);
        UIFactory.AddGrid(window.Grid, new Vector2(80.0f, 80.0f), new Vector2(6.0f, 6.0f), 6);

        var equipTitle = label(frame, "EquipTitle", EText.Equipment, 22.0f, TextAlignmentOptions.Left);
        equipTitle.color = UIFactory.Skin != null ? UIFactory.Skin.TitleColor : Color.white;
        UIFactory.Place(equipTitle, new Vector2(0.0f, 1.0f), new Vector2(606.0f, -70.0f), new Vector2(200.0f, 30.0f));
        window.EquipRoot = UIFactory.CreateRect(frame, "Equip");
        UIFactory.Place(window.EquipRoot, new Vector2(0.0f, 1.0f), new Vector2(598.0f, -102.0f), new Vector2(110.0f, 460.0f));
        UIFactory.AddGrid(window.EquipRoot, new Vector2(80.0f, 80.0f), new Vector2(6.0f, 6.0f), 1);

        var statsBack = UIFactory.CreateInset(frame, "StatsBack", new Color(0.0f, 0.0f, 0.0f, 0.35f));
        UIFactory.Place(statsBack, new Vector2(0.0f, 1.0f), new Vector2(724.0f, -70.0f), new Vector2(376.0f, 470.0f));
        window.Stats = UIFactory.CreateText(statsBack, "Stats", string.Empty, 17.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Stretch(window.Stats.rectTransform, 14.0f);

        window.Tooltip = UIFactory.CreateText(frame, "Tooltip", string.Empty, 18.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Place(window.Tooltip, new Vector2(0.0f, 0.0f), new Vector2(28.0f, 18.0f), new Vector2(1060.0f, 92.0f));
        window.SlotPrefab = InSlot;
        save<InventoryWindow>(root, "Windows/InventoryWindow");
    }

    private static void buildBalanceOverlay()
    {
        RectTransform root;
        var window = createWindow<BalanceOverlayWindow>(out root);
        var frame = UIFactory.CreateInset(root, "Frame", new Color(0.0f, 0.0f, 0.0f, 0.65f));
        UIFactory.Place(frame, new Vector2(0.0f, 1.0f), new Vector2(20.0f, -20.0f), new Vector2(560.0f, 640.0f));
        frame.GetComponent<Image>().raycastTarget = false;
        window.Body = UIFactory.CreateText(frame, "Body", string.Empty, 16.0f, TextAlignmentOptions.TopLeft);
        UIFactory.Stretch(window.Body.rectTransform, 14.0f);
        save<BalanceOverlayWindow>(root, "Windows/BalanceOverlayWindow");
    }

    private static void buildCheatConsole()
    {
        RectTransform root;
        var window = createWindow<CheatConsoleWindow>(out root);
        var frame = UIFactory.CreateWindowFrame(root, "Frame");
        UIFactory.Place(frame, new Vector2(0.0f, 1.0f), new Vector2(20.0f, -20.0f), new Vector2(920.0f, 540.0f));
        var heading = label(frame, "Title", EText.CheatTitle, 24.0f, TextAlignmentOptions.Left);
        heading.color = UIFactory.Skin != null ? UIFactory.Skin.TitleColor : Color.white;
        UIFactory.Place(heading, new Vector2(0.0f, 1.0f), new Vector2(22.0f, -14.0f), new Vector2(860.0f, 34.0f));

        window.Output = UIFactory.CreateText(frame, "Output", Tables.Text(EText.CheatHint), 17.0f, TextAlignmentOptions.BottomLeft);
        window.Output.overflowMode = TextOverflowModes.Truncate;
        UIFactory.Place(window.Output, new Vector2(0.0f, 0.0f), new Vector2(22.0f, 72.0f), new Vector2(876.0f, 410.0f));

        window.Input = UIFactory.CreateInputField(frame, "Input", "help", 20.0f);
        UIFactory.Place(window.Input, new Vector2(0.0f, 0.0f), new Vector2(18.0f, 18.0f), new Vector2(884.0f, 46.0f));
        save<CheatConsoleWindow>(root, "Windows/CheatConsoleWindow");
    }

    private static void buildTitle()
    {
        RectTransform root;
        var window = createWindow<TitleWindow>(out root);
        var back = UIFactory.CreatePanel(root, "Back", new Color(0.03f, 0.0f, 0.02f, 0.82f));
        UIFactory.Stretch(back, 0.0f);

        var gameTitle = label(back, "Title", EText.GameTitle, 84.0f, TextAlignmentOptions.Center);
        gameTitle.color = UIFactory.Skin != null ? UIFactory.Skin.AccentColor : Color.red;
        gameTitle.fontStyle = FontStyles.Bold;
        UIFactory.Place(gameTitle, new Vector2(0.5f, 0.5f), new Vector2(0.0f, 230.0f), new Vector2(1400.0f, 120.0f));

        var menu = UIFactory.CreateWindowFrame(back, "Menu");
        UIFactory.Place(menu, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -70.0f), new Vector2(500.0f, 400.0f));

        window.StartButton = button(menu, "Single", EText.SinglePlayer, 28.0f, window.OnStartClicked);
        UIFactory.Place(window.StartButton, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -34.0f), new Vector2(420.0f, 70.0f));
        var multi = button(menu, "Multi", EText.MultiplayerSoon, 22.0f, null);
        multi.interactable = false;
        UIFactory.Place(multi, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -124.0f), new Vector2(420.0f, 60.0f));
        var quit = button(menu, "Quit", EText.Quit, 22.0f, window.OnQuitClicked);
        UIFactory.Place(quit, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -204.0f), new Vector2(420.0f, 60.0f));
        // 언어 전환 (Unity Localization). 문구 "Language: English" / "언어: 한국어" 가 UIText 로 현재 언어를 보여 준다.
        var language = button(menu, "Language", EText.Language, 20.0f, window.OnLanguageClicked);
        UIFactory.Place(language, new Vector2(0.5f, 1.0f), new Vector2(0.0f, -284.0f), new Vector2(420.0f, 56.0f));

        window.Status = UIFactory.CreateText(back, "Status", string.Empty, 22.0f, TextAlignmentOptions.Center);
        UIFactory.Place(window.Status, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -300.0f), new Vector2(800.0f, 40.0f));

        var controls = label(back, "Controls", EText.Controls, 20.0f, TextAlignmentOptions.Center);
        controls.color = new Color(1.0f, 1.0f, 1.0f, 0.7f);
        UIFactory.Place(controls, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 40.0f), new Vector2(1500.0f, 150.0f));
        save<TitleWindow>(root, "Windows/TitleWindow");
    }
}
