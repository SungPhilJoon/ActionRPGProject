# GAS (Gameplay Ability System) 적용 내역

언리얼 GAS 의 개념을 Unity C# 으로 옮겨 `Assets/Framework/Runtime/Ability` 에 두었고, 게임 쪽 구현은 `Assets/Game/Runtime/Ability` 에 있습니다.
Framework 쪽은 게임 데이터를 전혀 모르므로 다른 프로젝트에 그대로 가져갈 수 있습니다.

## 1. 대응표

| 언리얼 | 이 프로젝트 | 파일 |
|---|---|---|
| UAbilitySystemComponent | `AbilitySystemComponent` (MonoBehaviour) | Framework/Ability/AbilitySystemComponent.cs |
| FGameplayTag | `ETag` (int id, 상한은 등록된 태그 수에 맞춰 늘어남) | GameEnums.cs |
| UGameplayTagsManager (계층 A.B.C) | `GameplayTagManager` (TagTable.csv 의 Parent) | GameplayTagManager.cs |
| FGameplayTagContainer | `GameplayTagContainer` — 명시 태그 비트 + 부모 포함 비트(캐시) | GameplayTagContainer.cs |
| FGameplayTagCountContainer | `GameplayTagCountContainer` — 명시/부모 포함 카운트를 따로 관리 (소유 태그, 차단 태그) | GameplayTagCountContainer.cs |
| UAttributeSet | `AttributeSet` (int 인덱스 float 배열, Base/Current) → `GameAttributeSet` | AttributeSet.cs, GameAttributeSet.cs |
| UGameplayEffect | `GameplayEffectDef` ← EffectTable.csv 행 | GameplayEffect.cs, EffectLibrary.cs |
| FGameplayEffectSpec | `GameplayEffectSpec` (시전자, 어빌리티 ID, SetByCaller, 시전자 속성 스냅샷) | GameplayEffect.cs |
| FActiveGameplayEffect | `ActiveGameplayEffect` (남은 시간, 주기 타이머, 스택) | GameplayEffect.cs |
| UGameplayEffectExecutionCalculation | `IGameplayEffectExecution` + `ExecutionRegistry` | GameplayEffectExecution.cs, GameExecutions.cs |
| SetByCaller (Tag) | `ESetByCaller` (int 키) | GameEnums.cs |
| UGameplayAbility | `GameplayAbility` → `GameAbility` → Melee/Projectile/Dash/AreaBurst/SelfBuff/Feed/Leap | GameplayAbility.cs, GameAbility.cs, GameAbilities.cs |
| UAbilityTask | `AbilityTask`, `WaitDelayTask`, `WaitDurationTask` | AbilityTask.cs |
| GameplayCue | `GameplayCueManager` + `IGameplayCueHandler` → `CueSpawner`(CueTable) | GameplayCue.cs, CueSpawner.cs |
| SendGameplayEventToActor | `ASC.SendGameplayEvent(int tag, payload)` | AbilitySystemComponent.cs |
| Meta Attribute (IncomingDamage) | `EAttribute.IncomingDamage / IncomingHeal` | GameAttributeSet.cs |

## 2. 이펙트 적용 흐름

```
GameplayEffectSpec 생성 (EffectLibrary.MakeDamageSpec 등)
   │  SetByCaller(Coefficient, DamageType), 시전자 속성 스냅샷
   ▼
ASC.ApplyGameplayEffectSpecToSelf
   ├ ApplicationRequiredTags / BlockedTags 검사 (예: State_Dead 면 피해 무시)
   ├ RemoveEffectsWithTags
   ├ Instant  → executeEffect → Base 변경 → PostGameplayEffectExecute → Cue Execute
   └ Duration/Infinite
        ├ 스택 정책(None/Refresh/Stack)
        ├ 주기(Period) 없으면 모디파이어를 Current 집계에 추가  (Base + ΣAdd) × (1 + ΣPercent/100), Override 우선
        ├ 주기 있으면 Period 마다 executeEffect (햇빛 화상, 혈액 고갈)
        ├ GrantedTags 추가 (참조 카운트)
        └ Cue OnActive / 제거 시 OnRemove (보호막 거품, 기절 별, 햇빛 연기)
```

## 3. 피해 공식 (DamageExecution)

```
기본    = Flat(SetByCaller.Damage) + Power × Coefficient%      Power = 물리/주문 공격력 (스펙 생성 시점 스냅샷)
레벨    × clamp(1 + (공격자 Level − 대상 Level) × 0.04, 0.5, 1.5)   ← V Rising 기어 레벨 보정 (ConstTable 11~13)
치명    CritChance% 확률로 × (1.5 + CritDamage%)
저항    × (1 − Resist%)
→ IncomingDamage → GameAttributeSet: 무적/God 검사 → Shield 먼저 흡수 → Health 감소 → CombatLog 기록 → DamageEvent 발행 → 흡혈(LifeLeech) → 사망 판정
```

햇빛: `MaxHealth × 6%/s × Period × 램프(노출 0→5초 동안 0→100%) × (1 − SunResist%)`, 저항/레벨 무시.

## 4. 어빌리티 흐름

```
PlayerController / MonsterBrain → Actor.TryActivate(slot, aimPoint)
  └ 이미 활성: OnInputPressedWhileActive (콤보용)
  └ ASC.TryActivateAbility → CanActivate
        ActivationBlockedTags(Dead, Stun, Feeding, 주문은 Silence), BlockAbilitiesWithTags(시전 중 다른 공격 차단),
        쿨다운(쿨다운 이펙트의 AbilityId 로 조회), 비용
     → InternalActivate: ActivationOwnedTags 추가(State_Attacking/Dashing), Cancel 태그 처리(이동기는 시전 캔슬)
     → OnActivate: CommitAbility(쿨다운/비용 이펙트 적용) → AbilityTask 로 타이밍 진행 → CombatUtil 로 히트 → EndAbility
```

어빌리티는 **데이터 + 종류(EAbilityType)** 로 정의됩니다. 새 스킬은 대부분 AbilityTable/EffectTable 행 추가만으로 만들 수 있고,
새로운 "동작 방식"이 필요할 때만 `GameAbility` 하위 클래스를 추가하고 `AbilityFactory` 에 등록합니다.

## 5. 언리얼과 다르게 단순화한 부분 (의도적)

| 항목 | 차이 | 이유 |
|---|---|---|
| 쿨다운 | 어빌리티마다 Cooldown 태그를 만들지 않고, 공용 쿨다운 이펙트 1개 + Spec.AbilityId 로 구분 | 처음엔 태그를 128개로 제한했기 때문 (지금은 상한 없음) |
| 태그 | 문자열 계층 태그 대신 int enum + CSV 부모. **비교 의미는 언리얼과 같고 저장/비교만 비트셋** | 문자열 비교 금지 규칙, 비교는 64태그당 AND 1번 |
| 보호막 | Shield 는 Base 속성, `State_Buff_Shield` 태그가 사라지면 0 으로 소멸 | 지속 모디파이어로 두면 흡수 시 Base 가 음수가 되는 문제 (Issues.md) |
| 예측/복제 | 없음 (오프라인 권한) | 멀티플레이 단계에서 추가 — Multiplayer.md |
| 어빌리티 인스턴싱 | InstancedPerActor 만 지원 | 데모 규모 |
| Modifier Magnitude | Scalar / SetByCaller / SourceAttribute / TargetAttribute 4종 | MMC(커스텀 계산)는 Execution 으로 대체 |

## 6. 확장 방법 예시

- **새 상태이상 (예: 침묵 2초)**: EffectTable 에 `HasDuration, 2, GrantedTags=State_Debuff_Silence` 행 추가 → 어떤 어빌리티의 HitEffects 에 그 Index 추가. 끝.
- **새 혈액 버프**: EffectTable 에 Infinite 모디파이어 행 추가 → BloodTypeTable.TierEffects 에 연결.
- **새 피해 공식**: `IGameplayEffectExecution` 구현 → `EExecution` 에 값 추가 → `GameExecutions.RegisterAll` 에 등록 → EffectTable.Execution 에 이름 기입.


## 7. 태그 비교 규칙 (언리얼과 같은 의미, 2026-10-14 개편)

- **명시 태그**: 실제로 부여/지정된 태그. **부모 캐시**: 명시 태그의 조상(자동 계산).
- "가진 쪽" 은 명시 + 부모로, "질의 쪽" 은 명시 태그만으로 비교한다. 질의 쪽은 절대 부모로 펼치지 않는다.

| 상황 | 결과 |
|---|---|
| `State_Debuff_Stun` 소유 → `HasTag(State_Debuff)` | true (자식은 부모 질의에 매칭) |
| `State_Debuff` 소유 → `HasTag(State_Debuff_Stun)` | false (부모는 자식 질의에 매칭 안 됨) |
| `State_Debuff_Stun` 소유 → `HasAny({State_Buff})` | false (공통 부모 `State` 가 있어도 형제는 불일치) |
| `HasTagExact(State_Debuff)` (Stun 만 소유) | false (명시만 비교) |
| `Ability_Weapon` 차단 → 주문 어빌리티 | 허용 (언리얼 `AbilityTags.HasAny(차단 명시 태그)`) |
| `Ability` 차단 → 모든 어빌리티 | 차단 |

구현 (`Framework/Runtime/Ability`):
- `HasAny(Q) = (Matched & Q.Explicit) != 0`, `HasAll(Q) = (Matched & Q.Explicit) == Q.Explicit` — 64태그당 ulong 1개, 할당 없음.
- 정의용 `GameplayTagContainer` 의 부모 비트는 태그 추가나 계층 변경(`GameplayTagManager.Version`) 때만 다시 계산.
- 소유용 `GameplayTagCountContainer` 는 명시 카운트와 부모 포함 카운트를 따로 두어, 여러 이펙트가 같은 태그를 줘도 마지막이 빠질 때까지 유지.
- 언리얼과의 차이: 컨테이너 크기가 "가진 태그 수" 가 아니라 "전체 태그 수 / 64" 워드. 태그가 수천 개가 되면 메모리·비교 비용이 커지므로 그때 다시 검토.

## 8. V Rising 상태이상 / 무적

수치는 V Rising 커뮤니티 정리 자료(젬 효과 표·가이드)를 기준으로 했다. 패치마다 바뀌므로 **ConstTable / EffectTable 에서 조정**한다.

| 상태 | 태그 (계층) | 효과 | 주는 스킬 |
|---|---|---|---|
| Leech (5초) | State.Debuff.Leech | 대상이 **물리 피해**를 받으면 공격자가 피해의 7% 회복 (`LeechHealPercent`) | Veil of Blood(대시 관통), Blood Rite, Crimson Storm |
| Ignite (5초) | State.Debuff.Ignite | 1초마다 주문력 10% 피해 (EffectTable `Coefficient`) | Chaos Volley |
| Chill (5초) | State.Debuff.Chill | 이동속도 −15% | Ice Nova, 메두사 근접 |
| Freeze (4초) | State.Debuff.**Stun**.Frozen | 이동·행동 불가 (Stun 의 자식이라 기절 규칙을 그대로 공유), **체력이 깎이면 해제** | Ice Nova 가 Chill 소모 |
| Static (5초) | State.Debuff.Static | 물리 피해 +10% (`StaticBonusPercent`) | Ball Lightning, 보스 도약 |
| Condemn (5초) | State.Debuff.Condemn | 받는 피해 +15% | Corrupted Skull, 트롤 |
| Weaken (5초) | State.Debuff.Weaken | 주는 피해 −15% | Wraith Spear, 코브라 |
| Immaterial | State.Immaterial | **지정형 공격(근접 부채꼴·발사체·대시 관통)이 통과**. 범위(원형) 공격과 햇빛은 맞는다 | Veil 대시 중(0.35초), Blood Rite(1.2초) |

**Consume (소모)** — AbilityTable `ConsumeTag` / `ConsumeEffects`: 맞은 대상이 그 상태면 상태를 지우고 `HitEffects` 대신 `ConsumeEffects` 를 건다.
- Ice Nova: Chill → Freeze / Ball Lightning: Static → 기절 1초 / Chaos Volley: Ignite → 폭발(주문력 60%) / 보스 투석: Static → 기절
- `ConsumeMinAgeSeconds`(0.3초) 이상 지난 상태만 소모 — 같은 시전의 2발째가 1발째가 방금 건 상태를 바로 터뜨리지 않게 (Issues 31).

**StartEffects** — 커밋 직후 자신에게 거는 이펙트 (Immaterial 등). `SelfEffects` 는 동작이 끝날 때.

**피해 배율 처리 위치**: `GameAttributeSet.applyStatusModifiers` (Condemn/Static/Weaken), Leech 회복·Freeze 해제는 `handleDamage`. 햇빛·고정 피해는 상태이상 영향 없음.

**정확도에 대한 솔직한 메모**
- Freeze 의 "보호막 부여"(자료에 언급) 는 구현하지 않았다. 체력 감소 시 해제만 구현.
- V Rising 의 Elude(Veil 후 어그로 해제), Condemn 처치 시 스켈레톤 소환, Weaken 처치 시 Phantasm 스택은 미구현.
- Immaterial 이 범위 공격을 막지 않는다는 점은 커뮤니티 보고 기준이다.

**주문 교체**: 기본 Spell1 = Chaos Volley(301), Spell2 = Blood Rite(302). 치트 `spell 303~306` 으로 Ice Nova / Ball Lightning / Corrupted Skull / Wraith Spear 장착. `status <effectId>` 로 주변 적에게 상태이상 테스트.
