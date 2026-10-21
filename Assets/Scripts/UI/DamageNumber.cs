using System.Collections.Generic;
using ActionRPG.Core;
using TMPro;
using UnityEngine;

namespace ActionRPG.UI
{
    /// <summary>
    /// 피해 숫자 하나. 위로 떠오르며 사라진다.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        [SerializeField] private TMP_Text text;
        [SerializeField] private float lifetime = 0.9f;
        [SerializeField] private float riseSpeed = 1.6f;
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private float _elapsed;
        private Transform _cameraTransform;
        private Vector3 _drift;

        public bool IsFinished => _elapsed >= lifetime;

        public void Play(float amount, bool critical, Color color, Transform cameraTransform)
        {
            _elapsed = 0f;
            _cameraTransform = cameraTransform;
            _drift = new Vector3(Random.Range(-0.4f, 0.4f), 0f, Random.Range(-0.4f, 0.4f));

            if (text == null) return;
            text.text = critical ? $"{Mathf.RoundToInt(amount)}!" : Mathf.RoundToInt(amount).ToString();
            text.color = color;
            text.fontSize = critical ? text.fontSize * 1.35f : text.fontSize;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            transform.position += (Vector3.up * riseSpeed + _drift) * Time.deltaTime;
            if (_cameraTransform != null)
                transform.rotation = Quaternion.LookRotation(transform.position - _cameraTransform.position, Vector3.up);

            if (text != null)
            {
                var color = text.color;
                color.a = alphaCurve.Evaluate(Mathf.Clamp01(_elapsed / lifetime));
                text.color = color;
            }

            if (IsFinished) gameObject.SetActive(false);
        }
    }
}
