using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 밸런스 툴 (Window/Game/Balance Tool).
///  [Live]      플레이 중 CombatLog 집계: 공격자·어빌리티별 누적 피해/DPS, 대상별 받은 피해 총합과 어빌리티 내역, 최근 기록
///  [Simulator] 테이블 값만으로 "공격자 어빌리티 → 방어자" 1타 피해, 처치까지 타수, 대략 DPS 를 계산 (DamageExecution 과 같은 공식)
/// </summary>
public class BalanceToolWindow : EditorWindow
{
    private static readonly string[] Tabs = { "Live", "Simulator" };

    private int Tab;
    private Vector2 Scroll;
    private GameTables TablesAsset;

    private int AttackerIndex;
    private int AbilityIndex;
    private int DefenderIndex;
    private float AttackerLevel = -1.0f;
    private float BonusPower;
    private float CritChance = 5.0f;

    [MenuItem("Window/Game/Balance Tool")]
    [MenuItem("Game/Tools/Balance Tool", priority = 30)]
    public static void Open()
    {
        GetWindow<BalanceToolWindow>("Balance Tool");
    }

    private void OnEnable()
    {
        TablesAsset = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
    }

    private void OnInspectorUpdate()
    {
        if (Application.isPlaying == true && Tab == 0)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        Tab = GUILayout.Toolbar(Tab, Tabs);
        Scroll = EditorGUILayout.BeginScrollView(Scroll);
        if (Tab == 0)
        {
            drawLive();
        }
        else
        {
            drawSimulator();
        }

        EditorGUILayout.EndScrollView();
    }

    // ------------------------------------------------------------------ live

    private void drawLive()
    {
        if (Application.isPlaying == false)
        {
            EditorGUILayout.HelpBox("Play 모드에서 전투 기록이 집계됩니다. (치트 'clearlog' 또는 아래 버튼으로 초기화)", MessageType.Info);
            return;
        }

        if (GUILayout.Button("Clear Combat Log") == true)
        {
            CombatLog.Clear();
        }

        float elapsed = Mathf.Max(1.0f, Time.time - CombatLog.SessionStartTime);
        EditorGUILayout.LabelField($"Session {elapsed:0.0}s, records {CombatLog.Count}", EditorStyles.boldLabel);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Damage dealt (attacker · ability)", EditorStyles.boldLabel);
        foreach (var pair in CombatLog.GetDamageBySourceAbility().OrderByDescending(p => p.Value))
        {
            int character;
            int ability;
            CombatLog.SplitKey(pair.Key, out character, out ability);
            EditorGUILayout.LabelField($"{characterName(character)} · {abilityName(ability)}", $"{pair.Value:0}   ({pair.Value / elapsed:0.0} DPS)");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Damage taken (target total → breakdown)", EditorStyles.boldLabel);
        var registry = Framework.Managers.Get<ActorRegistry>();
        foreach (var pair in CombatLog.GetDamageTakenByTarget().OrderByDescending(p => p.Value))
        {
            var actor = registry != null ? registry.Find(pair.Key) : null;
            string label = actor != null ? $"{characterName(actor.CharacterId)} #{pair.Key}" : $"#{pair.Key} (gone)";
            EditorGUILayout.LabelField(label, $"{pair.Value:0}");
            var breakdown = CombatLog.GetTakenBreakdown(pair.Key);
            if (breakdown == null)
            {
                continue;
            }

            EditorGUI.indentLevel += 1;
            foreach (var item in breakdown.OrderByDescending(p => p.Value))
            {
                EditorGUILayout.LabelField(abilityName(item.Key), $"{item.Value:0} ({item.Value / pair.Value * 100.0f:0}%)");
            }

            EditorGUI.indentLevel -= 1;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Recent", EditorStyles.boldLabel);
        for (int i = 0; i < Mathf.Min(30, CombatLog.Count); ++i)
        {
            var r = CombatLog.GetRecent(i);
            string kind = r.IsHeal == true ? "HEAL" : r.DamageType.ToString();
            string crit = r.IsCritical == true ? " CRIT" : string.Empty;
            EditorGUILayout.LabelField($"{r.Time:0.00}s {characterName(r.SourceCharacter)} → {characterName(r.TargetCharacter)}",
                $"[{abilityName(r.AbilityId)}] {kind} raw {r.Raw:0} final {r.Final:0} abs {r.Absorbed:0}{crit}");
        }
    }

    // ------------------------------------------------------------------ simulator

    private void drawSimulator()
    {
        if (TablesAsset == null)
        {
            TablesAsset = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
            if (TablesAsset == null)
            {
                EditorGUILayout.HelpBox("GameTables.asset 이 없습니다. Game/Setup/2. Import CSV 를 먼저 실행하세요.", MessageType.Warning);
                return;
            }
        }

        var characters = TablesAsset.Character.All.ToList();
        var abilities = TablesAsset.Ability.All.ToList();
        string[] characterNames = characters.Select(c => $"{c.Index}: {text(c.NameId)}").ToArray();
        string[] abilityNames = abilities.Select(a => $"{a.Index}: {text(a.NameId)} ({a.Type})").ToArray();

        AttackerIndex = EditorGUILayout.Popup("Attacker", AttackerIndex, characterNames);
        AbilityIndex = EditorGUILayout.Popup("Ability", AbilityIndex, abilityNames);
        DefenderIndex = EditorGUILayout.Popup("Defender", DefenderIndex, characterNames);
        AttackerLevel = EditorGUILayout.FloatField("Attacker Level (-1 = table)", AttackerLevel);
        BonusPower = EditorGUILayout.FloatField("Bonus Power (gear)", BonusPower);
        CritChance = EditorGUILayout.Slider("Crit Chance %", CritChance, 0.0f, 100.0f);

        var attacker = characters[Mathf.Clamp(AttackerIndex, 0, characters.Count - 1)];
        var ability = abilities[Mathf.Clamp(AbilityIndex, 0, abilities.Count - 1)];
        var defender = characters[Mathf.Clamp(DefenderIndex, 0, characters.Count - 1)];

        float perLevel = constant(EConst.LevelDamageScalePerLevel);
        float minScale = constant(EConst.LevelDamageScaleMin);
        float maxScale = constant(EConst.LevelDamageScaleMax);
        float critMultiplier = constant(EConst.CritBaseMultiplier);

        float level = AttackerLevel >= 0.0f ? AttackerLevel : attacker.Level;
        float power = (ability.DamageType == EDamageType.Spell ? attacker.SpellPower : attacker.PhysicalPower) + BonusPower;
        float raw = power * ability.Damage * 0.01f;
        float levelScale = Mathf.Clamp(1.0f + (level - defender.Level) * perLevel, minScale, maxScale);
        float resist = ability.DamageType == EDamageType.Spell ? defender.SpellResist : (ability.DamageType == EDamageType.Physical ? defender.PhysicalResist : 0.0f);
        float hit = raw * levelScale * (1.0f - resist * 0.01f);
        float expected = hit * (1.0f + CritChance * 0.01f * (critMultiplier - 1.0f));
        int pulses = Mathf.Max(1, ability.Count);
        bool multiHit = ability.Type == EAbilityType.AreaBurst;
        float perCast = multiHit == true ? expected * pulses : expected;
        float castTime = Mathf.Max(0.1f, Mathf.Max(ability.MoveLock, ability.CastTime + (multiHit == true ? ability.Spread * (pulses - 1) : 0.0f)));
        float cycle = Mathf.Max(castTime, ability.Cooldown);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Result", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Power × Coefficient", $"{power:0.0} × {ability.Damage}% = {raw:0.0}");
        EditorGUILayout.LabelField("Level scale", $"{levelScale:0.00} (atk {level:0} vs def {defender.Level:0})");
        EditorGUILayout.LabelField("Resist", $"{resist:0}%");
        EditorGUILayout.LabelField("Damage per hit", $"{hit:0.0} (crit {hit * critMultiplier:0.0})");
        EditorGUILayout.LabelField("Expected per cast", $"{perCast:0.0}{(multiHit == true ? $"  ({pulses} pulses)" : string.Empty)}");
        EditorGUILayout.LabelField("Hits to kill", $"{Mathf.CeilToInt(defender.MaxHealth / Mathf.Max(0.01f, expected))}");
        EditorGUILayout.LabelField("Sustained DPS (spam)", $"{perCast / cycle:0.0}  (cycle {cycle:0.00}s)");
    }

    private float constant(EConst InConst)
    {
        ConstRow row;
        return TablesAsset.Const.TryGet((int)InConst, out row) == true ? row.Value : 0.0f;
    }

    private string text(int InId)
    {
        var tables = TablesAsset != null ? TablesAsset : Tables.Root;
        StringRow row;
        if (tables == null || tables.String.TryGet(InId, out row) == false)
        {
            return $"#{InId}";
        }

        return row.Text;
    }

    private string characterName(int InCharacterId)
    {
        var tables = TablesAsset != null ? TablesAsset : Tables.Root;
        CharacterRow row;
        if (InCharacterId == 0 || tables == null || tables.Character.TryGet(InCharacterId, out row) == false)
        {
            return "World";
        }

        return text(row.NameId);
    }

    private string abilityName(int InAbilityId)
    {
        var tables = TablesAsset != null ? TablesAsset : Tables.Root;
        AbilityRow row;
        if (InAbilityId == 0 || tables == null || tables.Ability.TryGet(InAbilityId, out row) == false)
        {
            return "Effect";
        }

        return text(row.NameId);
    }
}
