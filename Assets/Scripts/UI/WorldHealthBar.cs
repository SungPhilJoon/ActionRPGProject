using ActionRPG.Combat;
using ActionRPG.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionRPG.UI
{
    /// <summary>
    /// 적 머리 위에 붙는 체력바. 카메라를 향해 돌고, 가득 찼거나 거리가 멀면 숨는다.
    /// 월드 스페이스 Canvas 안에 둔다.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [SerializeField] private Health target;
        [SerializeField] private Image fill;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("표시 조건")]
        [Tooltip("이 거리보다 멀면 숨긴다.")]
        [SerializeField] private float maxVisibleDistance = 30f;
        [Tooltip("체력이 가득 찼을 때도 계속 보일지.")]
        [SerializeField] private bool hideWhenFull = true;
        [SerializeField] private float fadeSpeed = 6f;

        private Transform _cameraTransform;
        private float _targetAlpha;

        private void Awake()
        {
            if (target == null) target = GetComponentInParent<Health>();
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (target == null) return;
            target.Changed += OnHealthChanged;
            target.Died += OnDied;
            OnHealthChanged(target.Current, target.Max);

            if (nameText != null)
            {
                var brain = target.GetComponent<AI.EnemyBrain>();
                nameText.text = brain != null && brain.Definition != null ? brain.Definition.DisplayName : string.Empty;
            }
        }

        private void OnDisable()
        {
            if (target == null) return;
            target.Changed -= OnHealthChanged;
            target.Died -= OnDied;
        }

        private void OnHealthChanged(float current, float max)
        {
            if (fill != null) fill.fillAmount = max > 0f ? current / max : 0f;
            _targetAlpha = ShouldShow(current, max) ? 1f : 0f;
        }

        private void OnDied(DamageInfo _) => _targetAlpha = 0f;

        private bool ShouldShow(float current, float max)
        {
            if (target == null || !target.IsAlive) return false;
            if (hideWhenFull && max > 0f && current >= max - 0.01f) return false;
            return true;
        }

        private void LateUpdate()
        {
            if (_cameraTransform == null)
            {
                var rig = Character.PlayerCameraRig.Instance;
                if (rig != null && rig.Camera != null) _cameraTransform = rig.Camera.transform;
                else if (Camera.main != null) _cameraTransform = Camera.main.transform;
            }

            if (_cameraTransform == null) return;

            // 카메라를 향해 세운다.
            transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position, Vector3.up);

            float distance = Vector3.Distance(transform.position, _cameraTransform.position);
            float wanted = distance > maxVisibleDistance ? 0f : _targetAlpha;

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, wanted, fadeSpeed * Time.deltaTime);
        }
    }
}
