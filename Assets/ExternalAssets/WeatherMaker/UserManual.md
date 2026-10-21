# Weather Maker — User Manual

Weather Maker (c) 2016 Digital Ruby, LLC &nbsp;|&nbsp; Created by Jeff Johnson &nbsp;|&nbsp; [digitalruby.com](https://www.digitalruby.com) &nbsp;|&nbsp; support@digitalruby.com

Weather Maker is an all‑in‑one sky, weather, cloud, water, fog, audio, and lighting system for Unity. It replaces the stock skybox, ambient lighting, fog and particle weather of Unity with a unified, profile‑driven system that includes:

- Precipitation (rain, snow, sleet, hail, mist, ripples, collisions, explosions)
- Procedural and textured sky sphere with atmospheric scattering
- Volumetric, flat and 2D clouds with temporal reprojection and cloud probes
- Sun, moon(s) with accurate astronomical positioning and moon phases
- Day/night cycle with ambient color driver
- Full thunder + procedural 3D lightning
- Volumetric fog with sun shafts, shadows, volumetric point/spot/area lights
- Advanced water (tessellated, underwater, caustics, waves, sparkle, foam)
- Weather zones, null zones, dampening zones, precipitation zones, wind zones
- Aurora borealis, meteor showers
- Sound/ambient audio zones with sound groups and scheduling
- Open Weather Map integration, location services, Mirror networking
- Third‑party integrations (Crest, WAPI, Playmaker, Vegetation Studio Pro, MegaSplat/MicroSplat, Uber, RTP, CTS, Gaia, Wet Stuff)
- Built‑in URP (Universal Render Pipeline) support on Unity 6000+

---

## Table of Contents

1. [Requirements and Compatibility](#1-requirements-and-compatibility)
2. [Installation and First‑Time Setup](#2-installation-and-first-time-setup)
3. [Project & Scene Setup Checklist](#3-project--scene-setup-checklist)
4. [The Weather Maker Prefab Tour](#4-the-weather-maker-prefab-tour)
5. [Player Setup](#5-player-setup)
6. [URP Setup](#6-urp-setup)
7. [Launcher / Multi‑Scene Setup](#7-launcher--multi-scene-setup)
8. [Workflow & Profiles](#8-workflow--profiles)
9. [Precipitation](#9-precipitation-rain-snow-sleet-hail)
10. [Weather Zones, Null Zones, Dampening & Precipitation Zones](#10-weather-zones-null-zones-dampening--precipitation-zones)
11. [Sky Sphere, Sun, Moon & Atmospheric Scattering](#11-sky-sphere-sun-moon--atmospheric-scattering)
12. [Day / Night Cycle](#12-day--night-cycle)
13. [Volumetric Clouds](#13-volumetric-clouds)
14. [Cloud Probes](#14-cloud-probes)
15. [Aurora Borealis / Northern Lights](#15-aurora-borealis--northern-lights)
16. [Meteor Shower](#16-meteor-shower)
17. [Lightning & Thunder](#17-lightning--thunder)
18. [Fog (Full Screen, Volumetric Sphere, Volumetric Cube)](#18-fog-full-screen-volumetric-sphere-volumetric-cube)
19. [Water](#19-water)
20. [Full Screen Overlays (Snow & Wetness)](#20-full-screen-overlays-snow--wetness)
21. [Wind](#21-wind)
22. [Light Manager](#22-light-manager)
23. [Audio, Sound Zones & Ambient Sounds](#23-audio-sound-zones--ambient-sounds)
24. [Performance Profiles](#24-performance-profiles)
25. [Virtual Reality](#25-virtual-reality)
26. [Floating Point Origin Offset](#26-floating-point-origin-offset)
27. [Slimming the Build (Resource Container)](#27-slimming-the-build-resource-container)
28. [Weather API / Open Weather Map](#28-weather-api--open-weather-map)
29. [Location Services](#29-location-services)
30. [Networking with Mirror](#30-networking-with-mirror)
31. [Shader Integration](#31-shader-integration)
32. [Third‑Party Integrations](#32-third-party-integrations)
33. [Demo Scenes Reference](#33-demo-scenes-reference)
34. [Tutorial Videos](#34-tutorial-videos)
35. [Scripting & API Cheat Sheet](#35-scripting--api-cheat-sheet)
36. [Performance Troubleshooting](#36-performance-troubleshooting)
37. [Troubleshooting / FAQ](#37-troubleshooting--faq)
38. [Known Issues](#38-known-issues)
39. [Credits](#39-credits)
40. [Support](#40-support)

---

## 1. Requirements and Compatibility

- **Unity** — Weather Maker runs on modern versions of the standard (built‑in) render pipeline. URP support requires **Unity 6000 or newer** and **URP 17.3 or newer**.
- **Graphics API** — Metal on macOS or **DirectX 12** on Windows is recommended when using URP. Weather Maker **requires texture array support**.
- **Not supported** — **WebGL**, **OpenGL**, **OpenGL ES**, **DirectX 9.0**, and **HDRP** (not currently supported).
- **.NET** — .NET Standard 2.1 or .NET 4.x (or newer) scripting runtime.
- **Color space** — Linear color space is strongly recommended. Gamma works but looks much worse.
- **HDR** — Enable HDR on your main camera. Mist, ripples, clouds and atmosphere all look significantly better with HDR.
- **Input systems** — Weather Maker uses Unity's `ENABLE_INPUT_SYSTEM` and `ENABLE_LEGACY_INPUT_MANAGER` scripting defines to choose input backends. Unity defines `ENABLE_INPUT_SYSTEM` when the Input System Package backend is enabled, and `ENABLE_LEGACY_INPUT_MANAGER` when the old Input Manager backend is enabled. Both defines can be active if **Project Settings → Player → Other Settings → Active Input Handling** is set to **Both**. Weather Maker uses the new input system for keyboard/mouse when `ENABLE_INPUT_SYSTEM` is present; otherwise it uses legacy `Input`. Location services still require the legacy `Input.location` API, so projects that use automatic location weather should keep legacy input enabled.
- **Important upgrade note** — If upgrading from a version earlier than 5.0.0, **delete your existing `WeatherMaker` folder** and re‑import from scratch.

> Before anything else, if you also have **Rain Maker** in the project, delete it. Rain Maker and Weather Maker cannot coexist.

---

## 2. Installation and First‑Time Setup

1. Open your Unity project (URP template for URP, 3D template for standard pipeline).
2. Import **Weather Maker** from the Asset Store.
3. *(URP only)* Click **Window → Weather Maker → Enable URP**.
4. Open the [demo scene](Demo/Scenes/DemoScene.unity) (`Demo/Scenes/DemoScene.unity`) to verify the install — press Play and confirm you see sky, clouds, sun, moon, and default ambient audio.
5. In your own scene, drag [WeatherMakerPrefab.prefab](Prefab/WeatherMakerPrefab.prefab) (or [WeatherMakerPrefab2D.prefab](Prefab/WeatherMakerPrefab2D.prefab) for 2D) into the root of the hierarchy.
6. Follow the [Project & Scene Setup Checklist](#3-project--scene-setup-checklist) to adjust camera, lighting, and environment settings.

> **Tip** — To get a ready‑made test UI, drag [WeatherMakerConfigurationCanvasPrefab.prefab](Prefab/WeatherMakerConfigurationCanvasPrefab.prefab) into the root of your scene. It lets you flip weather, fog density, time of day and more at runtime.

---

## 3. Project & Scene Setup Checklist

Use this list for every new scene:

**Project Settings**
- [ ] Color space → **Linear** (Edit → Project Settings → Player → Other Settings → Rendering).
- [ ] Scripting runtime → **.NET 4.x** or **.NET Standard 2.1+**.
- [ ] HDR enabled on main camera.
- [ ] **MainCamera** tag assigned to your main camera.
- [ ] Screen‑space shadows enabled in graphics settings (needed for cloud shadows and snow overlay shadows).
- [ ] Post‑processing package installed (recommended — SMAA/Temporal AA, bloom, color grading, depth of field, ambient occlusion, vignette).
- [ ] **Turn OFF deferred fog** if you are using Weather Maker fog.

**Camera**
- [ ] Clear flags → **Solid Color**, color = **(0, 0, 0, 0)** (so the sky sphere / cloud pass renders).
  *Alternative:* if you keep Skybox clear flags, set `WeatherMakerPrefab → FullScreenEffects → Clouds → Render Queue` to **After Skybox** and disable the sky sphere renderer.
- [ ] Far plane ≥ **10,000** (≥ **100,000** for flight sim / high altitude).
- [ ] If using storm lightning in 3D, far plane must contain the lightning distances or set them smaller on `WeatherMakerPrefab → Precipitation → ThunderAndLightning → Positioning`.

**Lighting**
- [ ] Window → Rendering → Lighting → Environment:
  - Skybox material → **WeatherMakerSkyboxMaterial**.
  - Sun source → **Sun** (from Weather Maker prefab).
  - Environment lighting source → **Color** or **Gradient**.
  - Environment reflections source → **Custom**.

**VR/AR**
- [ ] Prefer **forward rendering** on VR/AR cameras to avoid Unity deferred + XR bugs.

**Zones & Extras**
- [ ] Place [WeatherMakerNullZone](Prefab/WeatherMakerNullZone.prefab) prefabs in caves, buildings, boats, etc. where precipitation/fog should be suppressed.
- [ ] Place Weather Zones for regional weather variation (rain forest, desert, snowy peak, etc.).
- [ ] Add `WeatherMakerCelestialObject` to any directional light you want Weather Maker to drive as a sun or moon (set `IsSun` accordingly).

**Build Step**
- [ ] Before building, check the `ResourceContainer` profile on `WeatherMakerScript` (root of prefab). **Only assets inside this container ship with your build.** Duplicate the default or mobile container and trim unused profiles, textures, and sounds.

---

## 4. The Weather Maker Prefab Tour

When you drop [WeatherMakerPrefab.prefab](Prefab/WeatherMakerPrefab.prefab) into a scene you get a full hierarchy of managers. **Do not delete these** — disable them if you do not need them.

```
WeatherMakerPrefab                   (WeatherMakerScript, WeatherMakerCommandBufferManagerScript)
├── AudioManager                     (WeatherMakerAudioManagerScript)
├── DayNightCycle                    (WeatherMakerDayNightCycleManagerScript)
│   ├── Sun                          (Directional Light + WeatherMakerCelestialObjectScript, IsSun = true)
│   └── Moon                         (Directional Light + WeatherMakerCelestialObjectScript, IsSun = false)
├── LightManager                     (WeatherMakerLightManagerScript)
├── SkySphere                        (WeatherMakerSkySphereScript + MeshRenderer)
├── Wind                             (WeatherMakerWindScript)
├── FullScreenEffects
│   ├── Clouds                       (WeatherMakerFullScreenCloudScript)
│   ├── Fog                          (WeatherMakerFullScreenFogScript)
│   ├── Snow (Overlay)               (WeatherMakerFullScreenOverlayScript)
│   └── Wetness (Overlay)            (WeatherMakerFullScreenOverlayScript)
├── Precipitation
│   ├── Rain, Snow, Hail, Sleet      (WeatherMakerFallingParticleScript subclasses)
│   ├── RainRipples, SleetRipples
│   └── ThunderAndLightningPrefab    (WeatherMakerThunderAndLightningScript)
│       └── LightningBoltPrefab      (WeatherMakerLightningBoltPrefabScript)
├── GlobalWeatherZone                (WeatherMakerWeatherZoneScript)
└── Extensions                       (slots for Wet Stuff, MegaSplat, RTP, WAPI, etc.)
```

**Scripts that MUST remain enabled:**

- `WeatherMakerPrefab` root — `WeatherMakerScript` and `WeatherMakerCommandBufferManagerScript`.
- `AudioManager`.
- `DayNightCycle` — set day/night **Speed** to `0` on the profile to freeze time, do not disable the script.
- `LightManager` — add any point/spot lights you want affecting clouds, fog, and water here.
- `SkySphere` — `WeatherMakerSkySphereScript` stays on; the `MeshRenderer` can be disabled if you prefer your own skybox.
- `FullScreenEffects/Clouds` — this one pass renders sun, moon, sky, atmosphere and clouds.

Other objects (fog, precipitation, overlays, lightning, etc.) can be **deactivated** — but **not deleted** — when unused.

**Useful `WeatherMakerScript` properties:**

- **AllowSceneCamera** — render in the scene view. Disable if you see UI corruption (Unity command‑buffer bug).
- **IsPermanent** — defaults to true. The prefab persists across scene loads. Set `false` if you want one prefab per scene.
- **AllowCameras / AllowCameraNames** — extra cameras you want Weather Maker to render into. Default is main camera only.
- **AutoCloneProfiles / CloneProfiles** — clones scriptable‑object profiles at play to avoid accidental asset writes. Disable to tweak the source asset live.
- **ResourceContainer** — profile defining what gets baked into the build. See [Slimming the Build](#27-slimming-the-build-resource-container).
- **PerformanceProfile** — see [Performance Profiles](#24-performance-profiles).
- **NetworkConnection** — interface for Mirror / custom network sync.
- Manager interfaces — plug in your own precipitation, wind, cloud, fog manager implementation if desired.

---

## 5. Player Setup

Weather Maker identifies **“the local player”** using an enabled `AudioListener`. Sound zones, weather zones, dampening zones, and null zones all rely on trigger events fired by the camera/player. Follow this recipe on your player (same GameObject as the AudioListener):

1. An enabled **AudioListener**.
2. A **kinematic Rigidbody** (required for `OnTriggerEnter/Exit` to fire reliably in Unity).
3. A tiny **Sphere Collider** with radius `0.001` and **Is Trigger** = true.
4. (Recommended) `WeatherMakerSoundZoneScript` — attach here to keep ambient sounds centered on the player.
5. (Networking) `WeatherMakerIsPlayerScript` — auto‑sets `IsLocalPlayer` if a `NetworkIdentity` is present.

A ready‑made example exists at [WeatherMakerPlayer.prefab](Prefab/WeatherMakerPlayer.prefab). Use it as a reference if you roll your own.

**Networked, non‑local players** — **disable** (do not delete) the player’s camera, audio listener and sound zone scripts. Only the local player should have them active. See [Networking with Mirror](#30-networking-with-mirror).

---

## 6. URP Setup

1. Create a new project using the **URP 3D** template (Unity 6000+ with URP 17.3+).
2. Import Weather Maker.
3. **Window → Weather Maker → Enable URP**.

That enables the URP shader variants, renderer features, and compile defines. After enabling, open [DemoScene.unity](Demo/Scenes/DemoScene.unity) to verify.

> HDRP is **not** currently supported. Raise the question with support@digitalruby.com if it matters to you.

---

## 7. Launcher / Multi‑Scene Setup

Most games use a launcher scene that owns permanent objects and transitions into main menu / gameplay scenes. Weather Maker ships with a ready‑made launcher.

1. Open or create your launcher scene.
2. Drag [WeatherMakerLaunchManager.prefab](Prefab/WeatherMakerLaunchManager.prefab) in. It survives across scene loads.
3. In **Build Settings**, ensure scene order is: **Launcher (index 0) → Main Menu → Game**.
4. On `WeatherMakerLauncherScript` set the main‑menu button name, gameplay button name, main‑menu scene name, and gameplay scene name so the launcher can wire itself to UI clicks.
5. Add `WeatherMakerPrefab` to the **launcher scene** (not to the per‑scene scenes). Because `IsPermanent = true` it persists.

If you need per‑scene weather, set `IsPermanent = false` on the root and drop a prefab into each scene individually.

---

## 8. Workflow & Profiles

Weather Maker is built on **scriptable object profiles**. Every feature reads its settings from a profile asset. This makes swapping looks a drag‑and‑drop operation.

**Profile folders** live under [Prefab/Profiles](Prefab/Profiles):

| Profile Type | Location | Purpose |
|--------------|----------|---------|
| Weather | [Prefab/Profiles/Weather](Prefab/Profiles/Weather) | Top‑level: glues precipitation + cloud + fog + wind + audio together. Assign to weather zones. |
| Precipitation | [Prefab/Profiles/Precipitation](Prefab/Profiles/Precipitation) | Rain/snow/sleet/hail intensity curves. |
| Clouds | [Prefab/Profiles/Clouds](Prefab/Profiles/Clouds) | Volumetric + flat layer settings. |
| Sky | [Prefab/Profiles/Sky](Prefab/Profiles/Sky) | Sky sphere / plane settings. |
| Atmosphere | [Prefab/Profiles/Atmosphere](Prefab/Profiles/Atmosphere) | Atmospheric scattering, density, light shafts. |
| DayNightCycle | [Prefab/Profiles/DayNightCycle](Prefab/Profiles/DayNightCycle) | Speed, ambient, lat/lon/date/time. |
| Fog | [Prefab/Profiles/Fog](Prefab/Profiles/Fog) | Fog density/height/shape/noise. |
| Wind | [Prefab/Profiles/Wind](Prefab/Profiles/Wind) | Gust intensity, direction, rotation. |
| Aurora | [Prefab/Profiles/Aurora](Prefab/Profiles/Aurora) | Aurora borealis visual settings. |
| SkyEffects | [Prefab/Profiles/SkyEffects](Prefab/Profiles/SkyEffects) | Meteor shower, etc. |
| Sound | [Prefab/Profiles/Sound](Prefab/Profiles/Sound) | Sound groups and individual sounds. |
| Water | [Prefab/Profiles/Water](Prefab/Profiles/Water) | Water look/waves/caustics. |
| NullZone | [Prefab/Profiles/NullZone](Prefab/Profiles/NullZone) | Null zone mask and fade. |
| Overlays | [Prefab/Profiles/Overlays](Prefab/Profiles/Overlays) | Snow / wetness overlay profiles. |
| Lightning | [Prefab/Profiles/Lightning](Prefab/Profiles/Lightning) | Lightning style overrides. |
| Performance | [Prefab/Profiles/Performance](Prefab/Profiles/Performance) | One per quality level. |
| ResourceContainer | [Prefab/Profiles/ResourceContainer](Prefab/Profiles/ResourceContainer) | What ships with your build. |
| PostProcessing | [Prefab/Profiles/PostProcessing](Prefab/Profiles/PostProcessing) | Post‑processing helpers. |

**Creating a profile** — right‑click in the Project view → **Create → Weather Maker → *(profile type)***.

**Editing clouds in edit mode** — as of 5.4.0 you can drag a cloud profile onto the cloud script in edit mode and tweak it live. Set the slot back to the base profile when done. (If clouds render oddly in edit mode, jostle the Scene / Game view — Unity is occasionally sluggish to send update events.)

**Changing a profile at runtime** — press Play, drag a profile into the inspector, then change properties. `AutoCloneProfiles = true` prevents your edits from persisting to the source asset.

**Duplicating a profile** — **always duplicate** before making major changes. It is the single best habit to avoid losing work.

---

## 9. Precipitation (Rain, Snow, Sleet, Hail)

Weather Maker ships four precipitation types. All derive from `WeatherMakerFallingParticleScript` and share a **four‑stage model**: `None → Light → Medium → Heavy`. Each stage cross‑fades audio and tuned particle emission.

### 9.1 Common Properties

- **Loop Source Light / Medium / Heavy** — 3D audio sources for each stage, cross‑fade automatically.
- **Sound Intensity Threshold** — thresholds at which the stages switch.
- **Intensity** — overall driver. `0` = no precipitation.
- **Intensity Multiplier**, **Secondary Intensity Multiplier**, **Mist Intensity Multiplier** — scale emission. Values above `1.0` can tank performance.
- **Base Emission Rate (+ Secondary, + Mist)** — raw particle rates per second.
- **Particle System / Particle System Secondary / Mist Particle System** — hooks to the underlying particle systems.
- **Secondary Threshold / Mist Threshold** — intensity levels at which secondary / mist particles kick in.

### 9.2 3D‑Only Properties

- **IsFirstPerson** — if true, the particle volume follows each rendered camera. If false, the precipitation is fixed in world space (used by precipitation zones).
- **Height / Forward Offset** — where the main particles spawn relative to the camera.
- **SecondaryHeight / SecondaryForwardOffset** — same for the secondary layer.
- **Mist Height** — spawn altitude for mist.
- **Collisions** — configured globally on `WeatherMakerPrefab`.
- **Ripples** (rain, sleet):
  - **AnimatedTextureRendererIntensityThreshold** — minimum intensity before ripples appear.
  - **AnimatedTextureRendererPositionOffset** — vertical offset of the ripple plane from the nearest ray‑traced hit below the camera.
  - Ripples automatically disable when there is geometry directly above the camera (player is indoors).

### 9.3 2D‑Only Properties

- **Height Multiplier / Width Multiplier** — extend the particle field beyond the camera rect.
- **Collision Mask** — globally driven from the prefab.
- **Collision Life Time / Explosion Emission Life Time Maximum** — when particles die on collision and optionally spawn an explosion (splash) particle.
- **Mist Collision Velocity Multiplier / Mist Collision Life Time Multiplier** — mist reaction on collision.

### 9.4 Snow Gotchas

Some snow particle systems use **local simulation space** so a fast‑moving player cannot outrun slow‑falling snow. If your snow looks wrong, toggle between world and local simulation space on the particle systems to find what fits your scale and movement speed.

### 9.5 Custom Precipitation

Create a profile and set it to use custom precipitation, then assign your own script:

```csharp
WeatherMakerScript.Instance.PrecipitationManager.CustomPrecipitationScript = yourCustomPrecipitationScript;
```

---

## 10. Weather Zones, Null Zones, Dampening & Precipitation Zones

### 10.1 Weather Zones

Weather zones bind a **weather profile (or weather group)** to a **collider**. Enter the collider and the associated weather kicks in. As of Weather Maker 4.0.0 weather zones replaced the legacy “weather manager.”

**Creating a weather zone:**

1. Create an empty GameObject child of whatever should own the zone (a landmass, a region).
2. Add a collider (sphere/box/mesh) and set **Is Trigger** = true.
3. Menu → **Component → Weather Maker → Weather Zone**.
4. Assign either a single **Weather Profile** or a **Weather Profile Group** (for random picks like “temperate” or “snow”).

**Post processing per zone** — create a layer (e.g. `Weather`), assign the zone to that layer, and include it in your post‑processing layer mask. This lets you blend post‑processing overrides with weather transitions.

**Global weather zone** — under the prefab, the `GlobalWeatherZone` object is a whole‑scene weather zone. Activate it and assign a weather group for dynamic world‑wide weather.

**Trigger requirements:**
- Audio listener on the main camera.
- Kinematic rigidbody on the same GameObject as the audio listener.
- Small trigger collider (sphere, radius `0.001`) on the same GameObject.

**First transition** — `WeatherMakerScript.HasHadWeatherTransition` starts `false`. While false, the first weather switch is instant — thereafter switches cross‑fade. Reset to `false` to force the next change to be instant.

**Demo:** [DemoSceneWeatherZones.unity](Demo/Scenes/DemoSceneWeatherZones.unity).

### 10.2 Null Zones

Null zones **suppress** rendering of precipitation, ripples, fog, overlays, and water in their volume — indoors, in caves, inside boats, around the player’s hands/armor, etc.

- Supported colliders: **sphere** and **box**. Zones can be scaled and rotated.
- Prefabs:
  - [WeatherMakerNullZone.prefab](Prefab/WeatherMakerNullZone.prefab)
  - [WeatherMakerNullZoneOpen.prefab](Prefab/WeatherMakerNullZoneOpen.prefab) — for open buildings
  - [WeatherMakerNullZoneClosed.prefab](Prefab/WeatherMakerNullZoneClosed.prefab) — for sealed interiors
  - [WeatherMakerNullZoneSphere.prefab](Prefab/WeatherMakerNullZoneSphere.prefab)
- **NullZoneFade** — how fast fading to zero occurs near the zone’s edge. `100` disables the fade.
- **NullZoneFade ≤ 0** — special overlay handling. As the camera approaches the zone, overlays inside fade out. Useful for open structures. `abs(fade)` controls speed.
- **Mask** — per‑profile mask selects which rendering to block. Example: block overlay only on the player’s weapon, leaving rain/fog visible.
- Create a **Null Zone Profile** with right‑click → Weather Maker → Null Zone Profile, then assign it to the script.

**Demo:** [DemoSceneNullZones.unity](Demo/Scenes/DemoSceneNullZones.unity).

### 10.3 Dampening Zones

Dampening zones **reduce** (rather than fully suppress) precipitation intensity, sound volume, and light intensity when the player is inside. Smooth transitions in and out.

- Prefab: [WeatherMakerDampeningZone.prefab](Prefab/WeatherMakerDampeningZone.prefab).
- Uses the same trigger requirements as null zones (audio listener + trigger).

### 10.4 Precipitation Zones

Precipitation zones host **static, non‑following** particle emitters — a waterfall of rain over a specific building, a perpetual snow patch on a mountain peak, etc. Their properties are the same as the first‑person precipitation scripts, except `IsFirstPerson = false`.

Prefabs (each uses the correct profile):
- [WeatherMakerRainZone.prefab](Prefab/WeatherMakerRainZone.prefab)
- [WeatherMakerSnowZone.prefab](Prefab/WeatherMakerSnowZone.prefab)
- [WeatherMakerHailZone.prefab](Prefab/WeatherMakerHailZone.prefab)
- [WeatherMakerSleetZone.prefab](Prefab/WeatherMakerSleetZone.prefab)

Tune the particle emission shape, size, and sound falloff to match your scene’s scale.

**Demo:** [DemoScenePrecipitationZones.unity](Demo/Scenes/DemoScenePrecipitationZones.unity).

### 10.5 Wind Zones

[WeatherMakerWindZonePrefab.prefab](Prefab/WeatherMakerWindZonePrefab.prefab) adds localized wind that influences rain direction, snow drift, fog velocity and water waves within its volume.

---

## 11. Sky Sphere, Sun, Moon & Atmospheric Scattering

### 11.1 Sky Sphere

- Located at `WeatherMakerPrefab/SkySphere`, driven by `WeatherMakerSkySphereScript` + a **Sky Profile**.
- Not used in pure 2D mode (see [Sky Plane](#114-sky-plane-2d)).
- If you see sky edges or banding, raise the sky sphere **resolution** property.

**Sky modes:**

| Mode | Behavior |
|------|----------|
| **Procedural** | Fully procedural (Rayleigh + Mie scattering). Day/dusk textures ignored; night texture used as sun dips below horizon. |
| **Procedural Textured** (default) | Blends procedural scattering with day/dawn/dusk textures. Textures should have translucent cloud areas. Night texture should be opaque. |
| **Textured** | Fully textured, no procedural scattering. |

**Texture modes** — set texture type to Advanced, wrap mode Clamp, filter Bilinear, aniso 1, format Automatic Truecolor:

- **Sphere** — 2:1 aspect ratio; top = sky, bottom = ground.
- **Panorama** — the entire texture maps to the upper hemisphere only (higher resolution if the player never looks below the horizon).
- **Panorama Mirror Down** — upper‑hemisphere panorama mirrored onto the lower hemisphere.
- **Dome** — center of texture = zenith, circle edge = horizon. Best quality, requires dome pre‑processing.
- **Dome Mirror Down** — dome mirrored onto the lower hemisphere.
- **Dome Double** — left half = bottom dome, right half = top dome.
- **Fish Eye Mirrored** — fish‑eye front + mirror back, not suitable for 360°.
- **Fish Eye 360** — fish‑eye mapped onto the whole sphere (slight polar distortion).

Examples live at [Prefab/Textures/SkySphere](Prefab/Textures/SkySphere).

**Night sky:**
- **NightIntensity** — overall brightness.
- **NightVisibilityThreshold** — which pixels appear. Raise for city / light‑polluted scenes.
- **Twinkle** — twinkle randomness + variance. Set both to `0` to disable twinkle.

**Rotation** — set the rotation axis to non‑zero to animate the sky with the day/night cycle. Requires a procedural sky mode. No perf overhead — night sky textures are *not* cubemaps.

### 11.2 Sun

- A directional light carrying `WeatherMakerCelestialObjectScript` with `IsSun = true`.
- **Keep enabled at all times** — other Weather Maker scripts depend on it.
- **Do not change sun intensity externally.** Instead modify `DirectionalLightIntensityMultipliers` on `WeatherMakerLightManagerScript.Instance`.
- Only one sun is currently supported (multi‑sun exotic worlds may arrive later).
- **Base Shadow Strength** = `1.0` for deepest shadows.

### 11.3 Moon

- Default setup has Earth’s moon in 3D scenes.
- Rendered as a mesh + directional light.
- **Moon phase** is computed from the day/night cycle’s lat/lon/date/time and influences moon brightness.
- **Moon fade** material property — `1` fades black crescent, `0` shows it fully.
- You can deactivate the moon if your world has none. For extra moons, add directional lights with `WeatherMakerCelestialObjectScript` (set `IsSun = false`).

### 11.4 Sky Plane (2D)

- Used only in 2D scenes instead of the sphere.
- Renders at the default sorting queue with `order = int.MinValue` so everything else draws over it.
- Settings mirror the Sky Sphere.
- See [DemoScene2D.unity](Demo/Scenes/DemoScene2D.unity).

### 11.5 Atmospheric Scattering

- Defined by an **Atmosphere Profile** referenced by the active **Sky Profile**.
- The **performance profile** controls whether scattering and light shafts are enabled, plus sample count and max ray length.
- Sky blends into the scene (as of 5.8.0) using **sky fade** and **intensity** on the sky profile.
- Ensure your camera far plane is set **before** tuning scattering; the horizon sampling depends on it.

### 11.6 Lens Flare Blocker

The sun has a lens flare blocker. With compute shaders available, it probes the volumetric clouds for occlusion, falling back to global cloud coverage if compute shaders are not supported. SRP lens flares are supported (v8.0.5+).

---

## 12. Day / Night Cycle

`WeatherMakerDayNightCycleManagerScript` + `WeatherMakerDayNightCycleProfileScript`.

**Core properties:**

- **Speed** — in‑game seconds per real second during the day. `1` = real‑time. Negative values reverse time.
- **NightSpeed** — same but for night. Sign should match **Speed**.
- **UpdateInterval** — how often (in seconds) the cycle runs. `0` = every frame. Increase if shadows flicker.
- **TimeOfDay** — current local time in seconds.
- **Year / Month / Day** — used for accurate sun/moon positioning.
- **TimeZoneOffsetSeconds** — local→UTC offset. Set to `-1111` for auto‑detect (editor: web lookup).
- **Latitude / Longitude** — decimal degrees on the planet.
- **AxisTilt** — planet axis tilt. Earth ≈ `23.439°`.
- **RotateYDegrees** — extra yaw rotation, useful for non‑east/west cycles.
- **DayDawnDuskNightGradient** — green = day, red = dawn/dusk, blue = night. Center = sun at horizon.
- **SunIntensityGradient** — sun intensity multiplier over the cycle.
- **SunShadowStrengthGradient** — shadow strength multiplier.

**Ambient modes:**
- **UnityAmbientSettings** — ignore day/night colors, use Unity’s.
- **Sky / Ground / Equator** — drive individual ambient channels.
- **All** / **AllWithUnityMode** — add computed ambient to all channels.

Freeze time by setting **Speed** and **NightSpeed** to `0`.

---

## 13. Volumetric Clouds

3D clouds are a full‑screen command‑buffer pass driven by a **Cloud Profile** ([Prefab/Profiles/Clouds](Prefab/Profiles/Clouds)). They ray‑march a 3D noise texture plus a weather map, apply multi‑light lighting and atmospheric blending, and integrate with fog.

### 13.1 Profiles

- **Cloud Profile** — volumetric + flat layer settings.
- **Cloud Layer Profile** — flat layer (cirrus, altostratus, etc.).
- **Weather Map** — a dynamically generated or custom texture:
  - **R** = cloud coverage
  - **G** = cloud density
  - **B** = cloud type (`0` = stratus, `1` = cumulus)
  - **A** = reserved.
- Artists can author custom weather maps in Photoshop/PS/GIMP and import as JPG/PNG.

### 13.2 Properties of Interest

- **CloudSeed** — deterministic cloud shapes between runs.
- **AutoCloneProfiles** — off if you want to tweak an asset directly at runtime (make a duplicate first).
- **AutoSetTemporalReprojectionBlendMode** — defaults true. Sharp reprojection by default, switches to blue‑noise when lightning is firing. Turn off if you need manual control.
- **FlatLayerMask** — LayerMask for flat cloud layers that coexist with the volumetric pass. Default includes layer 4 (cirrus).
- **Ray offset / Horizon fade** — reduce / zero for fly‑throughs.
- **Planet radius / cloud min height / cloud max height** — shape the spherical volume for fly‑through realism.
- **Downsample scale**, **LOD settings**, **temporal reprojection** — top three performance levers.

### 13.3 Fly‑Through

- Only the **fly‑through cloud profile** supports proper cloud fly‑through.
- Camera far plane must be large.
- Your world must have a surface covering beyond the far plane (terrain / ocean plane) to avoid horizon artifacts.
- Zero the ray offset and reduce horizon fade.

See [DemoSceneFlythrough.unity](Demo/Scenes/DemoSceneFlythrough.unity).

### 13.4 Lighting

- Directional, point, and spot lights registered in the Light Manager all influence cloud lighting (including lightning).
- Your day/night profile’s **Ambient**, **Ambient Ground**, and **Ambient Sky** colors + intensities heavily shape cloud coloration. If clouds are too bright, set ground ambient to `1` and sky ambient to `2` on the cloud profile (per 8.0.2 release notes).

### 13.5 Shadows

- Cloud shadows work with **screen‑space shadows** (3D perspective only — not orthographic).
- Unity fades shadows at distance by default. Long‑distance cloud shadows require a larger **shadow distance** (Quality Settings) **and** optionally patching `UnityShadowLibrary.cginc`:
  ```c
  half UnityComputeShadowFade(float fadeDist) { return 0.0; }
  ```
  (Must be reapplied after every Unity install/update.)

### 13.6 Noise Textures

Weather Maker ships several 3D noise textures. Author your own with **Window → Weather Maker → Cloud Noise Editor**. Background reading: [bitsquid on volumetric clouds](http://bitsquid.blogspot.com/2016/07/volumetric-clouds.html).

### 13.7 Temporal Reprojection

- On by default. Blend modes: sharp (low ghost) vs. blue noise (artifact cleanup).
- Blur mode of the temporal reprojection material → set to `1` for very crisp clouds, but watch for ghosting at night with lightning.

### 13.8 Post Processing on Clouds

The full screen cloud script has options to post‑process the weather map or the cloud pass. Apply custom materials to either. See [DemoSceneCloudPostProcessing.unity](Demo/Scenes/DemoSceneCloudPostProcessing.unity) and [DemoSceneCloudPostProcessingCartoon.unity](Demo/Scenes/DemoSceneCloudPostProcessingCartoon.unity).

### 13.9 Compile Out

If you don’t use clouds, comment line 26 of `WeatherMakerCloudVolumetricShaderInclude.cginc` to disable the volumetric code path and shrink shader size / compile times.

### 13.10 2D Clouds

2D flat and volumetric cloud modes are supported. See [DemoScene2D.unity](Demo/Scenes/DemoScene2D.unity) and `LegacyCloudScript2D`.

### 13.11 Reflection Probes

- Do **not** use box projection — unsupported.
- Set realtime mode for moving clouds/lightning.
- Disable reflection probes in performance profiles if you see flickering.

---

## 14. Cloud Probes

Add a `WeatherMakerCloudProbeScript` to any transform to sample volumetric cloud density.

- Probe is a **point** if target transform is null (or equals source).
- Probe is a **ray cast** between the source and target transforms otherwise.

```csharp
var result = WeatherMakerFullScreenCloudScript.Instance.GetCloudProbe(camera, sourceTransform, targetTransform);
```

Requires compute shaders. Without them, the probe returns `0`. Only game cameras can probe — reflection and cubemap cameras cannot.

---

## 15. Aurora Borealis / Northern Lights

- Assign an **Aurora Profile** ([Prefab/Profiles/Aurora](Prefab/Profiles/Aurora)) to the full screen cloud script.
- Performance profile controls aurora sample/sub‑sample counts; the aurora profile can request lower counts to override those values.

---

## 16. Meteor Shower

- Prefab: [WeatherMakerMeteorShower.prefab](Prefab/WeatherMakerMeteorShower.prefab).
- Drop it into the scene root.
- A **Meteor Shower Profile** exposes common parameters; the underlying particle system is fully editable.

---

## 17. Lightning & Thunder

Weather Maker embeds the core of **Procedural Lightning** (`WeatherMakerThunderAndLightningScript` + `WeatherMakerLightningBoltPrefabScript`). Randomized bolts spawn in a hemisphere around the main camera, with day/night‑aware brightness.

### 17.1 Normal vs Intense

- **Intense** — close, loud, short sound delay.
- **Normal** — distant, quieter, longer delay.
- `LightningIntenseProbability` sets the random mix.

### 17.2 WeatherMakerThunderAndLightning Properties

- **Lightning Bolt Script** — the bolt script used. Leave as default.
- **Camera** — which camera to strike around. Default is main camera.
- **Lightning Interval Time Range** — seconds between strikes.
- **Thunder Sounds Normal / Intense** — audio pools.
- **Start/End X/Y/Z Variance** and **Start Y Base** — randomize bolt positions. End Y rays to the ground, so true end Y can change.
- **Lightning Forced Visibility Probability** — chance the bolt is forced to be visible in the camera view.
- **Ground / Cloud Lightning Chance** — split between ground strikes and cloud‑only flashes.
- **Sun** — used to dim brightness during the day.
- **Normal Distance / Intense Distance** — min/max distance windows for each mode. Bolts are always at least the minimum of the active mode’s distance.

### 17.3 LightningBoltPrefab Properties

- **Duration Range** — bolt visible time in seconds.
- **Trunk Width Range** — main bolt width range.
- **Glow Tint Color** — outer glow color.
- **Generations** — subdivisions (more = more detail but more cost).
- **Glow Intensity / Width Multiplier** — how bright / wide the outer glow is.
- **Lights** — `LightPercent = 0` disables point lights from the bolt.

### 17.4 Scripting Callbacks

```csharp
WeatherMakerThunderAndLightningScript.Instance.LightningStartedCallback += (bolt) => { ... };
WeatherMakerThunderAndLightningScript.Instance.LightningEndedCallback   += (bolt) => { ... };
WeatherMakerThunderAndLightningScript.Instance.ThunderSoundPlayed       += (clip) => { ... };

WeatherMakerThunderAndLightningScript.Instance.CallNormalLightning(start, end);
WeatherMakerThunderAndLightningScript.Instance.CallIntenseLightning(start, end);
```

### 17.5 Performance Tips

- Reduce **Generations**.
- Set **LightPercent = 0** to disable per‑bolt lights.
- Zero the **Glow Intensity** for cheaper bolts.

---

## 18. Fog (Full Screen, Volumetric Sphere, Volumetric Cube)

Fog is **3D perspective only** (no 2D/orthographic). Three fog flavors:

- **Full‑screen fog** — `WeatherMakerFullScreenFogScript`. Covers the camera view.
- **Volumetric fog sphere** — [WeatherMakerVolumetricFogSphere.prefab](Prefab/WeatherMakerVolumetricFogSphere.prefab).
- **Volumetric fog cube** — [WeatherMakerVolumetricFogCube.prefab](Prefab/WeatherMakerVolumetricFogCube.prefab).

The fog sphere/cube require a prefab in the scene configured properly.

### 18.1 Full‑Screen Fog

```csharp
WeatherMakerFullScreenFogScript.Instance.TransitionFogDensity(from, to, seconds);
```

Ensure a fog profile is assigned.

**Key properties:**
- Fog height (ground fog).
- Fog density / noise scale / noise adder / noise multiplier. Set **noise adder** negative for holes in the fog.
- **FogNoiseSampleCount** — tune for platform. Fewer samples on mobile.
- **Fog shadow sample count** — `> 0` enables sun shadow maps in fog (expensive — tune carefully).
- **Sun shaft sample count** — `> 0` enables sun shafts. `32` is a sweet spot.
- **Dithering level** — raise if you see banding at low density.
- **FogRenderQueue** — change if you see order‑of‑operations artifacts.

### 18.2 Volumetric Point / Spot / Area Lights

- Enable `EnableFogLights = true` on `WeatherMakerScript`.
- Lights must be registered with the Light Manager (simplest: drop them in **AutoAddLights**).
- Works best in **linear** color space.
- Fog supports temporal reprojection (5.0.0+).

### 18.3 Fog Noise Requirement

`WeatherMakerLightManagerScript.NoiseTexture3D` must be set for noise fog to work. Null it out to save a few MB if you do not use noise fog.

### 18.4 Tree Billboards

If you must use billboard trees, enable `WeatherMakerTreeBillboardShader.shader` (read the top of that file for the steps) so billboards receive full‑screen fog.

---

## 19. Water

The [WeatherMakerWaterPrefab.prefab](Prefab/WeatherMakerWaterPrefab.prefab) includes tessellated + non‑tessellated modes, underwater rendering, caustics, sparkle, foam, and up to **8 wave directions**.

### 19.1 Rendering Modes

- **One Pass** — all lights in a single draw call. Big win on mobile / VR.
- **Forward Base + Add** — one draw call per light. Higher fidelity, higher cost.

### 19.2 Material Parameters

- **inv fade.x** — shore fade. `< 100` fades more softly but introduces refraction artifacts. `> 100` keeps refraction correct with a harder shoreline.
- **inv fade.y** — normal fade as camera rises above water.
- **inv fade.z** — reflection strength.
- **inv fade.w** — normal fade as vertex distance from camera increases.
- **_SparkleScale.w** = 0 disables sparkle.
- **Caustics** — configurable on the water material. Requires **screen‑space shadows** to render shadows over caustics.

### 19.3 Waves

- Up to 8 waves via `WaterWave1..WaterWave8`: `(x, y)` = direction, `z` = amplitude, `w` = wavelength. All‑zero disables a wave slot.
- Each wave has extras: `x` = speed, `y` = height reducer (smaller height → smaller wave).
- A **depth camera** snapshots a water height/depth map (driven by `WeatherMakerDepthCameraScript`). Configure which static layers are included (terrain, mesh floors). Omit boats/swimmers. Set `Dirty = true` if the static volume changes to recompute the depth map (expensive).
- Wave heights and water fade track the depth map — shallow water = smaller waves and more fade.
- **Wind‑driven waves** — set the wind option in the water profile. Zero wind = calm.

### 19.4 Tessellation

- **TesselationParams.x** — vertex count driver.
- **TesselationParams.y** — max displacement (set to your max wave height for correct culling).
- **Displacement** — extra detail maps for tessellated water.

### 19.5 Underwater

- Supported out of the box.
- Add a post‑processing layer to the camera and a post‑processing volume to the water object for underwater grading (color grading + depth of field are highly recommended).

### 19.6 Gotchas

- Keep the water plane at least **2 world units** below the surrounding ground to prevent the water null zone fade from bleeding through. Or raise null‑zone fade tolerances.

**Demo:** [DemoSceneWater.unity](Demo/Scenes/DemoSceneWater.unity).

---

## 20. Full Screen Overlays (Snow & Wetness)

`WeatherMakerFullScreenOverlayScript` drives both a snow overlay and a wetness overlay.

- Best appearance with **deferred shading**, but works on forward too.
- **AutoIntensityMultiplier** — link overlay intensity to the weather snow/wetness intensity. Tune to taste.
- If grass looks odd under the overlay, enable `WeatherMakerWavingGrass*` shaders (swap the top line to `Unity` namespace) and refresh your terrain details.

**Demo:** [DemoSceneWetness.unity](Demo/Scenes/DemoSceneWetness.unity).

---

## 21. Wind

- `WeatherMakerWindScript` + a **Wind Profile** ([Prefab/Profiles/Wind](Prefab/Profiles/Wind)).
- **WindIntensity** — enable/disable wind.
- **Random direction** — set **WindMaximumChangeRotation** to zero to disable; then the **Wind object’s transform rotation** becomes the fixed wind direction.
- **FogVelocityMultiplier** — zero to prevent wind from moving fog.
- Wind directly drives rain/snow/mist angle and, when enabled, water waves.
- See tooltips on each property in `WeatherMakerWindScript.cs` for the full list.

---

## 22. Light Manager

`WeatherMakerLightManagerScript` collects the lights that should affect Weather Maker shaders (fog, clouds, water, overlays, etc.) and sets them as global shader variables.

- Sun, moon, and lightning lights are registered automatically.
- Optional **auto‑find all lights** in the scene (convenient but can be a perf hit — prefer the `AutoAddLights` list).
- **Real‑time area lights** — supported. Set area light quadratic attenuation to `0` to disable.
- Area size = the light’s **lossy scale** (since area size is an editor‑only Unity property).
- **DirectionalLightIntensityMultipliers** and **DirectionalLightShadowStrengthMultipliers** dictionaries let you temporarily scale sun/moon intensity or shadows by key.
- **MaximumLightCount** (compile‑time constant) — reduce if GPU is pegged in many‑light scenes.
- **GlobalShadow** (`WeatherMakerLightManagerScript.Instance.GlobalShadow`) — the shared shadow used by Weather Maker fog and clouds.

---

## 23. Audio, Sound Zones & Ambient Sounds

Weather Maker ships a full ambient‑audio system — dawn birdsong, rain loops, night wolves, cave drips, etc.

### 23.1 Sound Groups

1. Right‑click in Project → **Create → Weather Maker → Sound Group**.
2. Create individual **Sound** scriptable objects. Each sound has:
   - Audio clip
   - Loop flag
   - **Interval** (time between attempts)
   - **Duration**
   - **Fade time**
   - Time‑of‑day window (dawn/day/dusk/night/specific hours)

### 23.2 Sound Zones

- Prefab: [WeatherMakerSoundZone.prefab](Prefab/WeatherMakerSoundZone.prefab).
- Drop into a player (scene‑wide) or a trigger volume (locational).
- Sounds are **2D** — they play while the trigger is entered.
- Zones can nest, but **avoid overlapping more than two**.
- For 3D ambient sound, use regular `AudioSource` sprinkled around your scene instead.

### 23.3 Volume Modifiers

`WeatherMakerAudioManagerScript.VolumeModifierDictionary` exposes per‑tag volume multipliers. Use them to duck ambient audio under voice or UI sounds.

### 23.4 Debugging

Uncomment the first line of `WeatherMakerSoundScript.cs` to log start/stop events to the console.

**Demo:** [DemoSceneSoundZones.unity](Demo/Scenes/DemoSceneSoundZones.unity).

---

## 24. Performance Profiles

Weather Maker maps one **Performance Profile** to each Unity Quality level. A profile carries all the quality knobs (cloud sample counts, downsample scale, reprojection, shadow samples, aurora sample counts, per‑pixel lights, fog sun shafts, reflection probes, precipitation collisions, etc.).

- Built‑in profiles match Unity’s default Quality levels — plus a **VR** profile.
- `WeatherMakerScript` has **Auto Performance Profile**. Disable to use your own profile.
- Set programmatically:
  ```csharp
  WeatherMakerScript.Instance.PerformanceProfile = myProfile;
  ```
- If no performance profile is set and VR is detected, the VR profile is auto‑selected.

**Tune each profile** until your target frame rate is hit on every supported platform.

---

## 25. Virtual Reality

- Use Unity **2020+** (newer is better).
- **Forward rendering** is strongly preferred (Unity deferred + XR has long‑standing bugs).
- Supported XR modes: **Multi‑pass**, **Single pass**, **Single pass instanced**.
- Use the **VR Performance Profile**, or leave the profile null to auto‑select VR at runtime.
- Demo: [DemoSceneVR.unity](Demo/Scenes/DemoSceneVR.unity).

---

## 26. Floating Point Origin Offset

32‑bit floats lose precision at large distances. Weather Maker supports a scene‑wide origin recenter via `WeatherMakerCommandBufferManagerScript`:

- **OriginOffsetDistance** — distance from (0,0,0) that triggers a recenter.
- **OriginOffsetAutoAdjustAll** — if true, Weather Maker shifts every root transform automatically (static objects are skipped by Unity).
- **OriginOffsetChanged** — event to implement your own per‑frame transform/shader adjustments when auto‑adjust is off.

**Helpers:**
```csharp
WeatherMakerCommandBufferManagerScript.Instance.GetCameraProperties(camera);
WeatherMakerCommandBufferManagerScript.Instance.ResetOriginOffset();
```

**Notes:**
- The event provides **current** and **cumulative** offsets. Use *current* to move GameObjects; use *cumulative* for shader camera positions.
- Only runs at play time.
- Best for **single‑camera** setups.

**Demo:** [DemoSceneOriginOffset.unity](Demo/Scenes/DemoSceneOriginOffset.unity).

---

## 27. Slimming the Build (Resource Container)

`WeatherMakerScript.ResourceContainer` is a profile that defines everything Weather Maker ships with the build. **Anything not referenced by this container is stripped at build.**

Steps to slim:

1. **Duplicate** an existing resource container (Default or Mobile) and rename it.
2. Remove profiles, textures, and audio clips you are sure you don’t use.
3. Assign the new container to the `WeatherMakerScript` **in the scene** or apply to the prefab.
4. Build and profile.

For a 2D game that doesn’t need any 3D bits, use [WeatherMakerPrefab2DNo3D.prefab](Prefab/WeatherMakerPrefab2DNo3D.prefab) — it strips weather zones, volumetric clouds, fog and big resource scripts/files. See [DemoScene2DNo3D.unity](Demo/Scenes/DemoScene2DNo3D.unity).

---

## 28. Weather API / Open Weather Map

`WeatherMakerLocationWeatherScript` syncs real‑world weather into a Weather Maker profile via the default Open Weather Map implementation.

1. Get a free API key at https://openweathermap.org/appid and assign it to the script.
2. Set a **location**:
   - **Lat/Lon** (explicit coordinates), or
   - **Place name** (overrides lat/lon if non‑empty), or
   - Lat/Lon `-999` — use the day/night cycle’s lat/lon.
   - Lat/Lon `999` — use current device location services.
3. The script maps Open Weather Map condition IDs to Weather Maker profiles.

**Demo:** [DemoSceneWeatherApi.unity](Demo/Scenes/DemoSceneWeatherApi.unity).

**Roll your own API:**
- Implement `IWeatherMakerLocationWeatherApi`.
- Assign your implementation to `WeatherMakerLocationWeatherScript.WeatherApi`.
- Reference `WeatherMakerOpenWeatherMapApi` as a starting example.

You can either map **conditions → Weather Maker profiles** (simple) or drive individual systems (wind, fog, clouds, rain, lightning) directly (advanced).

---

## 29. Location Services

The weather API feature uses Unity location services. Some platforms pop a permission dialog, which may be undesirable. To disable it:

- Wrap `WeatherMakerLocationWeatherScript.cs` with:
  ```csharp
  #if !ENABLE_THIS
  // ... file content ...
  #endif
  ```

---

## 30. Networking with Mirror

Weather Maker’s `WeatherMakerMirrorNetworkScript` syncs the **time of day** and the **global weather zone** from server to clients.

1. Project Settings → Player → Other Settings → Scripting Runtime = **.NET 4.x** / **.NET Standard 2.0+** (do this BEFORE adding Mirror or you will get crashes).
2. Import **Mirror** from the Asset Store (https://www.assetstore.unity3d.com/en/#!/content/129321).
3. Restart Unity.
4. On your **player prefab**:
   - Add a script patterned on [WeatherMakerNetworkPlayerScript.cs](Prefab/Scripts/Other/WeatherMakerNetworkPlayerScript.cs). For **non‑local players**, disable (do not delete) the audio listener, sound zone scripts, and cameras.
   - Make sure your player controller only runs on the local player (check `isLocalPlayer` / server commands).
   - Configure your layers so that the player layer can collide with the Weather Zone layer (default is `Default`).
   - (Optional) Add `WeatherMakerIsPlayerScript` — alongside a trigger collider. With a `NetworkIdentity` present, `IsLocalPlayer` populates automatically.
5. Add the **Mirror Network Manager** to your scene. Configure offline/online scenes, player prefab, max connections.
6. Add an empty GameObject to the root of your scene and attach `WeatherMakerMirrorNetworkScript`. Put it on its own object.
7. If building a **headless server**, define `UNITY_SERVER` in Scripting Define Symbols so Weather Maker skips command buffer execution on the server build.

**Caveats:**
- Weather profiles won’t be pixel‑identical per client, but look the same qualitatively per profile.
- Lightning and other random effects are per‑client random but match the active profile.
- To sync reliably, **all instances must be the same mode** — either all editor or all player builds. Mixing causes ScriptableObject serialization mismatches.

**Demo:** [DemoSceneMirror.unity](Demo/Scenes/DemoSceneMirror.unity).
Example prefab: `WeatherMaker/Prefab/NetworkPlayer.zip`.

**Other networking systems:** re‑write `WeatherMakerMirrorNetworkScript` to your preferred stack and assign it to `WeatherMakerScript.Instance.NetworkConnection`.

---

## 31. Shader Integration

`WeatherMakerFogExternalShaderInclude.cginc` is the bridge for your custom shaders:

- **`ComputeWeatherMakerFog`** — apply Weather Maker fog to a transparent shader.
- **`ComputeWeatherMakerShadows`** — sample Weather Maker shadows.
- Search the include for **`External shader integration functions`** to see all entry points.

Global shader variables set by Weather Maker are documented in `WeatherMakerCoreShaderInclude.cginc`.

**Downsampled depth buffers:**

```hlsl
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTextureHalf);
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTextureQuarter);
UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTextureEighth);
```

Use them instead of rolling your own depth downsampling — Weather Maker keeps them correct per rendered camera.

**Preprocessor define:** wrap asset‑integration code with `#if WEATHER_MAKER_PRESENT ... #endif` so it degrades gracefully when Weather Maker is absent.

---

## 32. Third‑Party Integrations

### Crest Ocean
- Use the fork: https://github.com/DigitalRuby/crest (standard pipeline only).
- `crest/Assets/Crest` lives as a sibling of the Weather Maker root.
- On main camera: add **OceanPlanarReflection**.
- On ocean + underwater materials: enable planar reflections and shadows.
- Set `OceanRenderer.primaryDirLight` to Weather Maker’s Sun (Moon at night).
- Add a reflection probe to the player.
- Set the Weather Maker fog render queue to **After Forward Alpha**.
- Place a large plane a few units below the ocean to fill the depth buffer if needed.
- Turn off reflection probes on Weather Maker performance profiles if flickering.
- In 8.0.4+: go to **Window → Weather Maker → Integrations → Crest** for one‑click setup.

### Playmaker
- Actions for get/set date‑time and weather profile by name.

### Vegetation Studio Pro
- Customize snow/rain buildup + melt speeds.
- Add **Weather Maker Vegetation Studio Pro** script to sync rain/snow/wind to biomes.
- Use a weather zone per biome (rain forest, snow mountain, desert).

### uMMORPG
- Works out of the box once Mirror instructions are followed exactly.

### Component → Weather Maker → Extensions → …

- **Wet Stuff**
  1. Define `WET_STUFF_PRESENT`.
  2. Camera = deferred.
  3. Drop `WeatherMakerExtensionWetStuffScript` onto a GameObject.
  4. Add Wet Stuff’s `AutoPuddlePrefab`, position/scale to cover your area.
  5. Set the puddle’s **base wetness source** to the Weather Maker extension script.
  6. Optional: add Wet Stuff particle scripts to Weather Maker particle systems and disable `RainRipples` / `SleetRipples`.

- **MegaSplat / MicroSplat** — customize rain and snow parameters.

- **WAPI (World Manager API)**
  - Drop `WeatherMakerExtensionWorldManagerScript`.
  - Do not use weather zones (WAPI takes over).
  - Assign cloud/fog profiles on the full screen cloud/fog scripts.

- **Uber — Standard Shader Ultra**
  - Define `UBER_STANDARD_SHADER_ULTRA_PRESENT`.
  - Add an `UBER_GlobalParams` somewhere, turn off **User Particle System**.
  - Tune global params.
  - Set minimum water level / wetness on `WeatherMakerExtensionUberScript`.
  - Weather Maker sets `RainIntensity`, `WaterLevel`, `WetnessAmount`, `SnowLevel`.

- **RTP — Relief Terrain Pack**
  - Define `RELIEF_TERRAIN_PACK_PRESENT`.
  - Weather Maker sets wetness and snow.

- **CTS (Complete Terrain Shader, old version)**
  - Rain/snow on terrain driven by precipitation.
  - Seasons driven by lat/lon/date/time.
  - Toggle which features are on.

- **Gaia**
  - Gaia Manager → **GX tab → Digital Ruby, LLC → Weather Maker**.
  - Set location, moon, time of day (editor only).

---

## 33. Demo Scenes Reference

All demo scenes live in [Demo/Scenes](Demo/Scenes). Open them as a guided tour of the features.

| Scene | Shows |
|-------|-------|
| [DemoScene.unity](Demo/Scenes/DemoScene.unity) | Main demo — kitchen‑sink. |
| [DemoSceneBlank.unity](Demo/Scenes/DemoSceneBlank.unity) | Minimal starter scene. |
| [DemoSceneNoPrefab.unity](Demo/Scenes/DemoSceneNoPrefab.unity) | Manual setup without the prefab. |
| [DemoScene2D.unity](Demo/Scenes/DemoScene2D.unity) | 2D with volumetric/flat clouds, sun, moon, lightning. |
| [DemoScene2DNo3D.unity](Demo/Scenes/DemoScene2DNo3D.unity) | Heavily optimized 2D‑only build. |
| [DemoSceneFlythrough.unity](Demo/Scenes/DemoSceneFlythrough.unity) | Flight‑through clouds (uses fly‑through profile). |
| [DemoSceneCloudBox.unity](Demo/Scenes/DemoSceneCloudBox.unity) | Cloud volume tests. |
| [DemoSceneCloudParticles.unity](Demo/Scenes/DemoSceneCloudParticles.unity) | Particle‑based clouds. |
| [DemoSceneCloudPostProcessing.unity](Demo/Scenes/DemoSceneCloudPostProcessing.unity) | Cloud post‑processing. |
| [DemoSceneCloudPostProcessingCartoon.unity](Demo/Scenes/DemoSceneCloudPostProcessingCartoon.unity) | Cartoon cloud post‑processing. |
| [DemoSceneCubemap.unity](Demo/Scenes/DemoSceneCubemap.unity) | Cubemap / 360° render. |
| [DemoSceneFogCubeTest.unity](Demo/Scenes/DemoSceneFogCubeTest.unity) | Volumetric fog cube. |
| [DemoSceneHeightFog.unity](Demo/Scenes/DemoSceneHeightFog.unity) | Height‑based ground fog. |
| [DemoSceneHorizon.unity](Demo/Scenes/DemoSceneHorizon.unity) | Horizon blending. |
| [DemoSceneIndoor.unity](Demo/Scenes/DemoSceneIndoor.unity) | Null zones, indoor/outdoor transitions. |
| [DemoSceneMirror.unity](Demo/Scenes/DemoSceneMirror.unity) | Mirror networking. |
| [DemoSceneMultipleCameras.unity](Demo/Scenes/DemoSceneMultipleCameras.unity) | Split‑screen / multiple cameras. |
| [DemoSceneNullZones.unity](Demo/Scenes/DemoSceneNullZones.unity) | Null zone masking + fade. |
| [DemoSceneOriginOffset.unity](Demo/Scenes/DemoSceneOriginOffset.unity) | Floating‑point origin shifts. |
| [DemoScenePrecipitationZones.unity](Demo/Scenes/DemoScenePrecipitationZones.unity) | Static precipitation zones. |
| [DemoSceneRollingStorm.unity](Demo/Scenes/DemoSceneRollingStorm.unity) | Storm sweeping across landscape. |
| [DemoSceneSkyFade.unity](Demo/Scenes/DemoSceneSkyFade.unity) | Sky fade + sky intensity. |
| [DemoSceneSoundZones.unity](Demo/Scenes/DemoSceneSoundZones.unity) | Ambient sound zones. |
| [DemoSceneVR.unity](Demo/Scenes/DemoSceneVR.unity) | VR setup. |
| [DemoSceneWater.unity](Demo/Scenes/DemoSceneWater.unity) | Water with caustics/waves/underwater. |
| [DemoSceneWeatherApi.unity](Demo/Scenes/DemoSceneWeatherApi.unity) | Open Weather Map integration. |
| [DemoSceneWeatherZones.unity](Demo/Scenes/DemoSceneWeatherZones.unity) | Weather zones with post FX. |
| [DemoSceneWetness.unity](Demo/Scenes/DemoSceneWetness.unity) | Full‑screen wetness overlay. |

---

## 34. Tutorial Videos

### Latest
- [Universal Render Pipeline](https://youtu.be/6RLPa1CWQS8)
- [Open Weather Map API](https://youtu.be/7_r7oibCM68)
- [uMMORPG / Mirror Tutorial](https://youtu.be/BIFQqmG2Gws)
- Volumetric Clouds
  - [Beautiful Sky Setup](https://youtu.be/1Xk2z858T1U)
  - [Complete Overview (40+ min)](https://youtu.be/ZoULn0mYbt8)
  - [Ray March Optimizations](https://youtu.be/Dwk4vF_6ogs)
  - [Cloud Noise Editor](https://youtu.be/NxuJ5H2mZwY)
  - [Aurora Borealis / Northern Lights](https://youtu.be/J9QSebVLEO8)
- [Temporal Reprojection](https://youtu.be/JCzE7JfFPU8)
- [Performance Profiles](https://youtu.be/-Yoj_OjJL4g)
- [Complete Setup Guide](https://youtu.be/DHNnS6f85rM)
- [Quick Start Guide](https://youtu.be/5IACGZULiDw)
- [New Prefab Overview](https://youtu.be/hWJF5v0s4gc)
- [Weather Zones](https://youtu.be/11QoMD_qhHw)
- [Null Zones](https://youtu.be/ZPBum2YcgB8)

### Older
- [Full Screen Snow](https://www.youtube.com/watch?v=b5j2wVHVxq0)
- [Water Full Overview](https://www.youtube.com/watch?v=oAQ4UFxa-X0)
- Fog
  - [Full Overview](https://www.youtube.com/watch?v=k-dC2EPd4no)
  - [Fog Shadow Map Lighting](https://www.youtube.com/watch?v=PTIC1oQzxno)
  - [Volumetric Lighting and Fog](https://www.youtube.com/watch?v=D9MUloqUQjU)
  - [Full Screen Fog (old)](https://www.youtube.com/watch?v=1_w9C8hWTXw)
  - [Fog Volumes (old)](https://www.youtube.com/watch?v=jJ_tx0Vog0o)
- Sound
  - [Scene / Ambient Sounds](https://www.youtube.com/watch?v=LdnALY4eCU4)
- Sky
  - [Setup](https://www.youtube.com/watch?v=QE3VZHWkVec)
  - [Clouds](https://www.youtube.com/watch?v=1YM1Z7ap0FU)
  - [Procedural Sky](https://www.youtube.com/watch?v=sB7U-yz-i6k)
  - [Suns and Moons](https://www.youtube.com/watch?v=neVZMeljYIQ)
  - [Day/Night Cycle](https://www.youtube.com/watch?v=M6PTyr52a00)
- Precipitation
  - [Rain Inside (deprecated, use null zone)](https://www.youtube.com/watch?v=zFT2KVoR3ro)
  - [Ripple Effect](https://www.youtube.com/watch?v=7V1ykljE9N8)

### Much Older
- [3D Demo](https://www.youtube.com/watch?v=25XEdmHFXQY)
- [2D Demo](https://www.youtube.com/watch?v=oX0Sa2IC2D4)
- [Multiple Cameras](https://www.youtube.com/watch?v=6y5U37p4RpE)

Full code documentation: https://unitydocs.digitalruby.com/

---

## 35. Scripting & API Cheat Sheet

```csharp
// Weather / profile
WeatherMakerScript.Instance.HasHadWeatherTransition = false; // next transition is instant
WeatherMakerScript.Instance.PerformanceProfile = myProfile;
WeatherMakerScript.Instance.PrecipitationManager.CustomPrecipitationScript = customScript;
WeatherMakerScript.Instance.NetworkConnection = myNetworkImpl;

// Day/Night
WeatherMakerDayNightCycleManagerScript.Instance.TimeOfDay = 12 * 3600f; // noon
WeatherMakerDayNightCycleManagerScript.Instance.Speed = 60f;            // 60x real time

// Fog
WeatherMakerFullScreenFogScript.Instance.TransitionFogDensity(0.0f, 0.5f, 5f);

// Lightning
WeatherMakerThunderAndLightningScript.Instance.CallNormalLightning(start, end);
WeatherMakerThunderAndLightningScript.Instance.CallIntenseLightning(start, end);
WeatherMakerThunderAndLightningScript.Instance.LightningStartedCallback += OnBoltStart;
WeatherMakerThunderAndLightningScript.Instance.LightningEndedCallback   += OnBoltEnd;
WeatherMakerThunderAndLightningScript.Instance.ThunderSoundPlayed       += OnThunder;

// Cloud probe
var probe = WeatherMakerFullScreenCloudScript.Instance.GetCloudProbe(camera, src, null);

// Light manager
WeatherMakerLightManagerScript.Instance.DirectionalLightIntensityMultipliers["voice_dim"] = 0.5f;
var globalShadow = WeatherMakerLightManagerScript.Instance.GlobalShadow;

// Origin offset
var cam = WeatherMakerCommandBufferManagerScript.Instance.GetCameraProperties(Camera.main);
WeatherMakerCommandBufferManagerScript.Instance.ResetOriginOffset();
```

---

## 36. Performance Troubleshooting

In order of impact (highest → lowest):

1. **Lower Unity Quality** (or use a lower‑quality Performance Profile).
2. **Clouds** — reduce sample counts, raise downsample scale, enable temporal reprojection, disable or simplify the flat layer.
3. **Water reflections** — set reflection layers to **None**, or swap reflection shader from Forward to **VertexLit** (daytime with few shadows).
4. **Fog** — raise downsample scale; reduce shadow and sun shaft sample counts; disable `EnableFogLights`.
5. **Precipitation** — lower emission rates (snow is the most CPU‑intensive). Raise mist threshold to `1` to disable mist. Disable collisions in the performance profile.
6. **Overlays** — disable snow / wetness full‑screen overlays.
7. **Lightning** — reduce **Generations**, set `LightPercent = 0`, zero glow intensity.
8. **Soft particles** — disable in Quality Settings (trade: harder edges).
9. **Collision** — simplify terrain. Use a collision plane where possible.
10. **Lights** — reduce `MaximumLightCount` in `WeatherMakerLightManagerScript.cs` if GPU is pegged.
11. **Eclipse check** — turn off `CheckForEclipse` on the sky sphere (Unity bug can cause CPU spikes).
12. **Null zones** — reduce the count.
13. **Reflection probes** — disable in the performance profile.
14. **Per‑pixel lighting** — off in the performance profile.

---

## 37. Troubleshooting / FAQ

- **Strange errors / compile issues after update** → delete the `WeatherMaker` folder, re‑import from the Asset Store, re‑add prefabs.
- **Clouds ghost / blur too much** → disable `WeatherMakerPrefab → FullScreenEffects → Clouds → Auto Set Temporal Reprojection Blend Mode`.
- **Clouds / shadows flicker, wiggle, jiggle, jitter** → add temporal AA, motion blur, bloom, color grading, or depth of field.
- **Too much fog** → check atmosphere fog density on the sky profile / atmosphere profile.
- **Everything looks gray** → confirm LWRP/HDRP defines are cleared and URP (if used) is actually enabled.
- **Scene view is white / flickering** → disable `AllowSceneCamera` on `WeatherMakerScript`.
- **Weather zone not triggering** → ensure kinematic rigidbody + tiny trigger sphere on the player and layer‑to‑layer collisions are enabled in Physics.
- **Rain / mist looks bad** → enable **HDR** on the camera.
- **Night sky invisible** → raise **NightIntensity** on the sky sphere profile.
- **Sky goes black when you alt‑tab back to the editor** → deactivate / reactivate the Weather Maker prefab. (Unity command buffer bug, editor only.)
- **Rain / snow flying upward** → particle system velocity‑over‑time Y values should be negative (everything except splash explosions).
- **Fog is white or invisible** → confirm Weather Maker prefab + Sun are enabled in the scene.
- **Shadow flickering** → add some wind, raise `CelestialObjectRotationUpdateInterval`, or bump shadow map resolution.
- **Shadows look wrong** → increase shadow distance, use 2 or 4 cascades, set sun **Base Shadow Strength** to `1`.
- **Depth artifacts at night with particles** → switch mist dest blend to **One (Additive)**.
- **Trees in / behind fog incorrectly** → enable `WeatherMakerTreeBillboardShader` (read the file header).
- **Sun shafts / FX fail in player build** → hide the Scene tab. (Unity quirk.)
- **Ultimate Survival / custom camera glitches** → tweak clear flags and culling mask.
- **Cloud shadows disappear at distance** → raise shadow distance; optionally patch `UnityShadowLibrary.cginc` (see [Cloud Shadows](#135-shadows)).
- **Reflection probes** → use realtime mode for moving clouds; do **not** use box projection.
- **Flickering lights** → disable reflection probes in the performance profile.

---

## 38. Known Issues

- Sky sphere **sphere** / **panorama** modes can show pole distortion. Use **dome** / **double dome** or correct the texture.
- **Fish Eye 360** sky sphere mode has side‑pole distortion. Fix TBD.
- Sun can shine through from below as it dips under the horizon. Add a large shadow‑casting cube as a floor (see `LargeGroundAndSunHorizonBlocker` in the main demo).
- **Odin serialization** should be disabled for Weather Maker — bugs.
- Volumetric clouds can jitter with temporal reprojection in VR. Use temporal AA.
- Fly‑through cloud horizon has some artifacts; terrain or mountains help mask them.

---

## 39. Credits

**Third‑party code / assets**
- [SlightlyMad AtmosphericScattering](https://github.com/SlightlyMad/AtmosphericScattering) (BSD, heavily modified)
- Airplane model: https://www.turbosquid.com/FullPreview/Index.cfm/ID/711530

**Code / rendering references**
- https://github.com/playdeadgames/temporal
- https://catlikecoding.com/unity/tutorials/flow/looking-through-water/
- https://patapom.com/topics/Revision2013/Revision%202013%20-%20Real-time%20Volumetric%20Rendering%20Course%20Notes.pdf
- Nubis / Decima Engine: http://advances.realtimerendering.com/s2017/Nubis%20-%20Authoring%20Realtime%20Volumetric%20Cloudscapes%20with%20the%20Decima%20Engine%20-%20Final%20.pdf
- https://gist.github.com/stephenmerendino/8b8aea77ac8d69a4427de588475cc0d2
- https://www.gamedev.net/forums/topic/680832-horizonzero-dawn-cloud-system/
- Horizon Zero Dawn clouds: http://killzone.dl.playstation.net/killzone/horizonzerodawn/presentations/Siggraph15_Schneider_Real-Time_Volumetric_Cloudscapes_of_Horizon_Zero_Dawn.pdf
- https://bib.irb.hr/datoteka/949019.Final_0036470256_56.pdf
- http://bitsquid.blogspot.com/2016/07/volumetric-clouds.html
- http://petewerner.blogspot.com/2015/02/intro-to-curl-noise.html

**Free sound / graphics**
- http://soundbible.com/1718-Hailstorm.html
- http://blenderartists.org/forum/archive/index.php/t-24038.html
- https://www.binpress.com/tutorial/creating-an-octahedron-sphere/162
- http://freesound.org/
- http://www.orangefreesounds.com/meditation-music-for-relaxation-and-dreaming/
- https://opengameart.org/content/seamless-animated-raindrop-ripples-texture
- https://freesound.org/people/akemov/sounds/255597/
- https://freesound.org/people/InspectorJ/sounds/398700/
- https://www.bensound.com — “Royalty Free Music from Bensound”

---

## 40. Support

- **Email** — support@digitalruby.com
- **Website** — https://www.digitalruby.com
- **Docs** — https://unitydocs.digitalruby.com/
- **Change Log** — [ChangeLog.txt](ChangeLog.txt)
- **Readme** — [Readme.txt](Readme.txt)

I’m Jeff Johnson — I built Weather Maker for you. Please send feedback, bug reports, and suggestions. Happy weather‑making.
