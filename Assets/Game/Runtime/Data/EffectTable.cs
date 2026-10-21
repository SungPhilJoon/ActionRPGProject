using System;
using Framework;
using Framework.Ability;
using UnityEngine;

[Serializable]
public class EffectRow : ITableRow
{
    public int Index;
    public EDurationPolicy DurationPolicy;
    public float Duration;
    public float Period;
    public bool ExecuteOnApply;
    public EExecution Execution;
    public ETag[] GrantedTags;
    public ETag[] RequiredTags;
    public ETag[] BlockedTags;
    public ETag[] RemoveTags;
    public EStackPolicy Stack;
    public int MaxStack;
    public int Cue;
    public bool IsCooldown;
    public EAttribute Mod1Attr;
    public EModifierOp Mod1Op;
    public EMagnitudeType Mod1Type;
    public int Mod1Key;
    public float Mod1Value;
    public EAttribute Mod2Attr;
    public EModifierOp Mod2Op;
    public EMagnitudeType Mod2Type;
    public int Mod2Key;
    public float Mod2Value;
    public EAttribute Mod3Attr;
    public EModifierOp Mod3Op;
    public EMagnitudeType Mod3Type;
    public int Mod3Key;
    public float Mod3Value;
    /// <summary>Execution=Damage 인 이펙트(도트/폭발)의 공격력 계수 %. 0 이면 사용 안 함.</summary>
    public float Coefficient;
    public EDamageType DamageType;

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>게임플레이 이펙트 (언리얼 GameplayEffect). 모디파이어 최대 3개.</summary>
[CreateAssetMenu(menuName = "Game/Tables/EffectTable")]
public class EffectTable : TableAsset<EffectRow>
{
}
