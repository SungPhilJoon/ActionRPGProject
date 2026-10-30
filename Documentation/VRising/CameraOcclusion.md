# 카메라 가림 처리 (Camera Occlusion) — 2026-10-29

쿼터뷰 카메라와 플레이어 사이를 나무 · 건물 · 바위 · 건축물이 가리거나, 카메라가 그 안에 들어가면 **그 물체의 메시를 카메라에서 숨긴다**(컬링).
가리지 않게 되면 바로(0.15초 뒤) 원래대로 되돌린다 — 카메라와 충돌하지 않는 물체는 숨기지 않는다.
이 문서가 카메라 가림의 원본입니다. 코드 위치 규칙은 [Architecture.md](Architecture.md), 테스트는 [README.md 7절](README.md#7-테스트).

## 1. 구조

```
TopDownCamera.Setup(target)                      (Game)  마스크 = LayerId.CameraOcclusionMask, 대상 = 플레이어
  └ CameraOcclusionSystem  (카메라 GameObject)   (Framework, [DefaultExecutionOrder(1000)] → 카메라 이동 뒤 LateUpdate)
       ├ ICameraOcclusionDetector  감지 전략   기본: SphereCastOcclusionDetector
       ├ ICameraOcclusionResponse  응답 전략   기본: HideMeshOcclusionResponse
       └ CameraOcclusionSettings   설정        Mask, CastRadius, CameraRadius, TargetHeight, TargetPadding, RevealDelay, MaxHits, Triggers
CameraOccluder  (숨길 대상 루트에 붙임, opt-in) — 자식 렌더러와 원래 그림자 모드를 기억
```

| 파일 | 내용 |
|---|---|
| `Framework/Runtime/Camera/Occlusion/CameraOcclusionSystem.cs` | 매 프레임 감지 → 콜라이더를 `GetComponentInParent<CameraOccluder>` 로 해석(캐시) → 새로 가린 대상 `OnOccluded`, RevealDelay 동안 안 걸린 대상 `OnRevealed`. 꺼지거나 파괴되면 전부 복구 |
| `Framework/Runtime/Camera/Occlusion/CameraOcclusionStrategies.cs` | 인터페이스 2개 + 기본 구현 2개 + `CameraOcclusionQuery` |
| `Framework/Runtime/Camera/Occlusion/CameraOccluder.cs` | 대상 표시 컴포넌트. `IsOccluded`, `GetRenderer(i)`, `GetOriginalShadowMode(i)`, `RefreshRenderers()` |
| `Game/Runtime/Camera/TopDownCamera.cs` `setupOcclusion()` | 게임 쪽 연결점. 전략을 바꿀 때 여기서 `SetDetector` / `SetResponse` |
| `Game/Runtime/Define/GameEnums.cs` `LayerId.CameraOcclusionMask` | Default(건물·바위) · Structure(건축물) · SunBlocker(나무 그늘 콜라이더) · Interactable(채집 노드) |

Framework 는 게임 타입을 모르므로 레이어 마스크와 대상은 게임(`TopDownCamera`)이 넣는다.

## 2. 기본 동작

| 단계 | 내용 |
|---|---|
| 감지 | 카메라 → 대상(발밑 + 1.2m) **SphereCast**(반지름 0.5m, 대상 앞 1m 는 제외 → 대상 뒤/옆 물체는 안 숨김) + 카메라 위치 **OverlapSphere**(1.5m, 카메라가 수관 · 벽 안에 들어간 경우) |
| 해석 | 걸린 콜라이더의 부모 중 `CameraOccluder` 가 있는 것만 대상. 없으면 무시 (지형, 캐릭터, 투사체 등) |
| 응답 | 그림자를 드리우던 렌더러 → `ShadowCastingMode.ShadowsOnly`, 그림자가 없던 렌더러 → `forceRenderingOff`. **그림자는 남긴다** — 숨겨도 땅의 그늘과 햇빛 판정(`SunExposure`, 그늘 콜라이더)이 화면과 어긋나지 않는다 |
| 복구 | 0.15초(RevealDelay) 동안 다시 걸리지 않으면 원래 그림자 모드 · 렌더링으로 복구 (경계에서 깜빡임 방지) |

`CameraOccluder` 가 붙는 곳:

| 대상 | 붙이는 곳 | 감지에 쓰이는 콜라이더 |
|---|---|---|
| 장식 나무 (절차 나무) | `TreeFactory.buildTree` (프리팹 루트) | `SunShade` MeshCollider (SunBlocker, 나무 메시 모양) |
| 채집 나무 | `ContentBuilder.buildTree` (노드 루트) | 줄기 캡슐 + 수관 트리거 + SunShade |
| 랜드마크 건물 · 바위 | `WorldBuilder.place` | 원래 콜라이더 (Default) |
| 플레이어 건축물 | `Structure.Initialize` (런타임) | 모듈 콜라이더 (Structure) |

덤불(높이 1~2m)과 Terrain 풀은 대상이 아니다.

## 3. 바꾸는 방법 (유지보수 포인트)

| 바꾸고 싶은 것 | 방법 |
|---|---|
| 숨기는 방식 (디더 페이드, 반투명, 실루엣) | `ICameraOcclusionResponse` 구현 → `TopDownCamera.setupOcclusion` 에서 `occlusion.SetResponse(new MyResponse())`. 페이드처럼 시간이 필요하면 `Tick(occluder, dt)` 를 쓴다 (가림 중인 대상마다 매 프레임 호출). `SetResponse` 는 이전 전략으로 숨긴 것을 먼저 복구한다 |
| 감지 방식 (여러 파티원, 화면 사각형, 렌더러 바운드) | `ICameraOcclusionDetector` 구현 → `SetDetector`. 입력은 `CameraOcclusionQuery`(카메라 위치 · 대상 위치 · 설정) 뿐이라 시스템을 고치지 않아도 된다 |
| 범위/민감도 | `CameraOcclusionSettings` 값 (카메라 인스펙터에서도 보임). 넓게 숨기려면 CastRadius, 대상 바로 뒤까지 숨기려면 TargetPadding 축소 |
| 대상 레이어 | `LayerId.CameraOcclusionMask` |
| 새로운 대상 종류 | 그 프리팹/오브젝트 루트에 `CameraOccluder` 를 붙이고 콜라이더가 마스크 레이어에 있게 한다. 런타임에 자식 렌더러를 바꿨다면 `RefreshRenderers()` |
| 특정 물체는 절대 숨기지 않음 | `CameraOccluder` 를 붙이지 않는다 |

## 4. 주의

- 숨긴 물체의 콜라이더는 그대로라 조준 레이캐스트(`TopDownCamera.ScreenToGround`, Ground|Structure)는 숨긴 벽 위를 맞힐 수 있다 (기존 동작 유지).
- `ResourceNode` 고갈은 `Renderer.enabled` 를 쓰고 가림 응답은 `shadowCastingMode` / `forceRenderingOff` 를 써서 서로 덮어쓰지 않는다.
- 감지 비용: 프레임당 SphereCast 1회 + OverlapSphere 1회 (NonAlloc). 콜라이더 → 대상 해석은 Dictionary 캐시.
