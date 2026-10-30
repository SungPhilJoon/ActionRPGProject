using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Ability] 탭. 언리얼 GameplayAbility 의 Details 패널 순서로 편집한다.
///  기본(AbilityTable: 이름 · 슬롯 · AbilityTags · 쿨다운 · AI 거리) → Tags(Cancel/Block/ActivationOwned/Required/Blocked)
///  → Costs → ActivateAbility 단계(그래프) → 타임라인 → 사용처
/// </summary>
public partial class GasToolWindow
{
    private const float TimelineHeight = 52.0f;

    private SerializedObject GraphSerialized;
    private AbilityGraphAsset GraphAsset;
    /// <summary>GraphAsset 이 어느 어빌리티의 것인지 (선택이 바뀌면 다시 읽는다).</summary>
    private int GraphAbilityId;
    private Type[] StepTypes;

    private bool ShowLegacy = true;

    // ------------------------------------------------------------------ list

    private void drawAbilityList()
    {
        ShowLegacy = EditorGUILayout.ToggleLeft("기존 타입 기반 어빌리티도 표시", ShowLegacy);
        var ids = new SortedSet<int>();
        foreach (var row in TablesAsset.Ability.All)
        {
            ids.Add(row.Index);
        }

        foreach (var pair in AbilityRows)
        {
            ids.Add(pair.Key);
        }

        foreach (int id in ids)
        {
            var row = AbilityRow(id);
            if (row == null)
            {
                continue;
            }

            var type = row.GetEnum("Type", EAbilityType.None);
            bool isGraph = type == EAbilityType.Graph;
            if (isGraph == false && ShowLegacy == false)
            {
                continue;
            }

            string label = $"[{id}] {AbilityName(id)}";
            if (matchesFilter(label) == false)
            {
                continue;
            }

            string suffix = isGraph == true ? row.Get("Slot") : $"{type} (기존)";
            if (listButton(id == SelectedAbilityId, $"{label} — {suffix}{(row.Dirty == true ? " *" : string.Empty)}", isGraph == false) == true)
            {
                SelectedAbilityId = id;
                GraphSerialized = null;
                GUI.FocusControl(null);
            }
        }
    }

    /// <summary>목록 아래 "새 Ability" 버튼 → 별도 창(CreateAbilityWindow)에서 템플릿 · 이름을 고른다.</summary>
    private void drawCreateAbility()
    {
        if (GUILayout.Button("+ 새 Ability…", GUILayout.Height(28.0f)) == true)
        {
            CreateAbilityWindow.Open(this);
        }
    }

    /// <summary>에셋 이름 규칙(영문 시작, 영문/숫자/_) + 같은 이름 에셋 없음.</summary>
    public static bool IsAbilityNameAvailable(string InName)
    {
        return isValidAssetName(InName) == true && AssetDatabase.LoadAssetAtPath<AbilityGraphAsset>($"{AbilityAssetFolder}/{InName}.asset") == null;
    }

    private static bool isValidAssetName(string InName)
    {
        if (string.IsNullOrEmpty(InName) == true || char.IsLetter(InName[0]) == false)
        {
            return false;
        }

        foreach (char c in InName)
        {
            if ((char.IsLetterOrDigit(c) == false && c != '_') || c > 127)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 템플릿으로 새 어빌리티를 만든다: 그래프 에셋(Content/Abilities) + AbilityTable / StringTable / AssetTable 행 (+ 방어 템플릿은 가드 GE).
    /// 파일 생성과 행 추가는 바로 저장한다 (주소 등록까지 끝나야 Play 에서 쓸 수 있으므로).
    /// </summary>
    public void CreateAbility(AbilityTemplates.EKind InKind, string InAssetName, string InDisplayName, bool InForMonster)
    {
        var template = AbilityTemplates.Create(InKind);
        int abilityId = nextFreeKey(AbilityCsv, 600, AbilityRows);
        int nameId = nextFreeKey(StringCsv, 3600, TextRows);
        int assetId = CsvEditUtility.NextKey(AssetCsv, 400);

        if (template.NeedsGuardEffect == true)
        {
            int effectId = createEffectRow(EffectPreset.Guard, $"가드: 저항 +60, State_Buff_Guard ({InDisplayName})");
            EffectRows[effectId].Save();
            foreach (var step in template.Steps)
            {
                var apply = step as ApplyEffectToSelfStep;
                if (apply != null)
                {
                    apply.EffectId = effectId;
                }
            }
        }

        EditorAssetUtil.EnsureFolder(AbilityAssetFolder);
        var asset = CreateInstance<AbilityGraphAsset>();
        asset.Description = template.Description;
        asset.ActivationOwnedTags = template.ActivationOwnedTags;
        asset.BlockAbilitiesWithTag = template.BlockAbilitiesWithTag;
        asset.CancelAbilitiesWithTag = template.CancelAbilitiesWithTag;
        asset.Steps = template.Steps;
        AssetDatabase.CreateAsset(asset, $"{AbilityAssetFolder}/{InAssetName}.asset");
        AssetDatabase.SaveAssets();

        var assetRow = CsvRow.Create(AssetCsv, assetId, null);
        assetRow.Set("Address", "Abilities/" + InAssetName);
        assetRow.SetEnum("Kind", EAssetKind.Data);
        assetRow.Save();

        var name = CsvRow.Create(StringCsv, nameId, null);
        name.Set("Text", InDisplayName);
        TextRows[nameId] = name;
        name.Save();

        var row = CsvRow.Create(AbilityCsv, abilityId, null);
        row.SetInt("NameId", nameId);
        row.SetInt("DescId", 0);
        row.SetEnum("Type", EAbilityType.Graph);
        row.SetEnum("Slot", template.Slot);
        var tags = new List<ETag>();
        tags.Add(InForMonster == true ? ETag.Ability_Monster : slotTag(template.Slot));
        row.SetEnums("AbilityTags", tags);
        row.SetFloat("Cooldown", template.Cooldown);
        row.SetFloat("Range", template.AiRange);
        row.SetEnum("DamageType", EDamageType.Physical);
        row.Set("IconColor", "#7A5A9A");
        row.SetEnum("ConsumeTag", ETag.None);
        row.SetInt("Graph", assetId);
        AbilityRows[abilityId] = row;
        row.Save();

        AddressablesSetup.Run();
        SelectedAbilityId = abilityId;
        GraphSerialized = null;
        Tab = 0;
        Focus();
        Repaint();
        Debug.Log($"[GAS Tool] created ability {abilityId} '{InDisplayName}' ({AbilityTemplates.Labels[(int)InKind]}) → {AbilityAssetFolder}/{InAssetName}.asset");
    }

    private static ETag slotTag(EAbilitySlot InSlot)
    {
        switch (InSlot)
        {
            case EAbilitySlot.Primary:
                return ETag.Ability_Primary;
            case EAbilitySlot.WeaponQ:
            case EAbilitySlot.WeaponE:
                return ETag.Ability_Weapon;
            case EAbilitySlot.Spell1:
            case EAbilitySlot.Spell2:
                return ETag.Ability_Spell;
            case EAbilitySlot.Ultimate:
                return ETag.Ability_Ultimate;
            case EAbilitySlot.Travel:
                return ETag.Ability_Travel;
            default:
                return ETag.Ability;
        }
    }

    // ------------------------------------------------------------------ editor

    private void drawAbilityEditor()
    {
        var row = SelectedAbilityId != 0 ? AbilityRow(SelectedAbilityId) : null;
        if (row == null)
        {
            EditorGUILayout.HelpBox("왼쪽에서 어빌리티를 고르거나 '새 Ability' 로 만드세요.\n" +
                                    "어빌리티 = 태그 규칙 + 비용/쿨다운 + 실행 단계 (언리얼 GameplayAbility). 공격은 공격 단계, 방어는 자신에게 GE 를 거는 단계로 만듭니다.", MessageType.Info);
            return;
        }

        var type = row.GetEnum("Type", EAbilityType.None);
        if (type != EAbilityType.Graph)
        {
            drawLegacyAbility(row, type);
            return;
        }

        if (GraphSerialized == null || GraphAsset == null || GraphAbilityId != SelectedAbilityId)
        {
            GraphAsset = loadGraph(row);
            GraphSerialized = GraphAsset != null ? new SerializedObject(GraphAsset) : null;
            GraphAbilityId = SelectedAbilityId;
        }

        EditorGUILayout.LabelField($"[{SelectedAbilityId}] {AbilityName(SelectedAbilityId)}", EditorStyles.largeLabel);
        drawAbilityBasics(row);
        if (GraphAsset == null)
        {
            EditorGUILayout.HelpBox($"그래프 에셋을 찾을 수 없습니다 (AssetTable {row.GetInt("Graph")}).", MessageType.Error);
            return;
        }

        EditorGUILayout.Space();
        drawAbilityTags();
        EditorGUILayout.Space();
        drawSteps();
        EditorGUILayout.Space();
        drawGraphTimeline();
        EditorGUILayout.Space();
        drawAbilityUsers();
    }

    private AbilityGraphAsset loadGraph(CsvRow InRow)
    {
        var asset = CsvRow.Load(AssetCsv, InRow.GetInt("Graph"));
        if (asset == null)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<AbilityGraphAsset>($"{EditorPaths.Content}/{asset.Get("Address")}.asset");
    }

    /// <summary>AbilityTable 행 값 (UI · AI · 쿨다운이 함께 쓰는 값).</summary>
    private void drawAbilityBasics(CsvRow InRow)
    {
        EditorGUILayout.LabelField("기본 (AbilityTable)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var name = TextRow(InRow.GetInt("NameId"));
        if (name != null)
        {
            name.Set("Text", EditorGUILayout.TextField("이름", name.Get("Text")));
        }

        if (GraphAsset != null)
        {
            EditorGUI.BeginChangeCheck();
            string description = EditorGUILayout.TextField(new GUIContent("설명", "에셋 Description (툴 표시용)"), GraphAsset.Description);
            if (EditorGUI.EndChangeCheck() == true)
            {
                Undo.RecordObject(GraphAsset, "Ability Description");
                GraphAsset.Description = description;
                EditorUtility.SetDirty(GraphAsset);
            }
        }

        InRow.SetEnum("Slot", (EAbilitySlot)EditorGUILayout.EnumPopup(new GUIContent("Slot", "입력/AI 슬롯. 한 캐릭터는 슬롯마다 어빌리티 하나 (같은 슬롯은 교체)."), InRow.GetEnum("Slot", EAbilitySlot.Primary)));
        GasEditorGUI.TagList(new GUIContent("AbilityTags", "이 어빌리티의 정체성 태그. 다른 어빌리티의 Block/Cancel 규칙이 이 태그로 매칭한다. 몬스터는 Ability_Monster."),
            InRow.GetEnums<ETag>("AbilityTags"), tags => InRow.SetEnums("AbilityTags", tags));
        InRow.SetFloat("Cooldown", EditorGUILayout.FloatField(new GUIContent("쿨다운 (초)", "커밋 시 범용 쿨다운 GE 로 적용 (쿨감 속성 반영)."), InRow.GetFloat("Cooldown")));
        InRow.SetFloat("Range", EditorGUILayout.FloatField(new GUIContent("AI 사용 거리", "몬스터 AI(UseAbility)가 타깃이 이 거리 안일 때만 사용. 방어/버프는 넉넉하게."), InRow.GetFloat("Range")));
        InRow.Set("IconColor", "#" + ColorUtility.ToHtmlStringRGB(EditorGUILayout.ColorField("아이콘 색", parseColor(InRow.Get("IconColor")))));
        EditorGUILayout.EndVertical();
    }

    private static Color parseColor(string InHtml)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(InHtml, out color) == true ? color : Color.gray;
    }

    /// <summary>언리얼 GameplayAbility 의 Tags / Costs 카테고리.</summary>
    private void drawAbilityTags()
    {
        EditorGUILayout.LabelField("Tags (발동 규칙)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var asset = GraphAsset;
        GasEditorGUI.TagList(new GUIContent("Cancel Abilities With Tag", "발동하면 이 태그를 가진 실행 중 어빌리티를 취소."), asset.CancelAbilitiesWithTag, tags => setTags(() => asset.CancelAbilitiesWithTag = tags));
        GasEditorGUI.TagList(new GUIContent("Block Abilities With Tag", "실행 중 이 태그를 가진 어빌리티 발동을 막음."), asset.BlockAbilitiesWithTag, tags => setTags(() => asset.BlockAbilitiesWithTag = tags));
        GasEditorGUI.TagList(new GUIContent("Activation Owned Tags", "실행 중 자신에게 붙는 태그 (예: State_Attacking, State_Invulnerable)."), asset.ActivationOwnedTags, tags => setTags(() => asset.ActivationOwnedTags = tags));
        GasEditorGUI.TagList(new GUIContent("Activation Required Tags", "이 태그가 모두 있어야 발동."), asset.ActivationRequiredTags, tags => setTags(() => asset.ActivationRequiredTags = tags));
        GasEditorGUI.TagList(new GUIContent("Activation Blocked Tags", "이 태그 중 하나라도 있으면 발동 불가. (사망/기절/흡혈 중은 항상 불가)"), asset.ActivationBlockedTags, tags => setTags(() => asset.ActivationBlockedTags = tags));

        EditorGUI.BeginChangeCheck();
        float cost = EditorGUILayout.FloatField(new GUIContent("Cost: BloodPool", "발동 시 소모 (0 = 없음). 부족하면 발동 불가."), asset.BloodCost);
        if (EditorGUI.EndChangeCheck() == true)
        {
            Undo.RecordObject(asset, "Ability Cost");
            asset.BloodCost = Mathf.Max(0.0f, cost);
            EditorUtility.SetDirty(asset);
        }

        EditorGUILayout.EndVertical();
    }

    private void setTags(Action InAssign)
    {
        Undo.RecordObject(GraphAsset, "Ability Tags");
        InAssign();
        EditorUtility.SetDirty(GraphAsset);
        GraphSerialized.Update();
        Repaint();
    }

    // ------------------------------------------------------------------ steps

    private void drawSteps()
    {
        if (StepTypes == null)
        {
            StepTypes = TypeCache.GetTypesDerivedFrom<AbilityStep>()
                .Where(t => t.IsAbstract == false && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToArray();
        }

        GraphSerialized.Update();
        var list = GraphSerialized.FindProperty("Steps");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"ActivateAbility 단계 ({list.arraySize}) — 위에서부터 실행, 대기 단계가 시간을 만든다", EditorStyles.boldLabel);
        if (GUILayout.Button("+ 단계", GUILayout.Width(70.0f)) == true)
        {
            showStepMenu(list.propertyPath);
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.LabelField("공격 = 피해 단계(부채꼴/원형/발사체), 방어 = 자신에게 GE(저항·보호막·무적) + 대기, 종료 시 자동 제거", EditorStyles.miniLabel);

        float time = 0.0f;
        for (int i = 0; i < list.arraySize; ++i)
        {
            var element = list.GetArrayElementAtIndex(i);
            var step = element.managedReferenceValue as AbilityStep;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string title = step != null ? $"{i + 1}. [{time:0.##}s] {category(step)} — {step.Describe()}" : $"{i + 1}. (타입 없음)";
            element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, title, true);
            if (stepButtons(list, i) == true)
            {
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                break;
            }

            EditorGUILayout.EndHorizontal();
            if (step != null && element.isExpanded == true)
            {
                var info = step.GetType().GetCustomAttribute<AbilityStepInfoAttribute>();
                if (info != null)
                {
                    EditorGUILayout.LabelField(info.Description, EditorStyles.wordWrappedMiniLabel);
                }

                drawStepFields(element);
            }

            if (step != null)
            {
                time += step.PlannedWait();
            }

            EditorGUILayout.EndVertical();
        }

        GraphSerialized.ApplyModifiedProperties();
    }

    private static string category(AbilityStep InStep)
    {
        var info = InStep.GetType().GetCustomAttribute<AbilityStepInfoAttribute>();
        return info != null ? info.Category : "기타";
    }

    /// <summary>단계 필드: 테이블 번호 필드는 이름이 보이는 선택 팝업으로, 나머지는 기본 필드로 그린다.</summary>
    private void drawStepFields(SerializedProperty InElement)
    {
        var iterator = InElement.Copy();
        var end = InElement.GetEndProperty();
        bool enterChildren = true;
        EditorGUI.indentLevel += 1;
        while (iterator.NextVisible(enterChildren) == true && SerializedProperty.EqualContents(iterator, end) == false)
        {
            enterChildren = false;
            var label = new GUIContent(iterator.displayName, iterator.tooltip);
            switch (iterator.name)
            {
                case "AnimationId":
                    iterator.intValue = GasEditorGUI.AnimationPopup(label, iterator.intValue, TablesAsset);
                    break;
                case "EffectId":
                    iterator.intValue = GasEditorGUI.EffectPopup(label, iterator.intValue, TablesAsset);
                    break;
                case "Projectile":
                    iterator.intValue = GasEditorGUI.ProjectilePopup(label, iterator.intValue, TablesAsset);
                    break;
                case "HitEffects":
                    drawEffectArray(iterator, label);
                    break;
                default:
                    EditorGUILayout.PropertyField(iterator, label, true);
                    break;
            }
        }

        EditorGUI.indentLevel -= 1;
    }

    /// <summary>GE 번호 배열: 줄마다 GE 선택 팝업 + 추가/삭제.</summary>
    private void drawEffectArray(SerializedProperty InArray, GUIContent InLabel)
    {
        EditorGUILayout.LabelField(InLabel);
        EditorGUI.indentLevel += 1;
        int remove = -1;
        for (int i = 0; i < InArray.arraySize; ++i)
        {
            var element = InArray.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            element.intValue = GasEditorGUI.EffectPopup(new GUIContent($"GE {i + 1}"), element.intValue, TablesAsset);
            if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22.0f)) == true)
            {
                remove = i;
            }

            EditorGUILayout.EndHorizontal();
        }

        if (remove >= 0)
        {
            InArray.DeleteArrayElementAtIndex(remove);
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Space(EditorGUI.indentLevel * 15.0f);
        if (GUILayout.Button("+ GE", EditorStyles.miniButton, GUILayout.Width(50.0f)) == true)
        {
            InArray.arraySize += 1;
            InArray.GetArrayElementAtIndex(InArray.arraySize - 1).intValue = 0;
        }

        EditorGUILayout.EndHorizontal();
        EditorGUI.indentLevel -= 1;
    }

    /// <summary>위/아래/삭제. 배열 구조가 바뀌면 true.</summary>
    private static bool stepButtons(SerializedProperty InList, int InIndex)
    {
        if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22.0f)) == true && InIndex > 0)
        {
            InList.MoveArrayElement(InIndex, InIndex - 1);
            return true;
        }

        if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22.0f)) == true && InIndex < InList.arraySize - 1)
        {
            InList.MoveArrayElement(InIndex, InIndex + 1);
            return true;
        }

        if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22.0f)) == true)
        {
            InList.DeleteArrayElementAtIndex(InIndex);
            return true;
        }

        return false;
    }

    private void showStepMenu(string InPropertyPath)
    {
        var menu = new GenericMenu();
        foreach (var type in StepTypes)
        {
            var info = type.GetCustomAttribute<AbilityStepInfoAttribute>();
            string path = (info != null ? info.Category + "/" : string.Empty) + ObjectNames.NicifyVariableName(type.Name.Replace("Step", string.Empty));
            var captured = type;
            menu.AddItem(new GUIContent(path), false, () =>
            {
                GraphSerialized.Update();
                var list = GraphSerialized.FindProperty(InPropertyPath);
                list.arraySize += 1;
                var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                element.managedReferenceValue = Activator.CreateInstance(captured);
                element.isExpanded = true;
                GraphSerialized.ApplyModifiedProperties();
            });
        }

        menu.ShowAsContext();
    }

    // ------------------------------------------------------------------ timeline

    /// <summary>단계를 시간 순으로 펼친 막대: 피해(빨강) · 효과(초록) · 동작(파랑) · 종료(흰색). 공격 속도 ×1 기준.</summary>
    private void drawGraphTimeline()
    {
        float time = 0.0f;
        var markers = new List<KeyValuePair<float, AbilityStep>>();
        foreach (var step in GraphAsset.Steps)
        {
            if (step == null)
            {
                continue;
            }

            if (step.Kind == EAbilityStepKind.Hit || step.Kind == EAbilityStepKind.Effect || step.Kind == EAbilityStepKind.Motion)
            {
                markers.Add(new KeyValuePair<float, AbilityStep>(time, step));
            }

            time += step.PlannedWait();
        }

        float end = time;
        float span = Mathf.Max(0.5f, end) * 1.1f;
        EditorGUILayout.LabelField($"타임라인 — 종료 {end:0.##}s (공격 속도 ×1)", EditorStyles.boldLabel);
        var rect = GUILayoutUtility.GetRect(10.0f, TimelineHeight, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.15f));
        var style = new GUIStyle(EditorStyles.miniLabel);
        for (int i = 0; i < markers.Count; ++i)
        {
            var marker = markers[i];
            var color = kindColor(marker.Value.Kind);
            float x = rect.x + rect.width * Mathf.Clamp01(marker.Key / span);
            EditorGUI.DrawRect(new Rect(x - 1.0f, rect.y, 2.0f, rect.height), color);
            style.normal.textColor = color;
            GUI.Label(new Rect(x + 3.0f, rect.y + 2.0f + (i % 3) * 15.0f, 220.0f, 16.0f), $"{marker.Key:0.##}s {marker.Value.Describe()}", style);
        }

        float endX = rect.x + rect.width * Mathf.Clamp01(end / span);
        EditorGUI.DrawRect(new Rect(endX - 1.0f, rect.y, 2.0f, rect.height), Color.white);

        bool hasHit = markers.Any(m => m.Value.Kind == EAbilityStepKind.Hit);
        bool hasEffect = markers.Any(m => m.Value.Kind == EAbilityStepKind.Effect);
        if (hasHit == false && hasEffect == false)
        {
            EditorGUILayout.HelpBox("피해 단계도 효과 단계도 없습니다 — 발동해도 아무 일도 일어나지 않습니다.", MessageType.Warning);
        }

        if (end <= 0.0f)
        {
            EditorGUILayout.HelpBox("대기 단계가 없어 발동 즉시 끝납니다. 방어(가드)처럼 유지되는 효과는 '종료 시 제거' 를 끄거나 대기 단계를 넣으세요.", MessageType.Info);
        }
    }

    private static Color kindColor(EAbilityStepKind InKind)
    {
        switch (InKind)
        {
            case EAbilityStepKind.Hit:
                return new Color(1.0f, 0.35f, 0.3f);
            case EAbilityStepKind.Effect:
                return new Color(0.5f, 0.9f, 0.5f);
            case EAbilityStepKind.Motion:
                return new Color(0.4f, 0.7f, 1.0f);
            default:
                return Color.gray;
        }
    }

    private void drawAbilityUsers()
    {
        var owners = abilityOwners(SelectedAbilityId);
        EditorGUILayout.LabelField("사용처", owners.Count > 0 ? string.Join(", ", owners) : "아직 없음 — StateTree Tool 의 몬스터 패널에서 붙입니다", EditorStyles.wordWrappedLabel);
        if (owners.Count > 1)
        {
            EditorGUILayout.HelpBox("여러 캐릭터가 공유합니다. 수정하면 모두에게 적용됩니다.", MessageType.Info);
        }
    }

    // ------------------------------------------------------------------ legacy

    /// <summary>기존 타입 기반 어빌리티 (MeleeCombo 등): 처리 방식과 값을 읽기 전용으로 보여준다. 수정은 AbilityTable.csv.</summary>
    private void drawLegacyAbility(CsvRow InRow, EAbilityType InType)
    {
        EditorGUILayout.LabelField($"[{SelectedAbilityId}] {AbilityName(SelectedAbilityId)} — {InType} (기존 타입 기반)", EditorStyles.largeLabel);
        EditorGUILayout.HelpBox("코드(GameAbilities.cs)에 구현된 기존 어빌리티입니다. 읽기 전용이며 값은 AbilityTable.csv 에서 고칩니다.\n" +
                                "같은 동작을 에디터로 만들려면 '새 Ability' 템플릿을 쓰세요.", MessageType.Info);
        EditorGUILayout.LabelField("처리 흐름", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(AbilityGuide.Flow(InType), MessageType.None);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.TextField("Slot / Tags", $"{InRow.Get("Slot")} / {InRow.Get("AbilityTags")}");
            EditorGUILayout.TextField("쿨다운 / 피해", $"{InRow.Get("Cooldown")}s / {InRow.Get("Damage")} {InRow.Get("DamageType")}");
            foreach (var field in AbilityGuide.Fields(InType))
            {
                EditorGUILayout.TextField(new GUIContent($"{field.Label} ({field.Column})", field.Tip), InRow.Get(field.Column));
            }

            EditorGUILayout.TextField("HitEffects / SelfEffects", $"{InRow.Get("HitEffects")} / {InRow.Get("SelfEffects")}");
            EditorGUILayout.TextField("Anims (AnimationTable)", InRow.Get("Anims"));
        }

        drawAbilityUsers();
    }
}
