using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 하늘 (2026-10-30 WeatherMaker 제거 후): URP 에서 그대로 쓰는 절차 스카이박스(Skybox/Procedural) + 지수 안개 + 하늘 반사 큐브맵.
/// 태양은 Directional Light 하나 (낮 = 해, 밤 = 달빛). 시간에 따른 라이트 · 하늘 노출 · 안개 색 · 반사 세기는 DayNightManager 가 바꾼다.
/// WeatherMaker 의 볼류메트릭 구름 · 대기 산란 패스(풀스크린 GPU 패스 여러 개)를 없애고, 분위기는 포스트 프로세싱 볼륨(.PostFx)과
/// 대기 원근 효과(Game/Atmosphere 풀스크린 패스)로 낸다.
/// </summary>
public static partial class WorldBuilder
{
    private static Light buildSky(Camera InCamera)
    {
        var go = new GameObject("Sun");
        var sun = go.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.6f;
        sun.color = new Color(1.0f, 0.93f, 0.82f);
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        go.transform.rotation = Quaternion.Euler(50.0f, -30.0f, 0.0f);

        RenderSettings.sun = sun;
        RenderSettings.skybox = skyboxMaterial();
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.78f, 0.8f, 0.88f);
        RenderSettings.ambientEquatorColor = new Color(0.6f, 0.58f, 0.54f);
        RenderSettings.ambientGroundColor = new Color(0.36f, 0.32f, 0.27f);

        // 반사: 빌드 때 구운 하늘 큐브맵 (물 · 금속 무기 · 갑옷에 하늘색이 비침). 예전에는 반사가 비어 금속이 검게 보였다.
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = bakeSkyReflection();
        RenderSettings.reflectionIntensity = 0.65f;

        // 원거리 지형이 하늘색으로 스며드는 지수 안개 (색 · 밀도는 DayNightManager 가 시간대마다 바꿈).
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = 0.0042f;
        RenderSettings.fogColor = new Color(0.62f, 0.68f, 0.74f);
        return sun;
    }

    /// <summary>
    /// 하늘 반사 큐브맵을 빌드 때 한 번 굽는다 (Camera.RenderToCubemap, 하늘만).
    /// 실시간 반사 프로브는 Game/Character 캐릭터가 청록색 실루엣으로 깨지는 문제가 있어 쓰지 않는다 (Issues 38).
    /// 밤에는 DayNightManager 가 반사 세기를 낮춘다.
    /// </summary>
    private static Cubemap bakeSkyReflection()
    {
        string path = EditorPaths.Generated + "/World/SkyReflection.cubemap";
        var cubemap = new Cubemap(128, TextureFormat.RGBAHalf, true);
        // 태양 원반까지 구우면 물 · 금속에 하얀 원반이 번져 블룸으로 날아간다 → 구울 때만 끈다 (해 하이라이트는 셰이더 스펙큘러가 따로 그림).
        var sky = RenderSettings.skybox;
        float sunDisk = sky != null ? sky.GetFloat("_SunDisk") : 0.0f;
        if (sky != null)
        {
            sky.SetFloat("_SunDisk", 0.0f);
        }

        var go = new GameObject("SkyCapture");
        var capture = go.AddComponent<Camera>();
        capture.cullingMask = 0;
        capture.clearFlags = CameraClearFlags.Skybox;
        capture.RenderToCubemap(cubemap);
        Object.DestroyImmediate(go);
        if (sky != null)
        {
            sky.SetFloat("_SunDisk", sunDisk);
        }

        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(cubemap, path);
        return cubemap;
    }

    private static Material skyboxMaterial()
    {
        string folder = EditorPaths.Generated + "/World";
        EditorAssetUtil.EnsureFolder(folder);
        string path = folder + "/M_Skybox.mat";
        var shader = Shader.Find("Skybox/Procedural");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetFloat("_SunDisk", 2.0f);
        material.SetFloat("_SunSize", 0.035f);
        material.SetFloat("_SunSizeConvergence", 6.0f);
        material.SetFloat("_AtmosphereThickness", 1.05f);
        material.SetColor("_SkyTint", new Color(0.52f, 0.56f, 0.62f));
        material.SetColor("_GroundColor", new Color(0.32f, 0.3f, 0.28f));
        material.SetFloat("_Exposure", 1.25f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
