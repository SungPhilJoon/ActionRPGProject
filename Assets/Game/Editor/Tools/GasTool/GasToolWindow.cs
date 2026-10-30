using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GAS Tool (Game/Tools/GAS Tool). 언리얼 GAS 방식으로 어빌리티와 게임플레이 이펙트를 만든다.
///  [Ability]         새 어빌리티 = AbilityGraphAsset (태그 규칙 · 비용 · 실행 단계) + AbilityTable 행 (Type=Graph, 이름 · 슬롯 · 쿨다운 · AI 거리)
///                    템플릿: 근접/원거리/범위/돌진 공격, 방어(가드), 자기 강화, 빈 어빌리티. 기존 타입 기반 어빌리티는 읽기 전용으로 보여준다.
///  [GameplayEffect]  EffectTable 행 (지속 방식 · 모디파이어 · 부여 태그 · 실행) — 방어 버프/디버프/보호막 등
/// 어빌리티를 몬스터에 붙이고 AI 를 짜는 일은 StateTree Tool 의 몬스터 패널에서 한다. 상세: Documentation/VRising/Tool.md
/// </summary>
public partial class GasToolWindow : EditorWindow
{
    private static readonly string[] Tabs = { "Ability", "GameplayEffect", "Ability Set" };
    private const int SetTab = 2;

    public const string AbilityCsv = EditorPaths.Csv + "/AbilityTable.csv";
    public const string EffectCsv = EditorPaths.Csv + "/EffectTable.csv";
    public const string StringCsv = EditorPaths.Csv + "/StringTable.csv";
    public const string AssetCsv = EditorPaths.Csv + "/AssetTable.csv";
    public const string CharacterCsv = EditorPaths.Csv + "/CharacterTable.csv";
    public const string AbilityAssetFolder = EditorPaths.Content + "/Abilities";
    private const float ListWidth = 270.0f;

    private GameTables TablesAsset;
    [SerializeField] private int Tab;
    [SerializeField] private int SelectedAbilityId;
    [SerializeField] private int SelectedEffectId;
    private Vector2 ListScroll;
    private Vector2 BodyScroll;
    private string Filter = string.Empty;

    // CSV 작업 사본 (저장 전까지 파일에 쓰지 않는다)
    private readonly Dictionary<int, CsvRow> AbilityRows = new Dictionary<int, CsvRow>();
    private readonly Dictionary<int, CsvRow> EffectRows = new Dictionary<int, CsvRow>();
    private readonly Dictionary<int, CsvRow> TextRows = new Dictionary<int, CsvRow>();
    private readonly Dictionary<int, CsvRow> CharacterRows = new Dictionary<int, CsvRow>();

    [MenuItem("Window/Game/GAS Tool")]
    [MenuItem("Game/Tools/GAS Tool", priority = 31)]
    public static void Open()
    {
        GetWindow<GasToolWindow>("GAS Tool");
    }

    /// <summary>다른 툴(StateTree Tool, 몬스터 인스펙터)에서 특정 어빌리티를 열 때.</summary>
    public static void OpenAbility(int InAbilityId)
    {
        var window = GetWindow<GasToolWindow>("GAS Tool");
        window.Tab = 0;
        window.SelectedAbilityId = InAbilityId;
        window.GraphSerialized = null;
    }

    /// <summary>캐릭터의 Ability Set (보유 어빌리티) 을 열 때.</summary>
    public static void OpenAbilitySet(int InCharacterId)
    {
        var window = GetWindow<GasToolWindow>("GAS Tool");
        window.Tab = SetTab;
        window.SelectedCharacterId = InCharacterId;
    }

    private void OnEnable()
    {
        TablesAsset = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        EditorApplication.playModeStateChanged += onPlayModeChanged;
        Undo.undoRedoPerformed += Repaint;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= onPlayModeChanged;
        Undo.undoRedoPerformed -= Repaint;
    }

    private void onPlayModeChanged(PlayModeStateChange InChange)
    {
        if (InChange == PlayModeStateChange.ExitingEditMode && hasUnsaved() == true)
        {
            if (EditorUtility.DisplayDialog("GAS Tool", "저장하지 않은 CSV 변경이 있습니다. Play 전에 저장할까요? (저장하지 않으면 사라집니다)", "저장", "버리기") == true)
            {
                save();
            }
        }
    }

    private void OnGUI()
    {
        if (TablesAsset == null)
        {
            EditorGUILayout.HelpBox($"GameTables 에셋이 없습니다: {EditorPaths.GameTablesAsset}", MessageType.Error);
            return;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        int tab = GUILayout.Toolbar(Tab, Tabs, EditorStyles.toolbarButton, GUILayout.Width(360.0f));
        if (tab != Tab)
        {
            Tab = tab;
            GUI.FocusControl(null);
        }

        if (Tab == 0 && GUILayout.Button("+ 새 Ability", EditorStyles.toolbarButton, GUILayout.Width(90.0f)) == true)
        {
            CreateAbilityWindow.Open(this);
        }

        GUILayout.FlexibleSpace();
        Filter = EditorGUILayout.TextField(Filter, EditorStyles.toolbarSearchField, GUILayout.Width(180.0f));
        if (GUILayout.Button(hasUnsaved() == true ? "저장 *" : "저장", EditorStyles.toolbarButton, GUILayout.Width(60.0f)) == true)
        {
            save();
        }

        if (GUILayout.Button("되돌리기", EditorStyles.toolbarButton, GUILayout.Width(60.0f)) == true)
        {
            discard();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUIUtility.labelWidth = 190.0f;
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(ListWidth));
        ListScroll = EditorGUILayout.BeginScrollView(ListScroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, GUIStyle.none);
        switch (Tab)
        {
            case 0:
                drawAbilityList();
                break;
            case 1:
                drawEffectList();
                break;
            default:
                drawSetList();
                break;
        }

        EditorGUILayout.EndScrollView();
        if (Tab == 0)
        {
            drawCreateAbility();
        }
        else if (Tab == 1)
        {
            drawCreateEffect();
        }

        EditorGUILayout.EndVertical();

        BodyScroll = EditorGUILayout.BeginScrollView(BodyScroll);
        switch (Tab)
        {
            case 0:
                drawAbilityEditor();
                break;
            case 1:
                drawEffectEditor();
                break;
            default:
                drawSetEditor();
                break;
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndHorizontal();
        EditorGUIUtility.labelWidth = 0.0f;
    }

    // ------------------------------------------------------------------ csv cache

    public CsvRow AbilityRow(int InId)
    {
        return cachedRow(AbilityRows, AbilityCsv, InId);
    }

    public CsvRow EffectRow(int InId)
    {
        return cachedRow(EffectRows, EffectCsv, InId);
    }

    public CsvRow TextRow(int InId)
    {
        return cachedRow(TextRows, StringCsv, InId);
    }

    private static CsvRow cachedRow(Dictionary<int, CsvRow> InCache, string InPath, int InId)
    {
        CsvRow row;
        if (InCache.TryGetValue(InId, out row) == false)
        {
            row = CsvRow.Load(InPath, InId);
            if (row != null)
            {
                InCache.Add(InId, row);
            }
        }

        return row;
    }

    private bool hasUnsaved()
    {
        return anyDirty(AbilityRows) == true || anyDirty(EffectRows) == true || anyDirty(TextRows) == true || anyDirty(CharacterRows) == true;
    }

    private static bool anyDirty(Dictionary<int, CsvRow> InRows)
    {
        foreach (var pair in InRows)
        {
            if (pair.Value.Dirty == true)
            {
                return true;
            }
        }

        return false;
    }

    private void save()
    {
        saveDirty(TextRows);
        saveDirty(EffectRows);
        saveDirty(AbilityRows);
        saveDirty(CharacterRows);
        AssetDatabase.SaveAssets();

        // Play 중 저장하면 테이블 행이 새로 만들어지므로 런타임 캐시를 다시 만든다 (새로 스폰/부여한 어빌리티부터 적용).
        if (Application.isPlaying == true && Tables.Root != null)
        {
            Tables.Root.OnBind();
            EffectLibrary.Clear();
        }

        Debug.Log("[GAS Tool] saved");
    }

    private void discard()
    {
        AbilityRows.Clear();
        EffectRows.Clear();
        TextRows.Clear();
        CharacterRows.Clear();
        GraphSerialized = null;
        GUI.FocusControl(null);
    }

    private static void saveDirty(Dictionary<int, CsvRow> InRows)
    {
        foreach (var pair in InRows)
        {
            if (pair.Value.Dirty == true)
            {
                pair.Value.Save();
            }
        }
    }

    // ------------------------------------------------------------------ names

    public string AbilityName(int InAbilityId)
    {
        var row = AbilityRow(InAbilityId);
        if (row == null)
        {
            return $"#{InAbilityId}";
        }

        var text = TextRow(row.GetInt("NameId"));
        return text != null ? text.Get("Text") : $"#{InAbilityId}";
    }

    public string EffectName(int InEffectId)
    {
        var row = EffectRow(InEffectId);
        return row != null ? row.Get("#Memo") : $"#{InEffectId} 없음";
    }

    private GUIStyle ListStyle;
    private GUIStyle ListStyleDim;

    /// <summary>목록 버튼: 왼쪽 정렬 + 길면 잘라서 한 줄로 (전체 이름은 툴팁).</summary>
    private bool listButton(bool InSelected, string InLabel, bool InDim)
    {
        if (ListStyle == null)
        {
            ListStyle = new GUIStyle(EditorStyles.toolbarButton) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip };
            ListStyleDim = new GUIStyle(ListStyle) { fontStyle = FontStyle.Italic };
            ListStyleDim.normal.textColor = Color.gray;
        }

        const int maxLength = 38;
        string shown = InLabel.Length > maxLength ? InLabel.Substring(0, maxLength - 1) + "…" : InLabel;
        bool clicked = GUILayout.Toggle(InSelected, new GUIContent(shown, InLabel), InDim == true ? ListStyleDim : ListStyle, GUILayout.Width(ListWidth - 26.0f));
        return clicked == true && InSelected == false;
    }

    private bool matchesFilter(string InText)
    {
        return string.IsNullOrEmpty(Filter) == true || InText.IndexOf(Filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>InAbilityId 를 가진 캐릭터 이름들.</summary>
    private List<string> abilityOwners(int InAbilityId)
    {
        var owners = new List<string>();
        foreach (var character in TablesAsset.Character.All)
        {
            if (character.Abilities != null && System.Array.IndexOf(character.Abilities, InAbilityId) >= 0)
            {
                StringRow text;
                owners.Add(TablesAsset.String.TryGet(character.NameId, out text) == true ? text.Text : $"#{character.Index}");
            }
        }

        return owners;
    }

    private static int nextFreeKey(string InPath, int InMin, Dictionary<int, CsvRow> InPending)
    {
        int key = CsvEditUtility.NextKey(InPath, InMin);
        while (InPending.ContainsKey(key) == true)
        {
            key = CsvEditUtility.NextKey(InPath, key + 1);
        }

        return key;
    }
}
