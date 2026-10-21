---
name: weather-maker-control-weather
description: "Use this skill whenever the user wants rain, snow, hail, sleet, storms, lightning, or wind with Weather Maker — e.g. 'make it rain', 'start a thunderstorm', 'heavy snow', 'turn the weather off', 'why isn't my rain showing', 'add lightning'. Covers precipitation type and intensity on WeatherMakerPrecipitationManagerScript, weather profiles and profile groups, thunder/lightning, and wind intensity. Do NOT use for first-time prefab/pipeline setup (see weather-maker-setup-and-overview), time of day (see weather-maker-day-night-cycle), cloud-only look without precipitation (see weather-maker-volumetric-clouds), fog-only (see weather-maker-fog-and-atmosphere), regional/indoor zones (see weather-maker-weather-zones), or water (see weather-maker-water). When in doubt whether a weather request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Control precipitation and weather with Weather Maker

Change the active precipitation (rain, snow, sleet, hail), intensity, bundled weather profiles, lightning, and wind. Prefer weather profiles when the user wants a complete look (clouds + precip + fog + wind + audio). Use the precipitation manager when they only want particles.

## When to use this skill

- "make it rain" / "make it snow" / "hailstorm" / "sleet"
- "start a thunderstorm" / "add lightning"
- "turn weather off" / "clear skies and no rain"
- "weather intensity is too weak" / "rain isn't showing"

Not for: scene setup (`weather-maker-setup-and-overview`), day/night (`weather-maker-day-night-cycle`), cloud profiles without precip (`weather-maker-volumetric-clouds`), fog volumes (`weather-maker-fog-and-atmosphere`), weather/null zones (`weather-maker-weather-zones`), water (`weather-maker-water`).

## Prerequisites

- Weather Maker 8.1.1, Unity 2019.4.35+, Built-in or URP (URP: Unity 6000+ / URP 17.3+).
- Scene already contains `WeatherMakerPrefab` (or 2D variant) with `Precipitation` children enabled. If not, run `weather-maker-setup-and-overview` first.

**Install check:** type `DigitalRuby.WeatherMaker.WeatherMakerPrecipitationManagerScript` exists and `Object.FindObjectOfType<WeatherMakerPrecipitationManagerScript>()` is non-null. If missing, add the prefab via `Window > Weather Maker > Add Weather Maker to Scene`. Asset Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955

HDR must be on for rain/mist to look correct. Precipitation GameObjects under the prefab must stay in the hierarchy (disable, do not delete).

## Quick start

1. Confirm `WeatherMakerPrefab` is in the scene and `Precipitation` is active.
2. In Play mode (or via a runtime script), set precipitation type and intensity:

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

public class WeatherMakerMakeRain : MonoBehaviour
{
    void Start()
    {
        var precip = WeatherMakerPrecipitationManagerScript.Instance;
        precip.PrecipitationChangeDuration = 4.0f; // default: 4 seconds to cross-fade types
        precip.Precipitation = WeatherMakerPrecipitationType.Rain;
        precip.PrecipitationIntensity = 0.5f; // 0–1
    }
}
```

3. Watch the Game view: rain particles follow the camera (`IsFirstPerson` default on 3D rain), rain audio cross-fades Light → Medium → Heavy by intensity.

Expected result: `WeatherMakerPrecipitationManagerScript.Precipitation == Rain` and `PrecipitationIntensity` ≈ 0.5. Rain particle systems emit. Console has no errors.

## Workflows

### Workflow: Set rain, snow, sleet, hail, or none

**Goal:** Drive a single precipitation type without swapping a full weather profile.

**Steps:**

1. Use `WeatherMakerPrecipitationType`: `None = 0`, `Rain = 1`, `Snow = 2`, `Sleet = 3`, `Hail = 4`, `Custom = 127`.
2. Assign `Precipitation` first, then `PrecipitationIntensity` (`0` = off, `1` = maximum). Defaults: type `None`, intensity `0`.
3. `PrecipitationChangeDuration` (default `4`) is the cross-fade time between types. `PrecipitationChangeDelay` (default `0`) delays intensity changes. `PrecipitationChangeThreshold` (default `0.1`) — smaller intensity deltas skip the long fade.
4. Intensity stages: None → Light → Medium → Heavy. Keep intensity multipliers at `1` unless asked; values above `1` are expensive (snow is the heaviest CPU cost).
5. Custom type: assign `CustomPrecipitationScript` (`WeatherMakerFallingParticleScript`) then set `Precipitation = Custom`.

**Expected result:** Only one precipitation type is active. Setting `None` or intensity `0` stops emission after the change duration.

### Workflow: Apply a weather profile (clouds + precip + fog + wind + audio)

**Goal:** One-shot “thunderstorm” or “blizzard” using shipped profiles.

**Steps:**

1. Profiles live under `Assets/WeatherMaker/Prefab/Profiles/Weather/Individual/` (e.g. `Rain/WeatherMakerProfile_Storm`, `Rain/WeatherMakerProfile_HeavyRain`, `Snow/WeatherMakerProfile_Blizzard`, `Other/WeatherMakerProfile_None`).
2. Create new profiles with `Create > WeatherMaker > Weather Profile`. Duplicate before editing. A `WeatherMakerProfileScript` holds Cloud, Sky, Aurora, Precipitation, Fog, Wind, Lightning, and Sound profile slots.
3. Apply via a weather zone (preferred). On `WeatherMakerPrefab/GlobalWeatherZone` (`WeatherMakerWeatherZoneScript`): activate the GameObject, assign `SingleProfile` to the asset (e.g. `WeatherMakerProfile_Storm`). If `SingleProfile` is set, `ProfileGroup` is ignored. For random rotating weather, leave `SingleProfile` null and assign a group such as `WeatherMakerProfileGroup_Temperate` (`Create > WeatherMaker > Weather Profile Group`).
4. First transition is instant while `WeatherMakerScript.Instance.HasHadWeatherTransition == false`. Set it back to `false` to force the next change to be instant. Later changes use each profile’s `TransitionDuration` / `HoldDuration` (0 means fall back to the group; random helper returns `10` seconds when max is 0).
5. Player must have AudioListener + kinematic Rigidbody + tiny trigger collider or the zone will not apply. See `weather-maker-weather-zones`.

Do **not** assign `WeatherMakerScript.LastLocalProfile` — that setter is observational; the comment on the API says it has no effect for driving weather. Use a zone.

**Expected result:** Entering Play (or the zone) transitions clouds, precipitation, fog, wind, lightning, and zone sounds to the profile. `LastLocalProfile` becomes the assigned profile after the zone triggers.

### Workflow: Lightning and thunder

**Goal:** Enable storm lightning or fire a bolt on demand.

**Steps:**

1. Keep `WeatherMakerPrefab/Precipitation/ThunderAndLightningPrefab` active (`WeatherMakerThunderAndLightningScript`).
2. Set `EnableLightning = true` **after** other lightning properties. Default interval: `LightningIntervalTimeRange` min `10`, max `25` seconds. `LightningIntenseProbability` default `0.2`.
3. Camera far plane must include bolt distances, or reduce `StartYBase` (default 500–600) / `StartYBaseCloudOnly` (default 1500–5000).
4. Scripted bolts (Play mode):

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

WeatherMakerThunderAndLightningScript.Instance.EnableLightning = true;
WeatherMakerThunderAndLightningScript.Instance.CallNormalLightning();
WeatherMakerThunderAndLightningScript.Instance.CallIntenseLightning();
// optional forced path:
WeatherMakerThunderAndLightningScript.Instance.CallNormalLightning(start, end);
```

5. Callbacks: `LightningStartedCallback`, `LightningEndedCallback`, `ThunderSoundPlayed`.
6. Performance: lower bolt **Generations**, set light percent to 0, zero glow if GPU-bound.

**Expected result:** A bolt renders and thunder audio plays. Random bolts continue while `EnableLightning` is true.

### Workflow: Wind

**Goal:** Blow precipitation, trees, fog, and (if enabled) water waves.

**Steps:**

1. `WeatherMakerPrefab/Wind` (`WeatherMakerWindScript`). `WindIntensity` default `0` (off). Range `0–1`. Effective speed = profile max speed × intensity × `ExternalIntensityMultiplier`.
2. Assign a wind profile from `Assets/WeatherMaker/Prefab/Profiles/Wind` or `Create > WeatherMaker > Wind Profile`.
3. Random direction: driven by the profile’s `WindMaximumChangeRotation` and `WindChangeInterval`. Set those ranges to 0 for a fixed direction, then rotate the Wind transform.
4. Prefab `WeatherMakerWindZonePrefab` adds localized wind. See `weather-maker-weather-zones` for volume placement.

**Expected result:** `WindIntensity > 0` produces a non-zero `CurrentWindVelocity` in Play mode; rain/snow streaks lean with the wind.

## Verification

- `WeatherMakerPrecipitationManagerScript.Instance` is non-null.
- After “make it rain”: `Precipitation == Rain` and `PrecipitationIntensity > 0`. Rain child particle systems are emitting.
- After “clear weather”: `Precipitation == None` or intensity `0`, or `SingleProfile` is `WeatherMakerProfile_None`.
- After a storm profile: clouds script is enabled, lightning object is active if the profile includes lightning.
- Console has no unhandled exceptions.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

var p = WeatherMakerPrecipitationManagerScript.Instance;
bool pass = p != null && p.Precipitation == WeatherMakerPrecipitationType.Rain && p.PrecipitationIntensity > 0.05f;
Debug.Log(pass ? "PASS: rain active" : "FAIL: rain not active");
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `WeatherMakerPrecipitationManagerScript.Instance` | Component | `Precipitation`, `PrecipitationIntensity` (0–1), change duration/delay/threshold |
| `WeatherMakerPrecipitationType` | Enum | None, Rain, Snow, Sleet, Hail, Custom |
| `WeatherMakerProfileScript` | ScriptableObject | Bundles cloud/sky/precip/fog/wind/lightning/sound. `Create > WeatherMaker > Weather Profile` |
| `WeatherMakerWeatherZoneScript.SingleProfile` / `ProfileGroup` | Fields | Applies a profile or random group when the player trigger enters the collider |
| `WeatherMakerThunderAndLightningScript.Instance` | Component | `EnableLightning`, `CallNormalLightning`, `CallIntenseLightning` |
| `WeatherMakerWindScript.Instance` | Component | `WindIntensity` (default 0), `WindProfile`, `CurrentWindVelocity` |
| `WeatherMakerScript.HasHadWeatherTransition` | Bool | First zone transition is instant while false |

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| No rain in view | Intensity 0, Precipitation None, or Precipitation object disabled | Set type + intensity; enable Precipitation children |
| Rain/mist looks washed out | HDR off | Enable HDR on the camera |
| Weather profile does nothing | Zone not triggered / LastLocalProfile assigned directly | Activate GlobalWeatherZone or a collider zone; fix player trigger; do not set LastLocalProfile |
| Lightning invisible | Far plane too small | Raise camera far plane or lower StartY ranges |
| Snow flies the wrong way | Velocity over lifetime Y positive | Keep Y negative except splash explosions |
| Snow too expensive | High emission | Lower intensity; disable collisions on the performance profile; raise mist threshold to 1 |
| Indoor rain | No null zone | See `weather-maker-weather-zones` |

## Boundaries

- Precipitation manager changes particles (and their audio). It does not by itself swap volumetric cloud or fog profiles — use a `WeatherMakerProfileScript` on a zone for a full storm look.
- Custom precipitation requires your own `WeatherMakerFallingParticleScript` instance.
- Open Weather Map / live location weather is `WeatherMakerLocationWeatherScript` (needs an API key) — not covered here.
- Do not use Unity’s built-in Particle System weather as a replacement while Weather Maker precipitation is active; they will fight visually.
