# 오픈월드 맵 (World.unity) — 2026-10-14 (식생 · 물 2026-10-29)

`Game > Setup > 5. Build World Scene` (`Game/Editor/World/WorldBuilder*.cs`) 이 코드로 생성합니다. 씬을 손으로 수정하면 다시 빌드할 때 사라지므로, 변경은 빌더 코드나 CSV 에서 합니다.

## 1. 지형 규격

| 항목 | 값 | 비고 |
|---|---|---|
| 크기 | 512 × 512m, 최대 높이 120m | 이전 256m / 40m 에서 확대 |
| 높이맵 | 513 해상도 | Perlin fbm 언덕 + 고원 + 가장자리 산맥 + 호수 분지 + 평탄화 존 |
| 산맥 | TerrainSampleAssets 의 실측 **Erosion 높이맵**을 GPU Blit/ReadPixels 로 읽어 가장자리에 합성 | 원본 텍스처 임포트 설정은 건드리지 않음 |
| Terrain Layer | TerrainSampleAssets 8종: Grass_A, Grass_Moss, Soil_Rocks, Rock, Grass_Dry, Muddy, Sand, Snow | 경사·고도·노이즈·지역으로 Splat 페인트 (`paintSplat`) |
| Detail (풀) | TerrainSampleAssets 풀/고사리/히스/식물 12종 → **Game/Foliage** 셰이더 프리팹(바람)으로 재생성 (`Art/Generated/World/Detail`), 지역 마스크별 밀도 (7.2) | `DetailScatterMode.InstanceCountMode` (Issues 19) |
| 나무 | **절차 생성 나무 4종 × 변형 12개** (가문비/활엽수/자작/고사목, LOD 2단계) + 덤불 4종, **GameObject** 로 약 3,500그루 + 덤불 약 670 (7.1) | Terrain Tree 가 아닌 이유: 나무마다 카메라 가림 숨김 · 건축 시 제거 |
| 바위 | TheTalesFactory MountainRocks (포토스캔) | 산맥 고리 70개 + 고원 절벽 |
| 물 | **강**(북서쪽 산 → 호수, 지형을 파서 만듦) + **호수**, 직접 만든 URP 물 셰이더 `Game/Water` (7.3) | |

## 2. 지역 (WorldRegions = SpawnTable 좌표)

| 지역 | 중심 (x, z) | 지형 | 내용 |
|---|---|---|---|
| Silverlight Glade (시작) | (256, 96) | 평지, 흙바닥 | 플레이어 시작, 상인 텐트+횃불, 폐허 벽·쉼터(그늘) |
| Meadow | (230, 170) | 완만한 초원 | 덤불 채집, 메두사 2 |
| Ruined Hamlet | (300, 215) | 평탄화 | ASTROFISH 건물(Composite_A/C, House1, RepunTower, OpenAirShelter, StoneLookoutTower), 울타리·통·횃불, 메두사 5 + 코브라 2 |
| Dunley Woods | (130, 230) | 숲 | 소나무/참나무 채집 노드 (그늘) |
| Lake / Serpent Marsh | (170, 262) / (175, 300) | 분지, 수면 4m, 진흙·모래 | 코브라 4, 덤불 |
| River | (46, 482) → (166, 266) | 북서쪽 산에서 호수로 흐르는 강 (반폭 3→6m, 깊이 1.3m, 걸어서 건넘) | 물가 모래·자작나무 |
| Rocky Highlands | (390, 220) | 고원 + 절벽 | 바위/큰 바위 채집 |
| Copper Hollow | (430, 350) | 산기슭 | 구리 광맥 |
| Troll Ridge | (250, 385) | 해발 +9m 고원 평탄화 | 바위·쉼터, 트롤 4 + 메두사 2 |
| Minotaur Arena (V Blood) | (390, 430) | 평탄화 | MountainRocks01_A 고리 + 횃불 기둥, 보스 미노타우로스 |
| 가장자리 | 맵 외곽 | 침식 산맥, 눈 | 이동 경계 |

몬스터·채집 노드·상인은 런타임에 `SpawnTable.csv` 로 생성됩니다 (월드 빌더는 지형·장식·랜드마크만 배치). 시작 시 확인: 몬스터 20, 채집 노드 231.

## 3. 하늘 / 햇빛 (2026-10-30 WeatherMaker 제거 후)

| 항목 | 내용 |
|---|---|
| 배치 | `Sun` Directional Light 1개 (낮 = 해, 밤 = 푸른 달빛) + 절차 스카이박스 `M_Skybox` (`WorldBuilder.Sky`) |
| 시간 | `DayNightManager` 가 주인 — 해 고도 · 방위, 라이트 색 · 세기, 하늘 노출 · 색, 안개, 앰비언트, 반사 세기, 낮/밤 볼륨 weight 를 매 프레임 계산 |
| 태양 궤적 | 일출 ~ 일몰 동안 고도 sin 곡선(최고 65°), 방위 −80° → 260°. 밤에는 달 고도 20° 이상 |
| 햇빛 판정 | `DayNightManager.DirectionToSun` → 화면의 해와 `SunExposure` 레이캐스트 방향이 일치 (Issues 26) |

라이트 · 앰비언트 · 안개 · 볼륨 등 **렌더 설정은 [URP.md 1~3절](URP.md#1-조명--그림자--하늘)** 이 원본입니다.
외부 하늘 에셋을 다시 붙이려면 `ISkyController` 를 구현해 `SkyBridge` 에 등록하면 됩니다 (등록되면 해 방향은 그쪽 값을 씀).

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
- 근접 콤보는 범위 안에 적이 없고 채집 노드가 있으면 그 노드의 채집 애니메이션(`ResourceNodeTable.Anim` → AnimationTable 12~14: Chop/Mine/Gather)을 재생 (`ResourceNode.FindHarvestAnim`).
- 건축 시 1.1초 이동 잠금 + Build 애니메이션 (`ConstTable.BuildAnim`).
- 애니메이션 재생 규칙(AnimationTable)은 [README.md 5절](README.md#5-데이터-테이블-csv--scriptableobject).
- 몬스터의 모델 · 클립 매핑은 `Game/Data/Monsters/*.asset`(MonsterAuthoring) 이 원본이며 그 에셋의 인스펙터에서 편집한다 → [Tool.md 3절](Tool.md#3-monsterauthoring-인스펙터).

## 6. 미검증 / 알려진 문제

지형 풀 미표시(Issues 19), 육안 미확인 지역 목록은 [Status.md 3절](Status.md#3-알려진-문제).

## 7. 식생 · 물 (2026-10-29)

레퍼런스: `Documentation/images/WorldMap/World1.jpg`, `World2.jpg` — 침엽수 숲 덩어리 + 초원의 키 큰 풀 · 마른 금빛 풀 + 드문드문 고사목 + 산자락 침엽수림.

### 7.1 나무 (`Game/Editor/World/Foliage`, 배치 `WorldBuilder.Foliage.cs`)

| 종 | 모양 | 껍질 / 잎 텍스처 | 어디에 |
|---|---|---|---|
| Spruce (가문비) ×4 | 높이 13~19m, 층층이 돌려난 가지마다 바늘잎 스프레이 카드 2장(V 자) | Poly Haven `pine_bark` / 절차 `T_SpruceSpray` | 산자락(가장자리 95m 안), 고지대(30m 이상), 동쪽 |
| Broadleaf (활엽수) ×3 | 9~13.5m, 굽은 줄기 → 큰 가지 6~8 → 잔가지 → 잎 뭉치 카드 | Poly Haven `bark_brown_02` / 절차 `T_BroadleafCluster` | 저지대, Dunley Woods |
| Birch (자작) ×3 | 10~15m, 흰 줄기, 늘어지는 작은 잎 뭉치 | 절차 `T_BirchBark` / `T_BirchCluster` | 강가 · 호숫가 14m 안, 습지 |
| Dead (고사목) ×2 | 7~12m, 잎 없는 꺾인 가지 | Poly Haven `bark_willow_02` | 드물게, 습지 · 동쪽 건조지대 |
| 덤불 ×4 | TerrainSampleAssets Bush_A/B, BushDry_A/B 메시 + Game/Foliage | 원본 텍스처 | 숲 가장자리, 동쪽은 마른 덤불 |

- 생성: `TreeGenerator.Build(종, 시드)` 가 골격(튜브 + 잎 카드)을 만들고 `ToMesh(골격, LOD)` 로 메시화 → LOD0(가문비 약 4.6k 삼각형) / LOD1(튜브 면 절반, 잔가지·덜 중요한 카드 생략). LOD 전환 16% / 컬 1.2%.
- 잎 카드 노멀은 수관 중심 기준 구형 노멀과 섞어 덩어리로 보이게, 정점 색에 바람 가중치 · 떨림 · AO 를 굽는다 (URP.md 8절).
- 배치: 4.2m 격자 + 지터. 밀도 = max(노이즈 숲 덩어리, Dunley Woods, 산자락, 물가) + 3% 단독목. 종 선택은 고도·가장자리·물가·습지·동쪽 가중치. 경사 40° · 고도 84m 초과, 물 위(+3m), 스폰/캠프 평탄화 구역(+3m) 은 비운다.
- 프리팹: `LODGroup` + `FoliageInstance`(건축 충돌 반지름) + `CameraOccluder` + `SunShade` MeshCollider(SunBlocker, 그늘 판정 — Issues 26 과 같은 방식). BatchingStatic 은 끔 (정적 배칭이 개체별 바람 위상을 없앰).
- 채집 나무(PineTree/OakTree 노드)도 같은 절차 나무를 쓴다 (`ContentBuilder.buildTree`).
- 다시 만들기: `Game > World > Rebuild Tree Prefabs` / `Rebuild Foliage Textures` → [Tool.md 4절](Tool.md#4-그-밖의-툴).

### 7.2 풀 (Terrain Detail)

`WorldBuilder.Terrain.cs` `DetailSpecs` — 종마다 지역 마스크 가중치(초원 = Grass_A 레이어, 건조 = Grass_Dry, 숲 = Grass_Moss, 물가 = 강·호수 8m 안)와 노이즈 문턱. 소수부는 확률로 올림.
초원은 Grass_B/C 가 셀당 3~4개로 빽빽, 동쪽은 GrassDry_A/C 금빛 풀, 숲 바닥은 고사리, 물가는 Grass_A · Plant_C. 강·호수 안은 비운다.

### 7.3 강 · 호수 (`WorldBuilder.Water.cs`)

1. 제어점 8개 → Catmull-Rom 3m 간격 → "강을 파기 전" 지형 높이 - 1m 를 따라 **하류로만 내려가는 수면 프로필** (이동 평균 후 단조 감소 재강제). 하구 30m 에서 호수 수면(4m)으로 내려간다.
2. `sampleHeight` 마지막에 `carveRiver`: 강 안은 수면 - 1.3m 포물선 바닥, 강둑 10m 는 높으면 깎고(계곡) 낮으면 돋움(제방). 호수 근처는 깎기만.
3. 수면 리본 메시(가로 4칸): 정점 색 = 흐름 방향 · 속도(경사 비례) · 급류 거품. 호수 수면 원(반지름 33m)과 겹치기 직전에서 끝난다.
4. 스플랫: 강바닥 Soil_Rocks, 물가 모래 · 진흙.
5. `WaterBody` 컴포넌트(강 = 중심선 + 반폭, 호수 = 중심 + 반지름) → `SpawnManager` 가 물속 스폰 지점을 최대 8번 다시 뽑는다 (여유 1.5m).

### 7.4 건축하면 나무 · 풀 제거

`BuildManager.place` → `StructurePlacedEvent { Structure, Bounds }` (렌더러 바운드 ∪ StructureTable.Footprint, 회전 반영) → `FoliageManager`:
- `FoliageRoot.ClearArea` — 16m 격자 색인에서 밑동 원(반지름 × 스케일)이 바운드(+0.4m)와 겹치는 나무/덤불을 비활성화.
- `TerrainUtil.ClearDetails` — 바운드 안 모든 Detail 레이어를 0.
- 시작할 때 `TerrainUtil.UseRuntimeCopy` 로 TerrainData 를 복제해 편집 → Play 모드에서도 원본 에셋(`TerrainData.asset`)이 바뀌지 않는다. 종료 시 원본 복구.
- 채집 노드(ResourceNode)는 지우지 않는다 (게임 오브젝트이며 채집 대상).

카메라 가림 숨김은 [CameraOcclusion.md](CameraOcclusion.md).
