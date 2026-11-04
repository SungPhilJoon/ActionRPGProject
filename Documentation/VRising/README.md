# BLOODLINE — V Rising 리버스 데모 (오프라인 1인 플레이 1차 구현)

[Project.md](../Project.md) 요구사항을 기준으로 만든 **V Rising 데모**입니다.
기존 `Assets/Scripts`(덱/던전 액션 RPG) 코드는 건드리지 않고, 새 게임은 전부 `Assets/Game/` 아래에 독립 어셈블리로 만들었습니다.

### 문서 지도

각 주제는 **한 문서에만** 자세히 쓰고, 다른 문서는 링크로 연결합니다.

| 문서 | 이 문서가 원본인 내용 |
|---|---|
| **README.md** (이 문서) | 실행 방법, 데이터 테이블 규칙, **테스트**, 코딩 규칙 적용 |
| **[Game.md](Game.md)** | **게임 설명서**: 흐름, 조작, 전투 · 상태이상, 흡혈 · 혈액, 햇빛, 몬스터 · 보스, 채집 · 제작 · 장비, 건축, 상점, 지역, HUD, 치트 |
| [Tool.md](Tool.md) | 에디터 툴 (GAS Tool, StateTree Tool, MonsterAuthoring 인스펙터, Balance Tool, UI Factory, 의존성 지도) |
| [UI.md](UI.md) | **UI 프리팹 + Addressables 구조**, 스킨, 새 창 추가 방법 |
| [Dependency.md](Dependency.md) | **Addressables 의존성 툴** (정적 검사, 런타임 추적, 의존성 테스트 기록, 이슈 규칙) |
| [DependencyIssues.md](DependencyIssues.md) | 의존성 이슈 현황 (툴이 자동 생성) |
| [Status.md](Status.md) | 기능별 상태, 규모, **알려진 문제**, 미구현 범위, 다음 단계 |
| [Architecture.md](Architecture.md) | 어셈블리 · 폴더 구조, 의존 규칙, 새 코드 위치, 디자인 패턴 |
| [GAS.md](GAS.md) | 언리얼 GAS 대응, 이펙트 · 어빌리티 흐름, 피해 공식, 태그 규칙, 상태이상 |
| [StateTree.md](StateTree.md) | 언리얼 StateTree 대응, 실행 규칙, 몬스터 트리, 에디터 사용법 |
| [World.md](World.md) | 지형 규격, 지역, 건축 모듈, 캐릭터 · 애니메이션 매핑, **나무 · 풀 · 강 · 호수**, 건축 시 식생 제거 |
| [CameraOcclusion.md](CameraOcclusion.md) | 카메라를 가리는 나무 · 건물 숨기기 (구조, 전략 교체 방법) |
| [URP.md](URP.md) | 조명 · 그림자, 하늘 · 낮밤, **포스트프로세싱 볼륨 6개 · 대기 패스 · 캐릭터 셰이더**, 머티리얼 변환, Terrain 렌더, **식생 · 물 셰이더** |
| [Issues.md](Issues.md) | 문제와 해결 (번호로 참조), **성능 관찰 포인트(18)** |
| [Optimization.md](Optimization.md) | **진행 상태 요약 + 최적화 툴 가이드** (Profiler · Frame Debugger · Render Graph Viewer · Rendering Debugger · Memory Profiler …, 측정 지점, 의심 지점별 확인 방법) |
| [AssetRequests.md](AssetRequests.md) | 사용 중인 외부 에셋, 부족한 에셋 요청 |
| [Multiplayer.md](Multiplayer.md) | 4인 협동 확장 설계 (미구현) |
| [CHANGELOG.md](CHANGELOG.md) | 날짜별 작업 기록 |
| [../Project.md](../Project.md) | 요구사항 · 개발 규칙 원문 |

---

## 1. 실행 방법

1. Unity 6000.3.24f1 로 프로젝트 열기
2. 메뉴 **Game > Setup > 0. Run All (First Time)**
   - 1. 레이어/물리 → 2. CSV 임포트 → 3. 콘텐츠(프리팹/VFX/아이콘) 빌드 → 4. Addressables 등록 → 5. 월드 씬 생성
3. `Assets/Game/Scenes/World.unity` 열고 Play → 타이틀에서 **Single Player (Offline)**

> 생성물(프리팹, 씬, 테이블 SO)은 이미 한 번 만들어 둔 상태입니다. CSV 만 수정했다면 저장 즉시 자동 재변환되므로 다시 Play 하면 됩니다.
> 독립 빌드(Player)에서는 실행 전에 **Game > Build > Build Addressables Content** 가 필요합니다.

## 2. 조작

조작 키 표는 **[Game.md 3절](Game.md#3-조작)** (키 배치는 `InputTable.csv`).

## 3. 게임 내용

무엇을 하는 게임인지, 전투 · 흡혈 · 햇빛 · 몬스터 · 건축 등 규칙과 수치는 **[Game.md](Game.md)** 에 정리했습니다.
한 줄 요약: 해를 피해 다니며 몬스터의 피를 마셔 강해지고, 성을 짓고 장비를 만들어 V Blood 보스(Minotaur Warlord)를 쓰러뜨리는 오프라인 1인 액션 RPG.

## 4. 폴더 구조

| 위치 | 내용 |
|---|---|
| `Packages/com.philjoon.framework/` | 게임을 모르는 재사용 기반 (GAS, StateTree, 매니저, CSV 테이블, UIFactory …). UPM 패키지, 별도 저장소 [Architecture.md 2.1](Architecture.md#21-패키지--별도-저장소-2026-11-04) |
| `Assets/Game/Runtime`, `Editor`, `Integration`, `Tests` | 이 게임의 코드 / Play 모드 테스트 |
| `Assets/Game/Data/Monsters` | 몬스터 저작 에셋 (MonsterAuthoring — 인스펙터에서 편집) |
| `Assets/Game/Data/CSV` → `Data/Tables` | 원본 데이터 19종 → ScriptableObject (런타임은 Tables 만 읽음) |
| `Assets/Game/Content/` | Addressables 루트. 파일 상대 경로(확장자 제외) = 주소 = AssetTable.Address |
| `Assets/Game/Art/` | `ThirdParty`(Kenney CC0), `Generated`(코드가 만든 재질·애니메이터·지형 데이터) |
| `Assets/Game/Scenes/World.unity` | 게임 씬 (WorldBuilder 가 생성) |
| `Assets/ExternalAssets/` | 외부 에셋 원본. 수정하지 않음 ([AssetRequests.md](AssetRequests.md)) |
| `Documentation/` | 이 문서들 (저장소 루트, Unity 밖) |
| `Tools/GitBatchPush/` | 대용량 저장소 분할 커밋/푸시 스크립트 (저장소 루트, Unity 밖) |

의존 방향과 폴더별 세부 내용은 [Architecture.md](Architecture.md).

## 5. 데이터 테이블 (CSV → ScriptableObject)

| 테이블 | 키 | 역할 |
|---|---|---|
| ConstTable | EConst | 낮 길이, 햇빛 피해, 혈액 소모, 레벨 보정, 카메라 등 상수 |
| TagTable | ETag | 게임플레이 태그 계층 (자식→부모) |
| StringTable | int | 모든 표시 문자열 |
| AssetTable | int | Addressables 주소 + 종류(`EAssetKind`: Prefab/Sprite/Material/Data) |
| CharacterTable | int | 플레이어/몬스터/보스/상인 능력치, 어빌리티, AI, 혈액, 드랍 |
| AbilityTable | int | 어빌리티 종류/슬롯/시전·쿨다운/범위/피해계수/부가 이펙트/큐 |
| EffectTable | int | GameplayEffect (지속 정책, 주기, Execution, 태그, 모디파이어 3개) |
| ItemTable / WeaponTable | int | 아이템(아이콘 = AssetTable 인덱스), 무기 → 어빌리티·채집 배율 |
| RecipeTable / StructureTable | int | 제작법, 건축물 |
| ResourceNodeTable / SpawnTable / LootTable | int | 채집 노드, 맵 배치, 드랍 |
| BloodTypeTable / ShopTable / CueTable / InputTable | enum/int | 혈액 단계 버프, 상점(가격 0), 연출, 키 바인딩 |
| AnimationTable | int | 애니메이션 재생 정의: Speed, Animator 파라미터들(`Type;Name;Value`), ApplyRootMotion |

규칙: 열거형은 CSV 에 **이름**으로 적고 임포트 때 int 로 변환. 배열은 `|`, 벡터/struct 는 `;`(struct 는 필드 선언 순서), `#` 으로 시작하는 컬럼/행은 메모.

애니메이션 재생은 `ActorAnimator.Play(AnimationTable Index, 속도)` 하나로 한다. `AbilityTable.Anims`(MeleeCombo 는 콤보 단계 순서),
`ResourceNodeTable.Anim`(채집), `ConstTable` 의 `BuildAnim`/`InteractAnim` 이 Index 를 참조한다. 최종 재생 속도 = `Speed` × 호출 속도(공격 속도)
→ 액션 상태의 speedParameter 인 `AttackSpeed` 에 들어간다. `EAnim` 은 이제 에디터 AnimatorController 빌더의 상태/트리거 슬롯 이름으로만 쓴다.
`Game > Data > Validate Tables` 가 없는 인덱스 참조, 가격 0 규칙, 에셋 파일 존재를 검사합니다.

## 6. 툴

에디터 툴의 상세 설명은 **[Tool.md](Tool.md)** 가 원본입니다.

- **GAS Tool** — 어빌리티 · GameplayEffect · Ability Set (캐릭터별 보유 어빌리티)
- **StateTree Tool** — 몬스터 AI (템플릿, `+ 블록`, 이 트리를 쓰는 몬스터·어빌리티 표시, Play 스폰)
- **MonsterAuthoring 인스펙터** — 몬스터 능력치 · 외형 · 클립 · AI 트리 지정 · 프리팹 빌드 · 복제
- **Balance Tool**(F8 오버레이) · **UI Factory**
- **치트 콘솔(`)** — 명령 목록은 [Game.md 14절](Game.md#14-치트--콘솔)

## 7. 테스트

Unity Test Framework (Window > General > Test Runner → **PlayMode** → Run All). `Assets/Game/Tests/PlayMode`, 17개, 약 55초 (성 짓기 쇼케이스 10초 포함).
첫 테스트가 World 씬을 로드하고 싱글 플레이를 시작하며, 테스트마다 월드 몬스터를 치우고 밤으로 고정한 뒤 플레이어를 초기화합니다 (`GamePlayTestBase`).

| 묶음 | 테스트 | 확인하는 것 |
|---|---|---|
| GAS (`GasAbilityTests`) | 가드 / 부채꼴 피해 / 발동 차단 태그 / 쿨다운 | 종료 시 GE·태그 제거, 이 어빌리티 번호의 피해 이벤트, BlockedByTags, Cooldown |
| 보스 격노 (`BossEnrageTests`) | 직접 발동 / 보스 AI Phase2 | GAS Tool 로 만든 #603(GA_BossEnrageBuff → GE #101)을 실제 테이블 · 에셋 · ST_Boss 로 실행: 시전 태그 · 이동 잠금 → GE 적용 → 속성 = EffectTable 행 모디파이어대로 → 쿨다운 → 만료 후 복귀 / Phase1 에선 안 쓰고 체력 45% Phase2 에서 Spell1 로 사용. 기댓값은 행에서 읽으므로 수치 조정에 테스트 수정 불필요. 직접 발동은 `CloseUpCamera`(보스 정면 근접 · UI 숨김 · 15시)로 보여 주고 시전 ~ 격노 직후를 0.25배속으로 재생, 화면 `Temp/BossEnrage_0_Idle · 1_Cast · 2_Enraged.png` |
| GAS 시연 (`GasShowcaseTests`, **[Explicit]**, Category `Showcase`) | 몬스터 한 마리 만들기 (약 60초, OBS 녹화용) | 화면 자막과 함께 AttributeSet → GE(분노 +50% 공격력) → Ability 2개(강타 · 분노의 포효) → Ability Set → StateTree → 전투 → 체력 40% 에서 분노 → 같은 강타 피해 ×1.5 → 만료 후 공격력 복귀. Run All 에서 빠지고 Test Runner 에서 골라 **Run Selected** 로 실행. 템포는 `Pace` 상수 |
| StateTree (`StateTreeTests`) | 기본 트리 공격 / 조건부 어빌리티 | 감지 → Combat → 피해, 체력 50% 초과에선 안 쓰고 40% 에서 가드 사용 |
| 시나리오 (`PlayScenarioTests`) | **성 짓기** | 빈 평지에 5×5칸(15m) 성을 처음부터 완성: 성 심장 → 바닥 25 → 외벽 20(남쪽 문, 3면 창) → 기둥 20 → 작업대·제련소·관·램프 4 (총 73개). 단계마다 설치 성공 · 개수 · 재료 차감 · 관 리스폰 지점 확인, 잘못된 설치 거부 확인 → **완성된 성을 10초간 카메라로 한 바퀴 돌며 보여줌** (HUD 숨김, 15시 햇빛, 스크린샷 `Temp/CastleShowcase_1·2.png`) |
| | **몬스터 전투** | 몬스터가 플레이어 공격 → 플레이어 평타로 처치, 처치자 = 플레이어 |
| | **보스 전투** | 교전 이벤트 → Phase1 → 체력 45% 에서 Phase2 + 격노 버프 → 보스 어빌리티 사용 → 처치 |
| 의존성 (`DependencyScenarioTests`, Category `Dependency`) | 부팅 / 창 열고 닫기 / 몬스터 스폰 · 제거 / 건축 고스트 / 어빌리티 8칸 | 각 동작을 3번 반복하며 Addressables 로드 · 반납 · 참조 카운트 · 동기 대기를 `DependencyReports/Runs` 에 기록. 로드 실패만 실패 처리, 나머지는 의존성 지도 이슈로 추적 ([Dependency.md](Dependency.md)) |
| 월드 (`WorldFoliageTests`) | 건축 시 식생 제거 / 카메라 가림 / 물 회피 스폰 | 건축 이벤트 바운드 안 나무 비활성 · 풀 0 · TerrainData 는 런타임 복제본 / 나무 앞에 서면 나무가 ShadowsOnly, 멀어지면 원래 모드 / 채집 노드가 WaterBody 안에 없음 |

플레이어 행동은 입력 대신 게임 API(`BuildManager.PlaceAt`, `Actor.TryActivate`)로, 테스트용 어빌리티·트리는 코드로 만들어 `GraphAbility.Create` · `Actor.GiveAbility` · `MonsterBrain.RunTree` 로 넣습니다.
Play 프레임은 에디터가 앞에 있어야 진행됩니다.
World 로드 중 오류 로그가 하나라도 나오면 테스트가 실패한다 (2026-10-30 WeatherMaker 를 제거해 예상 로그 처리도 지움).

## 8. 코딩 규칙 적용

규칙 원문은 [../Project.md](../Project.md). 이 코드에서 지킨 방식:

- 문자열 비교/하드코딩 대신 int 인덱스·enum·계층 태그 비트셋(`GameplayTagContainer`, 언리얼과 같은 비교 규칙)·FNV 해시(`HashUtil`) 사용
- 조건식은 `== null`, `== false`, `== true` 로 명시 (`!` 미사용), `_` 접두 필드 없음, private 메서드 소문자 시작, 매개변수 `In` 접두 (frameworkLib 스타일)
- 공통 로직은 `Framework/Runtime/Util`, 게임 공통은 `CombatUtil`, `UIUtil`, `LootUtil`, `TagUtil`

예외(솔직하게 남기는 부분): Animator 파라미터 이름(`AnimatorParam` 에서 한 번만 해시), Input System 컨트롤 경로(CSV 데이터), 에디터 빌더의 에셋 경로·본 이름 검색, 치트 콘솔 도움말 문자열은 문자열이 남아 있습니다.

## 9. 현재 범위 / 미구현

멀티플레이 미구현(설계만), 세이브 없음, 제작 즉시 완료 등 범위와 알려진 문제는 [Status.md 3~4절](Status.md#3-알려진-문제).
