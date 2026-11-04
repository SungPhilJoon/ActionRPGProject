using System.Collections;
using Framework;
using Framework.Ability;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// GAS Tool 로 만든 보스 격노 어빌리티 (AbilityTable #603, Type=Graph → GA_BossEnrageBuff → GE #101) 플레이 검증.
/// 실제 테이블 · 그래프 에셋 · 보스 트리를 그대로 쓴다 — 기댓값은 EffectTable/AbilityTable 행에서 읽으므로 수치를 조정해도 테스트를 고칠 필요가 없다.
///  1. 직접 발동: 시전 태그 · 이동 잠금 → 대기 뒤 GE 적용 → 속성 = 행의 모디파이어대로 → 종료 · 쿨다운 → 지속시간 뒤 원래 값
///  2. 보스 AI: 보스(Grimgor) 의 Spell1 을 #603 으로 바꾸고 체력 45% → ST_Boss Phase2 공격 상태가 Spell1 을 골라 격노 사용
/// </summary>
public class BossEnrageTests : GamePlayTestBase
{
    private const int BossGrimgor = 5;
    private const int BossEnrageAbilityId = 603;
    private const int BossEnrageEffectId = 101;

    /// <summary>시전 ~ GE 적용 직후 재생 속도 (0.25 = 4배 느리게).</summary>
    private const float SlowMotionScale = 0.25f;
    /// <summary>어빌리티 종료 뒤 느린 화면을 유지하는 게임 시간 (실제 시간 = 이 값 / SlowMotionScale).</summary>
    private const float SlowMotionHoldSeconds = 1.0f;
    /// <summary>카메라가 정면으로 옮겨 가 자리 잡을 때까지 기다리는 시간.</summary>
    private const float CloseUpSettleSeconds = 1.0f;

    private MonsterCharacter Boss;

    private IEnumerator spawnBossWithEnrage(bool InRunAi)
    {
        yield return SpawnMonster(BossGrimgor, new Vector3(0.0f, 0.0f, 8.0f), m => Boss = m);
        if (InRunAi == false)
        {
            Boss.Brain.enabled = false;
            Boss.Motor.Stop();
        }

        var ability = Boss.GiveAbility(BossEnrageAbilityId);
        Assert.IsNotNull(ability, $"AbilityTable #{BossEnrageAbilityId} 부여 실패");
        Assert.IsInstanceOf<GraphAbility>(ability, "Type=Graph 여야 함");
        Assert.IsNotNull(((GraphAbility)ability).Graph, "그래프 에셋(GA_BossEnrageBuff) 로드 실패");
        Assert.AreSame(ability, Boss.GetSlot(EAbilitySlot.Spell1), "Spell1 슬롯에 들어가야 함");
    }

    /// <summary>
    /// 직접 발동: 시전 → GE #101 → 속성 변화 → 종료 · 쿨다운 → 만료 후 복귀.
    /// 격노 연출을 자세히 보도록 카메라를 보스 정면 가까이 붙이고(HUD 숨김), 시전 ~ GE 적용 직후까지 SlowMotionScale 로 느리게 재생한다.
    /// 결과 확인용 화면: Temp/BossEnrage_0_Idle.png (시전 전), Temp/BossEnrage_1_Cast.png (시전 중), Temp/BossEnrage_2_Enraged.png (격노 적용 뒤).
    /// timeScale 로 늦추므로 애니메이션 · 대기 단계 · GE 큐가 같은 비율로 느려지고 타이밍은 그대로다 (WaitFor 시간 제한도 게임 시간 기준).
    /// </summary>
    [UnityTest]
    public IEnumerator BossEnrage_Activate_AppliesEffectFromTable()
    {
        yield return spawnBossWithEnrage(false);
        Boss.Motor.Face(Player.transform.position - Boss.transform.position, true);
        var effectRow = Tables.Root.Effect.Get(BossEnrageEffectId);
        var abilityRow = Tables.Root.Ability.Get(BossEnrageAbilityId);
        var ability = Boss.GetSlot(EAbilitySlot.Spell1);

        float basePower = Boss.Attributes.Get(EAttribute.PhysicalPower);
        float baseAttackSpeed = Boss.Attributes.Get(EAttribute.AttackSpeed);
        float baseCrit = Boss.Attributes.Get(EAttribute.CritChance);

        var closeUp = new CloseUpCamera();
        closeUp.Begin(Boss);
        try
        {
            yield return WaitSeconds(CloseUpSettleSeconds);
            ScreenCapture.CaptureScreenshot("Temp/BossEnrage_0_Idle.png");
            yield return null;
            Time.timeScale = SlowMotionScale;

            EActivateFailReason reason;
            Assert.IsTrue(Boss.TryActivate(EAbilitySlot.Spell1, Player.transform.position, Player, out reason), $"발동 실패: {reason}");
            Assert.IsTrue(Boss.HasTag((int)ETag.State_Casting), "ActivationOwnedTags(State_Casting) 가 붙어야 함");
            Assert.IsTrue(Boss.Motor.IsMovementLocked, "LockMovement 단계로 이동이 잠겨야 함");
            Assert.IsNull(Boss.Asc.FindEffectByDefId(BossEnrageEffectId), "대기 단계 전에 GE 가 걸리면 안 됨");
            yield return WaitSeconds(0.3f);
            ScreenCapture.CaptureScreenshot("Temp/BossEnrage_1_Cast.png");

            yield return WaitFor(() => Boss.Asc.FindEffectByDefId(BossEnrageEffectId) != null, 2.0f, $"GE #{BossEnrageEffectId} 적용");
            Assert.AreEqual(expected(effectRow, EAttribute.PhysicalPower, basePower), Boss.Attributes.Get(EAttribute.PhysicalPower), 0.01f, "PhysicalPower");
            Assert.AreEqual(expected(effectRow, EAttribute.AttackSpeed, baseAttackSpeed), Boss.Attributes.Get(EAttribute.AttackSpeed), 0.01f, "AttackSpeed");
            Assert.AreEqual(expected(effectRow, EAttribute.CritChance, baseCrit), Boss.Attributes.Get(EAttribute.CritChance), 0.01f, "CritChance");
            foreach (var tag in effectRow.GrantedTags)
            {
                Assert.IsTrue(Boss.HasTag((int)tag), $"GrantedTag {tag}");
            }

            yield return WaitFor(() => ability.IsActive == false, 2.0f, "어빌리티 종료");
            Assert.IsFalse(Boss.HasTag((int)ETag.State_Casting), "종료 뒤 State_Casting 이 남아 있음");
            Assert.IsNotNull(Boss.Asc.FindEffectByDefId(BossEnrageEffectId), "RemoveOnEnd=false 라 종료 뒤에도 격노가 유지되어야 함");
            float cooldown = Boss.Asc.GetCooldownRemaining(BossEnrageAbilityId);
            Assert.Greater(cooldown, 0.0f, "쿨다운이 적용되어야 함");
            Assert.LessOrEqual(cooldown, abilityRow.Cooldown, "쿨다운은 AbilityTable.Cooldown 이하");
            Assert.IsFalse(Boss.TryActivate(EAbilitySlot.Spell1, Player.transform.position, Player, out reason), "쿨다운 중 재발동");
            Assert.AreEqual(EActivateFailReason.Cooldown, reason);

            // 격노 큐(붉은 기운)가 켜진 모습을 느린 화면으로 조금 더 보여 준 뒤 정상 속도로 만료까지.
            yield return WaitSeconds(SlowMotionHoldSeconds * 0.5f);
            ScreenCapture.CaptureScreenshot("Temp/BossEnrage_2_Enraged.png");
            yield return WaitSeconds(SlowMotionHoldSeconds * 0.5f);
            Time.timeScale = TestTimeScale;

            yield return WaitFor(() => Boss.Asc.FindEffectByDefId(BossEnrageEffectId) == null, effectRow.Duration + 2.0f, "격노 만료");
            Assert.AreEqual(basePower, Boss.Attributes.Get(EAttribute.PhysicalPower), 0.01f, "만료 후 PhysicalPower 복귀");
            Assert.AreEqual(baseAttackSpeed, Boss.Attributes.Get(EAttribute.AttackSpeed), 0.01f, "만료 후 AttackSpeed 복귀");
            Assert.AreEqual(baseCrit, Boss.Attributes.Get(EAttribute.CritChance), 0.01f, "만료 후 CritChance 복귀");
        }
        finally
        {
            Time.timeScale = TestTimeScale;
            closeUp.End();
        }
    }

    /// <summary>보스 AI: 체력이 높을 땐 Phase1 (Primary 만) → 45% 에서 Phase2 → Spell1(#603) 사용으로 격노.</summary>
    [UnityTest]
    public IEnumerator BossEnrage_UsedByBossAi_InPhase2()
    {
        Player.AddTag((int)ETag.Cheat_God);
        yield return spawnBossWithEnrage(true);
        int enrageUses = 0;
        System.Action<GameplayAbility> onActivated = a => enrageUses += a.AbilityId == BossEnrageAbilityId ? 1 : 0;
        Boss.Asc.OnAbilityActivated += onActivated;
        try
        {
            yield return WaitFor(() => IsInState(Boss, "Phase1"), 10.0f, "보스 교전 (Phase1)");
            yield return WaitSeconds(2.0f);
            Assert.AreEqual(0, enrageUses, "Phase1 에서는 Spell1 을 쓰지 않아야 함");

            Boss.Attributes.Set(EAttribute.Health, Boss.Attributes.Get(EAttribute.MaxHealth) * 0.45f);
            yield return WaitFor(() => IsInState(Boss, "Phase2"), 5.0f, "체력 45% 에서 Phase2 진입");
            yield return WaitFor(() => enrageUses > 0, 10.0f, $"Phase2 에서 #{BossEnrageAbilityId} 사용");
            yield return WaitFor(() => Boss.Asc.FindEffectByDefId(BossEnrageEffectId) != null, 3.0f, $"GE #{BossEnrageEffectId} 적용");
        }
        finally
        {
            Boss.Asc.OnAbilityActivated -= onActivated;
        }
    }

    /// <summary>행의 모디파이어 3개 중 InAttribute 에 해당하는 것을 AttributeSet 과 같은 규칙으로 집계: (Base + ΣAdd) × (1 + ΣPercent/100).</summary>
    private static float expected(EffectRow InRow, EAttribute InAttribute, float InBase)
    {
        float add = 0.0f;
        float percent = 0.0f;
        accumulate(InRow.Mod1Attr, InRow.Mod1Op, InRow.Mod1Type, InRow.Mod1Value, InAttribute, ref add, ref percent);
        accumulate(InRow.Mod2Attr, InRow.Mod2Op, InRow.Mod2Type, InRow.Mod2Value, InAttribute, ref add, ref percent);
        accumulate(InRow.Mod3Attr, InRow.Mod3Op, InRow.Mod3Type, InRow.Mod3Value, InAttribute, ref add, ref percent);
        return (InBase + add) * (1.0f + percent * 0.01f);
    }

    private static void accumulate(EAttribute InModAttribute, EModifierOp InOp, EMagnitudeType InType, float InValue, EAttribute InAttribute, ref float InOutAdd, ref float InOutPercent)
    {
        if (InModAttribute != InAttribute || InValue == 0.0f)
        {
            return;
        }

        Assert.AreEqual(EMagnitudeType.Scalar, InType, $"{InAttribute}: 이 테스트는 Scalar 모디파이어만 계산한다");
        if (InOp == EModifierOp.Add)
        {
            InOutAdd += InValue;
        }
        else if (InOp == EModifierOp.MultiplyPercent)
        {
            InOutPercent += InValue;
        }
    }
}
