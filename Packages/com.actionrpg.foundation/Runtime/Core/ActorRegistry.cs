using System.Collections.Generic;
using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 살아 있는 플레이어/적을 추적하는 가벼운 런타임 인덱스.
    /// 매 프레임 FindObjectsOfType 을 돌리지 않기 위한 것이며, 등록은 각 액터가 스스로 한다.
    /// </summary>
    public static class ActorRegistry
    {
        private static readonly List<IDamageable> All = new List<IDamageable>(64);
        private static readonly List<IDamageable> Players = new List<IDamageable>(GameConstants.MaxPlayers);
        private static readonly List<IDamageable> Enemies = new List<IDamageable>(64);

        public static IReadOnlyList<IDamageable> AllActors => All;
        public static IReadOnlyList<IDamageable> PlayerActors => Players;
        public static IReadOnlyList<IDamageable> EnemyActors => Enemies;

        /// <summary>
        /// 로컬 클라이언트가 조종하는 플레이어. 없으면 null.
        /// </summary>
        public static GameObject LocalPlayer { get; private set; }

        /// <summary>로컬 클라이언트가 소유한 플레이어를 등록한다.</summary>
        public static void SetLocalPlayer(GameObject player) => LocalPlayer = player;

        /// <summary>player 가 현재 로컬 플레이어일 때만 해제한다.</summary>
        public static void ClearLocalPlayer(GameObject player)
        {
            if (LocalPlayer == player) LocalPlayer = null;
        }

        public static void Register(IDamageable actor)
        {
            if (actor == null || All.Contains(actor)) return;
            All.Add(actor);
            if (actor.Faction == Faction.Player) Players.Add(actor);
            else if (actor.Faction == Faction.Enemy) Enemies.Add(actor);
        }

        public static void Unregister(IDamageable actor)
        {
            if (actor == null) return;
            All.Remove(actor);
            Players.Remove(actor);
            Enemies.Remove(actor);
            if (LocalPlayer != null && actor.Transform != null && LocalPlayer == actor.Transform.gameObject)
                LocalPlayer = null;
        }

        /// <summary>origin 기준 radius 안에서 faction 에 적대적인 대상을 모은다.</summary>
        public static void QueryHostiles(Vector3 origin, float radius, Faction faction, List<IDamageable> results)
        {
            results.Clear();
            float sqr = radius * radius;
            var source = faction == Faction.Player ? Enemies : faction == Faction.Enemy ? Players : All;
            for (int i = 0; i < source.Count; i++)
            {
                var actor = source[i];
                if (actor == null || !actor.IsAlive || actor.Transform == null) continue;
                if (!faction.IsHostileTo(actor.Faction)) continue;
                if ((actor.Transform.position - origin).sqrMagnitude <= sqr) results.Add(actor);
            }
        }

        public static IDamageable FindNearestHostile(Vector3 origin, float radius, Faction faction)
        {
            IDamageable best = null;
            float bestSqr = radius * radius;
            var source = faction == Faction.Player ? Enemies : faction == Faction.Enemy ? Players : All;
            for (int i = 0; i < source.Count; i++)
            {
                var actor = source[i];
                if (actor == null || !actor.IsAlive || actor.Transform == null) continue;
                if (!faction.IsHostileTo(actor.Faction)) continue;
                float sqr = (actor.Transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = actor;
                }
            }
            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            All.Clear();
            Players.Clear();
            Enemies.Clear();
            LocalPlayer = null;
        }
    }
}
