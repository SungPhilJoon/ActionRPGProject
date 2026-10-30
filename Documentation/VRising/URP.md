# URP 기능 사용 내역 (2026-10-14 512m 오픈월드 / 2026-10-29 식생 · 물 셰이더 / 2026-10-30 WeatherMaker 제거 · 포스트 프로세싱 · 캐릭터 셰이더)

파이프라인 에셋: `Assets/Settings/PC_RPAsset.asset` + `PC_Renderer.asset` (기존 프로젝트 설정을 그대로 사용하고, WorldBuilder 가 일부 값을 조정).
최적화는 요구사항에 따라 아직 하지 않았습니다. 비용이 큰 지점은 [Issues.md 18](Issues.md#18-성능-관찰-포인트-최적화는-직접-진행-예정) 에 모아 두었습니다.
게임 시간·태양 궤적·지역 배치는 [World.md](World.md) 가 원본이고, 이 문서는 **렌더 설정**의 원본입니다.

> 참고: `Packages/manifest.json` 에 **HDRP 패키지도 들어 있습니다** (외부 에셋 임포트 때 추가된 것으로 보입니다). 활성 파이프라인은 URP 이고, 제가 제거하지는 않았습니다. 쓰지 않는다면 제거하면 임포트/컴파일 시간이 줄어듭니다.

## 1. 조명 / 그림자 / 하늘

2026-10-30 WeatherMaker 를 제거했다 (연산이 큰 데 비해 쿼터뷰에서 보이는 효과가 작음 — 볼류메트릭 구름 · 대기 산란 · temporal reprojection 풀스크린 패스 여러 개).
하늘 · 낮밤은 URP 기본 기능으로 다시 만들었다.

| 기능 | 설정 | 위치 |
|---|---|---|
| 태양 / 달 | Directional Light 1개 (낮 = 해, 밤 = 푸른 달빛, 그림자 맵 1개 유지) | WorldBuilder.Sky, DayNightManager |
| 하늘 | **Skybox/Procedural** (`M_Skybox`) — 해 원반 · 대기 두께. 실행 중 사본을 만들어 노출 0.14(밤) ~ 1.25(낮), 하늘 색을 바꿈 | WorldBuilder.Sky, DayNightManager.applyAtmosphere |
| 안개 | Unity 지수 제곱 안개 — 낮 0.0042 회청색 / 해질녘 주황 / 밤 0.0065 남색 | 〃 |
| 반사 | **빌드 때 구운 하늘 큐브맵** (`SkyReflection.cubemap`, 해 원반 제외) → Custom 반사. 낮 0.65 ~ 밤 0.2. 예전에는 반사가 비어 금속 무기가 검게 보였음. 실시간 반사 프로브는 캐릭터가 깨져 쓰지 않음 (Issues 38) | WorldBuilder.Sky |
| Ambient | Trilight 하늘 · 지평선 · 바닥 3색을 DayNightManager 가 낮/밤 보간 (낮 하늘 0.78/0.8/0.88, 밤 하늘 0.38/0.42/0.62) | DayNightManager |
| Main Light Shadow | Soft Shadows (High), Cascade 2, **Distance 90**, 그림자 세기 0.8 | WorldBuilder.configurePipeline, .Sky |
| Additional Lights | 횃불 · 성 심장 · 제련소 · 투사체 Point Light | ContentBuilder, WorldBuilder.Landmarks |
| SSAO | 강도 1.1, 반경 0.55, 직접광 0.45 (나무 밑 · 건물 모서리 접촉 그림자) | WorldBuilder.configureRendering |
| 카메라 | Clear = Skybox, HDR, **SMAA High**, Dithering(밴딩 방지), Far 2000 | WorldBuilder.buildCamera |

**햇빛 판정과 그림자의 관계**: 화면의 그림자는 섀도우맵이지만, 게임 판정(햇빛 화상)은 섀도우맵을 읽지 않고
`SunExposure` 가 태양 방향(`DayNightManager.DirectionToSun`)으로 물리 레이캐스트(SunBlocker/Structure/Ground/Interactable/Default)를 쏩니다.
그림자를 드리우는 정적 메시에 같은 모양의 SunBlocker MeshCollider 를 붙여 **판정 형태도 그림자와 같게** 했습니다 (Issues 26).

## 2. (삭제) WeatherMaker 통합

2026-10-30 제거. 렌더러 피처(`WeatherMakerURPRenderFeatureScript`), 스크립팅 디파인(`WEATHER_MAKER_PRESENT`, `UNITY_URP`), `Game/Integration/WeatherMaker`(어댑터), `ExternalAssets/WeatherMaker`(321MB) 를 지웠다.
`ISkyController` / `SkyBridge` 인터페이스는 남겨 두었다 (외부 하늘 에셋을 다시 붙일 때의 확장점, 지금은 등록된 구현 없음). 복구가 필요하면 git 이력에 있다.

## 3. Post-processing (Volume 6개, weight 블렌딩)

| 볼륨 | 우선순위 · weight | 내용 |
|---|---|---|
| **Day** (`VP_Day`) | 0 · dayWeight | ACES, Bloom(1.0 / 0.85, HQ 필터, 약한 웜 틴트) + **Screen Space Lens Flare**, Color Adjustments(+0.2, 대비 12, 채도 18), White Balance(−4), **Split Toning**(그늘 청록 · 하이라이트 옅은 금빛), **Shadows/Midtones/Highlights**(그늘 +0.03), **Lift/Gamma/Gain**, Vignette 0.24, Film Grain 0.14, Chromatic Aberration 0.06, Motion Blur(카메라, 0.18) |
| **Night** (`VP_Night`) | 0 · 1 − dayWeight | ACES, Bloom(0.7 / 1.6, 붉은 틴트 — 횃불이 번짐) + 렌즈 플레어, 노출 +1.3 · 대비 14 · 채도 −12 · 푸른 필터, White Balance −18, Split Toning(그늘 남색 · 하이라이트 주황, 균형 −10), Vignette 0.34 검붉음, Film Grain 0.3 |
| **Focus** (`VP_Focus`) | 5 · 1 | **보케 피사계 심도** 40mm f/9 — 초점 = 카메라 ~ 플레이어 거리(매 프레임). 화면 위쪽 먼 지형이 살짝 흐려져 미니어처 느낌 |
| **Sunburn** (`VP_Sunburn`) | 10 · 햇빛 노출 | 주황 비네트 0.38, 색수차 0.3, 렌즈 왜곡 −0.12(열기), 노출 +0.3 · 약한 웜 필터 (처음 값은 화면 전체가 주황으로 덮였음, Issues 39) — 햇빛 화상 중임을 화면으로 알림 |
| **LowHealth** (`VP_LowHealth`) | 11 · 체력 35% 미만 × 맥동 | 붉은 비네트 0.55, 채도 −55, 색수차 — 심장 박동처럼 맥동 (체력이 낮을수록 빠름) |
| **Death** (`VP_Death`) | 12 · 사망 중 | 흑백, −0.6 노출, 대비 25, 검은 비네트 |

- 낮/밤 weight: `DayNightManager.applyVolumes` (태양 고도 0~12° → dayWeight).
- 상태 볼륨 weight · 피사계 심도 초점 · 대기 패스 전역 값: `ScreenEffects` (카메라, `Game/Runtime/Camera/ScreenEffects.cs`). weight 만 바꾸므로 프로필 에셋은 수정되지 않는다.
- URP 에셋: **HDR 컬러 그레이딩**(톤매핑 전에 보정 → 블룸 · 횃불이 하얗게 날아가지 않음), LUT 32.
- 볼륨 값은 `WorldBuilder.PostFx` 가 만든다 (`Game > Setup > 5. Build World Scene`). 손으로 고친 프로필은 다시 빌드하면 덮어써진다.

## 4. HDR 발광 + Bloom

투사체(BloodBolt/Shockwave)와 성 심장은 URP Lit `_EMISSION` 을 HDR 강도(×3~6)로 켜서 Bloom 이 걸리게 했습니다.
파티클은 `Universal Render Pipeline/Particles/Unlit` 가산(Additive) 재질과 코드로 생성한 원형 텍스처를 씁니다.

## 5. 외부 에셋 머티리얼 → URP Lit

`Framework/Editor/Util/MaterialUpgradeUtil` 이 Built-in/Standard/UnityChan 셰이더 머티리얼의 **URP Lit 복사본**을 `Art/Generated/Materials/*` 에 만들고, 생성 프리팹에서만 교체합니다 (원본 무수정, Issues 23).

## 6. Terrain (URP Terrain Lit)

- 512×512m, 높이 120m, Erosion 실측 높이맵 합성 → 자세한 내용은 [World.md](World.md)
- Terrain Layer 8장 (TerrainSampleAssets 의 TerrainLayer 에셋을 그대로 참조)
- **Draw Instanced** 활성화
- Detail: **Game/Foliage** 풀 프리팹 (8절), **InstanceCountMode** (Issues 19)
- 나무: Terrain Tree 를 쓰지 않고 GameObject + LODGroup 절차 나무 (World.md 7.1)
- 강 · 호수: **Game/Water** (9절)

## 7. 성능 관찰 포인트

렌더링(GPU) 항목을 포함한 전체 목록은 [Issues.md 18](Issues.md#18-성능-관찰-포인트-최적화는-직접-진행-예정) 이 원본입니다.

## 8. 식생 셰이더 `Game/Foliage` (`Assets/Game/Art/Shaders/Foliage.shader`)

손으로 쓴 HLSL URP 셰이더. 나무 껍질 · 잎 · 덤불 · 풀(Terrain Detail 인스턴싱)이 모두 같은 셰이더라 바람이 같이 움직인다.

| 패스 | 내용 |
|---|---|
| ForwardLit | `UniversalFragmentPBR` (메인/추가 라이트, 그림자, Forward+ `_CLUSTER_LIGHT_LOOP`, SSAO, 안개) + **잎 반투과**(해를 등진 잎이 역광으로 빛남, 그림자 안에서는 꺼짐) + 개체별 색 변화(오브젝트 원점 해시) |
| ShadowCaster / DepthOnly / DepthNormals | 같은 바람 변형 + 알파 클립 → 그림자 · 깊이가 흔들리는 잎과 일치 |

| 프로퍼티 | 의미 |
|---|---|
| `_WindStrength` / `_WindSpeed` / `_WindDirection` / `_WindFlutter` | 줄기 흔들림(m), 속도, 방향(xz), 잎 떨림(m). 같은 나무의 껍질/잎 머티리얼은 값이 같아야 한다 (`TreeFactory.setTreeWind`) |
| 정점 색 | R = 흔들림 가중치, G = 떨림, B = AO. 정점 색이 없는 외부 메시는 `_UseHeightWeight = 1` (오브젝트 높이 / `_SwayHeight`) |
| `_FlipBackNormals` | 풀 = 1(뒷면 노멀 뒤집기), 나무 잎 = 0(수관 구형 노멀 유지) |
| `_Cull` | 잎/풀 Off, 껍질 Back |

텍스처: 잎 카드 절차 텍스처는 `FoliageCanvas` 가 CPU 로 그린다(안티앨리어싱 선 · 잎 모양, 투명 영역 색 채움, 알파 커버리지 보존 밉맵). 껍질 3종은 Poly Haven CC0 (`Art/ThirdParty/PolyHaven/Bark`).

## 9. 물 셰이더 `Game/Water` (`Assets/Game/Art/Shaders/Water.shader`)

Transparent-10 큐, ZWrite Off. **URP 에셋의 Depth Texture · Opaque Texture 가 켜져 있어야 한다** (`WorldBuilder.configurePipeline` 이 켠다, PC_RPAsset 은 이미 켜짐).

| 요소 | 방법 |
|---|---|
| 깊이 색 | `_CameraDepthTexture` 로 수면 뒤 지형까지 거리 → 얕은 색(굴절된 바닥 × 틴트) ↔ 깊은 색(조명 반영) smoothstep |
| 굴절 | 노멀로 `_CameraOpaqueTexture` UV 를 왜곡. 왜곡된 지점이 수면보다 앞이면 원래 UV (물 앞 물체가 번지지 않게) |
| 노멀 | 타일링 절차 노멀맵 `T_WaterNormal` — 바람 물결 2방향 + **2위상 플로우맵**(정점 색 RG 방향 · B 속도, 위상 0.5 차이 두 샘플 교차) |
| 반사 | 프레넬 × (`GlossyEnvironmentReflection`(구운 하늘 큐브맵)과 하늘 대체색 혼합) + 해 스펙큘러(그림자 반영) |
| 거품 | 물가(얕은 깊이 `_FoamDistance`) + 급류(정점 색 A) × 흐르는 거품 노이즈 `T_WaterFoam` |
| 가장자리 | 알파 = 깊이 / `_EdgeFade` → 물가가 부드럽게 사라짐 |
| 기타 | 메인 라이트 그림자 수신, 안개(MixFog) |

## 10. 대기 풀스크린 패스 `Game/PostFx/Atmosphere` (`Assets/Game/Art/Shaders/Atmosphere.shader`)

WeatherMaker 대기 산란을 대신하는 가벼운 패스 1개. `PC_Renderer` 에 **Full Screen Pass Renderer Feature** `Atmosphere (Game)` 로 들어간다 (`WorldBuilder.addAtmosphereFeature`).

| 항목 | 내용 |
|---|---|
| 시점 | `BeforeRenderingPostProcessing` — 불투명 + 물까지 그린 HDR 색에, 톤매핑 · 블룸 전에 섞음. Depth 텍스처 필요 |
| 높이 안개 | 밀도 `d · exp(−k(h − base))` 를 카메라 → 픽셀 직선을 따라 해석적으로 적분 → 낮은 곳(호수 분지 · 강 · 계곡)에 안개가 고임. 최대량 `FogMax` |
| 태양 산란 | 시선이 해 쪽일수록 안개 색이 햇빛 색으로 (역광 아지랑이) |
| 하늘 | 깊이 = 먼 평면인 픽셀은 건드리지 않음 |
| 값 | `ScreenEffects` (카메라) 인스펙터: `FogDensity` 0.018, `FogFalloff` 0.12, `FogBaseHeight` 5, `FogMax` 0.55. 매 프레임 전역 셰이더 값(`_AtmoSunDir` 등)으로 넣음 |

비용: 풀스크린 1패스 + 깊이 샘플 1회 (WeatherMaker 는 구름 · 대기 · temporal reprojection 등 풀스크린 패스 여러 개).

## 11. 캐릭터 셰이더 `Game/Character` (`Assets/Game/Art/Shaders/Character.shader`)

플레이어 · 몬스터 공용. URP Lit 변환 후 캐릭터가 배경에 묻히고 unity-chan 툰 룩이 사라졌던 문제를 해결하려고 만들었다.

| 패스 | 내용 |
|---|---|
| ForwardLit | URP Lit 과 같은 맵 규칙(Base, Normal, MetallicGloss R/A, Occlusion G, Emission) + 메인/추가 라이트(Forward+), 그림자, SSAO, 안개 |
| Outline | 노멀 방향으로 부풀린 뒷면 껍질 (`SRPDefaultUnlit` 라이트모드 — URP 포워드가 같은 오브젝트에 추가로 그림) |
| ShadowCaster / DepthOnly / DepthNormals | 그림자 · SSAO · 피사계 심도 |

| 기능 | 플레이어 (툰) | 몬스터 (PBR) |
|---|---|---|
| 라이팅 | `_Toon = 1` — 메인 라이트 2단계 밴드 + 그늘 틴트(0.72/0.6/0.68), 횃불은 부드러운 밴드 | PBR + **보조광** `_FillLight` 0.45 (반-램버트, 해가 머리 위일 때 세로 면이 검게 뭉개지지 않게) |
| 림라이트 | 0.35 | 0.8 — 쿼터뷰에서 캐릭터가 지면과 분리돼 보임 |
| 외곽선 | 0.0035m | 0.006m |
| 피격 플래시 | 붉은색 | 흰색 — `CharacterHitFlash` 가 `_HitFlash` 를 MaterialPropertyBlock 으로 0.14초 올렸다 내림 (끝나면 블록을 지워 SRP Batcher 로 복귀) |

- 변환: `CharacterLook` (Game.Editor) 이 Lit 머티리얼을 `Art/Generated/Materials/Characters/<이름>_Char.mat` 사본으로 바꾼다. 원본 머티리얼은 그대로.
  `ContentBuilder` 가 캐릭터 프리팹을 저장할 때 자동 호출, 이미 만든 프리팹은 메뉴 **Game > Art > Apply Character Look**.
- 반투명 재질은 변환하지 않음. 눈 · 속눈썹 · 이빨 · 볼터치 · 입 등(이름으로 판별)은 외곽선 끔.
- 실시간 반사 프로브를 쓰면 캐릭터가 청록색 실루엣으로 깨져서 하늘 큐브맵 반사만 쓴다 (Issues 38).
