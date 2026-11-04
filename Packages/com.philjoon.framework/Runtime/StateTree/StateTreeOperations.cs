using System;

namespace Framework.StateTree
{
    /// <summary>비교 연산 (ECompareOp 마다 한 클래스, 2026-11-01 switch → 다형성). 조건 노드 · 에디터 표시가 같이 쓴다.</summary>
    public abstract class CompareOperation
    {
        protected const float Epsilon = 0.0001f;

        public static CompareOperation Of(ECompareOp InOp)
        {
            var operation = KindRegistry<CompareOperation>.Get((int)InOp);
            return operation != null ? operation : KindRegistry<CompareOperation>.Get((int)ECompareOp.NotEqual);
        }

        public abstract string Symbol { get; }
        public abstract bool Compare(float InLeft, float InRight);
    }

    [Kind(ECompareOp.Less)]
    public sealed class LessOperation : CompareOperation
    {
        public override string Symbol => "<";
        public override bool Compare(float InLeft, float InRight) => InLeft < InRight;
    }

    [Kind(ECompareOp.LessOrEqual)]
    public sealed class LessOrEqualOperation : CompareOperation
    {
        public override string Symbol => "<=";
        public override bool Compare(float InLeft, float InRight) => InLeft <= InRight;
    }

    [Kind(ECompareOp.Greater)]
    public sealed class GreaterOperation : CompareOperation
    {
        public override string Symbol => ">";
        public override bool Compare(float InLeft, float InRight) => InLeft > InRight;
    }

    [Kind(ECompareOp.GreaterOrEqual)]
    public sealed class GreaterOrEqualOperation : CompareOperation
    {
        public override string Symbol => ">=";
        public override bool Compare(float InLeft, float InRight) => InLeft >= InRight;
    }

    [Kind(ECompareOp.Equal)]
    public sealed class EqualOperation : CompareOperation
    {
        public override string Symbol => "==";
        public override bool Compare(float InLeft, float InRight) => Math.Abs(InLeft - InRight) < Epsilon;
    }

    [Kind(ECompareOp.NotEqual)]
    public sealed class NotEqualOperation : CompareOperation
    {
        public override string Symbol => "!=";
        public override bool Compare(float InLeft, float InRight) => Math.Abs(InLeft - InRight) >= Epsilon;
    }

    /// <summary>트랜지션이 이번 틱에 발동하는가 (ETransitionTrigger 마다 한 클래스).</summary>
    public abstract class TransitionTrigger
    {
        public static TransitionTrigger Of(ETransitionTrigger InTrigger)
        {
            return KindRegistry<TransitionTrigger>.Get((int)InTrigger);
        }

        public abstract bool IsTriggered(StateTreeInstance InTree, StateTreeTransition InTransition, ETaskStatus InPathStatus);
    }

    [Kind(ETransitionTrigger.OnTick)]
    public sealed class OnTickTrigger : TransitionTrigger
    {
        public override bool IsTriggered(StateTreeInstance InTree, StateTreeTransition InTransition, ETaskStatus InPathStatus) => true;
    }

    [Kind(ETransitionTrigger.OnEvent)]
    public sealed class OnEventTrigger : TransitionTrigger
    {
        public override bool IsTriggered(StateTreeInstance InTree, StateTreeTransition InTransition, ETaskStatus InPathStatus) => InTree.HasEventThisTick(InTransition.EventId);
    }

    [Kind(ETransitionTrigger.OnStateCompleted)]
    public sealed class OnStateCompletedTrigger : TransitionTrigger
    {
        public override bool IsTriggered(StateTreeInstance InTree, StateTreeTransition InTransition, ETaskStatus InPathStatus) => InPathStatus != ETaskStatus.Running;
    }

    [Kind(ETransitionTrigger.OnStateSucceeded)]
    public sealed class OnStateSucceededTrigger : TransitionTrigger
    {
        public override bool IsTriggered(StateTreeInstance InTree, StateTreeTransition InTransition, ETaskStatus InPathStatus) => InPathStatus == ETaskStatus.Succeeded;
    }

    [Kind(ETransitionTrigger.OnStateFailed)]
    public sealed class OnStateFailedTrigger : TransitionTrigger
    {
        public override bool IsTriggered(StateTreeInstance InTree, StateTreeTransition InTransition, ETaskStatus InPathStatus) => InPathStatus == ETaskStatus.Failed;
    }

    /// <summary>트랜지션이 어디로 가는가 (ETransitionType 마다 한 클래스).</summary>
    public abstract class TransitionTarget
    {
        public static TransitionTarget Of(ETransitionType InType)
        {
            return KindRegistry<TransitionTarget>.Get((int)InType);
        }

        public abstract bool Execute(StateTreeInstance InTree, int InOwnerStateId, StateTreeTransition InTransition, string InReason);
    }

    [Kind(ETransitionType.GotoState)]
    public sealed class GotoStateTarget : TransitionTarget
    {
        public override bool Execute(StateTreeInstance InTree, int InOwnerStateId, StateTreeTransition InTransition, string InReason)
        {
            return InTree.GoToState(InTransition.TargetStateId, InReason);
        }
    }

    [Kind(ETransitionType.NextState)]
    public sealed class NextStateTarget : TransitionTarget
    {
        public override bool Execute(StateTreeInstance InTree, int InOwnerStateId, StateTreeTransition InTransition, string InReason)
        {
            return InTree.GoToState(InTree.FindNextSibling(InOwnerStateId), InReason);
        }
    }

    [Kind(ETransitionType.Root)]
    public sealed class RootTarget : TransitionTarget
    {
        public override bool Execute(StateTreeInstance InTree, int InOwnerStateId, StateTreeTransition InTransition, string InReason)
        {
            return InTree.RestartFromRoot($"{InTree.Asset.GetPath(InOwnerStateId)} → Root ({InReason})");
        }
    }
}
