using ActionRPG.Core.Lifecycle;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ActionRPG.Core.AddressableAssets
{
    /// <summary>
    /// Addressables 의 <see cref="AsyncOperationHandle{T}"/> 를 감싼 얇은 값 타입.
    /// 호출부가 Addressables API 를 직접 몰라도 되게 하고, 해제를 잊지 않도록
    /// LifecycleTracker 에 자동으로 등록/해제한다 — 나중에 Lifecycle Tracker 창을 열어 보면
    /// "로드했는데 안 지운" 에셋 핸들이 몇 개인지 바로 보인다.
    /// </summary>
    public readonly struct AssetHandle<T>
    {
        public readonly AsyncOperationHandle<T> Raw;
        public readonly LifecycleHandle Lifecycle;

        public T Result => Raw.Result;
        public bool IsValid => Raw.IsValid();

        internal AssetHandle(AsyncOperationHandle<T> raw, LifecycleHandle lifecycle)
        {
            Raw = raw;
            Lifecycle = lifecycle;
        }

        /// <summary>이 핸들이 참조하는 에셋(과 참조 카운트)을 반환한다. 로드한 만큼 반드시 호출할 것.</summary>
        public void Release()
        {
            Lifecycle.Dispose();
            if (Raw.IsValid()) UnityEngine.AddressableAssets.Addressables.Release(Raw);
        }
    }
}
