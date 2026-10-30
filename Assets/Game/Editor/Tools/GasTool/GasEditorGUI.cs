using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GAS Tool / 몬스터 패널이 함께 쓰는 IMGUI 조각: 태그 목록(칩 + 추가 메뉴), EffectTable / AnimationTable / 발사체 선택 팝업.
/// 표시 이름은 테이블 에셋(GameTables)에서 읽고, 값은 int / ETag 그대로 돌려준다.
/// </summary>
public static class GasEditorGUI
{
    /// <summary>
    /// 태그 목록: 현재 태그를 칩(✕로 제거)으로, "+" 로 ETag 를 골라 추가. 바뀐 배열은 InSet 으로 넘긴다
    /// (추가 메뉴는 OnGUI 밖에서 콜백되므로 반환값 대신 콜백을 쓴다).
    /// </summary>
    public static void TagList(GUIContent InLabel, ETag[] InTags, System.Action<ETag[]> InSet)
    {
        var tags = new List<ETag>(InTags != null ? InTags : new ETag[0]);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(InLabel);
        if (tags.Count == 0)
        {
            GUILayout.Label("(없음)", EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
        }

        int remove = -1;
        for (int i = 0; i < tags.Count; ++i)
        {
            if (GUILayout.Button(tags[i] + "  ✕", EditorStyles.miniButton, GUILayout.ExpandWidth(false)) == true)
            {
                remove = i;
            }
        }

        if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(22.0f)) == true)
        {
            var menu = new GenericMenu();
            foreach (ETag tag in System.Enum.GetValues(typeof(ETag)))
            {
                if (tag == ETag.None || tags.Contains(tag) == true)
                {
                    continue;
                }

                var captured = tag;
                menu.AddItem(new GUIContent(tag.ToString().Replace('_', '/')), false, () =>
                {
                    var added = new List<ETag>(tags);
                    added.Add(captured);
                    InSet(added.ToArray());
                });
            }

            menu.ShowAsContext();
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        if (remove >= 0)
        {
            tags.RemoveAt(remove);
            InSet(tags.ToArray());
        }
    }

    // ------------------------------------------------------------------ table pickers

    public static int EffectPopup(GUIContent InLabel, int InEffectId, GameTables InTables)
    {
        var ids = new List<int> { 0 };
        var labels = new List<string> { "(없음)" };
        foreach (var row in InTables.Effect.All)
        {
            ids.Add(row.Index);
            labels.Add($"#{row.Index} {EffectSummary(row)}");
        }

        if (ids.Contains(InEffectId) == false)
        {
            ids.Add(InEffectId);
            labels.Add($"#{InEffectId} (저장 전 새 GE)");
        }

        int index = EditorGUILayout.Popup(InLabel, ids.IndexOf(InEffectId), labels.ToArray());
        return ids[Mathf.Max(0, index)];
    }

    public static int AnimationPopup(GUIContent InLabel, int InAnimationId, GameTables InTables)
    {
        var ids = new List<int> { 0 };
        var labels = new List<string> { "(재생 안 함)" };
        foreach (var row in InTables.Animation.All)
        {
            ids.Add(row.Index);
            string trigger = row.Parameters != null && row.Parameters.Length > 0 ? row.Parameters[0].Name : "?";
            labels.Add($"#{row.Index} {trigger} ×{row.Speed:0.##}");
        }

        if (ids.Contains(InAnimationId) == false)
        {
            ids.Add(InAnimationId);
            labels.Add($"#{InAnimationId} (없는 행)");
        }

        int index = EditorGUILayout.Popup(InLabel, ids.IndexOf(InAnimationId), labels.ToArray());
        return ids[Mathf.Max(0, index)];
    }

    /// <summary>AssetTable 의 Projectiles/ 프리팹 중에서 고른다.</summary>
    public static int ProjectilePopup(GUIContent InLabel, int InAssetId, GameTables InTables)
    {
        var ids = new List<int>();
        var labels = new List<string>();
        foreach (var row in InTables.Asset.All)
        {
            if (row.Address != null && row.Address.StartsWith("Projectiles/") == true)
            {
                ids.Add(row.Index);
                labels.Add($"#{row.Index} {row.Address}");
            }
        }

        if (ids.Contains(InAssetId) == false)
        {
            ids.Add(InAssetId);
            labels.Add($"#{InAssetId}");
        }

        int index = EditorGUILayout.Popup(InLabel, ids.IndexOf(InAssetId), labels.ToArray());
        return ids[Mathf.Max(0, index)];
    }

    /// <summary>GE 한 줄 요약: 지속 방식, 부여 태그, 첫 모디파이어.</summary>
    public static string EffectSummary(EffectRow InRow)
    {
        string duration = InRow.DurationPolicy == Framework.Ability.EDurationPolicy.HasDuration ? $"{InRow.Duration:0.##}s" : InRow.DurationPolicy.ToString();
        string tags = InRow.GrantedTags != null && InRow.GrantedTags.Length > 0 ? string.Join("|", InRow.GrantedTags) : string.Empty;
        string modifier = InRow.Mod1Value != 0.0f || InRow.Mod1Type != Framework.Ability.EMagnitudeType.Scalar ? $"{InRow.Mod1Attr} {InRow.Mod1Op} {InRow.Mod1Value:0.##}" : string.Empty;
        string execution = InRow.Execution != EExecution.None ? InRow.Execution.ToString() : string.Empty;
        var parts = new List<string> { duration };
        if (tags.Length > 0)
        {
            parts.Add(tags);
        }

        if (modifier.Length > 0)
        {
            parts.Add(modifier);
        }

        if (execution.Length > 0)
        {
            parts.Add(execution);
        }

        return string.Join(", ", parts);
    }
}
