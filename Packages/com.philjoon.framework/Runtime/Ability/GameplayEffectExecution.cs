using System.Collections.Generic;

namespace Framework.Ability
{
    /// <summary>
    /// 언리얼 UGameplayEffectExecutionCalculation 대응.
    /// 피해 공식처럼 여러 속성을 함께 읽어 결과를 내야 하는 계산을 담당한다 (Strategy 패턴).
    /// 결과는 직접 속성을 바꾸지 않고 OutModifiers 로 돌려주며, ASC 가 일괄 적용한다.
    /// </summary>
    public interface IGameplayEffectExecution
    {
        void Execute(GameplayEffectSpec InSpec, AbilitySystemComponent InTarget, List<EvaluatedModifier> OutModifiers);
    }

    /// <summary>ExecutionId(int) → 계산 클래스.</summary>
    public static class ExecutionRegistry
    {
        private static readonly Dictionary<int, IGameplayEffectExecution> Executions = new Dictionary<int, IGameplayEffectExecution>();

        public static void Register(int InId, IGameplayEffectExecution InExecution)
        {
            Executions[InId] = InExecution;
        }

        public static IGameplayEffectExecution Get(int InId)
        {
            IGameplayEffectExecution execution;
            if (Executions.TryGetValue(InId, out execution) == true)
            {
                return execution;
            }

            return null;
        }

        public static void Clear()
        {
            Executions.Clear();
        }
    }
}
