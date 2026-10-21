using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>대사만 보여 주고 시간이 지나면 넘어간다. 스토리 전달용.</summary>
    [CreateAssetMenu(menuName = "ActionRPG/Tutorial/Steps/Dialogue", fileName = "TS_Dialogue")]
    public class DialogueStep : TutorialStep
    {
        [Tooltip("대사 시간이 끝나야 넘어간다. 끄면 즉시 통과.")]
        public bool WaitForDialogue = true;

        private float _elapsed;

        public override void Enter(TutorialContext ctx) => _elapsed = 0f;

        public override bool Tick(TutorialContext ctx, float deltaTime)
        {
            if (!WaitForDialogue) return true;
            _elapsed += deltaTime;
            return _elapsed >= DialogueDuration;
        }

        public override float Progress => DialogueDuration > 0f ? Mathf.Clamp01(_elapsed / DialogueDuration) : 1f;
    }
}
