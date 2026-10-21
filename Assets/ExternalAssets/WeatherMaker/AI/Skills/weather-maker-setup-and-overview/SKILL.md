---
name: weather-maker-setup-and-overview
description: "Use this skill whenever the user wants to install, add, or first-configure Weather Maker in a Unity scene — e.g. 'add weather to my scene', 'drop in the weather prefab', 'enable URP for weather maker', 'why is there no sky', 'set up volumetric weather', 'import weather maker'. Covers install verification, Window > Weather Maker menus, Built-in vs URP enablement, adding WeatherMakerPrefab or WeatherMakerPrefab2D, camera and lighting checklist, player trigger setup, and where key files live. Do NOT use for making it rain or snow (see weather-maker-control-weather), changing time of day (see weather-maker-day-night-cycle), cloud look (see weather-maker-volumetric-clouds), fog (see weather-maker-fog-and-atmosphere), regional or indoor zones (see weather-maker-weather-zones), or oceans/lakes (see weather-maker-water). When in doubt whether a weather, sky, or atmosphere request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Setup Weather Maker

Add Weather Maker to a Unity project or scene, switch render pipelines, satisfy camera and lighting requirements, and confirm the prefab is running. This is the first skill to use when the asset is missing from the scene or the sky, clouds, or weather do not appear after import.

## When to use this skill

- "add weather to my scene" / "drop in weather maker"
- "enable URP" / "switch back to built-in" for Weather Maker
- "why is my sky black / missing" after importing the asset
- "set up the weather prefab" / "first-time weather setup"
- Confirming install, locating prefabs, or opening a demo scene

Not for:

- Rain, snow, hail, sleet, lightning, or swapping weather profiles — see `weather-maker-control-weather`
- Time of day, sun/moon position, lat/lon — see `weather-maker-day-night-cycle`
- Cloud cover, fly-through, probes — see `weather-maker-volumetric-clouds`
- Fog density or fog volumes — see `weather-maker-fog-and-atmosphere`
- Weather zones, null (indoor) zones — see `weather-maker-weather-zones`
- Water / ocean / lake — see `weather-maker-water`

## Prerequisites

- Weather Maker 8.1.1 (folder `Assets/WeatherMaker`). Minimum Unity **2019.4.35**. URP requires **Unity 6000+** and **URP 17.3+** (URP 17.4+ on Unity 6000.4.4+).
- Render pipelines: **Built-in** and **URP**. HDRP is not supported.
- Graphics: texture arrays required. Not supported: WebGL, OpenGL, OpenGL ES, DirectX 9.
- Do not keep **Rain Maker** in the same project — delete it before using Weather Maker.
- Linear color space is strongly recommended (`Edit > Project Settings > Player > Other Settings > Rendering`).
- HDR on the main camera is strongly recommended.

**Install check (run before any other Weather Maker work):**

1. Confirm folder `Assets/WeatherMaker` exists.
2. Confirm menu `Window > Weather Maker > Add Weather Maker to Scene` exists.
3. Confirm type `DigitalRuby.WeatherMaker.WeatherMakerScript` compiles.

If any check fails, tell the user to import Weather Maker from the Asset Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955 then reimport. Support: https://www.digitalruby.com (email support@digitalruby.com). Full manual: `Assets/WeatherMaker/UserManual.md`.

## Quick start

1. Open an empty 3D scene (`File > New Scene`). Ensure the camera is tagged **MainCamera**.
2. For URP projects (Unity 6000+, URP 17.3+): `Window > Weather Maker > Enable Universal Render Pipeline`. Wait for recompile. For Built-in: `Window > Weather Maker > Enable Standard Render Pipeline` if URP was previously enabled.
3. `Window > Weather Maker > Add Weather Maker to Scene`. This instantiates `WeatherMakerPrefab` (perspective camera) or `WeatherMakerPrefab2D` (orthographic), parents it at the scene root, places it at the origin, and sets `Camera.main` clear flags to **Solid Color** with background `(0,0,0,0)`.
4. On `WeatherMakerPrefab` root (`WeatherMakerScript`): leave `IsPermanent` at default `true` unless each scene needs its own prefab. Leave `AllowSceneCamera` `true` unless the Scene view UI corrupts (Unity command-buffer bug).
5. Lighting: `Window > Rendering > Lighting > Environment` — Skybox Material = `WeatherMakerSkyboxMaterial`, Sun Source = the **Sun** child under `DayNightCycle`, Environment Lighting Source = **Color** or **Gradient**, Environment Reflections Source = **Custom**.
6. Camera far plane ≥ **10000** (use ≥ **100000** for flight / high altitude).
7. Enter Play. The Game view should show a procedural sky, sun (default time is noon), optional moon, and volumetric clouds from the prefab’s default cloud profile.

Expected result: Hierarchy contains a root object named `WeatherMakerPrefab` (or `WeatherMakerPrefab2D`) with `WeatherMakerScript` and `WeatherMakerCommandBufferManagerScript` enabled. Console has no Weather Maker compile errors.

## Workflows

### Workflow: Verify install and open a demo

**Goal:** Confirm the package is present and a known-good scene renders.

**Steps:**

1. Confirm `Assets/WeatherMaker/Readme.txt` lists `Current Version : 8.1.1`.
2. Open `Assets/WeatherMaker/Demo/Scenes/DemoScene.unity` (Built-in) or `Assets/WeatherMaker/Prefab/ScriptableRenderPipeline/URP/DemoSceneURP.unity` (URP).
3. Press Play. Confirm sky, clouds, sun, and default ambient audio.

**Expected result:** Demo plays without magenta shaders or missing-script warnings on the Weather Maker prefab.

### Workflow: Enable URP or return to Built-in

**Goal:** Match Weather Maker shaders and defines to the project’s pipeline.

**Steps:**

1. URP: import URP 17.3+ (`com.unity.render-pipelines.universal`), then `Window > Weather Maker > Enable Universal Render Pipeline`. This sets Graphics/Quality pipeline assets to `WeatherMakerURPProfile`, defines `UNITY_URP`, and tries to add Screen Space Shadows (first) plus the Weather Maker URP renderer feature (second).
2. Built-in: `Window > Weather Maker > Enable Standard Render Pipeline`. This clears the default pipeline assets, removes `UNITY_URP` / `UNITY_HDRP`, and restores grab-pass shaders.
3. Wait for script recompile. If URP rendering looks wrong, recreate the URP renderer, add **Screen Space Shadows** then **Weather Maker** render features, and reassign the renderer to the URP asset.

**Expected result:** Player scripting defines include `UNITY_URP` only for URP. Weather Maker demo scene renders in the active pipeline. VR is not supported in URP.

### Workflow: Add Weather Maker without the menu

**Goal:** Instantiate the prefab from script (Editor).

**Steps:**

```csharp
using DigitalRuby.WeatherMaker;
using UnityEditor;
using UnityEngine;

// Run from an Editor script or the Console. This calls the shipped menu command.
UnityEditor.EditorApplication.ExecuteMenuItem("Window/Weather Maker/Add Weather Maker to Scene");
var wm = Object.FindObjectOfType<WeatherMakerScript>();
Debug.Log(wm != null
    ? "PASS: " + wm.gameObject.name + " in scene, IsPermanent=" + wm.IsPermanent
    : "FAIL: WeatherMakerScript not found");
```

Alternatively drag `Assets/WeatherMaker/Prefab/WeatherMakerPrefab.prefab` (3D) or `WeatherMakerPrefab2D.prefab` (2D) to the Hierarchy root. For a 2D game that must strip 3D volumetrics, use `WeatherMakerPrefab2DNo3D.prefab`.

**Expected result:** Same as Quick start. Do not parent the prefab under another moving object; keep it at scene root.

### Workflow: Camera, player, and objects that must stay enabled

**Goal:** Satisfy the runtime contract so zones, audio, sky, and clouds work.

**Steps:**

1. Main camera: **MainCamera** tag, HDR on, far plane ≥ 10000, clear flags **Solid Color** black/clear. Alternative: keep Skybox clear flags, set `WeatherMakerPrefab > FullScreenEffects > Clouds > Render Queue` to **After Skybox**, and disable only the SkySphere **MeshRenderer** (leave `WeatherMakerSkySphereScript` enabled).
2. Player / camera object that owns the **AudioListener**: kinematic **Rigidbody**, Sphere Collider radius `0.001`, **Is Trigger** = true. Weather Maker identifies the local player from the enabled AudioListener. Ready-made: `Window > Weather Maker > Add Weather Maker Player to Scene` (`WeatherMakerPlayer.prefab`).
3. Do **not** delete children of the prefab. Disable unused ones. These must remain **enabled**:
   - Root: `WeatherMakerScript`, `WeatherMakerCommandBufferManagerScript`
   - `AudioManager`
   - `DayNightCycle` (`WeatherMakerDayNightCycleManagerScript`) — freeze time by setting profile **Speed** / **NightSpeed** to `0`, do not disable the component
   - `LightManager`
   - `SkySphere` script (renderer may be disabled)
   - `FullScreenEffects/Clouds` — this pass draws sun, moon, sky, atmosphere, and clouds
4. Optional test UI: drag `Assets/WeatherMaker/Prefab/WeatherMakerConfigurationCanvasPrefab.prefab` into the scene root.
5. Optional post-processing (Built-in stack v2): `Window > Weather Maker > Setup Post Processing`. Requires package `com.unity.postprocessing` and define `UNITY_POST_PROCESSING_STACK_V2`. Turns off deferred fog on the post-process layer.

**Expected result:** Play mode shows sky/clouds. Trigger-based zones will fire once the AudioListener object has the kinematic body + tiny trigger.

### Workflow: Multi-scene / launcher

**Goal:** One permanent Weather Maker instance across menu and gameplay scenes.

**Steps:**

1. Put `WeatherMakerLaunchManager.prefab` and `WeatherMakerPrefab` in the launcher scene (build index 0). `IsPermanent` defaults to `true`.
2. Configure `WeatherMakerLauncherScript` button names and scene names.
3. If each scene needs its own instance, set `IsPermanent = false` and add a prefab per scene.

**Expected result:** Loading the game scene does not create a second `WeatherMakerScript`. `WeatherMakerScript.HasInstance()` remains true.

## Verification

- `Assets/WeatherMaker` exists and `DigitalRuby.WeatherMaker.WeatherMakerScript` compiles.
- Scene contains exactly one active `WeatherMakerScript` (object name `WeatherMakerPrefab` or `WeatherMakerPrefab2D`).
- That object has `WeatherMakerCommandBufferManagerScript`. Children `DayNightCycle`, `LightManager`, `SkySphere`, and `FullScreenEffects/Clouds` are active.
- Main camera: tag `MainCamera`, `clearFlags == CameraClearFlags.SolidColor` (unless using After Skybox workflow), `farClipPlane >= 10000`.
- URP projects: `UNITY_URP` is defined; Graphics Settings default pipeline is assigned.
- Play mode: Console has no missing shader / missing script errors from Weather Maker. Game view is not a solid gray/magenta screen.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

public static class WeatherMakerSetupVerify
{
    public static void Log()
    {
        var wm = Object.FindObjectOfType<WeatherMakerScript>();
        if (wm == null) { Debug.LogError("FAIL: no WeatherMakerScript"); return; }
        bool clouds = wm.transform.Find("FullScreenEffects/Clouds") != null;
        bool day = wm.GetComponentInChildren<WeatherMakerDayNightCycleManagerScript>(true) != null;
        Debug.Log((clouds && day) ? "PASS: Weather Maker hierarchy present" : "FAIL: missing Clouds or DayNightCycle");
    }
}
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `Window > Weather Maker > Add Weather Maker to Scene` | Menu | Instantiates 3D or 2D prefab; sets main camera clear to solid transparent black |
| `Window > Weather Maker > Add Weather Maker Player to Scene` | Menu | Instantiates `WeatherMakerPlayer` at origin |
| `Window > Weather Maker > Enable Universal Render Pipeline` | Menu | Assigns URP profile, sets `UNITY_URP`, syncs renderer features |
| `Window > Weather Maker > Enable Standard Render Pipeline` | Menu | Clears SRP assets, removes `UNITY_URP` / `UNITY_HDRP` |
| `DigitalRuby.WeatherMaker.WeatherMakerScript` | Component | Root manager. `Instance`, `HasInstance()`, `IsPermanent` (default `true`), `AllowSceneCamera` (default `true`), `AutoFindMainCamera` (default `true`), `PerformanceProfile`, `ResourceContainer`, `HasHadWeatherTransition` |
| `WeatherMakerScript.LoadResource<T>(string name)` | Method | Loads a profile/texture by name from `ResourceContainer` (build-included assets only) |
| `WeatherMakerCommandBufferManagerScript` | Component | Required sibling on the prefab root; origin-offset and camera command buffers |
| `WeatherMakerConfigurationCanvasPrefab` | Prefab | Runtime test UI for weather, fog, time of day |

Read `Assets/WeatherMaker/UserManual.md` sections 1–7 for camera checklists, URP, and launcher detail.

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| Menu missing / type not found | Asset not imported | Import from the Asset Store into `Assets/WeatherMaker` |
| Magenta / compile errors after upgrade | Leftover files from pre-5.0.0 | Delete the `WeatherMaker` folder and re-import |
| Sky or clouds missing; camera shows skybox only | Clear flags still Skybox, clouds draw under it | Solid Color `(0,0,0,0)`, or Clouds Render Queue = After Skybox and disable SkySphere mesh renderer |
| Everything gray | Pipeline define mismatch | Enable the matching pipeline menu; confirm HDRP is not in use |
| Scene view white / UI corruption | Unity command-buffer bug | Set `AllowSceneCamera = false` on `WeatherMakerScript` |
| Weather zones never fire | Player lacks AudioListener + kinematic RB + tiny trigger | Follow player setup; or add `WeatherMakerPlayer` |
| Rain Maker compile conflicts | Both assets present | Delete Rain Maker |
| URP looks wrong after Enable URP | Renderer features missing or wrong order | Screen Space Shadows first, Weather Maker second on the URP renderer |
| Effects missing in player build | Profile not in `ResourceContainer` | Duplicate Default/Mobile container, add used profiles, assign on `WeatherMakerScript` |

## Boundaries

- HDRP is not supported. WebGL, OpenGL, OpenGL ES, and DX9 are not supported. VR is not supported in URP.
- Weather Maker needs texture arrays and (for URP) Metal on macOS or DirectX 12 on Windows as the documented path.
- This skill does not change weather, time, clouds, fog, zones, or water — use the sibling skills.
- Networking (Mirror), Open Weather Map, Crest, Wet Stuff, Vegetation Studio Pro, and other integrations are documented in `UserManual.md` §28–32, not here.
- Do not delete prefab children. Do not disable `DayNightCycle` to “pause time” — set Speed to 0 instead.

**Other skills:** `weather-maker-control-weather` · `weather-maker-day-night-cycle` · `weather-maker-volumetric-clouds` · `weather-maker-fog-and-atmosphere` · `weather-maker-weather-zones` · `weather-maker-water`
