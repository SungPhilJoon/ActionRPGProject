using UnityEngine;

namespace Framework
{
    public static class MathUtil
    {
        /// <summary>y 를 버린 평면 방향. 길이가 0 이면 InFallback 을 돌려준다.</summary>
        public static Vector3 FlatDirection(Vector3 InFrom, Vector3 InTo, Vector3 InFallback)
        {
            Vector3 dir = InTo - InFrom;
            dir.y = 0.0f;
            if (dir.sqrMagnitude < 0.0001f)
            {
                return InFallback;
            }

            return dir.normalized;
        }

        public static Vector3 Flat(Vector3 InVector)
        {
            InVector.y = 0.0f;
            return InVector;
        }

        public static float FlatDistance(Vector3 InA, Vector3 InB)
        {
            float dx = InA.x - InB.x;
            float dz = InA.z - InB.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>InForward 기준 InAngle(도) 부채꼴 안에 InTarget 방향이 있는지.</summary>
        public static bool IsInArc(Vector3 InForward, Vector3 InToTarget, float InAngle)
        {
            InToTarget.y = 0.0f;
            InForward.y = 0.0f;
            if (InToTarget.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            return Vector3.Angle(InForward, InToTarget) <= InAngle * 0.5f;
        }

        public static float Percent(float InValue, float InPercent)
        {
            return InValue * InPercent * 0.01f;
        }
    }
}
