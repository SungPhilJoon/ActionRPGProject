using Framework;
using UnityEngine;

/// <summary>
/// 낮/밤 주기. 하루 = DayLengthSeconds 초, 밤 비율 = NightRatio.
/// 태양(Directional Light)을 회전시키고, 낮/밤 Post-processing Volume 의 weight 를 섞는다.
/// 밤에는 같은 라이트를 달빛(푸른색, 약한 세기)으로 쓴다 — 그림자 맵을 하나만 유지하기 위함.
/// </summary>
public class DayNightManager : IManager
{
    private static readonly Color DayColor = new Color(1.0f, 0.93f, 0.82f);
    private static readonly Color DuskColor = new Color(1.0f, 0.55f, 0.35f);
    private static readonly Color MoonColor = new Color(0.45f, 0.55f, 0.9f);
    private static readonly Color DayAmbient = new Color(0.55f, 0.55f, 0.6f);
    private static readonly Color NightAmbient = new Color(0.3f, 0.32f, 0.48f);

    private const float DayIntensity = 1.6f;
    private const float MoonIntensity = 0.9f;

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
        apply();
    }

    void IManager.UnInitialize()
    {
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
        // 외부 하늘 시스템(WeatherMaker)이 연결되어 있으면 시간만 넘기고, 태양 방향/고도는 그쪽 값을 쓴다.
        // 라이트·스카이·앰비언트는 WeatherMaker 가 담당하고 우리는 컬러 그레이딩 볼륨 가중치만 조절한다.
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

        RenderSettings.ambientLight = Color.Lerp(NightAmbient, DayAmbient, dayWeight);
        applyVolumes(dayWeight);
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
