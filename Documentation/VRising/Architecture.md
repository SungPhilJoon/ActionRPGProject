# 아키텍처 — 어셈블리 · 폴더 · 디자인 패턴

코드가 어디에 있고, 무엇이 무엇을 참조할 수 있으며, 어떤 패턴으로 짜여 있는지 정리한 문서입니다.
시스템별 상세는 [GAS.md](GAS.md) · [StateTree.md](StateTree.md), 구조가 바뀐 이력은 [CHANGELOG.md](CHANGELOG.md).

## 1. 핵심 원칙

| 구분 | 위치 | 어셈블리 | 규칙 |
|---|---|---|---|
| **Framework** | `Assets/Framework/Runtime` | `Framework.Runtime` | 게임 타입(ETag, Actor, Tables …)을 **절대 참조하지 않는다**. 다른 프로젝트에 폴더째 복사해서 쓸 수 있어야 한다 |
| | `Assets/Framework/Editor` | `Framework.Editor` | 에디터 전용. `Framework.Runtime` 만 참조 |
| **Game** | `Assets/Game/Runtime` | `Game.Runtime` | 이 게임의 플레이 코드. `Framework.Runtime` 참조 |
| | `Assets/Game/Editor` | `Game.Editor` | 이 게임 전용 에디터 툴. `Game.Runtime`, `Framework.Runtime`, `Framework.Editor` 참조 |
| | `Assets/Game/Tests/PlayMode` | `Game.Tests.PlayMode` | Play 모드 테스트 (Unity Test Framework). `Game.Runtime`, `Framework.Runtime` 참조, UNITY_INCLUDE_TESTS 일 때만 컴파일 |
| **Integration** | `Assets/Game/Integration` (현재 없음) | (asmdef 없음 → `Assembly-CSharp`) | asmdef 없는 외부 에셋과 게임을 잇는 어댑터만 둔다. 게임 인터페이스를 구현해 런타임에 등록. 유일한 어댑터였던 WeatherMaker 브리지는 2026-10-30 삭제 |

```
Framework.Runtime  ◀──  Game.Runtime
       ▲                    ▲
Framework.Editor   ◀──  Game.Editor

Assembly-CSharp (asmdef 없는 외부 에셋 + Game/Integration) ──▶ Game.Runtime   (Game.Runtime 은 반대 방향을 모름)
```

- 의존은 **위 그림의 화살표 방향으로만** 흐른다. asmdef 참조로 강제되므로 Framework 에서 게임 타입을 쓰면 컴파일 에러가 난다.
- Framework 가 게임 정보를 알아야 할 때는 **어트리뷰트로 역연결**한다.
  - `[StateTreeEventEnum]` → StateTree 에디터가 게임의 이벤트 enum(`EAIEvent`)을 이름 팝업으로 표시
  - `[StateTreeBlackboardEnum]` → 디버거가 게임의 블랙보드 키 enum(`EBBKey`)을 이름으로 표시
- 게임 쪽 Framework 사용은 `using Framework;`, `using Framework.Ability;`, `using Framework.StateTree;`, `using Framework.UI;`, (에디터) `using Framework.Editor;`

## 2. Framework

```
Assets/Framework/
  Runtime/                          Framework.Runtime (namespace Framework.*)
    Core/        Log, Managers(IManager)
      Behaviours/ Behaviour → BaseBehaviour → ActorBehaviour / UIBehaviour (모든 MonoBehaviour 의 공통 부모, 5.1 참고)
    Util/        HashUtil, MathUtil, ComponentUtil, BoundsUtil, TerrainUtil (런타임 TerrainData 복제 · Detail 지우기)
    Camera/Occlusion/ CameraOcclusionSystem + 감지/응답 전략 + CameraOccluder ([CameraOcclusion.md](CameraOcclusion.md))
    Event/       EventBus<T>
    Pool/        GameObjectPool
    Asset/       AssetLoader (Addressables 래퍼, (주소,타입) 캐시, 소유자 검사)
    Data/        CsvReader, CsvConvert, TableAsset<TRow>
    Ability/     GAS — AbilitySystemComponent, AttributeSet, GameplayEffect, GameplayEffectExecution(ExecutionRegistry), GameplayAbility, AbilityTask, GameplayCue, GameplayTagManager/GameplayTagContainer/GameplayTagCountContainer
    StateTree/   StateTree — Asset, Nodes(Task/Condition/Evaluator, Blackboard, Context), Instance, Types, CommonNodes(Wait/SendEvent/DebugLog/AlwaysTrue)
    Asset/       AssetLoader, AssetTracker(로드 · 참조 · 반납 · 동기 대기 기록), DependencyRecord(시나리오 기록 · DependencyRecorder)
    UI/          UIManager(프리팹 Instantiate), UIWindow(Initialize/OnInitialize), UIItemList(위젯 프리팹 목록), UIBar, UISkin — UI.md
  Editor/                           Framework.Editor (namespace Framework.Editor)
    StateTree/   StateTreeEditorWindow (Window > Framework > StateTree Editor, 에셋 더블클릭), StateTreeEditorExtension (게임이 템플릿·블록·컨텍스트·툴바를 붙이는 확장점)
    Data/        TableImportUtility (CSV → TableAsset), TableImportPostprocessor (CSV 저장 시 Source 로 연결된 테이블 자동 재변환), CsvEditUtility · CsvRow (툴이 CSV 한 행을 작업 사본으로 읽고 쓰기)
    Util/        EditorAssetUtil.EnsureFolder, MaterialUpgradeUtil (Built-in/Standard → URP Lit 복사본), ShadeColliderUtil (그림자 메시 → 그늘 판정 콜라이더)
```

## 3. Game

```
Assets/Game/
  Runtime/                          Game.Runtime (전역 네임스페이스)
    Core/        GameBootstrap(진입점), GameEvents(EventBus 페이로드)
    Define/      GameEnums (ETag, EAttribute, EBBKey, EAIEvent, LayerId, UILayerId …)
    Data/        테이블 20종 행/에셋 클래스 + GameTables(Tables 파사드)
    Ability/     GameAttributeSet, EffectLibrary(+TagUtil), GameExecutions, GameAbility + 구현(근접/투사체/대시/범위/버프/흡혈/도약)
      Graph/     AbilityGraphAsset(에디터로 만드는 어빌리티), AbilitySteps(단계 13종), GraphAbility(실행기) — GAS.md 4.1
    Combat/      CombatUtil(히트 처리), CombatLog, Projectile(ProjectileSystem), CueSpawner, PooledEffect(외부 VFX 풀 래퍼), TimedDespawn
    Actor/       Actor, ActorMotor, ActorAnimator(+AnimatorParam, AnimationTable 재생), ActorRegistry
    Player/      PlayerCharacter, PlayerController, AbilityInputBuffer(공격 중 입력 버퍼), BloodSystem, SunExposure
    Input/       GameInput (InputTable.csv → Input System)
    Camera/      TopDownCamera
    Monster/     MonsterCharacter, MonsterBrain(StateTree 호스트)
      StateTree/ 몬스터 노드: Evaluator / Condition / Task
    World/       DayNightManager, SkyBridge(ISkyController), SpawnManager, ResourceNode, Merchant, Interactable, SceneContext, WaterBody(강/호수 영역)
      Foliage/   FoliageInstance(장식 나무/덤불), FoliageRoot(목록 + 격자 색인), FoliageManager(건축 시 나무·풀 제거)
    Building/    BuildManager, Structure
    Quest/       QuestManager (QuestTable 체인 진행, 게임 이벤트 구독 → QuestChangedEvent / QuestsCompletedEvent)
    Inventory/   Inventory, Equipment, LootUtil
    Cheat/       CheatManager
    UI/          UIUtil
      Windows/   Hud, Inventory, Craft, Build, Shop, Title, CheatConsole, BalanceOverlay, ListWindow(공통 부모) — 레이아웃은 프리팹(Content/UI)
      Widgets/   UIText(TextId 문구), ListRowView, AbilitySlotView, WorldHealthBarView, ItemSlotView
      Widgets/   ItemSlotView
      WorldSpace/ DamageNumberLayer, WorldHealthBarLayer
  Editor/                           Game.Editor
    Setup/       GameSetupMenu(Run All), ProjectSetup(레이어), EditorPaths
    Data/        CsvTableImporter (게임 테이블 생성 + GameTables 연결 + 참조 검증)
    Content/     ContentBuilder(.Characters = 플레이어/몬스터/상인/무기/애니메이터, .Structures = 성 모듈/채집 노드, .Vfx = 절차 VFX, .SkillVfx = 스킬 이펙트 팩), AddressablesSetup
    World/       WorldBuilder(.Terrain = 높이맵/레이어/풀, .Foliage = 나무·덤불 배치, .Water = 강·호수, .Landmarks = 바위/마을/아레나, .Sky = 절차 스카이박스 · 안개 · 하늘 반사, .PostFx = 포스트 프로세싱 볼륨 · 렌더러 피처)
      Foliage/   TreeGenerator(절차 나무 골격 → LOD 메시), TreeFactory(프리팹·머티리얼), FoliageTextures + FoliageCanvas(절차 텍스처)
    AI/          MonsterStateTreeDefaults (기본 트리 3종 + Game > Tools > StateTree Tool 메뉴)
      MonsterAIExtension (StateTree Tool 몬스터 AI 도우미)
      Monster/   MonsterAuthoring(→ Data/Monsters) + 인스펙터(능력치·외형·클립·AI 트리·빌드), MonsterAuthoringUtil, MonsterPreview
    UI/          UIPrefabBuilder(UI 프리팹 굽기), UISkinSetup
    Tools/       BalanceToolWindow, UIFactoryWindow
      DependencyTool/ DependencyMapWindow(의존성 지도), DependencyMapService, GameDependencyUsage — [Dependency.md](Dependency.md)
      GasTool/   GasToolWindow(+Ability/Effect/AbilitySet 탭), AbilityTemplates, GasEditorGUI, AbilityInfo, AbilityGuide(기존 타입 설명) — [Tool.md](Tool.md)
  Tests/PlayMode/                   Game.Tests.PlayMode
    GamePlayTestBase(World 로드·싱글 시작·정리), GasAbilityTests, StateTreeTests, PlayScenarioTests(몬스터/보스) + PlayScenarioTests.Castle(성 짓기 + 10초 쇼케이스), QuestTests(퀘스트 1→17 전체 체인)
```

## 4. 새 코드를 어디에 둘지

| 만들 것 | 위치 |
|---|---|
| 어떤 게임에서도 쓸 수 있는 시스템/유틸 (게임 enum·테이블을 모름) | `Framework/Runtime/<영역>` |
| 그 시스템의 범용 에디터 | `Framework/Editor/<영역>` |
| 이 게임의 규칙·데이터를 아는 코드 | `Game/Runtime/<영역>` |
| 이 게임 콘텐츠를 만드는 에디터 툴 | `Game/Editor/<영역>` |
| 새 몬스터 StateTree 노드 | `Game/Runtime/Monster/StateTree` (MonsterTask/Condition/Evaluator 상속) |
| asmdef 없는 외부 에셋과의 연결 | 인터페이스는 `Game/Runtime`, 구현은 `Game/Integration/<에셋명>` |
| 새 UI 창 | 스크립트 `Game/Runtime/UI/Windows` (UI Factory 툴) + 레이아웃 `Game/Editor/UI/UIPrefabBuilder` + `EUIAsset`/AssetTable 행 — [UI.md 3절](UI.md#3-고치는-방법). 런타임에 자식을 만들지 않는다 |
| 새 의존성 테스트 시나리오 | `Game/Tests/PlayMode/DependencyScenarioTests` (`DependencyRecorder.Begin/End`) |
| 새 에디터 툴 | `Game/Editor/Tools/<툴이름>` (여러 파일이면 폴더), 설명은 [Tool.md](Tool.md) 에 추가 |
| 새 Play 모드 테스트 | `Game/Tests/PlayMode` — `GamePlayTestBase` 상속, `[UnityTest]` |
| 새 어빌리티 / GameplayEffect | GAS Tool (그래프 에셋 `Game/Content/Abilities`, 행 AbilityTable/EffectTable) |
| 새 몬스터 | MonsterAuthoring 인스펙터의 "복제해서 새 몬스터" (저작 에셋 `Game/Data/Monsters`, 프리팹 `Game/Content/Characters`) → 어빌리티는 GAS Tool, AI 는 StateTree Tool (`Game/Content/AI`) |
| 새 그래프 단계 | `Game/Runtime/Ability/Graph/AbilitySteps.cs` 에 `AbilityStep` 하위 클래스 + `[AbilityStepInfo]` (GAS Tool 메뉴에 자동 표시) |
| 새 MonoBehaviour | `MonoBehaviour` 대신 `BaseBehaviour`(UI 는 `UIBehaviour`, 액터는 `Actor`) 상속 — 5.1 |

## 5. 사용한 디자인 패턴

### 5.1 Behaviour 계층 (MonoBehaviour 공통 부모)

```
MonoBehaviour
 └ Framework.Behaviour          Awake / OnDestroy 를 virtual 로 연다
    └ Framework.BaseBehaviour   transform·gameObject 캐시, Awake/OnDestroy 봉인 → OnUnityAwake / OnUnityDestroy
       ├ Framework.ActorBehaviour  [RequireComponent(ASC)] ActorId, Asc, Tags/Effects/Abilities(ASC 프로퍼티), HasTag/AddTag/RemoveTag,
       │                           ApplyEffectToSelf/RemoveEffect, GrantAbility/RemoveAbility, ASC 이벤트 → 가상 훅
       │  └ Actor (Game)           GameAttributeSet, 슬롯 어빌리티, 사망/부활 → PlayerCharacter, MonsterCharacter
       ├ Framework.UIBehaviour     [RequireComponent(RectTransform)] Rect 캐시, IsVisible / SetVisible
       │  └ UIWindow, UIBar, ItemSlotView, DamageNumberLayer, WorldHealthBarLayer
       └ 그 외 모든 Framework/Game 컴포넌트 (ASC, ActorMotor, ActorAnimator, MonsterBrain, GameBootstrap …)
```

- 파생 클래스는 `Awake` / `OnDestroy` 를 선언하지 않는다 (봉인됨, 선언하면 CS0114 경고와 함께 부모 흐름이 끊긴다).
  초기화는 `protected override void OnUnityAwake()` 에서 `base.OnUnityAwake()` 를 먼저, 정리는 `OnUnityDestroy()` 에서 `base.OnUnityDestroy()` 를 마지막에 호출한다.
- `Start / OnEnable / OnDisable / Update` 는 그대로 Unity 메시지를 쓴다.
- **타입 확인은 `is`**: 외부에서 받은 Behaviour 가 무엇인지는 enum 이 아니라 타입으로 확인한다 (`x is ActorBehaviour actor`, `x is UIBehaviour`).
  종류를 나타내는 enum 을 따로 두지 않는다 — 새 Behaviour 를 추가해도 등록할 곳이 없고, 클래스 이름 변경·이동이 리팩터링 도구로 그대로 따라온다.
  Unity 객체는 `is` 가 파괴 여부를 보지 않으므로, 파괴됐을 수 있는 참조는 `== null` 로 먼저 거른다.
  예: `BaseBehaviour.SetParent` 는 `this is UIBehaviour` 면 로컬 레이아웃 유지(`worldPositionStays = false`), 그 외는 월드 위치 유지.
  클래스 계층 안의 차이는 기존처럼 virtual override 로 처리한다 (타입 확인은 "받은 객체가 무엇인지 모를 때" 만).
- **ASC 가 상태의 원본**: `AbilitySystemComponent.OwnerBehaviour` (BaseBehaviour) 를 `InitializeActor` 가 연결하고,
  `OwnerActor` 는 소유자가 `ActorBehaviour` 일 때만(`is`) 값을 준다. 게임은 `Actor.From(asc)` 로 이펙트 Source·이벤트 페이로드의 액터를 얻는다 (GetComponent 없음).
  `GameAttributeSet.Actor` 도 `Owner`(ASC) 에서 얻는다.
  - 사망: `Actor.IsDead` = `State_Dead` 태그. `Die` 는 태그를 먼저 붙인 뒤 어빌리티 취소/이펙트 제거.
  - 보호막: 매 프레임 폴링 대신 `OnTagChanged(State_Buff_Shield, false)` 훅에서 Shield = 0.
  - 상시 이펙트(장비/혈액): `ApplyEffectToSelf(def, abilityId)` → 내부에서 `Asc.MakeOutgoingSpec` 사용.
  - 훅: `OnTagChanged / OnEffectAdded / OnEffectRemoved / OnEffectExecuted / OnAbilityActivated / OnAbilityEnded / OnGameplayEvent`.
    같은 GameObject 의 다른 컴포넌트(MonsterBrain 등)는 지금처럼 ASC 이벤트를 직접 구독한다.
- 액터 초기화 순서: `Actor.Initialize(row, id)` → `InitializeActor(id, GameAttributeSet)` (ASC 바인딩) → 능력치 → 어빌리티 부여(`GrantAbility`) → `ActorRegistry.Add`. 파괴 시 `OnUnityDestroy` 에서 레지스트리 제거.
- `Framework.Behaviour` / `Framework.UIBehaviour` 는 `UnityEngine.Behaviour` / `UnityEngine.EventSystems.UIBehaviour` 와 이름이 같다. `using UnityEngine.EventSystems;` 와 함께 쓰는 파일에서는 `Framework.UIBehaviour` 로 한정한다.
- 도입 전 코드는 git 브랜치 `backup/before-behaviour-flow` 에 남아 있다 (프로젝트 안에 옛 코드를 두지 않음).

### 5.2 패턴 목록

| 패턴 | 어디에 | 왜 |
|---|---|---|
| **Service Locator + 명시적 라이프사이클** | `Framework/Runtime/Core/Managers.cs` (`IManager`, `Managers.Regist/Get/Update/Clear`) | MonoBehaviour 싱글톤 대신 순수 C# 매니저. 등록 순서 = 업데이트 순서, 역순 정리로 의존 관계가 명확 |
| **Observer (정적 제네릭 이벤트 버스)** | `Framework/Runtime/Event/EventBus.cs`, `Game/Runtime/Core/GameEvents.cs` | 게임플레이 → UI 단방향. 이벤트 종류는 문자열이 아니라 struct 타입. 타입별 정적 리스트라 Dictionary 조회/박싱 없음 |
| **Object Pool** | `Framework/Runtime/Pool/GameObjectPool.cs`, `DamageNumberLayer` | 발사체·VFX·데미지 숫자 반복 생성/파괴 방지 |
| **Flyweight** | `EffectLibrary` (EffectRow → GameplayEffectDef 캐시), 테이블 행 공유 | 모든 스펙이 같은 정의 객체를 공유 |
| **Factory** | `AbilityFactory` (EAbilityType → 구현 클래스), `UIFactory` (에디터 uGUI 조립), `ContentBuilder` · `UIPrefabBuilder` (프리팹 생성) | 생성 로직을 한 곳에 모아 데이터만 바꿔 확장 |
| **Prefab + 직렬화 참조 (UI)** | `UIManager.Create(프리팹)` → `UIWindow.Initialize`, 위젯은 `UIItemList<T>` | 런타임 조립 · 이름 검색 없이 디자이너가 프리팹을 직접 편집 (UI.md) |
| **Observer (로드 추적)** | `AssetLoader` → `AssetTracker` (이벤트 · OnEvent) → 의존성 지도 실시간 탭 / `DependencyRecorder` | 로더 코드는 툴을 모른다 |
| **Plugin (사용처 제공)** | `IDependencyUsageProvider` ← `GameDependencyUsage` (TypeCache 자동 발견) | Framework 분석기가 게임 테이블을 몰라도 사용처를 그래프에 표시 |
| **Template Method** | `GameplayAbility` (CanActivate → InternalActivate → OnActivate/OnTick/OnEnd), `ListWindow` (Build → Fill) | 공통 흐름은 부모, 차이점만 자식 |
| **Strategy** | `IGameplayEffectExecution` (`EExecution`: Damage/Heal/SunDamage/PercentHeal/PercentDamage) + `ExecutionRegistry` | 피해 공식 교체를 데이터(EffectTable.Execution)로 선택 |
| **Strategy (카메라 가림)** | `ICameraOcclusionDetector` / `ICameraOcclusionResponse` → `CameraOcclusionSystem.SetDetector/SetResponse` | 감지(캐스트 방식)와 응답(숨김/페이드)을 독립적으로 교체 ([CameraOcclusion.md](CameraOcclusion.md)) |
| **Observer (게임 이벤트 → 퀘스트)** | `ItemObtainedEvent` · `PlayerAbilityUsedEvent` · `PlayerFedEvent` · `ActorDiedEvent` · `StructurePlacedEvent` · `EquipmentChangedEvent` → `QuestManager` | 게임플레이 코드는 퀘스트를 모르고 "무슨 일이 있었는지" 만 발행. 새 목표 종류 = `EQuestObjective` + 구독 하나 |
| **Observer (건축 → 식생)** | `StructurePlacedEvent` → `FoliageManager` | BuildManager 는 나무/풀을 모른다. 멀티플레이에선 이벤트만 각 피어에서 발행 |
| **Spatial Hash** | `FoliageRoot` (16m 격자) | 수천 그루 중 건축 바운드 주변만 검사 |
| **Command** | `CheatManager` (명령어 해시 → 핸들러), `PlayerController` (입력 → `Actor.TryActivate`) | 입력과 실행 분리. 멀티플레이 시 명령을 서버로 전송하는 지점이 됨 |
| **State (계층형 상태 머신 = StateTree)** | `Framework/Runtime/StateTree`, `MonsterBrain` 이 실행 | 몬스터 행동을 코드가 아닌 에셋으로. 상태 계층/트랜지션을 툴에서 편집 |
| **Composite** | StateTree 상태 계층 (부모 상태의 태스크도 자식이 활성인 동안 계속 실행) | 공통 행동(예: 2페이즈 버프)을 부모에 한 번만 |
| **Prototype** | `StateTreeNodeUtil.Clone` — 에셋의 노드를 몬스터마다 복제해 인스턴스 데이터로 사용 | 언리얼 InstanceData 대응 |
| **Blackboard** | `StateTreeBlackboard` (int 키) | 평가기 출력 → 조건/태스크 입력 (Property Binding 대체) |
| **Adapter** | `ActorMotor` (CharacterController / NavMeshAgent 를 같은 API 로) | 어빌리티가 이동 방식을 몰라도 됨 |
| **Data-Driven / Type Object** | CSV → ScriptableObject 테이블, CharacterRow/AbilityRow 가 "종류"를, AnimationRow 가 애니메이션 재생 방식을 정의 | 코드 수정 없이 밸런스·콘텐츠 변경 |
| **Facade** | `Tables` 정적 클래스 (`Tables.Const`, `Tables.Text`, `Tables.Address`, `Tables.Animation`) | 테이블 접근 단순화 |
| **Builder (에디터)** | `WorldBuilder`, `ContentBuilder`, `TreeFactory` | 씬/프리팹을 재현 가능하게 코드로 생성 |
| **골격 → 메시 (에디터)** | `TreeGenerator.Build` → `ToMesh(골격, LOD)` | 같은 골격을 LOD 별 디테일로 메시화해 LOD 사이 모양이 같다 |
| **Component** | Actor = ASC + Motor + Animator + (Blood/Sun/Inventory/Equipment 또는 Brain) | Unity 컴포넌트 조합 |
| **Adapter + Dependency Inversion (Bridge)** | `ISkyController` / `SkyBridge` (Game.Runtime) ← 외부 하늘 에셋 어댑터 (Assembly-CSharp, `Game/Integration`) | Game.Runtime 은 asmdef 없는 외부 에셋을 참조할 수 없음. 인터페이스는 게임에, 구현은 외부 쪽에 두고 런타임에 등록. 2026-10-30 WeatherMaker 어댑터를 지워 지금은 구현 없음 → 기본 낮밤(DayNightManager)으로 동작 |
| **Null Object / Fallback** | `DayNightManager.apply` (SkyBridge.Controller 가 null 이면 자체 Directional Light) | 외부 에셋 제거 시에도 게임 유지 |
| **Builder + 캐시 (에디터)** | `MaterialUpgradeUtil` (원본 머티리얼 → URP 복사본, 같은 원본은 한 번만 변환) | 외부 에셋을 수정하지 않고 파이프라인에 맞춤 |
