using System.Collections.Generic;
using ActionRPG.Abilities;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ActionRPG.Networking;
using ActionRPG.Skills;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 인게임 HUD. 로컬 플레이어가 스폰되면 스스로 찾아 연결한다.
    /// 네트워크를 전혀 모르며 이벤트만 구독한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class HUDController : MonoBehaviour
    {
        [Header("자원")]
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private Image manaFill;
        [SerializeField] private TMP_Text manaText;

        [Header("스킬")]
        [SerializeField] private SkillSlotView[] skillSlots = new SkillSlotView[GameConstants.SkillSlotCount];
        [Tooltip("각 슬롯에 표시할 키 문구. 슬롯 수와 길이를 맞춘다.")]
        [SerializeField] private string[] skillKeyLabels = { "Q", "E", "R", "F" };

        [Header("진행 / 대사")]
        [SerializeField] private TMP_Text objectiveText;
        [SerializeField] private GameObject objectivePanel;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private GameObject dialoguePanel;

        [Header("파티")]
        [SerializeField] private PartyFrameView partyFramePrefab;
        [SerializeField] private Transform partyFrameParent;

        [Header("상태")]
        [SerializeField] private GameObject deathOverlay;
        [SerializeField] private TMP_Text interactPrompt;

        private GameObject _boundPlayer;
        private Health _health;
        private AbilitySystemComponent _asc;
        private SkillCaster _caster;
        private Character.PlayerInteractor _interactor;

        private readonly Dictionary<PlayerNetworkIdentity, PartyFrameView> _partyFrames = new Dictionary<PlayerNetworkIdentity, PartyFrameView>();
        private float _dialogueHideTime;
        private float _partyRefreshTimer;

        private void Awake()
        {
            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] == null) continue;
                string key = skillKeyLabels != null && i < skillKeyLabels.Length ? skillKeyLabels[i] : (i + 1).ToString();
                skillSlots[i].Initialize(i, key);
            }

            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (deathOverlay != null) deathOverlay.SetActive(false);
            if (interactPrompt != null) interactPrompt.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            GameEventBus.ObjectiveTextChanged += OnObjectiveChanged;
            GameEventBus.DialogueRequested += OnDialogue;
        }

        private void OnDisable()
        {
            GameEventBus.ObjectiveTextChanged -= OnObjectiveChanged;
            GameEventBus.DialogueRequested -= OnDialogue;
            Unbind();
        }

        private void Update()
        {
            EnsureBound();
            UpdateDialogueTimer();
            UpdateInteractPrompt();

            _partyRefreshTimer -= Time.deltaTime;
            if (_partyRefreshTimer <= 0f)
            {
                _partyRefreshTimer = 0.5f;
                RefreshPartyFrames();
            }
        }

        // ---------------------------------------------------------------- 바인딩

        private void EnsureBound()
        {
            var player = ActorRegistry.LocalPlayer;
            if (player == _boundPlayer) return;

            Unbind();
            if (player == null) return;

            _boundPlayer = player;
            _health = player.GetComponent<Health>();
            _asc = player.GetComponent<AbilitySystemComponent>();
            _caster = player.GetComponent<SkillCaster>();
            _interactor = player.GetComponent<Character.PlayerInteractor>();

            if (_health != null)
            {
                _health.Changed += OnHealthChanged;
                _health.Died += OnDied;
                _health.Revived += OnRevived;
                OnHealthChanged(_health.Current, _health.Max);
            }

            if (_asc != null && _asc.HasAttributeSetForAttribute(ManaSet.ManaAttribute))
            {
                // GAS 어트리뷰트 변경 델리게이트 (언리얼 GetGameplayAttributeValueChangeDelegate).
                _asc.GetGameplayAttributeValueChangeDelegate(ManaSet.ManaAttribute).OnValueChanged += OnManaAttributeChanged;
                _asc.GetGameplayAttributeValueChangeDelegate(ManaSet.MaxManaAttribute).OnValueChanged += OnManaAttributeChanged;
                OnManaAttributeChanged(default);
            }

            if (_caster != null)
            {
                _caster.LoadoutChanged += OnLoadoutChanged;
                for (int i = 0; i < skillSlots.Length; i++)
                    if (skillSlots[i] != null) skillSlots[i].Bind(_caster);
            }
        }

        private void Unbind()
        {
            if (_health != null)
            {
                _health.Changed -= OnHealthChanged;
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }
            if (_asc != null && _asc.HasAttributeSetForAttribute(ManaSet.ManaAttribute))
            {
                _asc.GetGameplayAttributeValueChangeDelegate(ManaSet.ManaAttribute).OnValueChanged -= OnManaAttributeChanged;
                _asc.GetGameplayAttributeValueChangeDelegate(ManaSet.MaxManaAttribute).OnValueChanged -= OnManaAttributeChanged;
            }
            if (_caster != null) _caster.LoadoutChanged -= OnLoadoutChanged;

            _boundPlayer = null;
            _health = null;
            _asc = null;
            _caster = null;
            _interactor = null;
        }

        // ---------------------------------------------------------------- 자원 표시

        private void OnHealthChanged(float current, float max)
        {
            if (healthFill != null) healthFill.fillAmount = max > 0f ? current / max : 0f;
            if (healthText != null) healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        private void OnManaAttributeChanged(OnAttributeChangeData _)
        {
            if (_asc == null) return;
            OnManaChanged(_asc.GetNumericAttribute(ManaSet.ManaAttribute), _asc.GetNumericAttribute(ManaSet.MaxManaAttribute));
        }

        private void OnManaChanged(float current, float max)
        {
            if (manaFill != null) manaFill.fillAmount = max > 0f ? current / max : 0f;
            if (manaText != null) manaText.text = max > 0f ? $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}" : string.Empty;
        }

        private void OnLoadoutChanged()
        {
            for (int i = 0; i < skillSlots.Length; i++)
                if (skillSlots[i] != null) skillSlots[i].Refresh();
        }

        private void OnDied(DamageInfo _)
        {
            if (deathOverlay != null) deathOverlay.SetActive(true);
        }

        private void OnRevived()
        {
            if (deathOverlay != null) deathOverlay.SetActive(false);
        }

        // ---------------------------------------------------------------- 목표 / 대사

        private void OnObjectiveChanged(string text)
        {
            bool has = !string.IsNullOrEmpty(text);
            if (objectivePanel != null) objectivePanel.SetActive(has);
            if (objectiveText != null) objectiveText.text = text;
        }

        private void OnDialogue(string line, float duration)
        {
            if (dialogueText != null) dialogueText.text = line;
            if (dialoguePanel != null) dialoguePanel.SetActive(true);
            _dialogueHideTime = Time.time + Mathf.Max(1f, duration);
        }

        private void UpdateDialogueTimer()
        {
            if (dialoguePanel == null || !dialoguePanel.activeSelf) return;
            if (Time.time >= _dialogueHideTime) dialoguePanel.SetActive(false);
        }

        private void UpdateInteractPrompt()
        {
            if (interactPrompt == null) return;

            var focus = _interactor != null ? _interactor.Focus : null;
            bool show = focus != null;
            if (interactPrompt.gameObject.activeSelf != show) interactPrompt.gameObject.SetActive(show);
            if (show) interactPrompt.text = $"[G] {focus.Prompt}";
        }

        // ---------------------------------------------------------------- 파티 프레임

        private void RefreshPartyFrames()
        {
            if (partyFramePrefab == null || partyFrameParent == null) return;

            // 사라진 플레이어 정리
            var toRemove = new List<PlayerNetworkIdentity>();
            foreach (var pair in _partyFrames)
                if (pair.Key == null || !PlayerNetworkIdentity.All.Contains(pair.Key)) toRemove.Add(pair.Key);

            for (int i = 0; i < toRemove.Count; i++)
            {
                if (_partyFrames.TryGetValue(toRemove[i], out var frame) && frame != null) Destroy(frame.gameObject);
                _partyFrames.Remove(toRemove[i]);
            }

            // 새로 들어온 플레이어 추가 (자기 자신은 메인 HUD 에 나오므로 제외)
            for (int i = 0; i < PlayerNetworkIdentity.All.Count; i++)
            {
                var identity = PlayerNetworkIdentity.All[i];
                if (identity == null || identity.IsOwner) continue;
                if (_partyFrames.ContainsKey(identity)) continue;

                var frame = Instantiate(partyFramePrefab, partyFrameParent);
                frame.Bind(identity);
                _partyFrames[identity] = frame;
            }
        }
    }
}
