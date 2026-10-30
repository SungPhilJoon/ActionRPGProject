using Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 게임 상태 → 화면 효과 (URP Volume weight 블렌딩, URP.md 3절).
///  - Focus 볼륨(항상 켜짐): 피사계 심도 초점 = 카메라 ~ 플레이어 거리 → 플레이어는 선명, 먼 지형 · 화면 위쪽은 흐릿 (쿼터뷰 미니어처 느낌)
///  - Sunburn 볼륨: 햇빛 노출(SunExposure.Exposure01)만큼 — 주황 비네트, 색수차, 열기(렌즈 왜곡), 과노출
///  - LowHealth 볼륨: 체력 35% 아래에서 붉은 비네트가 심장 박동처럼 맥동 + 채도 감소
///  - Death 볼륨: 사망 중 흑백 + 어둡게
///  - 대기 풀스크린 패스(Game/PostFx/Atmosphere) 전역 값: 태양 방향 · 색, 안개 색(RenderSettings.fogColor)
/// 볼륨은 WorldBuilder.PostFx 가 만들고 이 컴포넌트(카메라)에 연결한다. weight 만 바꾸므로 프로필 에셋은 수정되지 않는다
/// (DoF 초점만 런타임 프로필 사본에서 바꾼다).
/// </summary>
public class ScreenEffects : BaseBehaviour
{
    private static readonly int SunDirId = Shader.PropertyToID("_AtmoSunDir");
    private static readonly int SunColorId = Shader.PropertyToID("_AtmoSunColor");
    private static readonly int FogColorId = Shader.PropertyToID("_AtmoFogColor");
    private static readonly int ParamsId = Shader.PropertyToID("_AtmoParams");

    private const float LowHealthRatio = 0.35f;
    private const float WeightSpeed = 4.0f;

    public Volume Focus;
    public Volume Sunburn;
    public Volume LowHealth;
    public Volume Death;

    [Header("Atmosphere (높이 안개)")]
    public float FogDensity = 0.018f;
    public float FogFalloff = 0.12f;
    public float FogBaseHeight = 5.0f;
    public float FogMax = 0.55f;

    private DepthOfField DepthOfField;
    private Camera Camera;

    protected override void OnUnityAwake()
    {
        base.OnUnityAwake();
        Camera = GetComponent<Camera>();
        if (Focus != null && Focus.profile.TryGet(out DepthOfField) == false)
        {
            DepthOfField = null;
        }
    }

    private void LateUpdate()
    {
        var player = PlayerCharacter.Local;
        float dt = Time.unscaledDeltaTime;

        if (DepthOfField != null && Camera != null)
        {
            Vector3 focus = player != null ? player.transform.position + Vector3.up : transform.position + transform.forward * 25.0f;
            DepthOfField.focusDistance.value = Vector3.Distance(Camera.transform.position, focus);
        }

        float sun = 0.0f;
        float low = 0.0f;
        float dead = 0.0f;
        if (player != null)
        {
            sun = player.Sun != null && player.Sun.IsExposed == true ? player.Sun.Exposure01 : 0.0f;
            float ratio = player.HealthRatio;
            if (player.IsDead == true)
            {
                dead = 1.0f;
            }
            else if (ratio < LowHealthRatio)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(4.0f, 9.0f, 1.0f - ratio / LowHealthRatio));
                low = (1.0f - ratio / LowHealthRatio) * pulse;
            }
        }

        blend(Sunburn, Mathf.Clamp01(0.25f + sun) * (sun > 0.0f ? 1.0f : 0.0f), dt);
        blend(LowHealth, low, dt * 3.0f);
        blend(Death, dead, dt);
        updateAtmosphere();
    }

    private static void blend(Volume InVolume, float InTarget, float InDeltaTime)
    {
        if (InVolume == null)
        {
            return;
        }

        InVolume.weight = Mathf.MoveTowards(InVolume.weight, InTarget, InDeltaTime * WeightSpeed);
    }

    private void updateAtmosphere()
    {
        var dayNight = Managers.Get<DayNightManager>();
        Vector3 toSun = dayNight != null ? dayNight.DirectionToSun : new Vector3(0.3f, 0.7f, 0.3f);
        bool day = dayNight == null || dayNight.IsDay == true;
        var sun = SceneContext.Current != null ? SceneContext.Current.Sun : null;
        Color sunColor = sun != null ? sun.color * Mathf.Min(sun.intensity, 1.5f) : Color.white;
        Shader.SetGlobalVector(SunDirId, new Vector4(toSun.x, toSun.y, toSun.z, day == true ? 0.9f : 0.35f));
        Shader.SetGlobalColor(SunColorId, sunColor);
        Shader.SetGlobalColor(FogColorId, RenderSettings.fogColor);
        Shader.SetGlobalVector(ParamsId, new Vector4(FogDensity, FogFalloff, FogBaseHeight, FogMax));
    }
}
