using System;
using System.IO;
using System.Linq;
using Framework.UI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UI Factory 툴 (Game > Tools > UI Factory). 프리팹 방식(2026-10-29) 기준:
///  1) 새 창 스크립트: 직렬화 필드 + OnInitialize 골격을 만든다. 레이아웃은 UIPrefabBuilder 에 build 메서드를 추가해 굽는다.
///  2) 프리팹: 전체 다시 굽기, 창 프리팹 열기(프리팹 모드에서 디자이너가 바로 편집), 스킨 선택.
/// </summary>
public class UIFactoryWindow : EditorWindow
{
    private string WindowName = "MyWindow";
    private Type[] WindowTypes;
    private Vector2 Scroll;

    [MenuItem("Window/Game/UI Factory")]
    [MenuItem("Game/Tools/UI Factory", priority = 31)]
    public static void Open()
    {
        GetWindow<UIFactoryWindow>("UI Factory");
    }

    private void OnEnable()
    {
        WindowTypes = TypeCache.GetTypesDerivedFrom<UIWindow>().Where(t => t.IsAbstract == false).OrderBy(t => t.Name).ToArray();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. 새 창 스크립트", EditorStyles.boldLabel);
        WindowName = EditorGUILayout.TextField("Class Name", WindowName);
        if (GUILayout.Button("Generate Script") == true)
        {
            generate();
        }

        EditorGUILayout.HelpBox("스크립트를 만든 뒤: ① UIPrefabBuilder 에 build 메서드 추가 ② EUIAsset + AssetTable.csv 에 행 추가 ③ GameBootstrap.setupUIAsync 에서 ui.Create — UI.md 3절", MessageType.None);

        EditorGUILayout.Space(12.0f);
        EditorGUILayout.LabelField("2. 프리팹", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(Application.isPlaying == true))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Rebuild All UI Prefabs", GUILayout.Height(28.0f)) == true)
            {
                UIPrefabBuilder.BuildAll();
                AddressablesSetup.Run();
            }

            if (GUILayout.Button("Select UI Skin", GUILayout.Height(28.0f)) == true)
            {
                UISkinSetup.Select();
            }

            EditorGUILayout.EndHorizontal();
        }

        Scroll = EditorGUILayout.BeginScrollView(Scroll);
        foreach (var type in WindowTypes)
        {
            string path = $"{UIPrefabBuilder.Folder}/Windows/{type.Name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(type.Name, prefab != null ? "프리팹 있음" : "프리팹 없음 (Rebuild 필요)");
            using (new EditorGUI.DisabledScope(prefab == null))
            {
                if (GUILayout.Button("Open", GUILayout.Width(60.0f)) == true)
                {
                    AssetDatabase.OpenAsset(prefab);
                }

                if (GUILayout.Button("Ping", GUILayout.Width(50.0f)) == true)
                {
                    EditorGUIUtility.PingObject(prefab);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void generate()
    {
        string className = WindowName.EndsWith("Window", StringComparison.Ordinal) == true ? WindowName : WindowName + "Window";
        string path = $"{EditorPaths.UIScripts}/{className}.cs";
        if (File.Exists(path) == true)
        {
            EditorUtility.DisplayDialog("UI Factory", $"{path} already exists.", "OK");
            return;
        }

        File.WriteAllText(path, template(className));
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[UI Factory] generated {path}. Add a build method to UIPrefabBuilder and register it (UI.md 3절).");
    }

    private static string template(string InClass)
    {
        return
$@"using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>UI Factory 로 생성된 창. 자식 참조는 프리팹(UI/Windows/{InClass})에 직렬화한다 (UIPrefabBuilder).</summary>
public class {InClass} : UIWindow
{{
    public TextMeshProUGUI Title;

    /// <summary>Instantiate 직후 한 번. 동적 목록 · 코드 리스너만 준비한다.</summary>
    protected override void OnInitialize()
    {{
    }}

    protected override void OnOpen()
    {{
    }}
}}
";
    }
}
