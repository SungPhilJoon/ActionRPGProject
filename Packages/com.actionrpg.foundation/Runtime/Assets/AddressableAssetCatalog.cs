using ActionRPG.Core.Lifecycle;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace ActionRPG.Core.AddressableAssets
{
    /// <summary>
    /// Addressables 기반 <see cref="IAssetCatalog"/> 구현.
    ///
    /// 로컬 전용으로 쓰는 방법: Addressables 그룹의 "Build & Load Paths" 를
    /// Local(빌드에 포함) 로 두면 별도 CDN 없이 프로젝트 자체에서 에셋 번들을 읽는다.
    /// 나중에 원격 CDN 으로 옮기고 싶으면 그룹 설정만 Remote 로 바꾸면 되고,
    /// 이 클래스(와 그 위의 게임 코드)는 전혀 손댈 필요가 없다 — 그게 Addressables 를
    /// 쓰는 핵심 이유다 (에셋의 "물리적 위치"와 "로딩 코드"가 분리된다).
    /// </summary>
    public sealed class AddressableAssetCatalog : IAssetCatalog
    {
        private bool _initialized;

        /// <summary>Addressables 런타임 데이터(기본 카탈로그, 원격 카탈로그 목록 등)를 초기화한다. 최초 1회만 실제로 기다린다.</summary>
        public async UniTask InitializeAsync()
        {
            if (_initialized) return;
            await Addressables.InitializeAsync().ToUniTask();
            _initialized = true;
        }

        public async UniTask<AssetHandle<T>> LoadAsync<T>(string key) where T : Object
        {
            await InitializeAsync();

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(key);
            await handle.ToUniTask();
            
            T result = handle.Result;

            var lifecycle = LifecycleTracker.Register(key, "Asset:" + typeof(T).Name, result);
            return new AssetHandle<T>(handle, lifecycle);
        }

        public async UniTask<AssetHandle<GameObject>> InstantiateAsync(string key, Transform parent = null, bool worldPositionStays = false)
        {
            await InitializeAsync();

            AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(key, parent, worldPositionStays);
            await handle.ToUniTask();
            
            GameObject result = handle.Result;

            var lifecycle = LifecycleTracker.Register(key, "Instance", result);
            return new AssetHandle<GameObject>(handle, lifecycle);
        }

        /// <summary>레이블에 묶인 에셋 번들을 미리 내려받아 캐시한다. 로딩 화면에서 호출.</summary>
        public async UniTask PreloadLabelAsync(string label)
        {
            await InitializeAsync();

            var sizeHandle = Addressables.GetDownloadSizeAsync(label);
            await sizeHandle.ToUniTask();            
            
            long size = sizeHandle.Result;
            
            Addressables.Release(sizeHandle);

            if (size <= 0) return; // 이미 로컬에 있거나 받을 게 없다.

            var downloadHandle = Addressables.DownloadDependenciesAsync(label);
            await downloadHandle.ToUniTask();
            
            Addressables.Release(downloadHandle);
        }

        /// <summary>
        /// 기본 카탈로그 외에 추가 카탈로그(JSON)를 얹는다 — 커스텀 카탈로그 확장 지점.
        ///
        /// 활용 예: StreamingAssets/DLC/catalog_1.0.0.json 처럼 별도로 빌드해 둔 콘텐츠 묶음을
        /// 게임 시작 후 조건부로 로드한다(예: 특정 이벤트 던전, 시즌 콘텐츠).
        /// 이렇게 하면 기본 빌드 크기를 늘리지 않고도 "로컬에 미리 넣어 둔 추가 에셋 번들"을
        /// 나중에 켤 수 있다 — 진짜 원격 서버 없이도 확장 가능한 구조라는 뜻이다.
        /// </summary>
        public async UniTask<bool> LoadAdditionalCatalogAsync(string catalogPath)
        {
            await InitializeAsync();

            var handle = Addressables.LoadContentCatalogAsync(catalogPath, false);
            try
            {
                await handle.ToUniTask();
                bool ok = handle.Status == AsyncOperationStatus.Succeeded;
                if (!ok) Debug.LogError($"[AddressableAssetCatalog] 카탈로그 로드 실패: {catalogPath}");
                return ok;
            }
            finally
            {
                Addressables.Release(handle);
            }
        }
    }
}
