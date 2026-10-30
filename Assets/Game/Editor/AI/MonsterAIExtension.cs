using System.Collections.Generic;
using Framework;
using Framework.Editor;
using Framework.StateTree;
using UnityEditor;
using UnityEngine;

/// <summary>
/// StateTree Tool 의 몬스터 AI 작성 도우미 (StateTreeEditorExtension 구현, 창이 자동으로 붙인다).
///  - New ▾ 템플릿: 근접 / 원거리 / 보스 (MonsterStateTreeDefaults 와 같은 구성)
///  - + 블록: 전투 골격, 공격·추적·대기, 조건부 어빌리티 사용(슬롯별), 페이즈, 귀환, 대기·배회
///  - 컨텍스트: 이 트리를 쓰는 몬스터와 그 어빌리티(슬롯 · 공격/방어), 트리와 어빌리티가 맞지 않는 곳 경고
///  - 툴바: Play 중 이 트리로 몬스터 스폰
/// 어빌리티 자체는 GAS Tool, 몬스터 외형/능력치는 MonsterAuthoring 인스펙터에서 만든다.
/// </summary>
public class MonsterAIExtension : StateTreeEditorExtension
{
    private const string AssetCsv = EditorPaths.Csv + "/AssetTable.csv";
    private const float ConditionalHealth = 0.5f;

    private static readonly string[] Templates = { "근접 몬스터", "원거리 몬스터 (거리 유지)", "보스 (체력 50% 2페이즈)" };
    private static readonly EAbilitySlot[] ConditionalSlots = { EAbilitySlot.WeaponQ, EAbilitySlot.WeaponE, EAbilitySlot.Spell1, EAbilitySlot.Spell2, EAbilitySlot.Ultimate };

    private GameTables TablesAsset;
    private List<string> BlockLabels;
    private int SpawnCharacterId;

    public override void OnEnable(StateTreeEditorWindow InWindow)
    {
        TablesAsset = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        BlockLabels = new List<string>
        {
            "전투 (타깃 있을 때) + 공격·추적·대기",
            "공격 · 추적 · 대기 (모든 슬롯)",
            "페이즈 (체력 50% 이하에서 진입)",
            "귀환 (집에서 너무 멀어지면)",
            "대기 · 배회",
        };
        foreach (var slot in ConditionalSlots)
        {
            BlockLabels.Add($"조건부 어빌리티 사용/{slot} — 체력 50% 이하");
        }
    }

    // ------------------------------------------------------------------ templates

    public override IReadOnlyList<string> TemplateNames
    {
        get { return Templates; }
    }

    public override StateTreeAsset CreateTemplate(int InIndex)
    {
        switch (InIndex)
        {
            case 0:
                return MonsterStateTreeDefaults.BuildMelee();
            case 1:
                return MonsterStateTreeDefaults.BuildRanged();
            default:
                return MonsterStateTreeDefaults.BuildBoss();
        }
    }

    // ------------------------------------------------------------------ blocks

    public override IReadOnlyList<string> BlockNames
    {
        get { return BlockLabels; }
    }

    public override void AddBlock(StateTreeAsset InAsset, int InParentId, int InIndex)
    {
        switch (InIndex)
        {
            case 0:
            {
                var ret = findByName(InAsset, "Return");
                var combat = MonsterStateTreeDefaults.AddCombat(InAsset, InParentId, ret != null ? ret.Id : 0);
                MonsterStateTreeDefaults.AddAttackChaseHold(InAsset, combat.Id, new EAbilitySlot[0]);
                break;
            }
            case 1:
                MonsterStateTreeDefaults.AddAttackChaseHold(InAsset, InParentId, new EAbilitySlot[0]);
                break;
            case 2:
            {
                var phase = InAsset.AddState("Phase2", InParentId);
                phase.Color = new Color(0.6f, 0.35f, 0.15f);
                phase.EnterConditions.Add(new BlackboardCompareCondition { Key = EBBKey.HealthRatio, Op = ECompareOp.LessOrEqual, Value = ConditionalHealth });
                MonsterStateTreeDefaults.AddAttackChaseHold(InAsset, phase.Id, new EAbilitySlot[0]);
                moveToFront(InAsset, phase);
                break;
            }
            case 3:
                moveToFront(InAsset, MonsterStateTreeDefaults.AddReturn(InAsset, InParentId, 0.0f));
                break;
            case 4:
            {
                var combat = findCombat(InAsset);
                MonsterStateTreeDefaults.AddIdle(InAsset, InParentId, combat != null ? combat.Id : 0, true);
                break;
            }
            default:
                addConditionalUse(InAsset, InParentId, ConditionalSlots[InIndex - 5]);
                break;
        }
    }

    /// <summary>
    /// 방어/버프처럼 조건이 맞을 때만 쓰는 어빌리티: InParentId 맨 앞에 (체력 조건 + AbilityReady[슬롯] → UseAbility[슬롯]) 상태를 넣고,
    /// "모든 슬롯" 으로 열린 공격 상태에서는 그 슬롯을 뺀다 (거리만 맞으면 방어부터 쓰는 것을 막음). 조건 값은 Details 에서 고친다.
    /// </summary>
    private void addConditionalUse(StateTreeAsset InAsset, int InParentId, EAbilitySlot InSlot)
    {
        var state = InAsset.AddState($"Use {InSlot}", InParentId);
        state.Color = new Color(0.25f, 0.4f, 0.6f);
        state.EnterConditions.Add(new BlackboardCompareCondition { Key = EBBKey.HealthRatio, Op = ECompareOp.LessOrEqual, Value = ConditionalHealth });
        state.EnterConditions.Add(new AbilityReadyCondition { Slots = new[] { InSlot } });
        state.Tasks.Add(new UseAbilityTask { Slots = new[] { InSlot } });
        state.Transitions.Add(new StateTreeTransition { Trigger = ETransitionTrigger.OnStateCompleted, Type = InParentId != 0 ? ETransitionType.GotoState : ETransitionType.Root, TargetStateId = InParentId });
        moveToFront(InAsset, state);

        var others = new List<EAbilitySlot>();
        foreach (var slot in ownedSlots(InAsset))
        {
            if (slot != InSlot && others.Contains(slot) == false)
            {
                others.Add(slot);
            }
        }

        foreach (var other in InAsset.States)
        {
            foreach (var task in other.Tasks)
            {
                var use = task as UseAbilityTask;
                if (use != null && use.Slots.Length == 0)
                {
                    use.Slots = others.ToArray();
                }
            }

            foreach (var condition in other.EnterConditions)
            {
                var ready = condition as AbilityReadyCondition;
                if (ready != null && ready.Slots.Length == 0)
                {
                    ready.Slots = others.ToArray();
                }
            }
        }
    }

    /// <summary>이 트리를 쓰는 몬스터들의 슬롯. 아무도 안 쓰면 공격/주문 슬롯 전부.</summary>
    private List<EAbilitySlot> ownedSlots(StateTreeAsset InAsset)
    {
        var result = new List<EAbilitySlot>();
        foreach (var character in treeUsers(InAsset))
        {
            foreach (int id in character.Abilities)
            {
                AbilityRow row;
                if (TablesAsset.Ability.TryGet(id, out row) == true && result.Contains(row.Slot) == false)
                {
                    result.Add(row.Slot);
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add(EAbilitySlot.Primary);
            result.AddRange(ConditionalSlots);
        }

        return result;
    }

    private static void moveToFront(StateTreeAsset InAsset, StateTreeState InState)
    {
        InAsset.States.Remove(InState);
        int first = InAsset.States.FindIndex(s => s.ParentId == InState.ParentId);
        InAsset.States.Insert(first >= 0 ? first : InAsset.States.Count, InState);
    }

    private static StateTreeState findByName(StateTreeAsset InAsset, string InName)
    {
        return InAsset.States.Find(s => s.Name == InName);
    }

    private static StateTreeState findCombat(StateTreeAsset InAsset)
    {
        foreach (var state in InAsset.States)
        {
            foreach (var condition in state.EnterConditions)
            {
                var hasTarget = condition as HasTargetCondition;
                if (hasTarget != null && hasTarget.Invert == false)
                {
                    return state;
                }
            }
        }

        return null;
    }

    // ------------------------------------------------------------------ context

    /// <summary>이 트리를 쓰는 몬스터와 어빌리티, 트리 ↔ 어빌리티 불일치 경고.</summary>
    public override void DrawContext(StateTreeEditorWindow InWindow, StateTreeAsset InAsset)
    {
        if (TablesAsset == null)
        {
            return;
        }

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        int assetId = assetIndex(InAsset);
        if (assetId == 0)
        {
            EditorGUILayout.HelpBox("AssetTable 에 등록되지 않은 트리입니다 — 몬스터(CharacterTable.StateTree)에 지정하려면 등록하세요.", MessageType.Info);
            using (new EditorGUI.DisabledScope(AssetDatabase.GetAssetPath(InAsset).StartsWith(EditorPaths.Content + "/AI/") == false))
            {
                if (GUILayout.Button("AssetTable 에 등록 (Content/AI 아래만)") == true)
                {
                    register(InAsset);
                }
            }
        }

        var users = treeUsers(InAsset);
        EditorGUILayout.LabelField(users.Count > 0 ? "이 트리를 쓰는 몬스터" : "이 트리를 쓰는 몬스터 없음 (MonsterAuthoring 인스펙터에서 지정)", EditorStyles.miniBoldLabel);
        foreach (var character in users)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"[{character.Index}] {text(character.NameId)}", EditorStyles.boldLabel, GUILayout.Width(170.0f));
            if (GUILayout.Button("몬스터", EditorStyles.miniButtonLeft, GUILayout.Width(52.0f)) == true)
            {
                selectAuthoring(character.Index);
            }

            if (GUILayout.Button("어빌리티", EditorStyles.miniButtonRight, GUILayout.Width(60.0f)) == true)
            {
                GasToolWindow.OpenAbilitySet(character.Index);
            }

            EditorGUILayout.EndHorizontal();
            foreach (int id in character.Abilities)
            {
                AbilityRow row;
                if (TablesAsset.Ability.TryGet(id, out row) == false)
                {
                    continue;
                }

                bool attack = AbilityInfo.IsAttack(TablesAsset, row);
                var states = statesUsing(InAsset, row.Slot);
                string usage = states.Count > 0 ? string.Join(", ", states) : "사용 안 됨";
                EditorGUILayout.LabelField($"  {row.Slot}: {text(row.NameId)} ({(attack == true ? "공격" : "방어/버프")}, {AbilityInfo.AiRange(row, character.AttackRange):0.#}m) → {usage}", EditorStyles.wordWrappedMiniLabel);
                if (attack == false && states.Count > 0 && states.TrueForAll(s => s.EndsWith("*") == true))
                {
                    EditorGUILayout.HelpBox($"{row.Slot} 는 방어/버프인데 '모든 슬롯' 공격 상태(*)가 거리만 보고 씁니다. '+ 블록 → 조건부 어빌리티 사용/{row.Slot}' 을 넣으세요.", MessageType.Warning);
                }
            }
        }

        EditorGUILayout.LabelField("* = 슬롯을 지정하지 않은(모든 슬롯) UseAbility", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
    }

    /// <summary>InSlot 을 쓸 수 있는 상태 이름 (모든 슬롯이면 * 표시).</summary>
    private static List<string> statesUsing(StateTreeAsset InAsset, EAbilitySlot InSlot)
    {
        var result = new List<string>();
        foreach (var state in InAsset.States)
        {
            foreach (var task in state.Tasks)
            {
                var use = task as UseAbilityTask;
                if (use != null && (use.Slots.Length == 0 || System.Array.IndexOf(use.Slots, InSlot) >= 0))
                {
                    result.Add(state.Name + (use.Slots.Length == 0 ? "*" : string.Empty));
                }
            }
        }

        return result;
    }

    // ------------------------------------------------------------------ toolbar

    /// <summary>Play 중: 몬스터를 골라 플레이어 앞에 스폰하고 이 트리로 AI 를 돌린다 (지정 트리와 달라도 됨).</summary>
    public override void DrawToolbar(StateTreeEditorWindow InWindow, StateTreeAsset InAsset)
    {
        if (Application.isPlaying == false || InAsset == null || TablesAsset == null)
        {
            return;
        }

        var ids = new List<int>();
        var labels = new List<string>();
        foreach (var character in TablesAsset.Character.All)
        {
            if (character.Team == ETeam.Monster)
            {
                ids.Add(character.Index);
                labels.Add($"{text(character.NameId)}");
            }
        }

        if (ids.Count == 0)
        {
            return;
        }

        if (ids.Contains(SpawnCharacterId) == false)
        {
            var users = treeUsers(InAsset);
            SpawnCharacterId = users.Count > 0 ? users[0].Index : ids[0];
        }

        SpawnCharacterId = ids[EditorGUILayout.Popup(ids.IndexOf(SpawnCharacterId), labels.ToArray(), EditorStyles.toolbarPopup, GUILayout.Width(130.0f))];
        if (GUILayout.Button("▶ 이 트리로 스폰", EditorStyles.toolbarButton, GUILayout.Width(100.0f)) == true)
        {
            spawnWithTree(SpawnCharacterId, InAsset);
        }
    }

    private static void spawnWithTree(int InCharacterId, StateTreeAsset InAsset)
    {
        var player = PlayerCharacter.Local;
        var spawner = Managers.Get<SpawnManager>();
        var registry = Managers.Get<ActorRegistry>();
        if (player == null || spawner == null || registry == null)
        {
            Debug.LogWarning("[StateTree Tool] 싱글 플레이를 시작한 뒤 스폰하세요.");
            return;
        }

        long before = 0;
        foreach (var actor in registry.All)
        {
            before = System.Math.Max(before, actor.ActorId);
        }

        spawner.SpawnMonster(InCharacterId, player.transform.position + player.transform.forward * 8.0f);

        // 스폰은 비동기(Addressables) → 새 개체가 등록될 때까지 기다렸다가 트리를 교체한다.
        double deadline = EditorApplication.timeSinceStartup + 5.0;
        EditorApplication.CallbackFunction poll = null;
        poll = () =>
        {
            if (Application.isPlaying == false || EditorApplication.timeSinceStartup > deadline)
            {
                EditorApplication.update -= poll;
                return;
            }

            foreach (var actor in registry.All)
            {
                var monster = actor as MonsterCharacter;
                if (monster != null && monster.ActorId > before && monster.CharacterId == InCharacterId)
                {
                    EditorApplication.update -= poll;
                    monster.Brain.RunTree(InAsset);
                    Selection.activeGameObject = monster.gameObject;
                    Debug.Log($"[StateTree Tool] {monster.name} #{monster.ActorId} 스폰 — 툴바 Debug 에서 선택하면 라이브 디버그");
                    return;
                }
            }
        };
        EditorApplication.update += poll;
    }

    // ------------------------------------------------------------------ lookups

    private int assetIndex(StateTreeAsset InAsset)
    {
        string path = AssetDatabase.GetAssetPath(InAsset);
        foreach (var row in TablesAsset.Asset.All)
        {
            if (row.Address != null && path == $"{EditorPaths.Content}/{row.Address}.asset")
            {
                return row.Index;
            }
        }

        return 0;
    }

    private List<CharacterRow> treeUsers(StateTreeAsset InAsset)
    {
        var result = new List<CharacterRow>();
        int assetId = assetIndex(InAsset);
        if (assetId == 0)
        {
            return result;
        }

        foreach (var character in TablesAsset.Character.All)
        {
            if (character.StateTree == assetId)
            {
                result.Add(character);
            }
        }

        return result;
    }

    private void register(StateTreeAsset InAsset)
    {
        string path = AssetDatabase.GetAssetPath(InAsset);
        string address = path.Substring(EditorPaths.Content.Length + 1).Replace(".asset", string.Empty);
        var row = CsvRow.Create(AssetCsv, CsvEditUtility.NextKey(AssetCsv, 300), null);
        row.Set("Address", address);
        row.SetEnum("Kind", EAssetKind.Data);
        row.Save();
        AddressablesSetup.Run();
    }

    private static void selectAuthoring(int InCharacterId)
    {
        foreach (var monster in MonsterAuthoringUtil.LoadAll())
        {
            if (monster.CharacterId == InCharacterId)
            {
                Selection.activeObject = monster;
                EditorGUIUtility.PingObject(monster);
                return;
            }
        }
    }

    private string text(int InStringId)
    {
        StringRow row;
        return TablesAsset.String.TryGet(InStringId, out row) == true ? row.Text : $"#{InStringId}";
    }
}
