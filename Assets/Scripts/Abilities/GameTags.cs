using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Abilities
{
    /// <summary>
    /// 게임에서 쓰는 네이티브 게임플레이 태그 (언리얼 UE_DEFINE_GAMEPLAY_TAG 에 해당).
    /// 코드에서 문자열을 직접 쓰지 않고 여기 정의한 값을 쓴다.
    /// </summary>
    public static class GameTags
    {
        // ---------------------------------------------------------------- 상태
        public static readonly GameplayTag State_Dead = GameplayTag.Request("State.Dead");
        public static readonly GameplayTag State_Invulnerable = GameplayTag.Request("State.Invulnerable");
        public static readonly GameplayTag State_Debuff = GameplayTag.Request("State.Debuff");
        public static readonly GameplayTag State_Debuff_Stun = GameplayTag.Request("State.Debuff.Stun");
        public static readonly GameplayTag State_Debuff_Root = GameplayTag.Request("State.Debuff.Root");
        public static readonly GameplayTag State_Debuff_Silence = GameplayTag.Request("State.Debuff.Silence");
        public static readonly GameplayTag State_Debuff_Disarm = GameplayTag.Request("State.Debuff.Disarm");
        public static readonly GameplayTag State_Debuff_Slow = GameplayTag.Request("State.Debuff.Slow");
        public static readonly GameplayTag State_Debuff_Poison = GameplayTag.Request("State.Debuff.Poison");
        public static readonly GameplayTag State_Buff = GameplayTag.Request("State.Buff");
        public static readonly GameplayTag State_Buff_Rally = GameplayTag.Request("State.Buff.Rally");
        public static readonly GameplayTag State_Attacking = GameplayTag.Request("State.Attacking");
        public static readonly GameplayTag State_Casting = GameplayTag.Request("State.Casting");

        // ---------------------------------------------------------------- 어빌리티
        public static readonly GameplayTag Ability = GameplayTag.Request("Ability");
        public static readonly GameplayTag Ability_BasicAttack = GameplayTag.Request("Ability.BasicAttack");
        public static readonly GameplayTag Ability_Skill = GameplayTag.Request("Ability.Skill");

        // ---------------------------------------------------------------- 쿨다운
        public static readonly GameplayTag Cooldown_Global = GameplayTag.Request("Cooldown.Global");
        public static readonly GameplayTag Cooldown_BasicAttack = GameplayTag.Request("Cooldown.BasicAttack");
        public static readonly GameplayTag Cooldown_Skill = GameplayTag.Request("Cooldown.Skill");

        /// <summary>스킬마다 다른 쿨다운 태그. 공용 쿨다운 이펙트에 동적 태그로 붙인다.</summary>
        public static GameplayTag SkillCooldown(ushort skillId) => GameplayTag.Request($"Cooldown.Skill.{skillId}");

        // ---------------------------------------------------------------- SetByCaller 데이터
        public static readonly GameplayTag SetByCaller_Damage = GameplayTag.Request("SetByCaller.Damage");
        public static readonly GameplayTag SetByCaller_AttackPowerScale = GameplayTag.Request("SetByCaller.AttackPowerScale");
        public static readonly GameplayTag SetByCaller_AbilityPowerScale = GameplayTag.Request("SetByCaller.AbilityPowerScale");
        public static readonly GameplayTag SetByCaller_Knockback = GameplayTag.Request("SetByCaller.Knockback");
        public static readonly GameplayTag SetByCaller_Stagger = GameplayTag.Request("SetByCaller.Stagger");
        public static readonly GameplayTag SetByCaller_Heal = GameplayTag.Request("SetByCaller.Heal");
        public static readonly GameplayTag SetByCaller_HealMaxHealthPercent = GameplayTag.Request("SetByCaller.HealMaxHealthPercent");
        public static readonly GameplayTag SetByCaller_Cooldown = GameplayTag.Request("SetByCaller.Cooldown");
        public static readonly GameplayTag SetByCaller_ManaCost = GameplayTag.Request("SetByCaller.ManaCost");
        public static readonly GameplayTag SetByCaller_Duration = GameplayTag.Request("SetByCaller.Duration");

        // ---------------------------------------------------------------- 피해 속성
        public static readonly GameplayTag Damage_Type_Physical = GameplayTag.Request("Damage.Type.Physical");
        public static readonly GameplayTag Damage_Type_Magical = GameplayTag.Request("Damage.Type.Magical");
        public static readonly GameplayTag Damage_Type_True = GameplayTag.Request("Damage.Type.True");

        public static GameplayTag DamageTypeTag(DamageType type) => type switch
        {
            DamageType.Magical => Damage_Type_Magical,
            DamageType.True => Damage_Type_True,
            _ => Damage_Type_Physical
        };

        public static DamageType DamageTypeFromTags(GameplayTagContainer tags)
        {
            if (tags.HasTag(Damage_Type_True)) return DamageType.True;
            if (tags.HasTag(Damage_Type_Magical)) return DamageType.Magical;
            return DamageType.Physical;
        }

        // ---------------------------------------------------------------- 게임플레이 큐
        public static readonly GameplayTag Cue_Character_Hit = GameplayTag.Request("GameplayCue.Character.Hit");
        public static readonly GameplayTag Cue_Character_Death = GameplayTag.Request("GameplayCue.Character.Death");
        public static readonly GameplayTag Cue_Skill_Cast = GameplayTag.Request("GameplayCue.Skill.Cast");
        public static readonly GameplayTag Cue_Skill_Effect = GameplayTag.Request("GameplayCue.Skill.Effect");
        public static readonly GameplayTag Cue_Status = GameplayTag.Request("GameplayCue.Status");

        /// <summary>정적 필드를 건드려 모든 태그를 매니저에 등록시킨다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            _ = State_Dead;
        }
    }

    /// <summary>ASC 입력 슬롯 번호. 스킬은 0~3, 평타는 10.</summary>
    public static class AbilityInputID
    {
        public const int Skill1 = 0;
        public const int Skill2 = 1;
        public const int Skill3 = 2;
        public const int Skill4 = 3;
        public const int BasicAttack = 10;
    }
}
