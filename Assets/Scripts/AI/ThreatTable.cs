using System.Collections.Generic;
using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.AI
{
    /// <summary>
    /// 어그로 관리. 4인 파티에서 적이 누구를 때릴지 정하는 기준이다.
    /// 서버에서만 쓰인다.
    /// </summary>
    public class ThreatTable
    {
        private readonly Dictionary<IDamageable, float> _threat = new Dictionary<IDamageable, float>(GameConstants.MaxPlayers);
        private readonly List<IDamageable> _expired = new List<IDamageable>(4);
        private readonly List<IDamageable> _keys = new List<IDamageable>(GameConstants.MaxPlayers);

        /// <summary>초당 감소하는 위협 수치. 오래 때리지 않으면 어그로가 풀린다.</summary>
        public float DecayPerSecond = 2f;

        /// <summary>현재 대상을 유지하려는 관성. 새 1위가 이 배율만큼 앞서야 대상이 바뀐다.</summary>
        public float SwitchThreshold = 1.2f;

        public IDamageable Current { get; private set; }

        public void Add(IDamageable source, float amount)
        {
            if (source == null || amount <= 0f) return;
            _threat.TryGetValue(source, out float existing);
            _threat[source] = existing + amount;
        }

        /// <summary>도발. 즉시 1위로 만든다.</summary>
        public void Taunt(IDamageable source, float bonus = 100f)
        {
            if (source == null) return;
            float highest = 0f;
            foreach (var pair in _threat) highest = Mathf.Max(highest, pair.Value);
            _threat[source] = highest + bonus;
            Current = source;
        }

        public void Remove(IDamageable source)
        {
            if (source == null) return;
            _threat.Remove(source);
            if (ReferenceEquals(Current, source)) Current = null;
        }

        public void Clear()
        {
            _threat.Clear();
            Current = null;
        }

        public bool HasAny => _threat.Count > 0;

        /// <summary>매 프레임 호출. 감쇠 처리 후 대상을 갱신한다.</summary>
        public IDamageable Tick(float deltaTime, Vector3 selfPosition, float leashRadius, Vector3 homePosition)
        {
            _expired.Clear();
            _keys.Clear();
            foreach (var key in _threat.Keys) _keys.Add(key);

            IDamageable best = null;
            float bestThreat = 0f;

            for (int i = 0; i < _keys.Count; i++)
            {
                var actor = _keys[i];
                if (actor == null || !actor.IsAlive || actor.Transform == null)
                {
                    _expired.Add(actor);
                    continue;
                }

                // 리쉬 밖으로 도망간 대상은 목록에서 뺀다.
                if (leashRadius > 0f && Vector3.Distance(actor.Transform.position, homePosition) > leashRadius * 1.5f)
                {
                    _expired.Add(actor);
                    continue;
                }

                float value = Mathf.Max(0f, _threat[actor] - DecayPerSecond * deltaTime);
                if (value <= 0.01f)
                {
                    _expired.Add(actor);
                    continue;
                }

                _threat[actor] = value;
                if (value > bestThreat)
                {
                    bestThreat = value;
                    best = actor;
                }
            }

            for (int i = 0; i < _expired.Count; i++) Remove(_expired[i]);

            if (Current == null || !Current.IsAlive)
            {
                Current = best;
                return Current;
            }

            // 관성: 현재 대상보다 충분히 높아야 바꾼다.
            if (best != null && !ReferenceEquals(best, Current))
            {
                float currentThreat = _threat.TryGetValue(Current, out float v) ? v : 0f;
                if (bestThreat > currentThreat * SwitchThreshold) Current = best;
            }

            return Current;
        }
    }
}
