using System;
using System.Collections.Generic;
using ActionRPG.Skills;

namespace ActionRPG.Networking
{
    /// <summary>접속 승인 시 클라이언트가 보내는 정보. JSON 으로 직렬화되어 페이로드에 실린다.</summary>
    [Serializable]
    public class ConnectionPayload
    {
        public string playerName = "모험가";
        public string buildVersion = "";
        public ushort slot0;
        public ushort slot1;
        public ushort slot2;
        public ushort slot3;

        public SkillLoadout ToLoadout() => new SkillLoadout { Slot0 = slot0, Slot1 = slot1, Slot2 = slot2, Slot3 = slot3 };

        public static ConnectionPayload FromLoadout(string name, in SkillLoadout loadout, string version)
        {
            return new ConnectionPayload
            {
                playerName = string.IsNullOrWhiteSpace(name) ? "모험가" : name.Trim(),
                buildVersion = version,
                slot0 = loadout[0],
                slot1 = loadout[1],
                slot2 = loadout[2],
                slot3 = loadout[3]
            };
        }
    }

    /// <summary>서버가 클라이언트마다 보관하는 정보.</summary>
    public class PlayerSessionData
    {
        public ulong ClientId;
        public string PlayerName;
        public SkillLoadout Loadout;
        public bool IsReady;
        /// <summary>재접속 판별용. 같은 사람이 끊겼다 돌아오면 덱을 다시 물어보지 않는다.</summary>
        public string SessionKey;
    }

    /// <summary>
    /// 서버가 들고 있는 접속자 목록. 스폰 시점에 어떤 덱을 적용할지 여기서 찾는다.
    /// 씬이 바뀌어도 살아남아야 하므로 정적 저장소로 둔다.
    /// </summary>
    public static class SessionManager
    {
        private static readonly Dictionary<ulong, PlayerSessionData> Sessions = new Dictionary<ulong, PlayerSessionData>();
        private static readonly Dictionary<string, PlayerSessionData> ByKey = new Dictionary<string, PlayerSessionData>();

        public static IReadOnlyDictionary<ulong, PlayerSessionData> All => Sessions;
        public static int Count => Sessions.Count;

        public static event Action<PlayerSessionData> PlayerJoined;
        public static event Action<ulong> PlayerLeft;
        public static event Action ReadyStateChanged;

        public static PlayerSessionData Register(ulong clientId, ConnectionPayload payload, string sessionKey)
        {
            // 재접속이면 이전 데이터를 살린다.
            if (!string.IsNullOrEmpty(sessionKey) && ByKey.TryGetValue(sessionKey, out var existing))
            {
                Sessions.Remove(existing.ClientId);
                existing.ClientId = clientId;
                Sessions[clientId] = existing;
                PlayerJoined?.Invoke(existing);
                return existing;
            }

            var data = new PlayerSessionData
            {
                ClientId = clientId,
                PlayerName = payload?.playerName ?? "모험가",
                Loadout = payload?.ToLoadout() ?? default,
                SessionKey = sessionKey
            };

            Sessions[clientId] = data;
            if (!string.IsNullOrEmpty(sessionKey)) ByKey[sessionKey] = data;

            PlayerJoined?.Invoke(data);
            return data;
        }

        public static void Unregister(ulong clientId)
        {
            if (!Sessions.Remove(clientId)) return;
            PlayerLeft?.Invoke(clientId);
        }

        public static bool TryGet(ulong clientId, out PlayerSessionData data) => Sessions.TryGetValue(clientId, out data);

        public static PlayerSessionData Get(ulong clientId) => Sessions.TryGetValue(clientId, out var data) ? data : null;

        public static void SetReady(ulong clientId, bool ready)
        {
            if (!Sessions.TryGetValue(clientId, out var data)) return;
            if (data.IsReady == ready) return;
            data.IsReady = ready;
            ReadyStateChanged?.Invoke();
        }

        public static bool AllReady()
        {
            if (Sessions.Count == 0) return false;
            foreach (var pair in Sessions)
                if (!pair.Value.IsReady) return false;
            return true;
        }

        public static void Clear()
        {
            Sessions.Clear();
            ByKey.Clear();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Sessions.Clear();
            ByKey.Clear();
            PlayerJoined = null;
            PlayerLeft = null;
            ReadyStateChanged = null;
        }
    }
}
