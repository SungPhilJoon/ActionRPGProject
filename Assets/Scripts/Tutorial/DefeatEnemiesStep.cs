using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>적을 정해진 수만큼 쓰러뜨리면 완료.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Defeat Enemies", fileName = "TS_Defeat")]
    public class DefeatEnemiesStep : TutorialStep
    {
        [Min(1)] public int RequiredKills = 2;
        [Tooltip("스텝 시작 시 소환할 적. 비워 두면 씬에 이미 있는 적을 상대한다.")]
        public GameObject EnemyPrefab;
        [Min(0)] public int SpawnCount = 0;
        [Tooltip("플레이어 기준 소환 거리.")]
        public float SpawnDistance = 8f;

        private int _kills;

        public override void Enter(TutorialContext ctx)
        {
            _kills = 0;
            GameEventBus.EntityDied += OnEntityDied;

            if (EnemyPrefab != null && SpawnCount > 0 && ctx.Director != null)
                ctx.Director.SpawnTutorialEnemies(EnemyPrefab, SpawnCount, SpawnDistance);
        }

        public override void Exit(TutorialContext ctx) => GameEventBus.EntityDied -= OnEntityDied;

        private void OnEntityDied(GameObject who, DamageInfo info)
        {
            if (who == null) return;
            var damageable = who.GetComponent<IDamageable>();
            if (damageable == null || damageable.Faction != Faction.Enemy) return;
            _kills++;
        }

        public override bool Tick(TutorialContext ctx, float deltaTime) => _kills >= RequiredKills;

        public override float Progress => Mathf.Clamp01((float)_kills / RequiredKills);
        public override string ProgressLabel => $"{Mathf.Min(_kills, RequiredKills)} / {RequiredKills}";
    }
}
