---
name: weather-maker-fog-and-atmosphere
description: "Use this skill whenever the user wants fog, mist, haze, atmospheric scattering, sky sphere look, or aurora with Weather Maker — e.g. 'add fog', 'ground fog', 'fog cube in the valley', 'make the sky hazier', 'northern lights', 'why is everything gray'. Covers full-screen fog density transitions, fog sphere/cube prefabs, sky and atmosphere profiles, and aurora profile assignment. Do NOT use for adding the prefab (see weather-maker-setup-and-overview), rain (see weather-maker-control-weather), time of day (see weather-maker-day-night-cycle), volumetric cloud cover (see weather-maker-volumetric-clouds), indoor null zones (see weather-maker-weather-zones), or water fog (see weather-maker-water). When in doubt whether a fog or sky-atmosphere request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Fog, sky, and atmosphere with Weather Maker

Drive full-screen volumetric fog, place fog volumes, and adjust sky/atmosphere (and optionally aurora). Fog is **3D perspective only** — it does not run in 2D/orthographic mode.

## When to use this skill

- "add fog" / "thick fog" / "ground fog" / "height fog"
- "fog box in this valley" / "fog sphere"
- "atmospheric haze" / "sky looks wrong" / "too much atmosphere fog"
- "aurora" / "northern lights"

Not for: prefab setup (`weather-maker-setup-and-overview`), precipitation (`weather-maker-control-weather`), time of day (`weather-maker-day-night-cycle`), cloud profiles (`weather-maker-volumetric-clouds`), null/weather zones (`weather-maker-weather-zones`), water (`weather-maker-water`).

## Prerequisites

- Weather Maker 8.1.1, Unity 2019.4.35+, Built-in or URP, perspective camera.
- `WeatherMakerPrefab` in the scene. Enable `FullScreenEffects/Fog` (`WeatherMakerFullScreenFogScript`) for full-screen fog. Turn **off** Unity deferred/post-process fog so it does not double up.

**Install check:** type `DigitalRuby.WeatherMaker.WeatherMakerFullScreenFogScript` exists. For sky: `WeatherMakerSkySphereScript`. If the prefab is missing, run `weather-maker-setup-and-overview`. Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955

Linear color space is required for volumetric fog lights to look correct.

## Quick start

Fade in medium full-screen fog over 5 seconds (Play mode). Fog object must be active and a fog profile assigned:

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

public class WeatherMakerAddFog : MonoBehaviour
{
    void Start()
    {
        var fog = WeatherMakerFullScreenFogScript.Instance;
        fog.gameObject.SetActive(true);
        fog.enabled = true;
        fog.TransitionFogDensity(0f, 0.5f, 5f);
    }
}
```

`TransitionFogDensity` logs an error and returns if the fog script is disabled. Density `0` is the profile default (no visible full-screen fog).

Expected result: Scene gains volumetric fog. `FogProfile.FogDensity` approaches `0.5`. Sun shafts and fog shadows follow the profile and performance profile sample counts.

## Workflows

### Workflow: Full-screen fog profiles and density

**Goal:** Choose a look, then animate density.

**Steps:**

1. Profiles: `Assets/WeatherMaker/Prefab/Profiles/Fog/` — `WeatherMakerFullScreenFogProfile_None`, `_Default`, `_Light`, `_Medium`, `_Heavy`, `_Height`, `_Smoky`, `_Sandstorm`. Volume defaults: `WeatherMakerFogProfile_CubeDefault`, `_SphereDefault`.
2. `Create > WeatherMaker > Full Screen Fog Profile` or `Fog Profile`. Assign to `WeatherMakerFullScreenFogScript.FogProfile`.
3. `FogMode` default `Exponential`. `UseUnityFog` default `false` — leave false to use Weather Maker volumetric fog.
4. Animate profile: `ShowFogAnimated(toProfile, delay, duration, newDensity)` copies and tweens density/color/noise. Animate density only: `TransitionFogDensity(from, to, seconds)`.
5. Height/ground fog: use `_Height` or set height on the full-screen fog profile (`DemoSceneHeightFog.unity`).
6. Noise holes: negative **FogNoiseAdder**. `WeatherMakerLightManagerScript.NoiseTexture3D` must be set for noise fog; null it to save memory if unused.
7. Lights in fog: `WeatherMakerPerformanceProfileScript.EnableFogLights` default `true`. Register extra lights on `WeatherMakerLightManagerScript` (`AutoAddLights` list). Sun fog shadows need `EnableFogLights` and a shadow sample count > 0 (expensive).
8. `DownSampleScale` default `FullResolution`. `FogRenderQueue` default `CameraEvent.AfterForwardAlpha`. `FogShadowStrength` default `1`.
9. If fog is too strong globally, also check **atmosphere** fog on the sky/atmosphere profile — that is separate from this full-screen fog.

**Expected result:** Visible fog matching the profile. Unity `RenderSettings.fog` is only used when `UseUnityFog` is true.

### Workflow: Fog cube and fog sphere volumes

**Goal:** Local fog in a region, not full screen.

**Steps:**

1. Instantiate `Assets/WeatherMaker/Prefab/WeatherMakerVolumetricFogCube.prefab` (`WeatherMakerBoxFogScript`) or `WeatherMakerVolumetricFogSphere.prefab` (`WeatherMakerSphereFogScript`).
2. Place and scale the volume. Assign `WeatherMakerFogProfile_CubeDefault` / `_SphereDefault` or a custom `Create > WeatherMaker > Fog Profile`.
3. Demos: `DemoSceneFogCubeTest.unity`.

**Expected result:** Fog is contained in the collider/mesh bounds, not the whole camera.

### Workflow: Sky sphere and atmosphere

**Goal:** Change sky model and atmospheric scattering.

**Steps:**

1. `WeatherMakerPrefab/SkySphere` (`WeatherMakerSkySphereScript`). Keep the script enabled. Disable only the MeshRenderer if using a custom skybox (and set cloud render queue After Skybox — see setup skill).
2. `SkySphereProfile` (`Create > WeatherMaker > Sky Profile`). If null at runtime, loads `WeatherMakerSkyProfile_Procedural`. Nested `AtmosphereProfile` (`Create > WeatherMaker > Atmosphere Profile`) loads `WeatherMakerAtmosphereProfile_Default` if null.
3. `WeatherMakeSkyMode`: `Textured = 0`, `ProceduralTexturedUnityStyle = 1`, `ProceduralUnityStyle = 2`, `ProceduralTexturedPhysicallyBased = 3`, `ProceduralPhysicallyBased = 4`.
4. Night: `NightIntensity` default `1`. Raise if stars are invisible.
5. Atmosphere is controlled by the atmosphere profile plus the **performance profile** (scattering on/off, sample count, shaft length). Tune camera far plane **before** scattering; horizon sampling depends on it.
6. Too much gray haze: lower atmosphere fog density on the atmosphere/sky profile, not only full-screen fog.

**Expected result:** Sky sphere matches the chosen mode. Physical/procedural modes respond to the sun from the day/night cycle.

### Workflow: Aurora borealis

**Goal:** Enable northern lights.

**Steps:**

1. Assign an aurora profile on `WeatherMakerFullScreenCloudsScript.AuroraProfile` (`Create > WeatherMaker > Aurora Profile`, assets under `Prefab/Profiles/Aurora`).
2. `AuroraAnimationDuration` default `10` seconds when switching profiles.
3. Demo/tutorial is in `UserManual.md` §15. Aurora is procedural (no texture lookup).

**Expected result:** Aurora visible at night along the sky, including from high altitude.

## Verification

- For full-screen fog: Fog child active, `WeatherMakerFullScreenFogScript.FogProfile != null`, `FogProfile.FogDensity` matches the requested amount (within tween).
- `UseUnityFog == false` unless the user asked for Unity fog.
- Unity post-process/deferred fog disabled.
- For volumes: scene contains `WeatherMakerBoxFogScript` or `WeatherMakerSphereFogScript` with a non-null profile.
- Sky: `WeatherMakerSkySphereScript` enabled; `SkySphereProfile` assigned.
- Console has no “Fog script must be enabled to show fog”.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

var f = WeatherMakerFullScreenFogScript.Instance;
bool pass = f != null && f.isActiveAndEnabled && f.FogProfile != null && f.FogProfile.FogDensity > 0.01f;
Debug.Log(pass ? "PASS: fog density > 0" : "FAIL: fog not active");
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `WeatherMakerFullScreenFogScript.Instance` | Component | Full-screen fog. `FogProfile`, `UseUnityFog`, `TransitionFogDensity`, `ShowFogAnimated` |
| `WeatherMakerFogProfileScript.FogDensity` | Float 0–1 | Default `0`. Mode default Exponential |
| `WeatherMakerBoxFogScript` / `WeatherMakerSphereFogScript` | Components | Volume fog on cube/sphere prefabs |
| `WeatherMakerSkySphereScript.Instance` | Component | `SkySphereProfile` |
| `WeatherMakerAtmosphereProfileScript` | ScriptableObject | Scattering / atmosphere fog |
| `WeatherMakerFullScreenCloudsScript.AuroraProfile` | Field | Aurora look; `AuroraAnimationDuration` default 10 |
| `WeatherMakerPerformanceProfileScript.EnableFogLights` | Bool | Default true; required for fog light shafts/shadows |

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| TransitionFogDensity errors | Fog component disabled | Enable Fog GameObject + script |
| Too much fog / world gray | Atmosphere fog + full-screen fog stacked, or pipeline mismatch | Lower atmosphere density; confirm URP/Built-in enable menu |
| Fog white or invisible | Prefab or Sun disabled | Enable Weather Maker + Sun |
| Fog ignored in 2D | Orthographic camera | Fog is 3D only |
| Double fog | Unity deferred/post fog on | Disable Unity fog; Setup Post Processing turns layer fog off |
| Noisy fog missing | NoiseTexture3D null | Assign the 3D noise on Light Manager |
| Tree billboards wrong in fog | Stock tree shader | Enable `WeatherMakerTreeBillboardShader` per file header |

## Boundaries

- Fog does not run in 2D/orthographic. Use precipitation mist for 2D moisture cues.
- Full-screen fog is not a particle system. Do not add Unity Fog on top.
- Underwater fog is part of `weather-maker-water`, not this skill.
- Blocking fog inside buildings is `weather-maker-weather-zones` (null zones).
- Meteor showers are a separate prefab/profile (`WeatherMakerMeteorShower`) — out of scope here.
