using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 레이어(UILayer) 별 캔버스와 창(타입) 레지스트리.
    /// 캔버스 · 창 · 위젯은 모두 프리팹이다. 게임이 로드한 프리팹을 넘기면 Instantiate 해서 등록한다.
    /// 어느 레이어에 넣을지는 프리팹의 UIBehaviour 가 virtual GetLayer() 로 알려 준다 (2026-11-01, 예전에는 호출마다 int 레이어를 넘겼다).
    /// </summary>
    public class UIManager : IManager
    {
        private readonly Dictionary<UILayer, Canvas> Layers = new Dictionary<UILayer, Canvas>();
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

        /// <summary>UILayer.All 의 레이어마다 캔버스 프리팹을 하나씩 Instantiate 한다. 정렬 순서 = UILayer.SortingOrder.</summary>
        public void RegistLayers(GameObject InCanvasPrefab)
        {
            for (int i = 0; i < UILayer.All.Count; ++i)
            {
                RegistLayer(UILayer.All[i], InCanvasPrefab);
            }
        }

        public Canvas RegistLayer(UILayer InLayer, GameObject InCanvasPrefab)
        {
            Canvas canvas;
            if (Layers.TryGetValue(InLayer, out canvas) == true)
            {
                return canvas;
            }

            if (InCanvasPrefab == null)
            {
                Log.Error($"UIManager: canvas prefab missing for layer {InLayer.Name}");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(InCanvasPrefab, Root, false);
            go.name = InLayer.Name;
            canvas = go.GetComponent<Canvas>();
            if (canvas == null)
            {
                Log.Error($"UIManager: prefab {InCanvasPrefab.name} has no Canvas");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            canvas.sortingOrder = InLayer.SortingOrder;
            Layers.Add(InLayer, canvas);
            return canvas;
        }

        public Canvas GetCanvas(UILayer InLayer)
        {
            Canvas canvas;
            Layers.TryGetValue(InLayer, out canvas);
            return canvas;
        }

        /// <summary>창 프리팹을 그 창의 레이어(GetLayer) 캔버스 아래에 Instantiate 하고 (기본은 닫힌 상태로) 등록한다.</summary>
        public T Create<T>(GameObject InPrefab, bool InOpen = false) where T : UIWindow
        {
            UIWindow existing;
            if (Windows.TryGetValue(typeof(T), out existing) == true)
            {
                return existing as T;
            }

            var window = Spawn<T>(InPrefab);
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
        public async UniTask<T> CreateAsync<T>(string InAddress, bool InOpen = false) where T : UIWindow
        {
            var prefab = await Managers.Get<AssetLoader>().LoadAsync<GameObject>(InAddress, null);
            if (prefab == null)
            {
                Log.Error($"UIManager: window prefab '{InAddress}' not loaded");
                return null;
            }

            return Create<T>(prefab, InOpen);
        }

        /// <summary>UI 프리팹을 그 UIBehaviour 의 레이어(GetLayer) 캔버스 아래에 Instantiate 한다. 창 레지스트리에는 넣지 않는다 (월드 레이어 등).</summary>
        public T Spawn<T>(GameObject InPrefab) where T : UIBehaviour
        {
            if (InPrefab == null)
            {
                Log.Error($"UIManager: prefab for {typeof(T).Name} missing");
                return null;
            }

            var template = InPrefab.GetComponent<T>();
            if (template == null)
            {
                Log.Error($"UIManager: prefab {InPrefab.name} has no {typeof(T).Name}");
                return null;
            }

            var layer = template.GetLayer();
            var canvas = GetCanvas(layer);
            if (canvas == null)
            {
                Log.Error($"UIManager: layer {layer.Name} not registered");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(InPrefab, canvas.transform, false);
            go.name = InPrefab.name;
            return go.GetComponent<T>();
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

        /// <summary>ESC: ClosesOnCancel 레이어(창 레이어)의 열린 창을 모두 닫는다.</summary>
        public void CloseAll()
        {
            foreach (var pair in Windows)
            {
                if (pair.Value != null && pair.Value.GetLayer().ClosesOnCancel == true)
                {
                    pair.Value.Close();
                }
            }
        }
    }
}
