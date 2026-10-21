---
name: weather-maker-weather-zones
description: "Use this skill whenever the user wants regional weather, indoor weather masking, or local rain/snow volumes with Weather Maker — e.g. 'no rain inside the house', 'desert here forest there', 'weather zone over the town', 'dampen audio in the cave', 'rain only on this roof'. Covers weather zones and profile groups, GlobalWeatherZone, null zones, dampening zones, precipitation zones, and the player trigger collider recipe. Do NOT use for adding the prefab (see weather-maker-setup-and-overview), global rain without zones (see weather-maker-control-weather), time of day (see weather-maker-day-night-cycle), cloud look (see weather-maker-volumetric-clouds), full-screen fog (see weather-maker-fog-and-atmosphere), or oceans (see weather-maker-water). When in doubt whether a zone or indoor-weather request could involve Weather Maker, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Weather zones and indoor masking with Weather Maker

Bind weather profiles to colliders, suppress effects indoors with null zones, dampen intensity, or place static precipitation volumes. Zones only fire if the local player object is set up correctly.

## When to use this skill

- "no rain inside" / "keep weather out of the cave"
- "rainforest on this side, desert on that side"
- "turn on random global weather"
- "always snow on the mountain peak" (static volume)
- "weather zone isn't triggering"

Not for: first-time prefab (`weather-maker-setup-and-overview`), global type/intensity without volumes (`weather-maker-control-weather`), day/night (`weather-maker-day-night-cycle`), clouds (`weather-maker-volumetric-clouds`), full-screen fog look (`weather-maker-fog-and-atmosphere`), water (`weather-maker-water`).

## Prerequisites

- Weather Maker 8.1.1, Unity 2019.4.35+, Built-in or URP.
- `WeatherMakerPrefab` in the scene.
- **Player trigger recipe** (same GameObject as the enabled AudioListener): kinematic Rigidbody, Sphere Collider radius `0.001`, Is Trigger = true. `Window > Weather Maker > Add Weather Maker Player to Scene` if needed. Physics matrix must allow the player layer to collide with the zone layer (GlobalWeatherZone defaults to Default).

**Install check:** types `DigitalRuby.WeatherMaker.WeatherMakerWeatherZoneScript` and `WeatherMakerNullZoneScript` exist. Store: https://assetstore.unity.com/packages/tools/particles-effects/weather-maker-volumetric-clouds-and-weather-system-for-unity-60955

## Quick start

Turn on whole-scene random-ish weather using the prefab’s global zone:

1. Select `WeatherMakerPrefab/GlobalWeatherZone`.
2. Activate the GameObject.
3. Assign `ProfileGroup` to `Assets/WeatherMaker/Prefab/Profiles/Weather/Groups/WeatherMakerProfileGroup_Temperate.asset` (or `SingleProfile` to one profile such as `WeatherMakerProfile_MediumRain`). If `SingleProfile` is non-null, the group is ignored.
4. Ensure the player/camera AudioListener object has the kinematic body + tiny trigger.
5. Press Play and enter the zone (global zone typically covers the world).

Expected result: Weather transitions to the profile/group. `WeatherMakerScript.WeatherZoneChanged` / `WeatherProfileChanged` fire. `HasHadWeatherTransition` becomes true after the first change (first change is instant).

## Workflows

### Workflow: Create a regional weather zone

**Goal:** Different weather inside a collider.

**Steps:**

1. Empty GameObject, add a Box/Sphere/Mesh collider, **Is Trigger** = true, scale to the region.
2. `Component > Weather Maker > Weather > Weather Zone` (`WeatherMakerWeatherZoneScript`).
3. Assign `SingleProfile` (`Create > WeatherMaker > Weather Profile`) **or** `ProfileGroup` (`Create > WeatherMaker > Weather Profile Group`). Groups pick profiles over time using each profile’s `HoldDuration` / `TransitionDuration`.
4. `TransitionDurationMultiplier` default `0.5` speeds the blend when entering from another zone.
5. Optional per-zone overrides: `CloudProfile`, `SkyProfile`, `AuroraProfile`, `PrecipitationProfile`, `FogProfile`, `WindProfile`, and the other override slots on the script — null means “use the weather profile.”
6. Optional post-processing: put the zone on a dedicated layer included in a post-process volume mask (`DemoSceneWeatherZones.unity`).
7. Nested zones: keep overlaps simple. The collider that contains the player drives the weather.

**Expected result:** Walking into the collider changes clouds/precip/fog/wind/audio to that profile. Leaving reverts to the previous overlapping zone (e.g. global).

### Workflow: Null zones (block weather indoors)

**Goal:** Stop precipitation, fog, overlays, and/or water inside a building, cave, or cockpit.

**Steps:**

1. Prefabs: `WeatherMakerNullZone.prefab`, `WeatherMakerNullZoneOpen.prefab` (open buildings), `WeatherMakerNullZoneClosed.prefab` (sealed interiors), `WeatherMakerNullZoneSphere.prefab`. Instantiate and fit to the interior. Colliders: **box or sphere**, may be rotated and scaled.
2. `WeatherMakerNullZoneScript.NullZoneStrength` default `1` (full). `0` disables. Animate with `SetStrengthAnimated(newStrength, duration)`.
3. Assign `NullZoneProfile` (`Create > WeatherMaker > Null Zone Profile`) or leave null for default. Profile `NullZoneRenderMask` flags: `Precipitation = 1`, `Fog = 2`, `Overlay = 4`, `Water = 8`.
4. Fade: profile state `Fade` default `0.2`. Manual: `100` disables edge fade. `Fade <= 0` special-cases overlay fade as the camera approaches (`abs(fade)` is speed) — used by open structures.
5. Demo: `DemoSceneNullZones.unity`, `DemoSceneIndoor.unity`.

**Expected result:** Inside the volume, masked effects disappear. Rain is gone in a closed house; sky may still be visible through windows depending on the mask.

### Workflow: Dampening zones

**Goal:** Reduce (not fully remove) precip, sound, and lightning light.

**Steps:**

1. Instantiate `WeatherMakerDampeningZone.prefab` (`WeatherMakerDampeningZoneScript`, requires a collider).
2. Defaults: `IntensityDampening = 0`, `SoundDampening = 0.2`. Range 0–1.
3. Same player trigger requirements as other zones.

**Expected result:** Intensity and audio duck while the listener is inside, then restore on exit.

### Workflow: Precipitation zones (static volumes)

**Goal:** Rain/snow that does **not** follow the camera — a peak that always snows, a courtyard that always rains.

**Steps:**

1. Prefabs: `WeatherMakerRainZone.prefab`, `WeatherMakerSnowZone.prefab`, `WeatherMakerHailZone.prefab`, `WeatherMakerSleetZone.prefab`. These use the same falling-particle scripts with `IsFirstPerson = false`.
2. Place, scale emission shape, and tune sound falloff to scene scale.
3. Demo: `DemoScenePrecipitationZones.unity`.
4. Localized wind: `WeatherMakerWindZonePrefab.prefab`.

**Expected result:** Particles emit in world space in that volume regardless of camera position.

## Verification

- Weather zone: GameObject active, trigger collider, `SingleProfile` or `ProfileGroup` assigned.
- Player: AudioListener enabled, kinematic Rigidbody, trigger sphere ~0.001.
- After Play + enter zone: `WeatherMakerScript.Instance.LastLocalProfile` is the expected profile (once the zone has triggered).
- Null zone: `WeatherMakerNullZoneScript` present, `NullZoneStrength > 0`, collider is box or sphere.
- Console clean; no missing scripts on prefabs.

```csharp
using DigitalRuby.WeatherMaker;
using UnityEngine;

var zone = Object.FindObjectOfType<WeatherMakerWeatherZoneScript>();
var playerCol = Object.FindObjectOfType<AudioListener>();
bool zoneOk = zone != null && zone.gameObject.activeInHierarchy &&
    (zone.SingleProfile != null || zone.ProfileGroup != null);
bool playerOk = playerCol != null && playerCol.GetComponent<Rigidbody>() != null;
Debug.Log(zoneOk && playerOk ? "PASS: zone + player trigger pieces" : "FAIL: zone or player setup missing");
```

## API quick reference

| Entry point | Type | What it does |
|-------------|------|----------------|
| `WeatherMakerWeatherZoneScript` | Component | `SingleProfile`, `ProfileGroup`, `TransitionDurationMultiplier` (default 0.5), profile overrides |
| `Component > Weather Maker > Weather > Weather Zone` | Menu | Adds the zone component |
| `WeatherMakerNullZoneScript` | Component | `NullZoneProfile`, `NullZoneStrength` (default 1), `SetStrengthAnimated` |
| `NullZoneRenderMask` | Enum flags | Precipitation, Fog, Overlay, Water |
| `WeatherMakerDampeningZoneScript` | Component | `IntensityDampening`, `SoundDampening` |
| `WeatherMakerScript.HasHadWeatherTransition` | Bool | First transition instant while false |
| `WeatherMakerScript.WeatherProfileChanged` / `WeatherZoneChanged` | Events | Fired for the local player |

## Common issues

| Symptom | Cause | Fix |
|---------|--------|-----|
| Zone never triggers | Missing kinematic RB / tiny trigger / AudioListener, or layer collision off | Player recipe; Physics collision matrix |
| Setting LastLocalProfile does nothing | Field is not the driver | Assign zone SingleProfile/ProfileGroup |
| Rain still inside | Null zone too small, wrong mask, or strength 0 | Fit collider; include Precipitation (and Fog) in mask |
| Overlapping zones fight | Multiple triggers without clear nesting | Simplify overlaps; use TransitionDurationMultiplier |
| Global zone does nothing | GameObject inactive | Activate `GlobalWeatherZone` |

## Boundaries

- Weather zones replaced the legacy weather manager (4.0.0+). Do not look for a separate weather manager component.
- Null zones support **box and sphere** colliders only (not mesh).
- Networked non-local players must **disable** (not delete) extra cameras, audio listeners, and sound zones — see `UserManual.md` §30. This skill does not set up Mirror.
- WAPI (World Manager API) takes over weather; do not combine WAPI extension with weather zones.
- Sound-only ambient zones use `WeatherMakerSoundZone.prefab` (`UserManual.md` §23), not weather profiles.
