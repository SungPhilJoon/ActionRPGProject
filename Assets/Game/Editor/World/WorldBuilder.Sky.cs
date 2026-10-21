using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// WeatherMaker 하늘/태양/달/구름 배치. 게임 시간과의 연결은 런타임에 WeatherMakerSkyBridge 가 자동으로 붙어 처리한다.
/// WeatherMaker 매뉴얼 체크리스트(카메라 Solid Color, HDR, Skybox = WeatherMakerSkyboxMaterial, Sun Source = WM Sun,
/// Environment Lighting = Gradient, Reflections = Custom, Unity Fog 끔) 를 코드로 적용한다.
/// </summary>
public static partial class WorldBuilder
{
    private const string WeatherMakerPrefab = External + "/WeatherMaker/Prefab/WeatherMakerPrefab.prefab";
    private const string WeatherMakerSkybox = External + "/WeatherMaker/Prefab/Material/Sky/WeatherMakerSkyBoxMaterial.mat";

    /// <summary>WeatherMaker 를 배치하고 태양 라이트를 돌려준다. WeatherMaker 가 없으면 기본 Directional Light 를 만든다.</summary>
    private static Light buildSky(Camera InCamera)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WeatherMakerPrefab);
        Light sun = null;
        if (prefab != null)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "WeatherMaker";
            foreach (var light in instance.GetComponentsInChildren<Light>(true))
            {
                if (light.type == LightType.Directional && light.name == "Sun")
                {
                    sun = light;
                }
            }

            var skybox = AssetDatabase.LoadAssetAtPath<Material>(WeatherMakerSkybox);
            if (skybox != null)
            {
                RenderSettings.skybox = skybox;
            }
        }
        else
        {
            Debug.LogWarning("[World] WeatherMaker prefab not found — fallback to plain directional light");
        }

        if (sun == null)
        {
            var go = new GameObject("Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.6f;
        }

        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.55f, 0.65f);
        RenderSettings.ambientEquatorColor = new Color(0.38f, 0.38f, 0.4f);
        RenderSettings.ambientGroundColor = new Color(0.18f, 0.16f, 0.14f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.fog = false;
        return sun;
    }
}
