using System.Collections.Generic;
using Framework;
using UnityEngine;

/// <summary>
/// 강/호수 영역 (WorldBuilder 가 수면 메시와 함께 만든다). 물 셰이더 메시와 같은 모양을 수학적으로 들고 있어
/// 스폰 위치가 물속인지 판정한다 (콜라이더 없음 — 캐릭터는 얕은 물을 걸어서 건넌다).
///  - 강: Path(중심선, y = 수면 높이) + HalfWidths
///  - 호수: Path 1점 + HalfWidths[0] = 반지름
/// </summary>
public class WaterBody : BaseBehaviour
{
    private static readonly List<WaterBody> Bodies = new List<WaterBody>();

    public Vector3[] Path = new Vector3[0];
    public float[] HalfWidths = new float[0];

    protected override void OnUnityAwake()
    {
        base.OnUnityAwake();
        Bodies.Add(this);
    }

    protected override void OnUnityDestroy()
    {
        Bodies.Remove(this);
        base.OnUnityDestroy();
    }

    /// <summary>XZ 위치가 어떤 물 영역 안(InMargin 만큼 넓혀서)에 있는지.</summary>
    public static bool IsInWater(Vector3 InPosition, float InMargin)
    {
        for (int i = 0; i < Bodies.Count; ++i)
        {
            if (Bodies[i] != null && Bodies[i].Contains(InPosition, InMargin) == true)
            {
                return true;
            }
        }

        return false;
    }

    public bool Contains(Vector3 InPosition, float InMargin)
    {
        if (Path.Length == 0 || HalfWidths.Length != Path.Length)
        {
            return false;
        }

        if (Path.Length == 1)
        {
            return MathUtil.FlatDistance(Path[0], InPosition) <= HalfWidths[0] + InMargin;
        }

        var p = new Vector2(InPosition.x, InPosition.z);
        for (int i = 0; i < Path.Length - 1; ++i)
        {
            var a = new Vector2(Path[i].x, Path[i].z);
            var b = new Vector2(Path[i + 1].x, Path[i + 1].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float halfWidth = Mathf.Lerp(HalfWidths[i], HalfWidths[i + 1], t);
            if (Vector2.Distance(p, a + ab * t) <= halfWidth + InMargin)
            {
                return true;
            }
        }

        return false;
    }
}
