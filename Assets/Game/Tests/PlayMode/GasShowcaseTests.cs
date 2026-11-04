using System.Collections;
using System.Collections.Generic;
using System.Text;
using Framework;
using Framework.Ability;
using Framework.StateTree;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// OBS 녹화용 시연 테스트 — "몬스터 한 마리를 GAS 로 만드는 과정" 을 화면 자막과 함께 순서대로 보여 주고, 각 단계 결과를 검증한다.
///  1 AttributeSet (MonsterAuthoring 인스펙터 능력치) → 2 GameplayEffect (GAS Tool GE 탭) → 3·4 Ability (GAS Tool Ability 탭)
///  → 5 Ability Set 부여 → 6 StateTree (StateTree Tool) → 7 전투 → 8 체력 50% 이하 분노 → 9 같은 스킬 피해 증가 확인 → 10 지속시간 끝나면 원래대로.
/// 툴이 만드는 것과 같은 데이터(GE 정의 · 그래프 에셋 · AbilityTable 행 · 트리)를 코드로 만들어서, 테이블 · 에셋을 건드리지 않는다.
/// [Explicit]: Run All 에는 빠지고 Test Runner 에서 이 테스트를 골라 Run Selected 로 실행한다 (약 60초, Pace = 1).
/// </summary>
[Explicit("OBS 시연용 — Test Runner 에서 직접 골라 실행")]
[Category("Showcase")]
public class GasShowcaseTests : GamePlayTestBase
{
    /// <summary>자막 한 장면을 읽을 시간 배율. 녹화 템포에 맞춰 조절한다 (1 = 기본).</summary>
    private const float Pace = 1.0f;

    private const int EnrageEffectId = 9901;
    private const int SmashAbilityId = 9201;
    private const int EnrageAbilityId = 9202;
    private const int EmpowerCueId = 10;
    private const int MonsterAttackAnimationId = 15;
    private const int BaseCharacterId = 2;

    private const float ShowcaseMaxHealth = 600.0f;
    private const float ShowcasePower = 30.0f;
    private const float EnragePowerPercent = 50.0f;
    private const float EnrageSpeedPercent = 30.0f;
    private const float EnrageDuration = 7.0f;
    private const float SmashCoefficient = 100.0f;

    private MonsterCharacter Monster;
    private ShowcaseCaption Caption;
    private readonly List<float> SmashDamage = new List<float>();
    private readonly List<bool> SmashEnraged = new List<bool>();

    [TearDown]
    public void CleanUpShowcase()
    {
        EffectLibrary.Unregister(EnrageEffectId);
        if (Caption != null)
        {
            Object.Destroy(Caption.gameObject);
        }
    }

    [UnityTest]
    public IEnumerator BuildMonsterWithGas_Showcase()
    {
        Caption = ShowcaseCaption.Create(liveText);
        EventBus<DamageEvent>.Subscribe(onDamage);
        try
        {
            yield return intro();
            yield return stepAttributes();
            yield return stepEffect();
            yield return stepAbilities();
            yield return stepStateTree();
            yield return stepCombat();
            yield return stepEnrage();
            yield return stepEnrageEnds();
        }
        finally
        {
            EventBus<DamageEvent>.Unsubscribe(onDamage);
        }
    }

    // ------------------------------------------------------------------ steps

    private IEnumerator intro()
    {
        Caption.Show("GAS 시연", "몬스터 한 마리 만들기",
            "Ability = 스킬 1개 (무엇을 · 언제)",
            "GameplayEffect = 스킬이 거는 효과 (수치를 어떻게 바꾸나)",
            "AttributeSet = 캐릭터의 능력치 (체력 · 공격력 · 이동속도)",
            "StateTree = 언제 어떤 스킬을 쓸지 정하는 AI");
        yield return hold(5.0f);
    }

    /// <summary>1. AttributeSet — 몸체(외형 · 애니메이션)는 기존 몬스터, 능력치만 새로.</summary>
    private IEnumerator stepAttributes()
    {
        Caption.Show("1단계 · AttributeSet", "능력치 정하기",
            "툴: Data/Monsters/*.asset 선택 → MonsterAuthoring 인스펙터 → 능력치",
            $"MaxHealth {ShowcaseMaxHealth:0} · PhysicalPower {ShowcasePower:0} · CritChance 0 (시연 수치 고정)");
        yield return SpawnMonster(BaseCharacterId, new Vector3(0.0f, 0.0f, 1.8f), m => Monster = m);
        Monster.Brain.enabled = false;
        Monster.Motor.Stop();
        Monster.Motor.Face(Player.transform.position - Monster.transform.position, true);

        Monster.Attributes.Set(EAttribute.MaxHealth, ShowcaseMaxHealth);
        Monster.Attributes.Set(EAttribute.Health, ShowcaseMaxHealth);
        Monster.Attributes.Set(EAttribute.PhysicalPower, ShowcasePower);
        Monster.Attributes.Set(EAttribute.CritChance, 0.0f);
        Assert.AreEqual(ShowcasePower, Monster.Attributes.Get(EAttribute.PhysicalPower), 0.01f);
        Caption.Add("→ 오른쪽 패널: AttributeSet 의 Current 값 (Base + 이펙트 모디파이어)");
        yield return hold(4.5f);
    }

    /// <summary>2. GameplayEffect — 분노: 7초 동안 공격력 +50%, 이동속도 +30%, Empower 태그 · 큐.</summary>
    private IEnumerator stepEffect()
    {
        Caption.Show("2단계 · GameplayEffect", "효과 정의: 분노 (Enrage)",
            "툴: GAS Tool → GameplayEffect 탭 → + 새 GE (EffectTable 행)",
            $"Duration Policy = HasDuration {EnrageDuration:0}초",
            $"Modifier 1: PhysicalPower  MultiplyPercent +{EnragePowerPercent:0}%",
            $"Modifier 2: MoveSpeed  MultiplyPercent +{EnrageSpeedPercent:0}%",
            "Granted Tag: State.Buff.Empower · Cue: 붉은 기운");

        var def = new GameplayEffectDef();
        def.Id = EnrageEffectId;
        def.DurationPolicy = EDurationPolicy.HasDuration;
        def.Duration = EnrageDuration;
        def.StackPolicy = EStackPolicy.Refresh;
        def.CueId = EmpowerCueId;
        def.GrantedTags = TagUtil.ToContainer(ETag.State_Buff_Empower);
        def.ApplicationBlockedTags = TagUtil.ToContainer(ETag.State_Dead);
        def.Modifiers = new[]
        {
            percentModifier(EAttribute.PhysicalPower, EnragePowerPercent),
            percentModifier(EAttribute.MoveSpeed, EnrageSpeedPercent),
        };
        EffectLibrary.Register(def);
        Assert.AreSame(def, EffectLibrary.Get(EnrageEffectId));
        Caption.Add("→ 수치(+50%)는 이펙트가 가진다. 능력치 원본(Base)은 바뀌지 않는다");
        yield return hold(5.0f);
    }

    /// <summary>3·4. Ability 2개 + 5. Ability Set (슬롯에 부여).</summary>
    private IEnumerator stepAbilities()
    {
        Caption.Show("3단계 · Ability", "스킬 1: 강타 (근접 공격)",
            "툴: GAS Tool → Ability 탭 → + 새 Ability → 템플릿 '근접 공격'",
            "단계: 회전 → 애니메이션 → 대기 0.35초 → 부채꼴 피해 → 대기 0.5초",
            $"피해 계수 {SmashCoefficient:0}% × PhysicalPower  → SetByCaller 로 피해 GE(#1)에 실어 보냄",
            "슬롯 Primary · 쿨다운 1.2초 · AI 사용 거리 2.6m");
        var smashGraph = MakeGraph(
            new FaceAimStep(),
            new PlayAnimationStep { AnimationId = MonsterAttackAnimationId },
            new WaitStep { Seconds = 0.35f },
            new MeleeHitStep { Range = 2.6f, Angle = 120.0f, Damage = SmashCoefficient, HitCue = 2 },
            new WaitStep { Seconds = 0.5f });
        smashGraph.ActivationOwnedTags = new[] { ETag.State_Attacking };
        var smash = GraphAbility.Create(MakeGraphRow(SmashAbilityId, EAbilitySlot.Primary, 1.2f, 2.6f), Monster, smashGraph);
        yield return hold(5.5f);

        Caption.Show("4단계 · Ability", "스킬 2: 분노의 포효 (자기 강화)",
            "툴: 템플릿 '자기 강화' → 단계에 '자신에게 GE #9901' (종료 시 제거 끔)",
            "단계: 자신에게 GE(분노) → 대기 0.6초",
            "슬롯 Spell1 · 쿨다운 30초 · 발동 중 다른 공격 차단(Block: Ability)");
        var enrageGraph = MakeGraph(
            new ApplyEffectToSelfStep { EffectId = EnrageEffectId, RemoveOnEnd = false },
            new WaitStep { Seconds = 0.6f, ScaleWithAttackSpeed = false });
        enrageGraph.BlockAbilitiesWithTag = new[] { ETag.Ability };
        var enrage = GraphAbility.Create(MakeGraphRow(EnrageAbilityId, EAbilitySlot.Spell1, 30.0f, 30.0f), Monster, enrageGraph);
        yield return hold(5.0f);

        Caption.Show("5단계 · Ability Set", "몬스터에게 스킬 부여",
            "툴: GAS Tool → Ability Set 탭 (CharacterTable.Abilities, 스폰 시 순서대로 부여)",
            "코드: actor.GiveAbility(ability) → 슬롯에 넣어야 AI 의 UseAbility 가 찾는다");
        Monster.GiveAbility(smash);
        Monster.GiveAbility(enrage);
        Assert.AreSame(smash, Monster.GetSlot(EAbilitySlot.Primary));
        Assert.AreSame(enrage, Monster.GetSlot(EAbilitySlot.Spell1));
        Caption.Add("→ Primary = 강타, Spell1 = 분노의 포효");
        yield return hold(4.0f);
    }

    /// <summary>6. StateTree — Combat[타깃] → 분노[체력 ≤ 50% · 준비됨] / 공격[사거리 · 준비됨] / 추적 / 대기.</summary>
    private IEnumerator stepStateTree()
    {
        Caption.Show("6단계 · StateTree", "AI: 언제 어떤 스킬을 쓸까",
            "툴: StateTree Tool → New ▾ 근접 몬스터 → + 블록 '조건부 어빌리티 사용 (Spell1)'",
            "Combat (타깃 있음)",
            "   ├ Enrage : 체력 ≤ 50% 이고 Spell1 준비 → UseAbility(Spell1)",
            "   ├ Attack : 사거리 안 · Primary 준비 → UseAbility(Primary)",
            "   ├ Chase  : 타깃 추적",
            "   └ Hold   : 타깃 바라보며 대기");
        Monster.Brain.RunTree(buildTree());
        Monster.Brain.enabled = true;
        yield return WaitFor(() => IsInState(Monster, "Combat"), 5.0f, "Combat 상태 진입");
        yield return hold(4.0f);
    }

    /// <summary>7. 전투 — 강타 피해 = 계수 100% × 공격력 30 (+ 레벨 · 저항 보정).</summary>
    private IEnumerator stepCombat()
    {
        Caption.Show("7단계 · 실행", "전투: 강타로 공격",
            "Ability 발동 → CommitAbility(쿨다운 GE) → 단계 실행 → 피해 GE → DamageExecution",
            "DamageExecution: 계수% × 공격자 PhysicalPower(스냅샷) × 레벨 보정 × (1 − 저항)");
        yield return WaitFor(() => SmashDamage.Count >= 2, 10.0f, "강타 2회 적중");
        Caption.Add($"→ 강타 피해: {SmashDamage[0]:0.#}, {SmashDamage[1]:0.#}");
        yield return hold(3.5f);
    }

    /// <summary>8·9. 체력 50% 이하 → 분노 → 같은 강타의 피해가 공격력 증가만큼 커진다.</summary>
    private IEnumerator stepEnrage()
    {
        Caption.Show("8단계 · 조건", "체력을 40% 로 깎으면…",
            "StateTree 의 Enrage 상태 조건(체력 ≤ 50%)이 참 → 분노의 포효 발동",
            "→ GE 적용: PhysicalPower 와 MoveSpeed 의 Current 값이 오른다 (오른쪽 패널)");
        float normalDamage = SmashDamage[SmashDamage.Count - 1];
        Monster.Attributes.Set(EAttribute.Health, ShowcaseMaxHealth * 0.4f);
        yield return WaitFor(() => Monster.HasTag((int)ETag.State_Buff_Empower) == true, 6.0f, "분노 GE 적용");

        float expectedPower = ShowcasePower * (1.0f + EnragePowerPercent * 0.01f);
        Assert.AreEqual(expectedPower, Monster.Attributes.Get(EAttribute.PhysicalPower), 0.01f, "분노 중 공격력 = 30 × 1.5");
        Assert.IsTrue(IsInState(Monster, "Enrage") == true || Monster.Asc.GetCooldownRemaining(EnrageAbilityId) > 0.0f, "Enrage 상태로 스킬을 써야 함");
        Caption.Add($"→ PhysicalPower {ShowcasePower:0} → {Monster.Attributes.Get(EAttribute.PhysicalPower):0}");
        yield return hold(3.0f);

        Caption.Show("9단계 · 결과", "같은 스킬, 더 큰 피해",
            "강타(Ability)는 바뀌지 않았다 — 계수 100% 그대로",
            "바뀐 것은 AttributeSet 의 공격력 (분노 GE 의 모디파이어)");
        int before = SmashDamage.Count;
        yield return WaitFor(() => SmashDamage.Count > before && SmashEnraged[SmashDamage.Count - 1] == true, 8.0f, "분노 중 강타 적중");
        float enragedDamage = SmashDamage[SmashDamage.Count - 1];
        Caption.Add($"→ 강타 피해: {normalDamage:0.#}  →  {enragedDamage:0.#}  (×{enragedDamage / Mathf.Max(normalDamage, 0.01f):0.00})");
        Assert.Greater(enragedDamage, normalDamage * 1.3f, "분노 중 강타 피해가 약 1.5배여야 함");
        yield return hold(4.5f);
    }

    /// <summary>10. 지속시간이 끝나면 모디파이어가 빠지고 Base 값으로 돌아온다.</summary>
    private IEnumerator stepEnrageEnds()
    {
        Caption.Show("10단계 · 만료", "지속시간이 끝나면 원래대로",
            "HasDuration GE 만료 → 모디파이어 제거 → Current = Base 로 재계산 · 태그 · 큐 제거");
        yield return WaitFor(() => Monster.HasTag((int)ETag.State_Buff_Empower) == false, EnrageDuration + 2.0f, "분노 만료");
        Assert.AreEqual(ShowcasePower, Monster.Attributes.Get(EAttribute.PhysicalPower), 0.01f, "만료 후 공격력 복귀");
        Caption.Add($"→ PhysicalPower {Monster.Attributes.Get(EAttribute.PhysicalPower):0} (Base)");
        yield return hold(3.0f);

        Caption.Show("정리", "몬스터 = 능력치 + 스킬 + 효과 + AI",
            "AttributeSet : 능력치 (MonsterAuthoring)",
            "Ability      : 스킬 1개 = 그래프 에셋 + AbilityTable 행 (GAS Tool)",
            "GameplayEffect: 수치 변화 · 상태 (GAS Tool → EffectTable)",
            "StateTree    : 언제 어떤 슬롯을 쓸지 (StateTree Tool)");
        yield return hold(6.0f);
    }

    // ------------------------------------------------------------------ helpers

    private void onDamage(DamageEvent InEvent)
    {
        if (InEvent.Source == Monster && InEvent.Target == Player && InEvent.AbilityId == SmashAbilityId)
        {
            SmashDamage.Add(InEvent.Amount + InEvent.Absorbed);
            SmashEnraged.Add(Monster.HasTag((int)ETag.State_Buff_Empower));
        }
    }

    private IEnumerator hold(float InSeconds)
    {
        yield return WaitSeconds(InSeconds * Pace);
    }

    private static ModifierDef percentModifier(EAttribute InAttribute, float InPercent)
    {
        var modifier = new ModifierDef();
        modifier.Attribute = (int)InAttribute;
        modifier.Op = EModifierOp.MultiplyPercent;
        modifier.MagnitudeType = EMagnitudeType.Scalar;
        modifier.Value = InPercent;
        return modifier;
    }

    private string liveText()
    {
        if (Monster == null)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        text.AppendLine("<b>몬스터 AttributeSet</b>");
        text.AppendLine($"Health        {Monster.Attributes.Get(EAttribute.Health):0} / {Monster.Attributes.Get(EAttribute.MaxHealth):0}");
        text.AppendLine($"PhysicalPower {Monster.Attributes.Get(EAttribute.PhysicalPower):0.#}");
        text.AppendLine($"MoveSpeed     {Monster.Attributes.Get(EAttribute.MoveSpeed):0.##}");
        text.AppendLine();
        text.AppendLine("<b>태그 · 상태</b>");
        bool enraged = Monster.HasTag((int)ETag.State_Buff_Empower);
        text.AppendLine(enraged == true ? "<color=#FF6060>State.Buff.Empower (분노)</color>" : "-");
        var tree = Monster.Brain != null ? Monster.Brain.Tree : null;
        text.AppendLine($"StateTree: {currentLeaf(tree)}");
        text.AppendLine($"분노 쿨다운: {Monster.Asc.GetCooldownRemaining(EnrageAbilityId):0.0}s");
        return text.ToString();
    }

    private static string currentLeaf(StateTreeInstance InTree)
    {
        if (InTree == null || InTree.Path.Count == 0)
        {
            return "-";
        }

        var state = InTree.Asset.Find(InTree.Path[InTree.Path.Count - 1]);
        return state != null ? state.Name : "-";
    }

    private static StateTreeAsset buildTree()
    {
        var tree = ScriptableObject.CreateInstance<StateTreeAsset>();
        tree.Evaluators.Add(new MonsterPerceptionEvaluator());
        var root = tree.AddState("Root", 0);
        var combat = tree.AddState("Combat", root.Id);
        combat.EnterConditions.Add(new HasTargetCondition());

        var enrage = tree.AddState("Enrage", combat.Id);
        enrage.EnterConditions.Add(new BlackboardCompareCondition { Key = EBBKey.HealthRatio, Op = ECompareOp.LessOrEqual, Value = 0.5f });
        enrage.EnterConditions.Add(new AbilityReadyCondition { Slots = new[] { EAbilitySlot.Spell1 } });
        enrage.Tasks.Add(new UseAbilityTask { Slots = new[] { EAbilitySlot.Spell1 } });
        Goto(enrage, combat.Id);

        var attack = tree.AddState("Attack", combat.Id);
        attack.EnterConditions.Add(new InAttackRangeCondition());
        attack.EnterConditions.Add(new AbilityReadyCondition { Slots = new[] { EAbilitySlot.Primary } });
        attack.Tasks.Add(new UseAbilityTask { Slots = new[] { EAbilitySlot.Primary } });
        Goto(attack, combat.Id);

        var chase = tree.AddState("Chase", combat.Id);
        chase.EnterConditions.Add(new InAttackRangeCondition { Invert = true });
        chase.Tasks.Add(new ChaseTargetTask());
        Goto(chase, combat.Id);

        var hold = tree.AddState("Hold", combat.Id);
        hold.Tasks.Add(new FaceTargetTask());
        hold.Tasks.Add(new WaitTask { Duration = 0.2f });
        Goto(hold, combat.Id);

        var idle = tree.AddState("Idle", root.Id);
        idle.Tasks.Add(new WaitTask { Duration = 0.5f });
        idle.Transitions.Add(new StateTreeTransition { Trigger = ETransitionTrigger.OnStateCompleted, Type = ETransitionType.Root });
        return tree;
    }
}
