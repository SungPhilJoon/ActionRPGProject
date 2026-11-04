using System;
using System.Collections.Generic;
using UnityEngine;

namespace Framework
{
    /// <summary>
    /// 씬 단위로 살아 있는 매니저. MonoBehaviour 가 아닌 순수 C# 객체로 두어
    /// 업데이트 순서를 Managers 가 명시적으로 결정하게 한다 (Service Locator + 명시적 라이프사이클).
    /// </summary>
    public interface IManager
    {
        void Initialize();
        void UnInitialize();
        void Update(float InDeltaTime);
    }

    public static class Managers
    {
        private static readonly Dictionary<Type, IManager> Registry = new Dictionary<Type, IManager>();
        private static readonly List<IManager> UpdateOrder = new List<IManager>();

        public static T Regist<T>(T InManager) where T : class, IManager
        {
            var type = typeof(T);
            if (Registry.ContainsKey(type) == true)
            {
                Log.Warning($"Managers.Regist duplicated: {type.Name}");
                return Registry[type] as T;
            }

            Registry.Add(type, InManager);
            UpdateOrder.Add(InManager);
            InManager.Initialize();
            return InManager;
        }

        public static T Get<T>() where T : class, IManager
        {
            IManager manager;
            if (Registry.TryGetValue(typeof(T), out manager) == false)
            {
                return null;
            }

            return manager as T;
        }

        public static void Update(float InDeltaTime)
        {
            for (int i = 0; i < UpdateOrder.Count; ++i)
            {
                UpdateOrder[i].Update(InDeltaTime);
            }
        }

        /// <summary>등록의 역순으로 정리한다. 나중에 만든 매니저가 먼저 만든 매니저에 의존하기 때문이다.</summary>
        public static void Clear()
        {
            for (int i = UpdateOrder.Count - 1; i >= 0; --i)
            {
                try
                {
                    UpdateOrder[i].UnInitialize();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            UpdateOrder.Clear();
            Registry.Clear();
        }
    }
}
