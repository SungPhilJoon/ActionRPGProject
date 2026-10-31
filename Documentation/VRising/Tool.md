# 툴

에디터 툴의 원본 문서입니다. 툴을 바꾸면 이 문서를 고치고, 다른 문서에는 링크만 둡니다.

언리얼과 같은 분담으로 몬스터를 만듭니다.

| 언리얼 | 이 프로젝트 | 하는 일 |
|---|---|---|
| GameplayAbility / GameplayEffect / AbilitySet | **GAS Tool** (Game > Tools > GAS Tool) | 어빌리티·이펙트 만들기, 캐릭터별 보유 어빌리티 |
| StateTree 에디터 | **StateTree Tool** (Game > Tools > StateTree Tool) | 몬스터 AI 만들기 |
| Character Blueprint | **MonsterAuthoring 인스펙터** (`Assets/Game/Data/Monsters/*.asset` 선택) | 능력치·외형·애니메이션 클립·AI 트리 지정·프리팹 빌드 |
| Automation / Functional Test | **Test Runner** (Window > General > Test Runner, PlayMode) | GAS · StateTree · 플레이 시나리오 검증 → [README.md 7절](README.md#7-테스트) |
| Asset Audit / Reference Viewer | **의존성 지도** (Game > Tools > Dependency Map) | Addressables 의존성 그래프 · 이슈 · 테스트 기록 · 실시간 참조 → [Dependency.md](Dependency.md) |

```
GAS Tool: GameplayEffect → Ability → Ability Set(몬스터에 붙이기)
                                          │
MonsterAuthoring 인스펙터: 능력치 · 외형 · 클립 · AI 트리 지정 → 저장 + 프리팹 빌드
                                          │
StateTree Tool: 템플릿으로 트리 → + 블록(조건부 어빌리티 등) → Play 에서 "이 트리로 스폰" + 라이브 디버그
```

## 1. GAS Tool

- **Ability 탭** — 새 Ability = `AbilityGraphAsset`(`Content/Abilities`, 태그 규칙 · BloodPool 비용 · 실행 단계) + AbilityTable 행(`Type=Graph`, 이름 · 슬롯 · 쿨다운 · AI 사용 거리).
  - 만들기: 툴바 또는 목록 아래 `+ 새 Ability` → **새 Ability 창** (왼쪽 템플릿 선택 → 오른쪽에 슬롯 · 쿨다운 · AI 거리 · 태그 · 단계 미리보기 → 표시 이름 · 에셋 이름(템플릿에 맞춰 자동 제안) · 몬스터용 → `만들기`).
  - 템플릿: 근접 / 원거리 / 범위 / 돌진 공격, 방어(가드 GE 함께 생성), 자기 강화, 빈 어빌리티.
  - 편집 순서는 언리얼 Details 와 같다: 기본 → Tags(Cancel / Block / ActivationOwned / Required / Blocked) → Cost → 단계 목록(`+ 단계`, ▲▼✕) → 타임라인 → 사용처.
  - 단계 13종: 대기 · 종료 / 애니메이션 재생(AnimationTable 행만 고름, 클립은 몬스터별) / 회전 · 이동 잠금 · 돌진 / 부채꼴 · 원형 · 발사체 피해 / 자신·대상에게 GE(종료 시 제거 옵션) · 회복 / Cue.
  - 기존 타입 기반 어빌리티(MeleeCombo 등)는 읽기 전용으로 보여준다 (값은 AbilityTable.csv).
- **GameplayEffect 탭** — EffectTable 행: Duration Policy · Period · Modifiers 3개 · Execution · 태그 4종 · Stack · Cue. 프리셋: 방어 / 무적 / 가속 / 둔화 / 출혈. #1~#8 은 시스템 예약.
- **Ability Set 탭** — 캐릭터의 `CharacterTable.Abilities` (스폰 시 이 순서로 부여, 같은 슬롯은 뒤가 교체 → 경고).
- 저장: 바뀐 CSV 행만 쓴다(`CsvRow`). 그래프 에셋은 바로 기록되고 Undo 된다. Play 중 저장하면 테이블을 다시 바인딩해 새로 스폰한 몬스터부터 적용.

런타임 구조와 언리얼 대응은 [GAS.md 4.1](GAS.md#41-그래프-어빌리티-typegraph-언리얼-blueprint-어빌리티-방식).

## 2. StateTree Tool

프레임워크 StateTree 에디터([StateTree.md 5절](StateTree.md))에 게임 쪽 확장(`MonsterAIExtension`)이 몬스터 AI 작성 도우미를 붙인다. AI 만 다룬다.

- **New ▾ → 템플릿**: 근접 몬스터 / 원거리 몬스터(거리 유지) / 보스(체력 50% 2페이즈).
- **+ 블록** (선택한 상태 아래): 전투(타깃 있을 때) + 공격·추적·대기 / 공격·추적·대기 / 페이즈(체력 50% 이하) / 귀환 / 대기·배회 /
  **조건부 어빌리티 사용**(슬롯별, 체력 50% 이하 + AbilityReady → UseAbility, "모든 슬롯" 공격 상태에서는 그 슬롯을 뺀다 — 방어/버프용).
- **컨텍스트 패널**: 이 트리를 쓰는 몬스터, 그 어빌리티(슬롯 · 공격/방어 · AI 거리)와 사용하는 상태. 방어/버프가 "모든 슬롯" 공격 상태로만 쓰이면 경고. 미등록 트리는 AssetTable 등록 버튼.
- **툴바 (Play)**: 몬스터를 골라 `▶ 이 트리로 스폰` — 플레이어 앞에 스폰하고 이 트리로 AI 를 돌린다 (`MonsterBrain.RunTree`). 툴바 `Debug:` 로 라이브 디버그.

## 3. MonsterAuthoring 인스펙터

몬스터 한 마리 = `Data/Monsters/{이름}.asset` + CharacterTable 행. 에셋을 선택하면 인스펙터에서:
능력치(체력 · 공격력 · 저항 · 속도 · 레벨 · 감지/공격 거리 · 배율 · 혈액 · Loot) · AI 트리 지정 / 전용 트리 만들기 / StateTree Tool 열기 ·
보유 어빌리티 요약(GAS Tool 로 이동) · 외형(모델 · 스케일 · 반지름 + 3D 미리보기) · 트리거별 클립(● = 보유 어빌리티가 재생, 없으면 경고) ·
`저장` / `저장 + 프리팹 빌드`(ContentBuilder.BuildMonster → Addressables) / `복제해서 새 몬스터`.

## 4. 그 밖의 툴

- **Balance Tool** — Live(플레이 중 피해/DPS 집계) / Simulator(테이블 값으로 1타 피해 · 처치 타수). 게임 화면에서는 F8.
- **UI Factory** (2026-10-29 프리팹 방식으로 변경) — 새 창 스크립트 골격(직렬화 필드 + OnInitialize), **Rebuild All UI Prefabs**(빌더 + Addressables 등록), 창 프리팹 열기 · Ping, UI 스킨 선택. UI 구조 · 새 창 추가 순서는 [UI.md](UI.md).
  - `Game > Setup > 3b. Build UI Prefabs` — `UIPrefabBuilder` 가 `Content/UI` 아래 캔버스 · 창 · 위젯 · 레이어 프리팹 17개(+ 지도 표식 스프라이트)를 굽는다 (Run All 에 포함).
  - `Game > UI > Select UI Skin` — `Art/Generated/UI/UISkin.asset` (스프라이트 · 틴트 · 폰트).
- **의존성 지도 (Dependency Map)** (2026-10-29) — Addressables 정적 검사 · 의존성 테스트 실행 · 이슈 갱신 · 보고서, 탭 5개(한눈에 보기 / 그래프 / 이슈 / 테스트 기록 / 실시간). 기획 · 디자인도 읽을 수 있게 한국어 설명 · 도움말 · 색+기호 표시. Play 전 자동 검사 토글. 원본 문서 [Dependency.md](Dependency.md).
- **Localization 동기화** (2026-10-31) — `Game > Data > Sync Localization (StringTable → Unity Localization)`: StringTable.csv 의 영어 · 한국어를 Unity Localization GameText 테이블로 옮긴다. CSV 임포트 · StringTable.csv 저장 때 자동 실행. 상세 [UI.md](UI.md#언어-unity-localization-2026-10-31).
- **월드맵 굽기** (2026-10-31) — `Game > World > Bake World Map`: 월드 씬을 위에서 내려다본 지도 텍스처(`Content/UI/Map/WorldMap.png`)를 다시 굽는다. 월드 빌드 때 자동 실행. 굽고 나면 `3b. Build UI Prefabs`. 상세 [World.md 8절](World.md#8-월드맵-그림).
- **치트 콘솔(`)** — [Game.md 14절](Game.md#14-치트--콘솔).
- **식생 재생성 메뉴** (2026-10-29) — 절차 나무 · 텍스처의 모양 코드를 바꾼 뒤 실행하고, 이어서 `Game > Setup > 5. Build World Scene` 으로 월드에 다시 배치한다. 상세 [World.md 7절](World.md#7-식생--물-2026-10-29).
  - `Game > World > Rebuild Tree Prefabs` — 나무 4종 × 변형 12개 + 덤불 4종 프리팹 · 메시 · 머티리얼 (`Art/Generated/World/Foliage`). 메시 에셋은 덮어써서 GUID 유지.
  - `Game > World > Rebuild Foliage Textures` — 잎 · 바늘잎 · 자작 껍질 · 물 노멀 · 거품 절차 텍스처를 지우고 다시 그림 (없을 때는 빌더가 자동 생성).
  - 채집 나무 노드(PineTree/OakTree)는 `Game > Setup > 3. Build Content` 에서 같은 나무로 다시 만들어진다.

## 5. 코드 위치

| 경로 | 내용 |
|---|---|
| `Game/Runtime/Ability/Graph/` | `AbilityGraphAsset`, `AbilitySteps`(단계 13종), `GraphAbility`(실행기, `Create` 로 직접 생성 가능) |
| `Game/Editor/Tools/GasTool/` | GAS Tool 창(Ability / Effect / AbilitySet), 템플릿, `AbilityInfo`(요약·타이밍), `AbilityGuide`(기존 타입 설명 — GameAbilities.cs 와 같이 수정) |
| `Framework/Editor/StateTree/StateTreeEditorExtension.cs` | 템플릿 · 블록 · 컨텍스트 · 툴바 확장점 |
| `Game/Editor/AI/MonsterAIExtension.cs`, `MonsterStateTreeDefaults.cs` | 몬스터 AI 도우미, 기본 트리/블록 빌더 |
| `Game/Editor/AI/Monster/` | `MonsterAuthoring`(+인스펙터, Util), `MonsterPreview` |
| `Framework/Editor/Data/CsvRow.cs`, `CsvEditUtility.cs` | CSV 한 행 작업 사본 / 행 읽기 · 교체 · 추가 |
| `Game/Editor/World/Foliage/` | `TreeGenerator`(종별 골격 · LOD 메시), `TreeFactory`(프리팹 · 머티리얼 · 메뉴), `FoliageTextures` / `FoliageCanvas`(절차 텍스처) |
| `Game/Editor/UI/` | `UIPrefabBuilder`(+`.Hud`, UI 프리팹 굽기), `UISkinSetup`(Kenney CC0 → 고딕 스킨) |
| `Game/Editor/Tools/DependencyTool/` | `DependencyMapWindow`(+`.Graph`, `.Tabs`), `DependencyMapService`(이슈 갱신 · 플레이 전 검사 · 테스트 종료 훅), `GameDependencyUsage`(AssetTable 사용처) |
| `Framework/Editor/Addressables/` | `AddressablesDependencyAnalyzer`(정적 그래프 · 이슈), `DependencyIssueTracker`(기록 → 이슈, 상태 이력, Markdown), `DependencyModel` |
| `Game/Tests/PlayMode/` | Play 모드 테스트 (`DependencyScenarioTests` = 의존성 기록 시나리오) |

## 6. 제약

- 어빌리티 · GE · 몬스터 삭제 기능 없음 (에셋 + CSV 행 직접 삭제). 새 CSV 행은 파일 끝에 붙는다.
- 그래프 단계는 순서 실행만 (분기 · 입력 대기 없음) — 콤보처럼 상태가 필요한 동작은 기존 타입을 쓴다.
- 프리팹을 다시 구우면 fileID 가 바뀌어 git diff 가 크다. 기본 4종의 커밋된 프리팹은 외부 원본보다 예전 상태라 다시 구우면 원본 기준으로 바뀐다.
