using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ActionRPG.Core.AddressableAssets
{
    /// <summary>
    /// 에셋 로딩 백엔드 추상화. 지금은 Addressables 하나뿐이지만, 인터페이스로
    /// 감싸 둔 덕분에 게임 코드는 "키로 에셋을 얻는다"는 개념만 알면 되고,
    /// 테스트 환경에서는 Resources 기반 가짜 구현으로 바꿔 끼울 수도 있다.
    /// </summary>
    public interface IAssetCatalog
    {
        UniTask<AssetHandle<T>> LoadAsync<T>(string key) where T : Object;
        UniTask<AssetHandle<GameObject>> InstantiateAsync(string key, Transform parent = null, bool worldPositionStays = false);
        UniTask PreloadLabelAsync(string label);
        UniTask<bool> LoadAdditionalCatalogAsync(string catalogPath);
    }
}
