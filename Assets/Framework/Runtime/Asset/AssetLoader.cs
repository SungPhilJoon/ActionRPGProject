using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Framework
{
    /// <summary>
    /// Addressables 로딩 래퍼.
    ///
    /// 1) 같은 주소는 핸들을 하나만 만들고 참조 카운트로 관리한다 (중복 로드/중복 Release 방지).
    /// 2) 비동기 로드는 "요청자(Owner)" 를 함께 받는다. 로드가 끝났을 때 요청자가 이미 파괴됐으면
    ///    결과를 넘기지 않고 즉시 참조를 반납한다. 파괴된 오브젝트에 결과를 꽂는 댕글링 참조를 막기 위함이다.
    /// 3) 로드 직후 바로 써야 하는(같은 프레임에 반드시 존재해야 하는) 경우는 LoadSync 를 쓴다.
    ///    → 자세한 배경은 Documentation/VRising/Issues.md 의 "비동기 로드 레이스" 항목.
    /// </summary>
    public class AssetLoader : IManager
    {
        private class Entry
        {
            public AsyncOperationHandle Handle;
            public int RefCount;
        }

        private readonly Dictionary<long, Entry> Entries = new Dictionary<long, Entry>();

        void IManager.Initialize()
        {
        }

        void IManager.UnInitialize()
        {
            foreach (var pair in Entries)
            {
                if (pair.Value.Handle.IsValid() == true)
                {
                    Addressables.Release(pair.Value.Handle);
                }
            }

            Entries.Clear();
        }

        void IManager.Update(float InDeltaTime)
        {
        }

        public bool IsLoaded<T>(string InAddress) where T : Object
        {
            Entry entry;
            if (Entries.TryGetValue(makeKey<T>(InAddress), out entry) == false)
            {
                return false;
            }

            return entry.Handle.IsDone == true && entry.Handle.Status == AsyncOperationStatus.Succeeded;
        }

        /// <summary>동기 로드. 이미 로드 중인 핸들이 있으면 그 핸들의 완료를 기다린다.</summary>
        public T LoadSync<T>(string InAddress) where T : Object
        {
            if (string.IsNullOrEmpty(InAddress) == true)
            {
                return null;
            }

            var entry = acquire<T>(InAddress);
            if (entry == null)
            {
                return null;
            }

            if (entry.Handle.IsDone == false)
            {
                entry.Handle.WaitForCompletion();
            }

            return getResult<T>(InAddress, entry);
        }

        /// <summary>
        /// 비동기 로드. InOwner 가 로드 도중 파괴되면 null 을 돌려주고 참조를 반납한다.
        /// InOwner 가 null 이면 소유자 검사 없이 결과를 돌려준다 (매니저 같은 수명이 긴 요청자용).
        /// </summary>
        public async UniTask<T> LoadAsync<T>(string InAddress, Object InOwner, CancellationToken InToken = default) where T : Object
        {
            if (string.IsNullOrEmpty(InAddress) == true)
            {
                return null;
            }

            bool hasOwner = InOwner != null;
            var entry = acquire<T>(InAddress);
            if (entry == null)
            {
                return null;
            }

            if (entry.Handle.IsDone == false)
            {
                try
                {
                    await entry.Handle.Task.AsUniTask().AttachExternalCancellation(InToken);
                }
                catch (OperationCanceledException)
                {
                    Release<T>(InAddress);
                    return null;
                }
            }

            if (hasOwner == true && InOwner == null)
            {
                Release<T>(InAddress);
                return null;
            }

            return getResult<T>(InAddress, entry);
        }

        /// <summary>콜백 버전. 코루틴/UniTask 를 쓰지 않는 곳에서 사용한다.</summary>
        public void LoadAsync<T>(string InAddress, Object InOwner, Action<T> InOnLoaded) where T : Object
        {
            loadWithCallback(InAddress, InOwner, InOnLoaded).Forget();
        }

        public void Release<T>(string InAddress) where T : Object
        {
            long key = makeKey<T>(InAddress);
            Entry entry;
            if (Entries.TryGetValue(key, out entry) == false)
            {
                return;
            }

            entry.RefCount -= 1;
            if (entry.RefCount > 0)
            {
                return;
            }

            if (entry.Handle.IsValid() == true)
            {
                Addressables.Release(entry.Handle);
            }

            Entries.Remove(key);
        }

        private async UniTaskVoid loadWithCallback<T>(string InAddress, Object InOwner, Action<T> InOnLoaded) where T : Object
        {
            var result = await LoadAsync<T>(InAddress, InOwner);
            if (result == null)
            {
                return;
            }

            InOnLoaded?.Invoke(result);
        }

        /// <summary>같은 주소라도 타입이 다르면(Texture2D vs Sprite) 다른 결과이므로 (주소, 타입) 을 키로 쓴다.</summary>
        private static long makeKey<T>(string InAddress) where T : Object
        {
            return ((long)HashUtil.Fnv1a(InAddress) << 32) | (uint)HashUtil.Fnv1a(typeof(T).Name);
        }

        private Entry acquire<T>(string InAddress) where T : Object
        {
            long key = makeKey<T>(InAddress);
            Entry entry;
            if (Entries.TryGetValue(key, out entry) == true)
            {
                entry.RefCount += 1;
                return entry;
            }

            AsyncOperationHandle<T> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<T>(InAddress);
            }
            catch (Exception e)
            {
                Log.Error($"AssetLoader: load failed {InAddress} ({e.Message})");
                return null;
            }

            entry = new Entry();
            entry.Handle = handle;
            entry.RefCount = 1;
            Entries.Add(key, entry);
            return entry;
        }

        private T getResult<T>(string InAddress, Entry InEntry) where T : Object
        {
            if (InEntry.Handle.Status != AsyncOperationStatus.Succeeded)
            {
                Log.Error($"AssetLoader: '{InAddress}' failed. {InEntry.Handle.OperationException}");
                Release<T>(InAddress);
                return null;
            }

            return InEntry.Handle.Result as T;
        }
    }
}
