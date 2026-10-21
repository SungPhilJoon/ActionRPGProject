# 사용한 디자인 패턴

| 패턴 | 어디에 | 왜 |
|---|---|---|
| **Service Locator + 명시적 라이프사이클** | `Framework/Runtime/Core/Managers.cs` (`IManager`, `Managers.Regist/Get/Update/Clear`) | MonoBehaviour 싱글톤 대신 순수 C# 매니저. 등록 순서 = 업데이트 순서, 역순 정리로 의존 관계가 명확 |
| **Observer (정적 제네릭 이벤트 버스)** | `Framework/Runtime/Event/EventBus.cs`, `Game/Runtime/Core/GameEvents.cs` | 게임플레이 → UI 단방향. 이벤트 종류는 문자열이 아니라 struct 타입. 타입별 정적 리스트라 Dictionary 조회/박싱 없음 |
| **Object Pool** | `Framework/Runtime/Pool/GameObjectPool.cs`, `DamageNumberLayer` | 발사체·VFX·데미지 숫자 반복 생성/파괴 방지 |
| **Flyweight** | `EffectLibrary` (EffectRow → GameplayEffectDef 캐시), 테이블 행 공유 | 모든 스펙이 같은 정의 객체를 공유 |
| **Factory** | `AbilityFactory` (EAbilityType → 구현 클래스), `UIFactory` (uGUI 조립), `ContentBuilder` (프리팹 생성) | 생성 로직을 한 곳에 모아 데이터만 바꿔 확장 |
| **Template Method** | `GameplayAbility` (CanActivate → InternalActivate → OnActivate/OnTick/OnEnd), `ListWindow` (Build → Fill) | 공통 흐름은 부모, 차이점만 자식 |
| **Strategy** | `IGameplayEffectExecution` (Damage/Heal/SunDamage/PercentDamage) + `ExecutionRegistry` | 피해 공식 교체를 데이터(EffectTable.Execution)로 선택 |
| **Command** | `CheatManager` (명령어 해시 → 핸들러), `PlayerController` (입력 → `Actor.TryActivate`) | 입력과 실행 분리. 멀티플레이 시 명령을 서버로 전송하는 지점이 됨 |
| **State (계층형 상태 머신 = StateTree)** | `Framework/Runtime/StateTree`, `MonsterBrain` 이 실행 | 몬스터 행동을 코드가 아닌 에셋으로. 상태 계층/트랜지션을 툴에서 편집 |
| **Composite** | StateTree 상태 계층 (부모 상태의 태스크도 자식이 활성인 동안 계속 실행) | 공통 행동(예: 2페이즈 버프)을 부모에 한 번만 |
| **Prototype** | `StateTreeNodeUtil.Clone` — 에셋의 노드를 몬스터마다 복제해 인스턴스 데이터로 사용 | 언리얼 InstanceData 대응 |
| **Blackboard** | `StateTreeBlackboard` (int 키) | 평가기 출력 → 조건/태스크 입력 (Property Binding 대체) |
| **Adapter** | `ActorMotor` (CharacterController / NavMeshAgent 를 같은 API 로) | 어빌리티가 이동 방식을 몰라도 됨 |
| **Data-Driven / Type Object** | CSV → ScriptableObject 테이블, CharacterRow/AbilityRow 가 "종류"를 정의 | 코드 수정 없이 밸런스·콘텐츠 변경 |
| **Facade** | `Tables` 정적 클래스 (`Tables.Const`, `Tables.Text`, `Tables.Address`) | 테이블 접근 단순화 |
| **Builder (에디터)** | `WorldBuilder`, `ContentBuilder` | 씬/프리팹을 재현 가능하게 코드로 생성 |
| **Component** | Actor = ASC + Motor + Animator + (Blood/Sun/Inventory/Equipment 또는 Brain) | Unity 컴포넌트 조합 |
| **Adapter + Dependency Inversion (Bridge)** | `ISkyController` / `SkyBridge` (Game.Runtime) ← `WeatherMakerSkyBridge` (Assembly-CSharp, `Game/Integration`) | Game.Runtime 은 WeatherMaker(asmdef 없음)를 참조할 수 없음. 인터페이스는 게임에, 구현은 외부 쪽에 두고 런타임에 등록 → WeatherMaker 가 없어도 기본 낮밤으로 동작 |
| **Null Object / Fallback** | `DayNightManager.apply` (SkyBridge.Controller 가 null 이면 자체 Directional Light) | 외부 에셋 제거 시에도 게임 유지 |
| **Builder + 캐시 (에디터)** | `MaterialUpgradeUtil` (원본 머티리얼 → URP 복사본, 같은 원본은 한 번만 변환) | 외부 에셋을 수정하지 않고 파이프라인에 맞춤 |
