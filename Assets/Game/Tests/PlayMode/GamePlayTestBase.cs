using System;
using System.Collections;
using System.Collections.Generic;
using Framework;
using Framework.Ability;
using Framework.StateTree;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play 모드 시나리오 테스트 공통 기반.
///  - 첫 테스트에서 World 씬을 로드하고 싱글 플레이를 시작한다 (이후 테스트는 같은 세션을 재사용).
///  - 테스트마다: 월드 몬스터 정리(간섭 방지), 밤 고정(햇빛 피해 방지), 플레이어 부활·체력 회복·위치 초기화.
///  - 테스트가 스폰한 몬스터는 TearDown 에서 제거한다.
/// WaitFor 는 매 프레임 InTick(기본: 플레이어 체력 유지)을 실행하며 조건을 기다리고, 시간이 넘으면 실패한다.
/// </summary>
public abstract class GamePlayTestBase
{
    protected const string WorldScenePath = "Assets/Game/Scenes/World.unity";

    protected PlayerCharacter Player;
    protected bool KeepPlayerAlive = true;
    private readonly List<MonsterCharacter> Spawned = new List<MonsterCharacter>();
    /// <summary>싱글 플레이 시작 위치 (테스트마다 플레이어를 여기로 되돌린다).</summary>
    protected static Vector3 StartPosition { get; private set; }
    private static bool Started;

    [UnitySetUp]
    public IEnumerator SetUpGame()
    {
        if (Started == false || PlayerCharacter.Local == null)
        {
            yield return loadWorld();
        }

        Player = PlayerCharacter.Local;
        Assert.IsNotNull(Player, "플레이어가 없습니다");
        KeepPlayerAlive = true;

        var dayNight = Managers.Get<DayNightManager>();
        dayNight.SetHour(0.0f);
        dayNight.Frozen = true;

        clearMonsters();
        if (Player.IsDead == true)
        {
            Player.Revive(StartPosition, 1.0f);
        }

        Player.RemoveTag((int)ETag.Cheat_God);
        Player.Motor.Teleport(StartPosition);
        Player.Attributes.Set(EAttribute.Health, Player.Attributes.Get(EAttribute.MaxHealth));
        Player.Asc.ClearCooldowns();
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDownGame()
    {
        foreach (var monster in Spawned)
        {
            if (monster != null)
            {
                UnityEngine.Object.Destroy(monster.gameObject);
            }
        }

        Spawned.Clear();
        if (Player != null)
        {
            Player.RemoveTag((int)ETag.Cheat_God);
        }

        yield return null;
    }

    // ------------------------------------------------------------------ world

    private static IEnumerator loadWorld()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(WorldScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene("World");
#endif
        yield return waitStatic(() => Tables.IsReady == true && Managers.Get<SpawnManager>() != null && title() != null, 60.0f, "World 로딩");
        title().OnStartSinglePlayer.Invoke();
        yield return waitStatic(() => PlayerCharacter.Local != null, 60.0f, "싱글 플레이 시작");
        StartPosition = PlayerCharacter.Local.transform.position;
        Started = true;
    }

    private static TitleWindow title()
    {
        var ui = Managers.Get<UIManager>();
        return ui != null ? ui.Get<TitleWindow>() : null;
    }

    /// <summary>월드에 이미 있는 몬스터는 지운다 (Destroy 는 리스폰을 일으키지 않는다).</summary>
    private static void clearMonsters()
    {
        var registry = Managers.Get<ActorRegistry>();
        var buffer = new List<Actor>(registry.All);
        foreach (var actor in buffer)
        {
            if (actor is MonsterCharacter)
            {
                UnityEngine.Object.Destroy(actor.gameObject);
            }
        }
    }

    // ------------------------------------------------------------------ helpers

    /// <summary>InCharacterId 몬스터를 플레이어 기준 InOffset(플레이어 로컬 방향) 위치에 스폰하고 기다린다.</summary>
    protected IEnumerator SpawnMonster(int InCharacterId, Vector3 InOffset, Action<MonsterCharacter> OnSpawned)
    {
        var registry = Managers.Get<ActorRegistry>();
        long before = 0;
        foreach (var actor in registry.All)
        {
            before = Math.Max(before, actor.ActorId);
        }

        var position = Player.transform.position + Player.transform.rotation * InOffset;
        Managers.Get<SpawnManager>().SpawnMonster(InCharacterId, position);
        MonsterCharacter spawned = null;
        yield return WaitFor(() =>
        {
            foreach (var actor in registry.All)
            {
                var monster = actor as MonsterCharacter;
                if (monster != null && monster.ActorId > before && monster.CharacterId == InCharacterId)
                {
                    spawned = monster;
                    return true;
                }
            }

            return false;
        }, 10.0f, $"몬스터 {InCharacterId} 스폰");

        Spawned.Add(spawned);
        OnSpawned(spawned);
    }

    /// <summary>InCondition 이 참이 될 때까지 기다린다. 매 프레임 플레이어 체력을 채운다(KeepPlayerAlive). 시간 초과면 실패.</summary>
    protected IEnumerator WaitFor(Func<bool> InCondition, float InTimeout, string InWhat, Action InTick = null)
    {
        float deadline = Time.time + InTimeout;
        while (InCondition() == false)
        {
            if (Time.time > deadline)
            {
                Assert.Fail($"시간 초과 ({InTimeout:0.#}s): {InWhat}");
            }

            tick();
            if (InTick != null)
            {
                InTick();
            }

            yield return null;
        }
    }

    protected IEnumerator WaitSeconds(float InSeconds, Action InTick = null)
    {
        float end = Time.time + InSeconds;
        while (Time.time < end)
        {
            tick();
            if (InTick != null)
            {
                InTick();
            }

            yield return null;
        }
    }

    private void tick()
    {
        if (KeepPlayerAlive == true && Player != null && Player.IsDead == false)
        {
            Player.Attributes.Set(EAttribute.Health, Player.Attributes.Get(EAttribute.MaxHealth));
        }
    }

    private static IEnumerator waitStatic(Func<bool> InCondition, float InTimeout, string InWhat)
    {
        float deadline = Time.realtimeSinceStartup + InTimeout;
        while (InCondition() == false)
        {
            if (Time.realtimeSinceStartup > deadline)
            {
                Assert.Fail($"시간 초과 ({InTimeout:0.#}s): {InWhat}");
            }

            yield return null;
        }
    }

    /// <summary>몬스터 AI 의 현재 활성 경로에 InStateName 상태가 있는가.</summary>
    protected static bool IsInState(MonsterCharacter InMonster, string InStateName)
    {
        var tree = InMonster.Brain != null ? InMonster.Brain.Tree : null;
        if (tree == null)
        {
            return false;
        }

        foreach (int id in tree.Path)
        {
            var state = tree.Asset.Find(id);
            if (state != null && state.Name == InStateName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>테스트용 AbilityTable 행 (Type=Graph). 그래프는 GraphAbility.Create 로 직접 넣는다.</summary>
    protected static AbilityRow MakeGraphRow(int InIndex, EAbilitySlot InSlot, float InCooldown, float InAiRange)
    {
        var row = new AbilityRow();
        row.Index = InIndex;
        row.Type = EAbilityType.Graph;
        row.Slot = InSlot;
        row.AbilityTags = new[] { ETag.Ability_Monster };
        row.Cooldown = InCooldown;
        row.Range = InAiRange;
        row.HitEffects = new int[0];
        row.SelfEffects = new int[0];
        row.StartEffects = new int[0];
        row.ConsumeEffects = new int[0];
        row.Anims = new int[0];
        row.ConsumeTag = ETag.None;
        return row;
    }

    protected static AbilityGraphAsset MakeGraph(params AbilityStep[] InSteps)
    {
        var graph = ScriptableObject.CreateInstance<AbilityGraphAsset>();
        graph.Steps = new List<AbilityStep>(InSteps);
        return graph;
    }

    protected static AbilityActivationInfo AimAt(Actor InSource, Actor InTarget)
    {
        var info = new AbilityActivationInfo();
        info.AimPoint = InTarget.transform.position;
        info.AimDirection = MathUtil.FlatDirection(InSource.transform.position, InTarget.transform.position, InSource.transform.forward);
        info.Target = InTarget.Asc;
        return info;
    }

    /// <summary>테스트가 직접 만든 StateTree 를 돌릴 때 쓰는 전환 헬퍼.</summary>
    protected static void Goto(StateTreeState InState, int InTarget)
    {
        InState.Transitions.Add(new StateTreeTransition { Trigger = ETransitionTrigger.OnStateCompleted, Type = ETransitionType.GotoState, TargetStateId = InTarget });
    }
}
