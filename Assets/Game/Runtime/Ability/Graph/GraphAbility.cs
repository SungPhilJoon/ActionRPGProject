using System.Collections.Generic;
using Framework;
using Framework.Ability;
using UnityEngine;

/// <summary>
/// AbilityGraphAsset 을 실행하는 어빌리티 (AbilityTable Type=Graph). 언리얼의 Blueprint GameplayAbility 와 같은 역할:
/// 태그 규칙·비용은 에셋의 Class Defaults 에서, 행동은 Steps 를 위에서부터 실행해 만든다.
///  발동: 비용/쿨다운 커밋 → Steps[0..] 실행 (WaitStep 등이 돌려준 시간만큼 WaitDelayTask 로 대기) → 끝나면 EndAbility
///  종료: RemoveOnEnd 로 건 효과 제거 + 각 단계 정리 (OnAbilityEnd)
/// </summary>
public class GraphAbility : GameAbility
{
    public AbilityGraphAsset Graph { get; private set; }

    private readonly List<int> EndEffects = new List<int>();
    private int StepIndex;

    // ------------------------------------------------------------------ step API

    public float AttackSpeedScale
    {
        get { return SpeedScale; }
    }

    public Vector3 Aim
    {
        get { return AimDirection; }
    }

    public Vector3 AimPoint
    {
        get { return ActivationInfo.AimPoint; }
    }

    /// <summary>발동 시 지정된 대상 (몬스터 AI 는 현재 타깃). 없으면 null.</summary>
    public Actor TargetActor
    {
        get { return Actor.From(ActivationInfo.Target); }
    }

    public void PlayAnimation(int InAnimationId)
    {
        PlayAnim(InAnimationId);
    }

    /// <summary>어빌리티가 끝날 때 제거할 활성 이펙트 핸들.</summary>
    public void TrackEffect(int InHandle)
    {
        if (InHandle != 0)
        {
            EndEffects.Add(InHandle);
        }
    }

    /// <summary>
    /// 테이블/Addressables 를 거치지 않고 그래프를 직접 지정해 만든다 (테스트, 런타임 생성 어빌리티).
    /// 부여는 호출한 쪽이 Actor.GrantAbility 로 한다.
    /// </summary>
    public static GraphAbility Create(AbilityRow InRow, Actor InActor, AbilityGraphAsset InGraph)
    {
        var ability = new GraphAbility();
        ability.Graph = InGraph;
        ability.Setup(InRow, InActor);
        return ability;
    }

    // ------------------------------------------------------------------ lifecycle

    protected override void OnSetup()
    {
        if (Graph == null)
        {
            string address = Tables.Address(Row.Graph);
            Graph = string.IsNullOrEmpty(address) == false ? Managers.Get<AssetLoader>().LoadSync<ScriptableObject>(address) as AbilityGraphAsset : null;
        }

        if (Graph == null)
        {
            Log.Error($"GraphAbility {Row.Index}: graph asset missing (AssetTable {Row.Graph})");
            return;
        }

        // GameAbility.Setup 의 타입별 기본 규칙 대신 에셋의 태그 규칙을 쓴다. 사망/기절/흡혈 중 발동 불가는 유지.
        ActivationOwnedTags.Clear();
        BlockAbilitiesWithTags.Clear();
        CancelAbilitiesWithTags.Clear();
        addTags(ActivationOwnedTags, Graph.ActivationOwnedTags);
        addTags(BlockAbilitiesWithTags, Graph.BlockAbilitiesWithTag);
        addTags(CancelAbilitiesWithTags, Graph.CancelAbilitiesWithTag);
        addTags(ActivationRequiredTags, Graph.ActivationRequiredTags);
        addTags(ActivationBlockedTags, Graph.ActivationBlockedTags);

        if (Graph.BloodCost > 0.0f)
        {
            CostEffect = EffectLibrary.Get(EffectLibrary.CostEffectId);
            CostAttribute = (int)EAttribute.BloodPool;
            CostAmount = Graph.BloodCost;
            SetByCallerCostKey = (int)ESetByCaller.Cost;
        }
    }

    protected override void OnActivate(AbilityActivationInfo InInfo)
    {
        if (Graph == null || Commit() == false)
        {
            EndAbility(true);
            return;
        }

        StepIndex = 0;
        runSteps();
    }

    protected override void OnEnd(bool InWasCancelled)
    {
        for (int i = 0; i < EndEffects.Count; ++i)
        {
            Actor.Asc.RemoveActiveEffect(EndEffects[i]);
        }

        EndEffects.Clear();
        if (Graph == null)
        {
            return;
        }

        for (int i = 0; i < Graph.Steps.Count; ++i)
        {
            if (Graph.Steps[i] != null)
            {
                Graph.Steps[i].OnAbilityEnd(this, InWasCancelled);
            }
        }
    }

    /// <summary>대기가 필요한 단계를 만날 때까지 연속 실행한다. 모든 단계가 끝나면 EndAbility.</summary>
    private void runSteps()
    {
        while (IsActive == true && StepIndex < Graph.Steps.Count)
        {
            var step = Graph.Steps[StepIndex];
            StepIndex += 1;
            if (step == null)
            {
                continue;
            }

            float wait = step.Execute(this);
            if (wait < 0.0f)
            {
                return;
            }

            if (wait > 0.0f)
            {
                Delay(wait, runSteps);
                return;
            }
        }

        if (IsActive == true)
        {
            EndAbility(false);
        }
    }

    private static void addTags(GameplayTagContainer InContainer, ETag[] InTags)
    {
        if (InTags == null)
        {
            return;
        }

        for (int i = 0; i < InTags.Length; ++i)
        {
            if (InTags[i] != ETag.None)
            {
                InContainer.Add((int)InTags[i]);
            }
        }
    }
}
