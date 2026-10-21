using ActionRPG.Combat;
using ActionRPG.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>같은 파티의 다른 플레이어 한 명을 보여 주는 작은 프레임.</summary>
    public class PartyFrameView : MonoBehaviour
    {
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image healthFill;
        [SerializeField] private GameObject deadMarker;

        private PlayerNetworkIdentity _identity;
        private Health _health;

        public void Bind(PlayerNetworkIdentity identity)
        {
            Unbind();
            _identity = identity;
            if (_identity == null) return;

            _health = _identity.GetComponent<Health>();

            _identity.NameChanged += OnNameChanged;
            OnNameChanged(_identity.DisplayName);

            if (_health != null)
            {
                _health.Changed += OnHealthChanged;
                _health.Died += OnDied;
                _health.Revived += OnRevived;
                OnHealthChanged(_health.Current, _health.Max);
                SetDead(!_health.IsAlive);
            }
        }

        private void OnDisable() => Unbind();

        private void Unbind()
        {
            if (_identity != null) _identity.NameChanged -= OnNameChanged;
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }
            _identity = null;
            _health = null;
        }

        private void OnNameChanged(string value)
        {
            if (nameText != null) nameText.text = value;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (healthFill != null) healthFill.fillAmount = max > 0f ? current / max : 0f;
        }

        private void OnDied(Core.DamageInfo _) => SetDead(true);
        private void OnRevived() => SetDead(false);

        private void SetDead(bool dead)
        {
            if (deadMarker != null) deadMarker.SetActive(dead);
        }
    }
}
