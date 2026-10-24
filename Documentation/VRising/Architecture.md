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
| **Integration** | `Assets/Game/Integration` | (asmdef 없음 → `Assembly-CSharp`) | asmdef 없는 외부 에셋(WeatherMaker 등)과 게임을 잇는 어댑터만 둔다. 게임 인터페이스를 구현해 런타임에 등록 |

```
Framework.Runtime  ◀──  Game.Runtime
       ▲                    ▲
Framework.Editor   ◀──  Game.Editor

Assembly-CSharp (WeatherMaker 등 외부 에셋 + Game/Integration) ──▶ Game.Runtime   (Game.Runtime 은 반대 방향을 모름)
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
      Behaviours/ Behaviour, BaseBehaviour (MonoBehaviour 공통 부모 — 작업 중, 아직 사용처 없음)
    Util/        HashUtil, MathUtil, ComponentUtil
    Event/       EventBus<T>
    Pool/        GameObjectPool
    Asset/       AssetLoader (Addressables 래퍼, (주소,타입) 캐시, 소유자 검사)
    Data/        CsvReader, CsvConvert, TableAsset<TRow>
    Ability/     GAS — AbilitySystemComponent, AttributeSet, GameplayEffect, GameplayEffectExecution(ExecutionRegistry), GameplayAbility, AbilityTask, GameplayCue, GameplayTagManager/GameplayTagContainer/GameplayTagCountContainer
    StateTree/   StateTree — Asset, Nodes(Task/Condition/Evaluator, Blackboard, Context), Instance, Types, CommonNodes(Wait/SendEvent/DebugLog/AlwaysTrue)
    UI/          UIFactory, UIManager, UIWindow, UIBar
  Editor/                           Framework.Editor (namespace Framework.Editor)
    StateTree/   StateTreeEditorWindow (Window > Framework > StateTree Editor, 에셋 더블클릭)
    Data/        TableImportUtility (CSV → TableAsset), TableImportPostprocessor (CSV 저장 시 Source 로 연결된 테이블 자동 재변환)
    Util/        EditorAssetUtil.EnsureFolder, MaterialUpgradeUtil (Built-in/Standard → URP Lit 복사본), ShadeColliderUtil (그림자 메시 → 그늘 판정 콜라이더)
```

## 3. Game

```
Assets/Game/
  Runtime/                          Game.Runtime (전역 네임스페이스)
    Core/        GameBootstrap(진입점), GameEvents(EventBus 페이로드)
    Define/      GameEnums (ETag, EAttribute, EBBKey, EAIEvent, LayerId, UILayerId …)
    Data/        테이블 19종 행/에셋 클래스 + GameTables(Tables 파사드)
    Ability/     GameAttributeSet, EffectLibrary(+TagUtil), GameExecutions, GameAbility + 구현(근접/투사체/대시/범위/버프/흡혈/도약)
    Combat/      CombatUtil(히트 처리), CombatLog, Projectile(ProjectileSystem), CueSpawner, PooledEffect(외부 VFX 풀 래퍼), TimedDespawn
    Actor/       Actor, ActorMotor, ActorAnimator(+AnimatorParam, AnimationTable 재생), ActorRegistry
    Player/      PlayerCharacter, PlayerController, AbilityInputBuffer(공격 중 입력 버퍼), BloodSystem, SunExposure
    Input/       GameInput (InputTable.csv → Input System)
    Camera/      TopDownCamera
    Monster/     MonsterCharacter, MonsterBrain(StateTree 호스트)
      StateTree/ 몬스터 노드: Evaluator / Condition / Task
    World/       DayNightManager, SkyBridge(ISkyController), SpawnManager, ResourceNode, Merchant, Interactable, SceneContext
    Building/    BuildManager, Structure
    Inventory/   Inventory, Equipment, LootUtil
    Cheat/       CheatManager
    UI/          UIUtil
      Windows/   Hud, Inventory, Craft, Build, Shop, Title, CheatConsole, BalanceOverlay, ListWindow(공통 부모)
      Widgets/   ItemSlotView
      WorldSpace/ DamageNumberLayer, WorldHealthBarLayer
  Editor/                           Game.Editor
    Setup/       GameSetupMenu(Run All), ProjectSetup(레이어), EditorPaths
    Data/        CsvTableImporter (게임 테이블 생성 + GameTables 연결 + 참조 검증)
    Content/     ContentBuilder(.Characters = 플레이어/몬스터/상인/무기/애니메이터, .Structures = 성 모듈/채집 노드, .Vfx = 절차 VFX, .SkillVfx = 스킬 이펙트 팩), AddressablesSetup
    World/       WorldBuilder(.Terrain = 높이맵/레이어/풀/나무, .Landmarks = 바위/마을/아레나/호수, .Sky = WeatherMaker)
    AI/          MonsterStateTreeDefaults (+ Game > AI > StateTree Editor 메뉴)
    Tools/       BalanceToolWindow, UIFactoryWindow
  Integration/                      (asmdef 없음 → Assembly-CSharp)
    WeatherMaker/ WeatherMakerSkyBridge (ISkyController 구현, 씬 로드 시 자동 부착)
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
| 새 UI 창 | `Game/Runtime/UI/Windows` (UI Factory 툴이 여기로 생성) |

## 5. 사용한 디자인 패턴

| 패턴 | 어디에 | 왜 |
|---|---|---|
| **Service Locator + 명시적 라이프사이클** | `Framework/Runtime/Core/Managers.cs` (`IManager`, `Managers.Regist/Get/Update/Clear`) | MonoBehaviour 싱글톤 대신 순수 C# 매니저. 등록 순서 = 업데이트 순서, 역순 정리로 의존 관계가 명확 |
| **Observer (정적 제네릭 이벤트 버스)** | `Framework/Runtime/Event/EventBus.cs`, `Game/Runtime/Core/GameEvents.cs` | 게임플레이 → UI 단방향. 이벤트 종류는 문자열이 아니라 struct 타입. 타입별 정적 리스트라 Dictionary 조회/박싱 없음 |
| **Object Pool** | `Framework/Runtime/Pool/GameObjectPool.cs`, `DamageNumberLayer` | 발사체·VFX·데미지 숫자 반복 생성/파괴 방지 |
| **Flyweight** | `EffectLibrary` (EffectRow → GameplayEffectDef 캐시), 테이블 행 공유 | 모든 스펙이 같은 정의 객체를 공유 |
| **Factory** | `AbilityFactory` (EAbilityType → 구현 클래스), `UIFactory` (uGUI 조립), `ContentBuilder` (프리팹 생성) | 생성 로직을 한 곳에 모아 데이터만 바꿔 확장 |
| **Template Method** | `GameplayAbility` (CanActivate → InternalActivate → OnActivate/OnTick/OnEnd), `ListWindow` (Build → Fill) | 공통 흐름은 부모, 차이점만 자식 |
| **Strategy** | `IGameplayEffectExecution` (`EExecution`: Damage/Heal/SunDamage/PercentHeal/PercentDamage) + `ExecutionRegistry` | 피해 공식 교체를 데이터(EffectTable.Execution)로 선택 |
| **Command** | `CheatManager` (명령어 해시 → 핸들러), `PlayerController` (입력 → `Actor.TryActivate`) | 입력과 실행 분리. 멀티플레이 시 명령을 서버로 전송하는 지점이 됨 |
| **State (계층형 상태 머신 = StateTree)** | `Framework/Runtime/StateTree`, `MonsterBrain` 이 실행 | 몬스터 행동을 코드가 아닌 에셋으로. 상태 계층/트랜지션을 툴에서 편집 |
| **Composite** | StateTree 상태 계층 (부모 상태의 태스크도 자식이 활성인 동안 계속 실행) | 공통 행동(예: 2페이즈 버프)을 부모에 한 번만 |
| **Prototype** | `StateTreeNodeUtil.Clone` — 에셋의 노드를 몬스터마다 복제해 인스턴스 데이터로 사용 | 언리얼 InstanceData 대응 |
| **Blackboard** | `StateTreeBlackboard` (int 키) | 평가기 출력 → 조건/태스크 입력 (Property Binding 대체) |
| **Adapter** | `ActorMotor` (CharacterController / NavMeshAgent 를 같은 API 로) | 어빌리티가 이동 방식을 몰라도 됨 |
| **Data-Driven / Type Object** | CSV → ScriptableObject 테이블, CharacterRow/AbilityRow 가 "종류"를, AnimationRow 가 애니메이션 재생 방식을 정의 | 코드 수정 없이 밸런스·콘텐츠 변경 |
| **Facade** | `Tables` 정적 클래스 (`Tables.Const`, `Tables.Text`, `Tables.Address`, `Tables.Animation`) | 테이블 접근 단순화 |
| **Builder (에디터)** | `WorldBuilder`, `ContentBuilder` | 씬/프리팹을 재현 가능하게 코드로 생성 |
| **Component** | Actor = ASC + Motor + Animator + (Blood/Sun/Inventory/Equipment 또는 Brain) | Unity 컴포넌트 조합 |
| **Adapter + Dependency Inversion (Bridge)** | `ISkyController` / `SkyBridge` (Game.Runtime) ← `WeatherMakerSkyBridge` (Assembly-CSharp, `Game/Integration`) | Game.Runtime 은 WeatherMaker(asmdef 없음)를 참조할 수 없음. 인터페이스는 게임에, 구현은 외부 쪽에 두고 런타임에 등록 → WeatherMaker 가 없어도 기본 낮밤으로 동작 |
| **Null Object / Fallback** | `DayNightManager.apply` (SkyBridge.Controller 가 null 이면 자체 Directional Light) | 외부 에셋 제거 시에도 게임 유지 |
| **Builder + 캐시 (에디터)** | `MaterialUpgradeUtil` (원본 머티리얼 → URP 복사본, 같은 원본은 한 번만 변환) | 외부 에셋을 수정하지 않고 파이프라인에 맞춤 |
