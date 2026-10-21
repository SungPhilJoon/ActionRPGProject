using System;
using Framework;
using UnityEngine;

[Serializable]
public class AbilityRow : ITableRow
{
    public int Index;
    public int NameId;
    public int DescId;
    public EAbilityType Type;
    public EAbilitySlot Slot;
    public ETag[] AbilityTags;
    public float CastTime;
    public float Cooldown;
    public float Range;
    public float Radius;
    public float Angle;
    public float Speed;
    public int Count;
    public float Spread;
    public float Damage;
    public EDamageType DamageType;
    public int[] HitEffects;
    public int[] SelfEffects;
    /// <summary>AnimationTable Index. 보통 1개, MeleeCombo 는 콤보 단계 순서대로 (예: 1|2|7).</summary>
    public int[] Anims;
    public int CastCue;
    public int HitCue;
    public int Projectile;
    public int Icon;
    public Color IconColor;
    public float ComboWindow;
    public float MoveLock;
    /// <summary>발동(커밋) 직후 자신에게 거는 이펙트 (예: Immaterial). SelfEffects 는 동작이 끝날 때.</summary>
    public int[] StartEffects;
    /// <summary>V Rising 의 "Consume": 맞은 대상이 이 태그를 가지고 있으면 그 상태를 지우고 HitEffects 대신 ConsumeEffects 를 건다.</summary>
    public ETag ConsumeTag = ETag.None;
    public int[] ConsumeEffects;

    /// <summary>InStep 번째 애니메이션 Index (단계 수보다 크면 순환, 없으면 0).</summary>
    public int GetAnim(int InStep)
    {
        if (Anims == null || Anims.Length == 0)
        {
            return 0;
        }

        return Anims[InStep % Anims.Length];
    }

    public int GetKey()
    {
        return Index;
    }
}

/// <summary>어빌리티 (언리얼 GameplayAbility 의 데이터).</summary>
[CreateAssetMenu(menuName = "Game/Tables/AbilityTable")]
public class AbilityTable : TableAsset<AbilityRow>
{
}
