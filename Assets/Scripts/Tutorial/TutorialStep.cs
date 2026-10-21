using System;
using UnityEngine;

namespace ActionRPG.Tutorial
{
    /// <summary>스텝이 실행되는 동안 필요한 참조 묶음.</summary>
    public class TutorialContext
    {
        public TutorialDirector Director;
        public GameObject Player;
        public float ElapsedInStep;
    }

    /// <summary>
    /// 튜토리얼 한 단계. 에셋에는 설정만 담고, 진행 상태는 복제본에서 관리한다
    /// (Director 가 Instantiate 로 복제해서 쓴다).
    /// </summary>
    public abstract class TutorialStep : ScriptableObject
    {
        [Header("공통")]
        [Tooltip("화면에 표시할 목표 문구.")]
        [TextArea(1, 3)] public string ObjectiveText = "목표";
        [Tooltip("스텝 시작 시 띄울 대사. 비우면 표시하지 않는다.")]
        [TextArea(2, 5)] public string DialogueLine;
        [Min(0f)] public float DialogueDuration = 4f;
        [Tooltip("완료 후 다음 스텝으로 넘어가기 전 대기 시간.")]
        [Min(0f)] public float CompletionDelay = 0.6f;
        [Tooltip("이 시간이 지나면 조건과 무관하게 넘어간다. 0 이면 무제한.")]
        [Min(0f)] public float TimeoutSeconds = 0f;

        /// <summary>스텝 진입. 이벤트 구독은 여기서.</summary>
        public virtual void Enter(TutorialContext ctx) { }

        /// <summary>매 프레임. true 를 반환하면 완료.</summary>
        public abstract bool Tick(TutorialContext ctx, float deltaTime);

        /// <summary>스텝 종료. 구독 해제는 여기서.</summary>
        public virtual void Exit(TutorialContext ctx) { }

        /// <summary>진행률 0~1. UI 가 게이지를 그릴 때 쓴다. 표시할 게 없으면 -1.</summary>
        public virtual float Progress => -1f;

        /// <summary>"2 / 5" 처럼 보여 줄 문자열. 없으면 null.</summary>
        public virtual string ProgressLabel => null;
    }
}
