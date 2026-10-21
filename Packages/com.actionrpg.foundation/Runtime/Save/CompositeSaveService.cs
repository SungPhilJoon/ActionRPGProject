using Cysharp.Threading.Tasks;
using UnityEngine;

namespace ActionRPG.Core.Save
{
    /// <summary>
    /// 1차 백엔드(보통 클라우드)를 시도하고, 실패하면 2차 백엔드(보통 로컬)로 폴백한다.
    /// 저장은 항상 "1차 성공 여부와 무관하게" 2차에도 같이 쓴다 — 오프라인일 때도
    /// 마지막 상태를 로컬에 남겨 두기 위해서다. 클라우드가 준비되면
    /// <c>SaveManager.Configure(new CompositeSaveService(cloud, local))</c> 한 줄로 켤 수 있다.
    /// </summary>
    public sealed class CompositeSaveService : ISaveService
    {
        private readonly ISaveService _primary;
        private readonly ISaveService _fallback;

        public CompositeSaveService(ISaveService primary, ISaveService fallback)
        {
            _primary = primary;
            _fallback = fallback;
        }

        public async UniTask<SaveResult> SaveAsync<T>(string slot, T data)
        {
            SaveResult primaryResult;
            try { primaryResult = await _primary.SaveAsync(slot, data); }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CompositeSaveService] 1차 백엔드 저장 실패, 폴백으로 전환: {e.Message}");
                primaryResult = SaveResult.IoError;
            }

            var fallbackResult = await _fallback.SaveAsync(slot, data);
            return primaryResult == SaveResult.Success ? SaveResult.Success : fallbackResult;
        }

        public async UniTask<SaveLoadResult<T>> LoadAsync<T>(string slot)
        {
            try
            {
                var primaryResult = await _primary.LoadAsync<T>(slot);
                if (primaryResult.Success) return primaryResult;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CompositeSaveService] 1차 백엔드 로드 실패, 폴백으로 전환: {e.Message}");
            }

            return await _fallback.LoadAsync<T>(slot);
        }

        public async UniTask<bool> ExistsAsync(string slot)
        {
            try { if (await _primary.ExistsAsync(slot)) return true; }
            catch { /* 폴백으로 넘어간다 */ }
            return await _fallback.ExistsAsync(slot);
        }

        public async UniTask DeleteAsync(string slot)
        {
            try { await _primary.DeleteAsync(slot); } catch { /* 폴백은 그래도 지운다 */ }
            await _fallback.DeleteAsync(slot);
        }
    }
}
