using System.Linq;
using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 포스트 프로세싱 (URP Volume) + 렌더러 피처 (2026-10-30, URP.md 3절 · 10절).
///  - 낮 / 밤 볼륨 (priority 0, DayNightManager 가 weight 를 섞음): ACES, 블룸 + 스크린 스페이스 렌즈 플레어, 화이트밸런스,
///    스플릿 토닝(그늘 청록 · 하이라이트 금빛), 섀도/미드톤/하이라이트, 리프트/감마/게인, 비네트, 필름 그레인, 색수차, 모션 블러
///  - Focus (priority 5, 항상 1): 보케 피사계 심도 — 초점은 ScreenEffects 가 플레이어 거리로 매 프레임 갱신
///  - Sunburn / LowHealth / Death (priority 10~12, weight 0 → ScreenEffects 가 게임 상태로 올림)
///  - 렌더러: SSAO 강화, 대기(높이 안개 · 태양 산란) 풀스크린 패스 추가, HDR 컬러 그레이딩
/// </summary>
public static partial class WorldBuilder
{
    private const string AtmosphereFeatureName = "Atmosphere (Game)";

    private static void buildVolumes(out Volume OutDay, out Volume OutNight)
    {
        string folder = EditorPaths.Generated + "/World";

        var day = createProfile(folder + "/VP_Day.asset");
        day.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloom = day.Add<Bloom>(true);
        bloom.threshold.Override(1.0f);
        bloom.intensity.Override(0.85f);
        bloom.scatter.Override(0.68f);
        bloom.tint.Override(new Color(1.0f, 0.95f, 0.88f));
        bloom.highQualityFiltering.Override(true);
        flare(day, 0.35f, new Color(1.0f, 0.85f, 0.65f));
        var color = day.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.2f);
        color.contrast.Override(12.0f);
        color.saturation.Override(18.0f);
        var white = day.Add<WhiteBalance>(true);
        // 따뜻한 화이트밸런스는 풀밭을 누런 올리브색으로 만들어서 살짝 차갑게 (2026-10-30 화면 확인 후 조정).
        white.temperature.Override(-4.0f);
        white.tint.Override(0.0f);
        var split = day.Add<SplitToning>(true);
        split.shadows.Override(new Color(0.4f, 0.52f, 0.6f));
        split.highlights.Override(new Color(0.86f, 0.82f, 0.74f));
        split.balance.Override(-10.0f);
        var smh = day.Add<ShadowsMidtonesHighlights>(true);
        // 그늘을 누르면(w < 0) 나무 그림자가 새까맣게 뭉개져서 살짝 들어 올린다.
        smh.shadows.Override(new Vector4(0.95f, 0.98f, 1.04f, 0.03f));
        smh.highlights.Override(new Vector4(1.06f, 1.0f, 0.92f, 0.02f));
        var lgg = day.Add<LiftGammaGain>(true);
        lgg.lift.Override(new Vector4(1.0f, 1.0f, 1.02f, 0.0f));
        lgg.gain.Override(new Vector4(1.03f, 1.0f, 0.96f, 0.03f));
        vignette(day, 0.24f, new Color(0.02f, 0.01f, 0.0f));
        grain(day, 0.14f);
        day.Add<ChromaticAberration>(true).intensity.Override(0.06f);
        motion(day);
        saveProfile(day);

        var night = createProfile(folder + "/VP_Night.asset");
        night.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
        var bloomNight = night.Add<Bloom>(true);
        bloomNight.threshold.Override(0.7f);
        bloomNight.intensity.Override(1.6f);
        bloomNight.scatter.Override(0.75f);
        bloomNight.tint.Override(new Color(1.0f, 0.55f, 0.5f));
        bloomNight.highQualityFiltering.Override(true);
        flare(night, 0.6f, new Color(1.0f, 0.55f, 0.4f));
        var colorNight = night.Add<ColorAdjustments>(true);
        // 밤에도 지형이 보이도록 (ACES 가 어두운 영역을 많이 눌러서 노출을 높게, 대비는 낮게).
        colorNight.postExposure.Override(1.3f);
        colorNight.contrast.Override(14.0f);
        colorNight.saturation.Override(-12.0f);
        colorNight.colorFilter.Override(new Color(0.75f, 0.85f, 1.0f));
        night.Add<WhiteBalance>(true).temperature.Override(-18.0f);
        var splitNight = night.Add<SplitToning>(true);
        splitNight.shadows.Override(new Color(0.2f, 0.3f, 0.6f));
        splitNight.highlights.Override(new Color(1.0f, 0.62f, 0.38f));
        splitNight.balance.Override(-10.0f);
        vignette(night, 0.34f, new Color(0.08f, 0.0f, 0.03f));
        grain(night, 0.3f);
        night.Add<ChromaticAberration>(true).intensity.Override(0.1f);
        motion(night);
        saveProfile(night);

        OutDay = createVolume("Volume_Day", day, 1.0f, 0.0f);
        OutNight = createVolume("Volume_Night", night, 0.0f, 0.0f);
    }

    /// <summary>게임 상태 볼륨 4개 + ScreenEffects (카메라에 붙인다).</summary>
    private static void buildScreenEffects(GameObject InCamera)
    {
        string folder = EditorPaths.Generated + "/World";

        var focus = createProfile(folder + "/VP_Focus.asset");
        var dof = focus.Add<DepthOfField>(true);
        dof.mode.Override(DepthOfFieldMode.Bokeh);
        dof.focusDistance.Override(28.0f);
        // 쿼터뷰라 화면 아래(가까움) ~ 위(멂) 거리 차가 커서 조리개를 좁게(f/9) — 화면 위쪽 먼 지형만 살짝 흐려진다.
        dof.focalLength.Override(40.0f);
        dof.aperture.Override(9.0f);
        dof.bladeCount.Override(6);
        saveProfile(focus);

        var sunburn = createProfile(folder + "/VP_Sunburn.asset");
        // 처음 값(비네트 0.5, 노출 +0.7, 채도 +20)은 화면 전체가 주황색으로 덮여 지형이 안 보였다 → 가장자리 위주로 약하게.
        vignette(sunburn, 0.38f, new Color(0.55f, 0.18f, 0.0f));
        sunburn.Add<ChromaticAberration>(true).intensity.Override(0.3f);
        sunburn.Add<LensDistortion>(true).intensity.Override(-0.12f);
        var burnColor = sunburn.Add<ColorAdjustments>(true);
        burnColor.postExposure.Override(0.3f);
        burnColor.colorFilter.Override(new Color(1.0f, 0.93f, 0.84f));
        burnColor.saturation.Override(8.0f);
        saveProfile(sunburn);

        var low = createProfile(folder + "/VP_LowHealth.asset");
        vignette(low, 0.55f, new Color(0.45f, 0.0f, 0.02f));
        low.Add<ChromaticAberration>(true).intensity.Override(0.35f);
        var lowColor = low.Add<ColorAdjustments>(true);
        lowColor.saturation.Override(-55.0f);
        lowColor.colorFilter.Override(new Color(1.0f, 0.82f, 0.82f));
        saveProfile(low);

        var death = createProfile(folder + "/VP_Death.asset");
        var deathColor = death.Add<ColorAdjustments>(true);
        deathColor.saturation.Override(-100.0f);
        deathColor.postExposure.Override(-0.6f);
        deathColor.contrast.Override(25.0f);
        vignette(death, 0.6f, Color.black);
        saveProfile(death);

        var effects = InCamera.AddComponent<ScreenEffects>();
        effects.Focus = createVolume("Volume_Focus", focus, 1.0f, 5.0f);
        effects.Sunburn = createVolume("Volume_Sunburn", sunburn, 0.0f, 10.0f);
        effects.LowHealth = createVolume("Volume_LowHealth", low, 0.0f, 11.0f);
        effects.Death = createVolume("Volume_Death", death, 0.0f, 12.0f);
    }

    private static void vignette(VolumeProfile InProfile, float InIntensity, Color InColor)
    {
        var vignette = InProfile.Add<Vignette>(true);
        vignette.intensity.Override(InIntensity);
        vignette.smoothness.Override(0.45f);
        vignette.color.Override(InColor);
    }

    private static void grain(VolumeProfile InProfile, float InIntensity)
    {
        var grain = InProfile.Add<FilmGrain>(true);
        grain.type.Override(FilmGrainLookup.Thin2);
        grain.intensity.Override(InIntensity);
        grain.response.Override(0.85f);
    }

    private static void flare(VolumeProfile InProfile, float InIntensity, Color InTint)
    {
        var flare = InProfile.Add<ScreenSpaceLensFlare>(true);
        flare.intensity.Override(InIntensity);
        flare.tintColor.Override(InTint);
        flare.streaksIntensity.Override(0.25f);
        flare.streaksLength.Override(0.35f);
    }

    private static void motion(VolumeProfile InProfile)
    {
        var blur = InProfile.Add<MotionBlur>(true);
        blur.mode.Override(MotionBlurMode.CameraOnly);
        blur.quality.Override(MotionBlurQuality.Medium);
        blur.intensity.Override(0.18f);
    }

    private static VolumeProfile createProfile(string InPath)
    {
        AssetDatabase.DeleteAsset(InPath);
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, InPath);
        return profile;
    }

    private static void saveProfile(VolumeProfile InProfile)
    {
        foreach (var component in InProfile.components)
        {
            component.name = component.GetType().Name;
            AssetDatabase.AddObjectToAsset(component, InProfile);
        }

        EditorUtility.SetDirty(InProfile);
    }

    private static Volume createVolume(string InName, VolumeProfile InProfile, float InWeight, float InPriority)
    {
        var go = new GameObject(InName);
        var volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = InProfile;
        volume.weight = InWeight;
        volume.priority = InPriority;
        return volume;
    }

    // ------------------------------------------------------------------ renderer

    /// <summary>URP 에셋 품질 + 렌더러 피처 (SSAO 강화, 대기 풀스크린 패스). configurePipeline 에서 부른다.</summary>
    private static void configureRendering(UniversalRenderPipelineAsset InAsset)
    {
        var serialized = new SerializedObject(InAsset);
        // HDR 그레이딩: 톤매핑 전에 색 보정 → 밝은 부분(블룸 · 횃불)이 하얗게 날아가지 않는다.
        serialized.FindProperty("m_ColorGradingMode").intValue = (int)ColorGradingMode.HighDynamicRange;
        serialized.FindProperty("m_ColorGradingLutSize").intValue = 32;
        serialized.FindProperty("m_SoftShadowQuality").intValue = 3;
        serialized.ApplyModifiedProperties();

        var renderers = InAsset.rendererDataList;
        var renderer = renderers.Length > 0 ? renderers[0] as UniversalRendererData : null;
        if (renderer == null)
        {
            return;
        }

        var rendererSerialized = new SerializedObject(renderer);
        var features = rendererSerialized.FindProperty("m_RendererFeatures");
        for (int i = 0; i < features.arraySize; ++i)
        {
            var ssao = features.GetArrayElementAtIndex(i).objectReferenceValue as ScreenSpaceAmbientOcclusion;
            if (ssao == null)
            {
                continue;
            }

            var settings = new SerializedObject(ssao).FindProperty("m_Settings");
            settings.FindPropertyRelative("Intensity").floatValue = 1.1f;
            settings.FindPropertyRelative("DirectLightingStrength").floatValue = 0.45f;
            settings.FindPropertyRelative("Radius").floatValue = 0.55f;
            settings.FindPropertyRelative("BlurQuality").intValue = 0;
            settings.serializedObject.ApplyModifiedProperties();
        }

        addAtmosphereFeature(renderer);
        EditorUtility.SetDirty(renderer);
    }

    /// <summary>대기 풀스크린 패스 (Full Screen Pass Renderer Feature + Game/PostFx/Atmosphere). 이미 있으면 머티리얼만 갱신.</summary>
    private static void addAtmosphereFeature(UniversalRendererData InRenderer)
    {
        string path = EditorPaths.Generated + "/World/M_Atmosphere.mat";
        var shader = Shader.Find("Game/PostFx/Atmosphere");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        var feature = InRenderer.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f => f.name == AtmosphereFeatureName);
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = AtmosphereFeatureName;
            AssetDatabase.AddObjectToAsset(feature, InRenderer);
            InRenderer.rendererFeatures.Add(feature);
            long localId;
            string guid;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out localId);
            var serialized = new SerializedObject(InRenderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize += 1;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // 불투명 + 반투명(물)까지 그린 뒤, 포스트 프로세싱(톤매핑 · 블룸) 전에 HDR 색에 안개를 섞는다.
        feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
        feature.fetchColorBuffer = true;
        feature.requirements = ScriptableRenderPassInput.Depth;
        feature.passMaterial = material;
        feature.passIndex = 0;
        feature.SetActive(true);
        EditorUtility.SetDirty(feature);
    }
}
