using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>카메라를 일정 각도 이상 돌리면 완료.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Look Around", fileName = "TS_Look")]
    public class LookAroundStep : TutorialStep
    {
        [Min(10f)] public float RequiredDegrees = 120f;

        private float _accumulated;
        private float _lastYaw;

        public override void Enter(TutorialContext ctx)
        {
            _accumulated = 0f;
            var rig = Character.PlayerCameraRig.Instance;
            _lastYaw = rig != null ? rig.transform.eulerAngles.y : 0f;
        }

        public override bool Tick(TutorialContext ctx, float deltaTime)
        {
            var rig = Character.PlayerCameraRig.Instance;
            if (rig == null) return false;

            float yaw = rig.transform.eulerAngles.y;
            _accumulated += Mathf.Abs(Mathf.DeltaAngle(_lastYaw, yaw));
            _lastYaw = yaw;

            return _accumulated >= RequiredDegrees;
        }

        public override float Progress => Mathf.Clamp01(_accumulated / RequiredDegrees);
    }
}
