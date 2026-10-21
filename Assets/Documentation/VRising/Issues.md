# 개발 중 문제 상황과 해결

## 1. 비동기 로드 레이스 → 댕글링 참조 (Addressables)

- **문제**: `LoadAssetAsync` 완료 콜백이 돌아왔을 때 요청한 오브젝트(창, 몬스터 스포너, 씬)가 이미 파괴되어 있으면 파괴된 대상에 결과를 넣거나, 핸들을 반납하지 않아 누수가 생긴다.
- **해결** (`Framework/Runtime/Asset/AssetLoader.cs`)
  - 같은 (주소, 타입)은 핸들 1개 + 참조 카운트.
  - `LoadAsync(address, owner, token)`: 완료 후 `owner` 가 파괴됐으면 결과를 버리고 즉시 Release. 취소 토큰이면 Release 후 null.
  - `SpawnManager` 는 자체 `CancellationTokenSource` 를 UnInitialize 에서 취소.
  - **동기 로드로 바꾼 곳**: 발사체/큐/무기 모델/건축 고스트는 "이번 프레임에 반드시 있어야" 하므로 `LoadSync` 를 쓴다. 대신 부트스트랩이 AssetTable 전체를 미리 비동기 로드하고, 그게 끝나야 시작 버튼이 활성화되므로 실제로는 캐시 히트다.

## 2. 같은 주소, 다른 타입 (Texture2D vs Sprite)

- **문제**: 프리로드를 `Object` 로 하니 PNG 주소가 Texture2D 로 캐시되어 이후 `LoadSync<Sprite>` 가 null.
- **해결**: 캐시 키를 `(주소 해시 << 32) | 타입 이름 해시` 로 바꾸고, AssetTable 에 `Kind`(Prefab/Sprite/Material) 컬럼을 추가해 정확한 타입으로 프리로드.

## 3. 보호막이 Base 를 음수로 만드는 문제

- **문제**: 보호막을 지속 이펙트의 Add 모디파이어(Current 에만 반영)로 두면, 피해 흡수는 Base 에서 빼므로 Base 가 음수가 되고 이펙트가 끝난 뒤 다음 보호막을 깎아 먹는다.
- **해결**: 보호막량은 Instant 이펙트(24)로 Base 에 더하고, 지속은 태그 이펙트(22, `State_Buff_Shield`)로 표현. `Actor.Update` 가 태그가 없는데 Shield > 0 이면 0 으로 만든다.

## 4. 주기 이펙트의 큐 중복 생성

- **문제**: 햇빛 화상(0.5초 주기)이 매 주기 `Cue Execute` 를 호출해 연기 파티클이 계속 쌓임.
- **해결**: 주기 실행은 Execute 큐를 내지 않고, 지속 큐(OnActive/OnRemove)만 사용.

## 5. CSV bool "0" 파싱 예외

- **문제**: `bool.Parse("0")` 예외. **해결**: CsvConvert 에서 "1"/"0" 을 먼저 처리.

## 6. HUD 가 플레이어 스폰 이벤트를 놓침

- **문제**: HUD 창은 닫힌 상태로 생성되어 OnEnable 구독 전이었고, 플레이어 스폰 이벤트가 그 사이에 발행되어 스킬 슬롯이 빈 칸(흰색)으로 표시됨.
- **해결**: `HudWindow.OnOpen` 에서 현재 플레이어로 한 번 동기화. (일반 원칙: "이벤트 + 열릴 때 현재 상태 Pull" 을 같이 쓴다)

## 7. 에디터가 포커스가 없으면 Play 모드 프레임이 진행되지 않음

- 자동화 테스트 중 `Time.frameCount` 가 1 에 멈춤. Editor 창 포커스 + `Application.runInBackground = true` 로 해결 (게임 코드 문제 아님).

## 8. Kenney 나무와 Terrain Tree 빌보드

- **문제**: "Nature/Soft Occlusion 셰이더가 아니면 빌보드가 동작하지 않음" 경고.
- **해결**: `treeBillboardDistance` 를 tree 거리보다 크게 해 빌보드를 쓰지 않음. 대신 정점 비용 증가 → 프로파일링 후보.

## 9. 그늘 판정의 한계 (→ 2026-10-14 해결, Issues 26)

- 지형 Tree(장식 나무)는 물리 트리거가 없어 그늘 판정에 들어가지 않는다. 플레이 영역의 나무는 전부 채집 노드(GameObject, 수관 트리거)로 배치해 영향을 줄였다.
- 해결 후보: 장식 나무도 캡슐 콜라이더가 있는 프리팹으로 바꾸거나, 그늘 판정용 저해상도 "그늘 맵"을 베이크.

## 11. 무기가 오른손 위치에 붙지 않음

- **문제**: 무기 소켓을 `arm-right` 본 로컬 (0, −0.2, 0.05) 에 두었는데, Kenney 미니 캐릭터의 팔 본은 어깨에서 **로컬 +X** 방향으로 뻗어 있다. 그래서 소켓이 주먹이 아니라 어깨 아래 몸통 안에 있었다.
- **확인 방법**: `arm-right` 에 100% 가중치가 걸린 정점을 본 로컬 좌표로 변환해 범위를 측정 → X 0 ~ 0.284 (주먹 끝), Y ±0.08, Z −0.07 ~ 0.12.
- **해결**: 소켓 위치를 (0.26, 0, 0.02) 로 변경 (`ContentBuilder.Actors.cs` 의 `WeaponSocketPosition`). 회전 (90, 0, 0) 은 칼날(+Y)을 캐릭터 정면(+Z)으로 돌리는 값이라 유지. 콘텐츠를 다시 빌드해 모든 캐릭터 프리팹에 반영.

## 12. 몬스터 체력바가 없음

- **원인**: 1차 구현에서 보스용 HUD 상단 바만 만들고 일반 몬스터 머리 위 체력바는 구현하지 않았다 (누락).
- **해결**: `WorldHealthBarLayer` 추가 (`GameBootstrap.setupUI` 에서 World 레이어에 생성). 데미지 숫자와 같은 "월드 → 스크린 좌표 오버레이" 방식이고 액터 ID 별로 바를 재사용한다.
  - 표시 조건: 살아 있는 적대 몬스터이면서 (피해를 입었거나 전투 중) 플레이어 30m 이내. 보스는 상단 바가 있으므로 제외.
  - 왼쪽 배지에 몬스터 레벨. 플레이어 기어 레벨보다 높으면 빨간색.
  - 스크린 좌표 변환에 캔버스 카메라를 넘기도록 `CanvasUtil.GetEventCamera` 를 추가해, 캔버스를 Camera 모드로 바꿔도 위치가 맞게 했다 (DamageNumberLayer 도 동일 적용).
- **성능 메모**: 매 프레임 전 액터 순회 + 스크린 변환. 프로파일링 후보.

## 13. (테스트 환경) 에디터 비활성 시 Play 모드 정지

- 에디터 창이 포커스를 잃으면 플레이 루프가 멈춰 자동 테스트가 진행되지 않았다. 일시정지 후 `EditorApplication.Step()` 을 반복 호출해 프레임을 진행시켜 확인했다. 게임 코드 문제는 아니다.

## 15. CSV 컬럼 추가와 컴파일 순서

- **문제**: CharacterTable 에 `StateTree` 컬럼, AssetTable 에 `Kind=Data` 를 추가하자 "컬럼에 맞는 필드 없음 / enum 값 없음" 오류. CSV 자동 재변환(AssetPostprocessor)이 스크립트 컴파일보다 먼저 돌았기 때문.
- **해결**: 컴파일 후 `Game > Data > Import All CSV` 재실행. 규칙: **행 클래스/enum 을 먼저 고치고 컴파일 → CSV 수정** 순서로 작업한다.

## 16. 몬스터 AI 를 FSM 에서 StateTree 로 교체하며 생긴 설계 결정

- 타깃 관리·어빌리티 선택·귀환 무적 처리 같은 "몬스터 공용 기능"은 노드가 아니라 `MonsterBrain` 에 남기고, 노드는 그것을 호출만 하게 했다. 노드마다 같은 로직을 복붙하지 않기 위함.
- 피격/타깃 획득은 C# 이벤트 → `Tree.SendEvent(EAIEvent)` 로 넘겨 트리의 `OnEvent` 트랜지션에서 처리 (Idle → Combat 즉시 전환).
- 상태가 "Hold ↔ Attack" 처럼 짧게 반복되는 구조라 트랜지션 로그가 빨리 쌓인다 → 디버그 로그는 최근 40건 링버퍼.

## 17. 어셈블리 이름 변경 후 StateTree 노드 Missing

- **문제**: 폴더 재정리에서 `Game.Framework` 어셈블리를 `Framework.Runtime` 으로 바꾸자, StateTree 에셋 3종에서 Framework 쪽 노드(`WaitTask`)가 "missing node" 로 검증 실패.
  `[SerializeReference]` 는 직렬화 데이터에 **어셈블리 이름 + 네임스페이스 + 클래스명**을 저장하기 때문이다. (게임 노드는 `Game.Runtime` 이름이 그대로라 영향 없음)
- **해결**: Framework StateTree 노드 클래스에 `[MovedFrom(true, "Framework.StateTree", "Game.Framework")]` 추가 → Unity 가 옛 이름으로 저장된 데이터를 새 타입으로 읽는다. 에셋을 다시 저장해 새 이름으로 갱신.
- **교훈**: SerializeReference 로 저장되는 클래스의 이름/네임스페이스/어셈블리를 바꿀 때는 항상 `MovedFrom` 을 남긴다. 스크립트 파일 이동만으로는 GUID 가 유지되지만, 어셈블리 이름은 별개다.

## 18. 성능 관찰 포인트 (최적화는 직접 진행 예정)

| 지점 | 내용 |
|---|---|
| `ActorRegistry.FindHostiles` | 매 히트/발사체 프레임마다 전 액터 순회 O(N). 액터 수 증가 시 공간 분할(그리드) 후보 |
| 태그 비교 | 비트셋 AND (태그 64개당 1워드). 정의 컨테이너는 부모 비트 캐시, 계층 변경 시 재계산 |
| `GameplayEffectSpec` 생성 | 히트마다 new + 시전자 속성 스냅샷 배열 할당 → GC. 풀링 후보 |
| `AbilitySystemComponent.executeEffect` | `EvaluatedBuffer.ToArray()` 할당 |
| `HudWindow.Update` | 매 프레임 문자열 보간(TMP SetText 가비지) |
| `FeedAbility.HasVictimInRange` | HUD 가 매 프레임 호출 (전 액터 순회) |
| `InteractableRegistry.FindNearest` | 매 프레임 |
| NavMeshObstacle carving | 채집 노드 231개가 모두 carve |
| `WorldHealthBarLayer.LateUpdate` | 매 프레임 전 액터 순회 + WorldToScreenPoint |
| StateTree | 몬스터마다 매 프레임 Tick, AbilityReadyCondition 이 상태 선택마다 PickAbility 호출, 노드 JSON 복제(스폰 시) |
| 지형 Detail (InstanceCountMode, 해상도 512 / 패치 16) | 풀 인스턴스 수가 많음. detailObjectDistance·밀도 조정 후보 |
| Terrain Tree 약 2,300그루 (메시, 빌보드 없음) | 정점/드로우콜. 빌보드 지원 나무로 교체 시 개선 |
| TheTalesFactory 포토스캔 바위 | 고폴리 메시 + 4K 텍스처. LOD 확인 필요 |
| WeatherMaker 구름/하늘 패스 | 풀스크린 GPU 패스. 구름 품질 프로필로 조정 가능 |
| Magic Pig / unity-chan 스키닝 | 몬스터 20마리 SkinnedMesh. Animator Culling Mode 확인 후보 |

## 19. 지형 풀이 그려지지 않음 (부분 해결, 조사 중)

- **문제 1**: TerrainSampleAssets 의 풀 Detail 은 HDRP 용 Shader Graph(TerrainGrass) 머티리얼이라 URP 에서 그려지지 않았다.
  **해결**: 풀/고사리 메시만 가져와 URP Lit **Alpha Clip + 양면** 머티리얼로 새 Detail 프리팹을 생성 (`WorldBuilder.makeUrpDetail`, `Art/Generated/World/Detail`).
- **문제 2**: 그래도 풀이 없었다. 원인은 Terrain 의 Detail Scatter Mode 가 **Coverage** 모드(0~255 를 덮는 비율로 해석)였던 것. 우리는 셀당 개수(Instance Count)로 값을 썼다.
  **해결**: `SetDetailResolution` 전에 `data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode)`. 편집 모드 Scene 뷰에서 풀이 그려지는 것을 확인.
- **남은 문제**: Play 모드 (215,150) 에서 `GetDetailLayer` 합이 0, 화면에도 풀이 없음. 원인 미확정 — 좌표 변환(GetDetailLayer 의 x/y 순서), 해당 위치 Splat 가중치, 런타임 변경 여부를 확인할 차례.

## 20. Magic Pig 애니메이션 이벤트 에러 폭주

- **문제**: Magic Pig 클립에 `PlayAudio`, `SetDesiredHairWeight` AnimationEvent 가 박혀 있는데 받는 컴포넌트가 없어 "has no receiver" 에러가 프레임마다 출력.
- **해결**: 외부 FBX 를 수정하지 않고 `Animator.fireEvents = false` (빌더에서 프리팹에, 그리고 `ActorAnimator.Awake` 에서 한 번 더). 게임은 애니메이션 이벤트를 쓰지 않고 어빌리티 타이밍으로 히트를 판정하므로 영향 없음.

## 21. WeatherMaker "Enable URP" 메뉴가 파이프라인 에셋을 교체함

- **문제**: WeatherMaker 의 URP 활성화 메뉴는 자체 파이프라인 에셋으로 바꿔 버려 기존 `PC_RPAsset` 설정(그림자, SSAO)이 사라진다.
- **해결**: 메뉴를 쓰지 않고 필요한 것만 수동 적용 — `UNITY_URP` 스크립팅 디파인, grab-pass 대체, `PC_Renderer` 에 Render Feature 추가 (순서: ScreenSpaceShadows → WeatherMakerURPRenderFeatureScript → SSAO).

## 22. WeatherMaker 와 게임 낮밤의 시간 주인 충돌

- **문제**: WeatherMaker 도 자체 시간 흐름이 있어, 두 시스템이 각자 해를 돌리면 화면의 해와 햇빛 화상 판정 방향이 어긋난다.
- **해결**: 게임이 시간의 주인. WeatherMaker Speed/NightSpeed = 0, 매 프레임 `TimeOfDay = hour × 3600`. 태양 방향은 거꾸로 WeatherMaker Sun 라이트에서 읽어 판정에 사용 (`ISkyController` 브리지, DesignPatterns 참고).
- **어셈블리 문제**: WeatherMaker 는 asmdef 가 없어 Assembly-CSharp 에 있다. Game.Runtime(asmdef)은 Assembly-CSharp 를 참조할 수 없으므로, 인터페이스(`ISkyController`)는 Game.Runtime 에, 구현(`WeatherMakerSkyBridge`)은 asmdef 없는 `Assets/Game/Integration` 에 두었다. 브리지는 `RuntimeInitializeOnLoadMethod` 로 씬의 WeatherMaker 에 자동 부착.
- **밝기**: WeatherMaker 태양만으로는 쿼터뷰 지면이 어두워 `DirectionalLightIntensityMultipliers` 에 1.7 배율 + Day 볼륨 노출 0.35.

## 23. 외부 에셋 머티리얼이 분홍색 (Built-in / Standard / UnityChan 셰이더)

- **문제**: ASTROFISH, Kenney, unity-chan, Magic Pig 일부, TheTalesFactory 머티리얼이 Built-in 셰이더.
- **해결**: `Framework/Editor/Util/MaterialUpgradeUtil` 이 URP Lit **복사본**을 만들어 (`Art/Generated/Materials/*`) 생성 프리팹에서만 교체. 원본 에셋은 수정하지 않는다 (에셋 업데이트 시 덮어쓰기 안전). `_MainTex/_Color/_BumpMap/_MetallicGlossMap/_Glossiness/_OcclusionMap/Emission/Cutout/Transparent/_Cull` 매핑.
- **한계**: unity-chan 의 툰 셰이딩(림/그림자 램프)은 URP Lit 으로 바꾸며 사라졌다. 툰 룩이 필요하면 URP 용 UnityChanToonShader 가 필요 (AssetRequests).

## 24. Kenney 나무가 하늘색으로 보임

- **문제**: 지형 Tree 로 쓰니 Kenney 나무 정점색/머티리얼이 WeatherMaker 앰비언트에서 청록색으로 떠 보였다.
- **해결**: 잎 (0.16, 0.27, 0.11), 껍질 (0.24, 0.17, 0.12) 로 재채색한 프리팹 생성 (`ContentBuilder.GetRecoloredTree`). 근본 해결은 사실적 나무 에셋 (AssetRequests).

## 25. 리타겟 / 소켓

- RPG Character Pack 클립은 Humanoid 라 unity-chan 에 그대로 리타겟된다. 검 위치는 두 캐릭터의 손 본 방향이 달라 오프셋을 직접 맞추기 어려워, **같은 포즈를 두 캐릭터에 샘플링해 손 기준 상대 변환을 계산**하는 방식으로 자동화했다 (`ContentBuilder.computeWeaponSocket`). 검 크기는 0.7 배.
- unity-chan 원본 스크립트(IdleChanger, FaceUpdate 등)는 구 Input Manager 를 써서 Input System 전용 설정에서 예외가 나므로 생성 프리팹에서 제거.
- 횃불이 공중에 떠 보이던 문제 → 기둥(PortchColumn) 위에 얹는 구성으로 변경.

## 26. 화면상 그늘에 있는데 햇빛 피해를 받음 (판정 ≠ 그림자)

- **원인**: 화면 그림자는 GPU 섀도우맵(모든 그림자 메시), 판정은 물리 레이캐스트(콜라이더만)라서 두 형태가 달랐다.
  1. Terrain Tree 약 2,300그루는 콜라이더가 없어 그림자만 보이고 판정에서 빠졌다.
  2. ASTROFISH 건물/쉼터는 일부 부품에만 콜라이더가 있어 지붕·처마 그림자가 판정에 없었다.
  3. 레이 시작점을 태양 쪽으로 0.6m 띄워서, 벽에 붙어 서 있으면 시작점이 벽 너머가 되어 벽을 놓쳤다.
  4. 판정 지점이 가슴 높이 1곳뿐이라, 발밑은 그늘인데 상체만 햇빛에 걸리는 경계에서 화상이 났다. 레이 길이 80m 라 멀리 있는 언덕 그림자도 놓쳤다.
  5. WeatherMaker 구름 그림자는 물리로 표현할 수 없다.
- **해결**
  - `Framework/Editor/Util/ShadeColliderUtil`: 그림자를 드리우는 MeshRenderer 마다 **같은 메시의 MeshCollider** 를 `SunBlocker` 레이어로 붙인다 → 판정 형태 = 그림자 형태. LOD1 이하와 그림자 끈 렌더러는 제외.
    - 랜드마크(`WorldBuilder.place`), 채집 노드·건축물 프리팹(`ContentBuilder.saveShadedPrefab`) 에 적용.
    - Terrain Tree 는 `BuildTreeShade` 가 인스턴스마다 나무 메시 콜라이더를 만든다 (`[TreeShade]`, 2,332개).
    - 빌드 시점에 만들어 두므로 런타임 메시 쿠킹이 없고 Read/Write 설정이 필요 없다.
    - SunBlocker 는 Player/Monster 와 충돌하지 않고, NavMesh·지면·발사체 마스크에도 없어 이동/전투에 영향 없음.
  - `SunExposure`: 발밑(0.2m)·몸통(1.0m) 2점 판정, **하나라도 가려지면 그늘**. 시작 오프셋 0.05m, 레이 길이 400m. Gizmo 는 선택했을 때 판정 레이를 그린다 (가려짐 = 하늘색).
  - 구름 그림자: `WeatherMakerSkyBridge.CloudShadows = false` (기본). 구름은 판정을 막지 않으므로 그리지 않는다.
- **확인**: 15시(태양 고도 32°)에 나무 그림자와 집 그림자 안으로 순간이동 → `IsExposed = false`, 체력 유지. 폐허 마을 1,380 지점 샘플 중 30 지점이 "예전엔 화상 / 이제 그늘".
- **남은 차이**: 캐릭터(스키닝 메시)의 그림자, 풀(Detail)의 그림자는 판정에 들어가지 않는다. 의도된 동작.
- **성능 메모**: 정적 MeshCollider 약 2,300(나무) + 700(랜드마크) + 노드/건축물마다 추가. 레이캐스트는 플레이어당 0.15초마다 2회. 프로파일링 후보.

## 27. 밤이 너무 어두워 월드가 보이지 않음

- **원인**: WeatherMaker 기본 밤 Ambient 세기가 0.1~0.25로 낮고, 달빛 배율은 0.5. 달이 지평선 아래면 달빛이 0. 여기에 Night 볼륨의 ACES + 대비 14 + 비네트가 어두운 영역을 더 눌렀다.
- **해결**
  - `WeatherMakerSkyBridge`: 밤 Ambient ×6 (`NightAmbientScale`), 새벽·황혼 ×2.5, 달빛 배율 1.6. WeatherMaker 는 Play 때 프로필을 복제해 쓰므로 원본 에셋은 바뀌지 않는다.
  - `VP_Night`: 노출 0.35 → 1.5, 대비 14 → 4, 채도 −20 → −10, 비네트 0.34 → 0.25 (WorldBuilder.buildVolumes 동일 값).
- **조정 위치**: 더 밝게 하려면 `NightAmbientScale` 또는 `VP_Night` 노출을 올리면 된다.

## 28. BlockedTag 가 관계없는 어빌리티까지 막음 (태그 비교 방향 오류)

- **문제**: `Ability_Weapon` 을 차단하면 주문·기본 공격까지 모두 막혔다.
- **원인**: 비트마스크 하나에 명시 태그와 부모 태그를 섞어 저장했고, `IsAbilityBlocked` 가 **양쪽을 모두 부모로 펼쳐** 비교했다.
  - 차단 목록: `{Ability_Weapon}` → 저장 시 `{Ability_Weapon, Ability}`
  - 질의: `{Ability_Spell}` → `Expand` 후 `{Ability_Spell, Ability}`
  - 공통 부모 `Ability` 가 겹쳐서 매칭됨.
- **해결**: 언리얼과 같은 의미로 태그 시스템을 개편 (GAS.md "태그 비교 규칙").
  - `TagMask`/`TagContainer`/`TagHierarchy` 삭제 → `GameplayTagManager`, `GameplayTagContainer`, `GameplayTagCountContainer`.
  - 명시 태그와 부모 캐시를 분리하고, 질의 쪽은 명시 태그만 비교. 비교 방향을 API 가 강제하므로 호출하는 쪽에서 `Expand` 를 쓰지 않는다.
  - `IsAbilityBlocked(abilityTags) = abilityTags.HasAnyExplicitOf(BlockedAbilityTags)` (언리얼 AreAbilityTagsBlocked).
  - 비교는 비트셋 그대로 유지 (64태그당 AND 1번). 128개 상한 제거.
  - 덤으로 고친 것: 갖지 않은 태그를 `RemoveTag` 하면 부모 카운트만 줄어드는 문제 → 실제로 바뀐 만큼만 부모에 반영.
- **확인**: 계층 비교 단위 케이스 20개 통과. Play 에서 `Ability_Weapon` 차단 → 무기 스킬만 BlockedByTags, 주문·기본 공격 허용. `Ability` 차단 → 전부 차단. 기절 → 차단, 둔화 → 허용.
- `GameAbility` 의 주석 처리된 `BlockAbilitiesWithTags` 줄은 이제 의도대로 동작한다 (다시 켤지는 기획 판단).

## 29. 몬스터가 이동하거나 다가올 때 끊김 (원인 분석)

측정 방법: Play 중 근접 몬스터(메두사) 1마리를 플레이어 옆에 소환하고, 매 프레임 `Time.deltaTime`, 몬스터 속도(`Motor.CurrentSpeed`, `NavMeshAgent.velocity`), 경로 상태(`pathPending`/`hasPath`/`isStopped`), 거리, StateTree 활성 상태를 CSV 로 기록했다 (420~700프레임). 튀는 프레임은 Unity Profiler 계층 데이터로 확인했다.

### 원인 1 (주원인): 주기적인 프레임 히치 — 에디터에 남은 WeatherMaker 물 프리팹 미리보기 씬

- **현상**: 몬스터 속도 값 자체는 4.19~4.20 으로 일정한데, **정확히 15프레임마다** 프레임 시간이 10ms → 45~58ms 로 튀었다. 화면 전체가 0.18초 주기로 멈칫하므로, 움직이는 몬스터에서 가장 잘 보인다.
- **Profiler**: 튀는 프레임에서 `WeatherMakerDepthCameraScript.Update → Camera.Render (WeatherMakerWaterPrefab_YDepthBufferCamera)` 약 7ms + `WaitForRenderJobs` 약 20ms.
- **코드**: `WeatherMakerDepthCameraScript.Update` 에 `#if UNITY_EDITOR ... if (!Application.isPlaying || ++dirtyCounter == 15) Dirty = true;` → 에디터에서는 15프레임마다 물 깊이 카메라가 씬을 한 번 더 렌더링한다.
- **왜 있었나**: 게임은 WeatherMaker 물을 쓰지 않는다. 앞선 작업에서 물 프리팹을 조사하려고 `PrefabUtility.LoadPrefabContents` 로 열고 **닫지 않아**, 에디터의 미리보기 씬(`WeatherMakerWaterPrefab (preview)`)에 남아 `ExecuteInEditMode` 로 계속 돌았다. (제 실수. 게임 코드 문제 아님)
- **해결**: `UnloadPrefabContents` 로 미리보기 씬을 닫고, 숨김 카메라(HideAndDontSave)를 삭제했다. 재측정: 700프레임 평균 9.8ms, 30ms 초과 4프레임(주기 없음).
- **빌드 영향**: 없음 (에디터 전용 코드 + 에디터 미리보기 씬). 에디터를 재시작해도 사라진다.
- **재발 방지**: 프리팹을 코드로 열면 반드시 `UnloadPrefabContents`. 게임/프레임워크 코드에는 해당 호출이 없음을 확인했다.

### 원인 2 (작음): 추적 시작·정지가 즉시 일어남

- **추적 시작**: Chase 진입 후 경로 계산(비동기)으로 **2~3프레임 정지**, 이후 가속도 40 으로 약 10프레임(0.1초) 만에 최고 속도. 이전 상태(Attack/Hold)가 `Motor.Stop()` → `ResetPath()` 로 경로를 지우기 때문에 매번 새로 계산한다.
- **공격 진입**: 사거리(AttackRange × 0.8) 도달 프레임에 속도 **4.19 → 0 으로 즉시 정지** (`Stop()` 이 `isStopped` + `ResetPath`). 감속 구간이 없다.
- 애니메이터 Speed 파라미터는 0.1초 댐핑이 있어 다리 모션은 부드럽지만, 몸 이동은 칼같이 멈추고 출발한다 → "툭툭 끊기는" 느낌의 일부.
- 개선 후보 (아직 적용하지 않음 — 공격 판정 위치와 연동돼 있어 기획 확인 필요):
  - 공격 진입 직전 짧은 감속 (`stoppingDistance` 를 사거리에 맞추고 autoBraking 사용)
  - Chase → Chase 재진입 때 경로 유지 (`ResetPath` 대신 `isStopped` 만)

### 원인 3 (설계상 정상): 공격 중 이동 잠금

- 근접 공격 `MoveLock` 0.9초 동안 몬스터가 제자리에 선다. 플레이어가 그 사이 멀어지면 "멈췄다 다시 쫓는" 것처럼 보인다. V Rising 도 공격 모션 중에는 이동하지 않으므로 의도된 동작이다. 짧게 하려면 AbilityTable 의 `MoveLock` 을 줄이면 된다.

### 원인이 아니었던 것 (측정으로 배제)

- Chase ↔ Hold 상태 진동: 이번 측정 시나리오(접근 / 플레이어보다 빠른 추격 / 느린 도주)에서는 발생하지 않았다.
- 경로 재계산(0.25초 주기 `SetDestination`): 기존 경로를 따라가는 중에 새 경로를 계산하므로 속도가 떨어지지 않았다 (`pathPending=True` 프레임에도 4.20 유지).

## 30. 공격 중 다른 공격 입력 (입력 버퍼) + 콤보 초기화

- **요구**: 공격 중에도 다른 공격을 입력할 수 있고, 들어온 순서대로 처리. 앞 처리 때문에 평타 간격(ComboWindow)을 넘기면 콤보는 1타부터.
- **구현**
  - `GameAbility`: 공격 어빌리티는 실행 중 `BlockAbilitiesWithTags`(Primary/Weapon/Spell/Ultimate/Feed/Monster) → 겹쳐 나가지 않는다. (Issues 28 의 태그 수정으로 이제 의도대로 동작)
  - `AbilityInputBuffer` (선입선출, 최대 3개, 같은 슬롯 1번): 막힘 이유가 BlockedByTags/AlreadyActive 면 대기, 쿨다운·비용·대상 없음이면 버림. 회피(Travel)는 큐를 비우고 즉시 실행(공격 캔슬).
  - 만료(3초)는 **큐 맨 앞이 된 순간부터** 센다. 처음에는 입력 시점부터 세서, Q(1.2초) 뒤에 쌓인 평타가 만료되는 버그가 있었다 (테스트로 발견, 수정).
  - `MeleeComboAbility`: 콤보 간격은 실제 발동 시점 기준 → 큐 때문에 늦게 나가면 자동으로 1타.
- **확인 (Play)**: 평타 중 Q, 평타 입력 → [평타(1타)] → [Q] → [평타(1타, Q 1.2초 > ComboWindow 0.5초라 초기화)].

## 31. 상태 소모(Consume) 가 같은 시전에서 바로 터짐

- **문제**: Chaos Volley 는 2발인데, 1발째가 건 Ignite 를 2발째가 즉시 소모해 Ignite 가 남지 않았다.
- **해결**: 소모할 상태 이펙트가 `ConsumeMinAgeSeconds`(0.3초) 이상 지났을 때만 소모 (`CombatUtil.hasAgedEffect`, 경과 = Duration − Remaining).
- **확인**: 1회 시전 → Ignite 유지, 다음 시전 → 폭발 (피해 40 → 62).

## 32. 스킬 이펙트 팩(SpecialSkillsEffectsPack) 과 오브젝트 풀

- **문제**: 팩의 보조 스크립트(`DelayActive` 는 Start 에서 1회 타이머, `NewMaterialChange` 는 페이드 후 자기 Destroy)가 풀 재사용과 맞지 않는다 → 두 번째 재생부터 일부가 사라지거나 늦게 켜짐.
- **해결**: `PooledEffect` 래퍼 — 루트만 풀링하고, 꺼낼 때마다 원본 이펙트를 자식으로 새로 Instantiate (돌려놓을 때 Destroy). 원본 에셋 무수정.
- **크기**: 원본이 데모용이라 수십 m (파티클 반경 최대 25m). 모든 파티클이 ScalingMode.Hierarchy 라서, 빌더가 Shape 반지름/시작 크기로 실제 크기를 재서 목표 반지름에 맞춘다 (`measureEffectRadius`). 미세 조정은 CueTable.Scale (예: Ignite 폭발 0.3).
- **URP**: 팩 셰이더(GAPH Integrated / Distortion / Decal)가 URP 서브셰이더를 갖고 있어 그대로 동작. Depth/Opaque Texture 켜져 있음 (데칼·소프트 파티클). Distortion 은 팩의 `GaphURPDistortion` 이 자동 등록.
- **성능 메모**: 시전마다 Instantiate/Destroy (GC·CPU). 프로파일링 후 필요하면 팩 스크립트를 제외한 순수 파티클 이펙트만 풀링하는 방식으로 바꿀 수 있다.

## 33. 몬스터가 플레이어에게 다가올 때 끊김 (Issues 29 이후 남은 원인)

측정: 몬스터 3마리 + 플레이어가 원을 그리며 이동(약 2.8m/s, 240프레임마다 방향 반전), 900프레임 동안 몬스터별 실제 이동 속도, NavMeshAgent velocity/desiredVelocity, 프레임당 회전각, StateTree 상태 기록.

- **원인 1 — 모두 플레이어 한 점을 목적지로 씀 → 회피(RVO)가 서로 밀어냄**: 추적 중인데도 Agent 의 desiredVelocity 자체가 4.2 → 약 2.0 으로 떨어졌다 (경로 감속이 아니라 회피 계산). 먼저 도착해 공격 중인 몬스터들이 플레이어 앞을 막아, 뒤 몬스터는 3.8m 에서 더 못 들어가고 좌우로 비비며 느리게 움직였다.
- **원인 2 — 방향을 회피가 섞인 velocity 로 정함**: 프레임마다 0° ↔ 8~11° 로 회전이 지그재그 → 눈에 보이는 "툭툭 끊김".
- **원인 3 — 목적지 갱신(0.25초) 때 회전이 한 프레임에 8~10° 튐**.
- **해결**
  - `MonsterBrain.GetApproachPoint`: 목적지 = 타깃 → 나 방향으로 사거리 안쪽(× `ApproachRatio` 0.6) + 개체별 각도 슬롯(±25°×2) → 몬스터마다 다른 지점으로 접근.
  - `ActorMotor`: 바라보기는 `Agent.steeringTarget`(경로 다음 모서리) 기준, 회전은 지수 감쇠 Slerp(`AgentTurnSharpness` 10). 회피 우선순위를 개체마다 다르게, MedQuality 회피, 움직이는 대상 추적이라 `autoBraking` 끔.
- **결과 (같은 시나리오)**

| | 수정 전 | 1차 (접근점·steering·우선순위) | 2차 (+회전 감쇠) |
|---|---|---|---|
| 추적 중 속도 3m/s 미만 | 35% | 15% | 19% |
| Agent 가 원하는 속도부터 3 미만 | 503프레임 | 94 | 231 |
| 프레임당 6° 넘는 회전 튐 | 253 | 106 | **25** |

  (시나리오에 플레이어 경로·몬스터 배치가 매번 달라 속도 수치는 ±5% 정도 흔들린다.)
- **남은 것**: 3마리 이상이 같은 쪽에 몰리면 여전히 회피로 감속한다 (정상적인 군중 동작). 더 줄이려면 플레이어 주위 공격 슬롯 예약(토큰) 방식이 필요하다.

## 34. Batches 700~900 — 원인 분해 (측정)

Play 중 폐허 마을(300, 205), 낮 13시, `UnityStats` 를 항목별로 끄면서 측정:

| 상태 | Batches | SetPass | Shadow Casters |
|---|---|---|---|
| 기본 | 893 | 322 | 653 |
| 지형 나무 끔 | 720 (−173) | 163 | 479 |
| 메인 라이트 그림자 끔 | 452 (−441) | 170 | 212 |
| 랜드마크(폐허 마을·바위) 끔 | 472 (−421) | 235 | 252 |
| 후처리 끔 | 865 (−28) | 307 | 644 |

관찰 사실
- **그림자 패스가 절반**: 그림자 캐스터 653개 × 캐스케이드 2단 (거리 90m).
- **랜드마크**: MeshRenderer 1,427개 (ASTROFISH 건물이 부품 단위로 쪼개져 있음), 재질 27종. 1,127개만 Static.
- **지형 나무**: 2,332그루, 재질(M_TreeLeaves/M_TreeBark) `enableInstancing = false`, 빌보드 미사용 → 인스턴싱 0.
- `instancedBatches = 0`, `staticBatches ≈ 1`. SRP Batcher 는 켜져 있다 → Batches 숫자는 크지만 SetPass(322) 가 실제 상태 전환 비용에 더 가깝다.
- 최적화는 직접 진행 예정이므로 수정하지 않았다 (후보: 그림자 거리/캐스케이드, 작은 소품 그림자 끄기, 나무 재질 인스턴싱 + 빌보드, 랜드마크 메시 결합/LOD, Static 플래그 정리).

## 35. 몬스터 메시가 NavMeshAgent 보다 앞서 걷다가 루트로 튀어 돌아옴 (끊김의 실제 원인)

- **발견**: 사용자 관찰 — Agent 는 일정 속도로 가는데 몬스터 메시가 앞장서 가다가, 루트에서 일정 거리 벗어나면 다시 루트로 돌아오기를 반복.
- **원인**: Magic Pig 몬스터의 걷기 클립이 제자리(in-place)가 아니다. Generic 리그에 Root node 가 지정돼 있지 않아 클립의 전진 이동이 그대로 이동 뼈에 들어간다. `applyRootMotion = false` 라 루트(Agent)는 이 이동을 받지 않고, 메시만 앞서가다 클립이 반복될 때 루트로 튄다.

| 몬스터 | 클립 | 이동 뼈 | 클립 1회 전진 (주기) |
|---|---|---|---|
| 메두사 | Walk | Root | 1.18m (0.83초) |
| 코브라 | AlertWalk | base_snake | 9.93m (3.93초) |
| 트롤 | Walk | Ogre_root | 1.91m (1.5초) |
| 미노타우로스 | Walk | Hips | 0.97m (0.93초) |

- **해결 (2단계)**
  1. 임포트 설정 (`ContentBuilder.makeInPlace`, 메뉴 `Game/Setup/3c`): FBX 의 Root node = 위 이동 뼈, 클립 XZ 는 Root node 기준(Original)으로 루트 모션 추출(Bake 해제), Y·회전은 포즈에 유지. 외부 FBX 의 `.meta` 임포트 설정만 변경 (모델·애니메이션 데이터는 그대로). 코브라·트롤은 이것만으로 해결됐다.
  2. 런타임 안전장치 `AnimationInPlace` (Game/Runtime/Actor): 애니메이터가 포즈를 쓴 직후 LateUpdate 에서 이동 뼈의 XZ 를 쉬는 자세 값으로 고정. 메두사는 임포트 설정만으로는 추출되지 않았다 (원인 미확정 — 메두사 Root 뼈의 스케일이 10 인 점이 다르다). 빌더가 모든 몬스터 모델에 붙인다.
- **확인 (Play, 걷기 중, 애니메이션 컬링 끈 상태)**: 이동 뼈의 루트 대비 전방 오프셋 — 코브라 0.00m, 트롤 0.00m, 메두사 메시 중심 −0.05~−0.04m (여러 루프 동안 1cm 범위, 되돌아오기 0회). 수정 전 메두사 0~1.2m 왕복.
- **측정 함정**: 몬스터 Animator 는 `CullUpdateTransforms` 라서 화면 밖에서는 뼈가 갱신되지 않는다 → 화면 밖 측정은 "멈춤" 으로 보인다. 그리고 편집 모드에서 `Animator.Update` 수동 호출로 잰 값도 뼈가 움직이지 않아 무효였다.
- 부수 효과: 공격 동작 안의 전진(돌진 모션)도 제자리가 된다. 실제 돌진은 어빌리티(Dash/Leap)가 이동시키므로 위치의 주인은 Agent 하나로 유지된다.

## 36. 귀환(ReturnHome) 중 몬스터가 제자리에 멈춤 — 매 프레임 SetDestination

- **원인**: `ReturnHomeTask.Tick` 이 매 프레임 `SetDestination(Home)`. 240m 처럼 먼 경로는 계산에 여러 프레임이 걸리는데 매 프레임 처음부터 다시 요청돼 `pathPending = true`, `hasPath = false`, 속도 0 으로 멈춰 있었다 (Play 에서 확인).
- **해결**: 진입 때 한 번만 요청, 경로를 잃었을 때(`HasPathOrPending == false`)만 재요청. `ChaseTargetTask` 도 계산 중(`IsPathPending`)에는 재요청하지 않는다.
- **확인**: 귀환 상태 몬스터가 4.2m/s 로 이동.
