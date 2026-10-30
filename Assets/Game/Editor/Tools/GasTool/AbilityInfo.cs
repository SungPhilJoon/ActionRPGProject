using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;

/// <summary>
/// 어빌리티 한 개의 요약 정보 (그래프 어빌리티 / 기존 타입 기반 공통). StateTree Tool 몬스터 패널과 테스트 탭이 쓴다.
///  - 재생하는 AnimationTable 번호 (그래프: PlayAnimationStep, 기존: AbilityRow.Anims)
///  - 예상 타이밍 (첫 피해 / 종료), AI 사용 거리 (MonsterBrain.GetAbilityRange 와 같은 규칙)
///  - 공격형인지 (피해 단계가 있는지) — 방어/버프는 StateTree 에 조건부 상태로 붙인다
/// </summary>
public static class AbilityInfo
{
    public static AbilityGraphAsset LoadGraph(GameTables InTables, AbilityRow InRow)
    {
        if (InRow == null || InRow.Type != EAbilityType.Graph || InRow.Graph == 0)
        {
            return null;
        }

        AssetRow asset;
        if (InTables.Asset.TryGet(InRow.Graph, out asset) == false)
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<AbilityGraphAsset>($"{EditorPaths.Content}/{asset.Address}.asset");
    }

    public static List<int> AnimationIds(GameTables InTables, AbilityRow InRow)
    {
        var result = new List<int>();
        var graph = LoadGraph(InTables, InRow);
        if (graph != null)
        {
            foreach (var step in graph.Steps)
            {
                var play = step as PlayAnimationStep;
                if (play != null && play.AnimationId != 0)
                {
                    result.Add(play.AnimationId);
                }
            }
        }
        else if (InRow != null && InRow.Anims != null)
        {
            result.AddRange(InRow.Anims);
        }

        return result;
    }

    /// <summary>예상 첫 피해 시점(없으면 -1)과 종료 시점 (공격 속도 ×1).</summary>
    public static void Timing(GameTables InTables, AbilityRow InRow, out float OutFirstHit, out float OutEnd)
    {
        OutFirstHit = -1.0f;
        OutEnd = 0.0f;
        var graph = LoadGraph(InTables, InRow);
        if (graph != null)
        {
            float time = 0.0f;
            foreach (var step in graph.Steps)
            {
                if (step == null)
                {
                    continue;
                }

                if (step.Kind == EAbilityStepKind.Hit && OutFirstHit < 0.0f)
                {
                    OutFirstHit = time;
                }

                time += step.PlannedWait();
            }

            OutEnd = time;
            return;
        }

        var csv = CsvRow.Load(EditorPaths.Csv + "/AbilityTable.csv", InRow.Index);
        if (csv != null)
        {
            AbilityGuide.Timeline(InRow.Type, csv, out OutEnd);
            OutFirstHit = AbilityGuide.FirstHit(InRow.Type, csv);
        }
    }

    /// <summary>MonsterBrain.GetAbilityRange 와 같은 규칙.</summary>
    public static float AiRange(AbilityRow InRow, float InAttackRange)
    {
        if (InRow.Type == EAbilityType.AreaBurst && InRow.Range <= 0.0f)
        {
            return InRow.Radius;
        }

        return InRow.Range > 0.0f ? InRow.Range : InAttackRange;
    }

    /// <summary>피해를 주는가. 그래프는 피해 단계 유무, 기존 타입은 Damage &gt; 0.</summary>
    public static bool IsAttack(GameTables InTables, AbilityRow InRow)
    {
        var graph = LoadGraph(InTables, InRow);
        if (graph != null)
        {
            foreach (var step in graph.Steps)
            {
                if (step != null && step.Kind == EAbilityStepKind.Hit)
                {
                    return true;
                }
            }

            return false;
        }

        return InRow.Damage > 0.0f;
    }

    /// <summary>한 줄 요약: 그래프는 단계 목록, 기존 타입은 타입 이름.</summary>
    public static string Summary(GameTables InTables, AbilityRow InRow)
    {
        var graph = LoadGraph(InTables, InRow);
        if (graph == null)
        {
            return $"{InRow.Type} (기존 타입) — 피해 {InRow.Damage:0}, CD {InRow.Cooldown:0.##}s";
        }

        var parts = new List<string>();
        foreach (var step in graph.Steps)
        {
            if (step != null && step.Kind != EAbilityStepKind.Wait && step.Kind != EAbilityStepKind.Other)
            {
                parts.Add(step.Describe());
            }
        }

        return parts.Count > 0 ? string.Join(" → ", parts) : "(단계 없음)";
    }
}
