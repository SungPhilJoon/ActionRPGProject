using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>피해 1건 기록 (밸런스 툴용).</summary>
public struct CombatRecord
{
    public float Time;
    public long SourceId;
    public int SourceCharacter;
    public long TargetId;
    public int TargetCharacter;
    public int AbilityId;
    public float Raw;
    public float Final;
    public float Absorbed;
    public bool IsCritical;
    public EDamageType DamageType;
    public bool IsHeal;
}

/// <summary>
/// 전투 기록 링버퍼 + 실시간 집계.
/// 런타임 밸런스 오버레이(F8)와 에디터 밸런스 툴(Window/Game/Balance Tool)이 같은 데이터를 읽는다.
/// "누가 / 어떤 어빌리티로 / 누구에게 / 얼마나" 를 기록하고, 대상별 누적 피해를 바로 조회할 수 있다.
/// </summary>
public static class CombatLog
{
    public const int Capacity = 2048;

    private static readonly CombatRecord[] Records = new CombatRecord[Capacity];
    private static int Head;
    private static int Total;

    /// <summary>(공격자 캐릭터, 어빌리티) → 누적 피해.</summary>
    private static readonly Dictionary<long, float> DamageBySourceAbility = new Dictionary<long, float>();
    /// <summary>대상 액터 → 받은 누적 피해.</summary>
    private static readonly Dictionary<long, float> DamageTakenByTarget = new Dictionary<long, float>();
    /// <summary>대상 액터 → (어빌리티 → 누적 피해).</summary>
    private static readonly Dictionary<long, Dictionary<int, float>> TakenBreakdown = new Dictionary<long, Dictionary<int, float>>();

    public static event Action<CombatRecord> OnRecord;

    public static bool Enabled = true;
    public static float SessionStartTime { get; private set; }

    public static int Count
    {
        get { return Math.Min(Total, Capacity); }
    }

    public static void Add(CombatRecord InRecord)
    {
        if (Enabled == false)
        {
            return;
        }

        if (Total == 0)
        {
            SessionStartTime = InRecord.Time;
        }

        Records[Head] = InRecord;
        Head = (Head + 1) % Capacity;
        Total += 1;

        if (InRecord.IsHeal == false)
        {
            long key = ((long)InRecord.SourceCharacter << 32) | (uint)InRecord.AbilityId;
            float sum;
            DamageBySourceAbility.TryGetValue(key, out sum);
            DamageBySourceAbility[key] = sum + InRecord.Final + InRecord.Absorbed;

            float taken;
            DamageTakenByTarget.TryGetValue(InRecord.TargetId, out taken);
            DamageTakenByTarget[InRecord.TargetId] = taken + InRecord.Final + InRecord.Absorbed;

            Dictionary<int, float> breakdown;
            if (TakenBreakdown.TryGetValue(InRecord.TargetId, out breakdown) == false)
            {
                breakdown = new Dictionary<int, float>();
                TakenBreakdown.Add(InRecord.TargetId, breakdown);
            }

            float abilitySum;
            breakdown.TryGetValue(InRecord.AbilityId, out abilitySum);
            breakdown[InRecord.AbilityId] = abilitySum + InRecord.Final + InRecord.Absorbed;
        }

        OnRecord?.Invoke(InRecord);
    }

    /// <summary>최신순 InIndex 번째 기록 (0 = 가장 최근).</summary>
    public static CombatRecord GetRecent(int InIndex)
    {
        int index = (Head - 1 - InIndex + Capacity * 2) % Capacity;
        return Records[index];
    }

    public static float GetDamageTaken(long InTargetId)
    {
        float value;
        DamageTakenByTarget.TryGetValue(InTargetId, out value);
        return value;
    }

    public static IReadOnlyDictionary<int, float> GetTakenBreakdown(long InTargetId)
    {
        Dictionary<int, float> breakdown;
        if (TakenBreakdown.TryGetValue(InTargetId, out breakdown) == true)
        {
            return breakdown;
        }

        return null;
    }

    public static IReadOnlyDictionary<long, float> GetDamageBySourceAbility()
    {
        return DamageBySourceAbility;
    }

    public static IReadOnlyDictionary<long, float> GetDamageTakenByTarget()
    {
        return DamageTakenByTarget;
    }

    public static void SplitKey(long InKey, out int OutCharacter, out int OutAbility)
    {
        OutCharacter = (int)(InKey >> 32);
        OutAbility = (int)(InKey & 0xffffffff);
    }

    public static void Clear()
    {
        Head = 0;
        Total = 0;
        DamageBySourceAbility.Clear();
        DamageTakenByTarget.Clear();
        TakenBreakdown.Clear();
        SessionStartTime = Time.time;
    }
}
