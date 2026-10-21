using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Framework.StateTree;
using UnityEditor;
using UnityEngine;

namespace Framework.Editor
{

    /// <summary>
    /// StateTree 편집 툴 (Window/Game/StateTree Editor). 언리얼 StateTree 에디터와 같은 구성:
    ///  - 왼쪽: Evaluators / Global Tasks / 상태 트리 뷰 (자식 추가, 형제 추가, 삭제, 순서 이동, 접기)
    ///  - 오른쪽: 선택한 상태의 Details (이름, 선택 규칙, Enter Conditions, Tasks, Transitions)
    ///  - Play 중: 디버그 대상 선택 → 활성 상태 강조 + 경과 시간, 블랙보드 값, 트랜지션 로그
    /// 노드 필드는 SerializedProperty 로 그리므로 Undo/Redo 가 동작한다.
    /// </summary>
    public class StateTreeEditorWindow : EditorWindow
    {
        private const float LeftWidth = 360.0f;
        private const float RowHeight = 22.0f;
        private const float Indent = 16.0f;

        private static readonly Color ActiveColor = new Color(0.25f, 0.75f, 0.35f, 0.55f);
        private static readonly Color SelectedColor = new Color(0.25f, 0.45f, 0.85f, 0.45f);

        [SerializeField] private StateTreeAsset Asset;
        [SerializeField] private int SelectedStateId;
        [SerializeField] private List<int> Collapsed = new List<int>();

        private SerializedObject Serialized;
        private Vector2 LeftScroll;
        private Vector2 RightScroll;
        private int DebugIndex = -1;
        private string[] EventNames;
        private int[] EventValues;
        private Type BlackboardEnum;
        private Type[] TaskTypes;
        private Type[] ConditionTypes;
        private Type[] EvaluatorTypes;
        private readonly List<StateTreeState> ChildBuffer = new List<StateTreeState>();
        private List<string> ValidationMessages = new List<string>();

        [MenuItem("Window/Framework/StateTree Editor")]
        public static void Open()
        {
            GetWindow<StateTreeEditorWindow>("StateTree");
        }

        public static void Open(StateTreeAsset InAsset)
        {
            var window = GetWindow<StateTreeEditorWindow>("StateTree");
            window.setAsset(InAsset);
        }

        /// <summary>프로젝트 창에서 StateTree 에셋을 더블클릭하면 이 창으로 연다.</summary>
        [UnityEditor.Callbacks.OnOpenAsset]
        private static bool onOpenAsset(int InInstanceId, int InLine)
        {
    #pragma warning disable CS0618 // Unity 6.3 에서 EntityId 로 이전 중. OnOpenAsset(int) 시그니처와 맞추기 위해 유지.
            var asset = EditorUtility.InstanceIDToObject(InInstanceId) as StateTreeAsset;
    #pragma warning restore CS0618
            if (asset == null)
            {
                return false;
            }

            Open(asset);
            return true;
        }

        private void OnEnable()
        {
            TaskTypes = collectTypes(typeof(StateTreeTask));
            ConditionTypes = collectTypes(typeof(StateTreeCondition));
            EvaluatorTypes = collectTypes(typeof(StateTreeEvaluator));
            collectEventEnum();
            Undo.undoRedoPerformed += Repaint;
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= Repaint;
        }

        private void OnInspectorUpdate()
        {
            if (Application.isPlaying == true)
            {
                Repaint();
            }
        }

        private void setAsset(StateTreeAsset InAsset)
        {
            Asset = InAsset;
            Serialized = InAsset != null ? new SerializedObject(InAsset) : null;
            SelectedStateId = InAsset != null && InAsset.States.Count > 0 ? InAsset.States[0].Id : 0;
            ValidationMessages.Clear();
        }

        // ------------------------------------------------------------------ GUI

        private void OnGUI()
        {
            drawToolbar();
            if (Asset == null)
            {
                EditorGUILayout.HelpBox("StateTree 에셋을 선택하거나 New 로 만드세요.\n기본 몬스터 트리: Game > AI > Create Default Monster StateTrees", MessageType.Info);
                return;
            }

            if (Serialized == null || Serialized.targetObject != Asset)
            {
                Serialized = new SerializedObject(Asset);
            }

            Serialized.Update();
            var debug = getDebugInstance();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(LeftWidth));
            LeftScroll = EditorGUILayout.BeginScrollView(LeftScroll);
            drawLeft(debug);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            RightScroll = EditorGUILayout.BeginScrollView(RightScroll);
            drawDetails();
            if (debug != null)
            {
                drawDebug(debug);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            Serialized.ApplyModifiedProperties();
        }

        private void drawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var picked = (StateTreeAsset)EditorGUILayout.ObjectField(Asset, typeof(StateTreeAsset), false, GUILayout.Width(260.0f));
            if (picked != Asset)
            {
                setAsset(picked);
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(45.0f)) == true)
            {
                createNew();
            }

            using (new EditorGUI.DisabledScope(Asset == null))
            {
                if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(65.0f)) == true)
                {
                    ValidationMessages = Asset.Validate();
                    if (ValidationMessages.Count == 0)
                    {
                        ValidationMessages.Add("OK");
                    }
                }

                if (GUILayout.Button("Ping", EditorStyles.toolbarButton, GUILayout.Width(40.0f)) == true)
                {
                    EditorGUIUtility.PingObject(Asset);
                }

                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(45.0f)) == true)
                {
                    EditorUtility.SetDirty(Asset);
                    AssetDatabase.SaveAssetIfDirty(Asset);
                }
            }

            GUILayout.FlexibleSpace();
            drawDebugPicker();
            EditorGUILayout.EndHorizontal();

            foreach (var message in ValidationMessages)
            {
                EditorGUILayout.HelpBox(message, message == "OK" ? MessageType.Info : MessageType.Warning);
            }
        }

        private void drawDebugPicker()
        {
            if (Application.isPlaying == false)
            {
                GUILayout.Label("Debug: (Play 모드에서)", EditorStyles.miniLabel);
                DebugIndex = -1;
                return;
            }

            var entries = StateTreeDebugger.All;
            var labels = new List<string> { "Debug: none" };
            labels.AddRange(entries.Select(e => e.Label));
            int selected = EditorGUILayout.Popup(DebugIndex + 1, labels.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(240.0f)) - 1;
            if (selected != DebugIndex)
            {
                DebugIndex = selected;
                if (selected >= 0 && selected < entries.Count && entries[selected].Instance.Asset != Asset)
                {
                    setAsset(entries[selected].Instance.Asset);
                }

                if (selected >= 0 && selected < entries.Count)
                {
                    Selection.activeGameObject = entries[selected].Owner.gameObject;
                }
            }
        }

        private StateTreeInstance getDebugInstance()
        {
            if (Application.isPlaying == false || DebugIndex < 0)
            {
                return null;
            }

            var entries = StateTreeDebugger.All;
            if (DebugIndex >= entries.Count)
            {
                DebugIndex = -1;
                return null;
            }

            var instance = entries[DebugIndex].Instance;
            return instance.Asset == Asset ? instance : null;
        }

        // ------------------------------------------------------------------ left panel

        private void drawLeft(StateTreeInstance InDebug)
        {
            EditorGUILayout.LabelField(Asset.name, EditorStyles.boldLabel);
            if (string.IsNullOrEmpty(Asset.Description) == false)
            {
                EditorGUILayout.LabelField(Asset.Description, EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.Space(4.0f);
            drawNodeList(Serialized.FindProperty("Evaluators"), "Evaluators", EvaluatorTypes);
            drawNodeList(Serialized.FindProperty("GlobalTasks"), "Global Tasks", TaskTypes);

            EditorGUILayout.Space(6.0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("States", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Root State", GUILayout.Width(100.0f)) == true)
            {
                addState(0);
            }

            EditorGUILayout.EndHorizontal();

            var roots = Asset.States.Where(s => s.ParentId == 0).ToList();
            foreach (var root in roots)
            {
                drawStateRow(root, 0, InDebug);
            }

            EditorGUILayout.Space(6.0f);
            drawStateCommands();
        }

        private void drawStateRow(StateTreeState InState, int InDepth, StateTreeInstance InDebug)
        {
            Asset.GetChildren(InState.Id, ChildBuffer);
            var children = ChildBuffer.ToList();
            bool collapsed = Collapsed.Contains(InState.Id);

            Rect rect = GUILayoutUtility.GetRect(LeftWidth - 20.0f, RowHeight);
            bool isActive = InDebug != null && InDebug.IsActive(InState.Id) == true;
            if (isActive == true)
            {
                EditorGUI.DrawRect(rect, ActiveColor);
            }
            else if (InState.Id == SelectedStateId)
            {
                EditorGUI.DrawRect(rect, SelectedColor);
            }

            Rect colorRect = new Rect(rect.x + InDepth * Indent, rect.y + 3.0f, 4.0f, rect.height - 6.0f);
            EditorGUI.DrawRect(colorRect, InState.Color);

            Rect foldRect = new Rect(colorRect.xMax + 2.0f, rect.y, 14.0f, rect.height);
            if (children.Count > 0)
            {
                bool expanded = EditorGUI.Foldout(foldRect, collapsed == false, GUIContent.none, true);
                if (expanded == collapsed)
                {
                    if (expanded == true)
                    {
                        Collapsed.Remove(InState.Id);
                    }
                    else
                    {
                        Collapsed.Add(InState.Id);
                    }
                }
            }

            string tasks = InState.Tasks.Count > 0 ? "  · " + string.Join(", ", InState.Tasks.Where(t => t != null).Select(t => t.Describe())) : string.Empty;
            string elapsed = isActive == true ? $"  [{InDebug.GetElapsed(InState.Id):0.0}s]" : string.Empty;
            string disabled = InState.Enabled == false ? " (off)" : string.Empty;
            var style = InState.Id == SelectedStateId ? EditorStyles.boldLabel : EditorStyles.label;
            Rect labelRect = new Rect(foldRect.xMax, rect.y, rect.xMax - foldRect.xMax, rect.height);
            GUI.Label(labelRect, new GUIContent($"{InState.Name}{disabled}{elapsed}{tasks}", describeState(InState)), style);

            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition) == true && foldRect.Contains(Event.current.mousePosition) == false)
            {
                SelectedStateId = InState.Id;
                GUI.FocusControl(null);
                Event.current.Use();
            }

            if (collapsed == true)
            {
                return;
            }

            foreach (var child in children)
            {
                drawStateRow(child, InDepth + 1, InDebug);
            }
        }

        private string describeState(StateTreeState InState)
        {
            var lines = new List<string>();
            lines.Add($"Id {InState.Id}, {InState.Selection}");
            foreach (var condition in InState.EnterConditions.Where(c => c != null))
            {
                lines.Add("Enter if: " + condition.Describe());
            }

            foreach (var transition in InState.Transitions)
            {
                lines.Add("→ " + describeTransition(transition));
            }

            return string.Join("\n", lines);
        }

        private void drawStateCommands()
        {
            var selected = Asset.Find(SelectedStateId);
            using (new EditorGUI.DisabledScope(selected == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Child") == true)
                {
                    addState(selected.Id);
                }

                if (GUILayout.Button("+ Sibling") == true)
                {
                    addState(selected.ParentId);
                }

                if (GUILayout.Button("▲", GUILayout.Width(28.0f)) == true)
                {
                    moveState(selected, -1);
                }

                if (GUILayout.Button("▼", GUILayout.Width(28.0f)) == true)
                {
                    moveState(selected, 1);
                }

                if (GUILayout.Button("Delete", GUILayout.Width(60.0f)) == true)
                {
                    deleteState(selected);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        // ------------------------------------------------------------------ details

        private void drawDetails()
        {
            int index = Asset.IndexOf(SelectedStateId);
            if (index < 0)
            {
                EditorGUILayout.HelpBox("왼쪽에서 상태를 선택하세요.", MessageType.None);
                return;
            }

            var stateProp = Serialized.FindProperty("States").GetArrayElementAtIndex(index);
            var state = Asset.States[index];

            EditorGUILayout.LabelField($"State  ·  {Asset.GetPath(state.Id)}  (Id {state.Id})", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(stateProp.FindPropertyRelative("Name"));
            EditorGUILayout.PropertyField(stateProp.FindPropertyRelative("Enabled"));
            EditorGUILayout.PropertyField(stateProp.FindPropertyRelative("Selection"));
            EditorGUILayout.PropertyField(stateProp.FindPropertyRelative("Color"));

            EditorGUILayout.Space(8.0f);
            drawNodeList(stateProp.FindPropertyRelative("EnterConditions"), "Enter Conditions", ConditionTypes);
            drawNodeList(stateProp.FindPropertyRelative("Tasks"), "Tasks", TaskTypes);
            drawTransitions(stateProp.FindPropertyRelative("Transitions"), state);
        }

        private void drawTransitions(SerializedProperty InList, StateTreeState InState)
        {
            EditorGUILayout.Space(4.0f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Transitions ({InList.arraySize})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Transition", GUILayout.Width(100.0f)) == true)
            {
                InList.arraySize += 1;
                var created = InList.GetArrayElementAtIndex(InList.arraySize - 1);
                created.FindPropertyRelative("Enabled").boolValue = true;
                created.FindPropertyRelative("Conditions").ClearArray();
            }

            EditorGUILayout.EndHorizontal();

            string[] stateLabels = Asset.States.Select(s => Asset.GetPath(s.Id)).ToArray();
            int[] stateIds = Asset.States.Select(s => s.Id).ToArray();

            for (int i = 0; i < InList.arraySize; ++i)
            {
                var item = InList.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string summary = i < InState.Transitions.Count ? describeTransition(InState.Transitions[i]) : "new";
                EditorGUILayout.LabelField($"#{i}  {summary}", EditorStyles.miniBoldLabel);
                if (moveButtons(InList, i) == true)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.PropertyField(item.FindPropertyRelative("Enabled"));
                var trigger = item.FindPropertyRelative("Trigger");
                EditorGUILayout.PropertyField(trigger);
                if (trigger.enumValueIndex == (int)ETransitionTrigger.OnEvent)
                {
                    drawEventField(item.FindPropertyRelative("EventId"));
                }

                var type = item.FindPropertyRelative("Type");
                EditorGUILayout.PropertyField(type);
                if (type.enumValueIndex == (int)ETransitionType.GotoState)
                {
                    var target = item.FindPropertyRelative("TargetStateId");
                    int current = Array.IndexOf(stateIds, target.intValue);
                    int picked = EditorGUILayout.Popup("Target State", current, stateLabels);
                    if (picked >= 0 && picked != current)
                    {
                        target.intValue = stateIds[picked];
                    }
                }

                EditorGUILayout.PropertyField(item.FindPropertyRelative("Delay"));
                drawNodeList(item.FindPropertyRelative("Conditions"), "Conditions", ConditionTypes);
                EditorGUILayout.EndVertical();
            }
        }

        private void drawEventField(SerializedProperty InProperty)
        {
            if (EventNames == null)
            {
                EditorGUILayout.PropertyField(InProperty, new GUIContent("Event Id"));
                return;
            }

            int current = Array.IndexOf(EventValues, InProperty.intValue);
            int picked = EditorGUILayout.Popup("Event", current, EventNames);
            if (picked >= 0 && picked != current)
            {
                InProperty.intValue = EventValues[picked];
            }
        }

        /// <summary>[SerializeReference] 노드 리스트 (추가 드롭다운, 삭제, 순서 이동, 필드 편집).</summary>
        private void drawNodeList(SerializedProperty InList, string InTitle, Type[] InTypes)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{InTitle} ({InList.arraySize})", EditorStyles.boldLabel);
            if (GUILayout.Button("+ Add", GUILayout.Width(60.0f)) == true)
            {
                showAddMenu(InList, InTypes);
            }

            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < InList.arraySize; ++i)
            {
                var element = InList.GetArrayElementAtIndex(i);
                var node = element.managedReferenceValue as StateTreeNode;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                string title = node != null ? $"{ObjectNames.NicifyVariableName(node.GetType().Name)}  —  {node.Describe()}" : "(missing type)";
                element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, title, true);
                if (moveButtons(InList, i) == true)
                {
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }

                EditorGUILayout.EndHorizontal();
                if (node != null)
                {
                    var info = node.GetType().GetCustomAttribute<StateTreeNodeInfoAttribute>();
                    if (info != null && element.isExpanded == true)
                    {
                        EditorGUILayout.LabelField(info.Description, EditorStyles.wordWrappedMiniLabel);
                    }
                }

                if (element.isExpanded == true)
                {
                    drawChildren(element);
                }

                EditorGUILayout.EndVertical();
            }
        }

        private static void drawChildren(SerializedProperty InElement)
        {
            var iterator = InElement.Copy();
            var end = InElement.GetEndProperty();
            bool enterChildren = true;
            EditorGUI.indentLevel += 1;
            while (iterator.NextVisible(enterChildren) == true && SerializedProperty.EqualContents(iterator, end) == false)
            {
                enterChildren = false;
                EditorGUILayout.PropertyField(iterator, true);
            }

            EditorGUI.indentLevel -= 1;
        }

        /// <summary>위/아래/삭제 버튼. 배열 구조가 바뀌면 true (그리기 중단).</summary>
        private static bool moveButtons(SerializedProperty InList, int InIndex)
        {
            if (GUILayout.Button("▲", EditorStyles.miniButtonLeft, GUILayout.Width(22.0f)) == true && InIndex > 0)
            {
                InList.MoveArrayElement(InIndex, InIndex - 1);
                return true;
            }

            if (GUILayout.Button("▼", EditorStyles.miniButtonMid, GUILayout.Width(22.0f)) == true && InIndex < InList.arraySize - 1)
            {
                InList.MoveArrayElement(InIndex, InIndex + 1);
                return true;
            }

            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(22.0f)) == true)
            {
                InList.DeleteArrayElementAtIndex(InIndex);
                return true;
            }

            return false;
        }

        private void showAddMenu(SerializedProperty InList, Type[] InTypes)
        {
            var menu = new GenericMenu();
            string propertyPath = InList.propertyPath;
            foreach (var type in InTypes)
            {
                var info = type.GetCustomAttribute<StateTreeNodeInfoAttribute>();
                string category = info != null ? info.Category + "/" : string.Empty;
                var captured = type;
                menu.AddItem(new GUIContent(category + ObjectNames.NicifyVariableName(type.Name)), false, () =>
                {
                    Serialized.Update();
                    var list = Serialized.FindProperty(propertyPath);
                    list.arraySize += 1;
                    var element = list.GetArrayElementAtIndex(list.arraySize - 1);
                    element.managedReferenceValue = Activator.CreateInstance(captured);
                    element.isExpanded = true;
                    Serialized.ApplyModifiedProperties();
                });
            }

            menu.ShowAsContext();
        }

        // ------------------------------------------------------------------ debug panel

        private void drawDebug(StateTreeInstance InDebug)
        {
            EditorGUILayout.Space(10.0f);
            EditorGUILayout.LabelField("Live Debug", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Active", InDebug.GetActivePathString());
            EditorGUILayout.LabelField("Path Status", InDebug.LastPathStatus.ToString());

            var blackboard = InDebug.Context.Blackboard;
            foreach (var pair in blackboard.EnumerateFloats().OrderBy(p => p.Key))
            {
                string key = blackboardKeyName(pair.Key);
                EditorGUILayout.LabelField(key, pair.Value.ToString("0.##"));
            }

            EditorGUILayout.Space(4.0f);
            EditorGUILayout.LabelField("Transition Log (최근 순)", EditorStyles.miniBoldLabel);
            var log = InDebug.Log;
            for (int i = log.Count - 1; i >= 0 && i >= log.Count - 15; --i)
            {
                EditorGUILayout.LabelField(log[i], EditorStyles.wordWrappedMiniLabel);
            }

            EditorGUILayout.EndVertical();
        }

        // ------------------------------------------------------------------ structural edits

        private void createNew()
        {
            string folder = Asset != null ? System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(Asset)).Replace('\\', '/') : "Assets";
            string path = EditorUtility.SaveFilePanelInProject("New StateTree", "ST_New", "asset", "StateTree 저장 위치", folder);
            if (string.IsNullOrEmpty(path) == true)
            {
                return;
            }

            var asset = CreateInstance<StateTreeAsset>();
            asset.AddState("Root", 0);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            setAsset(asset);
        }

        private void addState(int InParentId)
        {
            Undo.RecordObject(Asset, "Add State");
            var state = Asset.AddState("New State", InParentId);
            SelectedStateId = state.Id;
            Collapsed.Remove(InParentId);
            markDirty();
        }

        private void deleteState(StateTreeState InState)
        {
            if (EditorUtility.DisplayDialog("Delete State", $"'{Asset.GetPath(InState.Id)}' 와 모든 자식을 삭제할까요?", "Delete", "Cancel") == false)
            {
                return;
            }

            Undo.RecordObject(Asset, "Delete State");
            var remove = new HashSet<int>();
            collectDescendants(InState.Id, remove);
            Asset.States.RemoveAll(s => remove.Contains(s.Id));
            foreach (var state in Asset.States)
            {
                foreach (var transition in state.Transitions)
                {
                    if (remove.Contains(transition.TargetStateId) == true)
                    {
                        transition.TargetStateId = 0;
                    }
                }
            }

            SelectedStateId = InState.ParentId;
            markDirty();
        }

        private void collectDescendants(int InId, HashSet<int> OutIds)
        {
            OutIds.Add(InId);
            foreach (var state in Asset.States.Where(s => s.ParentId == InId).ToList())
            {
                collectDescendants(state.Id, OutIds);
            }
        }

        /// <summary>같은 부모를 가진 형제 중 앞/뒤 상태와 리스트 위치를 바꾼다.</summary>
        private void moveState(StateTreeState InState, int InDirection)
        {
            var siblings = Asset.States.Where(s => s.ParentId == InState.ParentId).ToList();
            int index = siblings.IndexOf(InState);
            int swapWith = index + InDirection;
            if (swapWith < 0 || swapWith >= siblings.Count)
            {
                return;
            }

            Undo.RecordObject(Asset, "Move State");
            int a = Asset.States.IndexOf(InState);
            int b = Asset.States.IndexOf(siblings[swapWith]);
            Asset.States[a] = siblings[swapWith];
            Asset.States[b] = InState;
            markDirty();
        }

        private void markDirty()
        {
            EditorUtility.SetDirty(Asset);
            Serialized = new SerializedObject(Asset);
        }

        // ------------------------------------------------------------------ helpers

        private string describeTransition(StateTreeTransition InTransition)
        {
            string trigger = InTransition.Trigger.ToString();
            if (InTransition.Trigger == ETransitionTrigger.OnEvent)
            {
                trigger = $"OnEvent({eventName(InTransition.EventId)})";
            }

            string target;
            switch (InTransition.Type)
            {
                case ETransitionType.GotoState:
                    target = Asset.GetPath(InTransition.TargetStateId);
                    break;
                case ETransitionType.NextState:
                    target = "Next";
                    break;
                default:
                    target = "Root";
                    break;
            }

            string conditions = InTransition.Conditions.Count > 0 ? " if " + string.Join(" & ", InTransition.Conditions.Where(c => c != null).Select(c => c.Describe())) : string.Empty;
            string delay = InTransition.Delay > 0.0f ? $" after {InTransition.Delay}s" : string.Empty;
            string off = InTransition.Enabled == false ? " (off)" : string.Empty;
            return $"{trigger} → {target}{conditions}{delay}{off}";
        }

        private string eventName(int InEventId)
        {
            if (EventValues == null)
            {
                return InEventId.ToString();
            }

            int index = Array.IndexOf(EventValues, InEventId);
            return index >= 0 ? EventNames[index] : InEventId.ToString();
        }

        private string blackboardKeyName(int InKey)
        {
            if (BlackboardEnum != null && Enum.IsDefined(BlackboardEnum, InKey) == true)
            {
                return Enum.GetName(BlackboardEnum, InKey);
            }

            return InKey.ToString();
        }

        private void collectEventEnum()
        {
            EventNames = null;
            EventValues = null;
            BlackboardEnum = TypeCache.GetTypesWithAttribute<StateTreeBlackboardEnumAttribute>().FirstOrDefault(t => t.IsEnum == true);
            foreach (var type in TypeCache.GetTypesWithAttribute<StateTreeEventEnumAttribute>())
            {
                if (type.IsEnum == false)
                {
                    continue;
                }

                EventNames = Enum.GetNames(type);
                EventValues = Enum.GetValues(type).Cast<object>().Select(v => Convert.ToInt32(v)).ToArray();
                break;
            }
        }

        private static Type[] collectTypes(Type InBase)
        {
            return TypeCache.GetTypesDerivedFrom(InBase)
                .Where(t => t.IsAbstract == false && t.IsGenericType == false && t.GetConstructor(Type.EmptyTypes) != null)
                .OrderBy(t => t.Name)
                .ToArray();
        }
    }
}
