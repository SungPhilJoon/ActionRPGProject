# 코드 폴더 / 어셈블리 구조 (2026-10-13 재정리, 2026-10-14 Integration 추가)

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
    Util/        HashUtil, MathUtil, ComponentUtil
    Event/       EventBus<T>
    Pool/        GameObjectPool
    Asset/       AssetLoader (Addressables 래퍼, (주소,타입) 캐시, 소유자 검사)
    Data/        CsvReader, CsvConvert, TableAsset<TRow>
    Ability/     GAS — AbilitySystemComponent, AttributeSet, GameplayEffect, GameplayAbility, AbilityTask, GameplayCue, GameplayTagManager/GameplayTagContainer/GameplayTagCountContainer
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
    Data/        테이블 18종 행/에셋 클래스 + GameTables(Tables 파사드)
    Ability/     GameAttributeSet, EffectLibrary, GameExecutions, GameAbility + 구현(근접/투사체/대시/범위/버프/흡혈/도약)
    Combat/      CombatUtil(히트 처리), CombatLog, Projectile, CueSpawner, TimedDespawn
    Actor/       Actor, ActorMotor, ActorAnimator, ActorRegistry
    Player/      PlayerCharacter, PlayerController, BloodSystem, SunExposure
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
    Content/     ContentBuilder(.Characters = 플레이어/몬스터/상인/무기, .Structures = 성 모듈/채집 노드, .Vfx), AddressablesSetup
    World/       WorldBuilder(.Terrain = 높이맵/레이어/풀/나무, .Landmarks = 바위/마을/아레나/호수, .Sky = WeatherMaker)
    AI/          MonsterStateTreeDefaults (+ Game > AI > StateTree Editor 메뉴)
    Tools/       BalanceToolWindow, UIFactoryWindow
  Integration/                      (asmdef 없음 → Assembly-CSharp)
    WeatherMaker/ WeatherMakerSkyBridge (ISkyController 구현, 씬 로드 시 자동 부착)
```

## 4. 이번 재정리에서 바뀐 것

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
| `StateTreeEditorWindow` (Game.Editor, EBBKey·EditorPaths 직접 참조) | Framework.Editor 로 이동, 게임 의존 제거 (어트리뷰트 연결, 저장 위치는 현재 에셋 폴더 기준) |
| `CsvTableImporter` 의 범용 변환·자동 재변환·폴더 생성 | `TableImportUtility`, `TableImportPostprocessor`, `EditorAssetUtil` (Framework.Editor) |
| CSV 자동 재변환: 경로 규칙(`Data/CSV/{이름}.csv`)으로 테이블 탐색 | 테이블의 `Source` 참조로 탐색 → 폴더 구조와 무관 |

## 5. 이동하면서 지킨 것 / 확인한 것

- 모든 이동은 `AssetDatabase.MoveAsset` 으로 수행 → `.meta` GUID 유지 → **프리팹·씬의 스크립트 참조가 끊기지 않음**
  (확인: Content 프리팹 / World 씬 Missing Script 0, GameBootstrap 의 GameTables 참조 유효)
- **어셈블리 이름 변경 부작용**: `[SerializeReference]` 는 타입의 어셈블리 이름을 저장하므로, StateTree 에셋 안의 Framework 노드(`WaitTask` 등)가 Missing 이 됐다.
  → Framework 노드에 `[MovedFrom(true, "Framework.StateTree", "Game.Framework")]` 를 붙여 기존 데이터를 그대로 읽게 하고, 에셋 3종을 다시 저장했다 (Issues.md 17).
- 재정리 후 Play 확인: 몬스터 17마리 StateTree 실행, 전투 기록 정상, 콘솔 에러 0.

## 6. 새 코드를 어디에 둘지

| 만들 것 | 위치 |
|---|---|
| 어떤 게임에서도 쓸 수 있는 시스템/유틸 (게임 enum·테이블을 모름) | `Framework/Runtime/<영역>` |
| 그 시스템의 범용 에디터 | `Framework/Editor/<영역>` |
| 이 게임의 규칙·데이터를 아는 코드 | `Game/Runtime/<영역>` |
| 이 게임 콘텐츠를 만드는 에디터 툴 | `Game/Editor/<영역>` |
| 새 몬스터 StateTree 노드 | `Game/Runtime/Monster/StateTree` (MonsterTask/Condition/Evaluator 상속) |
| asmdef 없는 외부 에셋과의 연결 | 인터페이스는 `Game/Runtime`, 구현은 `Game/Integration/<에셋명>` |
| 새 UI 창 | `Game/Runtime/UI/Windows` (UI Factory 툴이 여기로 생성) |
