using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>지정한 지점에 도달하면 완료. 씬의 마커를 이름으로 찾는다.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Reach Point", fileName = "TS_Reach")]
    public class ReachPointStep : TutorialStep
    {
        [Tooltip("씬에 있는 빈 오브젝트의 이름. TutorialDirector 의 마커 목록에서 찾는다.")]
        public string MarkerName = "Marker_Exit";
        [Min(0.5f)] public float Radius = 2.5f;

        private Transform _marker;

        public override void Enter(TutorialContext ctx)
        {
            _marker = ctx.Director != null ? ctx.Director.FindMarker(MarkerName) : null;
            if (_marker == null) Debug.LogWarning($"[ReachPointStep] 마커 '{MarkerName}' 를 찾지 못했습니다.");
        }

        public override bool Tick(TutorialContext ctx, float deltaTime)
        {
            if (_marker == null || ctx.Player == null) return false;
            return Vector3.Distance(ctx.Player.transform.position, _marker.position) <= Radius;
        }
    }
}
