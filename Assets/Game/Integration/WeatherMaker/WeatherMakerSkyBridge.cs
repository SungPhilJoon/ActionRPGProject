using DigitalRuby.WeatherMaker;
using UnityEngine;

/// <summary>
/// WeatherMaker ↔ 게임 연결 어댑터 (Adapter + Dependency Inversion).
///
/// 이 파일은 asmdef 가 없는 폴더(Game/Integration)에 있어 Assembly-CSharp 로 컴파일된다.
/// 그래서 WeatherMaker(Assembly-CSharp) 와 Game.Runtime(autoReferenced) 을 둘 다 참조할 수 있다.
///  - 시간의 주인은 게임(DayNightManager)이다. 매 프레임 SetHour 로 WeatherMaker 시간을 덮어쓰고, WeatherMaker 자체 시간 흐름(Speed)은 0 으로 멈춘다.
///  - 태양 방향은 WeatherMaker 의 Sun 라이트에서 읽어 햇빛 판정(SunExposure)에 쓴다 → 화면의 해와 판정이 일치한다.
/// 씬에 WeatherMakerScript 가 있으면 씬 로드 직후 자동으로 그 오브젝트에 붙는다 (AutoInstall).
/// </summary>
public class WeatherMakerSkyBridge : MonoBehaviour, ISkyController
{
    private const float SecondsPerHour = 3600.0f;
    /// <summary>WeatherMaker 의 라이트 배율 사전 키 (WeatherMaker API 가 문자열 키를 요구한다).</summary>
    private const string BoostKey = "GameSunBoost";

    [Tooltip("쿼터뷰에서 지면이 어둡게 보이지 않도록 태양 세기에 곱하는 배율 (WeatherMaker 권장 방식: DirectionalLightIntensityMultipliers).")]
    public float SunIntensityBoost = 1.7f;

    [Tooltip("태양 궤적 계산용 위도. 높을수록 해가 낮게 떠서 그림자가 길다.")]
    public double Latitude = 40.0;
    [Tooltip("경도 0 + 시간대 0 이면 정오 12시에 해가 가장 높다.")]
    public double Longitude = 0.0;
    [Tooltip("추분(9/21) 은 일출·일몰이 6시/18시에 가깝다.")]
    public int Month = 9;
    public int Day = 21;

    [Tooltip("구름 그림자를 지면에 그릴지. 구름은 햇빛 판정(물리 레이캐스트)을 막지 않으므로, 끄면 '보이는 그늘 = 안전한 그늘' 이 된다.")]
    public bool CloudShadows = false;

    [Header("Night (뱀파이어는 밤에 잘 본다 — V Rising 처럼 밤에도 지형이 보이게)")]
    [Tooltip("WeatherMaker DayNight 프로필의 밤 Ambient 세기에 곱하는 배율. 그늘진 면의 밤 밝기를 결정한다.")]
    public float NightAmbientScale = 6.0f;
    [Tooltip("새벽/황혼 Ambient 세기 배율 (낮↔밤 전환 때 갑자기 어두워지지 않게).")]
    public float DawnDuskAmbientScale = 2.5f;
    [Tooltip("달빛 세기 배율 (WeatherMaker 기본 0.5). 달이 지평선 아래면 0 이 되므로 Ambient 가 밤 밝기의 바닥이 된다.")]
    public float MoonLightMultiplier = 1.6f;

    private Vector3 LastDirectionToSun = Vector3.up;

    /// <summary>씬에 WeatherMaker 가 있으면 브리지를 자동으로 붙인다 (에디터 빌더가 Assembly-CSharp 타입을 몰라도 되게).</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void autoInstall()
    {
        var weatherMaker = FindFirstObjectByType<WeatherMakerScript>();
        if (weatherMaker == null || weatherMaker.GetComponent<WeatherMakerSkyBridge>() != null)
        {
            return;
        }

        weatherMaker.gameObject.AddComponent<WeatherMakerSkyBridge>();
    }

    public Vector3 DirectionToSun
    {
        get
        {
            var sun = findSun();
            if (sun != null)
            {
                LastDirectionToSun = -sun.transform.forward;
            }

            return LastDirectionToSun;
        }
    }

    public float SunElevation
    {
        get { return Mathf.Asin(Mathf.Clamp(DirectionToSun.y, -1.0f, 1.0f)) * Mathf.Rad2Deg; }
    }

    private void OnEnable()
    {
        SkyBridge.Register(this);
    }

    private void OnDisable()
    {
        SkyBridge.Unregister(this);
    }

    private void Start()
    {
        if (WeatherMakerDayNightCycleManagerScript.HasInstance() == false)
        {
            return;
        }

        var cycle = WeatherMakerDayNightCycleManagerScript.Instance;
        cycle.Speed = 0.0f;
        cycle.NightSpeed = 0.0f;
        cycle.Latitude = Latitude;
        cycle.Longitude = Longitude;
        cycle.TimeZoneOffsetSeconds = 0;
        cycle.Month = Month;
        cycle.Day = Day;

        applyNightLighting(cycle.DayNightProfile);

        if (WeatherMakerLightManagerScript.HasInstance() == true)
        {
            var lights = WeatherMakerLightManagerScript.Instance;
            lights.DirectionalLightIntensityMultipliers[BoostKey] = SunIntensityBoost;
            if (CloudShadows == false)
            {
                disableCloudShadows();
            }

            foreach (var moon in lights.Moons)
            {
                if (moon != null)
                {
                    moon.LightMultiplier = MoonLightMultiplier;
                }
            }
        }
    }

    /// <summary>태양 라이트의 섀도우맵에 구름 그림자를 합성하는 재질을 비운다 (WeatherMaker 규약: null = 구름 그림자 없음). 씬 인스턴스만 바꾼다.</summary>
    private void disableCloudShadows()
    {
        var sun = findSun();
        if (sun == null)
        {
            return;
        }

        var shadowMap = sun.GetComponent<WeatherMakerShadowMapScript>();
        if (shadowMap != null)
        {
            shadowMap.CloudShadowMaterial = null;
        }
    }

    /// <summary>
    /// 밤/새벽·황혼 Ambient 세기를 올린다. WeatherMaker 는 Play 시작 때 프로필을 복제(ScriptableObject.Instantiate)해 쓰므로
    /// 여기서 바꿔도 원본 에셋은 바뀌지 않는다.
    /// </summary>
    private void applyNightLighting(WeatherMakerDayNightCycleProfileScript InProfile)
    {
        if (InProfile == null)
        {
            return;
        }

        InProfile.NightAmbientColorIntensity *= NightAmbientScale;
        InProfile.NightAmbientColorSkyIntensity *= NightAmbientScale;
        InProfile.NightAmbientColorGroundIntensity *= NightAmbientScale;
        InProfile.NightAmbientColorEquatorIntensity *= NightAmbientScale;
        InProfile.DawnDuskAmbientColorIntensity *= DawnDuskAmbientScale;
        InProfile.DawnDuskAmbientColorSkyIntensity *= DawnDuskAmbientScale;
        InProfile.DawnDuskAmbientColorGroundIntensity *= DawnDuskAmbientScale;
        InProfile.DawnDuskAmbientColorEquatorIntensity *= DawnDuskAmbientScale;
    }

    public void SetHour(float InHour)
    {
        if (WeatherMakerDayNightCycleManagerScript.HasInstance() == false)
        {
            return;
        }

        WeatherMakerDayNightCycleManagerScript.Instance.TimeOfDay = Mathf.Repeat(InHour, 24.0f) * SecondsPerHour;
    }

    private Light findSun()
    {
        if (WeatherMakerLightManagerScript.HasInstance() == false)
        {
            return null;
        }

        var sun = WeatherMakerLightManagerScript.Instance.SunPerspective;
        return sun != null ? sun.Light : null;
    }
}
