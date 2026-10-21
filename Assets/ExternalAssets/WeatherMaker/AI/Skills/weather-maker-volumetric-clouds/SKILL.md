---
name: weather-maker-volumetric-clouds
description: "Use this skill whenever the user wants volumetric clouds, overcast, fly-through clouds, cloud shadows, or cloud density probes with Weather Maker — e.g. 'add volumetric clouds', 'make it overcast', 'fly through the clouds', 'why are clouds flickering', 'sample cloud density'. Covers cloud profiles on WeatherMakerFullScreenCloudsScript, ShowCloudsAnimated/HideCloudsAnimated, fly-through profile, probes, and performance knobs. Do NOT use for adding the prefab (see weather-maker-setup-and-overview), rain/storms (see weather-maker-control-weather), time of day (see weather-maker-day-night-cycle), fog (see weather-maker-fog-and-atmosphere), zones (see weather-maker-weather-zones), or water (see weather-maker-water). When in doubt whether a sky-cloud request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Configure volumetric clouds with Weather Maker

Assign and animate volumetric (and flat-layer) cloud profiles, enable fly-through, and sample cloud density. Clouds are a full-screen pass on `WeatherMakerPrefab/FullScreenEffects/Clouds` — keep that object enabled; it also composites sun, moon, sky, and atmosphere.

## When to use this skill

- "add volumetric clouds" / "make it overcast" / "storm clouds"
- "I want to fly through clouds" / "flight sim clouds"
- "clouds are flickering / ghosting / too expensive"
- "get cloud density at this transform" / cloud probes

Not for: prefab/pipeline setup (`weather-maker-setup-and-overview`), precipitation (`weather-maker-control-weather`), time of day (`weather-maker-day-night-cycle`), fog (`weather-maker-fog-and-atmosphere`), zones (`weather-maker-weather-zones`), water (`weather-maker-water`).

## Prerequisites

- Weather Maker 8.1.1, Unity 2019.4.35+, Built-in or URP.
- `WeatherMakerPrefab` in the scene. `FullScreenEffects/Clouds` enabled (`WeatherMakerFullScreenCloudsScript`). Camera far plane ≥ 10000 (100000+ for high altitude). Screen-space shadows on for cloud shadows.

**Install check:** type `DigitalRuby.WeatherMaker.WeatherMakerFullScreenCloudsScript` exists and Instance/FindObjectOfType is non-null. If missing, run `weather-maker-setup-and-overview`. Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955

The class name is **CloudsScript** (plural). Do not call `WeatherMakerFullScreenCloudScript`.

## Quick start

Animate to a storm cloud profile over 8 seconds (Play mode). The name must exist in `WeatherMakerScript.ResourceContainer`:

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

public class WeatherMakerShowStormClouds : MonoBehaviour
{
    void Start()
    {
        var clouds = WeatherMakerFullScreenCloudsScript.Instance;
        clouds.ShowCloudsAnimated(8f, "WeatherMakerCloudProfile_Storm");
    }
}
```

Expected result: Volumetric cover increases to the Storm profile. Game view shows thick clouds. `CloudProfile` is no longer the None profile. For a named hide: `HideCloudsAnimated(8f)`.

## Workflows

### Workflow: Assign or animate a cloud profile

**Goal:** Change cloud look using shipped profiles.

**Steps:**

1. Profile assets: `Assets/WeatherMaker/Prefab/Profiles/Clouds/Profiles/`. Names include `WeatherMakerCloudProfile_None`, `_Light`, `_LightScattered`, `_LightMedium`, `_Medium`, `_MediumScattered`, `_PartlyCloudy`, `_Puffy`, `_Overcast`, `_Gloomy`, `_HeavyScattered`, `_HeavyBright`, `_HeavyDark`, `_Storm`, `_StormMultiLayer`, `_RollingStorm`, `_Flythrough`, `_CloudBox`.
2. `Create > WeatherMaker > Cloud Profile` (and Cloud Layer / Cloud Layer Volumetric / Cloud Noise Profile as needed). Duplicate before editing.
3. Inspector (edit or play): drag a profile onto `WeatherMakerFullScreenCloudsScript.CloudProfile`. `CloudSeed` default `-1` (auto). `DownSampleScale` default **HalfResolution**. `TemporalReprojection` default **TwoByTwo**. `RenderQueue` default `CameraEvent.BeforeImageEffectsOpaque`.
4. Play-mode animate from a resource-container name (must match `LoadResource`): `ShowCloudsAnimated(duration, profileName)`. Or pass an asset: `ShowCloudsAnimated(profile, transitionDelay, transitionDuration)`. Passing `null` profile hides clouds.
5. Smooth fades require matching noise textures on layers in both profiles (API comment on `ShowCloudsAnimated`).
6. `ShowCloudsAnimated` logs an error if the clouds object is disabled, or if `PerformanceProfile` / sun is missing.

**Expected result:** Cloud coverage matches the chosen profile. `CloudGlobalShadow` updates as cover changes.

### Workflow: Fly-through / high altitude

**Goal:** Camera can enter the cloud volume without a flat “ceiling.”

**Steps:**

1. Assign `WeatherMakerCloudProfile_Flythrough` (see `DemoSceneFlythrough.unity`).
2. Camera far plane ≥ **100000**.
3. Expect some horizon artifacts; terrain/mountains hide them (known limitation in `UserManual.md` §38).
4. Demo `DemoSceneCloudBox.unity` tests a bounded cloud volume (`WeatherMakerCloudProfile_CloudBox`).

**Expected result:** Flying the camera up enters volumetric cloud density instead of popping through a thin layer.

### Workflow: Cloud probes

**Goal:** Read cloud density on the CPU for a Transform (e.g. aircraft in cloud).

**Steps:**

1. Requires compute shaders and async GPU readback: `WeatherMakerFullScreenCloudsScript.CloudProbeEnabled`.
2. Add `WeatherMakerCloudProbeScript` to the object. Set `ProbeSource` (default this transform). `ProbeDestination` null/same = point sample; different = ray accumulation. `ProbeSourceIsCamera` overrides source to the rendering camera.
3. The probe script calls `RequestCloudProbe` each camera pre-cull. Results must be requested every frame (cleared each frame).
4. Read: `WeatherMakerFullScreenCloudsScript.Instance.GetCloudProbe(camera, source, dest)` returns `CloudProbeResult` with `DensitySource`, `DensityTarget`, `DensityRaySum`, `DensityRayAverage`.

**Expected result:** Inside thick clouds `DensitySource` is > 0. Outside, near 0.

### Workflow: Performance and visual stability

**Goal:** Recover frame rate or stop flicker.

**Steps:**

1. Lower Unity Quality or assign a cheaper `WeatherMakerPerformanceProfile` (`WeatherMakerScript.Instance.PerformanceProfile = profile`, `Create > WeatherMaker > Performance Profile`). `AutoSetPerformanceProfile` default `true`.
2. Clouds: raise downsample scale, keep temporal reprojection, reduce volumetric sample counts on the performance profile, simplify/disable flat layers.
3. Ghosting, blur, or jitter: set `TemporalReprojection` to `None` (default is `TwoByTwo`) and/or add temporal anti-aliasing, motion blur, bloom, or color grading.
4. Cloud shadows fade in the distance: raise shadow distance; enable screen-space shadows.
5. Clouds too bright after 8.0.2 lighting changes: on the volumetric layer, try ground ambient `1` and sky ambient `2`.
6. Authoring noise: `Window > Weather Maker > Cloud Noise Editor` (advanced; optional `Generate 3D Texture` / `Extract 3D Texture` menus). Not required for using shipped profiles.

**Expected result:** Frame time drops with a lower performance profile. Shadows remain visible at the gameplay camera distance.

## Verification

- `WeatherMakerFullScreenCloudsScript.Instance` is enabled and the Clouds GameObject is active.
- After showing Storm: resource profile name contains `Storm` or cover is visibly dense vs None.
- After hide: `HideCloudsAnimated` completed and cover is ~0.
- Camera `farClipPlane` adequate for the chosen profile.
- Console has no “Full screen cloud script must be enabled” error.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

var c = WeatherMakerFullScreenCloudsScript.Instance;
bool pass = c != null && c.isActiveAndEnabled && c.CloudProfile != null;
Debug.Log(pass ? "PASS: clouds script + profile" : "FAIL: clouds missing");
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `WeatherMakerFullScreenCloudsScript.Instance` | Component | `CloudProfile`, `ShowCloudsAnimated`, `HideCloudsAnimated`, `GetCloudProbe`, `RequestCloudProbe`, `CloudGlobalShadow` |
| `ShowCloudsAnimated(float duration, string profileName)` | Method | Loads profile by ResourceContainer name, delay 0 |
| `ShowCloudsAnimated(WeatherMakerCloudProfileScript, float delay, float duration)` | Method | Animate to a profile; null hides |
| `WeatherMakerCloudProfileScript` | ScriptableObject | `Create > WeatherMaker > Cloud Profile` |
| `WeatherMakerCloudProbeScript` | Component | Per-object probe requests |
| `WeatherMakerCloudType` | Enum | None, Light, LightScattered, … Overcast, Storm, Custom — used to build names `WeatherMakerCloudProfile_` + enum |
| `Window > Weather Maker > Cloud Noise Editor` | Menu | Author cloud noise textures |

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| ShowCloudsAnimated errors | Clouds disabled or no sun/performance profile | Enable Clouds object; keep DayNightCycle/Sun; assign performance profile |
| Profile name loads null | Name not in ResourceContainer | Use `LoadResource` names already in the container, or add the asset to the container |
| Clouds hidden by skybox | Camera clear flags Skybox | Solid Color clear, or Render Queue After Skybox |
| Flicker / ghost | Temporal reprojection | Set `TemporalReprojection` to `None`; add TAA |
| No shadows on clouds | Screen-space shadows off | Enable in graphics/URP renderer features |
| Edit-mode clouds look stale | Unity skip update | Nudge Scene/Game view; Weather Maker 5.4.0+ supports edit-mode cloud editing |

## Boundaries

- Clouds are not Unity Particle System clouds. Particle cloud demos (`DemoSceneCloudParticles`) are a separate sample, not this API.
- 2D orthographic scenes use different cloud paths (`WeatherMakerLegacyCloudScript2D` in the configuration canvas). Prefer `WeatherMakerPrefab2D` and `DemoScene2D.unity`.
- Generating custom 3D noise is optional power-user work (`Cloud Noise Editor`); shipped profiles are enough for gameplay clouds.
- Weather maps: R = coverage, G = reserved/density, B = type (0 stratus, 1 cumulus) — see `UserManual.md` §13. Do not invent extra channels.
