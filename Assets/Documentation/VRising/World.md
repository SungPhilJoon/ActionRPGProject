# 오픈월드 맵 (World.unity) — 2026-10-14

`Game > Setup > 5. Build World Scene` (`Game/Editor/World/WorldBuilder*.cs`) 이 코드로 생성합니다. 씬을 손으로 수정하면 다시 빌드할 때 사라지므로, 변경은 빌더 코드나 CSV 에서 합니다.

## 1. 지형 규격

| 항목 | 값 | 비고 |
|---|---|---|
| 크기 | 512 × 512m, 최대 높이 120m | 이전 256m / 40m 에서 확대 |
| 높이맵 | 513 해상도 | Perlin fbm 언덕 + 고원 + 가장자리 산맥 + 호수 분지 + 평탄화 존 |
| 산맥 | TerrainSampleAssets 의 실측 **Erosion 높이맵**을 GPU Blit/ReadPixels 로 읽어 가장자리에 합성 | 원본 텍스처 임포트 설정은 건드리지 않음 |
| Terrain Layer | TerrainSampleAssets 8종: Grass_A, Grass_Moss, Soil_Rocks, Rock, Grass_Dry, Muddy, Sand, Snow | 경사·고도·노이즈·지역으로 Splat 페인트 (`paintSplat`) |
| Detail (풀) | TerrainSampleAssets 풀/고사리 메시 → URP Lit Alpha Clip 프리팹으로 재생성 (`Art/Generated/World/Detail`) | `DetailScatterMode.InstanceCountMode` (Issues 19) |
| Tree | Kenney 나무 재채색 프리팹 (`Art/Generated/World/Trees`), 약 2,300그루 | 사실적 나무 에셋 없음 → AssetRequests |
| 바위 | TheTalesFactory MountainRocks (포토스캔) | 산맥 고리 70개 + 고원 절벽 |
| 물 | URP 반투명 평면 (호수) | WeatherMaker 물 프리팹은 무한 바다(스케일 4096)라 미사용 |

## 2. 지역 (WorldRegions = SpawnTable 좌표)

| 지역 | 중심 (x, z) | 지형 | 내용 |
|---|---|---|---|
| Silverlight Glade (시작) | (256, 96) | 평지, 흙바닥 | 플레이어 시작, 상인 텐트+횃불, 폐허 벽·쉼터(그늘) |
| Meadow | (230, 170) | 완만한 초원 | 덤불 채집, 메두사 2 |
| Ruined Hamlet | (300, 215) | 평탄화 | ASTROFISH 건물(Composite_A/C, House1, RepunTower, OpenAirShelter, StoneLookoutTower), 울타리·통·횃불, 메두사 5 + 코브라 2 |
| Dunley Woods | (130, 230) | 숲 | 소나무/참나무 채집 노드 (그늘) |
| Lake / Serpent Marsh | (170, 262) / (175, 300) | 분지, 수면 4m, 진흙·모래 | 코브라 4, 덤불 |
| Rocky Highlands | (390, 220) | 고원 + 절벽 | 바위/큰 바위 채집 |
| Copper Hollow | (430, 350) | 산기슭 | 구리 광맥 |
| Troll Ridge | (250, 385) | 해발 +9m 고원 평탄화 | 바위·쉼터, 트롤 4 + 메두사 2 |
| Minotaur Arena (V Blood) | (390, 430) | 평탄화 | MountainRocks01_A 고리 + 횃불 기둥, 보스 미노타우로스 |
| 가장자리 | 맵 외곽 | 침식 산맥, 눈 | 이동 경계 |

몬스터·채집 노드·상인은 런타임에 `SpawnTable.csv` 로 생성됩니다 (월드 빌더는 지형·장식·랜드마크만 배치). 시작 시 확인: 몬스터 20, 채집 노드 231.

## 3. 하늘 / 햇빛 (WeatherMaker 8.1.2)

| 항목 | 내용 |
|---|---|
| 배치 | `WeatherMakerPrefab` 인스턴스 (태양/달/하늘/구름) |
| 시간 | 게임(`DayNightManager`)이 주인. `WeatherMakerSkyBridge.SetHour` 가 매 프레임 `TimeOfDay` 를 덮어쓰고 WeatherMaker 자체 Speed 는 0 |
| 태양 궤적 | 위도 40°, 경도 0, 시간대 0, 9월 21일(추분) → 일출/일몰 ≈ 6시/18시, 정오에 최고 고도 |
| 밝기 | `DirectionalLightIntensityMultipliers["GameSunBoost"] = 1.7` (쿼터뷰에서 지면이 어둡지 않도록) |
| 햇빛 판정 | `SkyBridge.Controller.DirectionToSun` = WeatherMaker Sun 라이트 방향 → 화면의 해와 `SunExposure` 레이캐스트 방향이 일치. 그림자 메시와 같은 모양의 SunBlocker 콜라이더로 판정 형태도 일치 (Issues 26) |
| 밤 | 밤 Ambient ×6, 달빛 ×1.6, VP_Night 노출 1.5 (Issues 27) |
| 구름 그림자 | 끔 (판정과 일치시키기 위해) |
| 볼륨 | 태양 고도(0~12°)로 Day/Night Volume weight 블렌드 |
| 렌더 설정 | 카메라 Solid Color(0,0,0,0)+HDR, Skybox = WeatherMakerSkyBoxMaterial, Ambient Trilight (색은 WeatherMaker DayNight 프로필이 런타임에 결정), Reflection Custom, Unity Fog 끔 |

WeatherMaker 를 지우면 `SkyBridge.Controller == null` 이 되어 `DayNightManager` 가 기존 방식(자체 Directional Light 회전)으로 동작합니다.

## 4. 건축 모듈 (ASTROFISH, 3m 그리드)

| 구조물 | 모델 | 스냅 |
|---|---|---|
| Castle Heart | 기둥 + LargeTorch + 발광 코어 | 셀 중심 |
| Floor | StoneFloor_Tile 3×3 | 셀 중심 |
| Wall / WallWindow / WallDoor | PlasterWalls_B_MasonaryWall / PlasterWalls_A_WindowBase / WallTall_DoorCropped (콜라이더 3개, 문 통과 가능) | 셀 가장자리 (회전에 따라 축 결정) |
| Pillar / Lamp | PortchColumn_Medium / 기둥 + IronFixtures_Lamp | 셀 모서리 (footprint ≤ 1 장식) |
| Workbench / Smelter / Coffin | ShortBoardPlanks + Hammer/Saw / Chimney_Small + 라이트 / Crafting Box | 셀 중심 |

모델 크기는 `fitHeight/fitHorizontal/fitBox` 로 그리드에 맞추고, 머티리얼은 URP Lit 복사본으로 변환합니다 (원본 에셋 수정 없음).

## 5. 캐릭터 / 애니메이션

| 대상 | 모델 | 애니메이션 |
|---|---|---|
| 플레이어 | unity-chan! (`unitychan_dynamic`), 원본 입력 스크립트(IdleChanger, FaceUpdate, IKCtrlRightHand, RandomWind) 제거 | RPG Character FREE: Idle/Walk/Run, 2Hand-Sword 공격 1~3, DiveRoll, GetHit, Knockdown / Crafting Pack: Item-Hammer-Wall(건축), Chop-Horizontal(벌목), Item-Swing-Vertical(채광), Gather(채집), Item-Take/Pickup |
| 메두사 (BanditThug 슬롯) | Magic Pig Medusa | 모델 자체 Generic 클립 |
| 코브라 (BanditArcher 슬롯) | Magic Pig CobraSnake ×0.55 | AlertIdle/AlertWalk, Spit1/2 |
| 트롤 (OrcBrute 슬롯) | Magic Pig Troll | Attack 1~, Got Hit, Jump |
| 미노타우로스 (보스) | Magic Pig Minotaur ×1.5 | RamAttack 등 |
| 상인 | Crafting Pack Crafter | Idle/Wave |

- 무기 소켓: 같은 포즈(`2Hand-Sword-Idle` 0프레임)를 RPG-Character 와 unity-chan 에 샘플링해 RPG 검 위치를 unity-chan RightHand 기준으로 변환 (`computeWeaponSocket`), 크기 보정 0.7.
- 근접 콤보는 범위 안에 적이 없고 채집 노드가 있으면 노드 종류에 맞는 채집 애니메이션(Chop/Mine/Gather)을 재생 (`ResourceNode.FindHarvestAnim`).
- 건축 시 1.1초 이동 잠금 + Build 애니메이션.

## 6. 미검증 / 알려진 문제 (솔직하게)

- **Play 모드에서 지형 풀이 보이지 않음 (조사 중)**: 편집 모드 Scene 뷰 (256,100) 에서는 풀이 그려지는 것을 확인했지만, Play 중 (215,150)·(256,96) 의 `GetDetailLayer` 합이 0 으로 읽혔고 화면에도 풀이 없었다. 시작 지점은 의도적으로 흙바닥이라 0 이 맞지만 (215,150) 은 원인 미확정. 후보: 샘플링 좌표 변환 오류, 해당 위치 Splat 가중치가 낮아 풀이 심어지지 않음, 런타임 변경. Issues 19.
- 육안 확인을 다 하지 못한 곳: Troll Ridge, Minotaur Arena, 호수 근처 근접 화면, 신규 몬스터 전투 애니메이션 전체, 건축 모듈 연결부(벽-기둥 이음새), 밤 화면 톤.
