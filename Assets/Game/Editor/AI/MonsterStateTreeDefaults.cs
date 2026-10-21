using Framework.Editor;
using Framework.StateTree;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 기본 몬스터 StateTree 3종을 생성한다 (Content/AI → Addressables 주소 AI/ST_*).
/// 이후 수정은 StateTree Editor 툴에서 한다. 다시 실행하면 덮어쓰므로 툴에서 고친 내용이 사라진다는 점에 주의.
/// </summary>
public static class MonsterStateTreeDefaults
{
    private static readonly Color ReturnColor = new Color(0.25f, 0.35f, 0.55f);
    private static readonly Color CombatColor = new Color(0.55f, 0.2f, 0.2f);
    private static readonly Color IdleColor = new Color(0.25f, 0.45f, 0.3f);
    private static readonly Color PhaseColor = new Color(0.6f, 0.35f, 0.15f);

    /// <summary>Framework 의 범용 StateTree 에디터를 게임 메뉴에서도 연다.</summary>
    [MenuItem("Game/AI/StateTree Editor", priority = 41)]
    public static void OpenEditor()
    {
        StateTreeEditorWindow.Open();
    }

    [MenuItem("Game/AI/Create Default Monster StateTrees (Overwrite)", priority = 40)]
    public static void CreateAll()
    {
        bool confirmed = Application.isBatchMode == true || EditorUtility.DisplayDialog("StateTree",
            "Content/AI 의 기본 StateTree 3종을 다시 만듭니다. 툴에서 수정한 내용은 덮어써집니다.", "Create", "Cancel");
        if (confirmed == false)
        {
            return;
        }

        CreateAllSilently();
    }

    public static void CreateAllSilently()
    {
        EditorAssetUtil.EnsureFolder(EditorPaths.Content + "/AI");
        save(buildMelee(), "ST_MeleeMonster");
        save(buildRanged(), "ST_RangedMonster");
        save(buildBoss(), "ST_Boss");
        AssetDatabase.SaveAssets();
        Debug.Log("[StateTree] default monster trees created");
    }

    // ------------------------------------------------------------------ trees

    private static StateTreeAsset buildMelee()
    {
        var asset = create("근접 몬스터: 귀환 → 전투(공격/추적/대기) → 배회/휴식");
        var root = asset.AddState("Root", 0);
        var ret = addReturn(asset, root.Id, 0.0f);
        var combat = addCombat(asset, root.Id, ret.Id);
        addAttackChaseHold(asset, combat.Id, new EAbilitySlot[0]);
        addIdle(asset, root.Id, combat.Id, true);
        return asset;
    }

    private static StateTreeAsset buildRanged()
    {
        var asset = create("원거리 몬스터: 너무 가까우면 거리 벌리기 → 공격/추적/대기");
        var root = asset.AddState("Root", 0);
        var ret = addReturn(asset, root.Id, 0.0f);
        var combat = addCombat(asset, root.Id, ret.Id);

        var retreat = asset.AddState("Retreat", combat.Id);
        retreat.Color = CombatColor;
        retreat.EnterConditions.Add(new BlackboardCompareCondition { Key = EBBKey.TargetDistance, Op = ECompareOp.Less, Value = 4.0f });
        retreat.Tasks.Add(new KeepDistanceTask { MinDistance = 7.0f, StepDistance = 6.0f });
        gotoOnComplete(retreat, combat.Id);

        addAttackChaseHold(asset, combat.Id, new EAbilitySlot[0]);
        addIdle(asset, root.Id, combat.Id, true);
        return asset;
    }

    private static StateTreeAsset buildBoss()
    {
        var asset = create("V Blood 보스: 1페이즈(투석/근접) → 체력 50% 이하 2페이즈(격노 + 도약/전 스킬)");
        var root = asset.AddState("Root", 0);
        var ret = addReturn(asset, root.Id, 30.0f);
        var combat = addCombat(asset, root.Id, ret.Id);

        var phase2 = asset.AddState("Phase2", combat.Id);
        phase2.Color = PhaseColor;
        phase2.EnterConditions.Add(new BlackboardCompareCondition { Key = EBBKey.HealthRatio, Op = ECompareOp.LessOrEqual, Value = 0.5f });
        phase2.Tasks.Add(new ApplyEffectToSelfTask { EffectId = 23, OncePerLife = true });
        addAttackChaseHold(asset, phase2.Id, new[] { EAbilitySlot.Spell1, EAbilitySlot.WeaponQ, EAbilitySlot.WeaponE, EAbilitySlot.Primary });

        var phase1 = asset.AddState("Phase1", combat.Id);
        phase1.Color = PhaseColor;
        var toPhase2 = transition(phase1, ETransitionTrigger.OnTick, ETransitionType.GotoState, phase2.Id);
        toPhase2.Conditions.Add(new BlackboardCompareCondition { Key = EBBKey.HealthRatio, Op = ECompareOp.LessOrEqual, Value = 0.5f });
        addAttackChaseHold(asset, phase1.Id, new[] { EAbilitySlot.WeaponE, EAbilitySlot.Primary });

        addIdle(asset, root.Id, combat.Id, false);
        return asset;
    }

    // ------------------------------------------------------------------ building blocks

    private static StateTreeState addReturn(StateTreeAsset InAsset, int InParent, float InLeash)
    {
        var state = InAsset.AddState("Return", InParent);
        state.Color = ReturnColor;
        state.EnterConditions.Add(new LeashExceededCondition { LeashRange = InLeash });
        state.Tasks.Add(new ReturnHomeTask());
        transition(state, ETransitionTrigger.OnStateCompleted, ETransitionType.Root, 0);
        return state;
    }

    private static StateTreeState addCombat(StateTreeAsset InAsset, int InParent, int InReturnId)
    {
        var combat = InAsset.AddState("Combat", InParent);
        combat.Color = CombatColor;
        combat.EnterConditions.Add(new HasTargetCondition());

        var lost = transition(combat, ETransitionTrigger.OnTick, ETransitionType.Root, 0);
        lost.Conditions.Add(new HasTargetCondition { Invert = true });

        var leash = transition(combat, ETransitionTrigger.OnTick, ETransitionType.GotoState, InReturnId);
        leash.Conditions.Add(new LeashExceededCondition());
        return combat;
    }

    /// <summary>Attack(쓸 수 있으면 공격) → Chase(사거리 밖이면 추적) → Hold(사거리 안인데 쿨다운이면 바라보며 대기).</summary>
    private static void addAttackChaseHold(StateTreeAsset InAsset, int InParent, EAbilitySlot[] InSlots)
    {
        var attack = InAsset.AddState("Attack", InParent);
        attack.Color = CombatColor;
        attack.EnterConditions.Add(new AbilityReadyCondition { Slots = InSlots });
        attack.Tasks.Add(new UseAbilityTask { Slots = InSlots });
        gotoOnComplete(attack, InParent);

        var chase = InAsset.AddState("Chase", InParent);
        chase.Color = CombatColor;
        chase.EnterConditions.Add(new InAttackRangeCondition { Multiplier = 0.9f, Invert = true });
        chase.Tasks.Add(new ChaseTargetTask { StopMultiplier = 0.8f });
        gotoOnComplete(chase, InParent);

        var hold = InAsset.AddState("Hold", InParent);
        hold.Color = CombatColor;
        hold.Tasks.Add(new FaceTargetTask());
        hold.Tasks.Add(new WaitTask { Duration = 0.25f });
        gotoOnComplete(hold, InParent);
    }

    private static void addIdle(StateTreeAsset InAsset, int InParent, int InCombatId, bool InWander)
    {
        var idle = InAsset.AddState("Idle", InParent);
        idle.Color = IdleColor;
        var acquired = transition(idle, ETransitionTrigger.OnEvent, ETransitionType.GotoState, InCombatId);
        acquired.EventId = (int)EAIEvent.TargetAcquired;

        if (InWander == false)
        {
            idle.Selection = ESelectionBehavior.TryEnterState;
            idle.Tasks.Add(new FaceTargetTask());
            idle.Tasks.Add(new WaitTask { Duration = 1.0f });
            transition(idle, ETransitionTrigger.OnStateCompleted, ETransitionType.Root, 0);
            return;
        }

        var wander = InAsset.AddState("Wander", idle.Id);
        wander.Color = IdleColor;
        wander.Tasks.Add(new WanderTask { Radius = 6.0f });
        transition(wander, ETransitionTrigger.OnStateCompleted, ETransitionType.NextState, 0);

        var rest = InAsset.AddState("Rest", idle.Id);
        rest.Color = IdleColor;
        rest.Tasks.Add(new WaitTask { Duration = 4.0f, RandomDeviation = 2.0f });
        transition(rest, ETransitionTrigger.OnStateCompleted, ETransitionType.Root, 0);
    }

    // ------------------------------------------------------------------ helpers

    private static StateTreeAsset create(string InDescription)
    {
        var asset = ScriptableObject.CreateInstance<StateTreeAsset>();
        asset.Description = InDescription;
        asset.Evaluators.Add(new MonsterPerceptionEvaluator());
        return asset;
    }

    private static StateTreeTransition transition(StateTreeState InState, ETransitionTrigger InTrigger, ETransitionType InType, int InTarget)
    {
        var transition = new StateTreeTransition();
        transition.Trigger = InTrigger;
        transition.Type = InType;
        transition.TargetStateId = InTarget;
        InState.Transitions.Add(transition);
        return transition;
    }

    private static void gotoOnComplete(StateTreeState InState, int InTarget)
    {
        transition(InState, ETransitionTrigger.OnStateCompleted, ETransitionType.GotoState, InTarget);
    }

    private static void save(StateTreeAsset InAsset, string InName)
    {
        string path = $"{EditorPaths.Content}/AI/{InName}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(InAsset, path);
    }
}
