using System;
using ActionRPG.Core;
using UnityEngine;

namespace ActionRPG.Skills
{
    /// <summary>
    /// 로컬 플레이어가 고른 덱의 저장/불러오기.
    /// 게임 입장 시 이 값이 접속 페이로드에 실려 서버로 간다.
    /// </summary>
    public static class LoadoutStorage
    {
        private const string PrefsKey = "ActionRPG.Loadout";
        private const string NameKey = "ActionRPG.PlayerName";

        [Serializable]
        private struct SerializedLoadout
        {
            public ushort s0, s1, s2, s3;
        }

        private static SkillLoadout? _cached;

        /// <summary>저장된 덱. 없으면 데이터베이스 앞쪽 스킬로 기본 덱을 만들어 준다.</summary>
        public static SkillLoadout Current
        {
            get
            {
                if (_cached.HasValue) return _cached.Value;
                _cached = Load();
                return _cached.Value;
            }
        }

        public static string PlayerName
        {
            get
            {
                string saved = PlayerPrefs.GetString(NameKey, string.Empty);
                return string.IsNullOrWhiteSpace(saved) ? "모험가" : saved;
            }
            set
            {
                PlayerPrefs.SetString(NameKey, string.IsNullOrWhiteSpace(value) ? "모험가" : value.Trim());
                PlayerPrefs.Save();
            }
        }

        public static void Save(in SkillLoadout loadout)
        {
            var data = new SerializedLoadout { s0 = loadout[0], s1 = loadout[1], s2 = loadout[2], s3 = loadout[3] };
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            _cached = loadout;

            // PlayerPrefs 는 UI 가 즉시 읽을 값이고, 이건 그 위에 얹는 "진짜" 영구 저장(비동기, 클라우드 이전 대비).
            ActionRPG.Save.PlayerProfileService.SaveCurrentAsync();
        }

        private static SkillLoadout Load()
        {
            string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var data = JsonUtility.FromJson<SerializedLoadout>(json);
                    var loadout = new SkillLoadout { Slot0 = data.s0, Slot1 = data.s1, Slot2 = data.s2, Slot3 = data.s3 };

                    var db = SkillDatabase.Instance;
                    if (db == null || db.Validate(loadout, out _)) return loadout;

                    Debug.LogWarning("[LoadoutStorage] 저장된 덱이 현재 스킬 목록과 맞지 않아 기본 덱으로 되돌립니다.");
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LoadoutStorage] 덱 불러오기 실패: {e.Message}");
                }
            }
            return BuildDefault();
        }

        /// <summary>데이터베이스에서 앞 4개를 뽑아 최소한 놀 수 있는 덱을 만든다.</summary>
        public static SkillLoadout BuildDefault()
        {
            var loadout = new SkillLoadout();
            var db = SkillDatabase.Instance;
            if (db == null) return loadout;

            int slot = 0;
            var all = db.All;
            for (int i = 0; i < all.Count && slot < GameConstants.SkillSlotCount; i++)
            {
                if (all[i] == null) continue;
                loadout[slot++] = all[i].Id;
            }
            return loadout;
        }

        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            _cached = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _cached = null;
    }
}
