# 개발 기록 (BLOODLINE)

"언제 무엇을 왜 바꿨는가" 를 시간 순으로 남깁니다. 현재 상태는 [Status.md](Status.md), 문제와 해결의 상세는 [Issues.md](Issues.md).
이전 프로젝트(Netcode 덱/던전 RPG)의 기록은 [../Legacy/CHANGELOG.md](../Legacy/CHANGELOG.md).

---

## 2026-10-31 · 한국어 (Unity Localization)

사용자 요청: "영어를 한글로 Localization — Unity Localization 기능을 사용해서 번역". 상세 [UI.md 언어 절](UI.md#언어-unity-localization-2026-10-31).

- 패키지 `com.unity.localization` 1.5.13 추가. Game.Runtime / Game.Editor asmdef 참조.
- `StringTable.csv` 에 `Korean` 열 — 200개 문구 전부 번역 (게임 용어: 흡혈, V 블러드, 성 심장, 피의 장막 …). 새 문구: 언어 버튼(62), 인벤토리 능력치 이름(9000 + EAttribute, 전에는 enum 이름이 그대로 보였음), 조작 안내에 M 월드맵.
- `LocalizationSync` (에디터): Localization Settings · Locale(en, ko, ko→en Fallback) · String Table Collection **GameText**(키 Id = Index, Preload) 생성/갱신, 시작 언어 = PlayerPrefs → 한국어. CSV 임포트 · StringTable.csv 저장 때 자동. `GameTables.TextTable` 에 GUID 참조.
- `GameLocalization` (런타임): 부트스트랩이 초기화를 기다린 뒤 UI 생성, `Tables.Text` 가 현재 언어 테이블에서 int 키로 조회 (없으면 CSV 영어). 언어 변경 → `LanguageChangedEvent` → `UIText` · HUD · 월드맵이 다시 채움. 타이틀 언어 버튼, 치트 `lang`.
- 폰트: 기본 LiberationSans 에 한글이 없어 Noto Sans KR(OFL, `Art/ThirdParty/Fonts`) → TMP 동적 폰트 에셋을 UI 스킨 기본 폰트 + TMP 전역 Fallback 으로. UI 프리팹 다시 구움.
- 확인: 타이틀 · HUD · 인벤토리 한국어 캡처, 게임 도중 영어로 바꾸면 바로 바뀜. `LocalizationTests` 2개 추가, Play 모드 테스트 25개 전부 통과.

## 2026-10-31 · 퀘스트 가이드 · 루트 모션 정리

사용자 요청: "미니맵과 맵에 어디로 가야 퀘스트를 진행할 수 있는지 가이드 표시", "메두사 Attack1 에 쓰이는 애니메이션 외에는 ApplyRootMotion 을 전부 false".

- **퀘스트 가이드** ([Game.md 2.1](Game.md#21-퀘스트)): `QuestManager.TryGetGuidePosition` — 처치 · 흡혈 = 가장 가까운 대상(없으면 SpawnTable 지역), 상점 = 상인, 채집 = 채집 지역, 성 건축물 = 성 심장. `MapView` 가 금색 고리 + 플레이어→목적지 선을 그리고, 미니맵 밖이면 가장자리 화살표가 목적지를 가리킨다. HUD 추적기 거리도 같은 위치 기준 (몬스터 목표만 → 가이드가 있는 모든 퀘스트, 문구 Nearest → Distance).
- **루트 모션**: AnimationTable 1 · 2 · 7(Attack1/2/3) 의 ApplyRootMotion 을 0 으로, 메두사 평타(Ability 501 Club)용 행 15(Trigger Attack1, 루트 모션 1)를 새로 만들어 501 의 Anims 를 15 로. 이제 루트 모션이 켜지는 건 행 15 하나. `ActorAnimator.StopRootMotion` — Play 는 행마다 값을 덮어쓰지만 이동 블렌드는 Play 를 거치지 않아 공격 뒤에도 켜진 채 남던 문제 → 어빌리티가 끝나거나 죽으면 끈다 (`Actor.OnAbilityEnded`). 행 15 를 쓰는 어빌리티 501 은 CharacterTable 7(New Monster, 메두사 복제)도 쓰고 있어 그 몬스터도 루트 모션이 켜진다.
- 테스트: `MapTests.Minimap_ShowsQuestGuide`, `RootMotionTests` 2개. Play 모드 테스트 23개 전부 통과.

## 2026-10-31 · 미니맵 · 월드맵 · 입력 맵 분리

사용자 요청: "맵과 미니맵 — 미니맵은 맵의 일부분을 오른쪽 위에, 맵은 M 키로 월드맵. UI 에 들어가면 카메라 확대/축소 스크롤이 작동하지 않도록 전투 InputMap 을 비활성화". 상세 [Game.md 13.1](Game.md#131-미니맵--월드맵), [UI.md](UI.md#지도-mapview--입력-맵-2026-10-31), [World.md 8절](World.md#8-월드맵-그림).

- **입력 맵**: `InputTable.csv` 에 `Map` 열(System / Movement / Combat) + `WorldMap`(M). `GameInput` 이 맵마다 InputActionMap 을 만들고 `SetMapEnabled` 로 켜고 끈다. `GameBootstrap.syncInputMaps` — 입력을 막는 창이 열려 있거나 콘솔 입력 중이면 Combat(어빌리티 · 상호작용 · 휠 줌 · 카메라 회전 · 건축 회전) 끔. 이동은 창이 열려 있어도 유지(기존 동작).
- **지도 그림**: `WorldMapBaker` 가 정사영 카메라로 월드를 내려다본 2048² 텍스처를 굽는다 (월드 빌드 마지막 + `Game > World > Bake World Map`). 처음엔 물과 호수가 안 보였음 — 물 셰이더가 이 카메라에서 그려지지 않고, 대기 풀스크린 패스가 낮은 곳을 안개로 덮음 → 굽는 동안 물은 단색 Unlit(양면), 풀스크린 패스는 끔.
- **MapView** (미니맵 · 월드맵 공용): 미니맵 = 반경 45m(ConstTable 36), 카메라 방향이 위(쿼터뷰 45° 회전과 화면 방향 일치), 주변 몬스터 · 퀘스트 대상(밖이면 가장자리). 월드맵 = 전체 · 북쪽 위 · 지역 이름(`MapLabelTable.csv`, 21번째 테이블). 상인 · 성 심장 · 관은 `MapMarkers` 에 등록. 표식 스프라이트는 프리팹 빌더가 절차 생성.
- HUD: 오른쪽 위에 미니맵이 들어가 시계 · 퀘스트 추적기를 아래로 내림. `EUIAsset` 415 MinimapWindow, 416 WorldMapWindow.
- 테스트 `MapTests` 2개 (월드맵 → Combat 꺼짐 · 이동 유지 · 줌 값 0 · 미니맵 숨김 / 미니맵 가운데 = 플레이어, 카메라 앞 = 위, 몬스터 표식). Play 모드 테스트 20개 전부 통과.

## 2026-10-31 · 퀘스트 시스템 (튜토리얼 → 사냥 → 보스)

사용자 요청: "게임의 목표가 너무 없어 보인다 — 처음엔 기초 기능을 한 번씩 둘러보는 튜토리얼, 그다음 몬스터를 처치해 가는 퀘스트, 마지막엔 보스를 죽이는 것으로 끝나게". 상세 [Game.md 2.1절](Game.md#21-퀘스트).

- **데이터** `QuestTable.csv` (20번째 테이블): 한 줄 = 목표 하나(`EQuestObjective`: Move · UseAbility · Buy · Gather · Craft · Equip · Build · Kill · Feed · GearLevel), `Next` 로 체인, 보상 아이템, HUD 에 붙일 키(`Key`). 17개 퀘스트 = 튜토리얼 11 + 사냥 5 + 보스 1. 첫 퀘스트 ConstTable 35 `FirstQuestId`, 문구 StringTable 50~59 · 7001~7117. `Validate Tables` 가 문구 · 대상 · 보상 · Next · 체인 고리를 검사.
- **QuestManager** (`Game/Runtime/Quest`, IManager): 첫 플레이어 스폰 때 시작, EventBus 구독으로 현재 목표만 셈. 새 이벤트 `ItemObtainedEvent`(채집 · 제작 · 상점 · 보상 구분), `PlayerAbilityUsedEvent`, `PlayerFedEvent`, `QuestChangedEvent`, `QuestsCompletedEvent` — 발행 지점은 ResourceNode · CraftWindow · ShopWindow · PlayerCharacter.OnAbilityActivated · BloodSystem.Consume.
- **HUD**: 시계 아래 퀘스트 추적기(묶음 · [키] 제목 · 설명 · 보상 · 진행도 · 가장 가까운 대상 거리), 모든 퀘스트 완료 시 클리어 오버레이 10초. UIPrefabBuilder 로 HudWindow 프리팹 다시 구움.
- **치트** `quest` (현재 완료) / `quest <id>` (점프).
- **테스트** `QuestTests.Scenario_QuestChain`: 1→17 전체 체인 + 클리어 이벤트 · 오버레이 · V Blood 해금. 건축 퀘스트 2개는 성 짓기 테스트와 성 심장 1개 제한이 겹쳐 치트 완료로 넘김. Play 모드 테스트 18개 전부 통과.

## 2026-10-30 · WeatherMaker 제거 · 포스트 프로세싱 · 캐릭터 셰이더

사용자 요청: "URP 를 쓴 느낌이 안 난다 — 포스트 프로세싱으로 시각 효과를 많이 주고 캐릭터 · 몬스터 퀄리티를 높이고, 연산에 비해 효과가 작은 WeatherMaker 를 빼 달라". 상세 [URP.md 1 · 3 · 10 · 11절](URP.md).

- **WeatherMaker 제거**: `ExternalAssets/WeatherMaker`(321MB), 어댑터 `Game/Integration/WeatherMaker`, 렌더러 피처, 디파인 `WEATHER_MAKER_PRESENT` · `UNITY_URP`, 테스트의 `LogAssert.Expect` 를 지움. `ISkyController` / `SkyBridge` 확장점은 남김.
- **하늘 · 낮밤 (URP 기본)**: 절차 스카이박스 + 지수 안개 + 빌드 때 구운 하늘 반사 큐브맵. `DayNightManager` 가 라이트 · 하늘 노출 · 안개 · 앰비언트 · 반사 세기를 시간대별로 바꿈.
- **포스트 프로세싱** (`WorldBuilder.PostFx`): 낮/밤 볼륨에 렌즈 플레어 · 화이트밸런스 · 스플릿 토닝 · SMH · LGG · 필름 그레인 · 색수차 · 모션 블러, 보케 피사계 심도(초점 = 플레이어), 게임 상태 볼륨(햇빛 화상 / 저체력 맥동 / 사망 흑백 — `ScreenEffects`). URP 에셋 HDR 그레이딩, SSAO 강화, SMAA High.
- **대기 풀스크린 패스** `Game/PostFx/Atmosphere`: 높이 안개 + 태양 산란, 풀스크린 1패스.
- **캐릭터 셰이더** `Game/Character` + `CharacterLook`: 플레이어 툰 셰이딩 복원(unity-chan), 몬스터 PBR + 보조광 + 림라이트 + 외곽선, 피격 플래시(`CharacterHitFlash`).
  - 금속이 검게 보여 실시간 반사 프로브를 넣었더니 캐릭터가 청록색 실루엣으로 깨짐 → 구운 하늘 큐브맵으로 대체 ([Issues 38](Issues.md#38-실시간-반사-프로브를-켜면-캐릭터가-청록색-실루엣으로-깨짐)).
- **Play 화면으로 조정** ([Issues 39](Issues.md)): 처음 값으로는 그늘이 새까맣고 풀밭이 누런 올리브색, 햇빛 화상 효과가 화면 전체를 주황으로 덮고 밤이 뿌옇게 떴음 → 앰비언트 3색 보간, 그레이딩 · Sunburn · Night 값 조정 후 캡처로 확인.

## 2026-10-29~30 · UI 프리팹 전환 · UI 스킨 · Addressables 의존성 툴

- **UI 프리팹 + Addressables** ([UI.md](UI.md)): 창이 런타임 `Build()` 에서 자식을 만들고 필드에 연결하던 방식을 없앰. `UIPrefabBuilder`(Game > Setup > 3b, Run All 포함) 가 캔버스 · 창 8 · 레이어 2 · 위젯 4 프리팹을 `Content/UI` 에 굽고(직렬화 참조, `UIText` TextId 문구, 영구 버튼 리스너), `GameBootstrap.setupUIAsync` 가 `EUIAsset`(AssetTable 400~414) 주소로 로드해 Instantiate. 동적 행 · 슬롯 · 체력바 · 데미지 숫자는 위젯 프리팹 복제 (`UIItemList<T>`). `UIFactory` 는 Framework.Editor 로 이동. UI Factory 툴을 프리팹 방식으로 다시 씀.
- **UI 스킨**: My Assets 에 UI 팩이 없어 Kenney "UI Pack: RPG Expansion"(CC0) 을 받아 흑백 + 대비 늘리기 → 검붉은 틴트로 고딕 톤 (`UISkin`). 처음엔 밝은 베이지 원본이라 틴트 후 리벳 · 테두리가 사라져 밋밋 → 갈색 원본 + 밝기 범위 0.42~1 재배치로 철판 테두리를 살림.
- **Addressables 의존성 툴** ([Dependency.md](Dependency.md)): `AssetTracker`(AssetLoader 로드 · 참조 · 반납 · 동기 대기 + 호출 코드 위치), 정적 분석기(그래프 · 7가지 정적 이슈), 플레이 전 자동 검사, 의존성 지도 창(한눈에 보기 / 그래프 / 이슈 / 테스트 기록 / 실시간), 이슈 이력(새 · 계속 · 해결) + 자동 보고서 [DependencyIssues.md](DependencyIssues.md).
- **의존성 테스트** `DependencyScenarioTests` 5개 (Category `Dependency`): 3회 반복 플레이 → `DependencyReports/Runs/*.json` → 테스트가 끝나면 이슈 자동 갱신 (TestRunnerApi 콜백).
  첫 결과: 로드 코드 전체에 Release 호출이 없어 반복마다 참조가 쌓임 (몬스터 프리팹 · AI 트리 · 아이콘 · 스킬 VFX · 건축 고스트), TMP 기본 폰트가 Resources 와 번들에 2벌, 플레이어 프리팹 동기 로드 41ms. 수정하지 않고 이슈로 기록.
  처음엔 "누가 불렀나" 에 시나리오 이름만 남아 고칠 곳을 찾기 어려웠음 → 소유자 없는 로드는 호출 코드 위치(예: `SpawnManager.spawnMonsterAsync`)를 기록하도록 수정 (에디터 · 개발 빌드만).
- 테스트 베이스: WeatherMaker 품질 설정 오류 로그 한 줄을 `LogAssert.Expect` 로 처리 → 첫 테스트가 늘 실패하던 문제 해결. Play 모드 테스트 17개 전부 통과.

## 2026-10-29 · 최적화 가이드 문서 + Frame Debugger 깜빡임 조사

- **[Optimization.md](Optimization.md)** 추가 — 진행 상태 요약, 툴별 용도(Profiler · Frame Debugger · Render Graph Viewer · Rendering Debugger · Memory Profiler · Profile Analyzer · Physics Debugger · Performance Testing), 고정 측정 지점 5곳, Issues 18 의심 지점별 확인 방법.
- Frame Debugger 깜빡임 → [Issues 37](Issues.md#37-frame-debugger-를-켜면-화면이-깜빡이고-draw-event-가-생겼다-사라짐). MCP 로는 Frame Debugger 이벤트 목록을 읽지 못해(보조 모니터 창 · 포커스 없음) 코드 분석으로 후보만 좁힘. 수정 없음.

## 2026-10-29 · 절차 나무 · 풀 · 강/물 셰이더 · 건축 시 식생 제거 · 카메라 가림 숨김

레퍼런스 `Documentation/images/WorldMap/World1·2.jpg` 를 보고 오픈월드 식생과 물을 만듦. 상세 [World.md 7절](World.md#7-식생--물-2026-10-29), [URP.md 8·9절](URP.md), [CameraOcclusion.md](CameraOcclusion.md).

- **절차 나무** (`Game/Editor/World/Foliage`): 가문비 · 활엽수 · 자작 · 고사목 4종 × 변형 12개, 골격 → LOD0/LOD1 메시, 잎/바늘잎/자작 껍질 텍스처는 CPU 로 그림, 껍질 3종은 Poly Haven CC0 다운로드. Kenney 로우폴리 나무(Terrain Tree), `ShadeColliderUtil.BuildTreeShade` 호출, `ContentBuilder.GetRecoloredTree` 제거 → 장식 나무 약 3,500 + 덤불 670 GameObject, 채집 나무 노드도 같은 나무.
  - 처음 생성했을 때 활엽수 · 자작 수관이 성겨서 가지 · 잎 뭉치 수와 카드 크기를 늘림. 메시를 `CopySerialized` 로 덮어쓰니 서브메시가 옛 범위로 남아 잎에 껍질이 칠해짐 → 데이터를 직접 복사하도록 수정.
- **풀**: Detail 7종 → 12종, 지역 마스크(초원/건조/숲/물가)별 밀도, 원본 HDRP 셰이더 대신 `Game/Foliage`(바람).
- **셰이더** (`Assets/Game/Art/Shaders`): `Game/Foliage`(PBR + 잎 반투과 + 바람, 4패스), `Game/Water`(깊이 색 · 굴절 · 2위상 플로우 · 프레넬 · 거품). URP Depth/Opaque Texture 를 `configurePipeline` 이 켬.
- **강**: 북서쪽 산 → 호수. 수면 프로필(하류로만 하강) → 지형 파기 → 리본 메시. 첫 빌드에서 하구 수면이 6.8m 로 남아 호수(4m)와 끊김 → 하구 30m 에서 호수 수면으로 내려가게, 호수 수면 원을 실제 물가(33m)로 축소. 옛 URP Lit 호수 평면 제거.
- `WaterBody` + `SpawnManager` 물속 스폰 재추첨.
- **건축 시 제거**: `StructurePlacedEvent`(렌더러 바운드 ∪ Footprint) → `FoliageManager` → `FoliageRoot.ClearArea` + `TerrainUtil.ClearDetails`, TerrainData 는 런타임 복제본. 처음엔 렌더러 바운드만 써서 성 심장(모델 0.9m)이 3×3m 칸의 풀을 못 지움 → Footprint 합침.
- **카메라 가림**: Framework `CameraOcclusionSystem` (감지 · 응답 Strategy), 기본 = SphereCast + 카메라 OverlapSphere → ShadowsOnly. 나무 · 랜드마크 · 건축물 · 채집 나무에 `CameraOccluder`.
- 테스트 `WorldFoliageTests` 3개 추가 → 전체 12개 중 11개 통과. 실패 1개는 첫 테스트가 WeatherMaker 품질 설정 오류 로그에 걸리는 기존 문제 (Status.md 3절). 건축 경로는 Play 모드에서 실제 `BuildManager.PlaceAt`(성 심장을 나무 위에)로도 확인.

## 2026-10-28 · 성 짓기 시나리오 테스트 확장

- `Scenario_BuildCastle` 을 완전한 성 짓기로 확장 (`PlayScenarioTests.Castle.cs`): 시작 지점 남서쪽 평지(높이 차 0.7m)의 채집 노드를 치우고
  성 심장 → 바닥 5×5 (가운데부터) → 외벽 20 (남쪽 문 벽, 북·동·서 창문 벽) → 외곽 기둥 20 → 작업대 · 제련소 · 관 · 램프 4, 총 73개.
  설치 성공, 개수, 재료 차감(StructureTable 비용 합과 비교), 관 리스폰 지점, 잘못된 설치 거부를 확인한 뒤 **완성된 성을 10초간 보여준다**
  (15시로 바꾸고 HUD 숨김, 카메라가 성 가운데를 기준으로 한 바퀴, 1/4·3/4 지점 스크린샷 `Temp/CastleShowcase_*.png`, 끝나면 모두 원복).
- 런타임 API: `BuildManager.SetRotation`(벽 방향), `TopDownCamera.SetView` / `ResetView`(연출 시점).
- 첫 실행 실패: 북서쪽 바깥 모서리 기둥이 설치 거리 12m 경계(11.7m)에 있었고 플레이어가 지은 건물 콜라이더에 밀려 범위를 벗어남 → 설치할 때마다 지점에서 성 안쪽 3m 에 서도록 수정.
  처음엔 18시(어둠) · HUD · 실패 알림이 화면을 가려 시간 15시, HUD 숨김, 실패 확인을 앞으로 옮김. 전체 9개 통과.

## 2026-10-28 · 게임 설명서 + 새 Ability 창

- **[Game.md](Game.md)** 추가 — 게임 규칙 · 콘텐츠의 원본 문서 (흐름, 조작, 플레이어, 어빌리티 · 피해 계산 · 상태이상, 흡혈 · 혈액 · V Blood, 낮밤 · 햇빛, 몬스터 · 보스 · 드랍, 채집 · 제작 · 장비, 건축, 상점, 지역, HUD, 치트). 수치는 CSV 에서 옮겨 적음.
  README 의 조작 · 게임 루프 · 치트 목록은 Game.md 로 옮기고 링크만 남김.
- GAS Tool: 새 Ability 를 목록 아래 작은 폼 대신 **별도 창**(`CreateAbilityWindow`)에서 만든다 — 템플릿 선택 → 미리보기(슬롯 · 쿨다운 · AI 거리 · 태그 · 단계) → 이름(자동 제안) → 만들기. 툴바에도 `+ 새 Ability`.

## 2026-10-28 · GAS Tool / StateTree Tool / 몬스터 인스펙터 + Play 모드 테스트

언리얼과 같은 분담으로 정리: **GAS Tool = 어빌리티**, **StateTree Tool = AI**, **MonsterAuthoring 인스펙터 = 몬스터 조립(Character Blueprint)**. 상세 [Tool.md](Tool.md).
(10-27 의 Monster Tool → 10-28 오전 "어빌리티/애니메이션 편집 + 테스트 탭" → "StateTree 몬스터 탭" 시도를 거쳐 이 구조로 확정. 중간 단계 코드는 삭제.)

- **그래프 어빌리티** (`EAbilityType.Graph`, `AbilityRow.Graph`): `AbilityGraphAsset`(태그 규칙 5종 · BloodPool 비용 · 단계 목록) + `AbilityStep` 13종 + `GraphAbility`. `GameAbility.OnSetup` 훅. 기존 타입 기반 어빌리티는 그대로. 태그 `State_Buff_Guard`(77) 추가.
- **GAS Tool**: Ability(템플릿 7종, Details 순서 편집, 타임라인) / GameplayEffect(프리셋 5종) / Ability Set(캐릭터별 보유 어빌리티).
- **StateTree Tool**: Framework `StateTreeEditorExtension`(템플릿 · 블록 · 컨텍스트 · 툴바) + 게임 `MonsterAIExtension` — 근접/원거리/보스 템플릿, 조건부 어빌리티 등 블록, 이 트리를 쓰는 몬스터·어빌리티 표시와 경고, Play 중 "이 트리로 스폰".
- **MonsterAuthoring 인스펙터**: 능력치 · AI 트리 지정/전용 트리 · 외형 + 미리보기 · 트리거별 클립 · 저장/프리팹 빌드 · 복제. 몬스터 외형/클립 원본은 `Data/Monsters`(ContentBuilder 하드코딩에서 이전).
- 툴용 유틸: `CsvRow` / `CsvEditUtility`(CSV 한 행 읽기·쓰기, 다른 행·BOM 유지).
- 런타임 API: `GraphAbility.Create`, `Actor.GiveAbility(GameAbility)`, `MonsterBrain.RunTree`, `BuildManager.PlaceAt` / `StructureCount`.
- **Play 모드 테스트** (`Assets/Game/Tests/PlayMode`, 9개): GAS 4 · StateTree 2 · 시나리오 3(건물 / 몬스터 전투 / 보스 전투) — 전부 통과 (약 14초). [README.md 7절](README.md#7-테스트).
- 첫 실행에서 2개 실패 → 수정: 보스 교전 이벤트를 스폰 뒤에 구독해 놓침(스폰 전 구독), AI 의 어빌리티 선택은 Actor 슬롯 표를 보는데 ASC 에만 부여함(`Actor.GiveAbility(GameAbility)` 추가).

## 2026-10-25 · ASC 중심 액터 + 타입(is) 분기

- Behaviour 종류 확인은 enum 없이 `is` 타입 검사로 (`ASC.OwnerActor`, `Actor.From`, `SetParent` 의 `this is UIBehaviour`). 처음엔 `EBehaviourType` enum 으로 만들었다가, enum 분기가 늘수록 복잡해지고 리팩터링에 불리해서 같은 날 제거했다.
- ASC 에 `OwnerBehaviour` / `OwnerActor` 추가. `GetComponent<Actor>()` 2곳(GameAttributeSet 피해 Source, MonsterBrain 피격 대상)을 `Actor.From(asc)` 로 교체, `GameAttributeSet.Actor` 수동 대입 제거.
- `ActorBehaviour` 가 ASC 이벤트 7종(Tags.OnTagChanged 포함)을 구독해 가상 훅으로 전달. 태그/이펙트 헬퍼 추가.
- `Actor.IsDead` 플래그 → `State_Dead` 태그. 보호막 만료 폴링(Update) → 태그 제거 이벤트.
- Equipment/BloodSystem/Actor.HealInstant 의 `new GameplayEffectSpec(def, Asc, 0)` → `ApplyEffectToSelf` / `Asc.MakeOutgoingSpec`, `Asc.Tags.X` 체인 → `HasTag/AddTag/RemoveTag`.
- 확인: 컴파일 경고 0, Play — 보호막 120→0(태그 제거 즉시), 플레이어 평타 피해 Source=플레이어·몬스터가 플레이어를 타겟, 사망 중 어빌리티 불가, 부활 후 HP 400. 콘솔 에러 0.

## 2026-10-25 · Behaviour 계층 도입 (BaseBehaviour / ActorBehaviour / UIBehaviour)

- `Framework/Runtime/Core/Behaviours` 의 `Behaviour`, `BaseBehaviour` 를 `namespace Framework` 로 옮기고, transform/gameObject 캐시를 지연 캐싱으로 바꿨다 (Awake 전 접근 시 null 반환 방지).
- `ActorBehaviour` 재작성: 게임 타입 없이 ASC 소유 + `InitializeActor(id, AttributeSet)` + `GrantAbility/RemoveAbility`. `Actor` 의 `ActorId`/`Asc` 와 ASC RequireComponent 를 여기로 이동.
- `UIBehaviour` 신규: RectTransform 캐시 + `SetVisible`. `UIWindow`(자체 `Rect` 제거), `UIBar`, `ItemSlotView`, `DamageNumberLayer`, `WorldHealthBarLayer` 가 상속.
- Framework/Game 의 나머지 MonoBehaviour 전부 `BaseBehaviour` 상속으로 변경, `Awake/OnDestroy` → `OnUnityAwake/OnUnityDestroy`.
- 도입 전 상태는 브랜치 `backup/before-behaviour-flow` 로 백업. 설계는 [Architecture.md 5.1](Architecture.md).
- 확인: 컴파일 에러·경고 0, Play → 싱글 플레이 → 플레이어 스폰(어빌리티 7, 평타 발동) → 몬스터 파괴 시 ActorRegistry 제거까지 콘솔 에러 0.

## 2026-10-24 · 문서 검토 + Documentation 을 Assets 밖으로

- `Assets/Documentation` → 저장소 루트 `Documentation/` (Unity 임포트 대상에서 제외, `.meta` 삭제). 코드 주석의 `Documentation/VRising/…` 경로는 그대로 유효.
- 문서 내용을 코드와 대조해 갱신: Architecture 폴더 트리에 빠진 파일(`Core/Behaviours`, `PooledEffect`, `AbilityInputBuffer`, `ContentBuilder.SkillVfx`) 추가,
  `EExecution` 목록(PercentHeal), `EAssetKind`(Material/Data), Issues·URP 의 옛 문서 링크 수정. 메뉴 경로·치트 명령 24개·키 바인딩은 일치 확인.

## 2026-10-21 · Git 사고 복구 + 저장소 업로드 + 문서 정리

**복구** — 저장소를 새로 만들며 이전 `.git` 을 휴지통으로 버렸고, 이어서 `Revert "프로젝트 로드 Part4"` 가 Part4 로 추가한 파일을 디스크에서 지웠다.
- Part4 커밋(aee10b8)에서 외부 에셋 2,694개 (Magic Pig, SpecialSkillsEffectsPack, RPG Fantasy Items) 복원
- 휴지통의 이전 `.git` 스냅샷(18:15)에서 3,773개 복원: ASTROFISH_GAMES, `Assets/Documentation`, `Assets/Data`, `Assets/Editor`, `AddressableAssetsData`(원래 GUID), `InputSystem_Actions.inputactions` 등
- 모두 디스크에 없는 파일만 원본 그대로 되살렸고, 새로 만든 파일은 없다

**수정**
- `Tables.Bind(null)` NRE — `GameBootstrap.OnDestroy` 가 해제용으로 null 을 넘기는데 `Root.OnBind()` 를 무조건 호출했다. null 이면 해제만 한다.
- `AnimationInPlace` 삭제(사용자 결정, 07:23) 후 몬스터 프리팹 4개에 남은 빈 컴포넌트 제거. 제자리 처리는 FBX 임포트 설정(`Game/Setup/3c`)만 남음 → Issues 35 참고.
- Play 확인: 타이틀 → 월드 스폰(액터 20) → 싱글 플레이 → 평타·Q·E·Space·R·C·T → 종료까지 콘솔 에러·경고 0.

**업로드** — 9.3GB 를 GitHub 에 올리기 위해 `Tools/GitBatchPush` 추가 (용량 기준 배치 → 배치마다 커밋·푸시). 100MB 초과 데모 라이트맵 3개는 Git LFS.

**문서 정리** — 이전 프로젝트 문서를 `Legacy/` 로 분리, CodeStructure + DesignPatterns → [Architecture.md](Architecture.md), 이 CHANGELOG 신설,
중복 설명은 한 곳(원본)에만 두고 나머지는 링크로 바꿈.

## 2026-10-20 · AnimationTable — 애니메이션 재생을 데이터로

- `AnimationTable.csv` 신설: `Index, Speed, Parameters(Type;Name;Value 의 배열), ApplyRootMotion`.
- 재생은 `ActorAnimator.Play(AnimationTable Index, 속도)` 하나로 통일. `EAnim` 은 에디터 AnimatorController 빌더의 상태/트리거 슬롯 이름으로만 남음.
- `AbilityTable.Anim(EAnim)` → `Anims(int[])` (MeleeCombo 는 콤보 단계 순서 `1|2|7`), `ResourceNodeTable.Anim`, `ConstTable.BuildAnim/InteractAnim` 추가.
- `CsvConvert` 가 `[Serializable]` struct 를 `;` 구분(필드 선언 순서)으로 읽도록 확장 (Framework).
- 컨트롤러 빌더가 AnimationTable 에만 있는 파라미터도 생성, Validate 가 없는 애니메이션 Index·Speed≤0 을 검사.

## 2026-10-14 · 외부 에셋 교체 + 512m 오픈월드 + WeatherMaker

1. **캐릭터**: 플레이어를 unity-chan 으로 교체, RPG Character FREE 애니메이션을 Humanoid 리타겟. 검 소켓은 같은 포즈를 두 캐릭터에 샘플링해 자동 계산.
2. **건축/채집 모션**: Crafting Pack 으로 건축(설치 시 1.1초 이동 잠금), 벌목·채광·채집. 주변에 적이 없고 채집 노드가 있으면 근접 공격 대신 채집 모션.
3. **몬스터**: Magic Pig 4종을 근접/원거리/브루트/보스 슬롯에 연결. 이름(StringTable), 사거리·크기(CharacterTable) 조정.
4. **성 건축**: ASTROFISH 모듈 10종, 구조물 종류별 스냅(중심/가장자리/모서리).
5. **월드**: 512m Terrain, 침식 높이맵, TerrainSampleAssets 레이어·풀, TheTalesFactory 바위, 폐허 마을, 호수, 트롤 능선, 보스 아레나. SpawnTable 재작성.
6. **햇빛**: WeatherMaker 하늘/태양/달/구름. 게임 시간이 주인이고 태양 방향을 판정에 역으로 사용.
7. **공통 유틸**: `MaterialUpgradeUtil` (외부 머티리얼 → URP Lit 복사본, 원본 무수정).
8. 태그 비교를 언리얼과 같은 의미로 개편 (GAS.md), V Rising 상태이상 7종 + Consume + Immaterial, 입력 버퍼/콤보 초기화, 스킬 이펙트 팩 적용.
9. Play 확인: 몬스터 20 / 채집 노드 231 스폰, unity-chan URP 렌더 + 검 장착, 나무·구름 그림자, 폐허 마을·상인·횃불 표시.

## 2026-10-13 · 코드 폴더 / 어셈블리 재정리

| 이전 | 이후 |
|---|---|
| `Assets/Game/Framework` (`Game.Framework`) | `Assets/Framework/Runtime` (`Framework.Runtime`) |
| (없음) | `Assets/Framework/Editor` (`Framework.Editor`) 신설 |
| `Assets/Game/Scripts` | `Assets/Game/Runtime` |
| `Scripts/AI` | `Runtime/Monster` |
| `Scripts/Player/GameInput`, `TopDownCamera` | `Runtime/Input`, `Runtime/Camera` |
| `Scripts/Ability/CombatUtil` | `Runtime/Combat` |
| `Scripts/UI/*` (평평함) | `UI/Windows`, `UI/Widgets`, `UI/WorldSpace` |
| `MonsterCharacter.cs` 안의 `LootUtil` | `Runtime/Inventory/LootUtil.cs` |
| `Game/Editor/*` (평평함) | `Setup / Data / Content / World / AI / Tools` |
| `StateTreeEditorWindow` (Game.Editor, EBBKey·EditorPaths 직접 참조) | Framework.Editor 로 이동, 게임 의존 제거 (어트리뷰트 연결) |
| `CsvTableImporter` 의 범용 변환·자동 재변환·폴더 생성 | `TableImportUtility`, `TableImportPostprocessor`, `EditorAssetUtil` (Framework.Editor) |
| CSV 자동 재변환: 경로 규칙으로 테이블 탐색 | 테이블의 `Source` 참조로 탐색 → 폴더 구조와 무관 |

- 모든 이동은 `AssetDatabase.MoveAsset` → `.meta` GUID 유지 → 프리팹·씬의 스크립트 참조 유지 (Missing Script 0 확인).
- 어셈블리 이름 변경으로 StateTree 에셋의 `[SerializeReference]` 노드가 Missing → `[MovedFrom]` 으로 해결 (Issues 17).
- 2026-10-14: asmdef 없는 외부 에셋 어댑터용 `Game/Integration` 추가 (WeatherMakerSkyBridge).
