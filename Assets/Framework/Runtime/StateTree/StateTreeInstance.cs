using System.Collections.Generic;
using UnityEngine;

namespace Framework.StateTree
{
    /// <summary>
    /// StateTree 실행기 (언리얼 FStateTreeExecutionContext + InstanceData).
    ///
    /// 한 틱의 순서:
    ///  1) Evaluator Tick → 블랙보드 갱신
    ///  2) Global Task Tick
    ///  3) 활성 경로(루트 → 리프)의 모든 상태의 Task Tick. 상태 안의 태스크 하나라도 끝나면 그 상태가 완료(Failed 우선)
    ///  4) 트랜지션 검사: 리프 → 루트 순서, 상태 안에서는 리스트 순서. 처음 통과한 것 하나만 실행
    ///  5) 완료됐는데 처리한 트랜지션이 없으면 루트부터 다시 선택
    /// 상태 선택: 대상 상태의 Enter Condition 통과 → 자식이 있으면 Selection 규칙으로 자식을 재귀 선택.
    /// </summary>
    public class StateTreeInstance
    {
        private class RuntimeState
        {
            public StateTreeState Def;
            public List<StateTreeCondition> EnterConditions;
            public List<StateTreeTask> Tasks;
            public List<ETaskStatus> TaskStatus = new List<ETaskStatus>();
            public List<RuntimeTransition> Transitions = new List<RuntimeTransition>();
            public List<int> Children = new List<int>();
            public float Elapsed;
        }

        private class RuntimeTransition
        {
            public StateTreeTransition Def;
            public List<StateTreeCondition> Conditions;
            public int OwnerStateId;
        }

        private struct PendingTransition
        {
            public RuntimeTransition Transition;
            public float Remaining;
        }

        public const int DebugLogCapacity = 40;

        public StateTreeAsset Asset { get; private set; }
        public StateTreeContext Context { get; private set; }
        public bool IsRunning { get; private set; }
        public ETaskStatus LastPathStatus { get; private set; }

        private readonly Dictionary<int, RuntimeState> States = new Dictionary<int, RuntimeState>();
        private readonly List<int> Roots = new List<int>();
        private readonly List<StateTreeEvaluator> Evaluators;
        private readonly List<StateTreeTask> GlobalTasks;
        private readonly List<int> ActivePath = new List<int>();
        private readonly List<int> PathBuffer = new List<int>();
        private readonly List<int> Events = new List<int>();
        private readonly List<int> EventsThisTick = new List<int>();
        private readonly List<string> DebugLog = new List<string>();
        private readonly System.Random Random = new System.Random();
        private PendingTransition Pending;
        private bool HasPending;

        public IReadOnlyList<int> Path
        {
            get { return ActivePath; }
        }

        public IReadOnlyList<string> Log
        {
            get { return DebugLog; }
        }

        public StateTreeInstance(StateTreeAsset InAsset, Component InOwner, StateTreeBlackboard InBlackboard)
        {
            Asset = InAsset;
            Context = new StateTreeContext();
            Context.Owner = InOwner;
            Context.Blackboard = InBlackboard != null ? InBlackboard : new StateTreeBlackboard();
            Context.Instance = this;

            Evaluators = StateTreeNodeUtil.CloneList(InAsset.Evaluators);
            GlobalTasks = StateTreeNodeUtil.CloneList(InAsset.GlobalTasks);

            foreach (var def in InAsset.States)
            {
                if (def.Enabled == false)
                {
                    continue;
                }

                var state = new RuntimeState();
                state.Def = def;
                state.EnterConditions = StateTreeNodeUtil.CloneList(def.EnterConditions);
                state.Tasks = StateTreeNodeUtil.CloneList(def.Tasks);
                for (int i = 0; i < state.Tasks.Count; ++i)
                {
                    state.TaskStatus.Add(ETaskStatus.Running);
                }

                foreach (var transitionDef in def.Transitions)
                {
                    if (transitionDef.Enabled == false)
                    {
                        continue;
                    }

                    var transition = new RuntimeTransition();
                    transition.Def = transitionDef;
                    transition.Conditions = StateTreeNodeUtil.CloneList(transitionDef.Conditions);
                    transition.OwnerStateId = def.Id;
                    state.Transitions.Add(transition);
                }

                States.Add(def.Id, state);
            }

            // 계층은 리스트 순서를 유지한 채 연결한다 (부모가 비활성이면 자식도 제외).
            foreach (var def in InAsset.States)
            {
                RuntimeState state;
                if (States.TryGetValue(def.Id, out state) == false)
                {
                    continue;
                }

                if (def.ParentId == 0)
                {
                    Roots.Add(def.Id);
                    continue;
                }

                RuntimeState parent;
                if (States.TryGetValue(def.ParentId, out parent) == true)
                {
                    parent.Children.Add(def.Id);
                }
            }
        }

        // ------------------------------------------------------------------ lifecycle

        public void Start()
        {
            if (IsRunning == true)
            {
                return;
            }

            IsRunning = true;
            for (int i = 0; i < Evaluators.Count; ++i)
            {
                Evaluators[i].TreeStart(Context);
            }

            for (int i = 0; i < GlobalTasks.Count; ++i)
            {
                GlobalTasks[i].EnterState(Context);
            }

            restartFromRoot("Start");
        }

        public void Stop()
        {
            if (IsRunning == false)
            {
                return;
            }

            exitFrom(0);
            for (int i = 0; i < GlobalTasks.Count; ++i)
            {
                GlobalTasks[i].ExitState(Context);
            }

            for (int i = 0; i < Evaluators.Count; ++i)
            {
                Evaluators[i].TreeStop(Context);
            }

            IsRunning = false;
            HasPending = false;
            addLog("Stop");
        }

        public void SendEvent(int InEventId)
        {
            Events.Add(InEventId);
        }

        public void Tick(float InDeltaTime)
        {
            if (IsRunning == false)
            {
                return;
            }

            Context.DeltaTime = InDeltaTime;
            EventsThisTick.Clear();
            EventsThisTick.AddRange(Events);
            Events.Clear();

            for (int i = 0; i < Evaluators.Count; ++i)
            {
                Evaluators[i].Tick(Context);
            }

            for (int i = 0; i < GlobalTasks.Count; ++i)
            {
                GlobalTasks[i].Tick(Context);
            }

            ETaskStatus pathStatus = tickTasks(InDeltaTime);
            LastPathStatus = pathStatus;

            if (HasPending == true)
            {
                Pending.Remaining -= InDeltaTime;
                if (Pending.Remaining <= 0.0f)
                {
                    HasPending = false;
                    executeTransition(Pending.Transition, "Delayed");
                    return;
                }
            }

            if (evaluateTransitions(pathStatus) == true)
            {
                return;
            }

            if (pathStatus != ETaskStatus.Running)
            {
                restartFromRoot($"Completed({pathStatus}) without transition");
            }
        }

        // ------------------------------------------------------------------ debug

        public float GetElapsed(int InStateId)
        {
            RuntimeState state;
            return States.TryGetValue(InStateId, out state) == true ? state.Elapsed : 0.0f;
        }

        public bool IsActive(int InStateId)
        {
            return ActivePath.Contains(InStateId);
        }

        public string GetActivePathString()
        {
            if (ActivePath.Count == 0)
            {
                return "(none)";
            }

            return Asset.GetPath(ActivePath[ActivePath.Count - 1]);
        }

        // ------------------------------------------------------------------ internal

        private ETaskStatus tickTasks(float InDeltaTime)
        {
            ETaskStatus result = ETaskStatus.Running;
            for (int depth = 0; depth < ActivePath.Count; ++depth)
            {
                var state = States[ActivePath[depth]];
                state.Elapsed += InDeltaTime;
                Context.StateElapsed = state.Elapsed;

                bool hasChildrenActive = depth < ActivePath.Count - 1;
                if (state.Tasks.Count == 0 && hasChildrenActive == false)
                {
                    // 태스크 없는 리프는 즉시 성공 (언리얼과 동일하게 "빈 상태 = 완료").
                    result = merge(result, ETaskStatus.Succeeded);
                    continue;
                }

                for (int i = 0; i < state.Tasks.Count; ++i)
                {
                    if (state.TaskStatus[i] == ETaskStatus.Running)
                    {
                        state.TaskStatus[i] = state.Tasks[i].Tick(Context);
                    }

                    result = merge(result, state.TaskStatus[i]);
                }
            }

            return result;
        }

        private static ETaskStatus merge(ETaskStatus InCurrent, ETaskStatus InNext)
        {
            if (InCurrent == ETaskStatus.Failed || InNext == ETaskStatus.Failed)
            {
                return ETaskStatus.Failed;
            }

            if (InCurrent == ETaskStatus.Succeeded || InNext == ETaskStatus.Succeeded)
            {
                return ETaskStatus.Succeeded;
            }

            return ETaskStatus.Running;
        }

        private bool evaluateTransitions(ETaskStatus InPathStatus)
        {
            for (int depth = ActivePath.Count - 1; depth >= 0; --depth)
            {
                var state = States[ActivePath[depth]];
                Context.StateElapsed = state.Elapsed;
                for (int i = 0; i < state.Transitions.Count; ++i)
                {
                    var transition = state.Transitions[i];
                    if (isTriggered(transition.Def, InPathStatus) == false)
                    {
                        continue;
                    }

                    if (StateTreeCondition.EvaluateAll(transition.Conditions, Context) == false)
                    {
                        continue;
                    }

                    if (transition.Def.Delay > 0.0f)
                    {
                        bool alreadyPending = HasPending == true && Pending.Transition == transition;
                        if (alreadyPending == false)
                        {
                            Pending.Transition = transition;
                            Pending.Remaining = transition.Def.Delay;
                            HasPending = true;
                        }

                        return false;
                    }

                    return executeTransition(transition, transition.Def.Trigger.ToString());
                }
            }

            return false;
        }

        private bool isTriggered(StateTreeTransition InTransition, ETaskStatus InPathStatus)
        {
            switch (InTransition.Trigger)
            {
                case ETransitionTrigger.OnTick:
                    return true;
                case ETransitionTrigger.OnEvent:
                    return EventsThisTick.Contains(InTransition.EventId);
                case ETransitionTrigger.OnStateCompleted:
                    return InPathStatus != ETaskStatus.Running;
                case ETransitionTrigger.OnStateSucceeded:
                    return InPathStatus == ETaskStatus.Succeeded;
                case ETransitionTrigger.OnStateFailed:
                    return InPathStatus == ETaskStatus.Failed;
            }

            return false;
        }

        private bool executeTransition(RuntimeTransition InTransition, string InReason)
        {
            int targetId = 0;
            switch (InTransition.Def.Type)
            {
                case ETransitionType.GotoState:
                    targetId = InTransition.Def.TargetStateId;
                    break;
                case ETransitionType.NextState:
                    targetId = findNextSibling(InTransition.OwnerStateId);
                    break;
                case ETransitionType.Root:
                    return restartFromRoot($"{Asset.GetPath(InTransition.OwnerStateId)} → Root ({InReason})");
            }

            if (targetId == 0 || States.ContainsKey(targetId) == false)
            {
                return false;
            }

            PathBuffer.Clear();
            if (buildPathTo(targetId, PathBuffer) == false)
            {
                return false;
            }

            string from = GetActivePathString();
            changePath(PathBuffer, targetId);
            addLog($"{from} → {GetActivePathString()} ({InReason})");
            return true;
        }

        private bool restartFromRoot(string InReason)
        {
            for (int i = 0; i < Roots.Count; ++i)
            {
                PathBuffer.Clear();
                if (trySelect(Roots[i], PathBuffer) == true)
                {
                    string from = GetActivePathString();
                    changePath(PathBuffer, Roots[i]);
                    addLog($"{from} → {GetActivePathString()} ({InReason})");
                    return true;
                }
            }

            return false;
        }

        /// <summary>대상 상태까지의 조상 경로 + 대상에서 시작하는 선택 결과.</summary>
        private bool buildPathTo(int InTargetId, List<int> OutPath)
        {
            var ancestors = new List<int>();
            int parent = States[InTargetId].Def.ParentId;
            int guard = 0;
            while (parent != 0 && guard < 64)
            {
                ancestors.Insert(0, parent);
                RuntimeState parentState;
                if (States.TryGetValue(parent, out parentState) == false)
                {
                    return false;
                }

                parent = parentState.Def.ParentId;
                guard += 1;
            }

            OutPath.AddRange(ancestors);
            return trySelect(InTargetId, OutPath);
        }

        private bool trySelect(int InStateId, List<int> OutPath)
        {
            var state = States[InStateId];
            Context.StateElapsed = 0.0f;
            if (StateTreeCondition.EvaluateAll(state.EnterConditions, Context) == false)
            {
                return false;
            }

            OutPath.Add(InStateId);
            bool isLeaf = state.Children.Count == 0 || state.Def.Selection == ESelectionBehavior.TryEnterState;
            if (isLeaf == true)
            {
                return true;
            }

            var order = new List<int>(state.Children);
            if (state.Def.Selection == ESelectionBehavior.TrySelectChildrenAtRandom)
            {
                for (int i = order.Count - 1; i > 0; --i)
                {
                    int j = Random.Next(i + 1);
                    int temp = order[i];
                    order[i] = order[j];
                    order[j] = temp;
                }
            }

            for (int i = 0; i < order.Count; ++i)
            {
                int count = OutPath.Count;
                if (trySelect(order[i], OutPath) == true)
                {
                    return true;
                }

                OutPath.RemoveRange(count, OutPath.Count - count);
            }

            OutPath.RemoveAt(OutPath.Count - 1);
            return false;
        }

        /// <summary>공통 접두 경로는 유지하고, 대상 상태부터 아래는 다시 진입한다.</summary>
        private void changePath(List<int> InNewPath, int InReenterFrom)
        {
            int common = 0;
            while (common < ActivePath.Count && common < InNewPath.Count && ActivePath[common] == InNewPath[common] && ActivePath[common] != InReenterFrom)
            {
                common += 1;
            }

            exitFrom(common);
            HasPending = false;
            for (int i = common; i < InNewPath.Count; ++i)
            {
                ActivePath.Add(InNewPath[i]);
                enterState(States[InNewPath[i]]);
            }
        }

        private void exitFrom(int InDepth)
        {
            for (int i = ActivePath.Count - 1; i >= InDepth; --i)
            {
                var state = States[ActivePath[i]];
                for (int t = state.Tasks.Count - 1; t >= 0; --t)
                {
                    state.Tasks[t].ExitState(Context);
                }

                ActivePath.RemoveAt(i);
            }
        }

        private void enterState(RuntimeState InState)
        {
            InState.Elapsed = 0.0f;
            Context.StateElapsed = 0.0f;
            for (int i = 0; i < InState.Tasks.Count; ++i)
            {
                InState.TaskStatus[i] = InState.Tasks[i].EnterState(Context);
            }
        }

        private int findNextSibling(int InStateId)
        {
            var state = States[InStateId];
            List<int> siblings;
            if (state.Def.ParentId == 0)
            {
                siblings = Roots;
            }
            else
            {
                siblings = States[state.Def.ParentId].Children;
            }

            int index = siblings.IndexOf(InStateId);
            return index >= 0 && index + 1 < siblings.Count ? siblings[index + 1] : 0;
        }

        private void addLog(string InMessage)
        {
            DebugLog.Add($"{Time.time:0.00}  {InMessage}");
            if (DebugLog.Count > DebugLogCapacity)
            {
                DebugLog.RemoveAt(0);
            }
        }
    }

    /// <summary>에디터 디버거가 실행 중인 인스턴스를 찾기 위한 등록부.</summary>
    public static class StateTreeDebugger
    {
        public struct Entry
        {
            public string Label;
            public Component Owner;
            public StateTreeInstance Instance;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        public static IReadOnlyList<Entry> All
        {
            get
            {
                Entries.RemoveAll(e => e.Owner == null);
                return Entries;
            }
        }

        public static void Register(string InLabel, Component InOwner, StateTreeInstance InInstance)
        {
            Entries.Add(new Entry { Label = InLabel, Owner = InOwner, Instance = InInstance });
        }

        public static void Unregister(StateTreeInstance InInstance)
        {
            Entries.RemoveAll(e => e.Instance == InInstance);
        }
    }
}
