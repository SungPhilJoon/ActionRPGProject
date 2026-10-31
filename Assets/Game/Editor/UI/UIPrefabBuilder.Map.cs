using Framework.Editor;
using Framework.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 지도 프리팹: 미니맵(UI/Windows/MinimapWindow, 오른쪽 위) · 월드맵(UI/Windows/WorldMapWindow, M).
/// 둘 다 MapView 하나를 쓰고, 설정(반경 · 회전 · 몬스터 · 지역 이름)과 표식 크기만 다르다.
/// 지도 그림은 WorldMapBaker 가 구운 Content/UI/Map/WorldMap.png, 표식 스프라이트는 여기서 절차 생성한다 (Content/UI/Map/Marker*.png).
/// </summary>
public static partial class UIPrefabBuilder
{
    private const int MarkerTextureSize = 64;

    private enum EMarkerShape
    {
        Circle,
        Arrow,
        Diamond,
        Square,
        Ring,
    }

    private static readonly Color[] MarkerColors =
    {
        new Color(1.0f, 0.95f, 0.8f),   // Player
        new Color(0.9f, 0.25f, 0.25f),  // Monster
        new Color(1.0f, 0.1f, 0.2f),    // Boss
        new Color(0.45f, 0.9f, 0.55f),  // Merchant
        new Color(0.75f, 0.45f, 1.0f),  // Castle
        new Color(1.0f, 0.82f, 0.3f),   // Quest
    };

    /// <summary>EMapMarker 순서의 스프라이트.</summary>
    private static Sprite[] buildMapMarkerSprites()
    {
        EditorAssetUtil.EnsureFolder(WorldMapBaker.Folder);
        var shapes = new Sprite[(int)EMarkerShape.Ring + 1];
        for (int i = 0; i < shapes.Length; ++i)
        {
            shapes[i] = saveMarkerSprite((EMarkerShape)i);
        }

        var sprites = new Sprite[(int)EMapMarker.Count];
        sprites[(int)EMapMarker.Player] = shapes[(int)EMarkerShape.Arrow];
        sprites[(int)EMapMarker.Monster] = shapes[(int)EMarkerShape.Circle];
        sprites[(int)EMapMarker.Boss] = shapes[(int)EMarkerShape.Diamond];
        sprites[(int)EMapMarker.Merchant] = shapes[(int)EMarkerShape.Circle];
        sprites[(int)EMapMarker.Castle] = shapes[(int)EMarkerShape.Square];
        sprites[(int)EMapMarker.Quest] = shapes[(int)EMarkerShape.Ring];
        return sprites;
    }

    private static void buildMinimap(Sprite[] InMarkers)
    {
        RectTransform root;
        var window = createWindow<MinimapWindow>(out root);
        window.Frame = UIFactory.CreateWindowFrame(root, "Frame");
        UIFactory.Place(window.Frame, new Vector2(1.0f, 1.0f), new Vector2(-20.0f, -20.0f), new Vector2(270.0f, 270.0f));

        window.View = createMapView(window.Frame, 12.0f, InMarkers, new[] { 18.0f, 9.0f, 20.0f, 14.0f, 14.0f, 20.0f }, false);
        window.View.RotateWithCamera = true;
        window.View.ShowMonsters = true;
        window.View.ClampQuestToEdge = true;
        window.View.ViewRadius = Tables.Const(EConst.MinimapRadius);
        save<MinimapWindow>(root, "Windows/MinimapWindow");
    }

    private static void buildWorldMap(Sprite[] InMarkers)
    {
        RectTransform root;
        var window = createWindow<WorldMapWindow>(out root);
        var frame = UIFactory.CreateWindowFrame(root, "Frame");
        UIFactory.Place(frame, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900.0f, 1000.0f));
        title(frame, EText.WorldMap, 600.0f);
        closeButton(frame, window);

        var area = UIFactory.CreateRect(frame, "MapArea");
        UIFactory.Place(area, new Vector2(0.5f, 0.5f), new Vector2(0.0f, -4.0f), new Vector2(860.0f, 860.0f));
        window.View = createMapView(area, 0.0f, InMarkers, new[] { 24.0f, 0.0f, 28.0f, 18.0f, 18.0f, 26.0f }, true);
        window.View.ShowLabels = true;

        window.Hint = UIFactory.CreateText(frame, "Hint", string.Empty, 16.0f, TextAlignmentOptions.Center);
        window.Hint.color = new Color(1.0f, 1.0f, 1.0f, 0.6f);
        UIFactory.Place(window.Hint, new Vector2(0.5f, 0.0f), new Vector2(0.0f, 14.0f), new Vector2(800.0f, 26.0f));
        save<WorldMapWindow>(root, "Windows/WorldMapWindow");
    }

    /// <summary>
    /// 마스크된 뷰포트 + MapView. 계층: Viewport(Mask, MapView) → Content(회전) → Map(RawImage) / Markers(표식 · 이름, 회전 안 함).
    /// </summary>
    private static MapView createMapView(RectTransform InParent, float InPadding, Sprite[] InMarkers, float[] InSizes, bool InLabels)
    {
        var viewport = UIFactory.CreatePanel(InParent, "Viewport", new Color(0.05f, 0.04f, 0.05f, 1.0f));
        UIFactory.Stretch(viewport, InPadding);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = true;
        var view = viewport.gameObject.AddComponent<MapView>();

        view.Content = UIFactory.CreateRect(viewport, "Content");
        view.Content.anchorMin = new Vector2(0.5f, 0.5f);
        view.Content.anchorMax = new Vector2(0.5f, 0.5f);
        view.Content.sizeDelta = Vector2.zero;

        var mapObject = UIFactory.CreateRect(view.Content, "Map");
        mapObject.anchorMin = new Vector2(0.5f, 0.5f);
        mapObject.anchorMax = new Vector2(0.5f, 0.5f);
        view.Map = mapObject.gameObject.AddComponent<RawImage>();
        view.Map.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WorldMapBaker.MapPath);
        view.Map.raycastTarget = false;
        if (view.Map.texture == null)
        {
            Debug.LogWarning($"[UI] {WorldMapBaker.MapPath} missing. Run 'Game/World/Bake World Map' and rebuild UI prefabs.");
        }

        view.MarkerRoot = UIFactory.CreateRect(viewport, "Markers");
        UIFactory.Stretch(view.MarkerRoot, 0.0f);

        // 퀘스트 가이드 선 (플레이어 → 갈 곳). 회전 · 길이는 MapView 가 매 프레임 맞춘다.
        view.GuideLine = UIFactory.CreateImage(view.MarkerRoot, "GuideLine", new Color(1.0f, 0.82f, 0.3f, 0.65f));
        setCenterAnchor(view.GuideLine.rectTransform);
        view.GuideLine.raycastTarget = false;
        view.GuideLine.gameObject.SetActive(false);
        view.GuideLineWidth = InLabels == true ? 4.0f : 3.0f;
        view.GuideArrow = InMarkers[(int)EMapMarker.Player];

        view.MarkerTemplate = UIFactory.CreateImage(view.MarkerRoot, "Marker", Color.white, InMarkers[0]);
        setCenterAnchor(view.MarkerTemplate.rectTransform);
        view.MarkerTemplate.raycastTarget = false;
        view.MarkerTemplate.gameObject.SetActive(false);

        if (InLabels == true)
        {
            view.LabelTemplate = UIFactory.CreateText(view.MarkerRoot, "Label", string.Empty, 17.0f, TextAlignmentOptions.Center);
            view.LabelTemplate.color = new Color(0.95f, 0.88f, 0.75f, 0.9f);
            view.LabelTemplate.fontStyle = FontStyles.Bold;
            view.LabelTemplate.outlineWidth = 0.25f;
            view.LabelTemplate.outlineColor = Color.black;
            view.LabelTemplate.textWrappingMode = TextWrappingModes.NoWrap;
            setCenterAnchor(view.LabelTemplate.rectTransform);
            view.LabelTemplate.rectTransform.sizeDelta = new Vector2(220.0f, 24.0f);
            view.LabelTemplate.gameObject.SetActive(false);
        }

        view.MarkerSprites = InMarkers;
        view.MarkerColors = (Color[])MarkerColors.Clone();
        view.MarkerSizes = InSizes;
        return view;
    }

    private static void setCenterAnchor(RectTransform InRect)
    {
        InRect.anchorMin = new Vector2(0.5f, 0.5f);
        InRect.anchorMax = new Vector2(0.5f, 0.5f);
        InRect.pivot = new Vector2(0.5f, 0.5f);
    }

    // ------------------------------------------------------------------ marker sprites

    /// <summary>흰색 도형 + 검은 테두리 (색은 Image.color 로 입힌다).</summary>
    private static Sprite saveMarkerSprite(EMarkerShape InShape)
    {
        string path = $"{WorldMapBaker.Folder}/Marker{InShape}.png";
        int size = MarkerTextureSize;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                // -1 ~ 1, 가장자리 테두리 두께 0.18.
                float u = (x + 0.5f) / size * 2.0f - 1.0f;
                float v = (y + 0.5f) / size * 2.0f - 1.0f;
                float distance = shapeDistance(InShape, u, v);
                float pixel = 2.0f / size;
                float outer = Mathf.Clamp01((0.0f - distance) / pixel + 0.5f);
                float inner = Mathf.Clamp01((-0.18f - distance) / pixel + 0.5f);
                float shade = InShape == EMarkerShape.Ring ? 1.0f - Mathf.Clamp01((-0.45f - distance) / pixel + 0.5f) : 1.0f;
                Color fill = Color.Lerp(Color.black, Color.white, inner);
                fill.a = outer * shade;
                pixels[y * size + x] = fill;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        System.IO.File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>도형 안쪽이 음수인 거리 (대략적인 SDF, 단위 = 텍스처 반폭).</summary>
    private static float shapeDistance(EMarkerShape InShape, float InU, float InV)
    {
        switch (InShape)
        {
            case EMarkerShape.Arrow:
                // 위를 가리키는 화살촉: 꼭짓점 (0, 0.95), 아래 모서리 (±0.75, -0.8), 오목점 (0, -0.4).
                float side = Mathf.Abs(InU) * 0.92f + (InV - 0.95f) * 0.39f;
                float bottom = -0.4f - Mathf.Abs(InU) * 0.53f - InV;
                return Mathf.Max(side, bottom);
            case EMarkerShape.Diamond:
                return (Mathf.Abs(InU) + Mathf.Abs(InV)) * 0.7071f - 0.68f;
            case EMarkerShape.Square:
                return Mathf.Max(Mathf.Abs(InU), Mathf.Abs(InV)) - 0.75f;
            default:
                return Mathf.Sqrt(InU * InU + InV * InV) - 0.9f;
        }
    }
}
