using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace Framework.StateTree
{
    /// <summary>InMin~InMax 초 대기 후 성공 (언리얼 Delay Task).</summary>
    // 어셈블리 이름 변경(Game.Framework → Framework.Runtime) 전에 저장된 [SerializeReference] 데이터를 그대로 읽기 위한 표시.
    [MovedFrom(true, "Framework.StateTree", "Game.Framework")]
    [Serializable]
    [StateTreeNodeInfo("Common", "대기 후 Succeeded. RandomDeviation 만큼 무작위 가감")]
    public class WaitTask : StateTreeTask
    {
        public float Duration = 1.0f;
        public float RandomDeviation;

        [NonSerialized] private float Remaining;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            Remaining = Duration + UnityEngine.Random.Range(-RandomDeviation, RandomDeviation);
            return ETaskStatus.Running;
        }

        public override ETaskStatus Tick(StateTreeContext InContext)
        {
            Remaining -= InContext.DeltaTime;
            return Remaining <= 0.0f ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return RandomDeviation > 0.0f ? $"Wait {Duration}±{RandomDeviation}s" : $"Wait {Duration}s";
        }
    }

    /// <summary>진입 시 이벤트를 보낸다 (같은 트리의 OnEvent 트랜지션을 깨우는 용도).</summary>
    // 어셈블리 이름 변경(Game.Framework → Framework.Runtime) 전에 저장된 [SerializeReference] 데이터를 그대로 읽기 위한 표시.
    [MovedFrom(true, "Framework.StateTree", "Game.Framework")]
    [Serializable]
    [StateTreeNodeInfo("Common", "진입 시 EventId 를 트리에 보낸다")]
    public class SendEventTask : StateTreeTask
    {
        public int EventId;
        public bool CompleteImmediately;

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            InContext.SendEvent(EventId);
            return CompleteImmediately == true ? ETaskStatus.Succeeded : ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Send Event {EventId}";
        }
    }

    /// <summary>진입 시 콘솔 로그 (디버깅용).</summary>
    // 어셈블리 이름 변경(Game.Framework → Framework.Runtime) 전에 저장된 [SerializeReference] 데이터를 그대로 읽기 위한 표시.
    [MovedFrom(true, "Framework.StateTree", "Game.Framework")]
    [Serializable]
    [StateTreeNodeInfo("Common", "진입 시 Debug.Log")]
    public class DebugLogTask : StateTreeTask
    {
        public string Message = "Enter";

        public override ETaskStatus EnterState(StateTreeContext InContext)
        {
            Debug.Log($"[StateTree] {InContext.Owner.name}: {Message}", InContext.Owner);
            return ETaskStatus.Running;
        }

        public override string Describe()
        {
            return $"Log '{Message}'";
        }
    }

    /// <summary>항상 참 (빈 Enter Condition 대신 명시용).</summary>
    // 어셈블리 이름 변경(Game.Framework → Framework.Runtime) 전에 저장된 [SerializeReference] 데이터를 그대로 읽기 위한 표시.
    [MovedFrom(true, "Framework.StateTree", "Game.Framework")]
    [Serializable]
    [StateTreeNodeInfo("Common", "항상 true")]
    public class AlwaysTrueCondition : StateTreeCondition
    {
        protected override bool Test(StateTreeContext InContext)
        {
            return true;
        }

        public override string Describe()
        {
            return Invert == true ? "Never" : "Always";
        }
    }
}
