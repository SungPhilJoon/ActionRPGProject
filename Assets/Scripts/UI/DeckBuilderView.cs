using System.Collections.Generic;
using ActionRPG.Core;
using ActionRPG.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 게임 입장 전 덱을 구성하는 화면.
    ///
    /// 조작 방식:
    ///  - 덱 슬롯을 눌러 편집할 칸을 고른다 (기본은 첫 빈 칸).
    ///  - 후보 카드를 누르면 그 칸에 들어간다. 이미 덱에 있는 스킬을 누르면 빠진다.
    ///  - 코스트 상한을 넘기면 저장 버튼이 잠긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DeckBuilderView : MonoBehaviour
    {
        [Header("목록")]
        [SerializeField] private SkillCardView cardPrefab;
        [SerializeField] private Transform libraryParent;

        [Header("덱 슬롯")]
        [SerializeField] private SkillCardView[] deckSlots = new SkillCardView[GameConstants.SkillSlotCount];
        [SerializeField] private string[] slotLabels = { "Q", "E", "R", "F" };

        [Header("상세")]
        [SerializeField] private TMP_Text detailNameText;
        [SerializeField] private TMP_Text detailDescriptionText;
        [SerializeField] private TMP_Text detailStatsText;
        [SerializeField] private Image detailIcon;

        [Header("코스트")]
        [Tooltip("덱에 넣을 수 있는 총 코스트. 0 이면 제한 없음.")]
        [SerializeField] private int costLimit = 8;
        [SerializeField] private TMP_Text costText;

        [Header("버튼")]
        [SerializeField] private Button saveButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text warningText;

        private readonly List<SkillCardView> _libraryCards = new List<SkillCardView>(32);
        private readonly List<SkillDefinition> _allSkills = new List<SkillDefinition>(32);

        private SkillLoadout _working;
        private int _editingSlot;

        private void Awake()
        {
            if (saveButton != null) saveButton.onClick.AddListener(Save);
            if (clearButton != null) clearButton.onClick.AddListener(ClearDeck);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        private void OnEnable()
        {
            _working = LoadoutStorage.Current;
            _editingSlot = NextEditSlot();
            BuildLibrary();
            RefreshAll();
        }

        private void BuildLibrary()
        {
            var database = SkillDatabase.Instance;
            if (database == null || cardPrefab == null || libraryParent == null) return;

            database.GetAll(_allSkills);

            // 카드 수가 부족하면 만들고, 남으면 감춘다.
            while (_libraryCards.Count < _allSkills.Count)
                _libraryCards.Add(Instantiate(cardPrefab, libraryParent));

            for (int i = 0; i < _libraryCards.Count; i++)
            {
                bool used = i < _allSkills.Count;
                _libraryCards[i].gameObject.SetActive(used);
                if (used) _libraryCards[i].Bind(_allSkills[i], OnLibraryCardClicked);
            }
        }

        private void RefreshAll()
        {
            RefreshDeckSlots();
            RefreshLibraryMarkers();
            RefreshCost();
        }

        private void RefreshDeckSlots()
        {
            var database = SkillDatabase.Instance;
            for (int i = 0; i < deckSlots.Length; i++)
            {
                if (deckSlots[i] == null) continue;
                var skill = database != null ? database.Get(_working[i]) : null;
                string label = slotLabels != null && i < slotLabels.Length ? slotLabels[i] : (i + 1).ToString();

                deckSlots[i].Bind(skill, OnDeckSlotClicked, i, label);
                deckSlots[i].SetSelected(i == _editingSlot);
            }
        }

        private void RefreshLibraryMarkers()
        {
            for (int i = 0; i < _libraryCards.Count; i++)
            {
                var card = _libraryCards[i];
                if (!card.gameObject.activeSelf || card.Skill == null) continue;
                card.SetInDeck(IsInDeck(card.Skill.Id));
            }
        }

        private void RefreshCost()
        {
            var database = SkillDatabase.Instance;
            int cost = database != null ? database.GetLoadoutCost(_working) : 0;
            bool overLimit = costLimit > 0 && cost > costLimit;
            bool empty = _working.FilledCount == 0;

            if (costText != null)
            {
                costText.text = costLimit > 0 ? $"코스트 {cost} / {costLimit}" : $"코스트 {cost}";
                costText.color = overLimit ? new Color(1f, 0.4f, 0.4f) : Color.white;
            }

            if (warningText != null)
            {
                if (overLimit) warningText.text = "코스트 상한을 넘었습니다.";
                else if (empty) warningText.text = "스킬을 하나 이상 넣어야 합니다.";
                else warningText.text = string.Empty;
            }

            if (saveButton != null) saveButton.interactable = !overLimit && !empty;
        }

        // ---------------------------------------------------------------- 입력

        private void OnDeckSlotClicked(SkillCardView card)
        {
            _editingSlot = Mathf.Clamp(card.SlotIndex, 0, GameConstants.SkillSlotCount - 1);

            // 이미 스킬이 있는 칸을 다시 누르면 비운다.
            if (_working[_editingSlot] != 0)
            {
                ShowDetail(SkillDatabase.Instance != null ? SkillDatabase.Instance.Get(_working[_editingSlot]) : null);
                _working[_editingSlot] = 0;
            }

            RefreshAll();
        }

        private void OnLibraryCardClicked(SkillCardView card)
        {
            if (card.Skill == null) return;
            ShowDetail(card.Skill);

            int existing = FindSlotOf(card.Skill.Id);
            if (existing >= 0)
            {
                // 덱에 이미 있으면 뺀다.
                _working[existing] = 0;
                _editingSlot = existing;
                RefreshAll();
                return;
            }

            int slot = _editingSlot;
            if (_working[slot] != 0)
            {
                int empty = FindFirstEmptySlot();
                // 덱이 꽉 찼으면 지금 편집 중인 칸을 덮어쓴다.
                if (empty >= 0) slot = empty;
            }

            _working[slot] = card.Skill.Id;
            _editingSlot = NextEditSlot();

            RefreshAll();
        }

        private void ShowDetail(SkillDefinition skill)
        {
            if (skill == null)
            {
                if (detailNameText != null) detailNameText.text = string.Empty;
                if (detailDescriptionText != null) detailDescriptionText.text = string.Empty;
                if (detailStatsText != null) detailStatsText.text = string.Empty;
                if (detailIcon != null) detailIcon.enabled = false;
                return;
            }

            if (detailNameText != null) detailNameText.text = skill.DisplayName;
            if (detailDescriptionText != null) detailDescriptionText.text = skill.Description;
            if (detailStatsText != null)
            {
                string weapon = skill.RequiredWeapon == WeaponType.Unarmed ? "무기 무관" : skill.RequiredWeapon == WeaponType.Sword ? "검 전용" : "활 전용";
                detailStatsText.text = $"쿨다운 {skill.Cooldown:0.#}초\n마나 {skill.ManaCost:0}\n사거리 {skill.Range:0.#}m\n{weapon}\n코스트 {skill.DeckCost}";
            }
            if (detailIcon != null)
            {
                detailIcon.enabled = skill.Icon != null;
                if (skill.Icon != null) detailIcon.sprite = skill.Icon;
            }
        }

        // ---------------------------------------------------------------- 동작

        public void Save()
        {
            var database = SkillDatabase.Instance;
            if (database != null && !database.Validate(_working, out string reason))
            {
                if (warningText != null) warningText.text = reason;
                return;
            }

            LoadoutStorage.Save(_working);
            if (warningText != null) warningText.text = "덱을 저장했습니다.";
        }

        public void ClearDeck()
        {
            _working = default;
            _editingSlot = 0;
            RefreshAll();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- 유틸

        private bool IsInDeck(ushort skillId) => FindSlotOf(skillId) >= 0;

        private int FindSlotOf(ushort skillId)
        {
            if (skillId == 0) return -1;
            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
                if (_working[i] == skillId) return i;
            return -1;
        }

        private int FindFirstEmptySlot()
        {
            for (int i = 0; i < GameConstants.SkillSlotCount; i++)
                if (_working[i] == 0) return i;
            return -1;
        }

        /// <summary>다음에 편집할 칸. 빈 칸이 없으면 첫 칸으로 돌아간다 (-1 이 나오지 않게).</summary>
        private int NextEditSlot()
        {
            int empty = FindFirstEmptySlot();
            return empty >= 0 ? empty : 0;
        }
    }
}
