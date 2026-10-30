using System.Collections.Generic;
using Framework.Editor;
using Framework.StateTree;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MonsterAuthoring 인스펙터 — 언리얼의 Character Blueprint 역할 (몬스터 한 마리의 조립).
///  능력치(CharacterTable) · 외형(모델/크기/반지름 + 미리보기) · 애니메이션(트리거별 클립) · AI 트리 지정 · 보유 어빌리티 요약
///  + 저장 / 프리팹 빌드 / 복제해서 새 몬스터.
/// 어빌리티는 GAS Tool(Ability · Ability Set), 행동은 StateTree Tool 에서 만든다.
/// </summary>
[CustomEditor(typeof(MonsterAuthoring))]
public class MonsterAuthoringEditor : UnityEditor.Editor
{
    private const string CharacterCsv = EditorPaths.Csv + "/CharacterTable.csv";
    private const string StringCsv = EditorPaths.Csv + "/StringTable.csv";
    private const string AssetCsv = EditorPaths.Csv + "/AssetTable.csv";
    private const float PreviewHeight = 260.0f;

    private MonsterAuthoring Monster;
    private GameTables TablesAsset;
    private CsvRow CharacterData;
    private CsvRow NameData;
    private readonly MonsterPreview Preview = new MonsterPreview();
    private List<AnimationClip> ModelClips = new List<AnimationClip>();
    private GameObject ModelClipsSource;
    private AnimationClip PreviewClip;
    private float PreviewTime;

    private bool ShowStats = true;
    private bool ShowAppearance = true;
    private bool ShowAnimation;
    private string NewId = "NewMonster";
    private string NewDisplayName = "New Monster";

    private void OnEnable()
    {
        Monster = (MonsterAuthoring)target;
        TablesAsset = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        CharacterData = CsvRow.Load(CharacterCsv, Monster.CharacterId);
        NameData = CharacterData != null ? CsvRow.Load(StringCsv, CharacterData.GetInt("NameId")) : null;
    }

    private void OnDisable()
    {
        Preview.Dispose();
    }

    public override void OnInspectorGUI()
    {
        if (TablesAsset == null)
        {
            EditorGUILayout.HelpBox("GameTables 에셋이 없습니다.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField($"CharacterId {Monster.CharacterId}  ·  {Monster.Address}", EditorStyles.miniBoldLabel);
        if (CharacterData == null)
        {
            EditorGUILayout.HelpBox($"CharacterTable.csv 에 Index {Monster.CharacterId} 행이 없습니다.", MessageType.Error);
            return;
        }

        if (NameData != null)
        {
            NameData.Set("Text", EditorGUILayout.TextField("표시 이름", NameData.Get("Text")));
        }

        ShowStats = EditorGUILayout.BeginFoldoutHeaderGroup(ShowStats, "능력치 · AI");
        if (ShowStats == true)
        {
            drawStats();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        drawAbilities();

        ShowAppearance = EditorGUILayout.BeginFoldoutHeaderGroup(ShowAppearance, "외형");
        if (ShowAppearance == true)
        {
            drawAppearance();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        ShowAnimation = EditorGUILayout.BeginFoldoutHeaderGroup(ShowAnimation, "애니메이션 (트리거별 클립)");
        if (ShowAnimation == true)
        {
            drawAnimation();
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
        EditorGUILayout.Space();
        drawButtons();
    }

    // ------------------------------------------------------------------ stats · AI

    private void drawStats()
    {
        floatField("MaxHealth", "최대 체력");
        floatField("PhysicalPower", "물리 공격력");
        floatField("SpellPower", "주문 공격력");
        floatField("PhysicalResist", "물리 저항");
        floatField("SpellResist", "주문 저항");
        floatField("MoveSpeed", "이동 속도");
        floatField("Level", "레벨");
        floatField("AggroRange", "감지 거리");
        floatField("AttackRange", "기본 공격 거리");
        floatField("Scale", "런타임 배율");
        CharacterData.SetEnum("BloodType", (EBloodType)EditorGUILayout.EnumPopup("혈액 종류", CharacterData.GetEnum("BloodType", EBloodType.None)));
        floatField("BloodQuality", "혈액 품질");
        CharacterData.SetBool("IsVBlood", EditorGUILayout.Toggle("V Blood (보스)", CharacterData.GetBool("IsVBlood")));
        CharacterData.SetInt("Loot", EditorGUILayout.IntField("Loot (LootTable)", CharacterData.GetInt("Loot")));

        // AI 트리: AssetTable 의 AI/ 데이터 중에서 지정
        int current = CharacterData.GetInt("StateTree");
        var ids = new List<int>();
        var labels = new List<string>();
        foreach (var row in TablesAsset.Asset.All)
        {
            if (row.Address != null && row.Address.StartsWith("AI/") == true)
            {
                ids.Add(row.Index);
                labels.Add(row.Address);
            }
        }

        if (ids.Contains(current) == false)
        {
            ids.Add(current);
            labels.Add(current == 0 ? "(없음)" : $"#{current}");
        }

        EditorGUILayout.BeginHorizontal();
        CharacterData.SetInt("StateTree", ids[EditorGUILayout.Popup("AI 트리", ids.IndexOf(current), labels.ToArray())]);
        var tree = loadTree(CharacterData.GetInt("StateTree"));
        using (new EditorGUI.DisabledScope(tree == null))
        {
            if (GUILayout.Button("StateTree Tool", GUILayout.Width(100.0f)) == true)
            {
                StateTreeEditorWindow.Open(tree);
            }
        }

        EditorGUILayout.EndHorizontal();
        using (new EditorGUI.DisabledScope(tree == null || Application.isPlaying == true))
        {
            if (GUILayout.Button("이 몬스터 전용 트리 만들기 (현재 트리 복제)") == true)
            {
                createOwnTree(tree);
            }
        }
    }

    private void floatField(string InColumn, string InLabel)
    {
        CharacterData.SetFloat(InColumn, EditorGUILayout.FloatField(InLabel, CharacterData.GetFloat(InColumn)));
    }

    private void drawAbilities()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("보유 어빌리티 (Ability Set)", EditorStyles.boldLabel);
        if (GUILayout.Button("GAS Tool 에서 편집", GUILayout.Width(130.0f)) == true)
        {
            GasToolWindow.OpenAbilitySet(Monster.CharacterId);
        }

        EditorGUILayout.EndHorizontal();
        foreach (int id in CharacterData.GetInts("Abilities"))
        {
            AbilityRow row;
            StringRow text;
            if (TablesAsset.Ability.TryGet(id, out row) == true)
            {
                string name = TablesAsset.String.TryGet(row.NameId, out text) == true ? text.Text : $"#{id}";
                EditorGUILayout.LabelField($"{row.Slot}: [{id}] {name} — {(AbilityInfo.IsAttack(TablesAsset, row) == true ? "공격" : "방어/버프")}", EditorStyles.miniLabel);
            }
        }

        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ appearance

    private void drawAppearance()
    {
        EditorGUI.BeginChangeCheck();
        var model = (GameObject)EditorGUILayout.ObjectField(new GUIContent("모델", "외부 모델 프리팹 또는 FBX. 빌드 시 데모 스크립트/콜라이더/파티클 제거."), Monster.Model, typeof(GameObject), false);
        float scale = EditorGUILayout.FloatField("모델 스케일", Monster.ModelScale);
        float radius = EditorGUILayout.FloatField(new GUIContent("판정 반지름", "Actor.Radius, NavMeshAgent.radius (미리보기 빨간 원)"), Monster.Radius);
        float height = EditorGUILayout.FloatField("Agent 높이", Monster.AgentHeight);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(Monster, "Monster Appearance");
            Monster.Model = model;
            Monster.ModelScale = Mathf.Max(0.01f, scale);
            Monster.Radius = Mathf.Max(0.05f, radius);
            Monster.AgentHeight = Mathf.Max(0.1f, height);
            EditorUtility.SetDirty(Monster);
        }

        Preview.SetModel(Monster.Model, Monster.ModelScale);
        Preview.Sample(PreviewClip != null ? PreviewClip : Monster.Idle, PreviewTime);
        var rect = GUILayoutUtility.GetRect(10.0f, PreviewHeight, GUILayout.ExpandWidth(true));
        Preview.Draw(rect, Monster.Radius);
    }

    // ------------------------------------------------------------------ animation

    private void drawAnimation()
    {
        if (ModelClipsSource != Monster.Model)
        {
            ModelClipsSource = Monster.Model;
            ModelClips = MonsterAuthoringUtil.GetModelClips(Monster.Model);
        }

        var required = requiredTriggers();
        EditorGUI.BeginChangeCheck();
        var idle = clipField("Idle", Monster.Idle, false);
        var walk = clipField("Walk", Monster.Walk, false);
        var dead = clipField("Dead", Monster.Dead, false);
        float threshold = EditorGUILayout.Slider("Walk 임계값", Monster.WalkThreshold, 0.1f, 1.0f);
        string motionNode = EditorGUILayout.TextField(new GUIContent("제자리 처리 뼈", "Generic 리그의 이동 뼈 (Issues 35). 비우면 그대로."), Monster.MotionNode);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(Monster, "Monster Animation");
            Monster.Idle = idle;
            Monster.Walk = walk;
            Monster.Dead = dead;
            Monster.WalkThreshold = threshold;
            Monster.MotionNode = motionNode;
            EditorUtility.SetDirty(Monster);
        }

        EditorGUILayout.LabelField("● = 보유 어빌리티의 애니메이션 단계가 재생하는 트리거", EditorStyles.miniLabel);
        foreach (EAnim anim in System.Enum.GetValues(typeof(EAnim)))
        {
            if (anim == EAnim.None)
            {
                continue;
            }

            var current = Monster.GetAction(anim);
            bool needed = required.Contains(anim);
            EditorGUI.BeginChangeCheck();
            var picked = clipField(anim + (needed == true ? "  ●" : string.Empty), current, true);
            if (EditorGUI.EndChangeCheck() == true && picked != current)
            {
                Undo.RecordObject(Monster, "Monster Animation");
                Monster.SetAction(anim, picked);
                EditorUtility.SetDirty(Monster);
            }

            if (needed == true && current == null)
            {
                EditorGUILayout.HelpBox($"{anim} 트리거를 재생하는 어빌리티가 있지만 클립이 없습니다.", MessageType.Warning);
            }
        }

        if (PreviewClip != null)
        {
            PreviewTime = EditorGUILayout.Slider($"미리보기: {PreviewClip.name}", PreviewTime, 0.0f, PreviewClip.length);
        }
    }

    /// <summary>모델 클립 팝업 + 미리보기 버튼.</summary>
    private AnimationClip clipField(string InLabel, AnimationClip InCurrent, bool InAllowNone)
    {
        var options = new List<AnimationClip> { null };
        options.AddRange(ModelClips);
        if (InCurrent != null && options.Contains(InCurrent) == false)
        {
            options.Add(InCurrent);
        }

        var names = new string[options.Count];
        names[0] = "(없음)";
        for (int i = 1; i < options.Count; ++i)
        {
            names[i] = options[i].name;
        }

        EditorGUILayout.BeginHorizontal();
        var picked = options[EditorGUILayout.Popup(InLabel, Mathf.Max(0, options.IndexOf(InCurrent)), names)];
        using (new EditorGUI.DisabledScope(picked == null))
        {
            if (GUILayout.Button("▶", EditorStyles.miniButton, GUILayout.Width(24.0f)) == true)
            {
                PreviewClip = picked;
                PreviewTime = 0.0f;
                ShowAppearance = true;
            }
        }

        EditorGUILayout.EndHorizontal();
        return picked;
    }

    /// <summary>보유 어빌리티가 재생하는 트리거 (AnimationTable 의 Trigger 이름을 해시로 EAnim 과 맞춘다).</summary>
    private HashSet<EAnim> requiredTriggers()
    {
        var hashToAnim = new Dictionary<int, EAnim>();
        foreach (EAnim anim in System.Enum.GetValues(typeof(EAnim)))
        {
            hashToAnim[Animator.StringToHash(AnimatorParam.TriggerName(anim))] = anim;
        }

        var result = new HashSet<EAnim>();
        foreach (int id in CharacterData.GetInts("Abilities"))
        {
            AbilityRow ability;
            if (TablesAsset.Ability.TryGet(id, out ability) == false)
            {
                continue;
            }

            foreach (int animId in AbilityInfo.AnimationIds(TablesAsset, ability))
            {
                AnimationRow row;
                if (TablesAsset.Animation.TryGet(animId, out row) == false || row.Parameters == null)
                {
                    continue;
                }

                foreach (var parameter in row.Parameters)
                {
                    EAnim anim;
                    if (parameter.Type == EAnimParamType.Trigger && hashToAnim.TryGetValue(Animator.StringToHash(parameter.Name), out anim) == true)
                    {
                        result.Add(anim);
                    }
                }
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ buttons

    private void drawButtons()
    {
        bool dirty = CharacterData.Dirty == true || (NameData != null && NameData.Dirty == true);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(dirty == true ? "저장 *" : "저장", GUILayout.Height(26.0f)) == true)
        {
            save();
        }

        if (GUILayout.Button("저장 + 프리팹 빌드", GUILayout.Height(26.0f)) == true)
        {
            save();
            if (ContentBuilder.BuildMonster(Monster) != null)
            {
                AddressablesSetup.Run();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("복제해서 새 몬스터", EditorStyles.boldLabel);
        NewId = EditorGUILayout.TextField("주소 이름 (영문)", NewId);
        NewDisplayName = EditorGUILayout.TextField("표시 이름", NewDisplayName);
        using (new EditorGUI.DisabledScope(isValidId(NewId) == false))
        {
            if (GUILayout.Button("만들기") == true)
            {
                duplicate();
            }
        }
    }

    private void save()
    {
        EditorUtility.SetDirty(Monster);
        AssetDatabase.SaveAssets();
        if (CharacterData.Dirty == true)
        {
            CharacterData.Save();
        }

        if (NameData != null && NameData.Dirty == true)
        {
            NameData.Save();
        }

        if (Application.isPlaying == true && Tables.Root != null)
        {
            Tables.Root.OnBind();
        }
    }

    private static bool isValidId(string InId)
    {
        if (string.IsNullOrEmpty(InId) == true || char.IsLetter(InId[0]) == false)
        {
            return false;
        }

        foreach (char c in InId)
        {
            if (char.IsLetterOrDigit(c) == false || c > 127)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>저작 에셋 + CharacterTable/StringTable/AssetTable 새 행. 능력치·AI 트리·어빌리티는 원본 그대로.</summary>
    private void duplicate()
    {
        string address = "Characters/" + NewId;
        if (AssetDatabase.LoadAssetAtPath<Object>($"{EditorPaths.Content}/{address}.prefab") != null)
        {
            EditorUtility.DisplayDialog("Monster", $"{address} 프리팹이 이미 있습니다.", "확인");
            return;
        }

        int characterId = CsvEditUtility.NextKey(CharacterCsv, 1);
        int nameId = CsvEditUtility.NextKey(StringCsv, 1000);
        int prefabId = CsvEditUtility.NextKey(AssetCsv, 100);

        var asset = MonsterAuthoringUtil.Create(NewId);
        EditorUtility.CopySerialized(Monster, asset);
        asset.name = NewId;
        asset.CharacterId = characterId;
        asset.Address = address;
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();

        var row = CsvRow.Create(CharacterCsv, characterId, CsvRow.Load(CharacterCsv, Monster.CharacterId));
        row.SetInt("NameId", nameId);
        row.SetInt("Prefab", prefabId);
        row.Save();
        var name = CsvRow.Create(StringCsv, nameId, null);
        name.Set("Text", NewDisplayName);
        name.Save();
        var prefab = CsvRow.Create(AssetCsv, prefabId, null);
        prefab.Set("Address", address);
        prefab.SetEnum("Kind", EAssetKind.Prefab);
        prefab.Save();

        Selection.activeObject = asset;
        Debug.Log($"[Monster] {address} (CharacterId {characterId}) 생성 — '저장 + 프리팹 빌드' 로 프리팹을 만드세요.");
    }

    /// <summary>현재 트리를 Content/AI/ST_{이름} 으로 복제, AssetTable 행 추가, 이 몬스터에 지정 (바로 저장).</summary>
    private void createOwnTree(StateTreeAsset InSource)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath($"{EditorPaths.Content}/AI/ST_{Monster.name}.asset");
        string name = System.IO.Path.GetFileNameWithoutExtension(path);
        var copy = Object.Instantiate(InSource);
        copy.Description = $"{Monster.name} 전용 (원본 {InSource.name}) — {InSource.Description}";
        AssetDatabase.CreateAsset(copy, path);
        AssetDatabase.SaveAssets();

        int assetId = CsvEditUtility.NextKey(AssetCsv, 300);
        var row = CsvRow.Create(AssetCsv, assetId, null);
        row.Set("Address", "AI/" + name);
        row.SetEnum("Kind", EAssetKind.Data);
        row.Save();

        CharacterData.SetInt("StateTree", assetId);
        CharacterData.Save();
        AddressablesSetup.Run();
        StateTreeEditorWindow.Open(copy);
    }

    private static StateTreeAsset loadTree(int InAssetId)
    {
        if (InAssetId == 0)
        {
            return null;
        }

        var row = CsvRow.Load(AssetCsv, InAssetId);
        return row != null ? AssetDatabase.LoadAssetAtPath<StateTreeAsset>($"{EditorPaths.Content}/{row.Get("Address")}.asset") : null;
    }
}
