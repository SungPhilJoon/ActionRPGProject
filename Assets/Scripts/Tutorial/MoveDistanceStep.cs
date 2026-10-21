using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>일정 거리를 이동하면 완료. WASD 학습용.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Move Distance", fileName = "TS_Move")]
    public class MoveDistanceStep : TutorialStep
    {
        [Min(0.5f)] public float RequiredDistance = 8f;

        private Vector3 _lastPosition;
        private float _travelled;

        public override void Enter(TutorialContext ctx)
        {
            _travelled = 0f;
            _lastPosition = ctx.Player != null ? ctx.Player.transform.position : Vector3.zero;
        }

        public override bool Tick(TutorialContext ctx, float deltaTime)
        {
            if (ctx.Player == null) return false;

            Vector3 now = ctx.Player.transform.position;
            Vector3 delta = now - _lastPosition;
            delta.y = 0f;
            _travelled += delta.magnitude;
            _lastPosition = now;

            return _travelled >= RequiredDistance;
        }

        public override float Progress => Mathf.Clamp01(_travelled / RequiredDistance);
        public override string ProgressLabel => $"{Mathf.Min(_travelled, RequiredDistance):0.#} / {RequiredDistance:0.#} m";
    }
}
