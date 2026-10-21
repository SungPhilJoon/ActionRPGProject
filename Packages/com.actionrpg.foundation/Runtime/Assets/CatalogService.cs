using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionRPG.Core.AddressableAssets
{
    /// <summary>게임 코드가 실제로 부르는 창구. 백엔드는 <see cref="Configure"/> 로 갈아 끼운다.</summary>
    public static class CatalogService
    {
        private static IAssetCatalog _catalog = new AddressableAssetCatalog();

        public static void Configure(IAssetCatalog catalog) => _catalog = catalog ?? new AddressableAssetCatalog();

        public static UniTask<AssetHandle<T>> LoadAsync<T>(string key) where T : Object => _catalog.LoadAsync<T>(key);
        public static UniTask<AssetHandle<GameObject>> InstantiateAsync(string key, Transform parent = null, bool worldPositionStays = false)
            => _catalog.InstantiateAsync(key, parent, worldPositionStays);
        public static UniTask PreloadLabelAsync(string label) => _catalog.PreloadLabelAsync(label);
        public static UniTask<bool> LoadAdditionalCatalogAsync(string catalogPath) => _catalog.LoadAdditionalCatalogAsync(catalogPath);
    }
}
