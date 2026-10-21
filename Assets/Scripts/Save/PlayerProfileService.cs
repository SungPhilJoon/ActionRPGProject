using ActionRPG.Core.Save;
using ActionRPG.Skills;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ActionRPG.Save
{
    /// <summary>
    /// LoadoutStorage(PlayerPrefs, 동기)와 SaveManager(Protobuf, 비동기)를 이어 주는 접착 계층.
    ///
    /// 왜 두 저장소를 같이 쓰는가:
    ///  - PlayerPrefs 는 메인 메뉴 UI 가 즉시 읽어야 하는 "마지막 이름/덱" 에 적합하다 (동기, 즉시 반영).
    ///  - PlayerProfileData(Protobuf)는 나중에 클라우드로 옮겨 갈 "진짜 세이브 데이터"이고,
    ///    UniTask 로 스레드풀에서 디스크 I/O 를 하므로 메인 스레드를 막지 않는다.
    ///
    /// 게임 흐름을 막지 않기 위해 저장은 fire-and-forget(.Forget())으로 호출한다 —
    /// 저장 실패는 로그로만 남기고 플레이를 방해하지 않는다. 반대로 불러오기는
    /// 명시적으로 await 해서 실패를 호출부가 알 수 있게 한다.
    /// </summary>
    public static class PlayerProfileService
    {
        private const string ProfileSlot = "player_profile";

        /// <summary>현재 이름/덱을 프로필로 비동기 저장한다. 실패해도 예외를 던지지 않는다.</summary>
        public static void SaveCurrentAsync()
        {
            SaveInternalAsync().Forget();
        }

        private static async UniTaskVoid SaveInternalAsync()
        {
            var profile = new PlayerProfileData
            {
                PlayerName = LoadoutStorage.PlayerName,
                LastLoadout = LoadoutStorage.Current,
                TotalPlaytimeSeconds = Time.realtimeSinceStartup
            };

            var result = await SaveManager.SaveAsync(ProfileSlot, profile);
            if (result != SaveResult.Success)
                Debug.LogWarning($"[PlayerProfileService] 프로필 저장 실패: {result}");
        }

        /// <summary>
        /// 저장된 프로필을 불러와 LoadoutStorage 에도 반영한다.
        /// 게임 부팅 시(예: Boot 씬 또는 메인 메뉴 Start) 한 번 호출하면 된다.
        /// </summary>
        public static async UniTask<PlayerProfileData> LoadAndApplyAsync()
        {
            var loadResult = await SaveManager.LoadAsync<PlayerProfileData>(ProfileSlot);
            if (!loadResult.Success)
            {
                if (loadResult.Result != SaveResult.NotFound)
                    Debug.LogWarning($"[PlayerProfileService] 프로필 로드 실패: {loadResult.Result}");
                return null;
            }

            var profile = loadResult.Value;
            if (!string.IsNullOrWhiteSpace(profile.PlayerName)) LoadoutStorage.PlayerName = profile.PlayerName;
            LoadoutStorage.Save(profile.LastLoadout);
            return profile;
        }
    }
}
