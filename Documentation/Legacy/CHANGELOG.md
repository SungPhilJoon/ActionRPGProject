# 개발 기록 (CHANGELOG)

이 문서는 지금까지 이 프로젝트에서 실제로 무엇을 만들고, 무엇을 왜 그렇게 결정했는지에 대한
작업 기록이다. 아키텍처 자체에 대한 설명(각 시스템 사용법, 확장 방법)은 `README.md` 를 본다.
이 문서는 "언제 무엇을 했는가" 와 "왜 그렇게 했는가" 를 시간 순으로 남겨서, 나중에
다시 합류하는 사람이나 미래의 나 자신이 판단 근거를 추적할 수 있게 하는 게 목적이다.

---

## 2026-09-29 · 1차: 3D 협동 액션 RPG 프레임워크 기초 구축

### 요청 배경
Netcode 기반으로 한 방 최대 4인이 던전을 함께 도는 3D 액션 RPG. 중세 배경, 검/활 액션,
스킬은 4개 슬롯이며 게임 입장 전 덱에서 골라 들어가는 방식. 본격 플레이 전 튜토리얼로
스토리와 조작을 가르친다. "3D RPG의 기본 프레임워크"를 만들어 달라는 요청이었다.

### 시작 시점 프로젝트 상태
- Unity 6000.3.24f1, URP 3D 템플릿으로 갓 생성된 새 프로젝트
- Netcode for GameObjects 는 아직 설치되지 않음
- Input System 액션 에셋(`InputSystem_Actions.inputactions`)만 기본 템플릿 상태로 존재

### 한 일
1. **패키지**: `com.unity.netcode.gameobjects` 2.0.0 을 `Packages/manifest.json` 에 추가.
2. **폴더 구조**: `Assets/_Project/` 아래에 `Scripts/Core|Combat|Character|Skills|AI|Dungeon|
   Networking|Tutorial|UI|Input`, `Editor`, `Data`, `Resources`, `Prefabs`, `Scenes` 를 만들고
   `ActionRPG.Runtime` / `ActionRPG.Editor` 두 개의 asmdef 로 어셈블리를 분리.
3. **권한 모델 결정** (이후 전체 설계의 기준이 됨):
   - 플레이어 이동은 **소유 클라이언트 권한** (`ClientNetworkTransform`) — 조작감 우선.
   - 적 이동/AI, 체력/피해/사망, 쿨다운/마나는 **서버 권한** — 치팅 방지.
   - 발사체는 궤적을 모든 피어가 동일하게 계산하고 명중 판정만 서버가 함 — 빠른 화살도 끊김 없이.
4. **Core 레이어**: `GameConstants`, `CombatTypes`(IDamageable, Faction, DamageInfo, WeaponType,
   DamageType, StatType, StatModifier), `StatBlock`(NetworkVariable 로 복제되는 unmanaged 구조체),
   `GameEventBus`(게임플레이 → UI/튜토리얼 단방향 이벤트), `ActorRegistry`.
5. **Input**: `InputReader` — 처음에는 Input System 의 `.inputactions` JSON 에셋
   (`RPGControls.inputactions`)을 `Resources` 에서 불러와 이름으로 액션을 찾는 방식으로 시작.
6. **Combat**: `CharacterStats`(기본 스탯 + 버프 보정 합성), `Health`(서버 권한 체력/사망),
   `DamageCalculator`(방어력 수확체감 공식), `CombatQuery`(구/부채꼴/레이캐스트 판정 헬퍼),
   `StatusController`+`StatusEffectDefinition`(버프/디버프/도트/군중제어 통합 표현),
   `Projectile`(궤적 동일 계산 + 서버 명중 판정).
7. **Character**: `CharacterMotor`(CharacterController 기반, 네트워크를 모르는 순수 이동 로직),
   `CharacterAnimatorDriver`, `ClientNetworkTransform`, `WeaponController`(검/활 전환),
   `BasicAttackController`(평타, 검 3연타 콤보 / 활 발사체), `PlayerCameraRig`(3인칭 궤도 카메라),
   `PlayerController`(입력 처리, 소유자 전용), `PlayerInteractor`.
8. **스킬/덱 시스템** (핵심 요구사항):
   - `SkillDefinition`(ScriptableObject) + `SkillEffect[]` 조합식 설계.
   - 효과를 `Execute`(서버 전용 판정) / `ExecuteVisual`(전 피어 연출)로 분리 —
     연출용 프리팹 참조를 RPC 로 보낼 필요가 없어지는 핵심 설계 결정.
   - 효과 8종: Arc/Area Damage, Projectile, Dash, Heal, Apply Status, Spawn Networked Prefab, Play VFX.
   - `SkillDatabase`(ID ↔ 에셋 매핑, `Resources` 폴더 필수), `SkillLoadout`(4슬롯, INetworkSerializable),
     `SkillCaster`(낙관적 예측 쿨다운 → 서버 재검증 → 거절 시 롤백), `LoadoutStorage`(PlayerPrefs).
9. **AI**: `EnemyDefinition`(스탯 + 공격 패턴 배열), `ThreatTable`(4인 파티 어그로 관리, 감쇠 +
   전환 관성), `EnemyBrain`(NavMeshAgent 로 경로만 계산, 실제 이동은 CharacterMotor 로 통일).
10. **던전**: `DungeonManager`(방 순서, 전멸 판정), `DungeonRoom`(웨이브 소환, 문 잠금/개방),
    `DungeonDoor`.
11. **네트워킹**: `ConnectionManager`(호스트/클라이언트 시작, 접속 승인 시 덱 검증),
    `SessionManager`(서버측 접속자 정보), `PlayerSpawnSystem`(스폰 위치, 부활),
    `PlayerNetworkIdentity`(이름/덱 적용, 소유자에게 텔레포트 RPC로 위임).
12. **튜토리얼**: `TutorialStep`(추상 클래스, 에셋을 복제해서 진행 상태 오염 방지),
    구현체 7종(Dialogue/MoveDistance/LookAround/PerformAction/DefeatEnemies/ReachPoint/Wait),
    `TutorialDirector`(호스트 모드로 혼자 돌아가되 실제 게임과 같은 코드 경로 사용).
13. **UI**: HUD(체력/마나/스킬 슬롯/목표/대사/파티 프레임/사망 오버레이), 덱 편집 화면, 메인 메뉴,
    월드 스페이스 체력바, 피해량 숫자 팝업.
14. **에디터 자동화 도구 3종** (`ActionRPG` 메뉴): 샘플 콘텐츠(스킬 10종·상태이상 4종·적 3종·
    튜토리얼 시퀀스) 생성, 샘플 프리팹(플레이어·적 3종·화살) 생성, 샘플 씬(MainMenu·Tutorial·
    Dungeon_01) 생성 — 프리미티브 메시로 즉시 플레이해서 시스템을 검증할 수 있게.
15. **검증**: Unity 에디터를 직접 실행하지 못하는 환경이라, 실제 Unity 6000.3.24f1 의
    UnityEngine/UnityEditor/Netcode DLL 을 대상으로 Roslyn(`csc.dll`)을 직접 호출해
    전체 54개 스크립트를 컴파일 검증 (경고 0, 오류 0).

### 이 시점에 의도적으로 비워 둔 것
아트(프리미티브만), 애니메이션 실제 연결, 로비 UI, 인터넷 매치메이킹, 아이템/장비/성장,
실제 세이브 시스템.

---

## 2026-09-29 · 2차: 구문 오류 수정 + 대규모 아키텍처 확장

### 요청 배경
1차 산출물을 Unity 에디터에서 열어 본 사용자가 `IDamageable`/`Faction`/`DamageInfo` 등에서
구문 오류를 확인. 동시에 다음 요구사항을 추가로 요청:
- `Assets/_Project` 래퍼 없애고 표준 `Assets/` 구조로 평탄화, 자주 안 바뀌는 부분은
  dll/package 로 분리.
- Input System 은 유지하되 C# 스크립트로 직접 개발할 수 있게.
- Addressables 로 로컬 에셋 번들 시스템 구성, 커스텀 카탈로그 기능 포함.
- 기획자가 CSV 로 데이터를 드리븐하고 이를 ScriptableObject 로 최종 직렬화.
- Protobuf 로 직렬화 최적화.
- UniTask 로 멀티스레드/동기화 처리 최적화.
- 오브젝트 라이프사이클을 나중에 만들 Tool 로 추적할 수 있게 연관 오브젝트를 매핑.
- Save 시스템은 나중에 클라우드로 확장할 수 있도록 지금 추상화.

### 구문 오류 조사 결과
Unity 에디터의 `Editor.log` 를 직접 열어 원인을 추적. 실제 원인은 1차 작업 중 실수로
`NetworkBootstrapGuard.cs` 에 남겼던 오타 특성 `[Tameltip_Placeholder]` 하나였고, 이게
해당 컴파일 패스 전체를 깨뜨리면서 같은 어셈블리의 다른 모든 파일이 "타입을 찾을 수 없음"
연쇄 오류를 낸 것이었다. 이 오타는 1차 작업 도중 이미 스스로 발견해 고쳤던 상태였고,
사용자가 본 오류는 그 수정 전 시점의 stale 콘솔 상태였다. 이번 2차 작업의 대규모 리컴파일로
자연히 해소됨을 확인.

### 사전 조사
작업 전 이 환경에서 외부 네트워크 접근이 가능한지 확인 (`git ls-remote`, OpenUPM/NuGet
레지스트리 조회). 가능함을 확인한 뒤, 다음 패키지의 정확한 최신 버전과 실제 배포 아카이브를
직접 내려받아 압축 해제 — 실제 API 시그니처를 소스코드에서 확인하고, 이후 컴파일 검증에
실제 DLL/소스를 사용하기 위함:
- `com.cysharp.unitask` 2.5.11 (OpenUPM)
- `org.nuget.protobuf-net` 3.4.30 + `org.nuget.protobuf-net.core` 3.4.30 (OpenUPM)
- `com.unity.addressables` 2.10.3 (Unity 공식 레지스트리, 79MB 전체 소스 패키지)

이 과정에서 `Input System` 패키지(이미 로컬에 캐시됨)의 `InputActionSetupExtensions.cs` 를
직접 읽어 `AddAction`/`AddBinding`/`AddCompositeBinding` 의 정확한 파라미터 시그니처를 확인했고,
Addressables 소스에서 `AddressableAssetSettings.CreateGroup`/`CreateOrMoveEntry`/`GetSchema<T>`/
`kLocalBuildPath` 등의 정확한 API 를 확인한 뒤 코드를 작성했다 — 기억에 의존하지 않고
실제 배포된 소스를 대조하는 방식으로 정확도를 높임.

### 구조 개편
1. `Assets/_Project/{Scripts,Editor,Data,Resources,Prefabs,Documentation}` 를 `.meta` 를 보존한
   채(GUID 유지) `Assets/{Scripts,Editor,Data,Resources,Prefabs,Documentation}` 로 이동.
   `Assets/_Project/Scenes/*` 는 이미 존재하던 `Assets/Scenes/` 에 병합.
2. `Assets/Scripts/Core` (자주 안 바뀌는 기반 타입)를 통째로 떼어
   **`Packages/com.actionrpg.foundation`** 로컬 UPM 패키지로 이전.
   `package.json`, `Runtime/ActionRPG.Foundation.asmdef`, `Editor/ActionRPG.Foundation.Editor.asmdef`
   신설. `ActionRPG.Runtime.asmdef` 가 이 패키지를 참조하도록 갱신 (역방향 참조 금지 원칙 수립:
   Foundation 은 게임플레이 코드를 몰라야 재사용 가능).

### 새로 추가한 시스템

| 시스템 | 위치 | 핵심 내용 |
|---|---|---|
| Input System 코드화 | `Assets/Scripts/Input/RPGInputActions.cs` | `.inputactions` JSON 대신 `InputActionMap`/바인딩을 전부 C# 코드로 생성. `InputReader` 가 이를 소비하도록 재작성. |
| Addressables + 커스텀 카탈로그 | `Foundation/Runtime/Assets/*` | `IAssetCatalog`/`AddressableAssetCatalog`/`CatalogService`(UniTask 기반), `AssetHandle<T>`(라이프사이클 자동 연동). 기본 카탈로그 외 추가 카탈로그를 얹는 `LoadAdditionalCatalogAsync` 확장점. 에디터 쪽 `AddressablesBootstrapper`(로컬 그룹 생성, 폴더 일괄 등록). |
| CSV 데이터 드리븐 | `Foundation/Runtime/DataDriven`, `Foundation/Editor/DataDriven` | `[CsvColumn]`/`[CsvPrimaryKey]` 특성, `CsvParser`(따옴표/이스케이프 지원 순수 C#), `CsvImportProfile`(에디터 전용 SO), `CsvScriptableObjectImporter`(리플렉션 기반 제네릭 엔진 — 타입을 안 가림). 예시로 `EnemyStatRow.cs` + `Assets/Data/CSV/EnemyStats.csv` + 데모 셋업 메뉴. |
| Protobuf 직렬화 | `Foundation/Runtime/Serialization` | 실제 protobuf-net 라이브러리(OpenUPM) 연결. `IBinarySerializer`/`ProtoBinarySerializer`/`SaveEnvelope<T>`. `SkillLoadout` 구조체에 `[ProtoContract]`/`[ProtoMember]` 추가해 세이브 데이터에 중첩 직렬화되게 함. AOT/IL2CPP 리스크를 코드 주석에 명시. |
| 세이브 시스템 추상화 | `Foundation/Runtime/Save` | `ISaveService` → `LocalFileSaveService`(스레드풀 I/O, protobuf-net), `CloudSaveServiceStub`(자리 표시자), `CompositeSaveService`(1차 실패 시 2차 폴백), `SaveManager`(교체 가능한 정적 창구). 게임 쪽 `PlayerProfileData`/`PlayerProfileService`(`Assets/Scripts/Save/`)가 `LoadoutStorage.Save()` 호출 시 자동으로 비동기 저장. |
| 라이프사이클 트래킹 | `Foundation/Runtime/Lifecycle`, `Foundation/Editor` | `ITrackable`/`LifecycleTracker`/`LifecycleHandle`(IDisposable 값 타입). `Health`/`Projectile` 에 연결. `ActionRPG > Lifecycle Tracker` 에디터 창으로 실시간 확인 가능. |
| UniTask 적용 범위 | 매니페스트 + 위 시스템들 | 스레드/비동기 경계가 실제로 있는 곳(파일 I/O, Addressables 로딩)에만 적용. 전투/스킬의 프레임 동기 시퀀싱은 기존 Coroutine 유지 — Coroutine 을 무조건 대체하는 게 아니라 용도를 구분하는 결정. |

### 패키지 의존성 변경
`Packages/manifest.json` 에 OpenUPM scoped registry(`package.openupm.com`) 추가 후
`com.cysharp.unitask`, `org.nuget.protobuf-net`(+ 전이 의존성), `com.unity.addressables` 등록.

### 검증
1차와 같은 방식(Roslyn 직접 호출)이되, 이번에는 실제로 내려받은 UniTask 소스(156개 파일),
protobuf-net/protobuf-net.Core DLL(netstandard2.1), Addressables 전체 소스(Runtime+Editor,
테스트 제외)까지 참조/소스에 포함해 **총 520개 파일**을 한 번에 컴파일. 최종적으로
**프로젝트 자체 코드(Assets/Scripts, Assets/Editor, Packages/com.actionrpg.foundation) 오류
0건**을 확인 (남은 오류 1건은 Addressables 의 Runtime/Editor 두 개 실제 어셈블리를 검증
편의상 하나로 합쳐서 생긴 순수 하네스 artifact이며, 실제 프로젝트에서는 발생하지 않음 — 이유까지
확인해 원인 불명 상태로 남기지 않음).

### 이 시점에 남아 있는 주의사항 / 다음 할 일
- Unity 에디터가 `Packages/manifest.json` 변경을 인터넷 연결 상태에서 실제로 내려받아
  `Library/PackageCache` 에 채워 넣어야 에디터 내부 컴파일도 통과한다 (Package Manager 창에서
  세 패키지가 보이는지 확인 필요).
- protobuf-net 을 IL2CPP(모바일/콘솔) 로 실제 배포하기 전에는 `RuntimeTypeModel.Compile(...)`
  사전 컴파일이 필요 (지금은 에디터/스탠드얼론 검증 단계라 기본 리플렉션 모드로 충분).
- `CloudSaveServiceStub` 은 아직 `NotImplementedException` 을 던지는 자리 표시자 — 실제
  클라우드 SDK 가 정해지면 이 파일만 채우면 됨.
- Addressables 로컬 그룹/카탈로그 실제 생성은 에디터 메뉴(`ActionRPG > Addressables > ...`)를
  사용자가 직접 한 번 실행해야 함 (라이브 에디터의 프로젝트 잠금 때문에 이 세션에서 대신
  실행할 수 없었음).

---

## 2026-10-02 · 3차: 능력치 · 스킬을 언리얼 GAS 구조로 재구성 (리슨 서버 방식)

### 요청 배경
캐릭터의 체력 · 마나 · 스킬을 언리얼 엔진 Gameplay Ability System 과 같은 구조로 만들고,
Netcode 로 언리얼 GAS 의 리슨 서버 방식처럼 동작하게 해 달라는 요청.

### 한 일
1. **GAS 프레임워크** (`Packages/com.actionrpg.foundation/Runtime/AbilitySystem/`, 게임을 모르는 계층)
   - AbilitySystemComponent(NetworkBehaviour), AttributeSet, GameplayEffect(+Spec/Context/Active/Query),
     MMC · Execution, GameplayAbility(+Spec/ActorInfo/정책), AbilityTask, GameplayTag(+Container/CountContainer),
     GameplayCue(+Manager/Notify), AbilitySystemGlobals, 라이라식 AbilitySet.
   - 리슨 서버 모델: 호스트는 권한 실행, 원격 클라이언트는 PredictionKey 로 예측 실행 →
     ServerTryActivateAbility → ClientActivateAbilitySucceed/Failed, ReplicatedPredictionKey 로 예측분 정리.
   - 복제: 어트리뷰트 · 태그 · 지속형 큐는 모두에게, 활성 이펙트는 복제 모드(Minimal/Mixed/Full)에 따라,
     어빌리티 스펙은 소유자에게.
2. **게임 쪽** (`Assets/Scripts/Abilities/`): HealthSet · ManaSet · CombatSet, DamageExecution · HealExecution,
   CooldownMagnitudeCalculation, GA_BasicAttack, ActionRPGGameData, GameTags.
3. **교체**: `CharacterStats` · `StatusController` · `StatusEffectDefinition` · `BasicAttackController` 삭제.
   `Health` 는 HealthSet 래퍼(라이라 HealthComponent), `SkillDefinition` 은 GameplayAbility 서브클래스,
   `SkillCaster` 는 덱 → 어빌리티 부여 + 입력 전달, 상태이상은 GameplayEffect 에셋(GE_Stun/Poison/Slow/Rally).
4. **에디터**: 태그 · 어트리뷰트 드롭다운, ASC 런타임 디버그 인스펙터, `Globals 갱신` 메뉴.
   샘플 콘텐츠 · 프리팹 생성기가 GAS 에셋과 ASC 프리팹을 만든다.
5. **파일 정리**: Unity 는 ScriptableObject/MonoBehaviour 클래스 이름과 파일 이름이 같아야 에셋이 스크립트를 찾는다.
   한 파일에 여러 클래스가 있던 스킬 효과 · 튜토리얼 스텝 · DamageNumber 등을 클래스별 파일로 나눴다
   (Editor.log 의 "No script asset for ..." 경고 원인). `SkillDeck.cs` 는 GUID 를 유지한 채 `SkillDeckPreset.cs` 로 이름을 바꿨다.
6. 기타: `SampleSceneGenerator` 가 씬 배치 NetworkObject 의 GlobalObjectIdHash 를 생성하도록 저장 순서 변경.

### 왜 이렇게 했나
- 프레임워크를 Foundation 에 둔 이유: 게임 규칙(체력 · 마나 수치, 피해 공식)과 무관한 범용 시스템이라서.
  게임 규칙은 AttributeSet · Execution · 이펙트 에셋으로 Runtime 쪽에 둔다.
- 피해를 메타 어트리뷰트(IncomingDamage)로 받는 이유: 무적 · 사망 판정과 피해 이벤트를 한 곳에서 처리하기 위해 (라이라와 같은 방식).
- 쿨다운을 공용 이펙트 + SetByCaller + 동적 태그로 만든 이유: 스킬마다 쿨다운 에셋을 만들지 않기 위해 (GASDocumentation 권장 방식).

### 검증
명령줄 C# 빌드(Unity 가 만든 csproj 를 복사해 소스를 와일드카드로 포함)로 4개 어셈블리 모두 오류 0 · 경고 0.
Unity 플레이 모드와 멀티플레이 실행은 아직 확인하지 않았다.

### 적용 순서
`ActionRPG > 샘플 콘텐츠 생성` → `샘플 프리팹 생성` → `샘플 씬 생성`.
기존 프리팹 · 상태이상 에셋은 삭제된 스크립트를 참조하므로 반드시 다시 생성해야 한다.
