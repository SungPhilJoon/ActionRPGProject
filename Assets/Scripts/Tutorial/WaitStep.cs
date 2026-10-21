using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>그냥 시간이 지나기를 기다린다. 연출 사이 간격용.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Wait", fileName = "TS_Wait")]
    public class WaitStep : TutorialStep
    {
        [Min(0.1f)] public float Seconds = 2f;

        private float _elapsed;

        public override void Enter(TutorialContext ctx) => _elapsed = 0f;

        public override bool Tick(TutorialContext ctx, float deltaTime)
        {
            _elapsed += deltaTime;
            return _elapsed >= Seconds;
        }

        public override float Progress => Mathf.Clamp01(_elapsed / Seconds);
    }
}
