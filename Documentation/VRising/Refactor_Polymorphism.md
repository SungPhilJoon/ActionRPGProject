# 리팩터링: enum 분기 → 상속 · 다형성, UI 레이어, 토스트, 드래그 앤 드롭 (2026-11-01)

포트폴리오용 비교 문서입니다. **변경 전 코드는 `backup/before-ui-polymorphism` 브랜치**에 그대로 있고, 변경 후는 `master` 입니다.

## 0. 비교하는 방법

```bash
# 바뀐 파일 목록
git diff --stat backup/before-ui-polymorphism master -- Assets/Framework Assets/Game

# 파일 하나씩 (예: 퀘스트)
git diff backup/before-ui-polymorphism master -- Assets/Game/Runtime/Quest/QuestManager.cs
git show backup/before-ui-polymorphism:Assets/Game/Runtime/Quest/QuestManager.cs   # 변경 전 전체

# 변경 전 상태로 실행해 보기 (작업 트리를 깨끗하게 한 뒤)
git switch backup/before-ui-polymorphism   # 돌아오기: git switch master
```

| 브랜치 | 내용 |
|---|---|
| `backup/before-ui-polymorphism` | 리팩터링 직전 기준점 (커밋 4a714905) — enum switch, int UI 레이어, HUD 한 줄 알림, 클릭 전용 인벤토리 |
| `master` | 이 문서의 변경 후 |

## 1. 핵심 아이디어 — enum 은 "데이터 키", 동작은 "클래스"

CSV · 직렬화 데이터는 여전히 enum 값(`EQuestObjective.Kill`, `EModifierOp.Add` …)을 쓴다. 바뀐 것은 **그 값에 따라 무엇을 할지**를 정하는 곳이다.

| | 변경 전 | 변경 후 |
|---|---|---|
| 동작 위치 | 사용하는 쪽의 `switch (row.Type)` 안 | `[Kind(enum값)]` 을 단 하위 클래스의 virtual 메서드 |
| 새 종류 추가 | enum 값 + 모든 switch 를 찾아 case 추가 | enum 값 + 클래스 하나 (기존 코드 수정 없음 — 개방/폐쇄 원칙) |
| 빠진 처리 | default 로 조용히 넘어감 | 테스트(`KindRegistry_EveryDataEnumHasClass`)가 빠진 클래스를 잡음 |

공통 장치는 Framework 의 `KindRegistry<TBase>` (`Framework/Runtime/Core/KindRegistry.cs`): `[Kind]` 를 단 하위 클래스를 한 번 찾아 키 → 타입 표를 만들고, `Get`(상태 없는 공유 전략) / `Create`(새 인스턴스)를 준다.

```csharp
[Kind(EAbilityType.Dash)]
public class DashAbility : GameAbility { ... }

var ability = KindRegistry<GameAbility>.Create((int)row.Type);   // switch 대신 한 줄
```

### 1.1 바꾼 분기 (runtime 의 `switch` 21곳 → 0)

| 영역 | enum | 변경 전 | 변경 후 (클래스) |
|---|---|---|---|
| 어빌리티 생성 | `EAbilityType` | `AbilityFactory` switch 8 case | 어빌리티 클래스에 `[Kind]` — 팩토리는 레지스트리 한 줄 |
| 퀘스트 목표 | `EQuestObjective` | `QuestManager` switch 3곳 + 이벤트마다 if | `QuestObjective` + 10 하위 클래스 (`QuestObjectives.cs`) — 이벤트 가상 메서드, 장소 찾기 |
| 스폰 | `ESpawnKind` | `SpawnManager` switch + 시작 지점 if | `SpawnHandler` 4종 (`SpawnHandlers.cs`) |
| 건축물 | `EStructureType` | `BuildManager` 스냅 switch · 성 심장/관 if 4곳 · `Structure` 상호작용 switch · `BuildWindow` if | `StructureKind` 6종 (`StructureKinds.cs`) — 스냅, 성 심장 여부, 설치 직후, 상호작용, 지도 표시 |
| 채집 | `EHarvestType` | `ResourceNode` switch | `HarvestKind` 3종 |
| 에셋 프리로드 | `EAssetKind` | `GameBootstrap` switch | `AssetPreloader<T>` 4종 |
| 애니메이션 파라미터 | `EAnimParamType` | `AnimationParameter.Apply` switch | `AnimationParameterOp` 4종 |
| 속성 클램프 | `EAttribute` | `GameAttributeSet` switch | `AttributeRule` (ClampToAttribute / Minimum / Range) 표 |
| 아이템 클릭 | `EItemType` | 인벤토리 창 if (장비 / 소비) | `ItemUse` (장착 / 소비) |
| GAS 모디파이어 | `EModifierOp` | `AttributeSet` switch 2곳 | `ModifierOperation` 3종 + `ModifierSum` (Framework) |
| GAS 크기 계산 | `EMagnitudeType` | `GameplayEffectSpec` switch | `MagnitudeCalculation` 4종 (Framework) |
| StateTree 비교 | `ECompareOp` | `CompareUtil` switch 2곳 | `CompareOperation` 6종 (Framework) |
| StateTree 트랜지션 | `ETransitionTrigger` / `ETransitionType` | `StateTreeInstance` switch 2곳 | `TransitionTrigger` 5종 / `TransitionTarget` 3종 (Framework) |
| 의존성 기록 | `EAssetEvent` | `DependencyRecord` switch 7 case | `AssetEventAccumulator` 5종 (한 클래스가 키 여러 개) |

동작이 없는 1:1 대응(퀘스트 묶음 이름, 토스트 경로 문구)은 클래스 대신 **표(배열)** 로 두었다 — 다형성은 "동작이 다를 때" 쓴다.

## 2. 예시 — 변경 전 / 후

### 2.1 퀘스트 목표

변경 전 (`QuestManager`, 요약):

```csharp
void Update(...)
{
    switch (Current.Objective)
    {
        case EQuestObjective.Move: trackMove(player); break;
        case EQuestObjective.GearLevel: checkState(player); break;
    }
}

void onItemObtained(ItemObtainedEvent e)
{
    if (Current.Objective == EQuestObjective.Gather && e.Source == EItemSource.Harvest) addProgress(e.Count);
    else if (Current.Objective == EQuestObjective.Craft && e.Source == EItemSource.Craft) addProgress(1);
    else if (Current.Objective == EQuestObjective.Buy && e.Source == EItemSource.Shop) addProgress(1);
}
// + TryGetGuidePosition 의 switch, checkState 의 switch, 이벤트마다 isActive(EQuestObjective.X) 검사 …
```

변경 후: 매니저는 체인 · 진행도만, 무엇을 셀지는 목표 객체가 안다.

```csharp
// QuestManager
private void onItemObtained(ItemObtainedEvent e) { active?.OnItemObtained(e); }

// QuestObjectives.cs
public abstract class ItemSourceObjective : QuestObjective
{
    protected abstract EItemSource Source { get; }
    protected virtual int CountOf(ItemObtainedEvent e) { return 1; }
    public override void OnItemObtained(ItemObtainedEvent e)
    {
        if (e.Source == Source && MatchesTarget(e.ItemId)) AddProgress(CountOf(e));
    }
}

[Kind(EQuestObjective.Gather)]
public sealed class GatherObjective : ItemSourceObjective
{
    protected override EItemSource Source => EItemSource.Harvest;
    protected override int CountOf(ItemObtainedEvent e) => e.Count;      // 채집은 개수
    public override bool TryGetGuidePosition(...) { /* 채집 노드 지역 */ }
}
```

### 2.2 GAS 모디파이어 (Framework, 매 프레임 경로)

```csharp
// 변경 전
switch (list[i].Op)
{
    case EModifierOp.Add: add += m; break;
    case EModifierOp.MultiplyPercent: percent += m; break;
    case EModifierOp.Override: hasOverride = true; overrideValue = m; break;
}

// 변경 후 — 연산 객체는 AddModifier 때 한 번 찾아 모디파이어에 들고 있다 (루프 안 사전 조회 없음)
list[i].Operation.Accumulate(ref sum, list[i].Magnitude);
float newValue = sum.Resolve(BaseValues[attribute]);
```

## 3. UI 레이어 — 각 UIBehaviour 가 자기 레이어를 고른다

| | 변경 전 | 변경 후 |
|---|---|---|
| 레이어 | 게임 `UILayerId` int 상수 | Framework `UILayer` 추상 클래스 + World / Hud / Window / Popup / **Toast** / System |
| 어디에 넣나 | 호출마다 `ui.Create<HudWindow>(UILayerId.Hud, prefab)` | `UIBehaviour.GetLayer()` (virtual) → `ui.Create<HudWindow>(prefab)` 가 그 레이어 캔버스 자식으로 |
| 입력 차단 | 창마다 `override bool BlocksGameInput => false` | 레이어 정책 `UILayer.BlocksGameInput` 이 기본값 (Window · System 만 막음) |
| ESC 로 닫기 | "입력을 막는 창" 을 모두 닫음 (타이틀도 대상이었음) | `UILayer.ClosesOnCancel` — 창 레이어만 |

```csharp
public class ToastWindow : UIWindow
{
    public override UILayer GetLayer() { return UILayer.Toast; }
}
```

## 4. 토스트 알림

- 변경 전: HUD 가운데 글자 한 줄 (`HudWindow.Toast`) 을 덮어씀 → 몬스터 전리품 3종이 연달아 들어오면 마지막 것만 보였다. 아이템 이름만, 아이콘 없음.
- 변경 후: `ToastWindow` (Toast 레이어, 오른쪽 아래에 위로 쌓임). 알림 종류마다 클래스 — `ItemToast`(아이콘 · 이름 · +개수 · 채집/제작/받음/전리품/보상), `TextToast`(문구). **같은 아이템은 한 줄로 합침**(`TryMerge`: 목재 +3, +2 → +5). 페이드 인/아웃, 최대 6줄.
- 아이템 획득은 모두 `ItemObtainedEvent` 하나로 (전리품도 이제 같은 이벤트) — 채집 · 제작 · 상점의 중복 알림을 지웠다.

## 5. 인벤토리 드래그 앤 드롭 — 더블 디스패치

칸이 "인벤토리 칸" 인지 "장비 부위" 인지에 따라 드롭 결과가 4가지(옮기기 · 합치기/바꾸기 · 장착 · 벗기 · 거절)로 갈린다. `if (source is …) if (target is …)` 대신 두 번의 가상 호출로 고른다.

```csharp
// 끌어온 칸.DropOnto(놓은 칸)  →  놓은 칸.ReceiveFrom{Inventory|Equipment}(...)
public sealed class InventorySlotBinding : ItemSlotBinding
{
    public override bool DropOnto(ItemSlotBinding target, PlayerCharacter p) => target.ReceiveFromInventory(Index, p);
    public override bool ReceiveFromInventory(int from, PlayerCharacter p) => p.Inventory.Move(from, Index);        // 옮기기 · 합치기 · 바꾸기
    public override bool ReceiveFromEquipment(EEquipSlot from, PlayerCharacter p) => p.Equipment.UnequipTo(from, Index); // 그 칸으로 벗기
}

public sealed class EquipSlotBinding : ItemSlotBinding
{
    public override bool DropOnto(ItemSlotBinding target, PlayerCharacter p) => target.ReceiveFromEquipment(Slot, p);
    public override bool ReceiveFromInventory(int from, PlayerCharacter p) => /* 이 부위 장비면 */ p.Equipment.EquipFromInventory(from);
    // ReceiveFromEquipment 는 기본값 false — 장비 → 장비는 거절
}
```

`ItemSlotView` 는 Unity `IBeginDragHandler / IDragHandler / IEndDragHandler / IDropHandler` 를 받아 창(`IItemSlotDragHost`)에 넘기기만 하고, 창은 끌리는 아이콘만 그린다. 장착할 때 벗은 장비는 끌어온 칸으로 들어가 자리가 바뀐 것처럼 보인다.

## 6. 검증

- PlayMode 테스트 `UIAndPolymorphismTests` 4개: 창 ↔ 레이어 캔버스, 레이어 정책, 토스트 합치기, 드래그 앤 드롭 규칙 6가지, **14개 enum 의 모든 값에 클래스가 있는지**.
- 기존 테스트(퀘스트 체인 · 전투 · 성 짓기 · StateTree · GAS · 지도 등)가 동작이 그대로인지 확인.
- Play 캡처: 모든 창이 레이어 캔버스(Hud / Window / Popup / Toast / System) 아래, 토스트 4종 + 합치기, 드롭으로 칸 이동 · 장착.
