using Cysharp.Threading.Tasks;

namespace ActionRPG.Core.Save
{
    /// <summary>
    /// 게임 코드가 실제로 부르는 창구. 백엔드는 <see cref="Configure"/> 로 갈아 끼운다.
    /// 기본값은 로컬 파일 저장이며, 클라우드가 준비되면 부트스트랩 코드에서
    ///   SaveManager.Configure(new CompositeSaveService(new CloudSaveServiceStub(), new LocalFileSaveService()));
    /// 한 줄만 바꾸면 된다.
    /// </summary>
    public static class SaveManager
    {
        private static ISaveService _service = new LocalFileSaveService();

        public static void Configure(ISaveService service) => _service = service ?? new LocalFileSaveService();

        public static UniTask<SaveResult> SaveAsync<T>(string slot, T data) => _service.SaveAsync(slot, data);
        public static UniTask<SaveLoadResult<T>> LoadAsync<T>(string slot) => _service.LoadAsync<T>(slot);
        public static UniTask<bool> ExistsAsync(string slot) => _service.ExistsAsync(slot);
        public static UniTask DeleteAsync(string slot) => _service.DeleteAsync(slot);
    }
}
