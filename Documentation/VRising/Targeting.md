# 대상 지정 · 하이라이트 (Targeting) — 2026-10-31

사용자 요청: "채집 · 공격 가능한 대상은 Fade In/Out 되지 않도록 하고 마우스를 Hover 했을 때 하이라이트되는 Shader", "클릭하면 다른 색으로 하이라이트해 대상 지정을 확인하고 대상의 상태를 중상단에 보여 주기".
카메라 가림 페이드는 [CameraOcclusion.md](CameraOcclusion.md).

## 1. 동작

| 상황 | 결과 |
|---|---|
| 마우스를 몬스터 · 채집 노드 위에 올림 | **금색** 하이라이트 (외곽선 + 가장자리 발광, 은은하게 맥동) |
| 그 위에서 좌클릭 | 대상 지정 — 몬스터는 **빨강**, 채집 노드는 **하늘색**. 좌클릭은 그대로 공격 · 채집도 한다 |
| 지정 중 | 화면 **중상단 정보창** (보스 바 아래) |
| 해제 | 다른 대상을 클릭, 몬스터가 죽음 · 사라짐, 60m 보다 멀어짐 (ConstTable `TargetMaxDistance`). 빈 땅 클릭은 유지 |
| UI 위 · 입력을 막는 창이 열림 | 호버 안 함 |

정보창:

| 대상 | 표시 |
|---|---|
| 몬스터 | 이름(V 블러드 보스는 빨강) · Lv(내 기어보다 높으면 빨강) · 체력 바 · 혈액 종류 · 품질 · V 블러드 · 걸린 상태이상(기절 · 둔화 · 점화 · 냉기 · 빙결 · 정전기 · 단죄 · 약화 · 생명력 흡수 · 출혈 · 비물질 · 방어 · 강화 · 가속) |
| 채집 노드 | 이름 · 내구도 바 · 획득물(아이템 · 개수) / 고갈되면 재생까지 남은 초 |

채집 노드는 카메라를 가려도 페이드하지 않는다 (`ResourceNode.Initialize` 가 `CameraOccluder` 를 끈다). 몬스터는 원래 가림 대상이 아니다.

## 2. 구조

| 코드 | 내용 |
|---|---|
| `Game/Runtime/Player/TargetSelector.cs` (IManager) | 마우스 광선(Monster · Interactable 레이어, 못 잡으면 반지름 0.4m SphereCast) → `MonsterCharacter` / `ResourceNode` 해석. 호버 · 선택 상태, 하이라이트 색, `TargetSelectedEvent` |
| `Framework/Runtime/Rendering/RendererHighlight.cs` | 대상 메시를 하이라이트 머티리얼로 한 번 더 그리는 **겹 렌더러** (원본 머티리얼은 건드리지 않음 → 페이드 · 피격 플래시와 충돌 없음). Skinned → 같은 본의 SkinnedMeshRenderer, Mesh → 같은 메시. LOD0 만. 원본이 컷아웃(잎)이면 원본 텍스처 · 문턱으로 잘라 사각형이 안 보이게 + 외곽선 끔. 원본이 꺼지면(고갈) 같이 꺼짐 |
| `Game/Art/Shaders/Highlight.shader` (**Game/Highlight**) | Outline 패스(법선으로 부풀린 뒷면, SRPDefaultUnlit) + Rim 패스(프레넬 + 옅은 채움, 가산, UniversalForward), 맥동. 색 `_HighlightColor`(a = 세기)는 프로퍼티 블록 |
| `Content/Materials/Highlight.mat` | 위 셰이더 머티리얼 (AssetTable 142, ConstTable `HighlightMaterialAsset`). `Game > Setup > 3e. Build Highlight Material` (Build Content 에 포함) |
| `HudWindow.updateTarget` + `UIPrefabBuilder.buildTarget` | 중상단 정보창 (보스 바 -30 · 정보창 -118 · 햇빛 경고 -200) |
| `UIUtil.AppendStatuses` | 상태이상 태그 → 문구 (StringTable 70~83) |

색: `TargetSelector.HoverColor` / `HostileColor` / `NodeColor`.

## 3. 바꾸는 방법

| 바꾸고 싶은 것 | 방법 |
|---|---|
| 하이라이트 모양 (두께 · 발광 · 맥동) | `Content/Materials/Highlight.mat` 인스펙터 (`_OutlineWidth`, `_RimPower`, `_RimIntensity`, `_Fill`, `_PulseSpeed`) |
| 색 | `TargetSelector` 의 색 상수 |
| 지정 가능한 대상 종류 | `TargetSelector.resolve` / `IsTargetable` (+ 정보창 `HudWindow.updateTarget`) |
| 정보창 상태이상 목록 | `UIUtil.StatusTags` / `StatusTexts` + StringTable |
