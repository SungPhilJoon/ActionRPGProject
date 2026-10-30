using UnityEngine;

/// <summary>
/// 외부 하늘/날씨 시스템 연결 인터페이스 (Dependency Inversion).
/// asmdef 없는 외부 에셋은 Assembly-CSharp 에 있어 Game.Runtime 이 직접 참조할 수 없으므로, Game.Runtime 이 이 인터페이스를 정의하고
/// Game/Integration 쪽 어댑터가 구현해 등록한다. 2026-10-30 WeatherMaker(유일한 구현)를 제거해 지금은 등록된 것이 없고,
/// DayNightManager 가 자체 라이트 · 스카이박스 · 안개 · 볼륨 제어로 동작한다 (Null Object / Fallback).
/// </summary>
public interface ISkyController
{
    /// <summary>게임 시간(0~24시)을 하늘 시스템에 전달한다. 시간의 주인은 DayNightManager 이다.</summary>
    void SetHour(float InHour);

    /// <summary>지면 → 태양 방향 (정규화). 햇빛 판정 레이캐스트에 쓴다.</summary>
    Vector3 DirectionToSun { get; }

    /// <summary>태양 고도 (도). 0 이하면 지평선 아래.</summary>
    float SunElevation { get; }
}

public static class SkyBridge
{
    public static ISkyController Controller { get; private set; }

    public static void Register(ISkyController InController)
    {
        Controller = InController;
    }

    public static void Unregister(ISkyController InController)
    {
        if (Controller == InController)
        {
            Controller = null;
        }
    }
}
