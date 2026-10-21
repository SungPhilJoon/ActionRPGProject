using System;

namespace Framework.StateTree
{
    /// <summary>언리얼 EStateTreeRunStatus 대응. 태스크/상태/트리의 실행 결과.</summary>
    public enum ETaskStatus
    {
        Running = 0,
        Succeeded = 1,
        Failed = 2,
    }

    /// <summary>언리얼 EStateTreeStateSelectionBehavior 대응. 상태가 선택될 때 자식을 어떻게 고를지.</summary>
    public enum ESelectionBehavior
    {
        /// <summary>자식을 위에서부터 순서대로 시도해 Enter Condition 을 통과한 첫 자식으로 들어간다.</summary>
        TrySelectChildrenInOrder = 0,
        /// <summary>자식이 있어도 이 상태에서 멈춘다 (리프처럼 동작).</summary>
        TryEnterState = 1,
        /// <summary>자식을 무작위 순서로 시도한다.</summary>
        TrySelectChildrenAtRandom = 2,
    }

    /// <summary>언리얼 EStateTreeTransitionTrigger 대응.</summary>
    public enum ETransitionTrigger
    {
        /// <summary>활성 경로의 상태가 Succeeded 또는 Failed 로 끝났을 때.</summary>
        OnStateCompleted = 0,
        OnStateSucceeded = 1,
        OnStateFailed = 2,
        /// <summary>매 틱 조건을 검사한다.</summary>
        OnTick = 3,
        /// <summary>SendEvent 로 EventId 가 들어온 틱에.</summary>
        OnEvent = 4,
    }

    /// <summary>언리얼 EStateTreeTransitionType 대응 (GotoState / NextState / Succeeded·Failed 는 Root 재시작으로 단순화).</summary>
    public enum ETransitionType
    {
        GotoState = 0,
        /// <summary>트랜지션을 가진 상태의 다음 형제 상태.</summary>
        NextState = 1,
        /// <summary>루트부터 다시 선택 (트리 재시작).</summary>
        Root = 2,
    }

    /// <summary>조건 결합 방식 (언리얼 Condition Operand). 왼쪽부터 순서대로 결합하며 우선순위는 없다.</summary>
    public enum EConditionOperand
    {
        And = 0,
        Or = 1,
    }

    public enum ECompareOp
    {
        Less = 0,
        LessOrEqual = 1,
        Greater = 2,
        GreaterOrEqual = 3,
        Equal = 4,
        NotEqual = 5,
    }

    /// <summary>
    /// 게임 쪽 이벤트 enum 에 붙이면 StateTree 에디터가 OnEvent 트랜지션의 EventId 를 이름 팝업으로 보여 준다.
    /// (Framework 는 게임 enum 을 모르므로 어트리뷰트로 연결한다.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class StateTreeEventEnumAttribute : Attribute
    {
    }

    /// <summary>
    /// 게임 쪽 블랙보드 키 enum 에 붙이면 StateTree 에디터의 Live Debug 가 키 이름으로 표시한다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Enum)]
    public sealed class StateTreeBlackboardEnumAttribute : Attribute
    {
    }

    /// <summary>노드 클래스에 붙이는 에디터 표시 정보 (카테고리 / 설명).</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class StateTreeNodeInfoAttribute : Attribute
    {
        public readonly string Category;
        public readonly string Description;

        public StateTreeNodeInfoAttribute(string InCategory, string InDescription)
        {
            Category = InCategory;
            Description = InDescription;
        }
    }

    public static class CompareUtil
    {
        public static bool Compare(float InLeft, ECompareOp InOp, float InRight)
        {
            switch (InOp)
            {
                case ECompareOp.Less:
                    return InLeft < InRight;
                case ECompareOp.LessOrEqual:
                    return InLeft <= InRight;
                case ECompareOp.Greater:
                    return InLeft > InRight;
                case ECompareOp.GreaterOrEqual:
                    return InLeft >= InRight;
                case ECompareOp.Equal:
                    return Math.Abs(InLeft - InRight) < 0.0001f;
                default:
                    return Math.Abs(InLeft - InRight) >= 0.0001f;
            }
        }

        public static string Symbol(ECompareOp InOp)
        {
            switch (InOp)
            {
                case ECompareOp.Less:
                    return "<";
                case ECompareOp.LessOrEqual:
                    return "<=";
                case ECompareOp.Greater:
                    return ">";
                case ECompareOp.GreaterOrEqual:
                    return ">=";
                case ECompareOp.Equal:
                    return "==";
                default:
                    return "!=";
            }
        }
    }
}
