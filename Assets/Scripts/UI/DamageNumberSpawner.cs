using System.Collections.Generic;
using ActionRPG.Core;
using TMPro;
using UnityEngine;

namespace ActionRPG.UI
{

    /// <summary>
    /// 피해 이벤트를 듣고 월드에 숫자를 띄운다. 씬에 하나만 두면 된다.
    /// 간단한 오브젝트 풀을 내장해 전투 중 GC 를 만들지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class DamageNumberSpawner : MonoBehaviour
    {
        [SerializeField] private DamageNumber numberPrefab;
        [SerializeField] private int poolSize = 32;

        [Header("색")]
        [SerializeField] private Color enemyDamageColor = new Color(1f, 0.92f, 0.5f);
        [SerializeField] private Color criticalColor = new Color(1f, 0.55f, 0.2f);
        [SerializeField] private Color playerDamageColor = new Color(1f, 0.35f, 0.35f);

        [Header("표시 조건")]
        [Tooltip("이 거리보다 먼 피해는 표시하지 않는다.")]
        [SerializeField] private float maxDistance = 35f;

        private readonly List<DamageNumber> _pool = new List<DamageNumber>(32);
        private Transform _cameraTransform;

        private void Awake()
        {
            if (numberPrefab == null) return;
            for (int i = 0; i < poolSize; i++)
            {
                var instance = Instantiate(numberPrefab, transform);
                instance.gameObject.SetActive(false);
                _pool.Add(instance);
            }
        }

        private void OnEnable() => GameEventBus.DamageDealt += OnDamageDealt;
        private void OnDisable() => GameEventBus.DamageDealt -= OnDamageDealt;

        private void OnDamageDealt(GameObject target, DamageInfo info)
        {
            if (target == null || info.Amount <= 0f) return;

            var cameraTransform = ResolveCamera();
            Vector3 position = info.HitPoint != Vector3.zero ? info.HitPoint : target.transform.position + Vector3.up * 1.6f;

            if (cameraTransform != null && Vector3.Distance(position, cameraTransform.position) > maxDistance) return;

            var number = Rent();
            if (number == null) return;

            var damageable = target.GetComponent<IDamageable>();
            bool onPlayer = damageable != null && damageable.Faction == Faction.Player;
            Color color = info.IsCritical ? criticalColor : onPlayer ? playerDamageColor : enemyDamageColor;

            number.transform.position = position;
            number.Play(info.Amount, info.IsCritical, color, cameraTransform);
        }

        private Transform ResolveCamera()
        {
            if (_cameraTransform != null) return _cameraTransform;

            var rig = Character.PlayerCameraRig.Instance;
            if (rig != null && rig.Camera != null) _cameraTransform = rig.Camera.transform;
            else if (Camera.main != null) _cameraTransform = Camera.main.transform;
            return _cameraTransform;
        }

        private DamageNumber Rent()
        {
            for (int i = 0; i < _pool.Count; i++)
                if (!_pool[i].gameObject.activeSelf) return _pool[i];

            // 풀이 부족하면 가장 오래된 것을 재활용한다.
            return _pool.Count > 0 ? _pool[0] : null;
        }
    }
}
