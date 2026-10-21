using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.StateTree
{
    /// <summary>언리얼 FStateTreeTransition.</summary>
    [Serializable]
    public class StateTreeTransition
    {
        public ETransitionTrigger Trigger = ETransitionTrigger.OnStateCompleted;
        /// <summary>OnEvent 트리거일 때 받을 이벤트 (게임 쪽 [StateTreeEventEnum] enum 값).</summary>
        public int EventId;
        public ETransitionType Type = ETransitionType.GotoState;
        /// <summary>GotoState 대상 상태 Id. 이름이 아니라 int Id 로 연결한다.</summary>
        public int TargetStateId;
        /// <summary>조건 통과 후 실제 전이까지 지연 (초). 그 사이 상태가 바뀌면 취소.</summary>
        public float Delay;
        public bool Enabled = true;

        [SerializeReference] public List<StateTreeCondition> Conditions = new List<StateTreeCondition>();
    }

    /// <summary>언리얼 UStateTreeState. 상태는 평평한 리스트로 저장하고 ParentId 로 계층을 만든다 (리스트 순서 = 형제 순서).</summary>
    [Serializable]
    public class StateTreeState
    {
        public int Id;
        /// <summary>표시용 이름. 런타임은 Id 만 쓴다.</summary>
        public string Name = "State";
        public int ParentId;
        public bool Enabled = true;
        public ESelectionBehavior Selection = ESelectionBehavior.TrySelectChildrenInOrder;
        public Color Color = new Color(0.3f, 0.3f, 0.35f);

        [SerializeReference] public List<StateTreeCondition> EnterConditions = new List<StateTreeCondition>();
        [SerializeReference] public List<StateTreeTask> Tasks = new List<StateTreeTask>();
        public List<StateTreeTransition> Transitions = new List<StateTreeTransition>();
    }

    /// <summary>
    /// 언리얼 UStateTree 에셋. 에디터 툴(Window/Game/StateTree Editor)로 편집한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/AI/State Tree")]
    public class StateTreeAsset : ScriptableObject
    {
        [TextArea] public string Description;
        public List<StateTreeState> States = new List<StateTreeState>();
        [SerializeReference] public List<StateTreeEvaluator> Evaluators = new List<StateTreeEvaluator>();
        [SerializeReference] public List<StateTreeTask> GlobalTasks = new List<StateTreeTask>();
        public int NextStateId = 1;

        public StateTreeState Find(int InId)
        {
            for (int i = 0; i < States.Count; ++i)
            {
                if (States[i].Id == InId)
                {
                    return States[i];
                }
            }

            return null;
        }

        public int IndexOf(int InId)
        {
            for (int i = 0; i < States.Count; ++i)
            {
                if (States[i].Id == InId)
                {
                    return i;
                }
            }

            return -1;
        }

        public void GetChildren(int InParentId, List<StateTreeState> OutChildren)
        {
            OutChildren.Clear();
            for (int i = 0; i < States.Count; ++i)
            {
                if (States[i].ParentId == InParentId)
                {
                    OutChildren.Add(States[i]);
                }
            }
        }

        public StateTreeState AddState(string InName, int InParentId)
        {
            var state = new StateTreeState();
            state.Id = NextStateId++;
            state.Name = InName;
            state.ParentId = InParentId;
            States.Add(state);
            return state;
        }

        /// <summary>"Root/Combat/Attack" 형태의 경로 (표시용).</summary>
        public string GetPath(int InId)
        {
            var state = Find(InId);
            if (state == null)
            {
                return "(none)";
            }

            string path = state.Name;
            int parent = state.ParentId;
            int guard = 0;
            while (parent != 0 && guard < 64)
            {
                var parentState = Find(parent);
                if (parentState == null)
                {
                    break;
                }

                path = parentState.Name + "/" + path;
                parent = parentState.ParentId;
                guard += 1;
            }

            return path;
        }

        /// <summary>구조 오류를 문자열 목록으로 돌려준다 (에디터 Validate 버튼).</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<int>();
            var children = new List<StateTreeState>();
            bool hasRoot = false;
            foreach (var state in States)
            {
                if (ids.Add(state.Id) == false)
                {
                    errors.Add($"duplicated state id {state.Id}");
                }

                if (state.ParentId == 0)
                {
                    hasRoot = true;
                }
                else if (Find(state.ParentId) == null)
                {
                    errors.Add($"'{state.Name}' parent {state.ParentId} missing");
                }

                GetChildren(state.Id, children);
                bool isLeaf = children.Count == 0 || state.Selection == ESelectionBehavior.TryEnterState;
                if (isLeaf == true && state.Tasks.Count == 0 && state.Transitions.Count == 0)
                {
                    errors.Add($"'{GetPath(state.Id)}' is a leaf with no task and no transition (completes immediately)");
                }

                foreach (var transition in state.Transitions)
                {
                    bool needsTarget = transition.Type == ETransitionType.GotoState;
                    if (needsTarget == true && Find(transition.TargetStateId) == null)
                    {
                        errors.Add($"'{GetPath(state.Id)}' transition target {transition.TargetStateId} missing");
                    }
                }

                checkNulls(state.Tasks, state, errors);
                checkNulls(state.EnterConditions, state, errors);
            }

            if (hasRoot == false)
            {
                errors.Add("no root state");
            }

            return errors;
        }

        private void checkNulls<T>(List<T> InNodes, StateTreeState InState, List<string> OutErrors) where T : StateTreeNode
        {
            for (int i = 0; i < InNodes.Count; ++i)
            {
                if (InNodes[i] == null)
                {
                    OutErrors.Add($"'{GetPath(InState.Id)}' has a missing node (class renamed or deleted?)");
                }
            }
        }
    }
}
