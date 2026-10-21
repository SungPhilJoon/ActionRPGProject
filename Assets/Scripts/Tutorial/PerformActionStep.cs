using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>특정 행동을 정해진 횟수만큼 수행하면 완료.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Perform Action", fileName = "TS_Action")]
    public class PerformActionStep : TutorialStep
    {
        public enum ActionKind : byte { Attack, Jump, Dodge, SwapWeapon, AnySkill, SpecificSkill, Interact }

        public ActionKind Action = ActionKind.Attack;
        [Tooltip("SpecificSkill 일 때 몇 번 슬롯인지 (0~3).")]
        [Range(0, 3)] public int SkillSlot = 0;
        [Min(1)] public int RequiredCount = 3;
        [Tooltip("Attack 일 때 특정 무기로만 인정할지. Unarmed 면 무기 무관.")]
        public WeaponType RequiredWeapon = WeaponType.Unarmed;

        private int _count;

        public override void Enter(TutorialContext ctx)
        {
            _count = 0;
            GameEventBus.PlayerAttacked += OnAttacked;
            GameEventBus.PlayerJumped += OnJumped;
            GameEventBus.PlayerDodged += OnDodged;
            GameEventBus.PlayerWeaponChanged += OnWeaponChanged;
            GameEventBus.PlayerSkillCast += OnSkillCast;
            GameEventBus.PlayerInteracted += OnInteracted;
        }

        public override void Exit(TutorialContext ctx)
        {
            GameEventBus.PlayerAttacked -= OnAttacked;
            GameEventBus.PlayerJumped -= OnJumped;
            GameEventBus.PlayerDodged -= OnDodged;
            GameEventBus.PlayerWeaponChanged -= OnWeaponChanged;
            GameEventBus.PlayerSkillCast -= OnSkillCast;
            GameEventBus.PlayerInteracted -= OnInteracted;
        }

        public override bool Tick(TutorialContext ctx, float deltaTime) => _count >= RequiredCount;

        private bool IsLocalPlayer(GameObject who) => who != null && who == ActorRegistry.LocalPlayer;

        private void OnAttacked(GameObject who, WeaponType weapon)
        {
            if (Action != ActionKind.Attack || !IsLocalPlayer(who)) return;
            if (RequiredWeapon != WeaponType.Unarmed && weapon != RequiredWeapon) return;
            _count++;
        }

        private void OnJumped(GameObject who)
        {
            if (Action == ActionKind.Jump && IsLocalPlayer(who)) _count++;
        }

        private void OnDodged(GameObject who)
        {
            if (Action == ActionKind.Dodge && IsLocalPlayer(who)) _count++;
        }

        private void OnWeaponChanged(GameObject who, WeaponType weapon)
        {
            if (Action == ActionKind.SwapWeapon && IsLocalPlayer(who)) _count++;
        }

        private void OnSkillCast(GameObject who, int slot)
        {
            if (!IsLocalPlayer(who)) return;
            if (Action == ActionKind.AnySkill) _count++;
            else if (Action == ActionKind.SpecificSkill && slot == SkillSlot) _count++;
        }

        private void OnInteracted(GameObject who, IInteractable target)
        {
            if (Action == ActionKind.Interact && IsLocalPlayer(who)) _count++;
        }

        public override float Progress => Mathf.Clamp01((float)_count / RequiredCount);
        public override string ProgressLabel => $"{Mathf.Min(_count, RequiredCount)} / {RequiredCount}";
    }
}
