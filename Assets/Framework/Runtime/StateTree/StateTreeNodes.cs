using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.StateTree
{
    /// <summary>
    /// 블랙보드. 언리얼 StateTree 의 Property Binding(평가기 출력 → 태스크 입력)을 int 키 슬롯으로 단순화했다.
    /// 평가기(Evaluator)가 값을 쓰고, 조건/태스크가 같은 키로 읽는다. 키는 게임 쪽 enum 을 int 로 쓴다.
    /// </summary>
    public class StateTreeBlackboard
    {
        private readonly Dictionary<int, float> Floats = new Dictionary<int, float>();
        private readonly Dictionary<int, Vector3> Vectors = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, object> Objects = new Dictionary<int, object>();

        public void SetFloat(int InKey, float InValue)
        {
            Floats[InKey] = InValue;
        }

        public float GetFloat(int InKey, float InDefault = 0.0f)
        {
            float value;
            return Floats.TryGetValue(InKey, out value) == true ? value : InDefault;
        }

        public void SetBool(int InKey, bool InValue)
        {
            Floats[InKey] = InValue == true ? 1.0f : 0.0f;
        }

        public bool GetBool(int InKey)
        {
            return GetFloat(InKey) > 0.5f;
        }

        public void SetVector(int InKey, Vector3 InValue)
        {
            Vectors[InKey] = InValue;
        }

        public Vector3 GetVector(int InKey)
        {
            Vector3 value;
            return Vectors.TryGetValue(InKey, out value) == true ? value : Vector3.zero;
        }

        public void SetObject(int InKey, object InValue)
        {
            Objects[InKey] = InValue;
        }

        public T GetObject<T>(int InKey) where T : class
        {
            object value;
            return Objects.TryGetValue(InKey, out value) == true ? value as T : null;
        }

        /// <summary>디버거 표시용.</summary>
        public IEnumerable<KeyValuePair<int, float>> EnumerateFloats()
        {
            return Floats;
        }
    }

    /// <summary>
    /// 실행 컨텍스트 (언리얼 FStateTreeExecutionContext). 노드는 이것만 통해 바깥 세계에 접근한다.
    /// Owner 는 트리를 실행하는 컴포넌트(게임 쪽 Schema 역할)이다.
    /// </summary>
    public class StateTreeContext
    {
        public Component Owner;
        public StateTreeBlackboard Blackboard;
        public StateTreeInstance Instance;
        public float DeltaTime;

        /// <summary>현재 틱을 실행 중인 상태의 경과 시간.</summary>
        public float StateElapsed;

        public void SendEvent(int InEventId)
        {
            Instance.SendEvent(InEventId);
        }
    }

    /// <summary>
    /// 모든 노드의 부모. [SerializeReference] 로 에셋에 저장되며, 실행 시 인스턴스마다 복제되므로
    /// 노드 안의 [NonSerialized] 필드를 런타임 인스턴스 데이터로 써도 된다 (언리얼 InstanceData 대응).
    /// </summary>
    [Serializable]
    public abstract class StateTreeNode
    {
        public bool Enabled = true;

        /// <summary>에디터 트리 뷰에 표시할 한 줄 요약.</summary>
        public virtual string Describe()
        {
            return GetType().Name;
        }
    }

    /// <summary>언리얼 FStateTreeTaskBase. 상태가 활성인 동안 실행된다.</summary>
    [Serializable]
    public abstract class StateTreeTask : StateTreeNode
    {
        /// <summary>상태 진입. Running 이 아니면 그 즉시 상태가 완료된다.</summary>
        public virtual ETaskStatus EnterState(StateTreeContext InContext)
        {
            return ETaskStatus.Running;
        }

        public virtual ETaskStatus Tick(StateTreeContext InContext)
        {
            return ETaskStatus.Running;
        }

        public virtual void ExitState(StateTreeContext InContext)
        {
        }
    }

    /// <summary>언리얼 FStateTreeConditionBase.</summary>
    [Serializable]
    public abstract class StateTreeCondition : StateTreeNode
    {
        public EConditionOperand Operand = EConditionOperand.And;
        public bool Invert;

        protected abstract bool Test(StateTreeContext InContext);

        public bool Evaluate(StateTreeContext InContext)
        {
            bool result = Test(InContext);
            return Invert == true ? result == false : result;
        }

        public static bool EvaluateAll(List<StateTreeCondition> InConditions, StateTreeContext InContext)
        {
            if (InConditions == null || InConditions.Count == 0)
            {
                return true;
            }

            bool result = true;
            bool first = true;
            for (int i = 0; i < InConditions.Count; ++i)
            {
                var condition = InConditions[i];
                if (condition == null || condition.Enabled == false)
                {
                    continue;
                }

                bool value = condition.Evaluate(InContext);
                if (first == true)
                {
                    result = value;
                    first = false;
                }
                else if (condition.Operand == EConditionOperand.Or)
                {
                    result = result == true || value == true;
                }
                else
                {
                    result = result == true && value == true;
                }
            }

            return result;
        }
    }

    /// <summary>언리얼 FStateTreeEvaluatorBase. 트리가 도는 동안 매 틱 먼저 실행되어 블랙보드를 채운다.</summary>
    [Serializable]
    public abstract class StateTreeEvaluator : StateTreeNode
    {
        public virtual void TreeStart(StateTreeContext InContext)
        {
        }

        public virtual void Tick(StateTreeContext InContext)
        {
        }

        public virtual void TreeStop(StateTreeContext InContext)
        {
        }
    }

    /// <summary>노드 복제 (에셋 정의 → 실행 인스턴스).</summary>
    public static class StateTreeNodeUtil
    {
        public static T Clone<T>(T InNode) where T : StateTreeNode
        {
            if (InNode == null)
            {
                return null;
            }

            string json = JsonUtility.ToJson(InNode);
            return (T)JsonUtility.FromJson(json, InNode.GetType());
        }

        public static List<T> CloneList<T>(List<T> InNodes) where T : StateTreeNode
        {
            var list = new List<T>();
            if (InNodes == null)
            {
                return list;
            }

            for (int i = 0; i < InNodes.Count; ++i)
            {
                if (InNodes[i] != null && InNodes[i].Enabled == true)
                {
                    list.Add(Clone(InNodes[i]));
                }
            }

            return list;
        }
    }
}
