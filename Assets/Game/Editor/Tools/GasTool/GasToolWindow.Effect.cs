using System.Collections.Generic;
using Framework.Ability;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [GameplayEffect] 탭: EffectTable 행 편집 (언리얼 UGameplayEffect 의 Duration / Modifiers / Granted Tags / Application Tags / Stacking / Execution).
/// 방어 어빌리티는 여기서 만든 GE(저항 증가, 무적 등)를 ApplyEffectToSelf 단계로 건다.
/// </summary>
public partial class GasToolWindow
{
    /// <summary>EffectLibrary 가 번호로 쓰는 시스템 GE (1 피해 ~ 8 부활 무적).</summary>
    private const int ReservedEffectMax = 8;

    public enum EffectPreset
    {
        Guard = 0,
        Invulnerable = 1,
        Haste = 2,
        Slow = 3,
        Bleed = 4,
        Empty = 5,
    }

    private static readonly string[] PresetLabels = { "방어 (저항 +60, 무한 — 어빌리티 종료 시 제거)", "무적 1.5초", "가속 4초", "둔화 3초 (디버프)", "출혈 (지속 피해)", "빈 GE" };

    private bool ShowCreateEffect;
    private EffectPreset NewPreset = EffectPreset.Guard;
    private string NewEffectMemo = "새 GE";
    private Dictionary<int, List<string>> EffectUsers;

    // ------------------------------------------------------------------ list

    private void drawEffectList()
    {
        var ids = new SortedSet<int>();
        foreach (var row in TablesAsset.Effect.All)
        {
            ids.Add(row.Index);
        }

        foreach (var pair in EffectRows)
        {
            ids.Add(pair.Key);
        }

        foreach (int id in ids)
        {
            string label = $"#{id} {EffectName(id)}";
            if (matchesFilter(label) == false)
            {
                continue;
            }

            var row = EffectRow(id);
            if (listButton(id == SelectedEffectId, label + (row != null && row.Dirty == true ? " *" : string.Empty), id <= ReservedEffectMax) == true)
            {
                SelectedEffectId = id;
                EffectUsers = null;
                GUI.FocusControl(null);
            }
        }
    }

    private void drawCreateEffect()
    {
        ShowCreateEffect = EditorGUILayout.Foldout(ShowCreateEffect, "새 GameplayEffect", true);
        if (ShowCreateEffect == false)
        {
            return;
        }

        NewPreset = (EffectPreset)EditorGUILayout.Popup("프리셋", (int)NewPreset, PresetLabels);
        NewEffectMemo = EditorGUILayout.TextField("이름 (메모)", NewEffectMemo);
        if (GUILayout.Button("만들기 (저장 전까지 작업 사본)") == true)
        {
            SelectedEffectId = createEffectRow(NewPreset, NewEffectMemo);
            ShowCreateEffect = false;
        }
    }

    /// <summary>프리셋으로 EffectTable 새 행 (작업 사본, 저장 필요). 번호 100 이상.</summary>
    private int createEffectRow(EffectPreset InPreset, string InMemo)
    {
        int id = nextFreeKey(EffectCsv, 100, EffectRows);
        var row = CsvRow.Create(EffectCsv, id, null);
        row.SetEnum("DurationPolicy", EDurationPolicy.HasDuration);
        row.SetFloat("Duration", 3.0f);
        row.SetFloat("Period", 0.0f);
        row.SetBool("ExecuteOnApply", false);
        row.SetEnum("Execution", EExecution.None);
        row.SetEnum("Stack", EStackPolicy.Refresh);
        row.SetInt("MaxStack", 1);
        row.SetInt("Cue", 0);
        row.SetBool("IsCooldown", false);
        for (int m = 1; m <= 3; ++m)
        {
            setModifier(row, m, EAttribute.Health, EModifierOp.Add, 0.0f);
        }

        row.SetFloat("Coefficient", 0.0f);
        row.SetEnum("DamageType", EDamageType.Physical);
        row.Set("#Memo", InMemo);

        switch (InPreset)
        {
            case EffectPreset.Guard:
                row.SetEnum("DurationPolicy", EDurationPolicy.Infinite);
                row.SetFloat("Duration", 0.0f);
                row.SetEnums("GrantedTags", new[] { ETag.State_Buff_Guard });
                setModifier(row, 1, EAttribute.PhysicalResist, EModifierOp.Add, 60.0f);
                setModifier(row, 2, EAttribute.SpellResist, EModifierOp.Add, 60.0f);
                break;
            case EffectPreset.Invulnerable:
                row.SetFloat("Duration", 1.5f);
                row.SetEnums("GrantedTags", new[] { ETag.State_Invulnerable });
                row.SetInt("Cue", 5);
                break;
            case EffectPreset.Haste:
                row.SetFloat("Duration", 4.0f);
                row.SetEnums("GrantedTags", new[] { ETag.State_Buff_Haste });
                setModifier(row, 1, EAttribute.MoveSpeed, EModifierOp.MultiplyPercent, 30.0f);
                break;
            case EffectPreset.Slow:
                row.SetFloat("Duration", 3.0f);
                row.SetEnums("GrantedTags", new[] { ETag.State_Debuff_Slow });
                setModifier(row, 1, EAttribute.MoveSpeed, EModifierOp.MultiplyPercent, -35.0f);
                break;
            case EffectPreset.Bleed:
                row.SetFloat("Duration", 4.0f);
                row.SetFloat("Period", 1.0f);
                row.SetEnum("Execution", EExecution.Damage);
                row.SetEnums("GrantedTags", new[] { ETag.State_Debuff_Bleed });
                row.SetFloat("Coefficient", 15.0f);
                break;
        }

        EffectRows[id] = row;
        EffectUsers = null;
        return id;
    }

    private static void setModifier(CsvRow InRow, int InIndex, EAttribute InAttribute, EModifierOp InOp, float InValue)
    {
        InRow.SetEnum($"Mod{InIndex}Attr", InAttribute);
        InRow.SetEnum($"Mod{InIndex}Op", InOp);
        InRow.SetEnum($"Mod{InIndex}Type", EMagnitudeType.Scalar);
        InRow.SetInt($"Mod{InIndex}Key", 0);
        InRow.SetFloat($"Mod{InIndex}Value", InValue);
    }

    // ------------------------------------------------------------------ editor

    private void drawEffectEditor()
    {
        var row = SelectedEffectId != 0 ? EffectRow(SelectedEffectId) : null;
        if (row == null)
        {
            EditorGUILayout.HelpBox("왼쪽에서 GE 를 고르거나 '새 GameplayEffect' 로 만드세요.\n" +
                                    "GE = 대상의 속성을 바꾸거나(모디파이어) 태그를 붙이는 효과 (언리얼 GameplayEffect). 버프/디버프/방어/도트에 씁니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"GE #{SelectedEffectId}", EditorStyles.largeLabel);
        if (SelectedEffectId <= ReservedEffectMax)
        {
            EditorGUILayout.HelpBox("시스템 예약 GE 입니다 (EffectLibrary 가 번호로 사용: 피해/회복/쿨다운/비용/햇빛/굶주림/흡혈 기절/부활 무적). 수정에 주의하세요.", MessageType.Warning);
        }

        row.Set("#Memo", EditorGUILayout.TextField("이름 (메모)", row.Get("#Memo")));

        EditorGUILayout.LabelField("Duration", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        var policy = (EDurationPolicy)EditorGUILayout.EnumPopup(new GUIContent("Duration Policy", "Instant: 기본값을 즉시 변경 / HasDuration: 시간 동안 / Infinite: 제거될 때까지 (어빌리티 '종료 시 제거' 와 함께)"), row.GetEnum("DurationPolicy", EDurationPolicy.Instant));
        row.SetEnum("DurationPolicy", policy);
        if (policy == EDurationPolicy.HasDuration)
        {
            row.SetFloat("Duration", EditorGUILayout.FloatField("Duration (초)", row.GetFloat("Duration")));
        }

        if (policy != EDurationPolicy.Instant)
        {
            row.SetFloat("Period", EditorGUILayout.FloatField(new GUIContent("Period (초)", "0 보다 크면 주기마다 실행 (도트). 0 이면 지속 모디파이어."), row.GetFloat("Period")));
            if (row.GetFloat("Period") > 0.0f)
            {
                row.SetBool("ExecuteOnApply", EditorGUILayout.Toggle("적용 즉시 1회 실행", row.GetBool("ExecuteOnApply")));
            }
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.LabelField("Modifiers (속성 변경, 최대 3개)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        for (int m = 1; m <= 3; ++m)
        {
            drawModifier(row, m);
        }

        EditorGUILayout.LabelField("Add: 더하기 / MultiplyPercent: 10 = +10% / Override: 덮어쓰기. SetByCaller·Source/TargetAttribute 는 Key 로 지정.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();

        EditorGUILayout.LabelField("Execution (계산식)", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        row.SetEnum("Execution", (EExecution)EditorGUILayout.EnumPopup(new GUIContent("Execution", "Damage: 시전자 공격력 × Coefficient% 피해 (도트/폭발)"), row.GetEnum("Execution", EExecution.None)));
        if (row.GetEnum("Execution", EExecution.None) == EExecution.Damage)
        {
            row.SetFloat("Coefficient", EditorGUILayout.FloatField("Coefficient (%)", row.GetFloat("Coefficient")));
            row.SetEnum("DamageType", (EDamageType)EditorGUILayout.EnumPopup("Damage Type", row.GetEnum("DamageType", EDamageType.Physical)));
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.LabelField("Tags", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        GasEditorGUI.TagList(new GUIContent("Granted Tags", "효과가 있는 동안 대상에게 붙는 태그 (상태)."), row.GetEnums<ETag>("GrantedTags"), tags => row.SetEnums("GrantedTags", tags));
        GasEditorGUI.TagList(new GUIContent("Application Required", "대상이 이 태그를 모두 가져야 적용."), row.GetEnums<ETag>("RequiredTags"), tags => row.SetEnums("RequiredTags", tags));
        GasEditorGUI.TagList(new GUIContent("Application Blocked", "대상이 이 태그 중 하나라도 있으면 적용 안 됨 (예: 무적이면 디버프 무시)."), row.GetEnums<ETag>("BlockedTags"), tags => row.SetEnums("BlockedTags", tags));
        GasEditorGUI.TagList(new GUIContent("Remove Effects With Tags", "적용 시 이 태그를 부여한 다른 효과를 제거 (정화 등)."), row.GetEnums<ETag>("RemoveTags"), tags => row.SetEnums("RemoveTags", tags));
        EditorGUILayout.EndVertical();

        EditorGUILayout.LabelField("Stacking / Cue", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        row.SetEnum("Stack", (EStackPolicy)EditorGUILayout.EnumPopup(new GUIContent("Stack", "None: 매번 별도 / Refresh: 시간만 갱신 / Stack: 중첩 (MaxStack 까지)"), row.GetEnum("Stack", EStackPolicy.None)));
        row.SetInt("MaxStack", Mathf.Max(1, EditorGUILayout.IntField("Max Stack", row.GetInt("MaxStack"))));
        row.SetInt("Cue", EditorGUILayout.IntField(new GUIContent("Cue", "효과가 있는 동안 표시할 GameplayCue (CueTable Index, 0 = 없음)."), row.GetInt("Cue")));
        EditorGUILayout.EndVertical();

        if (row.Dirty == true)
        {
            EditorGUILayout.LabelField("변경됨 — 저장하면 EffectTable.csv 에 기록됩니다.", EditorStyles.miniBoldLabel);
        }

        EditorGUILayout.Space();
        drawEffectUsers();
    }

    private static void drawModifier(CsvRow InRow, int InIndex)
    {
        string prefix = $"Mod{InIndex}";
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"{InIndex}", GUILayout.Width(14.0f));
        InRow.SetEnum(prefix + "Attr", (EAttribute)EditorGUILayout.EnumPopup(InRow.GetEnum(prefix + "Attr", EAttribute.Health), GUILayout.Width(140.0f)));
        InRow.SetEnum(prefix + "Op", (EModifierOp)EditorGUILayout.EnumPopup(InRow.GetEnum(prefix + "Op", EModifierOp.Add), GUILayout.Width(120.0f)));
        var type = (EMagnitudeType)EditorGUILayout.EnumPopup(InRow.GetEnum(prefix + "Type", EMagnitudeType.Scalar), GUILayout.Width(120.0f));
        InRow.SetEnum(prefix + "Type", type);
        if (type != EMagnitudeType.Scalar)
        {
            EditorGUILayout.LabelField("Key", GUILayout.Width(26.0f));
            InRow.SetInt(prefix + "Key", EditorGUILayout.IntField(InRow.GetInt(prefix + "Key"), GUILayout.Width(36.0f)));
        }

        InRow.SetFloat(prefix + "Value", EditorGUILayout.FloatField(InRow.GetFloat(prefix + "Value")));
        bool unused = InRow.GetFloat(prefix + "Value") == 0.0f && type != EMagnitudeType.SetByCaller;
        EditorGUILayout.LabelField(unused == true ? "(미사용)" : string.Empty, EditorStyles.miniLabel, GUILayout.Width(52.0f));
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>이 GE 를 쓰는 어빌리티: 기존 행의 Hit/Self/Start/ConsumeEffects + 그래프 에셋의 단계.</summary>
    private void drawEffectUsers()
    {
        if (EffectUsers == null)
        {
            EffectUsers = collectEffectUsers();
        }

        List<string> users;
        EffectUsers.TryGetValue(SelectedEffectId, out users);
        EditorGUILayout.LabelField("사용처", users != null && users.Count > 0 ? string.Join(", ", users) : "없음", EditorStyles.wordWrappedLabel);
        if (GUILayout.Button("사용처 다시 찾기", GUILayout.Width(120.0f)) == true)
        {
            EffectUsers = null;
        }
    }

    private Dictionary<int, List<string>> collectEffectUsers()
    {
        var result = new Dictionary<int, List<string>>();
        foreach (var ability in TablesAsset.Ability.All)
        {
            string label = $"[{ability.Index}] {AbilityName(ability.Index)}";
            addUsers(result, ability.HitEffects, label);
            addUsers(result, ability.SelfEffects, label);
            addUsers(result, ability.StartEffects, label);
            addUsers(result, ability.ConsumeEffects, label);
        }

        foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(AbilityGraphAsset)))
        {
            var graph = AssetDatabase.LoadAssetAtPath<AbilityGraphAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (graph == null)
            {
                continue;
            }

            foreach (var step in graph.Steps)
            {
                var self = step as ApplyEffectToSelfStep;
                var target = step as ApplyEffectToTargetStep;
                var hit = step as HitStep;
                if (self != null)
                {
                    addUsers(result, new[] { self.EffectId }, graph.name);
                }
                else if (target != null)
                {
                    addUsers(result, new[] { target.EffectId }, graph.name);
                }
                else if (hit != null)
                {
                    addUsers(result, hit.HitEffects, graph.name);
                }
            }
        }

        return result;
    }

    private static void addUsers(Dictionary<int, List<string>> InResult, int[] InIds, string InLabel)
    {
        if (InIds == null)
        {
            return;
        }

        foreach (int id in InIds)
        {
            List<string> users;
            if (InResult.TryGetValue(id, out users) == false)
            {
                users = new List<string>();
                InResult.Add(id, users);
            }

            if (users.Contains(InLabel) == false)
            {
                users.Add(InLabel);
            }
        }
    }
}
