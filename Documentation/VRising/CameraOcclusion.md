# 카메라 가림 처리 (Camera Occlusion) — 2026-10-29

쿼터뷰 카메라와 플레이어 사이를 나무 · 건물 · 바위 · 건축물이 가리거나, 카메라가 그 안에 들어가면 **캐릭터를 가린 정도만큼 그 물체를 반투명하게 페이드 아웃**한다 (2026-10-31, 전에는 즉시 숨김).
가리지 않게 되면 0.15초 뒤부터 다시 불투명해질 때까지 **페이드 인**한다. 채집 노드는 가려도 페이드하지 않는다 (대신 마우스 하이라이트 — [Targeting.md](Targeting.md)).
이 문서가 카메라 가림의 원본입니다. 코드 위치 규칙은 [Architecture.md](Architecture.md), 테스트는 [README.md 7절](README.md#7-테스트).

## 1. 구조

```
TopDownCamera.Setup(target)                      (Game)  마스크 = LayerId.CameraOcclusionMask, 대상 = 플레이어
  └ CameraOcclusionSystem  (카메라 GameObject)   (Framework, [DefaultExecutionOrder(1000)] → 카메라 이동 뒤 LateUpdate)
       ├ ICameraOcclusionDetector  감지 전략   기본: CoverageOcclusionDetector (광선 12개) / 예전: SphereCastOcclusionDetector
       ├ ICameraOcclusionResponse  응답 전략   기본: FadeOcclusionResponse (알파 페이드) / 예전: HideMeshOcclusionResponse
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

## 2. 기본 동작 (2026-10-31 가림 정도 페이드)

| 단계 | 내용 |
|---|---|
| 감지 | 카메라 → 캐릭터 몸의 **12점**(발 0.3 · 허리 0.9 · 가슴 1.5 · 머리 1.9m × 좌 · 중 · 우 0.35m) 으로 광선 12개 (`RaycastNonAlloc`, 대상 앞 1m 는 제외 → 대상 뒤/옆 물체 무시) + 카메라 위치 **OverlapSphere**(1.5m, 카메라가 수관 · 벽 안에 들어감 = 완전히 가림) |
| 해석 | 걸린 콜라이더의 부모 중 **켜진** `CameraOccluder` 만 대상. 한 광선이 같은 대상의 콜라이더 여러 개에 걸려도 한 번 |
| 가림 정도 | 대상별 **막힌 광선 수 / 12** (0~1). 몸 일부만 가리면 조금, 전부 가리면 많이 투명해진다 |
| 응답 | 목표 알파 = lerp(1, MinAlpha 0.25, 가림 정도), 현재 알파가 초당 3 씩 따라간다. **그림자는 그대로** (땅의 그늘 · 햇빛 판정과 화면이 어긋나지 않음) |
| 복구 | 0.15초(RevealDelay) 동안 다시 걸리지 않으면 가림 정도 0 → 알파 1 까지 페이드 인 → 원래 머티리얼 · 프로퍼티로 되돌리고 목록에서 뺀다 |

머티리얼별 반투명 방식 (`FadeOcclusionResponse`, Framework 는 게임 셰이더 이름을 모르고 프로퍼티로 고른다):

| 머티리얼 | 방식 |
|---|---|
| `_OcclusionFade` 가 있는 셰이더 (**Game/Foliage** — 절차 나무 · 덤불) | MaterialPropertyBlock 으로 값만 넘김. 셰이더 ForwardLit 이 알파 블렌드(1 이면 불투명과 같음), 페이드 중엔 깊이 패스(DepthOnly/DepthNormals)를 그리지 않아 SSAO 가 남지 않음 |
| URP Lit 계열 (`_Surface` · `_BaseColor`) — 건물 · 바위 · 건축물 | 원본마다 한 번 만든 **반투명 사본**(Transparent 큐, ZWrite 끔, DepthOnly 끔)으로 바꾸고 `_BaseColor` 알파를 렌더러별 프로퍼티 블록으로 |
| 그 밖 | 알파 0.5 미만일 때만 메시 숨김 |

- 반투명 사본의 ZWrite 를 켜면 깊이를 읽는 풀스크린 대기 안개(Game/PostFx/Atmosphere)가 반투명 건물을 가까운 벽으로 보고 화면을 하늘색으로 덮었다 → 끔 (건물 안쪽 면이 겹쳐 보일 수 있음).
- 컴포넌트 필드 초기화(= MonoBehaviour 생성자)에서는 `Shader.PropertyToID` · `new MaterialPropertyBlock` 이 금지라 응답 객체가 null 이 됐다 → 처음 쓸 때 얻는다.

`CameraOccluder` 가 붙는 곳 (채집 나무 노드는 붙어 있지만 `ResourceNode.Initialize` 가 **꺼서** 페이드하지 않는다):

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
| 즉시 숨김으로 되돌리기 | `occlusion.SetResponse(new HideMeshOcclusionResponse())` + `SetDetector(new SphereCastOcclusionDetector())` |
| 페이드 세기 · 속도 | `new FadeOcclusionResponse { MinAlpha = …, FadeSpeed = … }` 로 SetResponse |
| 감지 점 (몸 높이 · 폭) | `CoverageOcclusionDetector.Heights` / `Offsets` (광선 32개 이하) |
| 숨기는 방식 (디더 페이드, 반투명, 실루엣) | `ICameraOcclusionResponse` 구현 → `TopDownCamera.setupOcclusion` 에서 `occlusion.SetResponse(new MyResponse())`. 페이드처럼 시간이 필요하면 `Tick(occluder, dt)` 를 쓴다 (가림 중인 대상마다 매 프레임 호출). `SetResponse` 는 이전 전략으로 숨긴 것을 먼저 복구한다 |
| 감지 방식 (여러 파티원, 화면 사각형, 렌더러 바운드) | `ICameraOcclusionDetector` 구현 → `SetDetector`. 입력은 `CameraOcclusionQuery`(카메라 위치 · 대상 위치 · 설정) 뿐이라 시스템을 고치지 않아도 된다 |
| 범위/민감도 | `CameraOcclusionSettings` 값 (카메라 인스펙터에서도 보임). 넓게 숨기려면 CastRadius, 대상 바로 뒤까지 숨기려면 TargetPadding 축소 |
| 대상 레이어 | `LayerId.CameraOcclusionMask` |
| 새로운 대상 종류 | 그 프리팹/오브젝트 루트에 `CameraOccluder` 를 붙이고 콜라이더가 마스크 레이어에 있게 한다. 런타임에 자식 렌더러를 바꿨다면 `RefreshRenderers()` |
| 특정 물체는 절대 숨기지 않음 | `CameraOccluder` 를 붙이지 않거나 `enabled = false` (채집 노드) |

## 4. 주의

- 숨긴 물체의 콜라이더는 그대로라 조준 레이캐스트(`TopDownCamera.ScreenToGround`, Ground|Structure)는 숨긴 벽 위를 맞힐 수 있다 (기존 동작 유지).
- `ResourceNode` 고갈은 `Renderer.enabled` 를 쓰고 가림 응답은 `shadowCastingMode` / `forceRenderingOff` 를 써서 서로 덮어쓰지 않는다.
- 감지 비용: 프레임당 Raycast 12회 + OverlapSphere 1회 (NonAlloc). 콜라이더 → 대상 해석은 Dictionary 캐시.
- 페이드 중인 렌더러는 프로퍼티 블록 때문에 SRP Batcher 에서 빠진다 (가림 대상 몇 개뿐이라 무시).
