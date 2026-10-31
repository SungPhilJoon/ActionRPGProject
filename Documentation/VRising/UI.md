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
| `UI/Windows/MinimapWindow` | 415 | HUD 레이어, 입력을 막지 않음. `MapView` (반경 = ConstTable `MinimapRadius`, 카메라 방향 회전, 몬스터 표시) |
| `UI/Windows/WorldMapWindow` | 416 | 입력을 막는 창 (M). `MapView` (월드 전체, 북쪽 위, 지역 이름) |
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

## 지도 (MapView) · 입력 맵 (2026-10-31)

- `MapView` (`Game/Runtime/UI/Widgets`) — 미니맵 · 월드맵 공용. 계층: `Viewport`(Mask + MapView) → `Content`(회전) → `Map`(RawImage, 구운 지도) / `Markers`(표식 · 이름, 회전 안 함). 표식 종류별 스프라이트 · 색 · 크기는 `EMapMarker` 순서 배열이라 프리팹 인스펙터에서 바꾼다. 표식 · 이름은 비활성 자식 템플릿을 `UIItemList` 로 복제.
- 표식 출처: 플레이어 = `PlayerCharacter.Local`, 몬스터 · 보스 = `ActorRegistry`, 퀘스트 가이드 = `QuestManager.TryGetGuidePosition` (고리 + `GuideLine` 선, 미니맵 밖이면 `GuideArrow` 로 바뀌어 대상 쪽으로 회전), 상인 · 성 심장 · 관 = `MapMarkers` (오브젝트가 켜질 때 등록).
- 프리팹 · 표식 스프라이트는 `UIPrefabBuilder.Map`, 지도 그림은 `WorldMapBaker` ([World.md 8절](World.md#8-월드맵-그림)).
- **입력 맵**: `GameInput` 이 InputTable `Map` 열(System / Movement / Combat)마다 InputActionMap 을 만든다. `GameBootstrap.syncInputMaps` 가 매 프레임 매니저 업데이트 전에 "입력을 막는 창(`BlocksGameInput`)이 열려 있거나 콘솔 입력 중이면 Combat 끔". 그래서 창 위에서 휠을 굴려도 카메라 줌이 되지 않는다. 새 창이 전투 입력을 막지 않게 하려면 `BlocksGameInput` 을 false 로 (HUD · 미니맵 · 밸런스 미터).

## 언어 (Unity Localization, 2026-10-31)

| 단계 | 무엇이 | 어디 |
|---|---|---|
| 원본 | `StringTable.csv` — `Index, Text(영어), Korean` | `Game/Data/CSV` |
| 변환 | CSV 임포트(`Game > Data > Import All CSV`) 마지막에 `LocalizationSync` 가 Unity Localization 으로 옮김. StringTable.csv 를 저장해도 자동. 따로 실행: `Game > Data > Sync Localization` | `Game/Editor/Data/LocalizationSync.cs` |
| Localization 에셋 | Localization Settings, Locale(English · Korean — 한국어 Fallback = 영어), String Table Collection **GameText**(en/ko, Preload). 키 Id = StringTable Index, 키 이름 = `EText` 이름 또는 `Text{Index}` | `Game/Data/Localization` (Addressables `Localization-*` 그룹은 패키지가 관리) |
| 런타임 | `GameBootstrap` 이 `GameLocalization.InitializeAsync(GameTables.TextTable)` 을 기다린 뒤 UI 를 만든다. `Tables.Text(id)` → 현재 언어 테이블의 키 Id(int) 조회 → 없으면 CSV 영어 원문 | `Game/Runtime/Data/GameLocalization.cs` |
| 언어 바꾸기 | `GameLocalization.Next / SetLanguage(SystemLanguage)` → 새 테이블 로드 → `LanguageChangedEvent` → `UIText` · HUD(안내 · 퀘스트 · 혈액 · 보스 · 어빌리티 이름) · 월드맵 안내가 다시 채움. 매 프레임 갱신하는 문구는 그대로 따라옴. 목록 창(제작 · 건축 · 상점)은 열 때 다시 채움 | 타이틀 언어 버튼, 치트 `lang` |
| 시작 언어 | Startup Selector: PlayerPrefs(마지막 선택) → 없으면 한국어 | `LocalizationSync.configureStartup` |
| 폰트 | `UISkinSetup` 이 Noto Sans KR(OFL) → TMP **동적** 폰트 에셋(`Art/Generated/UI/NotoSansKR SDF.asset`)을 만들어 UI 스킨 기본 폰트 + TMP 전역 Fallback 으로 등록. 글리프는 실행 중 필요할 때 아틀라스에 추가 (Clear Dynamic Data On Build 켬) | `Art/ThirdParty/Fonts/NotoSansKR` |

- **번역 고치기**: CSV 의 `Korean` 열을 고친다. Localization Tables 창에서 고친 값은 다음 CSV 임포트에 CSV 값으로 덮어써진다 (원본은 CSV 하나).
- **새 문구**: CSV 에 행 추가(Text + Korean) → 코드에서 쓰면 `EText` 에 같은 번호. 능력치 이름은 `9000 + EAttribute` (`UIUtil.AttributeTextId`).
- **새 언어**: `LocalizationSync` 에 Locale 과 CSV 열을 추가 (지금은 en/ko 두 개).
- 동적 폰트 아틀라스는 에디터에서 플레이할 때 글리프가 쌓여 에셋이 바뀔 수 있다 (커밋 전 확인, 빌드 시에는 비워짐).
