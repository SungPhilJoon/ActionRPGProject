using Framework;
using UnityEngine;

/// <summary>
/// 낮/밤 주기. 하루 = DayLengthSeconds 초, 밤 비율 = NightRatio.
/// 태양(Directional Light)을 회전시키고, 낮/밤 Post-processing Volume 의 weight 를 섞는다.
/// 밤에는 같은 라이트를 달빛(푸른색, 약한 세기)으로 쓴다 — 그림자 맵을 하나만 유지하기 위함.
/// 하늘(절차 스카이박스 노출 · 색), 안개 색 · 밀도, 반사 세기도 시간대에 맞춰 바꾼다 (2026-10-30 WeatherMaker 대체).
/// </summary>
public class DayNightManager : IManager
{
    private static readonly Color DayColor = new Color(1.0f, 0.93f, 0.82f);
    private static readonly Color DuskColor = new Color(1.0f, 0.55f, 0.35f);
    private static readonly Color MoonColor = new Color(0.45f, 0.55f, 0.9f);
    // Trilight 앰비언트 (하늘 / 지평선 / 바닥). 하늘 색만 바꾸고 지평선 · 바닥을 고정했을 때는 그늘이 새까맣게 뭉개졌다.
    private static readonly Color DayAmbientSky = new Color(0.78f, 0.8f, 0.88f);
    private static readonly Color DayAmbientEquator = new Color(0.6f, 0.58f, 0.54f);
    private static readonly Color DayAmbientGround = new Color(0.36f, 0.32f, 0.27f);
    private static readonly Color NightAmbientSky = new Color(0.38f, 0.42f, 0.62f);
    private static readonly Color NightAmbientEquator = new Color(0.26f, 0.28f, 0.4f);
    private static readonly Color NightAmbientGround = new Color(0.12f, 0.12f, 0.16f);

    private static readonly Color DayFog = new Color(0.62f, 0.68f, 0.74f);
    private static readonly Color DuskFog = new Color(0.78f, 0.52f, 0.42f);
    private static readonly Color NightFog = new Color(0.07f, 0.09f, 0.16f);
    private static readonly Color DaySkyTint = new Color(0.52f, 0.56f, 0.62f);
    private static readonly Color NightSkyTint = new Color(0.12f, 0.16f, 0.32f);
    private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
    private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");

    private const float DayIntensity = 1.6f;
    private const float MoonIntensity = 0.9f;
    private const float DayFogDensity = 0.0042f;
    private const float NightFogDensity = 0.0065f;
    private const float DayReflection = 0.65f;
    private const float NightReflection = 0.2f;

    private Material SkyMaterial;

    private SceneContext Context;
    private float DayLength;
    private float SunriseHour;
    private float SunsetHour;

    /// <summary>0 ~ 24</summary>
    public float Hour { get; private set; }
    public float TimeScale = 1.0f;
    public bool Frozen;

    public bool IsDay
    {
        get { return Hour >= SunriseHour && Hour < SunsetHour; }
    }

    public bool IsSunUp
    {
        get { return IsDay == true && SunElevation >= Tables.Const(EConst.SunMinElevation); }
    }

    public float SunElevation { get; private set; }

    /// <summary>지면 → 태양 방향 (정규화).</summary>
    public Vector3 DirectionToSun { get; private set; }

    void IManager.Initialize()
    {
        Context = SceneContext.Current;
        DayLength = Mathf.Max(10.0f, Tables.Const(EConst.DayLengthSeconds));
        float nightRatio = Mathf.Clamp(Tables.Const(EConst.NightRatio), 0.1f, 0.9f);
        float dayHours = 24.0f * (1.0f - nightRatio);
        SunriseHour = 12.0f - dayHours * 0.5f;
        SunsetHour = 12.0f + dayHours * 0.5f;
        Hour = Tables.Const(EConst.StartHour);

        // 스카이박스 에셋을 직접 고치면 에디터 Play 중 변경이 디스크에 남으므로 사본을 만들어 쓴다.
        if (RenderSettings.skybox != null)
        {
            SkyMaterial = new Material(RenderSettings.skybox);
            RenderSettings.skybox = SkyMaterial;
        }

        apply();
    }

    void IManager.UnInitialize()
    {
        if (SkyMaterial != null)
        {
            Object.Destroy(SkyMaterial);
            SkyMaterial = null;
        }
    }

    void IManager.Update(float InDeltaTime)
    {
        if (Frozen == false)
        {
            Hour += InDeltaTime * TimeScale * 24.0f / DayLength;
            if (Hour >= 24.0f)
            {
                Hour -= 24.0f;
            }
        }

        apply();
    }

    public void SetHour(float InHour)
    {
        Hour = Mathf.Repeat(InHour, 24.0f);
        apply();
    }

    /// <summary>낮이면 일몰까지, 밤이면 일출까지 남은 시간 비율 (HUD 시계용 0~1).</summary>
    public float PhaseProgress
    {
        get
        {
            if (IsDay == true)
            {
                return Mathf.InverseLerp(SunriseHour, SunsetHour, Hour);
            }

            float nightLength = 24.0f - (SunsetHour - SunriseHour);
            float sinceSunset = Hour >= SunsetHour ? Hour - SunsetHour : Hour + 24.0f - SunsetHour;
            return Mathf.Clamp01(sinceSunset / nightLength);
        }
    }

    private void apply()
    {
        // 외부 하늘 시스템(ISkyController)이 등록되어 있으면 시간만 넘기고, 태양 방향/고도는 그쪽 값을 쓴다.
        // 2026-10-30 WeatherMaker 를 제거해 지금은 등록된 것이 없다 → 아래 자체 라이트 · 하늘 · 안개 제어로 동작한다.
        var sky = SkyBridge.Controller;
        if (sky != null)
        {
            sky.SetHour(Hour);
            SunElevation = sky.SunElevation;
            DirectionToSun = sky.DirectionToSun;
            applyVolumes(Mathf.Clamp01(SunElevation / 12.0f));
            return;
        }

        float azimuth;
        if (IsDay == true)
        {
            float t = Mathf.InverseLerp(SunriseHour, SunsetHour, Hour);
            SunElevation = Mathf.Sin(t * Mathf.PI) * 65.0f;
            azimuth = Mathf.Lerp(-80.0f, 260.0f, t);
        }
        else
        {
            float t = PhaseProgress;
            SunElevation = -Mathf.Sin(t * Mathf.PI) * 40.0f;
            azimuth = Mathf.Lerp(260.0f, 440.0f, t);
        }

        var toSun = Quaternion.Euler(-Mathf.Max(SunElevation, 2.0f), azimuth, 0.0f) * Vector3.forward;
        DirectionToSun = toSun.normalized;

        if (Context == null || Context.Sun == null)
        {
            return;
        }

        var light = Context.Sun;
        float dayWeight;
        if (IsDay == true)
        {
            float elevation01 = Mathf.Clamp01(SunElevation / 30.0f);
            light.transform.rotation = Quaternion.LookRotation(-DirectionToSun);
            light.color = Color.Lerp(DuskColor, DayColor, elevation01);
            light.intensity = Mathf.Lerp(0.6f, DayIntensity, elevation01);
            dayWeight = Mathf.Clamp01(SunElevation / 12.0f);
        }
        else
        {
            float moonElevation = Mathf.Max(20.0f, -SunElevation + 20.0f);
            var toMoon = Quaternion.Euler(-moonElevation, azimuth + 180.0f, 0.0f) * Vector3.forward;
            light.transform.rotation = Quaternion.LookRotation(-toMoon);
            light.color = MoonColor;
            light.intensity = MoonIntensity;
            dayWeight = 0.0f;
        }

        RenderSettings.ambientSkyColor = Color.Lerp(NightAmbientSky, DayAmbientSky, dayWeight);
        RenderSettings.ambientEquatorColor = Color.Lerp(NightAmbientEquator, DayAmbientEquator, dayWeight);
        RenderSettings.ambientGroundColor = Color.Lerp(NightAmbientGround, DayAmbientGround, dayWeight);
        applyAtmosphere(dayWeight);
        applyVolumes(dayWeight);
    }

    /// <summary>하늘 노출 · 색, 안개, 반사 세기. InDayWeight: 0 = 밤, 1 = 한낮 (해질녘은 중간값 → 노을색).</summary>
    private void applyAtmosphere(float InDayWeight)
    {
        float dusk = IsDay == true ? 1.0f - Mathf.Clamp01(SunElevation / 18.0f) : 0.0f;
        Color dayFog = Color.Lerp(DayFog, DuskFog, dusk);
        RenderSettings.fogColor = Color.Lerp(NightFog, dayFog, InDayWeight);
        RenderSettings.fogDensity = Mathf.Lerp(NightFogDensity, DayFogDensity, InDayWeight);
        if (SkyMaterial != null)
        {
            SkyMaterial.SetFloat(ExposureId, Mathf.Lerp(0.14f, 1.25f, InDayWeight));
            SkyMaterial.SetColor(SkyTintId, Color.Lerp(NightSkyTint, DaySkyTint, InDayWeight));
        }

        // 반사 큐브맵은 낮 하늘로 한 번 구운 것이라 밤에는 세기만 낮춘다.
        RenderSettings.reflectionIntensity = Mathf.Lerp(NightReflection, DayReflection, InDayWeight);
    }

    private void applyVolumes(float InDayWeight)
    {
        if (Context == null)
        {
            return;
        }

        float dayWeight = InDayWeight;
        if (Context.DayVolume != null)
        {
            Context.DayVolume.weight = dayWeight;
        }

        if (Context.NightVolume != null)
        {
            Context.NightVolume.weight = 1.0f - dayWeight;
        }
    }
}
