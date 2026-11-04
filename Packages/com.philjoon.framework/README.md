# PhilJoon Framework

직접 만든 Unity Framework Package 초기 버전입니다. 후에 추가 개발을 통해 업데이트 예정에 있습니다.

Unity 런타임 · 에디터 프레임워크입니다.

- Unity 6000.3 이상
- 의존성: UniTask 2.5.11, Addressables 2.10.3, uGUI 2.0 (TextMeshPro 포함)

## 설치

### 1. UniTask 레지스트리 (처음 한 번)

UniTask 는 OpenUPM 에 있으므로 `Packages/manifest.json` 에 scoped registry 를 추가합니다.

```json
"scopedRegistries": [
  {
    "name": "package.openupm.com",
    "url": "https://package.openupm.com",
    "scopes": [ "com.cysharp.unitask" ]
  }
]
```

### 2. 패키지 추가

Package Manager → `+` → **Add package from git URL** →

```
https://github.com/SungPhilJoon/Custom_Unity_Framework.git
```

특정 버전으로 고정하려면 태그를 붙입니다: `https://github.com/SungPhilJoon/Custom_Unity_Framework.git#v0.2.0`

## 구성

| 모듈 | 경로 | 내용 |
|---|---|---|
| Ability (GAS) | `Runtime/Ability` | `AbilitySystemComponent`, `GameplayAbility` + `AbilityTask`, `GameplayEffectDef` / `Spec` / `ActiveGameplayEffect`, `AttributeSet`(Base/Current, 모디파이어 집계), `IGameplayEffectExecution`, `GameplayCueManager`, 계층 태그 `GameplayTagContainer` · `GameplayTagCountContainer` · `GameplayTagManager` |
| StateTree | `Runtime/StateTree`, `Editor/StateTree` | 언리얼 StateTree 방식 AI: 상태 · 진입 조건 · 태스크 · 평가자 · 트랜지션, 블랙보드, 디버거, 에디터 창 + 게임별 확장점 |
| UI | `Runtime/UI` | `UIManager`, `UIWindow`, `UILayer`(World/Hud/Window/Popup/Toast/System, 입력 차단 · ESC 정책), `UISkin`, `UIBar`, `UIItemList` |
| Core | `Runtime/Core` | `BaseBehaviour` / `ActorBehaviour` / `UIBehaviour`(Awake/OnDestroy 봉인 → `OnUnityAwake`/`OnUnityDestroy`), `StaticBehaviour`(정적 배칭) / `DynamicBehaviour`(SRP Batcher), `Managers`(서비스 로케이터), `KindRegistry` + `[Kind]`, `Log` |
| Asset | `Runtime/Asset`, `Editor/Addressables` | Addressables `AssetLoader`(참조 카운트), `AssetTracker`, 의존성 기록 · 정적 분석 · 이슈 추적 |
| Data | `Runtime/Data`, `Editor/Data` | CSV → `TableAsset<TRow>` ScriptableObject, CSV 한 행 편집 유틸 |
| Event | `Runtime/Event` | 할당 없는 정적 `EventBus<T>` (struct 이벤트) |
| Pool | `Runtime/Pool` | `GameObjectPool`, `IPoolable` |
| Camera | `Runtime/Camera/Occlusion` | 카메라 가림 처리 (감지 · 응답 전략 교체 가능) |
| Rendering · Util | `Runtime/Rendering`, `Runtime/Util` | `RendererHighlight`, `StaticBatching`(런타임 일괄 정적 배칭), `SharedMaterialVariants`(MaterialPropertyBlock 없는 색 변형), 수학 · 해시 · 바운드 · 지형 · 컴포넌트 유틸 |

어셈블리: `Framework.Runtime`(네임스페이스 `Framework`), `Framework.Editor`.

## 게임 코드와 연결하는 법

프레임워크는 게임 타입을 참조하지 않습니다. 게임 쪽이 아래 확장점으로 연결합니다.

| 확장점 | 사용법 |
|---|---|
| 서비스 | `IManager` 구현 → `Managers.Regist(new MyManager())`, 어디서나 `Managers.Get<MyManager>()` |
| 다형성 팩토리 | 하위 클래스에 `[Kind((int)EMyType.X)]` → `KindRegistry<TBase>.Create(id)` (enum switch 대신) |
| 어빌리티 | `GameplayAbility` 상속, 속성은 `AttributeSet` 상속(`PreAttributeChange` · `PostGameplayEffectExecute`), 피해 공식은 `IGameplayEffectExecution` → `ExecutionRegistry` |
| 태그 | 정수 id + 부모 관계를 `GameplayTagManager` 에 등록 (문자열 비교 없음, 64태그당 AND 1번) |
| StateTree 노드 | `StateTreeTask` / `StateTreeCondition` / `StateTreeEvaluator` 상속 + `[StateTreeNodeInfo]`, 이벤트 · 블랙보드 키 enum 에 `[StateTreeEventEnum]` · `[StateTreeBlackboardEnum]` |
| StateTree 에디터 | `StateTreeEditorExtension` 상속 → 템플릿 · 블록 · 컨텍스트 패널 · 툴바 (창이 자동으로 찾는다) |
| UI 레이어 | `UIBehaviour.GetLayer()` override → `UIManager.Create/Spawn` 이 해당 레이어 캔버스에 붙인다 |

## 배칭: StaticBehaviour / DynamicBehaviour

| | StaticBehaviour | DynamicBehaviour |
|---|---|---|
| 대상 | 절대 움직이지 않는 물체 | 제자리에 있지만 사라지거나 생기는 물체 |
| 배칭 | 정적 배칭 (에디터: Batching Static 자동, 런타임 생성: 프레임 끝 일괄 `StaticBatchingUtility.Combine`) | SRP Batcher (정적 플래그 해제, MaterialPropertyBlock 미사용) |
| API | `IsBatched`, `StaticBatching.AutoFlush / Flush()` | `Show / Hide / SetShown`, `SetTint / ClearTint`(공유 머티리얼 변형), `IPoolable` |

- 런타임 정적 배칭은 메시 Read/Write 가 필요하다.
- 플래그는 인스펙터에서 붙일 때 · 씬/프리팹 저장 때 · `Window > Framework > Rendering > Apply Static Flags (Open Scenes)` 에서 맞춰진다.

## 주의

- `[SerializeReference]` 데이터(StateTree 노드 등)는 어셈블리 · 네임스페이스 · 클래스 이름으로 저장됩니다. 이름을 바꿀 때는 `[MovedFrom(...)]` 를 붙이세요.
- 예측 / 네트워크 복제는 없습니다 (오프라인 권한 기준).

## 변경 기록

[CHANGELOG.md](CHANGELOG.md)
