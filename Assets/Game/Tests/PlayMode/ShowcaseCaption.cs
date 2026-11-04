using System;
using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 시연(OBS 녹화)용 화면 자막. 테스트가 단계 제목 · 설명 줄을 넣고, 매 프레임 InLive 가 돌려준 실시간 값(속성 · 태그)을 오른쪽 패널에 그린다.
/// 테스트 어셈블리에만 있다 (게임 빌드에 들어가지 않음). IMGUI 라 UI 프리팹 · 캔버스와 무관하다.
/// </summary>
public class ShowcaseCaption : BaseBehaviour
{
    private const string KoreanFontPath = "Assets/Game/Art/ThirdParty/Fonts/NotoSansKR/NotoSansKR-VF.ttf";

    private string Step = string.Empty;
    private string Title = string.Empty;
    private readonly List<string> Lines = new List<string>();
    private Func<string> Live;
    private GUIStyle TitleStyle;
    private GUIStyle BodyStyle;
    private GUIStyle StepStyle;
    private Texture2D Background;

    public static ShowcaseCaption Create(Func<string> InLive)
    {
        var go = new GameObject("ShowcaseCaption");
        var caption = go.AddComponent<ShowcaseCaption>();
        caption.Live = InLive;
        return caption;
    }

    /// <summary>새 단계: 제목과 설명 줄을 바꾼다.</summary>
    public void Show(string InStep, string InTitle, params string[] InLines)
    {
        Step = InStep;
        Title = InTitle;
        Lines.Clear();
        Lines.AddRange(InLines);
        Log.Message($"[Showcase] {InStep} {InTitle}");
    }

    /// <summary>현재 단계에 설명 줄 추가 (결과 · 측정값).</summary>
    public void Add(string InLine)
    {
        Lines.Add(InLine);
        Log.Message($"[Showcase]   {InLine}");
    }

    protected override void OnUnityDestroy()
    {
        if (Background != null)
        {
            Destroy(Background);
        }
    }

    private void ensureStyles()
    {
        if (TitleStyle != null)
        {
            return;
        }

        Font font = null;
#if UNITY_EDITOR
        font = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(KoreanFontPath);
#endif
        Background = new Texture2D(1, 1);
        Background.SetPixel(0, 0, new Color(0.05f, 0.03f, 0.04f, 0.78f));
        Background.Apply();

        StepStyle = makeStyle(font, 20, new Color(0.85f, 0.35f, 0.35f), FontStyle.Bold);
        TitleStyle = makeStyle(font, 32, Color.white, FontStyle.Bold);
        BodyStyle = makeStyle(font, 21, new Color(0.9f, 0.88f, 0.85f), FontStyle.Normal);
    }

    private static GUIStyle makeStyle(Font InFont, int InSize, Color InColor, FontStyle InFontStyle)
    {
        var style = new GUIStyle(GUI.skin.label);
        if (InFont != null)
        {
            style.font = InFont;
        }

        style.fontSize = InSize;
        style.fontStyle = InFontStyle;
        style.wordWrap = true;
        style.richText = true;
        style.normal.textColor = InColor;
        return style;
    }

    private void OnGUI()
    {
        ensureStyles();
        float scale = Screen.height / 1080.0f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1.0f));
        float width = Screen.width / scale;

        // 왼쪽 위: 단계 설명
        var box = new Rect(24.0f, 24.0f, Mathf.Min(900.0f, width * 0.55f), 70.0f + Lines.Count * 34.0f);
        GUI.DrawTexture(box, Background);
        GUILayout.BeginArea(new Rect(box.x + 20.0f, box.y + 12.0f, box.width - 40.0f, box.height - 16.0f));
        GUILayout.Label(Step, StepStyle);
        GUILayout.Label(Title, TitleStyle);
        foreach (var line in Lines)
        {
            GUILayout.Label(line, BodyStyle);
        }

        GUILayout.EndArea();

        // 오른쪽 위: 실시간 값
        string live = Live != null ? Live() : string.Empty;
        if (string.IsNullOrEmpty(live) == false)
        {
            var panel = new Rect(width - 444.0f, 24.0f, 420.0f, 330.0f);
            GUI.DrawTexture(panel, Background);
            GUILayout.BeginArea(new Rect(panel.x + 18.0f, panel.y + 12.0f, panel.width - 36.0f, panel.height - 20.0f));
            GUILayout.Label(live, BodyStyle);
            GUILayout.EndArea();
        }
    }
}
