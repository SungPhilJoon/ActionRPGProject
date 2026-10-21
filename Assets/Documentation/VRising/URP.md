# URP 기능 사용 내역 (2026-10-14 갱신: WeatherMaker 통합, 512m 오픈월드)

파이프라인 에셋: `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset` (기존 프로젝트 설정을 그대로 사용하고, WorldBuilder 가 일부 값을 조정).
최적화는 요구사항에 따라 아직 하지 않았습니다. 비용이 큰 지점은 마지막 절에 정리했습니다.

> 참고: `Packages/manifest.json` 에 **HDRP 패키지도 들어 있습니다** (외부 에셋 임포트 때 추가된 것으로 보입니다). 활성 파이프라인은 URP 이고, 제가 제거하지는 않았습니다. 쓰지 않는다면 제거하면 임포트/컴파일 시간이 줄어듭니다.

## 1. 조명 / 그림자

| 기능 | 설정 | 위치 |
|---|---|---|
| 태양 / 달 | **WeatherMaker** Sun/Moon Directional Light. 시간은 게임이 지정 (World.md 3절) | WorldBuilder.Sky, WeatherMakerSkyBridge |
| 태양 밝기 | `DirectionalLightIntensityMultipliers["GameSunBoost"] = 1.7` | WeatherMakerSkyBridge |
| 밤 밝기 | 밤 Ambient ×6, 새벽·황혼 ×2.5, 달빛 배율 1.6 (런타임 복제 프로필에 적용) | WeatherMakerSkyBridge |
| 구름 그림자 | 끔 (`CloudShadows = false`) — 구름은 햇빛 판정을 막지 않으므로 | WeatherMakerSkyBridge |
| Main Light Shadow | Soft Shadows, Cascade 2, **Distance 90** (맵 확대로 70 → 90) | WorldBuilder.configurePipeline |
| Additional Lights | 횃불 기둥, 성 심장, 제련소, 투사체 Point Light (그림자 없음) | ContentBuilder, WorldBuilder.Landmarks |
| Ambient | 모드는 Trilight (WorldBuilder.Sky). **색은 런타임에 WeatherMaker DayNight 프로필이 매 프레임 덮어씀** (AmbientColorMode = AllWithUnityMode) → 낮/새벽·황혼/밤 Ambient 그라디언트로 조정 | WeatherMakerDayNightCycleProfile_Default |
| Reflection | Custom (WeatherMaker 권장) | WorldBuilder.Sky |
| SSAO | PC_Renderer 의 ScreenSpaceAmbientOcclusion (기존) | PC_Renderer.asset |

**WeatherMaker 가 없을 때**: `SkyBridge.Controller == null` → `DayNightManager` 가 예전처럼 한 개의 Directional Light 를 직접 회전·색 보간 (태양/달빛 겸용).

**햇빛 판정과 그림자의 관계**: 화면의 그림자는 섀도우맵이지만, 게임 판정(햇빛 화상)은 섀도우맵을 읽지 않고
`SunExposure` 가 태양 방향으로 물리 레이캐스트(SunBlocker/Structure/Ground/Interactable/Default)를 쏩니다.
GPU 섀도우맵을 CPU 로 읽는 것은 비싸고, 멀티플레이 서버에서는 할 수 없기 때문입니다. 태양 방향은 WeatherMaker Sun 라이트에서 읽으므로 화면의 해와 판정 방향이 같습니다.
그림자를 드리우는 모든 정적 메시(지형 나무 포함)에 같은 모양의 SunBlocker MeshCollider 를 붙여 **판정 형태도 그림자와 같게** 했습니다 (Issues 26).

## 2. WeatherMaker 통합 (URP)

WeatherMaker 의 "Enable URP" 메뉴는 파이프라인 에셋을 교체하므로 쓰지 않고, 아래 항목만 수동으로 적용했습니다 (Issues 21).

| 항목 | 내용 |
|---|---|
| 스크립팅 디파인 | `UNITY_URP` |
| Render Feature (PC_Renderer, 순서) | ScreenSpaceShadows → **WeatherMakerURPRenderFeatureScript** → SSAO |
| grab-pass | URP 에 없는 GrabPass 사용 부분을 URP 방식으로 대체 |
| 카메라 | Clear = Solid Color (0,0,0,0), HDR 켬, Far 2000 |
| Skybox | `WeatherMakerSkyBoxMaterial` |
| Fog | Unity Fog 끔 (WeatherMaker 가 대기 산란 처리) |

외부 에셋 파일 자체는 수정하지 않았습니다. 바꾼 것은 렌더러 데이터, 디파인, 그리고 grab-pass 대체입니다.

## 3. Post-processing (Volume 2개를 낮/밤 weight 로 블렌드)

| | Day (`VP_Day`) | Night (`VP_Night`) |
|---|---|---|
| Tonemapping | ACES | ACES |
| Bloom | threshold 1.1, intensity 0.45 | threshold 0.8, intensity 1.2, 붉은 틴트 (혈액 이펙트 강조) |
| Color Adjustments | exposure +0.35, contrast 10, saturation 6 | exposure **+1.5**, contrast 4, saturation −10, 푸른 컬러 필터 (밤에도 보이게, Issues 27) |
| Vignette | 0.2 | 0.25, 검붉은 색 |

`DayNightManager.applyVolumes` 는 태양 고도(0~12°)를 `dayWeight` 로 바꿔 `DayVolume.weight = dayWeight`, `NightVolume.weight = 1 − dayWeight` 를 넣습니다.
카메라: `UniversalAdditionalCameraData.renderPostProcessing = true`, **SMAA**.

## 4. HDR 발광 + Bloom

투사체(BloodBolt/Shockwave)와 성 심장은 URP Lit `_EMISSION` 을 HDR 강도(×3~6)로 켜서 Bloom 이 걸리게 했습니다.
파티클은 `Universal Render Pipeline/Particles/Unlit` 가산(Additive) 재질과 코드로 생성한 원형 텍스처를 씁니다.

## 5. 외부 에셋 머티리얼 → URP Lit

`Framework/Editor/Util/MaterialUpgradeUtil` 이 Built-in/Standard/UnityChan 셰이더 머티리얼의 **URP Lit 복사본**을 `Art/Generated/Materials/*` 에 만들고, 생성 프리팹에서만 교체합니다 (원본 무수정, Issues 23).

## 6. Terrain (URP Terrain Lit)

- 512×512m, 높이 120m, Erosion 실측 높이맵 합성 → 자세한 내용은 [World.md](World.md)
- Terrain Layer 8장 (TerrainSampleAssets 의 TerrainLayer 에셋을 그대로 참조)
- **Draw Instanced** 활성화
- Detail: URP Lit Alpha Clip 풀 프리팹, **InstanceCountMode** (Issues 19)
- Tree: 재채색 Kenney 나무 메시 (빌보드 미사용)
- 호수: URP Lit Transparent 평면

## 7. 성능 관찰 포인트 (직접 프로파일링할 후보)

| 지점 | 예상 비용 | 메모 |
|---|---|---|
| Soft Shadow + Cascade 2 + 그림자 거리 90 | GPU | 쿼터뷰라 거리를 줄일 여지가 큼 |
| SSAO | GPU (풀해상도) | Downsample 옵션 |
| WeatherMaker 구름/대기 | GPU 풀스크린 | 구름 프로필 품질 조정 가능 |
| Detail 풀 인스턴싱 | GPU 정점 | 밀도 / detailObjectDistance |
| Terrain Tree 메시 약 2,300 (빌보드 끔) | 드로우콜/정점 | 빌보드 지원 나무로 교체 시 개선 |
| TheTalesFactory 포토스캔 바위 | 정점 / 텍스처 메모리 | LOD 확인 |
| 횃불/투사체 Point Light | Forward 라이트 수 제한 | Forward+ 전환 검토 |
| 파티클 Overdraw | GPU | 가산 블렌드 겹침 |
| TMP 데미지 숫자 | Canvas 리빌드 | 숫자마다 별도 텍스트 |
