# UI — 프리팹 + Addressables (2026-10-29)

UI 구조 · 스킨 · 새 창 추가 방법의 원본 문서입니다. 툴 사용법은 [Tool.md 4절](Tool.md#4-그-밖의-툴), 폴더 규칙은 [Architecture.md](Architecture.md).

## 1. 바뀐 점

| | 예전 | 지금 |
|---|---|---|
| 창 만들기 | 각 창의 `Build()` 가 **실행 중에** `UIFactory` 로 자식 오브젝트를 만들고 필드에 연결 | 에디터 빌더가 **미리** 프리팹을 굽고, 자식 참조는 프리팹에 직렬화 |
| 실행 중 | `UIManager.Create<T>()` → AddComponent → Build | Addressables 로 프리팹 로드 → Instantiate → `Initialize()` (동적 목록 · 코드 리스너만) |
| 목록 행 · 슬롯 · 체력바 · 데미지 숫자 | 코드로 조립 | 위젯 프리팹을 `UIItemList<T>` / 풀로 복제 |
| 문구 | `Tables.Text` 를 Build 에서 대입 | `UIText`(TextId) 가 켜질 때 현재 언어로 바꿈 — 인스펙터에서 TextId 만 바꾸면 됨 |
| 버튼 | `AddListener` | 고정 버튼은 프리팹의 **영구 리스너**(인스펙터 OnClick 에 보임), 재사용 행만 코드 리스너 |
| 캔버스 | 코드로 생성 | `UI/LayerCanvas` 프리팹을 레이어마다 Instantiate |
| `UIFactory` | Framework.Runtime | **Framework.Editor** (빌더 전용 — 런타임에서 쓸 수 없음) |

## 2. 구조

```
GameBootstrap.setupUIAsync()
  AssetLoader.LoadAsync<GameObject>(Tables.Address((int)EUIAsset.X))   ← AssetTable.csv 400~414
  UIManager.RegistLayer(layer, name, LayerCanvas 프리팹)
  UIManager.Spawn<WorldHealthBarLayer / DamageNumberLayer>(UILayerId.World, 프리팹)
  UIManager.Create<HudWindow …>(layer, 프리팹)  → Instantiate → UIWindow.Initialize() → OnInitialize()
```

| 주소 (Content/UI/…) | EUIAsset | 내용 |
|---|---|---|
| `UI/LayerCanvas` | 400 | Screen Space Overlay 캔버스 (1920×1080 기준) |
| `UI/Windows/HudWindow` | 401 | 체력 · 혈액, 어빌리티 8칸(`AbilitySlotView`), 시계, 보스 바, 퀘스트 추적기, 안내 · 토스트 · 사망 · 클리어 |
| `UI/Windows/InventoryWindow` | 402 | 그리드(칸 수 = ConstTable.InventorySize → `ItemSlot` 프리팹 복제), 장비, 능력치 |
| `UI/Windows/CraftWindow` · `BuildWindow` · `ShopWindow` | 403~405 | `ListWindow` 공통 레이아웃 + `ListRow` 행 프리팹 |
| `UI/Windows/BalanceOverlayWindow` · `CheatConsoleWindow` · `TitleWindow` | 406~408 | |
| `UI/Layers/WorldHealthBarLayer` · `DamageNumberLayer` | 409, 410 | `WorldHealthBar` · `DamageNumber` 프리팹을 풀링 |
| `UI/Widgets/ListRow` · `ItemSlot` · `WorldHealthBar` · `DamageNumber` | 411~414 | 위젯 (창 프리팹이 직접 참조 → Addressables 의존성으로 같이 로드) |

| 코드 | 위치 |
|---|---|
| `UIWindow`(Initialize/OnInitialize), `UIManager`(RegistLayer · Create · CreateAsync · Spawn), `UIItemList<T>`, `UIBar`, `UISkin` | `Framework/Runtime/UI` |
| `UIFactory` (조립 함수, 스킨 적용, 영구 리스너) | `Framework/Editor/UI` |
| 창 · 위젯 컴포넌트 (`UIText`, `ListRowView`, `AbilitySlotView`, `WorldHealthBarView`, `ItemSlotView`) | `Game/Runtime/UI` |
| `UIPrefabBuilder`(+`.Hud`), `UISkinSetup` | `Game/Editor/UI` |

## 3. 고치는 방법

| 하고 싶은 것 | 방법 |
|---|---|
| 위치 · 크기 · 색 조금 바꾸기 | 프리팹을 열어(Game > Tools > UI Factory → Open) 바로 수정. **단, `Game > Setup > 3b. Build UI Prefabs` 를 다시 실행하면 빌더 코드 기준으로 덮어쓴다** → 오래 유지할 변경은 `UIPrefabBuilder` 에도 반영 |
| 레이아웃을 크게 바꾸기 | `Game/Editor/UI/UIPrefabBuilder*.cs` 의 build 메서드 수정 → 3b 실행 |
| 문구 바꾸기 | StringTable.csv (같은 TextId) 또는 프리팹의 `UIText.TextId` |
| 텍스처(스킨) 바꾸기 | `Game > UI > Select UI Skin` → `UISkin` 에셋의 스프라이트 · 틴트 수정 → 3b 실행. 스프라이트는 9-slice(Sprite Editor 의 Border) 필수 |
| 새 창 추가 | ① UI Factory 의 Generate Script (직렬화 필드 + OnInitialize 골격) ② `UIPrefabBuilder` 에 build 메서드 추가 ③ `EUIAsset` 값 + AssetTable.csv 행(같은 번호, `UI/Windows/<이름>`) ④ `GameBootstrap.setupUIAsync` 에서 `ui.Create<T>(레이어, await loadUIPrefab(EUIAsset.X))` ⑤ 3b → 4. Addressables |

설정 순서: `Game > Setup > 0. Run All` 이 3(콘텐츠) → **3b(UI 프리팹)** → 4(Addressables) 를 차례로 돈다. 3b 를 빼먹으면 부트스트랩이 "UI prefabs missing" 오류를 낸다.

## 4. 스킨 (2026-10-29)

- 원본: **Kenney "UI Pack: RPG Expansion"** (CC0, `Art/ThirdParty/Kenney/UI_RPG`, 라이선스 `License_UIPackRPG.txt`).
  My Assets 에 받아 둔 Asset Store 패키지 15개에는 UI 팩이 없었다 (AssetRequests.md). Asset Store 의 UI 팩은 계정에서 "My Assets 에 추가" 해야 받을 수 있으므로, 받으면 `UISkin` 스프라이트만 바꿔 끼우면 된다.
- 원본이 밝은 카툰 색이라 그대로 쓰지 않는다: `UISkinSetup` 이 갈색 판넬 · 버튼 · 슬롯을 **흑백 + 대비 늘리기**(밝기 범위를 0.42~1 로 재배치)한 사본(`Art/Generated/UI/T_UI_*.png`)을 9-slice 로 만들고,
  `UISkin` 틴트(검붉은 철 판넬, 핏빛 버튼)를 곱한다 → 밝은 테두리 · 리벳은 틴트 색, 바탕은 어둡게 남아 고딕 철판처럼 보인다. 게이지는 좌/중/우 3조각을 한 장으로 합친다.
- `UISkinSetup.GetOrCreate` 는 이미 있는 `UISkin` 값을 덮어쓰지 않는다 (빈 칸만 채움).

## 5. 주의

- 창 프리팹은 Instantiate 직후 활성 상태라 `OnEnable` 이 한 번 돈 뒤 닫힌다 (구독 → 해제). 부작용이 있는 코드는 `OnOpen` 에 둔다.
- `UIText` 는 `Tables.IsReady` 일 때만 문구를 바꾼다. 테이블 로드 전에 보이는 UI 는 프리팹에 구운 문구가 그대로 보인다.
- UI 프리팹 로드 · 참조 상태는 의존성 지도(Dependency.md)에서 볼 수 있다.
