using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 레이어(int) 별 캔버스와 창(타입) 레지스트리.
    /// 캔버스 · 창 · 위젯은 모두 프리팹이다. 게임이 Addressables 주소(또는 로드한 프리팹)를 넘기면 Instantiate 해서 등록한다.
    /// </summary>
    public class UIManager : IManager
    {
        private readonly Dictionary<int, Canvas> Layers = new Dictionary<int, Canvas>();
        private readonly Dictionary<Type, UIWindow> Windows = new Dictionary<Type, UIWindow>();
        private Transform Root;

        void IManager.Initialize()
        {
            var root = new GameObject("[UI]");
            Root = root.transform;
        }

        void IManager.UnInitialize()
        {
            Windows.Clear();
            Layers.Clear();
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root.gameObject);
            }
        }

        void IManager.Update(float InDeltaTime)
        {
        }

        /// <summary>캔버스 프리팹을 Instantiate 해 레이어로 등록한다. 정렬 순서 = 레이어 번호.</summary>
        public Canvas RegistLayer(int InLayer, string InName, GameObject InCanvasPrefab)
        {
            Canvas canvas;
            if (Layers.TryGetValue(InLayer, out canvas) == true)
            {
                return canvas;
            }

            if (InCanvasPrefab == null)
            {
                Log.Error($"UIManager: canvas prefab missing for layer {InName}");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(InCanvasPrefab, Root, false);
            go.name = InName;
            canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                Log.Error($"UIManager: prefab {InCanvasPrefab.name} has no Canvas");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            canvas.sortingOrder = InLayer;
            Layers.Add(InLayer, canvas);
            return canvas;
        }

        public Canvas GetLayer(int InLayer)
        {
            Canvas canvas;
            Layers.TryGetValue(InLayer, out canvas);
            return canvas;
        }

        /// <summary>창 프리팹을 레이어 캔버스 아래에 Instantiate 하고 (기본은 닫힌 상태로) 등록한다.</summary>
        public T Create<T>(int InLayer, GameObject InPrefab, bool InOpen = false) where T : UIWindow
        {
            UIWindow existing;
            if (Windows.TryGetValue(typeof(T), out existing) == true)
            {
                return existing as T;
            }

            var window = Spawn<T>(InLayer, InPrefab);
            if (window == null)
            {
                return null;
            }

            window.Initialize();
            Windows.Add(typeof(T), window);
            window.gameObject.SetActive(InOpen);
            return window;
        }

        /// <summary>Addressables 주소로 창 프리팹을 로드한 뒤 Create. 로드 실패 시 null.</summary>
        public async UniTask<T> CreateAsync<T>(int InLayer, string InAddress, bool InOpen = false) where T : UIWindow
        {
            var prefab = await Managers.Get<AssetLoader>().LoadAsync<GameObject>(InAddress, null);
            if (prefab == null)
            {
                Log.Error($"UIManager: window prefab '{InAddress}' not loaded");
                return null;
            }

            return Create<T>(InLayer, prefab, InOpen);
        }

        /// <summary>창이 아닌 UI 프리팹(월드 레이어 등)을 레이어 아래에 Instantiate 한다. 레지스트리에는 넣지 않는다.</summary>
        public T Spawn<T>(int InLayer, GameObject InPrefab) where T : Component
        {
            var canvas = GetLayer(InLayer);
            if (canvas == null)
            {
                Log.Error($"UIManager: layer {InLayer} not registered");
                return null;
            }

            if (InPrefab == null)
            {
                Log.Error($"UIManager: prefab for {typeof(T).Name} missing");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(InPrefab, canvas.transform, false);
            go.name = InPrefab.name;
            var component = go.GetComponent<T>();
            if (component == null)
            {
                Log.Error($"UIManager: prefab {InPrefab.name} has no {typeof(T).Name}");
                UnityEngine.Object.Destroy(go);
            }

            return component;
        }

        public T Get<T>() where T : UIWindow
        {
            UIWindow window;
            if (Windows.TryGetValue(typeof(T), out window) == true)
            {
                return window as T;
            }

            return null;
        }

        /// <summary>등록된 창 목록 (의존성 테스트 · 툴용).</summary>
        public IEnumerable<UIWindow> AllWindows
        {
            get { return Windows.Values; }
        }

        public bool IsAnyBlockingWindowOpen()
        {
            foreach (var pair in Windows)
            {
                var window = pair.Value;
                if (window != null && window.IsOpen == true && window.BlocksGameInput == true)
                {
                    return true;
                }
            }

            return false;
        }

        public void CloseAll()
        {
            foreach (var pair in Windows)
            {
                if (pair.Value != null && pair.Value.BlocksGameInput == true)
                {
                    pair.Value.Close();
                }
            }
        }
    }
}
