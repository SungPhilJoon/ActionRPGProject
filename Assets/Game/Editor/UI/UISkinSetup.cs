using System.IO;
using Framework.Editor;
using Framework.UI;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기본 UI 스킨 (Art/Generated/UI/UISkin.asset).
/// 원본: Kenney "UI Pack: RPG Expansion" (CC0, Art/ThirdParty/Kenney/UI_RPG). 밝은 카툰 색이라 그대로 쓰지 않고
/// 흑백으로 바꾼 사본(T_UI_*.png)을 9-slice 스프라이트로 만든 뒤, UISkin 틴트(검붉은 철 · 핏빛)를 곱해 고딕 톤으로 맞춘다.
/// 게이지는 원본이 좌/중/우 3조각이라 한 장으로 합친다.
/// 이미 있는 UISkin 의 값은 덮어쓰지 않는다 (비어 있는 스프라이트 칸만 채움) → 디자이너가 바꾼 스킨이 유지된다.
/// </summary>
public static class UISkinSetup
{
    private const string Source = EditorPaths.Kenney + "/UI_RPG/";
    private const string Folder = EditorPaths.Generated + "/UI";
    public const string SkinPath = Folder + "/UISkin.asset";

    public static UISkin GetOrCreate()
    {
        EditorAssetUtil.EnsureFolder(Folder);
        var skin = AssetDatabase.LoadAssetAtPath<UISkin>(SkinPath);
        if (skin == null)
        {
            skin = ScriptableObject.CreateInstance<UISkin>();
            AssetDatabase.CreateAsset(skin, SkinPath);
        }

        skin.Panel = orDefault(skin.Panel, () => gray("panel_brown", "T_UI_Panel", new Vector4(20, 20, 20, 20)));
        skin.PanelInset = orDefault(skin.PanelInset, () => gray("panelInset_brown", "T_UI_Inset", new Vector4(14, 14, 14, 14)));
        skin.Button = orDefault(skin.Button, () => gray("buttonLong_brown", "T_UI_Button", new Vector4(12, 14, 12, 10)));
        skin.ButtonPressed = orDefault(skin.ButtonPressed, () => gray("buttonLong_brown_pressed", "T_UI_ButtonPressed", new Vector4(12, 10, 12, 10)));
        skin.Slot = orDefault(skin.Slot, () => gray("buttonSquare_brown", "T_UI_Slot", new Vector4(10, 14, 10, 10)));
        skin.BarBack = orDefault(skin.BarBack, () => bar("barBack", "T_UI_BarBack"));
        skin.BarFill = orDefault(skin.BarFill, () => bar("barRed", "T_UI_BarFill"));
        skin.CloseIcon = orDefault(skin.CloseIcon, () => gray("iconCross_grey", "T_UI_Close", Vector4.zero));
        if (skin.Font == null)
        {
            skin.Font = TMPro.TMP_Settings.defaultFontAsset;
        }

        EditorUtility.SetDirty(skin);
        AssetDatabase.SaveAssets();
        return skin;
    }

    [MenuItem("Game/UI/Select UI Skin", priority = 60)]
    public static void Select()
    {
        Selection.activeObject = GetOrCreate();
    }

    private static Sprite orDefault(Sprite InCurrent, System.Func<Sprite> InCreate)
    {
        return InCurrent != null ? InCurrent : InCreate();
    }

    /// <summary>원본 PNG 를 흑백(밝기 유지)으로 바꿔 저장하고 9-slice 스프라이트로 임포트한다.</summary>
    private static Sprite gray(string InSource, string InName, Vector4 InBorder)
    {
        var source = read(Source + InSource + ".png");
        if (source == null)
        {
            return null;
        }

        source.SetPixels(contrastGray(source.GetPixels(), 0.42f));
        return save(source, InName, InBorder);
    }

    /// <summary>게이지 좌 · 중 · 우 조각을 [좌 9px][중 18px][우 9px] 한 장으로 합치고 흑백으로 바꾼다.</summary>
    private static Sprite bar(string InColor, string InName)
    {
        var left = read(Source + InColor + "_horizontalLeft.png");
        var mid = read(Source + InColor + "_horizontalMid.png");
        var right = read(Source + InColor + "_horizontalRight.png");
        if (left == null || mid == null || right == null)
        {
            return null;
        }

        int height = mid.height;
        var result = new Texture2D(left.width + mid.width + right.width, height, TextureFormat.RGBA32, false);
        blit(result, left, 0);
        blit(result, mid, left.width);
        blit(result, right, left.width + mid.width);
        var pixels = result.GetPixels();
        for (int i = 0; i < pixels.Length; ++i)
        {
            float l = Mathf.Clamp01(pixels[i].grayscale * 1.6f);
            pixels[i] = new Color(l, l, l, pixels[i].a);
        }

        result.SetPixels(pixels);
        return save(result, InName, new Vector4(left.width, 0, right.width, 0));
    }

    /// <summary>
    /// 흑백 + 대비 늘리기: 불투명 픽셀의 밝기 범위를 [InFloor, 1] 로 다시 펼친다.
    /// 틴트를 곱했을 때 밝은 테두리 · 리벳은 틴트 색 그대로, 어두운 바탕은 InFloor 배로 어두워져 철판처럼 보인다.
    /// </summary>
    private static Color[] contrastGray(Color[] InPixels, float InFloor)
    {
        float min = 1.0f;
        float max = 0.0f;
        for (int i = 0; i < InPixels.Length; ++i)
        {
            if (InPixels[i].a > 0.5f)
            {
                min = Mathf.Min(min, InPixels[i].grayscale);
                max = Mathf.Max(max, InPixels[i].grayscale);
            }
        }

        float range = Mathf.Max(0.0001f, max - min);
        for (int i = 0; i < InPixels.Length; ++i)
        {
            float t = Mathf.Clamp01((InPixels[i].grayscale - min) / range);
            float l = Mathf.Lerp(InFloor, 1.0f, t);
            InPixels[i] = new Color(l, l, l, InPixels[i].a);
        }

        return InPixels;
    }

    private static void blit(Texture2D InTarget, Texture2D InSource, int InX)
    {
        for (int y = 0; y < InSource.height && y < InTarget.height; ++y)
        {
            for (int x = 0; x < InSource.width; ++x)
            {
                InTarget.SetPixel(InX + x, y, InSource.GetPixel(x, y));
            }
        }
    }

    private static Texture2D read(string InPath)
    {
        if (File.Exists(InPath) == false)
        {
            Debug.LogWarning("[UISkin] missing " + InPath);
            return null;
        }

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        texture.LoadImage(File.ReadAllBytes(InPath));
        return texture;
    }

    private static Sprite save(Texture2D InTexture, string InName, Vector4 InBorder)
    {
        string path = $"{Folder}/{InName}.png";
        File.WriteAllBytes(path, InTexture.EncodeToPNG());
        Object.DestroyImmediate(InTexture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.spriteBorder = InBorder;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
