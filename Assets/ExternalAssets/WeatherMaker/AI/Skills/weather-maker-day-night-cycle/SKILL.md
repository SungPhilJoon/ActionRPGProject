---
name: weather-maker-day-night-cycle
description: "Use this skill whenever the user wants to change time of day, sunrise, sunset, night, sun or moon position, or calendar/location for Weather Maker — e.g. 'make it night', 'set sunset', 'freeze time', 'put the sun in the west', 'set lat lon to London'. Covers WeatherMakerDayNightCycleManagerScript TimeOfDay/Speed, date, timezone, latitude/longitude, and celestial sun/moon lights. Do NOT use for adding the prefab (see weather-maker-setup-and-overview), rain/storms (see weather-maker-control-weather), cloud shapes (see weather-maker-volumetric-clouds), fog (see weather-maker-fog-and-atmosphere), zones (see weather-maker-weather-zones), or water (see weather-maker-water). When in doubt whether a sky or lighting-time request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "Weather Maker"
  publisher: "Digital Ruby, LLC"
  asset-version: "8.1.1"
  skill-version: "1.0.0"
  unity: "2019.4.35+"
  render-pipelines: "Built-in, URP"
  category: "tools/particles-effects"
  asset-store-url: "https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955"
  documentation-url: "UserManual.md"
  support-url: "https://www.digitalruby.com"
  last-verified: "2026-08-28"
---

# Drive the day/night cycle with Weather Maker

Set local time, freeze or speed the clock, and place the sun and moon from date plus latitude/longitude. Weather Maker owns directional sun/moon lights — do not animate those lights yourself.

## When to use this skill

- "make it night" / "make it noon" / "sunset" / "sunrise"
- "freeze the time of day" / "speed up the day night cycle"
- "set the date" / "move this to Iceland" / "change latitude"
- "sun is in the wrong place" / "moon phase is wrong"

Not for: prefab setup (`weather-maker-setup-and-overview`), precipitation (`weather-maker-control-weather`), volumetric cloud profiles (`weather-maker-volumetric-clouds`), fog (`weather-maker-fog-and-atmosphere`), zones (`weather-maker-weather-zones`), water (`weather-maker-water`).

## Prerequisites

- Weather Maker 8.1.1, Unity 2019.4.35+, Built-in or URP.
- `WeatherMakerPrefab` in the scene with `DayNightCycle` **enabled**. Never disable this object to pause time.

**Install check:** `DigitalRuby.WeatherMaker.WeatherMakerDayNightCycleManagerScript` exists and `WeatherMakerDayNightCycleManagerScript.Instance` (or FindObjectOfType) is non-null. If missing, run `weather-maker-setup-and-overview`. Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955

Lighting window: Skybox Material `WeatherMakerSkyboxMaterial`, Sun Source = Weather Maker **Sun**.

## Quick start

Make it night and freeze the clock:

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

public class WeatherMakerSetNight : MonoBehaviour
{
    void Start()
    {
        var cycle = WeatherMakerDayNightCycleManagerScript.Instance;
        cycle.TimeOfDay = 22f * 3600f; // 22:00 local, seconds in 0–86400
        cycle.Speed = 0f;              // freeze day
        cycle.NightSpeed = 0f;         // freeze night; keep sign matched to Speed when non-zero
    }
}
```

Expected result: Sun is below the horizon, night sky/stars visible, `TimeOfDayCategory` reflects night, time does not advance. Default unfrozen speeds are `Speed = 10` and `NightSpeed = 10` (10× real time). Default `TimeOfDay` is noon (`86400 * 0.5`).

## Workflows

### Workflow: Set time of day

**Goal:** Jump to a specific hour.

**Steps:**

1. `TimeOfDay` is **seconds** from local midnight, range `0`–`86400` (`WeatherMakerDayNightCycleProfileScript.SecondsPerDay`). `HighNoonTimeOfDay` = `43200`.
2. Convenience: `TimeOfDayTimespan` (`System.TimeSpan`). `DateTime` sets year/month/day plus time together.
3. Examples: sunrise-ish `6 * 3600`, noon `12 * 3600`, sunset-ish `18 * 3600`, midnight `0`. Actual sunrise/sunset depend on lat/lon/date.
4. Read-only multipliers: `DayMultiplier`, `DawnDuskMultiplier`, `NightMultiplier` (0–1). Category: `TimeOfDayCategory`.
5. Events on `WeatherMakerScript`: `DawnBegin`, `DuskBegin`, `DayBegin`, `NightBegin`, `SunriseBegin` / `SunriseEnd`, `SunsetBegin` / `SunsetEnd`, plus year/month/day/hour/minute/second changed.

**Expected result:** Sun directional light rotates to match the new time. Ambient sky colors update from the day/night profile gradients.

### Workflow: Freeze, speed, or reverse the cycle

**Goal:** Stop time or run a fast preview.

**Steps:**

1. Real-time: `Speed = 1`, `NightSpeed = 1`.
2. Default preview: both `10`.
3. Freeze: both `0`. Do not disable `WeatherMakerDayNightCycleManagerScript`.
4. Reverse: negative speeds (range allows `-100000` to `100000`). Keep **Speed** and **NightSpeed** the same sign.
5. `UpdateInterval` default `0.03` seconds. Raise it if shadows flicker.

**Expected result:** With Speed 0, `TimeOfDay` stays constant across frames. With Speed 100, the sun moves visibly in seconds.

### Workflow: Date, timezone, latitude, longitude

**Goal:** Astronomically plausible sun/moon for a place and date.

**Steps:**

1. Valid year range **1900–2100** (profile tooltip: correct from 1 Mar 1900 to 28 Feb 2100). Defaults: `Year = 2000`, `Month = 9`, `Day = 21`.
2. `Latitude` default `40.7608` (Salt Lake City), range -90 to 90. `Longitude` default `-111.8910`, range -180 to 180.
3. `TimeZoneOffsetSeconds` default `-21600` (UTC-6). Set to `-1111` to auto-calculate (Editor uses a web lookup; Play mode uses longitude).
4. `AdjustDateWhenDayEnds` default `true` — leave on if time actually advances so moon phase stays correct.
5. Assign via manager properties (`Latitude`, `Longitude`, `Year`, `Month`, `Day`, `TimeZoneOffsetSeconds`) which write through to `DayNightProfile`.

**Expected result:** At high northern latitude in December near noon, the sun stays low. Moon phase follows the date.

### Workflow: Sun and moon lights

**Goal:** Keep celestial lights under Weather Maker control.

**Steps:**

1. Hierarchy: `WeatherMakerPrefab/DayNightCycle/Sun` and `Moon` — each is a directional light with `WeatherMakerCelestialObjectScript`. Sun has `IsSun = true`; moon `IsSun = false`.
2. Keep the **Sun object enabled** even at night; other systems depend on it. Do not drive `Light.intensity` yourself. Temporarily scale sun/moon with `WeatherMakerLightManagerScript.Instance.DirectionalLightIntensityMultipliers["key"] = 0.5f` and `DirectionalLightShadowStrengthMultipliers`.
3. Darkest shadows: Sun celestial **Base Shadow Strength** = `1`.
4. Extra moons: add a directional light + `WeatherMakerCelestialObjectScript` with `IsSun = false`. Only one sun is supported.
5. `RotateYDegrees` on the celestial script yaws the orbit (non-east/west sun paths).
6. Night sky brightness: `WeatherMakerSkySphereScript.Instance.SkySphereProfile.NightIntensity` (default `1`). Raise if stars are invisible.

**Expected result:** Sun source in the Lighting window is the Weather Maker Sun. Intensity changes come from the day/night profile, not a random Light component tweak.

## Verification

- `WeatherMakerDayNightCycleManagerScript.Instance` non-null and the component enabled.
- After “make it night”: `TimeOfDay` in a night band (e.g. > 20*3600 or < 5*3600 at mid-latitudes) and `NightMultiplier` > 0.
- After freeze: `Speed == 0` and `NightSpeed == 0`.
- Sun GameObject active; `WeatherMakerCelestialObjectScript.IsSun == true`.
- Console clean.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

var c = WeatherMakerDayNightCycleManagerScript.Instance;
bool night = c != null && c.NightMultiplier > 0.5f && c.Speed == 0f;
Debug.Log(night ? "PASS: frozen night" : "FAIL: not frozen night");
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `WeatherMakerDayNightCycleManagerScript.Instance` | Component | `TimeOfDay` (seconds), `Speed`, `NightSpeed`, date, lat/lon, multipliers |
| `WeatherMakerDayNightCycleProfileScript` | ScriptableObject | `Create > WeatherMaker > Day-Night Cycle Profile`. Defaults: Speed 10, noon, SLC lat/lon |
| `WeatherMakerCelestialObjectScript` | Component | `IsSun`, `RotateYDegrees` on directional lights |
| `WeatherMakerSkySphereScript.Instance.SkySphereProfile` | Profile | Sky mode, `NightIntensity`, atmosphere slot |
| `WeatherMakerLightManagerScript.Instance.DirectionalLightIntensityMultipliers` | Dictionary | Temporary sun/moon intensity scale by key |
| `WeatherMakerDayNightCycleProfileScript.SecondsPerDay` | Const | `86400` |

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| Time does not change | Speed 0, or DayNightCycle disabled | Set Speed; re-enable the component |
| Night sky black | NightIntensity too low | Raise `NightIntensity` on the sky profile |
| Sun intensity fights the cycle | Something else writes Light.intensity | Use DirectionalLightIntensityMultipliers |
| Wrong sun angle | Lat/lon/date/timezone mismatch | Set all four; year 1900–2100 |
| Shadow flicker as time moves | Update too frequent | Raise `UpdateInterval`; add temporal AA |
| Sky black after alt-tab (Editor) | Unity command-buffer bug | Deactivate/reactivate the Weather Maker prefab |

## Boundaries

- Weather Maker replaces Unity’s default skybox/ambient driving for the main camera. Do not also rotate a second “game sun” directional light expecting Weather Maker to follow it — mark Weather Maker’s Sun as the Lighting window sun source.
- Multi-sun worlds are not supported.
- Sky sphere **sphere** / **panorama** / **Fish Eye 360** modes can show pole distortion; prefer dome modes (see `UserManual.md` §11).
- This skill does not place clouds or rain; night + storm is `weather-maker-control-weather` plus this skill.
