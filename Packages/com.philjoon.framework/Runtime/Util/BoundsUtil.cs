using UnityEngine;

namespace Framework
{
    public static class BoundsUtil
    {
        /// <summary>InObject 아래 렌더러(없으면 콜라이더) 바운드를 합친다. 둘 다 없으면 false.</summary>
        public static bool TryGetWorldBounds(GameObject InObject, out Bounds OutBounds)
        {
            OutBounds = default;
            bool found = false;
            foreach (var renderer in InObject.GetComponentsInChildren<Renderer>())
            {
                encapsulate(ref OutBounds, ref found, renderer.bounds);
            }

            if (found == true)
            {
                return true;
            }

            foreach (var collider in InObject.GetComponentsInChildren<Collider>())
            {
                encapsulate(ref OutBounds, ref found, collider.bounds);
            }

            return found;
        }

        /// <summary>XZ 평면에서 원(InCenter, InRadius)과 바운드 사각형이 겹치는지.</summary>
        public static bool CircleOverlapsXZ(Bounds InBounds, Vector3 InCenter, float InRadius)
        {
            float x = Mathf.Clamp(InCenter.x, InBounds.min.x, InBounds.max.x) - InCenter.x;
            float z = Mathf.Clamp(InCenter.z, InBounds.min.z, InBounds.max.z) - InCenter.z;
            return x * x + z * z <= InRadius * InRadius;
        }

        private static void encapsulate(ref Bounds InOutBounds, ref bool InOutFound, Bounds InBounds)
        {
            if (InOutFound == false)
            {
                InOutBounds = InBounds;
                InOutFound = true;
                return;
            }

            InOutBounds.Encapsulate(InBounds);
        }
    }
}
