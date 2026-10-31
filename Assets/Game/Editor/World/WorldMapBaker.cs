using Framework.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
