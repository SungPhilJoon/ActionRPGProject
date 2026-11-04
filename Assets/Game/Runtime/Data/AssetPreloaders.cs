using Cysharp.Threading.Tasks;
using Framework;
using UnityEngine;

/// <summary>AssetTable 행을 미리 로드한다 (EAssetKind 마다 한 클래스 = 로드할 Unity 타입, 2026-11-01 switch → 다형성).</summary>
public abstract class AssetPreloader
{
    public abstract UniTask Preload(AssetLoader InLoader, string InAddress, Object InOwner);
}

/// <summary>T 타입으로 비동기 로드.</summary>
public abstract class AssetPreloader<T> : AssetPreloader where T : Object
{
    public override UniTask Preload(AssetLoader InLoader, string InAddress, Object InOwner)
    {
        return InLoader.LoadAsync<T>(InAddress, InOwner).AsUniTask();
    }
}

[Kind(EAssetKind.Prefab)]
public sealed class PrefabPreloader : AssetPreloader<GameObject>
{
}

[Kind(EAssetKind.Sprite)]
public sealed class SpritePreloader : AssetPreloader<Sprite>
{
}

[Kind(EAssetKind.Material)]
public sealed class MaterialPreloader : AssetPreloader<Material>
{
}

[Kind(EAssetKind.Data)]
public sealed class DataPreloader : AssetPreloader<ScriptableObject>
{
}
