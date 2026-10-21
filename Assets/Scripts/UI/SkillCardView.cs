using System;
using ActionRPG.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 덱 편집 화면의 스킬 카드 한 장. 후보 목록과 덱 슬롯 양쪽에 같은 프리팹을 쓴다.
    /// </summary>
    public class SkillCardView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text slotLabel;
        [SerializeField] private Button button;
        [SerializeField] private GameObject selectedMarker;
        [SerializeField] private GameObject inDeckMarker;
        [SerializeField] private GameObject emptyState;

        private Action<SkillCardView> _onClick;

        public SkillDefinition Skill { get; private set; }
        /// <summary>덱 슬롯으로 쓰일 때의 인덱스. 후보 카드면 -1.</summary>
        public int SlotIndex { get; private set; } = -1;

        private void Awake()
        {
            if (button == null) button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(() => _onClick?.Invoke(this));
        }

        public void Bind(SkillDefinition skill, Action<SkillCardView> onClick, int slotIndex = -1, string slotText = null)
        {
            Skill = skill;
            SlotIndex = slotIndex;
            _onClick = onClick;

            bool hasSkill = skill != null;
            if (emptyState != null) emptyState.SetActive(!hasSkill);

            if (iconImage != null)
            {
                iconImage.enabled = hasSkill && skill.Icon != null;
                if (hasSkill && skill.Icon != null) iconImage.sprite = skill.Icon;
            }

            if (nameText != null) nameText.text = hasSkill ? skill.DisplayName : "비어 있음";
            if (costText != null) costText.text = hasSkill ? skill.DeckCost.ToString() : string.Empty;
            if (slotLabel != null)
            {
                slotLabel.gameObject.SetActive(!string.IsNullOrEmpty(slotText));
                slotLabel.text = slotText ?? string.Empty;
            }

            SetSelected(false);
            SetInDeck(false);
        }

        public void SetSelected(bool value)
        {
            if (selectedMarker != null) selectedMarker.SetActive(value);
        }

        public void SetInDeck(bool value)
        {
            if (inDeckMarker != null) inDeckMarker.SetActive(value);
        }

        public void SetInteractable(bool value)
        {
            if (button != null) button.interactable = value;
        }
    }
}
