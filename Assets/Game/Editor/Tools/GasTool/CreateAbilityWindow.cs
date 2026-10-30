using UnityEditor;
using UnityEngine;

/// <summary>
/// GAS Tool 의 "새 Ability" 창. 왼쪽에서 템플릿을 고르면 오른쪽에 그 템플릿의 슬롯 · 쿨다운 · AI 거리 · 태그 · 단계가 미리 보이고,
/// 아래에서 이름을 정해 만든다. 실제 생성(에셋 + CSV 행 + Addressables 등록)은 GasToolWindow.CreateAbility 가 한다.
/// </summary>
public class CreateAbilityWindow : EditorWindow
{
    private const float TemplateListWidth = 190.0f;

    private GasToolWindow Owner;
    private AbilityTemplates.EKind Kind = AbilityTemplates.EKind.Melee;
    private string AssetName = "GA_NewAttack";
    private string DisplayName = string.Empty;
    private bool ForMonster = true;
    private bool AssetNameEdited;
    private bool DisplayNameEdited;
    private Vector2 PreviewScroll;

    public static void Open(GasToolWindow InOwner)
    {
        var window = CreateInstance<CreateAbilityWindow>();
        window.Owner = InOwner;
        window.titleContent = new GUIContent("새 Ability");
        window.minSize = new Vector2(680.0f, 480.0f);
        window.applySuggestedName();
        window.ShowUtility();
    }

    private void OnGUI()
    {
        if (Owner == null)
        {
            EditorGUILayout.HelpBox("GAS Tool 창이 닫혔습니다. 다시 열어 주세요.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginHorizontal();
        drawTemplateList();
        EditorGUILayout.BeginVertical();
        drawPreview();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();

        drawFields();
    }

    // ------------------------------------------------------------------ template list

    private void drawTemplateList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(TemplateListWidth), GUILayout.ExpandHeight(true));
        EditorGUILayout.LabelField("템플릿", EditorStyles.boldLabel);
        for (int i = 0; i < AbilityTemplates.Labels.Length; ++i)
        {
            var kind = (AbilityTemplates.EKind)i;
            bool selected = kind == Kind;
            if (GUILayout.Toggle(selected, AbilityTemplates.Labels[i], "Button", GUILayout.Height(30.0f)) == true && selected == false)
            {
                Kind = kind;
                applySuggestedName();
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndVertical();
    }

    // ------------------------------------------------------------------ preview

    /// <summary>선택한 템플릿이 만들 내용: AbilityTable 값, 태그 규칙, 단계 목록 (시작 시각 포함).</summary>
    private void drawPreview()
    {
        var template = AbilityTemplates.Create(Kind);
        EditorGUILayout.LabelField(AbilityTemplates.Labels[(int)Kind], EditorStyles.largeLabel);
        if (string.IsNullOrEmpty(template.Description) == false)
        {
            EditorGUILayout.HelpBox(template.Description, MessageType.None);
        }

        PreviewScroll = EditorGUILayout.BeginScrollView(PreviewScroll, EditorStyles.helpBox, GUILayout.ExpandHeight(true));
        EditorGUIUtility.labelWidth = 210.0f;
        previewRow("슬롯 / 쿨다운 / AI 사용 거리", $"{template.Slot}  /  {template.Cooldown:0.##}초  /  {template.AiRange:0.#}m");
        previewRow("실행 중 태그 (ActivationOwned)", tagText(template.ActivationOwnedTags));
        previewRow("실행 중 막는 태그 (Block)", tagText(template.BlockAbilitiesWithTag));
        if (template.NeedsGuardEffect == true)
        {
            previewRow("함께 만드는 GE", "가드: 물리·주문 저항 +60, State_Buff_Guard (무한, 어빌리티 종료 시 제거)");
        }

        EditorGUIUtility.labelWidth = 0.0f;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"단계 {template.Steps.Count}개", EditorStyles.boldLabel);
        if (template.Steps.Count == 0)
        {
            EditorGUILayout.LabelField("(없음 — 만든 뒤 '+ 단계' 로 추가)", EditorStyles.miniLabel);
        }

        float time = 0.0f;
        for (int i = 0; i < template.Steps.Count; ++i)
        {
            var step = template.Steps[i];
            var apply = step as ApplyEffectToSelfStep;
            string text = apply != null && apply.EffectId == 0 && template.NeedsGuardEffect == true ? "자신에게 가드 GE (함께 생성, 종료 시 제거)" : step.Describe();
            EditorGUILayout.LabelField($"{i + 1}.  [{time:0.##}s]  {text}");
            time += step.PlannedWait();
        }

        if (template.Steps.Count > 0)
        {
            EditorGUILayout.LabelField($"종료 {time:0.##}s (공격 속도 ×1)", EditorStyles.miniBoldLabel);
        }

        EditorGUILayout.EndScrollView();
    }

    /// <summary>라벨 + 줄바꿈되는 값 (긴 태그 목록이 잘리지 않게).</summary>
    private static void previewRow(string InLabel, string InValue)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(InLabel);
        EditorGUILayout.LabelField(InValue, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndHorizontal();
    }

    private static string tagText(ETag[] InTags)
    {
        return InTags == null || InTags.Length == 0 ? "(없음)" : string.Join(", ", InTags);
    }

    // ------------------------------------------------------------------ fields

    private void drawFields()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUIUtility.labelWidth = 150.0f;
        EditorGUI.BeginChangeCheck();
        DisplayName = EditorGUILayout.TextField(new GUIContent("표시 이름", "게임/툴에 보이는 이름 (StringTable). 한글 가능."), DisplayName);
        if (EditorGUI.EndChangeCheck() == true)
        {
            DisplayNameEdited = true;
        }

        EditorGUI.BeginChangeCheck();
        AssetName = EditorGUILayout.TextField(new GUIContent("에셋 이름 (영문)", "Content/Abilities/{이름}.asset, Addressables 주소 Abilities/{이름}. 영문으로 시작, 영문·숫자·_ 만."), AssetName);
        if (EditorGUI.EndChangeCheck() == true)
        {
            AssetNameEdited = true;
        }

        ForMonster = EditorGUILayout.Toggle(new GUIContent("몬스터용", "AbilityTags 에 Ability_Monster (몬스터 AI 가 쓰는 어빌리티). 끄면 슬롯에 맞는 플레이어 태그."), ForMonster);
        EditorGUIUtility.labelWidth = 0.0f;

        bool available = GasToolWindow.IsAbilityNameAvailable(AssetName);
        bool hasName = string.IsNullOrEmpty(DisplayName.Trim()) == false;
        if (available == false)
        {
            EditorGUILayout.HelpBox("에셋 이름이 규칙에 맞지 않거나 이미 있습니다.", MessageType.Warning);
        }

        EditorGUILayout.LabelField("만들면 에셋 · CSV 행(AbilityTable 600~, StringTable 3600~, AssetTable 400~)이 바로 저장되고 Addressables 에 등록됩니다.", EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("취소", GUILayout.Width(90.0f), GUILayout.Height(28.0f)) == true)
        {
            Close();
        }

        using (new EditorGUI.DisabledScope(available == false || hasName == false))
        {
            if (GUILayout.Button("만들기", GUILayout.Width(120.0f), GUILayout.Height(28.0f)) == true)
            {
                Owner.CreateAbility(Kind, AssetName, DisplayName.Trim(), ForMonster);
                Close();
                GUIUtility.ExitGUI();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    /// <summary>직접 고치지 않은 이름은 템플릿에 맞춰 제안한다: 표시 이름 = 템플릿 이름, 에셋 이름 = 겹치지 않는 GA_Melee, GA_Melee2 …</summary>
    private void applySuggestedName()
    {
        if (DisplayNameEdited == false)
        {
            string label = AbilityTemplates.Labels[(int)Kind];
            int paren = label.IndexOf(" (");
            DisplayName = paren > 0 ? label.Substring(0, paren) : label;
        }

        if (AssetNameEdited == true)
        {
            return;
        }

        string[] bases = { "GA_Melee", "GA_Ranged", "GA_Area", "GA_Charge", "GA_Guard", "GA_Buff", "GA_New" };
        string baseName = bases[Mathf.Clamp((int)Kind, 0, bases.Length - 1)];
        string candidate = baseName;
        for (int i = 2; GasToolWindow.IsAbilityNameAvailable(candidate) == false && i < 100; ++i)
        {
            candidate = baseName + i;
        }

        AssetName = candidate;
    }
}
