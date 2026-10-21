using System;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using UnityEngine;

namespace ActionRPG.AI
{
    /// <summary>
    /// 적 한 종류의 데이터. 같은 프리팹에 다른 정의를 꽂아 변종을 만들 수 있다.
    /// </summary>
    [CreateAssetMenu(menuName = "ActionRPG/AI/Enemy", fileName = "EN_NewEnemy")]
    public class EnemyDefinition : ScriptableObject
    {
        [Serializable]
        public class AttackPattern
        {
            public string Name = "기본 공격";

            [Header("조건")]
            [Tooltip("이 거리 안에 들어와야 쓴다.")]
            public float MinRange = 0f;
            public float MaxRange = 2.5f;
            [Tooltip("패턴 자체의 재사용 대기 시간.")]
            [Min(0f)] public float Cooldown = 3f;
            [Tooltip("선택 가중치. 여러 패턴이 조건을 만족하면 이 비율로 뽑힌다.")]
            [Min(0.01f)] public float Weight = 1f;

            [Header("타이밍")]
            [Tooltip("공격 모션 시작부터 판정까지. 플레이어가 피할 수 있는 시간이다.")]
            [Min(0f)] public float WindupTime = 0.7f;
            [Tooltip("판정 후 경직. 이 동안 움직이지 않는다.")]
            [Min(0f)] public float RecoveryTime = 0.8f;
            public int AnimationIndex = 0;

            [Header("피해")]
            public float FlatDamage = 8f;
            public float AttackPowerScale = 1f;
            public DamageType DamageType = DamageType.Physical;
            public float Knockback = 3f;
            [Tooltip("맞은 대상에게 걸 게임플레이 이펙트 (기절 등).")]
            public GameplayEffect ApplyStatus;

            [Header("범위")]
            public float HitRadius = 2.4f;
            [Range(10f, 360f)] public float ArcDegrees = 110f;
            [Tooltip("지정하면 근접 대신 발사체를 쏜다.")]
            public GameObject ProjectilePrefab;
            public float ProjectileSpeed = 18f;
        }

        [Header("정보")]
        public string DisplayName = "고블린 전사";
        public Sprite Portrait;
        [Tooltip("보스는 화면 상단에 별도 체력바를 띄운다.")]
        public bool IsBoss;

        [Header("스탯")]
        public StatBlock Stats = StatBlock.DefaultEnemy;

        [Header("감지 / 추적")]
        [Tooltip("이 거리 안의 플레이어를 발견한다.")]
        public float DetectionRadius = 12f;
        [Tooltip("시야각 밖이면 소리로만 반응한다고 보고 절반 거리만 적용한다.")]
        [Range(30f, 360f)] public float FieldOfView = 160f;
        [Tooltip("스폰 위치에서 이만큼 벗어나면 귀환한다. 0 이면 무제한 추격.")]
        public float LeashRadius = 25f;
        [Tooltip("공격 사거리 안에서도 이 거리는 유지하려 한다.")]
        public float PreferredDistance = 1.8f;

        [Header("순찰")]
        public bool Patrol = true;
        public float PatrolRadius = 6f;
        public float PatrolWaitSeconds = 2.5f;

        [Header("공격 패턴")]
        public AttackPattern[] Attacks = { new AttackPattern() };

        [Header("보상")]
        [Min(0)] public int ExperienceReward = 10;

        public AttackPattern PickAttack(float distanceToTarget, float[] cooldownEndTimes, float now)
        {
            float totalWeight = 0f;
            for (int i = 0; i < Attacks.Length; i++)
            {
                if (!IsUsable(i, distanceToTarget, cooldownEndTimes, now)) continue;
                totalWeight += Attacks[i].Weight;
            }
            if (totalWeight <= 0f) return null;

            float roll = UnityEngine.Random.value * totalWeight;
            for (int i = 0; i < Attacks.Length; i++)
            {
                if (!IsUsable(i, distanceToTarget, cooldownEndTimes, now)) continue;
                roll -= Attacks[i].Weight;
                if (roll <= 0f) return Attacks[i];
            }
            return null;
        }

        public int IndexOf(AttackPattern pattern) => Array.IndexOf(Attacks, pattern);

        private bool IsUsable(int index, float distance, float[] cooldownEndTimes, float now)
        {
            var attack = Attacks[index];
            if (attack == null) return false;
            if (distance < attack.MinRange || distance > attack.MaxRange) return false;
            if (cooldownEndTimes != null && index < cooldownEndTimes.Length && now < cooldownEndTimes[index]) return false;
            return true;
        }
    }
}
