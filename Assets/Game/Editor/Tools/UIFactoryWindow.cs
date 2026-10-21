using Framework.Editor;
using System;
using System.IO;
using System.Linq;
using Framework.UI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UI Factory 툴 (Window/Game/UI Factory).
///  1) Generate: 템플릿(빈 창 / 목록 창)으로 UIWindow 스크립트 골격을 만든다. Build() 안은 UIFactory 호출로 채워져 있다.
///  2) Preview: 기존 UIWindow 를 편집 모드 씬에 조립해서 레이아웃을 바로 확인한다 (코드 → 화면 왕복 시간 단축).
///  3) Bake Prefab: 미리보기 결과를 프리팹으로 저장해 디자이너가 검토/참고할 수 있게 한다.
/// </summary>
public class UIFactoryWindow : EditorWindow
{
    private enum ETemplate
    {
        Empty,
        List,
    }

    private string WindowName = "MyWindow";
    private ETemplate Template = ETemplate.Empty;
    private int TitleTextId = (int)EText.Inventory;
    private Vector2 WindowSize = new Vector2(800.0f, 560.0f);

    private Type[] WindowTypes;
    private int PreviewIndex;
    private GameObject PreviewRoot;

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

    private void OnDisable()
    {
        clearPreview();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("1. Generate Window Script", EditorStyles.boldLabel);
        WindowName = EditorGUILayout.TextField("Class Name", WindowName);
        Template = (ETemplate)EditorGUILayout.EnumPopup("Template", Template);
        TitleTextId = EditorGUILayout.IntField("Title Text Id (StringTable)", TitleTextId);
        if (Template == ETemplate.Empty)
        {
            WindowSize = EditorGUILayout.Vector2Field("Size", WindowSize);
        }

        if (GUILayout.Button("Generate Script") == true)
        {
            generate();
        }

        EditorGUILayout.Space(12.0f);
        EditorGUILayout.LabelField("2. Preview / Bake", EditorStyles.boldLabel);
        if (WindowTypes == null || WindowTypes.Length == 0)
        {
            EditorGUILayout.HelpBox("UIWindow 하위 클래스가 없습니다.", MessageType.Info);
            return;
        }

        PreviewIndex = EditorGUILayout.Popup("Window", PreviewIndex, WindowTypes.Select(t => t.Name).ToArray());
        using (new EditorGUI.DisabledScope(Application.isPlaying == true))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preview In Scene") == true)
            {
                preview(WindowTypes[PreviewIndex]);
            }

            if (GUILayout.Button("Clear Preview") == true)
            {
                clearPreview();
            }

            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Bake Preview To Prefab") == true)
            {
                bake();
            }
        }

        EditorGUILayout.HelpBox("미리보기는 편집 모드에서 Tables 가 없으므로 문자열이 #ID 로 표시됩니다. 레이아웃 확인용입니다.", MessageType.None);
    }

    private void generate()
    {
        string className = WindowName.EndsWith("Window") == true ? WindowName : WindowName + "Window";
        string path = $"{EditorPaths.UIScripts}/{className}.cs";
        if (File.Exists(path) == true)
        {
            EditorUtility.DisplayDialog("UI Factory", $"{path} already exists.", "OK");
            return;
        }

        string body = Template == ETemplate.List ? listTemplate(className) : emptyTemplate(className);
        File.WriteAllText(path, body);
        AssetDatabase.ImportAsset(path);
        Debug.Log($"[UI Factory] generated {path}. Register it in GameBootstrap.setupUI: ui.Create<{className}>(UILayerId.Window);");
    }

    private string emptyTemplate(string InClass)
    {
        return
$@"using Framework.UI;
using TMPro;
using UnityEngine;

/// <summary>UI Factory 로 생성된 창. Build() 에서 UIFactory 로 자식을 조립한다.</summary>
public class {InClass} : UIWindow
{{
    protected override void Build()
    {{
        var frame = UIFactory.CreateWindowFrame(Rect, ""Frame"");
        UIFactory.Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2({WindowSize.x:0}.0f, {WindowSize.y:0}.0f));

        var title = UIFactory.CreateText(frame, ""Title"", Tables.Text({TitleTextId}), 30.0f, TextAlignmentOptions.Left);
        UIFactory.Place(title, new Vector2(0.0f, 1.0f), new Vector2(24.0f, -12.0f), new Vector2(600.0f, 44.0f));

        var close = UIFactory.CreateButton(frame, ""Close"", Tables.Text(EText.Close), 18.0f, Close);
        UIFactory.Place(close, new Vector2(1.0f, 1.0f), new Vector2(-16.0f, -14.0f), new Vector2(110.0f, 38.0f));
    }}

    protected override void OnOpen()
    {{
    }}
}}
";
    }

    private string listTemplate(string InClass)
    {
        return
$@"using UnityEngine;

/// <summary>UI Factory 로 생성된 목록 창. Fill() 에서 AddRow 로 행을 채운다.</summary>
public class {InClass} : ListWindow
{{
    protected override int TitleTextId
    {{
        get {{ return {TitleTextId}; }}
    }}

    protected override void Fill()
    {{
        // 예: AddRow(icon, Color.gray, title, detail, Tables.Text(EText.Buy), () => {{ }}, true);
    }}
}}
";
    }

    private void preview(Type InType)
    {
        clearPreview();
        var canvas = UIFactory.CreateCanvas("[UIFactoryPreview]", 100);
        PreviewRoot = canvas.gameObject;
        var rect = UIFactory.CreateRect(canvas.transform, InType.Name);
        UIFactory.Stretch(rect, 0.0f);
        var window = (UIWindow)rect.gameObject.AddComponent(InType);
        try
        {
            window.Construct();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[UI Factory] preview partially built: {e.Message}");
        }

        Selection.activeGameObject = rect.gameObject;
    }

    private void bake()
    {
        if (PreviewRoot == null)
        {
            preview(WindowTypes[PreviewIndex]);
        }

        EditorAssetUtil.EnsureFolder(EditorPaths.UIPrefabs);
        string path = $"{EditorPaths.UIPrefabs}/{WindowTypes[PreviewIndex].Name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(PreviewRoot, path);
        Debug.Log("[UI Factory] baked " + path);
    }

    private void clearPreview()
    {
        if (PreviewRoot != null)
        {
            DestroyImmediate(PreviewRoot);
        }

        PreviewRoot = null;
    }
}
