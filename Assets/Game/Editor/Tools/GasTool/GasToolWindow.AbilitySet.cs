using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// [Ability Set] 탭: 캐릭터가 가진 어빌리티 목록 (CharacterTable.Abilities) — 언리얼 Lyra 의 AbilitySet 대응.
/// 스폰 시 Actor.Initialize 가 이 순서대로 GiveAbility 하며, 같은 Slot 은 뒤의 것이 앞의 것을 교체한다.
/// AI 가 언제 어떤 슬롯을 쓰는지는 StateTree Tool 에서 정한다.
/// </summary>
public partial class GasToolWindow
{
    [SerializeField] private int SelectedCharacterId;
    private int SetAddIndex;
    private bool SetMonsterOnly = true;

    public CsvRow CharacterRow(int InId)
    {
        return cachedRow(CharacterRows, CharacterCsv, InId);
    }

    private void drawSetList()
    {
        foreach (var character in TablesAsset.Character.All)
        {
            string label = $"[{character.Index}] {characterText(character.NameId)} ({character.Team})";
            if (matchesFilter(label) == false)
            {
                continue;
            }

            var row = CharacterRow(character.Index);
            if (listButton(character.Index == SelectedCharacterId, label + (row != null && row.Dirty == true ? " *" : string.Empty), character.Team != ETeam.Monster) == true)
            {
                SelectedCharacterId = character.Index;
                GUI.FocusControl(null);
            }
        }
    }

    private void drawSetEditor()
    {
        var row = SelectedCharacterId != 0 ? CharacterRow(SelectedCharacterId) : null;
        if (row == null)
        {
            EditorGUILayout.HelpBox("왼쪽에서 캐릭터를 고르세요. Ability Set = 그 캐릭터가 스폰될 때 받는 어빌리티 목록입니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField($"[{SelectedCharacterId}] {characterText(row.GetInt("NameId"))} — Ability Set", EditorStyles.largeLabel);
        var ids = new List<int>(row.GetInts("Abilities"));
        var slotOwner = new Dictionary<EAbilitySlot, int>();
        int remove = -1;
        int moveUp = -1;
        for (int i = 0; i < ids.Count; ++i)
        {
            AbilityRow ability;
            bool exists = TablesAsset.Ability.TryGet(ids[i], out ability);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            string kind = exists == true ? (ability.Type == EAbilityType.Graph ? "GAS" : ability.Type + " (기존)") : "?";
            string attack = exists == true ? (AbilityInfo.IsAttack(TablesAsset, ability) == true ? "공격" : "방어/버프") : string.Empty;
            EditorGUILayout.LabelField(exists == true ? $"{ability.Slot}  [{ids[i]}] {AbilityName(ids[i])}  —  {kind} · {attack} · CD {ability.Cooldown:0.##}s · AI {AbilityInfo.AiRange(ability, row.GetFloat("AttackRange")):0.#}m" : $"[{ids[i]}] AbilityTable 에 없음", EditorStyles.boldLabel);
            if (exists == true && GUILayout.Button("편집", EditorStyles.miniButtonLeft, GUILayout.Width(40.0f)) == true)
            {
                Tab = 0;
                SelectedAbilityId = ids[i];
                GraphSerialized = null;
            }

            using (new EditorGUI.DisabledScope(i == 0))
            {
                if (GUILayout.Button("▲", EditorStyles.miniButtonMid, GUILayout.Width(24.0f)) == true)
                {
                    moveUp = i;
                }
            }

            if (GUILayout.Button("✕", EditorStyles.miniButtonRight, GUILayout.Width(24.0f)) == true)
            {
                remove = i;
            }

            EditorGUILayout.EndHorizontal();
            if (exists == true)
            {
                EditorGUILayout.LabelField(AbilityInfo.Summary(TablesAsset, ability), EditorStyles.wordWrappedMiniLabel);
                int previous;
                if (slotOwner.TryGetValue(ability.Slot, out previous) == true)
                {
                    EditorGUILayout.HelpBox($"Slot {ability.Slot} 이 [{previous}] 와 겹칩니다. 스폰 시 뒤의 [{ids[i]}] 가 앞의 것을 교체합니다.", MessageType.Warning);
                }

                slotOwner[ability.Slot] = ids[i];
            }

            EditorGUILayout.EndVertical();
        }

        if (moveUp > 0)
        {
            int id = ids[moveUp];
            ids[moveUp] = ids[moveUp - 1];
            ids[moveUp - 1] = id;
            row.SetInts("Abilities", ids);
        }

        if (remove >= 0)
        {
            ids.RemoveAt(remove);
            row.SetInts("Abilities", ids);
        }

        EditorGUILayout.Space();
        drawSetAdd(row, ids);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("AI 가 언제 어떤 슬롯을 쓰는지는 StateTree Tool 에서 정합니다. 공격은 기본 Attack 상태(거리 + 쿨다운)가 쓰고, 방어/버프는 '+ 블록 → 조건부 어빌리티 사용' 으로 연결하세요.", MessageType.None);
        if (GUILayout.Button("이 캐릭터의 AI 트리 열기 (StateTree Tool)", GUILayout.Height(24.0f)) == true)
        {
            openTree(row.GetInt("StateTree"));
        }

        if (row.Dirty == true)
        {
            EditorGUILayout.LabelField("변경됨 — 저장하면 CharacterTable.csv 에 기록됩니다.", EditorStyles.miniBoldLabel);
        }
    }

    private void drawSetAdd(CsvRow InRow, List<int> InIds)
    {
        var candidates = new List<AbilityRow>();
        foreach (var ability in TablesAsset.Ability.All)
        {
            bool monster = ability.AbilityTags != null && System.Array.IndexOf(ability.AbilityTags, ETag.Ability_Monster) >= 0;
            if (SetMonsterOnly == true && monster == false)
            {
                continue;
            }

            candidates.Add(ability);
        }

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("어빌리티 추가");
        if (candidates.Count > 0)
        {
            var labels = new string[candidates.Count];
            for (int i = 0; i < candidates.Count; ++i)
            {
                var ability = candidates[i];
                labels[i] = $"[{ability.Index}] {AbilityName(ability.Index)} — {ability.Slot}{(InIds.Contains(ability.Index) == true ? " (보유)" : string.Empty)}";
            }

            SetAddIndex = Mathf.Clamp(EditorGUILayout.Popup(SetAddIndex, labels), 0, candidates.Count - 1);
            var picked = candidates[SetAddIndex];
            using (new EditorGUI.DisabledScope(InIds.Contains(picked.Index) == true))
            {
                if (GUILayout.Button("추가", GUILayout.Width(50.0f)) == true)
                {
                    InIds.Add(picked.Index);
                    InRow.SetInts("Abilities", InIds);
                }
            }
        }

        SetMonsterOnly = GUILayout.Toggle(SetMonsterOnly, new GUIContent("몬스터용만", "AbilityTags 에 Ability_Monster"), EditorStyles.miniButton, GUILayout.Width(80.0f));
        EditorGUILayout.EndHorizontal();
    }

    private void openTree(int InAssetId)
    {
        AssetRow asset;
        if (InAssetId == 0 || TablesAsset.Asset.TryGet(InAssetId, out asset) == false)
        {
            ShowNotification(new GUIContent("AI 트리가 지정되지 않았습니다"));
            return;
        }

        var tree = AssetDatabase.LoadAssetAtPath<Framework.StateTree.StateTreeAsset>($"{EditorPaths.Content}/{asset.Address}.asset");
        if (tree != null)
        {
            StateTreeEditorWindow.Open(tree);
        }
    }

    private string characterText(int InStringId)
    {
        var text = TextRow(InStringId);
        return text != null ? text.Get("Text") : $"#{InStringId}";
    }
}
