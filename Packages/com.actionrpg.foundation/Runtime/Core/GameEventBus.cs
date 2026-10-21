using System;
using UnityEngine;

namespace ActionRPG.Core
{
    /// <summary>
    /// 시스템 간 느슨한 연결을 위한 전역 이벤트 허브.
    /// UI / 튜토리얼 / 사운드처럼 듣기만 하는 쪽이 게임플레이 코드를 직접 참조하지 않게 한다.
    /// 구독자는 반드시 OnDisable 에서 해제할 것.
    /// </summary>
    public static class GameEventBus
    {
        // 전투
        public static event Action<GameObject, DamageInfo> DamageDealt;
        public static event Action<GameObject, DamageInfo> EntityDied;
        public static event Action<GameObject, float, float> HealthChanged;   // target, current, max

        // 플레이어 행동 (튜토리얼 완료 조건 판정에 사용)
        public static event Action<GameObject, WeaponType> PlayerAttacked;
        public static event Action<GameObject, int> PlayerSkillCast;          // player, slotIndex
        public static event Action<GameObject> PlayerDodged;
        public static event Action<GameObject> PlayerJumped;
        public static event Action<GameObject, WeaponType> PlayerWeaponChanged;
        public static event Action<GameObject, IInteractable> PlayerInteracted;

        // 진행
        public static event Action<string> DungeonRoomStarted;
        public static event Action<string> DungeonRoomCleared;
        public static event Action<bool> DungeonFinished;                     // success
        public static event Action<string> ObjectiveTextChanged;
        public static event Action<string, float> DialogueRequested;          // line, duration

        public static void RaiseDamageDealt(GameObject target, DamageInfo info) => DamageDealt?.Invoke(target, info);
        public static void RaiseEntityDied(GameObject target, DamageInfo info) => EntityDied?.Invoke(target, info);
        public static void RaiseHealthChanged(GameObject target, float current, float max) => HealthChanged?.Invoke(target, current, max);

        public static void RaisePlayerAttacked(GameObject player, WeaponType weapon) => PlayerAttacked?.Invoke(player, weapon);
        public static void RaisePlayerSkillCast(GameObject player, int slot) => PlayerSkillCast?.Invoke(player, slot);
        public static void RaisePlayerDodged(GameObject player) => PlayerDodged?.Invoke(player);
        public static void RaisePlayerJumped(GameObject player) => PlayerJumped?.Invoke(player);
        public static void RaisePlayerWeaponChanged(GameObject player, WeaponType weapon) => PlayerWeaponChanged?.Invoke(player, weapon);
        public static void RaisePlayerInteracted(GameObject player, IInteractable target) => PlayerInteracted?.Invoke(player, target);

        public static void RaiseDungeonRoomStarted(string roomId) => DungeonRoomStarted?.Invoke(roomId);
        public static void RaiseDungeonRoomCleared(string roomId) => DungeonRoomCleared?.Invoke(roomId);
        public static void RaiseDungeonFinished(bool success) => DungeonFinished?.Invoke(success);
        public static void RaiseObjectiveTextChanged(string text) => ObjectiveTextChanged?.Invoke(text);
        public static void RaiseDialogue(string line, float duration) => DialogueRequested?.Invoke(line, duration);

        /// <summary>Domain Reload 를 꺼 둔 에디터에서 이전 플레이의 구독이 남지 않도록 초기화한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DamageDealt = null;
            EntityDied = null;
            HealthChanged = null;
            PlayerAttacked = null;
            PlayerSkillCast = null;
            PlayerDodged = null;
            PlayerJumped = null;
            PlayerWeaponChanged = null;
            PlayerInteracted = null;
            DungeonRoomStarted = null;
            DungeonRoomCleared = null;
            DungeonFinished = null;
            ObjectiveTextChanged = null;
            DialogueRequested = null;
        }
    }
}
