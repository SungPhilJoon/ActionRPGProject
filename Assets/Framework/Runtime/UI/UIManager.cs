using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 레이어(int) 별 캔버스와 창(타입) 레지스트리.
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

        public Canvas RegistLayer(int InLayer, string InName)
        {
            Canvas canvas;
            if (Layers.TryGetValue(InLayer, out canvas) == true)
            {
                return canvas;
            }

            canvas = UIFactory.CreateCanvas(InName, InLayer, Root);
            Layers.Add(InLayer, canvas);
            return canvas;
        }

        public Canvas GetLayer(int InLayer)
        {
            Canvas canvas;
            Layers.TryGetValue(InLayer, out canvas);
            return canvas;
        }

        /// <summary>창을 만들고 (닫힌 상태로) 등록한다.</summary>
        public T Create<T>(int InLayer, bool InOpen = false) where T : UIWindow
        {
            UIWindow existing;
            if (Windows.TryGetValue(typeof(T), out existing) == true)
            {
                return existing as T;
            }

            var canvas = GetLayer(InLayer);
            if (canvas == null)
            {
                Log.Error($"UIManager: layer {InLayer} not registered");
                return null;
            }

            var rect = UIFactory.CreateRect(canvas.transform, typeof(T).Name);
            UIFactory.Stretch(rect, 0.0f);
            var window = rect.gameObject.AddComponent<T>();
            window.Construct();
            Windows.Add(typeof(T), window);
            rect.gameObject.SetActive(InOpen);
            return window;
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
