# 4인 협동 확장 계획 (미구현 — 설계 메모)

요구사항 6("먼저 오프라인 플레이")에 따라 이번 단계는 **오프라인 1인만 구현**했습니다. 멀티플레이 코드는 아직 없습니다.
프로젝트에 `com.unity.netcode.gameobjects 2.0.0` 이 이미 설치되어 있으므로 NGO 리슨 서버(호스트 1 + 클라이언트 최대 3) 기준으로 확장할 계획입니다.

## 이미 확장을 염두에 두고 나눠 둔 지점

| 지점 | 현재 | 멀티플레이에서 |
|---|---|---|
| `PlayerController` | 입력 → `Actor.TryActivate` 직접 호출 | 입력 → ServerRpc(슬롯, 조준점) 전송, 서버가 `TryActivate` |
| `AbilitySystemComponent` | 로컬 권한으로 이펙트/속성 변경 | 서버만 적용, 속성/태그는 NetworkVariable 또는 델타 복제 |
| `GameplayCueManager` | 로컬 재생 | 서버가 CueId(int)+위치만 ClientRpc → 각 피어가 CueTable 로 재생 (프리팹 참조를 보내지 않음) |
| `PlayerCharacter.PlayerSlot`, `ActorKey.Player(slot)` | 0 고정 | 접속 순서 0~3 (ConstTable.MaxPartySize = 4) |
| `PlayerCharacter.Local` | 단일 | "로컬 소유 플레이어" 의미로 유지, 다른 플레이어는 ActorRegistry 로 조회 |
| `SpawnManager` | 로컬 Instantiate | 서버에서만 스폰 + NetworkObject.Spawn |
| `MonsterBrain` | 로컬 | 서버에서만 Update |
| 피해 숫자/HUD | EventBus 구독 | 동일 (복제된 이벤트를 다시 EventBus 로 발행) |
| TitleWindow | 협동 버튼 비활성 | Host / Join (IP) 버튼 |

## 예상 이슈
- 이동은 반응성을 위해 소유 클라이언트 권한(ClientNetworkTransform), 전투 판정은 서버 권한.
- 대시/콤보 예측: 클라이언트 선실행 후 서버 거절 시 롤백 (언리얼 GAS PredictionKey 방식).
- 햇빛 판정은 물리 레이캐스트라 서버에서 그대로 계산 가능 (URP.md 참고).
