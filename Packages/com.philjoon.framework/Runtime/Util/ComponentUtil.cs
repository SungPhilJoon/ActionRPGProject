using UnityEngine;

namespace Framework
{
    public static class ComponentUtil
    {
        public static T GetOrAdd<T>(GameObject InObject) where T : Component
        {
            var component = InObject.GetComponent<T>();
            if (component == null)
            {
                component = InObject.AddComponent<T>();
            }

            return component;
        }

        public static void SetLayerRecursive(GameObject InObject, int InLayer)
        {
            InObject.layer = InLayer;
            var transform = InObject.transform;
            for (int i = 0; i < transform.childCount; ++i)
            {
                SetLayerRecursive(transform.GetChild(i).gameObject, InLayer);
            }
        }

        /// <summary>파괴되었거나 null 인 UnityEngine.Object 를 한 번에 판정한다 (Unity 의 fake null 포함).</summary>
        public static bool IsAlive(Object InObject)
        {
            return InObject != null;
        }
    }
}
