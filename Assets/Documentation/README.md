# ActionRPGProject — 3D 협동 액션 RPG 프레임워크

Unity 6000.3.24f1 / URP / Netcode for GameObjects 2.0.0 기반.
중세 배경, 검과 활, 4인 협동 던전, 덱에서 고른 스킬 4개로 싸우는 3D 액션 RPG의 뼈대다.

**2차 개편(이 문서 기준)에서 바뀐 것**: 프로젝트 구조를 `Assets/_Project/...` 래퍼 없이
표준 `Assets/...` 레이아웃으로 평탄화했고, 자주 바뀌지 않는 기반 코드를
`Packages/com.actionrpg.foundation` 로컬 패키지로 분리했다. 그리고 Input System 코드 기반 액션,
Addressables(로컬 커스텀 카탈로그), CSV → ScriptableObject 데이터 드리븐 파이프라인,
protobuf-net 직렬화, UniTask, 오브젝트 라이프사이클 트래킹, 클라우드 이전을 염두에 둔
세이브 시스템 추상화를 추가했다. 아래 문서는 전부 이 최신 구조 기준이다.

---

## 0. 프로젝트 구조 한눈에

```
ActionRPGProject/
  Assets/
    Scripts/            게임플레이 코드 (ActionRPG.Runtime 어셈블리) — 자주 바뀐다
    Editor/             에디터 도구 (ActionRPG.Editor 어셈블리)
    Data/               생성된 ScriptableObject, CSV 원본 (Data/CSV), CSV 결과물 (Data/Generated)
    Resources/          SkillDatabase, 네트워크 프리팹 목록
    Prefabs/            캐릭터 · 적 · 발사체 · UI
    Scenes/             MainMenu, Tutorial, Dungeon_01
    Documentation/       이 문서
  Packages/
    manifest.json        패키지 의존성 (OpenUPM scoped registry 포함)
    com.actionrpg.foundation/   로컬 패키지 — 자주 바뀌지 않는 기반 코드 (아래 1장)
```

**왜 나눴는가**: `Assets/Scripts` 는 스킬 밸런스, AI 패턴, UI 처럼 매일 손대는 코드다.
`Packages/com.actionrpg.foundation` 은 전투 기초 타입, 직렬화, 세이브, 라이프사이클 추적처럼
한 번 자리잡으면 거의 안 바뀌는 코드다. asmdef 경계로 컴파일 단위를 분리해 두면
게임플레이 코드를 고칠 때마다 기반 코드가 다시 컴파일되지 않고, 다음 프로젝트에
`Packages/com.actionrpg.foundation` 폴더만 그대로 복사해 재사용할 수도 있다.

---

## 1. 처음 한 번만: 셋업

### 1.1 패키지 해석 대기

`Packages/manifest.json` 에 Unity 공식 패키지 외에 OpenUPM scoped registry 로
아래 세 가지를 추가해 두었다. **Unity 에디터가 인터넷에 연결되어 있어야** 자동으로 받아진다.

| 패키지 | 용도 |
|---|---|
| `com.cysharp.unitask` | 스레드/비동기 작업 (Addressables 로딩, 세이브 I/O) |
| `com.unity.addressables` | 로컬 에셋 번들 시스템 |
| `org.nuget.protobuf-net` | 세이브 데이터 바이너리 직렬화 |

에디터를 열어 두면 Package Manager 가 백그라운드에서 알아서 받는다. `Window > Package Manager`
에서 세 패키지가 목록에 뜨면 준비된 것이다. (사내망이라 OpenUPM 접근이 막혀 있다면
`Packages/manifest.json` 의 `scopedRegistries` 를 사내 미러로 바꾸면 된다.)

### 1.2 샘플 콘텐츠 생성 (3단계)

Unity 에디터 상단 **ActionRPG** 메뉴에서 위에서부터 순서대로 실행한다.

| 순서 | 메뉴 | 하는 일 |
|---|---|---|
| 1 | `ActionRPG > 샘플 콘텐츠 생성` | GAS 에셋(게임플레이 이펙트 · 계산 클래스 · GA_BasicAttack · AbilitySet), 스킬 10종, 상태이상 이펙트 4종, 적 정의 3종, 덱 프리셋 2종, 튜토리얼 시퀀스, `Resources/SkillDatabase` · `ActionRPGGameData` · `AbilitySystemGlobals` 생성 |
| 2 | `ActionRPG > 샘플 프리팹 생성` | 레이어(Player/Enemy/Ground/Interactable) 추가, PlayerCharacter · 적 3종(모두 AbilitySystemComponent 포함) · Arrow 프리팹, 네트워크 프리팹 목록 생성 |
| 3 | `ActionRPG > 샘플 씬 생성` | MainMenu / Tutorial / Dungeon_01 씬 생성 + Build Settings 등록 + NavMesh 굽기 |

> **먼저 할 것:** `Window > TextMeshPro > Import TMP Essential Resources`.
> TMP 기본 폰트가 없으면 UI 텍스트가 보이지 않는다.

끝나면 **MainMenu** 씬에서 재생하면 된다.

### 1.3 (선택) Addressables 로컬 그룹 준비

CatalogService 로 에셋을 로드해 볼 계획이라면 한 번 실행해 둔다.

- `ActionRPG > Addressables > 초기 설정 (로컬 그룹 생성)` — 원격 CDN 없이 빌드에 포함되는
  로컬 그룹을 만든다.
- 프로젝트 창에서 폴더(예: `Assets/Prefabs/Enemies`)를 선택하고
  `ActionRPG > Addressables > 선택 폴더의 프리팹을 Addressable 로 등록` 실행.

### 1.4 (선택) CSV 데이터 드리븐 데모

- `ActionRPG > 데이터 > CSV 데모 프로필 생성` 한 번 실행 →
  `Assets/Data/CSV/EnemyStats.csv` 가 `Assets/Data/Generated/EnemyStats/*.asset` 으로 변환된다.
- CSV 를 고친 뒤에는 `Assets/Data/CSV_EnemyStats.asset` 을 선택하고
  `ActionRPG > 데이터 > CSV 임포트 (선택된 프로필)` 만 다시 실행하면 된다.

### 멀티플레이 테스트
- 호스트: MainMenu → `방 만들기`
- 클라이언트: 빌드를 하나 만들어 실행 → 주소 `127.0.0.1`, 포트 `7777` → `참가하기`
- 또는 `Window > Multiplayer > Multiplayer Play Mode` 로 에디터 안에서 여러 인스턴스를 띄운다.

---

## 2. 조작

| 키 | 동작 |
|---|---|
| WASD | 이동 (카메라 기준) |
| 마우스 이동 | 카메라 회전 |
| Shift | 달리기 |
| Space | 점프 |
| Left Ctrl | 회피 구르기 (쿨다운 0.8초) |
| 마우스 좌클릭 | 평타 (검은 3연타 콤보, 활은 화살) |
| Tab | 검 ↔ 활 전환 |
| Q / E / R / F | 덱 슬롯 1~4번 스킬 |
| G | 상호작용 |

게임패드도 같은 액션에 매핑되어 있다.

**바인딩은 JSON 에셋이 아니라 코드에 있다.** `Assets/Scripts/Input/RPGInputActions.cs` 가
Input System 의 `InputActionMap`/`InputAction`/바인딩을 전부 C# 코드로 생성한다.
바꾸려면 이 파일을 편집하면 된다 — Input Actions 에디터 창을 열 필요도, 별도 코드 재생성도 필요 없다.
(`Assets/Resources/RPGControls.inputactions` 는 예전 방식의 참고용으로만 남겨 두었고
런타임에는 더 이상 로드되지 않는다.)

```csharp
// RPGInputActions.cs 발췌 — 이런 식으로 순수 C# 로 바인딩을 정의한다.
Attack = Player.AddAction("Attack", InputActionType.Button);
Attack.AddBinding("<Mouse>/leftButton");
Attack.AddBinding("<Gamepad>/rightTrigger");
```

---

## 3. 아키텍처

### 3.1 권한 모델

PvE 협동이라 "조작감 우선 / 전투는 서버 판정"으로 나눴다.

| 대상 | 권한 | 이유 |
|---|---|---|
| 플레이어 이동 | **소유 클라이언트** (`ClientNetworkTransform`) | 입력 지연 없이 즉시 움직여야 액션 게임이 된다 |
| 적 이동 / AI | **서버** (`NetworkTransform`) | 모두가 같은 적 위치를 봐야 한다 |
| 체력 · 마나 · 능력치 · 상태이상 | **서버** (GAS 어트리뷰트 · 이펙트) | 치팅 방지의 핵심 지점 |
| 평타 · 스킬 | **리슨 서버식 GAS 예측** — 소유 클라이언트가 먼저 실행, 서버가 검증 | 거절되면 `ClientActivateAbilityFailedRpc` 로 예측 쿨다운 · 비용을 되돌린다 |
| 발사체 | 궤적은 **모든 피어가 동일 계산**, 명중은 **서버** (발사 시점 피해 스펙 적용) | 빠른 화살이 끊겨 보이지 않게 |

> 능력치 · 스킬 시스템은 언리얼 GAS 구조로 되어 있다. 자세한 내용은 [AbilitySystem.md](AbilitySystem.md).

> 소유자가 아닌 피어에서는 `CharacterMotor` 를 **꺼야** 한다.
> 켜 두면 CharacterController 의 중력이 복제된 좌표와 매 프레임 싸운다.
> `PlayerController` / `EnemyBrain` 의 `OnNetworkSpawn` 에서 처리하고 있다.

### 3.2 폴더 (게임플레이 어셈블리)

```
Assets/Scripts/                 ActionRPG.Runtime asmdef
  Input/       InputReader, RPGInputActions (Input System 코드 기반 액션)
  Abilities/   GAS 게임 쪽: 태그, AttributeSet(Health/Mana/Combat), 피해 · 회복 Execution, 쿨다운 MMC, GA_BasicAttack, GameData
  Combat/      Health(HealthSet 래퍼), Projectile, 피해 수식, 범위 판정
  Character/   CharacterMotor, PlayerController, 카메라 리그, 무기, 상호작용
  Skills/      SkillDefinition(GameplayAbility), SkillCaster, SkillDatabase, 덱, Effects/
  AI/          EnemyDefinition, EnemyBrain, ThreatTable
  Dungeon/     DungeonManager, DungeonRoom, DungeonDoor
  Networking/  ConnectionManager, SessionManager, PlayerSpawnSystem, PlayerNetworkIdentity
  Save/        PlayerProfileData(Protobuf), PlayerProfileService — Foundation 의 Save 계층에 얹는 게임 쪽 데이터
  Tutorial/    TutorialDirector, TutorialStep + 스텝 구현들
  UI/          HUD, 스킬 슬롯, 파티 프레임, 덱 편집, 메인 메뉴, 데미지 숫자
  Data/        CSV 로 데이터 드리븐하는 예시 (EnemyStatRow)
Assets/Editor/                  ActionRPG.Editor asmdef — 샘플 콘텐츠 · 프리팹 · 씬 생성기, Addressables 부트스트랩
Packages/com.actionrpg.foundation/   ActionRPG.Foundation asmdef — 1장 참고
```

`ActionRPG.Runtime` 은 `ActionRPG.Foundation` 을 참조한다 (반대 방향은 성립하지 않는다 —
Foundation 은 게임플레이 코드를 몰라야 재사용 가능한 기반이 된다).

### 3.3 시스템 간 결합

게임플레이 → UI/튜토리얼/사운드 방향은 전부 `GameEventBus`(Foundation) 정적 이벤트로만 흐른다.
UI 는 게임플레이 코드를 참조하지만, 게임플레이는 UI 를 모른다.
**구독자는 반드시 `OnDisable` 에서 해제할 것.**

---

## 4. 스킬 / 덱 시스템

### 4.1 구조

```
SkillDefinition : GameplayAbility (ScriptableObject)
  ├ Id (ushort)          ← 덱 · 네트워크 식별자 (쿨다운 태그 Cooldown.Skill.<Id> 에도 쓰인다)
  ├ 쿨다운 / 마나 / 사거리 / 요구 무기 / 덱 코스트  ← 공용 이펙트(GE_Cooldown_Skill, GE_Cost_Mana)의 SetByCaller 로 전달
  └ Effects[] (SkillEffect)
        ├ Execute(ctx)        ← 서버에서만. 게임플레이 이펙트 적용 (피해 · 회복 · 상태이상), 네트워크 스폰
        ├ ExecuteLocal(ctx)   ← 조종하는 피어에서만. 돌진 같은 이동 (예측 실행)
        └ ExecuteVisual(ctx)  ← 모든 피어에서. GameplayCue.Skill.Effect 큐로 VFX 재생
```

연출용 프리팹 참조를 RPC 로 보내지 않는다. 큐 파라미터에 실린 스킬 ID 로 각 피어가 같은 스킬 에셋을 찾아 스스로 재생한다.

### 4.2 시전 흐름 (GAS)

1. 소유자 `SkillCaster.TryCast(slot, aimDir, groundPoint)` — 조준 정보를 만들어 `ASC.AbilityLocalInputPressed(slot, targetData)`
2. 원격 클라이언트는 예측 키를 만들어 어빌리티를 먼저 실행하고 `ServerTryActivateAbilityRpc` 를 보낸다 (호스트는 바로 권한 실행)
3. `CommitAbility` 가 쿨다운 · 마나 이펙트를 적용 (클라이언트는 예측 적용 → HUD 즉시 반영)
4. 서버가 태그(사망 · 기절 · 침묵) · 쿨다운 · 비용을 재검증하고 같은 어빌리티를 실행
5. `CastTime` 뒤 효과 조각 실행 (효과별 `DelaySeconds` 로 다단히트)
6. 거절되면 `ClientActivateAbilityFailedRpc` 로 예측 실행 · 쿨다운 · 비용을 되돌림

자세한 흐름은 [AbilitySystem.md](AbilitySystem.md) 9장.

### 4.3 새 스킬 만들기

1. `Assets > Create > ActionRPG > Skills > Effects > ...` 로 효과 에셋 생성
   (Arc Damage / Area Damage / Projectile / Dash / Heal / Apply Status / Spawn Prefab / Play VFX)
2. `Assets > Create > ActionRPG > Skills > Skill` 로 스킬 생성, 효과를 배열에 넣기
3. **`Id` 를 다른 스킬과 겹치지 않게 지정** (0은 빈 슬롯 의미)
4. `Resources/SkillDatabase` 의 목록에 추가
5. `ActionRPG > Ability System > Globals 갱신` 실행 (클라이언트가 어빌리티 ID 를 해석할 수 있게 등록)

> ID 가 중복되거나 데이터베이스에 없으면 콘솔에 에러가 뜬다.
> 호스트와 클라이언트의 SkillDatabase · AbilitySystemGlobals 는 반드시 같아야 한다.

### 4.4 새 효과 타입 만들기

```csharp
[CreateAssetMenu(menuName = "ActionRPG/Skills/Effects/My Effect")]
public class MyEffect : SkillEffect
{
    public override void Execute(in SkillCastContext ctx)
    {
        // 서버 전용: 게임플레이 이펙트를 적용한다
        // var spec = CombatSpecs.MakeDamageSpec(ctx.SourceASC, ctx.MakeEffectContext(hitPoint, dir), 10f, 1f, 0f, DamageType.Physical);
        // CombatSpecs.ApplyToTarget(ctx.SourceASC, spec, target);
    }

    public override void ExecuteLocal(in SkillCastContext ctx)
    {
        // 조종하는 피어 전용: 이동 권한이 필요한 일 (돌진 등)
    }

    public override void ExecuteVisual(in SkillCastContext ctx)
    {
        // 모든 피어: 상태를 바꾸지 않는 연출만 (GameplayCue.Skill.Effect 로 호출됨)
    }
}
```

### 4.5 덱

- 로컬 선택은 `LoadoutStorage` 가 PlayerPrefs 에 저장 (UI 가 즉시 읽는 값)
- 저장할 때마다 `PlayerProfileService.SaveCurrentAsync()` 가 같이 호출되어
  Protobuf 로 직렬화된 `PlayerProfileData` 도 `SaveManager` 를 통해 비동기로 저장된다
  (8장 참고) — 이게 나중에 클라우드로 옮겨 갈 "진짜" 프로필이다.
- 접속 시 `ConnectionPayload` 에 실려 서버로 전달
- 서버가 `SkillDatabase.Validate()` 로 검증 (중복 · 미등록 ID 거름) 후 `SkillCaster.ApplyLoadout()` 가 슬롯 0~3 에 스킬 어빌리티를 `GiveAbility`
- 덱 코스트 상한은 `DeckBuilderView.costLimit` (기본 8)

---

## 5. 전투 시스템

### 5.1 피해 공식 (`DamageExecution`)

```
원 피해 = SetByCaller.Damage + 공격력 × AttackPowerScale + 주문력 × AbilityPowerScale   (공격자 값은 스냅샷)
치명타면 × 치명타 배율
최종 피해 = 원 피해 × (100 / (100 + 방어력))      ※ Damage.Type.True 태그면 감소 없음
→ HealthSet.IncomingDamage 에 더해지고, HealthSet.PostGameplayEffectExecute 가 Health 를 깎는다
```

밸런싱은 `DamageExecution` · `DamageCalculator` 와 각 효과 에셋의 수치에서 만진다.

### 5.2 상태이상 = 게임플레이 이펙트

버프 · 디버프 · 도트 · 군중제어는 모두 `GameplayEffect` 에셋이다 (`GE_Stun`, `GE_Poison`, `GE_Slow`, `GE_Rally`).
행동 제한은 이펙트가 부여하는 태그로 표현한다.

| 태그 | 막는 것 | 확인하는 곳 |
|---|---|---|
| `State.Debuff.Stun` | 이동 · 평타 · 스킬 · AI | PlayerController, 어빌리티 ActivationBlockedTags, EnemyBrain |
| `State.Debuff.Root` | 이동 | PlayerController |
| `State.Debuff.Silence` | 스킬 | SkillDefinition ActivationBlockedTags |
| `State.Debuff.Disarm` | 평타 | GA_BasicAttack ActivationBlockedTags |

### 5.3 능력치 = 어트리뷰트

`HealthSet` · `ManaSet` · `CombatSet` 이 능력치를 가진다. 플레이어 기본값은 `GE_InitStats_Player`(Override 이펙트),
적 기본값은 `EnemyDefinition.Stats`(StatBlock)을 `AttributeInitializer` 가 옮긴다.
보정은 지속형 이펙트의 모디파이어로 하며, 집계 순서는 언리얼과 같다: ((Base + ΣAdd) × ΣMul) ÷ ΣDiv.

---

## 6. 적 AI

`EnemyBrain` 은 서버에서만 돈다. 상태: Idle / Patrol / Chase / Attack / Return / Stagger / Dead.

- **경로**는 `NavMeshAgent` 가 계산하고 **이동**은 `CharacterMotor` 가 한다
  (`updatePosition = false`). 플레이어와 같은 이동 규칙을 쓰기 위해서다.
- **어그로**는 `ThreatTable` 이 관리한다. 4인 파티에서 누구를 때릴지 정하는 곳이다.
  - 받은 피해량만큼 위협 수치 누적, 초당 감쇠
  - 대상 전환에 관성(`SwitchThreshold` 배)을 둬서 어그로가 덜덜 떨리지 않게 함
  - `Taunt()` 로 즉시 1위 만들기 지원 (도발 스킬용)
- **공격 패턴**은 `EnemyDefinition.Attacks[]` 에 사거리 · 쿨다운 · 가중치로 여러 개를 두고 뽑는다.
  `WindupTime` 이 플레이어가 회피할 수 있는 시간이다.

보스 예시(`EN_OrcChieftain`)는 근접 휘두르기 + 원거리 도약 강타 2패턴을 가진다.

---

## 7. 던전 진행

```
DungeonManager
  └ DungeonRoom[]  (순서대로 클리어)
        ├ 트리거에 플레이어가 들어오면 Begin()
        ├ 문 잠금 → 웨이브 순차 소환 → 전멸 대기 → 다음 웨이브
        └ 전부 정리되면 문 개방 + 체크포인트 갱신
```

- 모든 플레이어가 쓰러진 채 `wipeGraceSeconds` 가 지나면 실패 처리
- 사망한 플레이어는 `PlayerSpawnSystem` 이 `respawnDelay` 후 체크포인트에서 부활시킨다
- `DungeonManager.Restart()` 로 던전 전체 초기화 가능

새 방을 추가하려면 씬에 `DungeonRoom` (+ NetworkObject + 트리거 콜라이더)을 두고
`DungeonManager.rooms` 배열에 순서대로 넣으면 된다.

---

## 8. 데이터 · 직렬화 · 세이브 (Foundation 패키지)

`Packages/com.actionrpg.foundation` 에 있다. 자세한 내용은
`Packages/com.actionrpg.foundation/Documentation~/com.actionrpg.foundation.md` 참고.

### 8.1 CSV 데이터 드리븐

기획자가 코드를 몰라도 밸런스 수치를 직접 관리할 수 있게 하는 파이프라인.

1. ScriptableObject 필드에 `[CsvColumn("컬럼명")]` 을 붙인다 (`EnemyStatRow.cs` 참고).
2. `CsvImportProfile` 에셋을 만들어 CSV 파일 + 대상 스크립트 + 출력 폴더를 지정한다.
3. `ActionRPG > 데이터 > CSV 임포트` 실행 — 행(row)마다 에셋을 생성/갱신하고,
   CSV 에서 사라진 ID 는 (옵션에 따라) 에셋도 같이 지운다.

지원 타입: string / int / long / float / double / bool / enum / 위 타입의 배열(`;` 구분).
`#` 으로 시작하는 행은 주석으로 건너뛴다. `EnemyDefinition` 처럼 스킬 배열 같은 중첩 구조가
있는 복잡한 데이터는 CSV 한 장으로 표현하기 어려우므로, 이런 "평평한" 밸런스 테이블에만 쓴다.

### 8.2 Protobuf 직렬화

`ProtoBinarySerializer` (protobuf-net 기반)가 `IBinarySerializer` 를 구현한다.
JSON 대비 크기가 훨씬 작고 파싱도 빠르다. 세이브 데이터(`PlayerProfileData`)에 쓰고 있다.

```csharp
[ProtoContract]
public sealed class MyData
{
    [ProtoMember(1)] public int Value;
}
```

**⚠ AOT / IL2CPP 주의**: 기본 동작은 `System.Reflection.Emit` 으로 직렬화 코드를 런타임에
동적 생성한다. 에디터 · Mono/CoreCLR 스탠드얼론에서는 문제없지만, IL2CPP 로 빌드하는
모바일/콘솔에 실제로 배포하기 전에는 `RuntimeTypeModel.Default.Compile(...)` 로 정적
직렬화 어셈블리를 미리 만들어야 한다 (`ProtoBinarySerializer.cs` 주석 참고).

### 8.3 세이브 시스템

```
ISaveService (Foundation)
  ├ LocalFileSaveService   ← 지금 기본값. Application.persistentDataPath 에 Protobuf 로 저장
  ├ CloudSaveServiceStub   ← 클라우드 SDK 연동 전 자리 표시자 (NotImplementedException)
  └ CompositeSaveService   ← 1차(클라우드) 시도 후 2차(로컬) 폴백
```

게임 코드는 `SaveManager.SaveAsync(slot, data)` / `LoadAsync<T>(slot)` 만 부른다.
클라우드가 준비되면 부트스트랩 코드 한 줄만 바꾸면 된다:

```csharp
SaveManager.Configure(new CompositeSaveService(new CloudSaveServiceStub(), new LocalFileSaveService()));
```

현재 실제로 저장되는 예시는 `PlayerProfileData`(`Assets/Scripts/Save/`) — 이름, 마지막 덱,
해금한 스킬, 플레이 시간. `LoadoutStorage.Save()` 가 호출될 때마다 자동으로 같이 저장된다.

### 8.4 UniTask — 언제 쓰고 언제 안 쓰는가

- **쓰는 곳**: 파일 I/O(`LocalFileSaveService`, 스레드풀로 전환 후 완료되면 메인 스레드로 복귀),
  Addressables 로딩(`AddressableAssetCatalog`) — 둘 다 "진짜" 비동기/스레드 경계가 있는 작업이다.
- **안 쓰는 곳**: 전투/스킬 판정(`SkillCaster`, `EnemyBrain` 등)은 여전히 Unity Coroutine 을 쓴다.
  이건 프레임에 동기화된 게임플레이 시퀀싱(선딜 대기, 콤보 타이밍)이라 스레드 전환이
  필요 없고, 서버의 매 프레임 결정론적 흐름을 그대로 유지하는 게 맞다. UniTask 는
  "스레드 경계를 넘는 곳"에 쓰는 도구지, Coroutine 을 무조건 대체하는 도구가 아니다.

### 8.5 라이프사이클 트래킹

`LifecycleTracker`(Foundation) 는 `ITrackable` 을 구현한 오브젝트의 생성/소멸을 기록한다.
지금은 `Health`, `Projectile` 에 연결되어 있다 (서버 기준).

```csharp
private LifecycleHandle _handle;
void OnNetworkSpawn() => _handle = LifecycleTracker.Register(this); // this : ITrackable
void OnNetworkDespawn() => _handle.Dispose();
```

`ActionRPG > Lifecycle Tracker` 창(플레이 모드 전용)에서 카테고리별 개수, 부모-자식 관계,
개별 오브젝트의 생존 시간을 실시간으로 볼 수 있다. 새 시스템에 붙이고 싶으면
`ITrackable` 을 구현하고 스폰/디스폰 시점에 `Register`/`Dispose` 만 호출하면 된다 —
게임 로직에는 전혀 영향을 주지 않는 순수 관찰용 레이어다.

### 8.6 Addressables — 로컬 커스텀 카탈로그

`CatalogService`(Foundation) 가 게임 코드가 부르는 창구다.

```csharp
var handle = await CatalogService.InstantiateAsync("Enemy_GoblinWarrior", parent);
// ... 다 쓰면
handle.Release();
```

- 기본은 **로컬** 빌드/로드 경로 (원격 CDN 없음) — `ActionRPG > Addressables > 초기 설정` 참고.
- **커스텀 카탈로그** 확장: `CatalogService.LoadAdditionalCatalogAsync(path)` 로 기본 카탈로그
  외에 별도로 빌드해 둔 카탈로그(JSON)를 조건부로 얹을 수 있다 — 예를 들어
  `StreamingAssets/AdditionalCatalogs/season1_catalog.json` 을 시즌 이벤트 시작 시 로드하는 식.
  원격 서버 없이도 "나중에 추가되는 로컬 콘텐츠 묶음" 구조를 지금 만들어 둔 것이다.
- 나중에 진짜 CDN 으로 옮기고 싶으면 Addressables 그룹의 Build/Load Path 만 Remote 로
  바꾸면 되고, `CatalogService` 를 호출하는 게임 코드는 한 줄도 안 바뀐다.

---

## 9. 튜토리얼

`TutorialSequence` 에 `TutorialStep` 들을 순서대로 담는다.
스텝은 ScriptableObject 지만 `TutorialDirector` 가 **복제해서** 돌리므로
진행 상태가 프로젝트 파일에 남지 않는다.

기본 제공 스텝:

| 스텝 | 완료 조건 |
|---|---|
| `DialogueStep` | 대사 시간 경과 |
| `MoveDistanceStep` | 누적 이동 거리 |
| `LookAroundStep` | 누적 카메라 회전 각도 |
| `PerformActionStep` | 공격 / 점프 / 회피 / 무기전환 / 스킬 / 상호작용 N회 |
| `DefeatEnemiesStep` | 적 N마리 처치 (적 소환 기능 포함) |
| `ReachPointStep` | 지정 마커 도달 |
| `WaitStep` | 시간 경과 |

완료 판정은 `GameEventBus` 이벤트를 구독해서 한다.
새 스텝은 `TutorialStep` 을 상속해 `Tick()` 이 `true` 를 반환하게 만들면 된다.

**튜토리얼도 호스트 모드로 돈다.** 혼자 하는 콘텐츠지만 실제 게임과 같은
프리팹 · 네트워크 코드 경로를 쓰므로, 여기서 배운 조작이 멀티에서 그대로 통한다.

---

## 10. 다음에 붙일 것들 (현재 비어 있는 부분)

프레임워크 뼈대는 서 있지만, 아래는 의도적으로 비워 두었다.

- **아트** — 프리팹은 전부 캡슐/박스 프리미티브다. 메시 · 애니메이션 · VFX · 사운드 교체 필요
- **애니메이션** — `CharacterAnimatorDriver` 가 쓰는 파라미터 이름은 `GameConstants.Anim` 에 정의되어 있다.
  Animator Controller 를 만들고 같은 이름의 파라미터를 두면 자동으로 연결된다.
  파라미터가 없으면 조용히 무시하므로 지금도 에러 없이 돌아간다.
- **로비 씬** — `SessionManager` 에 준비 상태(`IsReady`) 기반이 들어 있지만 UI 는 없다.
  현재는 호스트가 방을 만들면 바로 던전으로 들어간다.
- **인터넷 매치메이킹** — 지금은 직접 IP 접속만 된다.
  공개 매치가 필요하면 `com.unity.services.multiplayer` (Relay + Lobby)를 추가하고
  `ConnectionManager` 의 `StartHost` / `StartClient` 만 갈아 끼우면 된다.
- **아이템 · 장비 · 성장** — 장비는 Infinite 게임플레이 이펙트(어트리뷰트 모디파이어)로 붙였다 떼면 된다.
  레벨업은 어빌리티 스펙 레벨(`GameplayAbilitySpec.Level`)과 ScalableFloat 커브로 표현할 수 있다.
- **클라우드 세이브 실제 연동** — `CloudSaveServiceStub` 을 실제 SDK 호출로 채우는 일만 남았다.
- **protobuf-net AOT 사전 컴파일** — IL2CPP 플랫폼에 실제로 배포하기 전에 필요 (8.2 참고).

## 11. 알아 두면 좋은 함정

- 새 네트워크 프리팹(발사체, 소환수, 적)은 **`Resources/ActionRPGNetworkPrefabs` 목록에 등록**해야 한다.
  등록하지 않으면 서버에서만 스폰되고 클라이언트에는 나타나지 않는다.
- `FixedString32Bytes` 는 UTF-8 로 29바이트까지다. 한글 이름은 9글자쯤에서 잘린다
  (`PlayerNetworkIdentity.Truncate` 가 처리 중).
- 서버가 클라이언트 권한 트랜스폼을 직접 옮길 수 없다.
  플레이어를 이동시킬 땐 반드시 `PlayerNetworkIdentity.Teleport()` 를 쓴다 (소유자에게 RPC 를 보낸다).
- `SkillDatabase` 는 `Resources` 폴더에 정확히 하나만 있어야 한다.
- **NetworkManager 는 씬을 넘어가도 살아남고 중복을 스스로 정리하지 않는다.**
  그래서 씬마다 둔 NetworkManager 오브젝트에는 `NetworkBootstrapGuard` 를 붙여 두었다.
  메뉴에서 넘어온 것이 이미 있으면 씬 쪽을 없애고, 없으면 그대로 두어
  Dungeon_01 을 단독으로 재생하는 것도 가능하게 한다 (이때는 호스트를 자동으로 시작한다).
- **`ActionRPG.Runtime` 이 `ActionRPG.Foundation` 을 참조하는 방향은 절대 반대로 만들지 말 것.**
  Foundation 에 게임플레이 타입(SkillLoadout 등)을 직접 두지 않고, `[ProtoMember]` 처럼
  필요한 속성만 게임플레이 쪽 타입에 붙이는 식으로 유지해야 패키지 재사용성이 깨지지 않는다.
- protobuf-net 필드 번호(`[ProtoMember(n)]`)는 한 번 배포한 뒤에는 재사용하거나 삭제하지 말 것 —
  기존 세이브 파일과의 하위 호환이 깨진다.
