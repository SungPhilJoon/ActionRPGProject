using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ActionRPG.Core.AbilitySystem;
using UnityEditor;
using UnityEngine;

namespace ActionRPG.Core.EditorTools.AbilitySystem
{
    /// <summary>
    /// Resources/AbilitySystemGlobals.asset 을 프로젝트 내용으로 다시 채운다.
    /// 클라이언트가 네트워크로 받은 이펙트 · 어빌리티 ID 를 에셋으로 되돌리려면 모든 에셋이 이 목록에 있어야 한다.
    /// </summary>
    public static class AbilitySystemGlobalsBuilder
    {
        private const string ResourcesFolder = "Assets/Resources";

        [MenuItem("ActionRPG/Ability System/Globals 갱신 (이펙트 · 어빌리티 · 큐 · 태그 등록)", priority = 30)]
        public static void RefreshMenu()
        {
            var globals = Refresh();
            Selection.activeObject = globals;
            Debug.Log($"[GAS] AbilitySystemGlobals 갱신: 이펙트 {globals.GameplayEffects.Count}, 어빌리티 {globals.GameplayAbilities.Count}, " +
                      $"큐 {globals.GameplayCueNotifies.Count}, 태그 {globals.GameplayTagList.Count}");
        }

        public static AbilitySystemGlobals Refresh()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
            string path = $"{ResourcesFolder}/{AbilitySystemGlobals.ResourcePath}.asset";
            var globals = AssetDatabase.LoadAssetAtPath<AbilitySystemGlobals>(path);
            if (globals == null)
            {
                globals = ScriptableObject.CreateInstance<AbilitySystemGlobals>();
                AssetDatabase.CreateAsset(globals, path);
            }

            globals.GameplayEffects = LoadAll<GameplayEffect>();
            globals.GameplayAbilities = LoadAll<GameplayAbility>();
            globals.GameplayCueNotifies = LoadAll<GameplayCueNotify>();

            // 에셋을 읽으면서 OnEnable 이 태그를 등록했으므로 매니저에 모인 이름을 그대로 저장한다.
            foreach (var effect in globals.GameplayEffects) effect.RegisterTags();
            foreach (var ability in globals.GameplayAbilities) ability.RegisterTags();
            globals.GameplayTagList = GameplayTagManager.AllTagNames
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

            CheckIdCollisions(globals);
            EditorUtility.SetDirty(globals);
            AssetDatabase.SaveAssets();
            return globals;
        }

        private static List<T> LoadAll<T>() where T : UnityEngine.Object
        {
            var result = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && !result.Contains(asset)) result.Add(asset);
            }
            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        private static void CheckIdCollisions(AbilitySystemGlobals globals)
        {
            var seen = new Dictionary<int, UnityEngine.Object>();
            foreach (UnityEngine.Object asset in globals.GameplayEffects.Cast<UnityEngine.Object>().Concat(globals.GameplayAbilities))
            {
                int id = AbilitySystemGlobals.GetAssetId(asset);
                if (seen.TryGetValue(id, out var other) && other != asset)
                    Debug.LogError($"[GAS] 에셋 ID 충돌: {other.name} ↔ {asset.name}. 같은 이름의 에셋은 하나만 두세요.", asset);
                seen[id] = asset;
            }
        }
    }

    /// <summary>프로젝트의 모든 AttributeSet 필드를 찾아 둔다. 드롭다운용.</summary>
    internal static class AttributeCatalog
    {
        private static string[] _attributeNames;
        private static string[] _setTypeNames;
        private static string[] _setTypeLabels;

        public static string[] AttributeNames
        {
            get
            {
                if (_attributeNames == null) Build();
                return _attributeNames;
            }
        }

        public static string[] SetTypeNames
        {
            get
            {
                if (_setTypeNames == null) Build();
                return _setTypeNames;
            }
        }

        public static string[] SetTypeLabels
        {
            get
            {
                if (_setTypeLabels == null) Build();
                return _setTypeLabels;
            }
        }

        private static void Build()
        {
            var attributes = new List<string> { "(None)" };
            var typeNames = new List<string> { string.Empty };
            var labels = new List<string> { "(None)" };
            foreach (var type in TypeCache.GetTypesDerivedFrom<AttributeSet>().OrderBy(t => t.Name))
            {
                if (type.IsAbstract) continue;
                typeNames.Add($"{type.FullName}, {type.Assembly.GetName().Name}");
                labels.Add(type.Name);
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                             .Where(f => f.FieldType == typeof(GameplayAttributeData))
                             .OrderBy(f => f.MetadataToken))
                    attributes.Add($"{type.Name}.{field.Name}");
            }
            _attributeNames = attributes.ToArray();
            _setTypeNames = typeNames.ToArray();
            _setTypeLabels = labels.ToArray();
        }
    }

    /// <summary>GameplayAttribute 를 "세트.어트리뷰트" 드롭다운으로 고르게 한다.</summary>
    [CustomPropertyDrawer(typeof(GameplayAttribute))]
    public class GameplayAttributeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative("attributeName");
            var options = AttributeCatalog.AttributeNames;
            int index = Mathf.Max(0, Array.IndexOf(options, nameProperty.stringValue));
            EditorGUI.BeginProperty(position, label, property);
            int next = EditorGUI.Popup(position, label.text, index, options);
            if (next != index) nameProperty.stringValue = next == 0 ? string.Empty : options[next];
            EditorGUI.EndProperty();
        }
    }

    /// <summary>AbilitySet 의 어트리뷰트 세트 타입을 드롭다운으로 고르게 한다.</summary>
    [CustomPropertyDrawer(typeof(AttributeSetTypeAttribute))]
    public class AttributeSetTypeDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var names = AttributeCatalog.SetTypeNames;
            int index = Mathf.Max(0, Array.IndexOf(names, property.stringValue));
            EditorGUI.BeginProperty(position, label, property);
            int next = EditorGUI.Popup(position, label.text, index, AttributeCatalog.SetTypeLabels);
            if (next != index) property.stringValue = names[next];
            EditorGUI.EndProperty();
        }
    }

    /// <summary>GameplayTag 를 이름 입력 + 등록된 태그 드롭다운으로 고르게 한다.</summary>
    [CustomPropertyDrawer(typeof(GameplayTag))]
    public class GameplayTagDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative("tagName");
            EditorGUI.BeginProperty(position, label, property);
            var fieldRect = new Rect(position.x, position.y, position.width - 22f, position.height);
            var buttonRect = new Rect(position.xMax - 20f, position.y, 20f, position.height);
            nameProperty.stringValue = EditorGUI.TextField(fieldRect, label, nameProperty.stringValue);

            if (GUI.Button(buttonRect, "▾", EditorStyles.miniButton))
            {
                var menu = new GenericMenu();
                var globals = Resources.Load<AbilitySystemGlobals>(AbilitySystemGlobals.ResourcePath);
                var names = new SortedSet<string>(GameplayTagManager.AllTagNames, StringComparer.Ordinal);
                if (globals != null) foreach (var n in globals.GameplayTagList) names.Add(n);
                var serializedObject = property.serializedObject;
                string propertyPath = nameProperty.propertyPath;
                foreach (var tagName in names)
                {
                    string captured = tagName;
                    menu.AddItem(new GUIContent(tagName.Replace('.', '/')), tagName == nameProperty.stringValue, () =>
                    {
                        serializedObject.Update();
                        serializedObject.FindProperty(propertyPath).stringValue = captured;
                        serializedObject.ApplyModifiedProperties();
                    });
                }
                if (names.Count == 0) menu.AddDisabledItem(new GUIContent("등록된 태그 없음 (Globals 갱신 메뉴 실행)"));
                menu.ShowAsContext();
            }
            EditorGUI.EndProperty();
        }
    }

    /// <summary>플레이 중 ASC 의 어트리뷰트 · 태그 · 이펙트 · 어빌리티를 보여 준다 (언리얼 showdebug abilitysystem).</summary>
    [CustomEditor(typeof(AbilitySystemComponent), true)]
    public class AbilitySystemComponentEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            if (!Application.isPlaying) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("런타임 상태", EditorStyles.boldLabel);
            var asc = (AbilitySystemComponent)target;
            EditorGUILayout.HelpBox(asc.GetDebugString(), MessageType.None);
            Repaint();
        }
    }
}
