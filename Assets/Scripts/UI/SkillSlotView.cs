using ActionRPG.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// HUD 의 스킬 슬롯 하나. 아이콘, 쿨다운 차오름, 마나 부족 표시를 담당한다.
    /// </summary>
    public class SkillSlotView : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [Tooltip("Image Type 을 Filled / Radial 360 으로 설정한다.")]
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private GameObject emptyState;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("색")]
        [SerializeField] private Color readyColor = Color.white;
        [SerializeField] private Color notEnoughManaColor = new Color(0.45f, 0.55f, 1f);
        [SerializeField] private Color onCooldownColor = new Color(0.45f, 0.45f, 0.45f);

        private SkillCaster _caster;
        private int _slotIndex;

        public void Initialize(int slotIndex, string keyText)
        {
            _slotIndex = slotIndex;
            if (keyLabel != null) keyLabel.text = keyText;
        }

        public void Bind(SkillCaster caster)
        {
            _caster = caster;
            Refresh();
        }

        /// <summary>덱이 바뀌었을 때 아이콘을 다시 그린다.</summary>
        public void Refresh()
        {
            var skill = _caster != null ? _caster.GetSkill(_slotIndex) : null;

            if (emptyState != null) emptyState.SetActive(skill == null);
            if (iconImage != null)
            {
                iconImage.enabled = skill != null && skill.Icon != null;
                if (skill != null && skill.Icon != null) iconImage.sprite = skill.Icon;
            }
        }

        private void Update()
        {
            if (_caster == null) return;

            var skill = _caster.GetSkill(_slotIndex);
            if (skill == null)
            {
                if (cooldownOverlay != null) cooldownOverlay.fillAmount = 0f;
                if (cooldownText != null) cooldownText.text = string.Empty;
                return;
            }

            float remaining = _caster.GetCooldownRemaining(_slotIndex);
            float normalized = _caster.GetCooldownNormalized(_slotIndex);

            if (cooldownOverlay != null) cooldownOverlay.fillAmount = normalized;

            if (cooldownText != null)
                cooldownText.text = remaining > 0.05f ? (remaining >= 1f ? remaining.ToString("0") : remaining.ToString("0.0")) : string.Empty;

            if (iconImage != null)
            {
                if (remaining > 0.05f) iconImage.color = onCooldownColor;
                else if (!_caster.IsSlotReady(_slotIndex)) iconImage.color = notEnoughManaColor;
                else iconImage.color = readyColor;
            }

            if (canvasGroup != null) canvasGroup.alpha = _caster.IsSlotReady(_slotIndex) ? 1f : 0.75f;
        }
    }
}
