# ActionRPGProject 문서

문서는 Unity 가 임포트하지 않도록 `Assets` 밖(저장소 루트 `Documentation/`)에 둡니다. `.meta` 파일이 없습니다.

이 저장소에는 **두 개의 게임 코드**가 함께 있습니다. 지금 개발 중인 것은 BLOODLINE 입니다.

| | BLOODLINE (현재) | 이전 프로젝트 (유지만) |
|---|---|---|
| 내용 | V Rising 리버스 데모, 오프라인 1인 (4인 협동 설계) | Netcode 4인 협동 덱/던전 액션 RPG 프레임워크 |
| 코드 | `Assets/Framework`, `Assets/Game` | `Assets/Scripts`, `Assets/Editor`, `Packages/com.actionrpg.foundation` |
| 시작 씬 | `Assets/Game/Scenes/World.unity` | `Assets/Scenes/MainMenu.unity` |
| 메뉴 | `Game > …` | `ActionRPG > …` |
| 문서 | **[VRising/README.md](VRising/README.md)** | [Legacy/README.md](Legacy/README.md) |

두 코드는 어셈블리가 분리되어 있고 서로 참조하지 않습니다. BLOODLINE 은 이전 코드를 수정하지 않습니다.

## 어디서부터 읽을까

| 하려는 일 | 문서 |
|---|---|
| 프로젝트 요구사항 · 개발 규칙 원문 | [Project.md](Project.md) |
| 실행 · 조작 · 툴 | [VRising/README.md](VRising/README.md) |
| 지금 무엇이 되고 무엇이 안 되는지 | [VRising/Status.md](VRising/Status.md) |
| 코드 구조 · 새 코드 위치 · 디자인 패턴 | [VRising/Architecture.md](VRising/Architecture.md) |
| 전투(GAS) / 몬스터 AI(StateTree) | [VRising/GAS.md](VRising/GAS.md) / [VRising/StateTree.md](VRising/StateTree.md) |
| 맵 · 렌더링 | [VRising/World.md](VRising/World.md) / [VRising/URP.md](VRising/URP.md) |
| 겪은 문제와 해결, 성능 의심 지점 | [VRising/Issues.md](VRising/Issues.md) |
| 날짜별 작업 기록 | [VRising/CHANGELOG.md](VRising/CHANGELOG.md) |
| 대용량 저장소를 GitHub 에 올리기 | [../Tools/GitBatchPush/README.md](../Tools/GitBatchPush/README.md) |

## 문서 작성 규칙

- 한 주제는 **한 문서에만** 자세히 쓰고(원본), 다른 곳에서는 한두 줄 요약 + 링크로 연결합니다. 원본 위치는 [VRising/README.md 문서 지도](VRising/README.md#문서-지도).
- 알려진 문제·미구현은 `Status.md`, 해결 과정은 `Issues.md`(번호 유지, 통합 시 빈 번호에 안내만 남김), 날짜별 이력은 `CHANGELOG.md`.
- 확인하지 못한 내용은 "미확인 / 추론" 으로 적습니다.
