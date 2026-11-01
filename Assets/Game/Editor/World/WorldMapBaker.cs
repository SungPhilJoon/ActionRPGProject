using Framework.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 월드맵 그림 굽기 (Game > World > Bake World Map, 월드 씬 빌드 마지막에도 자동 실행).
/// 지형 위에 정사영 카메라를 두고 북쪽(+Z)이 위가 되게 내려다본 장면을 한 장 렌더링해 Content/UI/Map/WorldMap.png 로 저장한다.
/// 런타임에 지도를 매 프레임 렌더링하지 않기 위해서다 (미니맵 · 월드맵이 같은 그림을 다른 범위로 보여 준다).
///  - 안개는 끄고, LOD 는 가장 자세한 단계로 고정한 채 렌더링한 뒤 원래대로 돌린다.
///  - 풀스크린 패스(대기 안개 Atmosphere 등)는 낮은 곳(호수)을 안개로 덮으므로 굽는 동안 끈다.
///  - 물 셰이더(Game/Water)는 깊이 · 불투명 텍스처를 쓰는 반투명이라 이 카메라에서 그려지지 않는다 → 굽는 동안만 단색 Unlit 머티리얼로 바꾼다.
///  - 약간 어둡게 · 채도를 낮추고 가장자리를 어둡게 해 표식이 잘 보이게 한다.
///  - NavMesh 를 겹쳐 그린다: 걸을 수 없는 곳(NavMesh 없음)은 어둡게, 길(Road 영역)은 밝은 흙색, 경계는 선 → 지도만 봐도 다닐 수 있는 곳이 보인다.
///    그래서 NavMesh 를 구운 뒤에 굽는다 (월드 빌드 순서: NavMesh → 지도).
/// 지형 · 나무 · 랜드마크가 바뀌면 다시 구워야 한다 (월드 빌드가 자동으로 한다).
/// </summary>
public static class WorldMapBaker
{
    public const string Folder = EditorPaths.Content + "/UI/Map";
    public const string MapPath = Folder + "/WorldMap.png";
    private const int Resolution = 2048;
    private static readonly Color WaterColor = new Color(0.16f, 0.27f, 0.36f);
    private const float CameraHeight = 400.0f;

    [MenuItem("Game/World/Bake World Map", priority = 30)]
    public static void BakeFromMenu()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != EditorPaths.WorldScene)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false)
            {
                return;
            }

            EditorSceneManager.OpenScene(EditorPaths.WorldScene);
        }

        var context = Object.FindAnyObjectByType<SceneContext>();
        if (context == null || context.Terrain == null)
        {
            Debug.LogError("[WorldMap] SceneContext / Terrain not found. Run 'Game/Setup/5. Build World Scene' first.");
            return;
        }

        Bake(context.Terrain);
    }

    /// <summary>InTerrain 범위를 내려다본 지도 텍스처를 굽는다 (지금 열린 씬 기준).</summary>
    public static Texture2D Bake(Terrain InTerrain)
    {
        EditorAssetUtil.EnsureFolder(Folder);
        Vector3 origin = InTerrain.transform.position;
        Vector3 size = InTerrain.terrainData.size;

        var cameraObject = new GameObject("[WorldMapBakeCamera]");
        cameraObject.hideFlags = HideFlags.HideAndDontSave;
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(size.x, size.z) * 0.5f;
        camera.aspect = 1.0f;
        camera.nearClipPlane = 1.0f;
        camera.farClipPlane = CameraHeight + size.y + 50.0f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.05f, 0.04f, 0.05f, 1.0f);
        camera.cullingMask = ~(1 << LayerId.Projectile);
        cameraObject.transform.position = new Vector3(origin.x + size.x * 0.5f, origin.y + CameraHeight, origin.z + size.z * 0.5f);
        // 아래를 보고 화면 위 = +Z (북쪽).
        cameraObject.transform.rotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);

        var waterRenderers = collectWaterRenderers();
        var waterMaterials = new Material[waterRenderers.Length];
        var waterFlat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        waterFlat.SetColor("_BaseColor", WaterColor);
        // 물 메시는 위에서 볼 때 뒷면일 수 있다 (게임 셰이더는 양면).
        waterFlat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        waterFlat.doubleSidedGI = true;
        var fullScreenPasses = collectActiveFullScreenPasses();
        bool fog = RenderSettings.fog;
        float lodBias = QualitySettings.lodBias;
        var target = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        Texture2D texture;
        try
        {
            RenderSettings.fog = false;
            QualitySettings.lodBias = 100.0f;
            setActive(fullScreenPasses, false);
            for (int i = 0; i < waterRenderers.Length; ++i)
            {
                waterMaterials[i] = waterRenderers[i].sharedMaterial;
                waterRenderers[i].sharedMaterial = waterFlat;
            }

            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            texture = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            stylize(texture);
            overlayNavMesh(texture, origin, size);
            texture.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            RenderSettings.fog = fog;
            QualitySettings.lodBias = lodBias;
            setActive(fullScreenPasses, true);
            for (int i = 0; i < waterRenderers.Length; ++i)
            {
                waterRenderers[i].sharedMaterial = waterMaterials[i];
            }

            Object.DestroyImmediate(waterFlat);
            Object.DestroyImmediate(cameraObject);
        }

        System.IO.File.WriteAllBytes(MapPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(MapPath, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MapPath);
        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = Resolution;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        Debug.Log("[WorldMap] baked: " + MapPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(MapPath);
    }

    private static System.Collections.Generic.List<ScriptableRendererFeature> collectActiveFullScreenPasses()
    {
        var result = new System.Collections.Generic.List<ScriptableRendererFeature>();
        var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (asset == null)
        {
            return result;
        }

        foreach (var data in asset.rendererDataList)
        {
            if (data == null)
            {
                continue;
            }

            foreach (var feature in data.rendererFeatures)
            {
                if (feature is FullScreenPassRendererFeature && feature.isActive == true)
                {
                    result.Add(feature);
                }
            }
        }

        return result;
    }

    private static void setActive(System.Collections.Generic.List<ScriptableRendererFeature> InFeatures, bool InActive)
    {
        for (int i = 0; i < InFeatures.Count; ++i)
        {
            InFeatures[i].SetActive(InActive);
        }
    }

    private static Renderer[] collectWaterRenderers()
    {
        var result = new System.Collections.Generic.List<Renderer>();
        foreach (var body in Object.FindObjectsByType<WaterBody>(FindObjectsSortMode.None))
        {
            result.AddRange(body.GetComponentsInChildren<Renderer>());
        }

        return result.ToArray();
    }

    private static readonly Color RoadColor = new Color(0.86f, 0.72f, 0.5f);
    private const float BlockedDarken = 0.4f;
    private const float RoadTint = 0.5f;

    private const byte MaskBlocked = 0;
    private const byte MaskWalkable = 1;
    private const byte MaskRoad = 2;

    /// <summary>NavMesh 삼각형을 지도 픽셀 마스크로 그려 걸을 수 없는 곳 · 길 · 경계를 칠한다. NavMesh 가 없으면 건너뛴다.</summary>
    private static void overlayNavMesh(Texture2D InTexture, Vector3 InOrigin, Vector3 InSize)
    {
        var triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.indices == null || triangulation.indices.Length == 0)
        {
            Debug.LogWarning("[WorldMap] no NavMesh in scene — walkable overlay skipped");
            return;
        }

        int width = InTexture.width;
        int height = InTexture.height;
        var mask = new byte[width * height];
        var vertices = triangulation.vertices;
        var indices = triangulation.indices;
        for (int t = 0; t < indices.Length / 3; ++t)
        {
            byte value = triangulation.areas[t] == NavAreaId.Road ? MaskRoad : MaskWalkable;
            var a = toPixel(vertices[indices[t * 3]], InOrigin, InSize, width, height);
            var b = toPixel(vertices[indices[t * 3 + 1]], InOrigin, InSize, width, height);
            var c = toPixel(vertices[indices[t * 3 + 2]], InOrigin, InSize, width, height);
            rasterize(mask, width, height, a, b, c, value);
        }

        var pixels = InTexture.GetPixels();
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int index = y * width + x;
                byte value = mask[index];
                Color color = pixels[index];
                if (value == MaskBlocked)
                {
                    float gray = color.grayscale;
                    color = Color.Lerp(color, new Color(gray, gray, gray * 1.1f), 0.5f) * BlockedDarken;
                }
                else if (isMaskEdge(mask, width, height, x, y) == true)
                {
                    color *= 0.55f;
                }
                else if (value == MaskRoad)
                {
                    color = Color.Lerp(color, RoadColor, RoadTint);
                }

                color.a = 1.0f;
                pixels[index] = color;
            }
        }

        InTexture.SetPixels(pixels);
    }

    private static Vector2 toPixel(Vector3 InWorld, Vector3 InOrigin, Vector3 InSize, int InWidth, int InHeight)
    {
        return new Vector2((InWorld.x - InOrigin.x) / InSize.x * InWidth, (InWorld.z - InOrigin.z) / InSize.z * InHeight);
    }

    /// <summary>걸을 수 있는 픽셀 중 바로 옆이 걸을 수 없는 픽셀 (경계선).</summary>
    private static bool isMaskEdge(byte[] InMask, int InWidth, int InHeight, int InX, int InY)
    {
        for (int dy = -1; dy <= 1; ++dy)
        {
            for (int dx = -1; dx <= 1; ++dx)
            {
                int nx = Mathf.Clamp(InX + dx, 0, InWidth - 1);
                int ny = Mathf.Clamp(InY + dy, 0, InHeight - 1);
                if (InMask[ny * InWidth + nx] == MaskBlocked)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>삼각형 안 픽셀에 InValue (길은 걷기 영역보다 우선).</summary>
    private static void rasterize(byte[] InMask, int InWidth, int InHeight, Vector2 InA, Vector2 InB, Vector2 InC, byte InValue)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(InA.x, Mathf.Min(InB.x, InC.x))));
        int maxX = Mathf.Min(InWidth - 1, Mathf.CeilToInt(Mathf.Max(InA.x, Mathf.Max(InB.x, InC.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(InA.y, Mathf.Min(InB.y, InC.y))));
        int maxY = Mathf.Min(InHeight - 1, Mathf.CeilToInt(Mathf.Max(InA.y, Mathf.Max(InB.y, InC.y))));
        float area = cross(InB - InA, InC - InA);
        if (Mathf.Abs(area) < 0.0001f)
        {
            return;
        }

        for (int y = minY; y <= maxY; ++y)
        {
            for (int x = minX; x <= maxX; ++x)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float w0 = cross(InB - InA, p - InA) / area;
                float w1 = cross(InC - InB, p - InB) / area;
                float w2 = cross(InA - InC, p - InC) / area;
                if (w0 < 0.0f || w1 < 0.0f || w2 < 0.0f)
                {
                    continue;
                }

                int index = y * InWidth + x;
                if (InMask[index] < InValue)
                {
                    InMask[index] = InValue;
                }
            }
        }
    }

    private static float cross(Vector2 InA, Vector2 InB)
    {
        return InA.x * InB.y - InA.y * InB.x;
    }

    /// <summary>채도 · 밝기를 살짝 낮추고 가장자리를 어둡게 (지도 위 표식이 묻히지 않게).</summary>
    private static void stylize(Texture2D InTexture)
    {
        var pixels = InTexture.GetPixels();
        int width = InTexture.width;
        int height = InTexture.height;
        for (int y = 0; y < height; ++y)
        {
            for (int x = 0; x < width; ++x)
            {
                int index = y * width + x;
                Color color = pixels[index];
                float gray = color.grayscale;
                color = Color.Lerp(new Color(gray, gray, gray), color, 0.75f);
                color = Color.Lerp(color, new Color(0.35f, 0.27f, 0.22f), 0.12f) * 0.9f;

                float u = (x + 0.5f) / width * 2.0f - 1.0f;
                float v = (y + 0.5f) / height * 2.0f - 1.0f;
                float vignette = 1.0f - Mathf.Clamp01((Mathf.Max(Mathf.Abs(u), Mathf.Abs(v)) - 0.85f) / 0.15f) * 0.6f;
                color *= vignette;
                color.a = 1.0f;
                pixels[index] = color;
            }
        }

        InTexture.SetPixels(pixels);
    }
}
