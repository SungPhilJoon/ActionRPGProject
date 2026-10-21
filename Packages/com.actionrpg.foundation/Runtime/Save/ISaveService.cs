using Cysharp.Threading.Tasks;

namespace ActionRPG.Core.Save
{
    public enum SaveResult : byte
    {
        Success = 0,
        NotFound = 1,
        Corrupted = 2,
        IoError = 3,
        VersionMismatch = 4
    }

    public readonly struct SaveLoadResult<T>
    {
        public readonly SaveResult Result;
        public readonly T Value;

        public bool Success => Result == SaveResult.Success;

        public SaveLoadResult(SaveResult result, T value)
        {
            Result = result;
            Value = value;
        }

        public static SaveLoadResult<T> Ok(T value) => new SaveLoadResult<T>(SaveResult.Success, value);
        public static SaveLoadResult<T> Fail(SaveResult result) => new SaveLoadResult<T>(result, default);
    }

    /// <summary>
    /// 세이브 백엔드 추상화. 지금은 <see cref="LocalFileSaveService"/> 하나뿐이지만,
    /// 나중에 클라우드 세이브(Unity Cloud Save, PlayFab, 자체 서버 등)를 붙일 때
    /// 이 인터페이스만 구현하면 게임 코드는 <see cref="SaveManager"/> 호출부를
    /// 전혀 바꿀 필요가 없다. 슬롯 이름(slot)은 "player", "settings", "dungeon_progress"
    /// 처럼 논리적인 저장 단위를 뜻하며, 실제 파일 경로/클라우드 키로의 변환은
    /// 구현체 내부 책임이다.
    ///
    /// 모든 메서드가 UniTask 인 이유: 로컬 구현은 스레드풀에서 파일 I/O 를 하고
    /// 메인 스레드로 복귀해야 하고(Unity API 접근 안전), 클라우드 구현은 당연히
    /// 네트워크 대기가 필요하기 때문이다. 호출부는 백엔드가 뭔지 몰라도 된다.
    /// </summary>
    public interface ISaveService
    {
        UniTask<SaveResult> SaveAsync<T>(string slot, T data);
        UniTask<SaveLoadResult<T>> LoadAsync<T>(string slot);
        UniTask<bool> ExistsAsync(string slot);
        UniTask DeleteAsync(string slot);
    }
}
