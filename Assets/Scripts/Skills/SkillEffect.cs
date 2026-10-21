using ActionRPG.Abilities;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.Skills
{
    /// <summary>
    /// 스킬 한 번의 시전에 필요한 정보. 효과(SkillEffect)들에게 넘겨진다.
    /// 서버 · 소유 클라이언트에서는 어빌리티가 만들고, 다른 피어에서는 게임플레이 큐 파라미터로 다시 만든다.
    /// </summary>
    public struct SkillCastContext
    {
        public SkillDefinition Skill;
        /// <summary>실행 중인 어빌리티 인스턴스. 큐로 재구성한 컨텍스트에서는 null.</summary>
        public GameplayAbility Ability;
        public AbilitySystemComponent SourceASC;
        public GameObject CasterObject;
        public Transform CasterTransform;
        public Faction Faction;
        public ulong CasterObjectId;
        public int SlotIndex;
        public float Level;

        /// <summary>시전 시작 위치(대개 가슴 높이).</summary>
        public Vector3 Origin;
        /// <summary>수평 정규화된 조준 방향.</summary>
        public Vector3 Direction;
        /// <summary>지면 조준 지점. GroundPoint 타게팅에서만 의미가 있다.</summary>
        public Vector3 TargetPoint;
        /// <summary>단일 대상 타게팅에서 선택된 적. 없으면 0.</summary>
        public ulong TargetObjectId;

        /// <summary>이 시전에서 나가는 이펙트용 컨텍스트 (시전자 · 어빌리티 · 맞은 위치).</summary>
        public GameplayEffectContext MakeEffectContext(Vector3 hitPoint, Vector3 hitDirection)
        {
            var context = SourceASC != null ? SourceASC.MakeEffectContext() : new GameplayEffectContext();
            if (Skill != null) context.SetAbility(Skill, Mathf.Max(1, Mathf.RoundToInt(Level)));
            context.AddSourceObject(Skill);
            context.AddOrigin(Origin);
            context.AddHitResult(hitPoint, hitDirection);
            return context;
        }

        /// <summary>대상에게 상태이상 이펙트를 건다. 서버 전용.</summary>
        public void ApplyEffectToTarget(GameplayEffect effect, GameObject target, Vector3 hitPoint, Vector3 hitDirection)
        {
            if (effect == null || SourceASC == null || !SourceASC.IsAuthority) return;
            var spec = SourceASC.MakeOutgoingSpec(effect, Mathf.Max(1f, Level), MakeEffectContext(hitPoint, hitDirection));
            CombatSpecs.ApplyToTarget(SourceASC, spec, target);
        }
    }

    /// <summary>
    /// 스킬이 실제로 무슨 일을 하는지 정의하는 조각.
    /// 하나의 스킬에 여러 개를 붙여 조합하므로 각 효과는 한 가지 일만 한다.
    ///
    /// GAS 위에서 역할이 셋으로 나뉜다.
    ///  - Execute      : 서버에서만. 게임플레이 이펙트(피해 · 회복 · 상태이상)를 적용하고 네트워크 오브젝트를 스폰한다.
    ///  - ExecuteLocal : 조종하는 피어에서만 (소유 클라이언트 · 리슨 서버 호스트 · AI 의 서버). 돌진처럼 이동 권한이 필요한 일.
    ///  - ExecuteVisual: 모든 피어에서. GameplayCue.Skill.Effect 큐를 통해 VFX 를 재생한다. 게임 상태를 바꾸면 안 된다.
    /// </summary>
    public abstract class SkillEffect : ScriptableObject
    {
        [Tooltip("스킬 발동 시점으로부터 이만큼 지난 뒤 실행된다. 다단히트 구성에 사용.")]
        [Min(0f)] public float DelaySeconds;

        /// <summary>서버 전용. ctx 는 시전 시점의 스냅샷이다.</summary>
        public virtual void Execute(in SkillCastContext ctx) { }

        /// <summary>조종하는 피어 전용 (예측 실행 포함).</summary>
        public virtual void ExecuteLocal(in SkillCastContext ctx) { }

        /// <summary>모든 피어에서 호출된다. 게임 상태를 바꾸면 안 된다.</summary>
        public virtual void ExecuteVisual(in SkillCastContext ctx) { }
    }
}
