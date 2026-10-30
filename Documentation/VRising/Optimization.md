# 진행 상태 요약 + 최적화 가이드 (2026-10-29)

최적화를 직접 진행할 때 **어디를 어떤 툴로 재면 되는지** 정리한 문서입니다. 이 문서가 "측정 방법 · 툴 사용법" 의 원본입니다.

- 기능 상태 · 알려진 문제의 원본은 [Status.md](Status.md)이고, 아래 1절은 그 요약입니다.
- 의심 지점 목록의 원본은 [Issues.md 18](Issues.md#18-성능-관찰-포인트-최적화는-직접-진행-예정), 이미 잰 Batches 분석은 [Issues.md 34](Issues.md#34-batches-700900--원인-분해-측정)입니다.
- Frame Debugger 화면 깜빡임은 [Issues.md 37](Issues.md#37-frame-debugger-를-켜면-화면이-깜빡이고-draw-event-가-생겼다-사라짐)에 있습니다.

---

## 1. 지금까지의 진행 상태 (요약)

| 영역 | 상태 | 비고 |
|---|---|---|
| 오프라인 1인 플레이 | ✅ | 타이틀 → 싱글 플레이 |
| 전투 (GAS) · 상태이상 · 흡혈/혈액 · V Blood 보스 | ✅ | [GAS.md](GAS.md), [Game.md](Game.md) |
| 몬스터 AI (StateTree) | ✅ | 근접 · 원거리 · 보스 2페이즈 ([StateTree.md](StateTree.md)) |
| 에디터 툴 (GAS Tool · StateTree Tool · MonsterAuthoring · Balance · UI Factory) | ✅ | [Tool.md](Tool.md) |
| 512m 오픈월드 + 하늘/낮밤 (URP 기본, 2026-10-30 WeatherMaker 제거) | ✅ | [World.md](World.md), [URP.md](URP.md) |
| **나무 · 풀 · 강/호수 물 셰이더** (10-29) | ✅ | 절차 나무 약 3,500 + 덤불 670, 풀 12종, `Game/Foliage` · `Game/Water` |
| **건축 시 나무 · 풀 제거** (10-29) | ✅ | `StructurePlacedEvent` → `FoliageManager` |
| **카메라 가림 숨김** (10-29) | ✅ | [CameraOcclusion.md](CameraOcclusion.md) |
| 채집 · 제작 · 건축 · 상점 | ✅ | 제작 즉시 완료 |
| Play 모드 자동 테스트 | ✅ | 17개 전부 통과 (2026-10-30, WeatherMaker 제거 후) |
| 4인 협동 | ❌ | 설계만 ([Multiplayer.md](Multiplayer.md)) |
| 세이브/로드 · 사운드 · 한글 UI | ❌ | |
| **최적화 / 프로파일링** | ⏳ 사용자 진행 예정 | 이 문서 |

---

## 2. 툴 한눈에 보기 — 무엇을 알고 싶을 때 무엇을 여나

| 알고 싶은 것 | 툴 | 여는 곳 | 이 프로젝트에서 볼 것 |
|---|---|---|---|
| 한 프레임이 **CPU 에서 어디에 시간을 쓰나** | **Profiler** — CPU Usage (Timeline / Hierarchy) | Window > Analysis > Profiler | `PlayerLoop > Update.ScriptRunBehaviourUpdate`(GameBootstrap → Managers.Update), `PreLateUpdate`(TopDownCamera, CameraOcclusionSystem), `Culling`(LODGroup 3,500개), `Physics.Processing`, `Terrain.Details` |
| **GC 할당** (끊김의 흔한 원인) | Profiler — Hierarchy 의 **GC Alloc** 열, Call Stacks 켜기 | Profiler 상단 Call Stacks | HudWindow 문자열, GameplayEffectSpec 생성 (Issues 18) |
| **GPU 시간** | Profiler — GPU Usage 모듈 (지원 플랫폼), 더 정확히는 **RenderDoc** / PIX / Nsight | GPU 모듈 추가 / Game 뷰 우클릭 "Load RenderDoc" | 포스트 프로세싱(보케 DoF · 모션 블러 · 렌즈 플레어) · 대기 패스, 그림자, 물(굴절), 풀 Overdraw |
| **어떤 Draw Call 이 어떤 순서로 그려지나**, 배칭이 왜 끊기나 | **Frame Debugger** | Window > Analysis > Frame Debugger | 이벤트를 누르면 "Why this draw call can't be batched" 표시. 그림자 패스 크기, 나무 2 머티리얼 × LOD, 물 패스 위치 |
| URP **패스 구성 · 렌더 타깃 · 컬링된 패스** | **Render Graph Viewer** (URP 17) | Window > Analysis > Render Graph Viewer | `Copy Color`(물 굴절용 Opaque Texture), `Copy Depth`, `Atmosphere (Game)` 풀스크린 패스, 포스트 프로세싱 패스, Main/Additional Light Shadow |
| **Overdraw · 그림자 캐스케이드 · 라이트 수** 시각화 | **Rendering Debugger** | Window > Analysis > Rendering Debugger | Rendering > Overdraw(풀 · 잎 알파클립), Lighting > Shadow Cascades, Map Overlays > Additional Lights Shadow Map |
| Batches / SetPass / 삼각형 수 즉석 확인 | Game 뷰 **Stats** / `UnityEditor.UnityStats` | Game 뷰 상단 Stats | Issues 34 처럼 항목을 하나씩 끄며 차이 측정 |
| **메모리** (텍스처 · 메시 · 콜라이더) | **Memory Profiler** 패키지 (미설치 → Package Manager 에서 `com.unity.memoryprofiler`) | Window > Analysis > Memory Profiler | 절차 나무 메시 + SunShade MeshCollider(잎 카드 포함), 4K 포토스캔 바위 텍스처, 씬 18MB |
| 두 측정 결과 **비교** (전/후) | **Profile Analyzer** 패키지 (`com.unity.performance.profile-analyzer`) | Window > Analysis > Profile Analyzer | 바꾸기 전/후 .data 파일을 Compare |
| 물리 비용 · 콜라이더 분포 | Profiler Physics 모듈 + **Physics Debugger** | Window > Analysis > Physics Debugger | SunShade MeshCollider 수천 개, 매 프레임 레이캐스트(SunExposure, CameraOcclusion, Aim) |
| 반복 가능한 수치 회귀 측정 | **Performance Testing** 패키지 (이미 로드됨 — 테스트 러너 로그에 `Unity.PerformanceTesting`) | PlayMode 테스트에 `Measure.Frames()` | 고정 위치 · 고정 시각에서 프레임 시간 측정 테스트 |
| 프로젝트 전반 정적 점검 | Project Auditor 패키지 (`com.unity.project-auditor`) | Window > Analysis > Project Auditor | 텍스처 임포트 설정, 셰이더 변형 수, 코드 할당 패턴 |

### 측정할 때 지킬 것

1. **에디터가 아니라 Development Build** 에서 재기 (`Autoconnect Profiler` + `Deep Profiling` 끔). 에디터 측정에는 Scene 뷰 렌더 · 인스펙터가 섞인다. 빌드 전 `Game > Build > Build Addressables Content` 필요 (README 1절).
2. **고정된 측정 지점** 을 정해 같은 곳에서 전/후 비교:

   | 지점 | 좌표 (x, z) | 이유 |
   |---|---|---|
   | 시작 지점 | (256, 96) | 기본 화면, 상인 · 횃불 |
   | 폐허 마을 | (300, 205) | Issues 34 기준 지점 (랜드마크 메시 많음) |
   | Dunley Woods 숲 | (130, 230) | 나무 밀도 최대 + 채집 나무 |
   | 강가 | (122, 300) | 물 셰이더 + 물가 풀 + 자작나무 |
   | 미노타우로스 아레나 | (390, 430) | 보스 전투 (VFX · 스키닝) |

   시간은 `DayNightManager.SetHour(13)` + `Frozen = true` 로 고정 (그림자 방향이 바뀌면 숫자도 바뀜). 치트 콘솔 텔레포트를 쓰면 편하다 ([Game.md 14절](Game.md#14-치트--콘솔)).
3. **한 번에 하나만** 바꾸고 Profile Analyzer 로 비교 → 결과는 Issues.md 에 새 번호로 남기기 (Issues 34 형식의 표).
4. 직접 만든 코드 구간에는 `Unity.Profiling.ProfilerMarker` 를 붙이면 Profiler 에서 이름으로 보인다 (후보: `CameraOcclusionSystem.LateUpdate`, `FoliageManager.ClearArea`, `SpawnManager.SpawnWorldAsync`, StateTree Tick).

---

## 3. 의심 지점별 힌트

의심 지점 목록 자체는 Issues 18 이 원본입니다. 여기는 **"무엇으로 확인하고, 어떤 방향을 볼지"** 만 적습니다 (아직 아무것도 고치지 않았습니다).

### 3.1 렌더링 (GPU · Draw Call)

| 지점 | 확인 방법 | 볼 방향 (후보) |
|---|---|---|
| 그림자 패스가 Batches 의 절반 (Issues 34) | Frame Debugger 에서 `MainLightShadow` 아래 이벤트 수, Rendering Debugger Shadow Cascades | 그림자 거리 90m · 캐스케이드 2 (쿼터뷰라 줄일 여지 큼), 작은 소품 · 덤불 그림자 끄기 |
| 장식 나무 3,500 GameObject (10-29) | Profiler `Culling` / `LODGroup`, Stats Batches, Frame Debugger 의 나무 Draw 수 | **GPU Resident Drawer** (URP 에셋에서 현재 꺼짐, Unity 6 기능 — 같은 메시·머티리얼을 자동 인스턴싱 + GPU 오클루전 컬링), LOD 전환 거리, 그림자 캐스터를 LOD1 로 |
| 풀 Detail 12종 | Rendering Debugger Overdraw, Profiler `Terrain.Details` | `detailObjectDistance`(70) · `detailObjectDensity`(0.8), 종 수, 셀당 개수 |
| 횃불 Point Light 그림자 (콘솔: "Reduced additional punctual light shadows resolution … 6 / 12 shadow maps") | Rendering Debugger Map Overlays > Additional Lights Shadow Map | 횃불 18개가 그림자 사용 → 그림자 끄기 또는 Additional Shadow 해상도/아틀라스 크기 |
| 물 셰이더 | Render Graph Viewer `Copy Color` | Opaque Texture 복사는 물이 화면에 없어도 매 프레임 일어남 → 다운샘플 옵션 |
| 포스트 프로세싱 · 대기 패스 (2026-10-30) | Frame Debugger / Render Graph Viewer 의 `Atmosphere (Game)`, `Bokeh Depth Of Field`, `Motion Blur`, `Lens Flare` 패스 | 볼륨 효과를 하나씩 꺼서 비교 (`VP_Day`/`VP_Focus`), DoF 를 Gaussian 으로, 렌즈 플레어 해상도 |
| 랜드마크 MeshRenderer 1,427개 (Issues 34) | Stats, Frame Debugger 배칭 끊김 이유 | 메시 결합, LOD, Static 플래그 정리 |
| SSAO · 후처리 | Render Graph Viewer, GPU 모듈 | SSAO Downsample |

### 3.2 CPU · 스크립트

| 지점 | 확인 방법 | 볼 방향 |
|---|---|---|
| `ActorRegistry.FindHostiles`, `FeedAbility.HasVictimInRange`, `InteractableRegistry.FindNearest`, `WorldHealthBarLayer.LateUpdate` (매 프레임 전 액터 순회) | Profiler Hierarchy (몬스터 수를 치트로 늘려 비교) | 공간 분할 그리드 (FoliageRoot 가 쓰는 16m 격자 방식 참고) |
| StateTree 몬스터마다 매 프레임 Tick | ProfilerMarker 추가 후 Hierarchy | Tick 주기 분산, 원거리 몬스터 LOD |
| GC: HudWindow 문자열, GameplayEffectSpec, `ToArray` | GC Alloc 열 + Call Stacks | 풀링, 값이 바뀔 때만 SetText |
| 물리: SunShade MeshCollider 수천 개, 매 프레임 레이캐스트 | Profiler Physics, Physics Debugger | 그늘 콜라이더를 LOD1 메시 또는 단순 형태로, 레이캐스트 주기 |
| 스키닝 (몬스터 20 + 플레이어) | Profiler Animation 모듈 | Animator Culling Mode (현재 CullUpdateTransforms — Issues 35 측정 함정 주의) |
| NavMeshObstacle carving (채집 노드 231개) | Profiler `NavMesh` | carve 대신 베이크 시 제외 |

### 3.3 로딩 · 메모리

| 지점 | 확인 방법 | 볼 방향 |
|---|---|---|
| World.unity 18MB (나무 프리팹 인스턴스 4,200개) | Profiler 로딩 마커, Memory Profiler | 나무 배치를 데이터(위치 배열) + 런타임 인스턴싱으로 |
| 4K 포토스캔 바위 텍스처 | Memory Profiler > Textures | 최대 크기 · 압축 |
| `TerrainUtil.UseRuntimeCopy` (시작 시 TerrainData 복제) | Profiler 시작 구간 | 첫 건축 때 지연 복제 |

---

## 4. 권장 순서 (참고)

1. Development Build + Profiler 로 **기준값** 을 5개 지점에서 기록 (CPU ms, GPU ms, Batches, SetPass, GC Alloc/frame).
2. 가장 큰 것부터: 보통 이 씬은 **그림자 → 나무/랜드마크 드로우 → 포스트 프로세싱** 순으로 클 가능성이 높다 (Issues 34 에서 그림자 끄기가 −441 Batches).
3. 바꿀 때마다 같은 지점 재측정 → Profile Analyzer 비교 → Issues.md 에 표로 기록.
