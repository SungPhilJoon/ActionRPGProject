---
name: weather-maker-water
description: "Use this skill whenever the user wants Weather Maker water, an ocean, a lake, waves, caustics, or underwater rendering — e.g. 'add an ocean', 'put a lake here', 'make waves', 'underwater fog', 'water is clipping the terrain'. Covers WeatherMakerWaterPrefab, water profiles, wind-driven waves, depth camera, and placement rules. Do NOT use for adding the weather prefab (see weather-maker-setup-and-overview), rain (see weather-maker-control-weather), time of day (see weather-maker-day-night-cycle), clouds (see weather-maker-volumetric-clouds), full-screen fog (see weather-maker-fog-and-atmosphere), or weather/null zones (see weather-maker-weather-zones). When in doubt whether a water request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Add Weather Maker water

Drop the water prefab, assign a profile (ocean, lake, tessellated), and place it below surrounding terrain. Water is a separate prefab from `WeatherMakerPrefab` — the weather system should already be in the scene so lighting, wind, and fog match.

## When to use this skill

- "add an ocean" / "add a lake" / "add water"
- "bigger waves" / "calm water" / "caustics"
- "underwater" / "water reflections too expensive"
- "water fog bleeding through the shore"

Not for: weather prefab setup (`weather-maker-setup-and-overview`), rain (`weather-maker-control-weather`), day/night (`weather-maker-day-night-cycle`), clouds (`weather-maker-volumetric-clouds`), full-screen fog (`weather-maker-fog-and-atmosphere`), weather/null zones (`weather-maker-weather-zones`) except using a null zone to hide water indoors.

## Prerequisites

- Weather Maker 8.1.1, Unity 2019.4.35+, Built-in or URP.
- `WeatherMakerPrefab` already in the scene (sun, light manager, wind). If missing, run `weather-maker-setup-and-overview`.
- Screen-space shadows enabled if caustics should receive shadows.

**Install check:** type `DigitalRuby.WeatherMaker.WeatherMakerWaterScript` exists and prefab `Assets/WeatherMaker/Prefab/WeatherMakerWaterPrefab.prefab` exists. Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955

There is **no** Window menu to add water — instantiate the prefab.

## Quick start

1. Keep `WeatherMakerPrefab` in the scene.
2. Drag `Assets/WeatherMaker/Prefab/WeatherMakerWaterPrefab.prefab` into the Hierarchy (or instantiate via Editor script). The object requires `WeatherMakerWaterScript` plus `MeshRenderer`, `MeshFilter`, and `BoxCollider`.
3. Place the plane at least **2 world units below** surrounding ground so the water null-zone fade does not bleed through terrain.
4. Assign a profile on `WeatherMakerWaterScript.WaterProfile` if the prefab’s default is not what the user asked for:
   - `WeatherMakerWaterProfileOcean` / `WeatherMakerWaterTesselatedProfileOcean` — ocean
   - `WeatherMakerWaterProfileLake` — lake
   - `WeatherMakerWaterProfileBlue` / `WeatherMakerWaterTesselatedProfileBlue` / `WeatherMakerWaterProfileClear` — other looks
5. Scale XZ to the body of water. Press Play.

Expected result: Hierarchy contains `WeatherMakerWaterPrefab` (or the instantiated name) with a non-null `WaterProfile` and visible water mesh. Reflections and waves respond to the sun and, if `WindAffectsWaves` is true (profile default), to `WeatherMakerWindScript`.

```csharp
using UnityEditor;
using UnityEngine;

string[] guids = AssetDatabase.FindAssets("WeatherMakerWaterPrefab");
foreach (string guid in guids)
{
    string path = AssetDatabase.GUIDToAssetPath(guid);
    if (!path.EndsWith("WeatherMakerWaterPrefab.prefab")) continue;
    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    obj.name = "WeatherMakerWaterPrefab";
    obj.transform.position = new Vector3(0f, -2f, 0f);
    Undo.RegisterCreatedObjectUndo(obj, "Add Weather Maker Water");
    Selection.activeGameObject = obj;
    break;
}
```

## Workflows

### Workflow: Choose tessellated vs non-tessellated profiles

**Goal:** Match quality to platform.

**Steps:**

1. `Create > WeatherMaker > Water Profile` to duplicate a shipped profile before editing. `WeatherMakerWaterProfileScript` fields: `Material`, `WaterDepthThreshold` (default `0` = no extra depth write; raise if fog/depth effects over deep water), `WindAffectsWaves` (default `true`), underwater/splash audio clips.
2. Tessellated profiles (`WeatherMakerWaterTesselatedProfileOcean`, `WeatherMakerWaterTesselatedProfileBlue`) use displacement. `UserManual.md` §19.4: tessellation params drive vertex count and max displacement (set max displacement to max wave height for correct culling).
3. Rendering modes in the water material (manual §19.1): **One Pass** (all lights, better on mobile/VR) vs **Forward Base + Add** (one draw per light).
4. Changing `WaterProfile` at runtime assigns `MeshRenderer.sharedMaterial` from `WaterProfile.Material` and sets the depth camera `Dirty = true`.

**Expected result:** Ocean uses an ocean profile; lakes use the lake profile. Material on the renderer matches the profile.

### Workflow: Waves, wind, and depth camera

**Goal:** Visible waves that shrink in shallows.

**Steps:**

1. Up to 8 waves on the water material: `WaterWave1`…`WaterWave8`. `(x,y)` direction, `z` amplitude, `w` wavelength. All-zero disables that slot. Extra vector: `x` speed, `y` height reducer.
2. `WindAffectsWaves` true: `WeatherMakerWindScript.WindIntensity` drives wave scale. Zero wind → calmer water.
3. `WeatherMakerDepthCameraScript` on the water snapshots a height/depth map of **static** layers (terrain, mesh floors). Do not include boats/swimmers in those layers. After moving static geometry, set `Dirty = true` (recompute is expensive).
4. Shallow water from the depth map reduces wave height and increases fade.

**Expected result:** Waves animate; near terrain beaches they flatten. Wind intensity 0 is visibly calmer than intensity 1.

### Workflow: Underwater, caustics, reflections

**Goal:** Camera can go under the surface with correct fog/audio.

**Steps:**

1. Underwater is built in. Optional: post-process volume on the water object (color grading + depth of field recommended).
2. `UnderwaterAudioSource` / `SplashAudioSource` on `WeatherMakerWaterScript`; clips come from the profile. Subscribe `UnderwaterCallback` for custom logic.
3. Caustics: configured on the water material; need screen-space shadows to shadow caustics. Sparkle off: `_SparkleScale.w = 0` (manual §19.2).
4. Reflections: expensive. For performance, set reflection layers to None or use VertexLit reflection shader in simple daytime scenes (`UserManual.md` §36).
5. Shore fade: material `inv fade.x` — values `< 100` fade softer but can artifact refraction; `> 100` harder shoreline, correct refraction.

**Expected result:** Dipping the camera below the plane applies underwater rendering and looping underwater audio if clips are assigned.

## Verification

- Scene contains `WeatherMakerWaterScript` with `WaterProfile != null` and `MeshRenderer` enabled.
- Water Y is ≥ 2 units below nearby walkable terrain (unless the user wants a flood).
- `WeatherMakerPrefab` still present so sun/wind exist.
- Play: water mesh visible; Console has no missing water material/shader errors.
- Demo reference: `Assets/WeatherMaker/Demo/Scenes/DemoSceneWater.unity`.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

var w = Object.FindObjectOfType<WeatherMakerWaterScript>();
bool pass = w != null && w.WaterProfile != null && w.GetComponent<MeshRenderer>() != null;
Debug.Log(pass ? "PASS: water prefab + profile" : "FAIL: water missing");
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `WeatherMakerWaterPrefab.prefab` | Prefab | Drop-in water; no Window menu |
| `WeatherMakerWaterScript` | Component | `WaterProfile`, underwater/splash audio, `UnderwaterCallback` |
| `WeatherMakerWaterProfileScript` | ScriptableObject | `Create > WeatherMaker > Water Profile`. `WindAffectsWaves` default true, `WaterDepthThreshold` default 0 |
| `WeatherMakerDepthCameraScript` | Component | Depth/height map; set `Dirty = true` after static geo changes |
| `WeatherMakerWindScript.WindIntensity` | Float | Drives waves when profile `WindAffectsWaves` is true |

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| Water fog/fade on dry ground | Plane too close to terrain | Lower water ≥ 2 units or raise null-zone fade tolerances |
| No waves | All wave slots zero, or wind 0 with WindAffectsWaves | Set wave amplitudes; add wind intensity |
| Depth map stale | Static terrain moved | `Dirty = true` on depth camera |
| Caustics unshadowed | Screen-space shadows off | Enable SSS in graphics / URP features |
| Reflections kill FPS | Too many reflection layers / forward reflections | Restrict layers; VertexLit reflection; or disable |
| Prefab missing scripts | Weather Maker not imported | Reimport; confirm `WeatherMakerWaterScript` compiles |

## Boundaries

- This is Weather Maker’s water, not Unity’s Standard water, Crest, or AQUAS. Crest integration (cloud shadows on Crest) is `Window > Weather Maker > Integrations > Crest > Enable Crest Cloud Shadows` and is out of scope except as a boundary.
- Water is not a weather zone. Hiding water inside a boat interior uses a null zone with the **Water** mask (`weather-maker-weather-zones`).
- Do not parent water under the Weather Maker prefab in a way that origin-offset or DontDestroyOnLoad duplicates it unexpectedly; keep it a scene object scaled to the body of water.
- WebGL/OpenGL are unsupported for Weather Maker as a whole, including water shaders.
