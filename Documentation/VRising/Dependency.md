# Addressables 의존성 툴 — 의존성 지도 (2026-10-29)

게임이 실행 중에 불러오는 에셋(Addressables)이 서로 어떻게 묶여 있는지, 어디서 문제가 생기는지를 **기획자 · 디자이너도 볼 수 있게** 만든 툴입니다.
이 문서가 의존성 툴 · 기록 · 이슈 규칙의 원본입니다. 현재 이슈 목록은 자동 생성 문서 [DependencyIssues.md](DependencyIssues.md).

## 1. 3분 사용법 (비개발자용)

1. `Game > Tools > Dependency Map (의존성 지도)` 를 연다.
2. 위의 **① 정적 검사** — 플레이 없이 파일만 분석한다 (몇 초).
3. **② 의존성 테스트 실행** — 게임을 자동으로 플레이하며 기록한다 (약 40초, Unity 창을 앞에 둘 것). 끝나면 이슈가 자동 갱신된다.
4. **🏠 한눈에 보기** 에서 오류 / 경고 / 참고 · 새 이슈 · 해결된 이슈 수를 본다.
5. **⚠ 이슈** 에서 하나씩 펼치면 *왜 문제인지 · 어떻게 고치는지 · 누가 보면 되는지(기획 / 아트 / 프로그래밍)* 가 나온다. "그래프에서 보기" 로 해당 에셋으로 이동.
6. **🕸 의존성 그래프** 에서 에셋을 고르면 *이걸 쓰는 곳 ← 에셋 → 이게 쓰는 것* 이 그림으로 나온다.

| 색 / 기호 | 뜻 |
|---|---|
| 파란 상자 | 게임이 이름(주소)으로 불러오는 에셋 (Addressables) |
| 회색 상자 | 딸려 오는 에셋 (등록 안 됐지만 위 에셋이 써서 같이 묶여 들어감) |
| 보라 상자 | 데이터 테이블 행 · 코드 (AssetTable, EUIAsset …) |
| 빨강 테두리 ⛔ / 주황 ⚠ / 파랑 ℹ | 오류 / 경고 / 참고 이슈가 걸린 에셋 |
| **의존 n** | 이 에셋에 기대는 것의 수. 많을수록 바꾸거나 지울 때 영향이 크다 |

## 2. 구성

| 기능 | 무엇을 | 코드 |
|---|---|---|
| **정적 검사** (플레이 전) | 모든 그룹 에셋에서 AssetDatabase 의존성을 따라가 그래프를 만들고, 다른 등록 에셋을 만나면 멈춘다 (그 사이 = 딸려 오는 에셋) | `Framework/Editor/Addressables/AddressablesDependencyAnalyzer` |
| **플레이 전 자동 검사** | Play 를 누를 때 "테이블 · 코드가 요구하는 주소가 모두 등록됐는지" 빠르게 확인 → 문제가 있으면 콘솔 오류 (Play 는 막지 않음). 창 상단 토글로 끄기 | `DependencyPrePlayCheck` (Game.Editor) |
| **런타임 추적** | `AssetLoader` 의 모든 로드 · 참조 추가 · 반납 · 동기 대기를 `AssetTracker` 에 보고. 주소별 참조 카운트, 누가 잡았는지(소유자 또는 **호출한 코드 위치** 예: `SpawnManager.spawnMonsterAsync`), 최근 이벤트 4000건 | `Framework/Runtime/Asset/AssetTracker` |
| **실시간 탭** | Play 중 지금 잡혀 있는 에셋 · 참조 카운트 · 최근 이벤트 | 창 ▶ 실시간 |
| **의존성 테스트** | 의심 지점을 실제로 플레이하며 구간 기록 → `DependencyReports/Runs/<시각>_<시나리오>.json` | `Game/Tests/PlayMode/DependencyScenarioTests` (Category `Dependency`), `DependencyRecorder` |
| **이슈 갱신** | 정적 이슈 + 기록 이슈 → 이전 목록(`DependencyReports/issues.json`)과 비교해 새 / 계속 / 해결 · 처음 · 마지막 발견일 → `DependencyIssues.md` | `DependencyIssueTracker`, `DependencyMapService` |
| **테스트 끝나면 자동 갱신** | 테스트 러너 종료 시 새 기록 파일이 있으면 이슈 갱신 | `DependencyTestWatcher` (TestRunnerApi 콜백) |
| **사용처 제공** | AssetTable 행 · EUIAsset · GameTables → 그래프의 보라 상자 | `GameDependencyUsage` (`IDependencyUsageProvider`) |

Framework 쪽(분석기 · 추적기 · 기록 · 이슈 추적)은 게임 타입을 모른다. 게임은 `IDependencyUsageProvider` 를 구현해 사용처만 알려 준다 (TypeCache 로 자동 발견).

## 3. 테스트 시나리오

각 시나리오는 같은 동작을 **3번 반복**한다. 반복할수록 참조가 늘면 누수, 한 번만 늘면 "처음 한 번 불러와 계속 씀" 이다.

| 시나리오 | 무엇을 | 끝나면 원래대로? |
|---|---|---|
| `Boot` | 게임 시작 ~ 싱글 플레이까지 로드된 전체 (프리로드 포함) | 아니오 (계속 들고 있는 게 정상) |
| `UI_OpenCloseWindows` | 인벤토리 · 제작 · 건축 · 상점 · 치트 · 밸런스 창 열고 닫기 (아이콘 스프라이트) | 예 |
| `Monster_SpawnAndRemove` | 메두사 · 코브라 · 트롤 스폰 → 처치 → 제거 (캐릭터 프리팹 · AI 트리 · VFX) | 예 |
| `Build_GhostBeginCancel` | 모든 건축물 건축 모드 진입(고스트 동기 로드) → 취소 | 예 |
| `Ability_CastAllSlots` | 플레이어 어빌리티 8칸 시전 (스킬 · 발사체 · 큐 VFX) | 예 |

테스트는 **로드 실패만** 실패로 처리한다. 누수 · 멈칫은 테스트를 깨지 않고 기록 → 이슈로 추적한다.
새 시나리오: `DependencyRecorder.Begin(이름, 설명, 반복, 원래대로?)` → 플레이 → `End(true, out path)`.

## 4. 이슈 규칙

| 코드 | 심각도 | 조건 | 출처 |
|---|---|---|---|
| `S-ADDR-MISSING` | ⛔ 오류 | 테이블 · 코드가 요구하는 주소가 Addressables 에 없음 | 정적 / 플레이 전 |
| `S-MISSING-REF` | ⛔ 오류 | 등록 에셋 안에 깨진 참조(Missing) · 사라진 스크립트 | 정적 |
| `S-EDITOR-DEP` | ⛔ 오류 | 게임 에셋이 Editor 폴더 에셋을 참조 | 정적 |
| `S-DUP-GROUP` | ⚠ 경고 | 딸려 오는 에셋이 여러 그룹 번들에 중복 | 정적 |
| `S-DUP-RESOURCES` | ⚠ 경고 | Resources 폴더 에셋이 번들에도 들어감 (2벌) | 정적 |
| `S-UNUSED` | ℹ 참고 | 아무도 안 쓰는 등록 에셋 | 정적 |
| `S-HEAVY` | ℹ 참고 | 딸려 오는 원본 합계 128MB 초과 | 정적 |
| `R-FAIL` | ⛔ 오류 | 실행 중 로드 실패 | 테스트 |
| `R-LEAK` | ⚠ 경고 | 원래대로 돌아와야 하는 시나리오에서 참조 카운트가 남음 | 테스트 |
| `R-SYNC` | ⚠ 경고 | 동기 로드 대기 16ms 초과 (멈칫) | 테스트 |
| `R-OUTSIDE` | ℹ 참고 | 그래프에 없는 주소를 코드가 직접 요청 | 테스트 |

상태: **새 이슈**(이번에 처음) → **계속**(다음 갱신에도 있음) → **해결됨**(사라짐, 해결 시각과 함께 기록에 남음). 같은 이슈인지는 `코드 + 대상(+시나리오)` 키로 판단한다.

## 5. 첫 실행 결과 (2026-10-30, 수정하지 않고 기록만 함)

- 정적: Addressables 에셋 101, 딸려 오는 에셋 451, 0.9~3초. 오류 0. `S-DUP-RESOURCES` 1 (TextMesh Pro 기본 폰트가 Resources 와 UI 번들에 2벌), 무거운 에셋 9.
- 테스트: **반납(Release) 이 0 건** — 프로젝트의 로드 코드는 모두 쓰고 나서 `AssetLoader.Release` 를 부르지 않는다. 그래서 같은 동작을 반복할 때마다 참조가 1씩 쌓인다.
  예: 몬스터 스폰마다 캐릭터 프리팹(`SpawnManager.spawnMonsterAsync`) · AI 트리(`MonsterBrain.loadTree`) +1, 창을 열 때마다 아이콘 스프라이트(`UIUtil.LoadSprite`) +1, 스킬 VFX(`CueSpawner.spawnInternal`) +1.
  에셋 자체는 처음 한 번만 메모리에 올라오므로 지금 당장 메모리가 늘지는 않지만, 참조가 0 이 되지 않아 **절대 내려가지 않는다** (씬 전환 · 지역 스트리밍을 하면 문제). 고칠지는 사용자가 판단 — [DependencyIssues.md](DependencyIssues.md).
- `Characters/Player` 동기 로드 41ms 멈칫 (`R-SYNC`, 싱글 플레이 시작 시).

## 6. 파일

| 경로 | 내용 | git |
|---|---|---|
| `DependencyReports/Runs/*.json` | 시나리오 기록 (실행할 때마다 1개씩) | 추적 권장 (작음, 추이 비교용) |
| `DependencyReports/issues.json` | 이슈 이력 (상태 · 발견일) | 추적 권장 |
| `Documentation/VRising/DependencyIssues.md` | 사람이 읽는 보고서 (자동 생성, 직접 고치지 말 것) | 추적 |
