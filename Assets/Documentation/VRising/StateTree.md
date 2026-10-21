# StateTree (언리얼 StateTree 의 Unity 구현) + 몬스터 AI 적용

몬스터 AI 를 하드코딩 FSM(`MonsterBrain` 의 switch 문)에서 **언리얼 StateTree 방식의 데이터 에셋**으로 옮겼습니다.
기획자는 코드를 고치지 않고 **Window > Framework > StateTree Editor (또는 Game > AI > StateTree Editor)** 에서 몬스터 행동을 편집합니다.

## 1. 언리얼 StateTree 와의 대응

| 언리얼 | 이 프로젝트 | 파일 |
|---|---|---|
| UStateTree (에셋) | `StateTreeAsset` (ScriptableObject) | Framework/Runtime/StateTree/StateTreeAsset.cs |
| UStateTreeState | `StateTreeState` (Id, ParentId, Selection, EnterConditions, Tasks, Transitions) | 〃 |
| FStateTreeTransition | `StateTreeTransition` (Trigger, EventId, Type, TargetStateId, Delay, Conditions) | 〃 |
| FStateTreeTaskBase | `StateTreeTask` (EnterState / Tick / ExitState → Running/Succeeded/Failed) | StateTreeNodes.cs |
| FStateTreeConditionBase | `StateTreeCondition` (Test, Invert, And/Or Operand) | 〃 |
| FStateTreeEvaluatorBase | `StateTreeEvaluator` (TreeStart / Tick / TreeStop) | 〃 |
| Global Tasks | `StateTreeAsset.GlobalTasks` | 〃 |
| FStateTreeExecutionContext + InstanceData | `StateTreeInstance` + `StateTreeContext` (노드는 인스턴스마다 복제 → 노드의 [NonSerialized] 필드가 InstanceData 역할) | StateTreeInstance.cs |
| Schema / Context (AIController, Pawn) | `MonsterBrain` (Context.Owner) + `MonsterTask/Condition/Evaluator` 의 `Brain(ctx)` 헬퍼 | Game/Runtime/Monster |
| Property Binding | **블랙보드 int 키** (`EBBKey`) — 평가기가 쓰고 조건/태스크가 읽음 | StateTreeBlackboard |
| Gameplay Tag 이벤트 | `SendEvent(int)` + `[StateTreeEventEnum] EAIEvent` | StateTreeTypes.cs |
| StateTree 에디터 / 디버거 | `StateTreeEditorWindow` (트리 뷰 + Details + Live Debug) | Framework/Editor/StateTree |
| UStateTreeComponent | `MonsterBrain` (Update 에서 Tick) | Game/Runtime/Monster/MonsterBrain.cs |

## 2. 실행 규칙 (StateTreeInstance)

한 틱의 순서:
1. **Evaluators** Tick → 블랙보드 갱신 (`MonsterPerceptionEvaluator`: 타깃 탐색, HasTarget/TargetDistance/HomeDistance/HealthRatio …)
2. **Global Tasks** Tick
3. 활성 경로(루트 → 리프)의 **모든 상태의 Task** Tick. 상태 안에서 태스크 하나라도 끝나면 그 경로가 완료 (Failed 우선)
4. **Transitions** 검사: 리프 → 루트 순서, 상태 안에서는 위에서부터. 처음 통과한 하나만 실행
   - Trigger: `OnStateCompleted / OnStateSucceeded / OnStateFailed / OnTick / OnEvent`
   - Type: `GotoState(Id) / NextState(다음 형제) / Root(처음부터 다시 선택)`
   - Delay > 0 이면 대기 후 실행 (그 사이 상태가 바뀌면 취소)
5. 완료됐는데 처리한 트랜지션이 없으면 루트부터 다시 선택

**상태 선택**: 대상 상태의 Enter Conditions 통과 → 자식이 있으면 `Selection` 규칙(`TrySelectChildrenInOrder / TryEnterState / TrySelectChildrenAtRandom`)으로 자식을 재귀 선택.
공통 조상은 유지하고 대상 상태부터 아래만 Exit/Enter 한다 (같은 상태로 Goto 하면 재진입).
태스크가 없는 리프는 진입 즉시 Succeeded.

문자열 규칙: 상태는 **int Id** 로 참조하고 이름은 표시용입니다. 이벤트는 enum(int), 블랙보드 키도 enum(int).

## 3. 제공 노드

| 종류 | 노드 | 설명 |
|---|---|---|
| Evaluator | MonsterPerceptionEvaluator | 어그로 범위 탐색, 놓치기 거리, 블랙보드 갱신 |
| Condition | HasTargetCondition | 타깃 유무 |
| | BlackboardCompareCondition | `EBBKey` 값 비교 (<, <=, >, >=, ==, !=) |
| | InAttackRangeCondition | AttackRange × 배율 이내 |
| | LeashExceededCondition | 집에서 LeashRange 초과 (0 = ConstTable) |
| | AbilityReadyCondition | 슬롯 목록 중 사용 가능한 어빌리티 존재 |
| | TimeInStateCondition / RandomChanceCondition / AlwaysTrueCondition | 유틸 |
| Task | WanderTask, ChaseTargetTask, KeepDistanceTask, ReturnHomeTask, FaceTargetTask | 이동 |
| | UseAbilityTask | 슬롯 우선순위로 GAS 어빌리티 발동, 끝나면 Succeeded |
| | ApplyEffectToSelfTask | 진입 시 EffectTable 이펙트 적용 (예: 2페이즈 격노) |
| | WaitTask, SendEventTask, DebugLogTask | 공용 (Framework) |

새 노드 추가: `MonsterTask`(또는 Condition/Evaluator)를 상속한 `[Serializable]` 클래스를 만들면 에디터의 `+ Add` 메뉴에 자동으로 나타납니다 (`[StateTreeNodeInfo("카테고리", "설명")]` 로 분류).

## 4. 기본 몬스터 트리 (Content/AI, Addressables 주소 `AI/ST_*`)

CharacterTable 의 `StateTree` 컬럼(AssetTable 300~302)으로 몬스터마다 지정합니다.

```
ST_MeleeMonster (메두사, 트롤)          ST_RangedMonster (코브라)         ST_Boss (미노타우로스)
Root                                     Root                              Root
├ Return   [Leash 초과] ReturnHome        ├ Return                          ├ Return [Leash 30m]
├ Combat   [HasTarget]                   ├ Combat [HasTarget]              ├ Combat [HasTarget]
│  ├ Attack [AbilityReady] UseAbility    │  ├ Retreat [거리<4] KeepDistance │  ├ Phase2 [HP<=50%] ApplyEffect(격노)
│  ├ Chase  [사거리 밖] ChaseTarget       │  ├ Attack / Chase / Hold        │  │   ├ Attack (Spell1,Q,E,평타) / Chase / Hold
│  └ Hold   FaceTarget + Wait 0.25        │                                │  └ Phase1 (OnTick HP<=50% → Phase2)
│  (OnTick: 타깃 없음 → Root, Leash → Return)                               │      ├ Attack (E 투석, 평타) / Chase / Hold
└ Idle  (OnEvent TargetAcquired → Combat) └ Idle                           └ Idle (제자리 대기)
   ├ Wander → (Next) Rest → (Root)
```

메뉴 **Game > AI > Create Default Monster StateTrees (Overwrite)** 로 다시 만들 수 있습니다 (툴에서 고친 내용은 덮어써짐).

## 5. 에디터 툴 사용법 (Window > Framework > StateTree Editor (또는 Game > AI > StateTree Editor))

- 프로젝트 창에서 StateTree 에셋을 **더블클릭**하면 열립니다. `New` 로 새 트리 생성.
- **왼쪽**: Evaluators / Global Tasks 목록, 상태 트리 뷰 (색 막대 = 상태 색, 회색 글씨 = 태스크 요약, 마우스를 올리면 Enter 조건과 트랜지션 툴팁)
  - `+ Root State`, `+ Child`, `+ Sibling`, `▲▼`(형제 순서 = 선택 우선순위), `Delete`(자식 포함, 이 상태를 가리키던 트랜지션 대상은 비움)
- **오른쪽 Details**: 이름/활성/Selection/색, Enter Conditions·Tasks (`+ Add` 메뉴는 카테고리별), Transitions (Trigger, Event 팝업, Type, Target State 팝업, Delay, Conditions)
- `Validate`: 없는 대상 상태, 태스크·트랜지션 없는 리프, 삭제된 노드 클래스 검사
- **Live Debug** (Play 중): 툴바의 `Debug:` 에서 몬스터 선택 → 활성 상태 초록 강조 + 경과 시간, 블랙보드 값, 최근 트랜지션 로그(사유 포함)
- 모든 편집은 Undo/Redo 가능 (SerializedProperty 기반)

## 6. 검증 결과 (Play 모드에서 확인)

- 몬스터 17마리 모두 트리 실행 (Idle/Wander/Rest 순환)
- 플레이어 접근 → `TargetAcquired` 이벤트 → Combat 진입, Attack → Hold → Attack 반복 (근접/궁수)
- 보스: 체력 40% 로 설정 → `Phase1 → Phase2` OnTick 트랜지션 발생, 2페이즈 슬롯 사용
- 보스를 집에서 35m 떨어뜨림 → Return(무적) → 귀환 후 체력 100% 회복 → Idle
- 에디터 창: 트리 뷰, Details, 트랜지션 편집 화면 정상 표시

## 7. 언리얼과 다른 점 / 한계 (솔직하게)

| 항목 | 차이 |
|---|---|
| Property Binding | 바인딩 UI 대신 블랙보드 키(enum)로 대체 |
| Linked / Subtree 상태 | 미구현 (트리 재사용은 에셋 복제로) |
| Transition Priority | 우선순위 enum 없음 — 리스트 순서가 우선순위 |
| Tree Succeeded/Failed 종료 | 몬스터는 계속 돌아야 하므로 `Root`(재시작)로 단순화 |
| 상태 완료 규칙 | "태스크 하나라도 끝나면 완료" 고정 (UE 5.4+ 의 Any/All 옵션 없음) |
| 그래프 뷰 | 언리얼과 같은 트리 뷰 + Details 방식 (노드 그래프 아님) |
| 네트워크 | 서버 전용 실행을 전제로 설계 (클라이언트 복제 없음) |
| 성능 | 노드는 몬스터마다 JSON 복제(스폰 시 1회). 몬스터 수가 많아지면 공유 노드 + 분리된 InstanceData 구조로 바꿀 지점 |
