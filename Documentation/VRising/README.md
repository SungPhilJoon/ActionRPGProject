# BLOODLINE — V Rising 리버스 데모 (오프라인 1인 플레이 1차 구현)

[Project.md](../Project.md) 요구사항을 기준으로 만든 **V Rising 데모**입니다.
기존 `Assets/Scripts`(덱/던전 액션 RPG) 코드는 건드리지 않고, 새 게임은 전부 `Assets/Game/` 아래에 독립 어셈블리로 만들었습니다.

### 문서 지도

각 주제는 **한 문서에만** 자세히 쓰고, 다른 문서는 링크로 연결합니다.

| 문서 | 이 문서가 원본인 내용 |
|---|---|
| **README.md** (이 문서) | 실행 방법, 조작, 게임 루프, 데이터 테이블 규칙, 툴, 코딩 규칙 적용 |
| [Status.md](Status.md) | 기능별 상태, 규모, **알려진 문제**, 미구현 범위, 다음 단계 |
| [Architecture.md](Architecture.md) | 어셈블리 · 폴더 구조, 의존 규칙, 새 코드 위치, 디자인 패턴 |
| [GAS.md](GAS.md) | 언리얼 GAS 대응, 이펙트 · 어빌리티 흐름, 피해 공식, 태그 규칙, 상태이상 |
| [StateTree.md](StateTree.md) | 언리얼 StateTree 대응, 실행 규칙, 몬스터 트리, 에디터 사용법 |
| [World.md](World.md) | 지형 규격, 지역, 건축 모듈, 캐릭터 · 애니메이션 매핑 |
| [URP.md](URP.md) | 조명 · 그림자, **WeatherMaker 하늘 설정**, 포스트프로세싱, 머티리얼 변환, Terrain 렌더 |
| [Issues.md](Issues.md) | 문제와 해결 (번호로 참조), **성능 관찰 포인트(18)** |
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

| 키 | 동작 | 키 | 동작 |
|---|---|---|---|
| WASD | 이동 | 좌클릭(누르고 있기) | 무기 콤보 |
| Q / E | 무기 스킬 | Space | Veil of Blood (회피 대시 + 강화) |
| R / C | 주문 1 / 2 | T | 궁극기 (보스 V Blood 흡혈 시 해금) |
| F | 흡혈 (체력 25% 이하 적) | G | 상호작용 (상인/작업대/관) |
| Tab | 인벤토리·장비 | K | 제작 |
| B | 건축 (X 회전, ESC 취소) | P | 상점 (전부 무료) |
| ` | 치트 콘솔 | F8 | 밸런스 미터 오버레이 |
| 휠 / 휠 드래그 | 줌 / 카메라 회전 | ESC | 창 닫기 |

키는 `Assets/Game/Data/CSV/InputTable.csv` 만 고치면 바뀝니다.

## 3. 데모 게임 루프 (V Rising 핵심 요소)

- **낮/밤 + 햇빛**: 8분 주기, 해 질 녘(18:30) 시작. 하늘·해·구름은 WeatherMaker. 낮에 그늘(나무 수관, 건물, 바위, 폐허 벽) 밖에 있으면 햇빛 화상이 점점 강해짐 (HUD 경고).
- **흡혈/혈액**: 적 체력을 25% 이하로 만들고 F → 혈액 종류(Warrior/Rogue/Scholar/Brute/Creature)와 품질 획득, 품질 단계별 버프.
- **V Blood 보스**: `Minotaur Warlord` (북동쪽 바위 고리 아레나, Magic Pig 미노타우로스). 흡혈 시 궁극기 *Crimson Storm* 해금.
- **월드**: 512m 대자연 오픈월드 — 시작 초원, 폐허 마을, 숲, 호수/늪, 고원, 트롤 능선, 보스 아레나 ([World.md](World.md)).
- **기어 레벨**: 장비 GearLevel 평균. 몬스터 레벨과의 차이로 주고받는 피해가 보정됨.
- **채집/제작/건축**: 무기로 나무·바위·덤불·구리 광맥을 때려 채집(벌목/채광/채집 모션) → 손 제작 / 작업대·제련소 제작 → 성 심장 주변 3m 그리드에 ASTROFISH 바닥·벽·창벽·문벽·기둥·램프·작업대·관 건축(망치질 모션). 관은 리스폰 지점.
- **상인/상점**: 시작 지점 옆 상인 또는 P 키. 프로젝트 규칙대로 **모든 상품 가격 0**.

## 4. 폴더 구조

| 위치 | 내용 |
|---|---|
| `Assets/Framework/` | 게임을 모르는 재사용 기반 (GAS, StateTree, 매니저, CSV 테이블, UIFactory …) |
| `Assets/Game/Runtime`, `Editor`, `Integration` | 이 게임의 코드 |
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

- **Game > Tools > Balance Tool** — Live(플레이 중 공격자·어빌리티별 누적 피해/DPS, 대상별 받은 피해 총합과 내역, 최근 기록) / Simulator(테이블 값으로 1타 피해·처치 타수·DPS 계산)
- **F8 밸런스 오버레이** — 같은 데이터를 게임 화면에서
- **Window > Framework > StateTree Editor (또는 Game > AI > StateTree Editor)** — 몬스터 AI 편집 (트리 뷰 + Details + Play 중 라이브 디버그). [StateTree.md](StateTree.md)
- **Game > Tools > UI Factory** — UIWindow 스크립트 골격 생성, 편집 모드 미리보기, 프리팹 굽기
- **치트 콘솔(`)** — `help`, `god`, `nocd`, `heal`, `give <id> [n]`, `giveall`, `equip <id>`, `time <h>`, `timescale`, `freeze`, `nosun`, `killall`, `spawn <id> [n]`, `lowhp`, `speed`, `stat <Attr> <v>`, `level`, `blood <type> <q>`, `unlock`, `tp <x> <z>`, `hurt`, `clearlog`, `spell <id>`(주문 교체 301~306), `status <effectId>`(주변 적에게 상태이상)

## 7. 코딩 규칙 적용

규칙 원문은 [../Project.md](../Project.md). 이 코드에서 지킨 방식:

- 문자열 비교/하드코딩 대신 int 인덱스·enum·계층 태그 비트셋(`GameplayTagContainer`, 언리얼과 같은 비교 규칙)·FNV 해시(`HashUtil`) 사용
- 조건식은 `== null`, `== false`, `== true` 로 명시 (`!` 미사용), `_` 접두 필드 없음, private 메서드 소문자 시작, 매개변수 `In` 접두 (frameworkLib 스타일)
- 공통 로직은 `Framework/Runtime/Util`, 게임 공통은 `CombatUtil`, `UIUtil`, `LootUtil`, `TagUtil`

예외(솔직하게 남기는 부분): Animator 파라미터 이름(`AnimatorParam` 에서 한 번만 해시), Input System 컨트롤 경로(CSV 데이터), 에디터 빌더의 에셋 경로·본 이름 검색, 치트 콘솔 도움말 문자열은 문자열이 남아 있습니다.

## 8. 현재 범위 / 미구현

멀티플레이 미구현(설계만), 세이브 없음, 제작 즉시 완료 등 범위와 알려진 문제는 [Status.md 3~4절](Status.md#3-알려진-문제).
