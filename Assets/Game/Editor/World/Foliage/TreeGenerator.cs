using System.Collections.Generic;
using UnityEngine;

/// <summary>절차 나무 종류.</summary>
public enum ETreeSpecies
{
    Spruce = 0,
    Broadleaf = 1,
    Birch = 2,
    Dead = 3,
}

/// <summary>
/// 절차 나무 생성기 (에디터). 종마다 "골격"(줄기/가지 튜브 + 잎 카드 목록)을 한 번 만들고,
/// 같은 골격을 LOD 단계별 디테일로 메시화한다 → LOD 사이 모양이 어긋나지 않는다.
///  - 서브메시 0 = 껍질, 1 = 잎 (Game/Foliage 셰이더 공용)
///  - 정점 색: R = 바람 흔들림 가중치, G = 잎 떨림, B = AO (Foliage.shader 규약)
///  - 잎 카드 노멀은 수관 중심에서 바깥 방향과 섞어(구형 노멀) 카드 단면이 드러나지 않고 덩어리로 보이게 한다
/// </summary>
public static class TreeGenerator
{
    public class Limb
    {
        public readonly List<Vector3> Points = new List<Vector3>();
        public readonly List<float> Radii = new List<float>();
        public readonly List<float> Sway = new List<float>();
        public int Depth;
    }

    public class Card
    {
        public Vector3 Origin;
        public Vector3 Axis;
        public Vector3 Side;
        public float Length;
        public float Width;
        public float Droop;
        public float SwayBase;
        public float SwayTip;
        public float Occlusion;
        public float Importance;
    }

    public class Skeleton
    {
        public readonly List<Limb> Limbs = new List<Limb>();
        public readonly List<Card> Cards = new List<Card>();
        public Vector3 CrownCenter;
        public Vector3 CrownRadii = Vector3.one;
        public float Height;
        public float TrunkRadius;
        public float BarkTiling = 1.0f;
    }

    // ------------------------------------------------------------------ species

    public static Skeleton Build(ETreeSpecies InSpecies, int InSeed)
    {
        var random = new System.Random(InSeed);
        switch (InSpecies)
        {
            case ETreeSpecies.Spruce:
                return buildSpruce(random);
            case ETreeSpecies.Birch:
                return buildBirch(random);
            case ETreeSpecies.Dead:
                return buildDead(random);
            default:
                return buildBroadleaf(random);
        }
    }

    /// <summary>가문비/전나무: 곧은 원뿔 줄기 + 층층이 돌려나는 가지마다 바늘잎 스프레이 카드 2장 (V 자).</summary>
    private static Skeleton buildSpruce(System.Random InRandom)
    {
        var tree = new Skeleton();
        float height = range(InRandom, 13.0f, 19.0f);
        float radius = height * 0.017f;
        tree.Height = height;
        tree.TrunkRadius = radius;
        tree.BarkTiling = 1.0f;

        Vector3 lean = new Vector3(range(InRandom, -0.04f, 0.04f), 1.0f, range(InRandom, -0.04f, 0.04f)).normalized;
        var trunk = new Limb { Depth = 0 };
        for (int i = 0; i <= 8; ++i)
        {
            float t = i / 8.0f;
            trunk.Points.Add(lean * (height * t));
            trunk.Radii.Add(Mathf.Lerp(radius * 1.25f, 0.03f, Mathf.Pow(t, 0.85f)) * (i == 0 ? 1.25f : 1.0f));
            trunk.Sway.Add(t * t * 0.6f);
        }

        tree.Limbs.Add(trunk);

        float crownBase = height * range(InRandom, 0.10f, 0.18f);
        float maxLength = height * 0.25f;
        float y = crownBase;
        float yaw = range(InRandom, 0.0f, 360.0f);
        while (y < height * 0.97f)
        {
            float t = (y - crownBase) / (height - crownBase);
            int count = t > 0.85f ? 4 : InRandom.Next(5, 8);
            float length = maxLength * Mathf.Pow(1.0f - t, 0.95f) + 0.35f;
            float pitch = Mathf.Lerp(-22.0f, 30.0f, t * t);
            for (int b = 0; b < count; ++b)
            {
                float angle = yaw + b * 360.0f / count + range(InRandom, -14.0f, 14.0f);
                var direction = Quaternion.Euler(-pitch + range(InRandom, -6.0f, 6.0f), angle, 0.0f) * Vector3.forward;
                Vector3 origin = lean * y;
                float branchLength = length * range(InRandom, 0.8f, 1.12f);
                float baseSway = t * t * 0.6f;

                if (branchLength > 1.0f)
                {
                    var branch = new Limb { Depth = 1 };
                    branch.Points.Add(origin);
                    branch.Points.Add(origin + direction * branchLength * 0.75f + Vector3.down * branchLength * 0.08f);
                    branch.Radii.Add(Mathf.Lerp(radius * 0.32f, 0.02f, t));
                    branch.Radii.Add(0.012f);
                    branch.Sway.Add(baseSway);
                    branch.Sway.Add(baseSway + 0.35f);
                    tree.Limbs.Add(branch);
                }

                Vector3 side = Vector3.Cross(Vector3.up, direction).normalized;
                float width = Mathf.Max(0.7f, branchLength * 0.62f);
                float droop = branchLength * Mathf.Lerp(0.32f, 0.08f, t);
                float occlusion = Mathf.Lerp(0.55f, 1.0f, Mathf.Clamp01(t * 1.2f + 0.15f));
                for (int c = -1; c <= 1; c += 2)
                {
                    var tilted = Quaternion.AngleAxis(c * range(InRandom, 22.0f, 38.0f), direction) * side;
                    tree.Cards.Add(new Card
                    {
                        Origin = origin - direction * 0.15f,
                        Axis = direction,
                        Side = tilted,
                        Length = branchLength * 1.08f,
                        Width = width,
                        Droop = droop,
                        SwayBase = baseSway,
                        SwayTip = baseSway + 0.45f,
                        Occlusion = occlusion,
                        Importance = c > 0 ? 1.0f : 0.4f,
                    });
                }
            }

            yaw += range(InRandom, 25.0f, 45.0f);
            y += range(InRandom, 0.34f, 0.48f) * Mathf.Lerp(1.15f, 0.8f, t);
        }

        // 꼭대기 새순: 수직 카드 2장
        Vector3 top = lean * (height * 0.93f);
        for (int c = 0; c < 2; ++c)
        {
            var side = Quaternion.Euler(0.0f, c * 90.0f + yaw, 0.0f) * Vector3.right;
            tree.Cards.Add(new Card { Origin = top, Axis = lean, Side = side, Length = height * 0.1f, Width = 0.6f, Droop = 0.0f, SwayBase = 0.55f, SwayTip = 0.7f, Occlusion = 1.0f, Importance = 1.0f });
        }

        tree.CrownCenter = lean * (height * 0.45f);
        tree.CrownRadii = new Vector3(maxLength, height * 0.55f, maxLength);
        return tree;
    }

    /// <summary>활엽수(참나무/너도밤나무): 굽은 줄기 → 큰 가지 5~7개 → 잔가지 → 끝마다 잎 뭉치 카드.</summary>
    private static Skeleton buildBroadleaf(System.Random InRandom)
    {
        var tree = new Skeleton();
        float height = range(InRandom, 9.0f, 13.5f);
        float radius = range(InRandom, 0.26f, 0.36f);
        tree.Height = height;
        tree.TrunkRadius = radius;
        tree.BarkTiling = 1.5f;
        float crownBase = height * range(InRandom, 0.3f, 0.4f);

        var trunk = curvedLimb(InRandom, Vector3.zero, Vector3.up, height * 0.82f, radius, radius * 0.35f, 7, 0.12f, 0.0f, 0.35f, 0);
        trunk.Radii[0] *= 1.35f;
        tree.Limbs.Add(trunk);

        var crownCenter = new Vector3(0.0f, height * 0.66f, 0.0f);
        var crownRadii = new Vector3(height * 0.36f, height * 0.3f, height * 0.36f);
        tree.CrownCenter = crownCenter;
        tree.CrownRadii = crownRadii;

        int primaryCount = InRandom.Next(6, 9);
        float yaw = range(InRandom, 0.0f, 360.0f);
        for (int p = 0; p < primaryCount; ++p)
        {
            float t = Mathf.Lerp(crownBase / (height * 0.82f), 0.85f, (p + range(InRandom, 0.0f, 0.6f)) / primaryCount);
            Vector3 origin = pointOn(trunk, t);
            float angle = yaw + p * 137.5f;
            float tilt = Mathf.Lerp(58.0f, 30.0f, (float)p / primaryCount) + range(InRandom, -8.0f, 8.0f);
            var direction = Quaternion.Euler(0.0f, angle, 0.0f) * Quaternion.Euler(tilt, 0.0f, 0.0f) * Vector3.up;
            float length = height * range(InRandom, 0.32f, 0.45f) * (1.0f - t * 0.4f);
            float sway = 0.15f + t * 0.2f;
            var primary = curvedLimb(InRandom, origin, direction, length, radiusAt(trunk, t) * 0.55f, 0.04f, 5, 0.25f, sway, sway + 0.4f, 1);
            tree.Limbs.Add(primary);

            int secondaryCount = InRandom.Next(4, 6);
            for (int s = 0; s < secondaryCount; ++s)
            {
                float st = range(InRandom, 0.35f, 0.95f);
                Vector3 sOrigin = pointOn(primary, st);
                Vector3 sDirection = (direction + random3(InRandom) * 0.9f + Vector3.up * 0.25f).normalized;
                float sLength = length * range(InRandom, 0.35f, 0.55f);
                float sSway = Mathf.Lerp(sway, sway + 0.4f, st);
                var secondary = curvedLimb(InRandom, sOrigin, sDirection, sLength, radiusAt(primary, st) * 0.6f, 0.015f, 3, 0.3f, sSway, sSway + 0.3f, 2);
                tree.Limbs.Add(secondary);
                addCluster(tree, InRandom, pointOn(secondary, 1.0f), sSway + 0.3f, InRandom.Next(6, 9), 2.0f, 3.0f, false);
                addCluster(tree, InRandom, pointOn(secondary, 0.55f), sSway + 0.15f, InRandom.Next(3, 5), 1.8f, 2.6f, false);
            }

            addCluster(tree, InRandom, pointOn(primary, 1.0f), sway + 0.4f, InRandom.Next(6, 8), 2.2f, 3.2f, false);
        }

        // 수관 안쪽을 채우는 뭉치 (위에서 내려다볼 때 구멍이 덜 보이게)
        for (int i = 0; i < 24; ++i)
        {
            Vector3 p = crownCenter + Vector3.Scale(random3(InRandom), crownRadii * 0.75f);
            addCluster(tree, InRandom, p, 0.5f, 4, 2.0f, 2.8f, false);
        }

        return tree;
    }

    /// <summary>자작나무: 가늘고 흰 줄기, 위로 뻗는 가는 가지, 늘어지는 작은 잎 뭉치.</summary>
    private static Skeleton buildBirch(System.Random InRandom)
    {
        var tree = new Skeleton();
        float height = range(InRandom, 10.0f, 15.0f);
        float radius = range(InRandom, 0.13f, 0.18f);
        tree.Height = height;
        tree.TrunkRadius = radius;
        tree.BarkTiling = 2.0f;

        var trunk = curvedLimb(InRandom, Vector3.zero, Vector3.up, height * 0.95f, radius, 0.03f, 8, 0.08f, 0.0f, 0.55f, 0);
        tree.Limbs.Add(trunk);
        tree.CrownCenter = new Vector3(0.0f, height * 0.65f, 0.0f);
        tree.CrownRadii = new Vector3(height * 0.22f, height * 0.33f, height * 0.22f);

        int count = InRandom.Next(13, 17);
        float yaw = range(InRandom, 0.0f, 360.0f);
        for (int b = 0; b < count; ++b)
        {
            float t = Mathf.Lerp(0.38f, 0.92f, (b + range(InRandom, 0.0f, 0.8f)) / count);
            Vector3 origin = pointOn(trunk, t);
            var direction = Quaternion.Euler(0.0f, yaw + b * 137.5f, 0.0f) * Quaternion.Euler(range(InRandom, 30.0f, 50.0f), 0.0f, 0.0f) * Vector3.up;
            float length = height * range(InRandom, 0.18f, 0.28f) * (1.0f - t * 0.45f);
            float sway = 0.2f + t * 0.35f;
            var branch = curvedLimb(InRandom, origin, direction, length, radius * 0.35f, 0.012f, 4, 0.2f, sway, sway + 0.45f, 1);
            tree.Limbs.Add(branch);
            addCluster(tree, InRandom, pointOn(branch, 1.0f), sway + 0.45f, InRandom.Next(5, 8), 1.5f, 2.2f, true);
            addCluster(tree, InRandom, pointOn(branch, 0.6f), sway + 0.3f, InRandom.Next(3, 5), 1.4f, 2.0f, true);
        }

        addCluster(tree, InRandom, pointOn(trunk, 1.0f), 0.6f, 5, 1.4f, 2.0f, true);
        for (int i = 0; i < 10; ++i)
        {
            Vector3 p = tree.CrownCenter + Vector3.Scale(random3(InRandom), tree.CrownRadii * 0.7f);
            addCluster(tree, InRandom, p, 0.5f, 3, 1.4f, 2.0f, true);
        }
        return tree;
    }

    /// <summary>고사목: 껍질만, 꺾인 큰 가지 몇 개 (World1 레퍼런스의 마른 나무).</summary>
    private static Skeleton buildDead(System.Random InRandom)
    {
        var tree = new Skeleton();
        float height = range(InRandom, 7.0f, 12.0f);
        float radius = range(InRandom, 0.2f, 0.3f);
        tree.Height = height;
        tree.TrunkRadius = radius;
        tree.BarkTiling = 1.2f;

        var trunk = curvedLimb(InRandom, Vector3.zero, Vector3.up, height, radius, radius * 0.25f, 7, 0.18f, 0.0f, 0.15f, 0);
        trunk.Radii[0] *= 1.3f;
        tree.Limbs.Add(trunk);
        tree.CrownCenter = new Vector3(0.0f, height * 0.6f, 0.0f);
        tree.CrownRadii = new Vector3(height * 0.25f, height * 0.4f, height * 0.25f);

        int count = InRandom.Next(4, 8);
        for (int b = 0; b < count; ++b)
        {
            float t = range(InRandom, 0.3f, 0.9f);
            Vector3 origin = pointOn(trunk, t);
            var direction = Quaternion.Euler(0.0f, range(InRandom, 0.0f, 360.0f), 0.0f) * Quaternion.Euler(range(InRandom, 35.0f, 75.0f), 0.0f, 0.0f) * Vector3.up;
            float length = height * range(InRandom, 0.18f, 0.35f);
            var branch = curvedLimb(InRandom, origin, direction, length, radiusAt(trunk, t) * 0.45f, 0.02f, 4, 0.35f, 0.05f, 0.2f, 1);
            tree.Limbs.Add(branch);
            if (InRandom.NextDouble() < 0.6)
            {
                float st = range(InRandom, 0.4f, 0.8f);
                var twig = curvedLimb(InRandom, pointOn(branch, st), (direction + random3(InRandom)).normalized, length * 0.45f, radiusAt(branch, st) * 0.6f, 0.01f, 3, 0.3f, 0.1f, 0.25f, 2);
                tree.Limbs.Add(twig);
            }
        }

        return tree;
    }

    private static void addCluster(Skeleton InTree, System.Random InRandom, Vector3 InCenter, float InSway, int InCount, float InMinSize, float InMaxSize, bool InDrooping)
    {
        for (int i = 0; i < InCount; ++i)
        {
            Vector3 outward = InCenter - InTree.CrownCenter;
            outward.y *= 0.6f;
            outward = outward.sqrMagnitude > 0.01f ? outward.normalized : Vector3.up;
            Vector3 axis = (outward + random3(InRandom) * 0.9f + (InDrooping == true ? Vector3.down * 0.9f : Vector3.up * 0.35f)).normalized;
            Vector3 side = Vector3.Cross(axis, random3(InRandom)).normalized;
            if (side.sqrMagnitude < 0.01f)
            {
                side = Vector3.right;
            }

            float size = range(InRandom, InMinSize, InMaxSize);
            Vector3 offset = Vector3.Scale(new Vector3(range(InRandom, -0.5f, 0.5f), range(InRandom, -0.3f, 0.3f), range(InRandom, -0.5f, 0.5f)), Vector3.one * size);
            Vector3 relative = InCenter - InTree.CrownCenter;
            float distance = new Vector3(relative.x / InTree.CrownRadii.x, relative.y / InTree.CrownRadii.y, relative.z / InTree.CrownRadii.z).magnitude;
            InTree.Cards.Add(new Card
            {
                Origin = InCenter + offset - axis * size * 0.35f,
                Axis = axis,
                Side = side,
                Length = size,
                Width = size,
                Droop = InDrooping == true ? size * 0.15f : 0.0f,
                SwayBase = InSway,
                SwayTip = InSway + 0.2f,
                Occlusion = Mathf.Lerp(0.5f, 1.0f, Mathf.Clamp01(distance + (relative.y > 0.0f ? 0.2f : 0.0f))),
                Importance = (float)InRandom.NextDouble(),
            });
        }
    }

    private static Limb curvedLimb(System.Random InRandom, Vector3 InOrigin, Vector3 InDirection, float InLength, float InBaseRadius, float InTipRadius, int InSegments, float InCurl, float InSwayBase, float InSwayTip, int InDepth)
    {
        var limb = new Limb { Depth = InDepth };
        Vector3 position = InOrigin;
        Vector3 direction = InDirection.normalized;
        float step = InLength / InSegments;
        for (int i = 0; i <= InSegments; ++i)
        {
            float t = (float)i / InSegments;
            limb.Points.Add(position);
            limb.Radii.Add(Mathf.Lerp(InBaseRadius, InTipRadius, Mathf.Pow(t, 0.8f)));
            limb.Sway.Add(Mathf.Lerp(InSwayBase, InSwayTip, t));
            direction = (direction + random3(InRandom) * InCurl + Vector3.up * (InDepth > 0 ? 0.04f : 0.0f)).normalized;
            position += direction * step;
        }

        return limb;
    }

    private static Vector3 pointOn(Limb InLimb, float InT)
    {
        float f = Mathf.Clamp01(InT) * (InLimb.Points.Count - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), InLimb.Points.Count - 2);
        return Vector3.Lerp(InLimb.Points[i], InLimb.Points[i + 1], f - i);
    }

    private static float radiusAt(Limb InLimb, float InT)
    {
        float f = Mathf.Clamp01(InT) * (InLimb.Radii.Count - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), InLimb.Radii.Count - 2);
        return Mathf.Lerp(InLimb.Radii[i], InLimb.Radii[i + 1], f - i);
    }

    private static float range(System.Random InRandom, float InMin, float InMax)
    {
        return InMin + (float)InRandom.NextDouble() * (InMax - InMin);
    }

    private static Vector3 random3(System.Random InRandom)
    {
        return new Vector3(range(InRandom, -1.0f, 1.0f), range(InRandom, -1.0f, 1.0f), range(InRandom, -1.0f, 1.0f));
    }

    // ------------------------------------------------------------------ mesh

    /// <summary>
    /// 골격 → 메시. InLod 0 = 원본, 1 = 원거리(튜브 면 수 절반, 잔가지 생략, 중요도 낮은 카드 생략 대신 남은 카드를 키움).
    /// </summary>
    public static Mesh ToMesh(Skeleton InTree, int InLod, string InName)
    {
        var builder = new MeshData();
        foreach (var limb in InTree.Limbs)
        {
            if (InLod > 0 && limb.Depth >= 2)
            {
                continue;
            }

            int sides = limb.Depth == 0 ? (InLod == 0 ? 10 : 6) : (InLod == 0 ? 6 : 4);
            addTube(builder, InTree, limb, sides);
        }

        foreach (var card in InTree.Cards)
        {
            if (InLod > 0 && card.Importance < 0.45f)
            {
                continue;
            }

            float grow = InLod > 0 ? 1.25f : 1.0f;
            addCard(builder, InTree, card, InLod == 0 ? 3 : 2, grow);
        }

        return builder.ToMesh(InName);
    }

    private static void addTube(MeshData InBuilder, Skeleton InTree, Limb InLimb, int InSides)
    {
        int start = InBuilder.Vertices.Count;
        int rings = InLimb.Points.Count;
        Vector3 previousNormal = Vector3.Cross(tangentAt(InLimb, 0), Vector3.forward);
        if (previousNormal.sqrMagnitude < 0.001f)
        {
            previousNormal = Vector3.Cross(tangentAt(InLimb, 0), Vector3.right);
        }

        previousNormal.Normalize();
        float length = 0.0f;
        float circumference = Mathf.Max(0.05f, InLimb.Radii[0] * Mathf.PI * 2.0f);
        float uRepeat = Mathf.Max(1.0f, Mathf.Round(circumference / 0.9f));
        for (int r = 0; r < rings; ++r)
        {
            Vector3 tangent = tangentAt(InLimb, r);
            // 평행 이동 프레임: 이전 링의 법선을 현재 접선에 수직으로 투영해 비틀림을 막는다.
            Vector3 normal = (previousNormal - tangent * Vector3.Dot(previousNormal, tangent)).normalized;
            Vector3 binormal = Vector3.Cross(tangent, normal);
            previousNormal = normal;
            if (r > 0)
            {
                length += Vector3.Distance(InLimb.Points[r], InLimb.Points[r - 1]);
            }

            float v = length / (circumference / uRepeat) * InTree.BarkTiling * 0.5f;
            float height01 = Mathf.Clamp01(InLimb.Points[r].y / Mathf.Max(1.0f, InTree.Height));
            float occlusion = InLimb.Depth == 0 ? Mathf.Lerp(0.55f, 1.0f, Mathf.Clamp01(height01 * 3.0f + 0.2f)) : 0.75f;
            for (int s = 0; s <= InSides; ++s)
            {
                float angle = (float)s / InSides * Mathf.PI * 2.0f;
                Vector3 radial = normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle);
                InBuilder.Vertices.Add(InLimb.Points[r] + radial * InLimb.Radii[r]);
                InBuilder.Normals.Add(radial);
                Vector3 around = Vector3.Cross(tangent, radial);
                InBuilder.Tangents.Add(new Vector4(around.x, around.y, around.z, -1.0f));
                InBuilder.Uvs.Add(new Vector2((float)s / InSides * uRepeat, v));
                InBuilder.Colors.Add(new Color(InLimb.Sway[r], 0.0f, occlusion, 1.0f));
            }
        }

        int stride = InSides + 1;
        for (int r = 0; r < rings - 1; ++r)
        {
            for (int s = 0; s < InSides; ++s)
            {
                int a = start + r * stride + s;
                int b = a + stride;
                InBuilder.Bark.Add(a);
                InBuilder.Bark.Add(a + 1);
                InBuilder.Bark.Add(b);
                InBuilder.Bark.Add(a + 1);
                InBuilder.Bark.Add(b + 1);
                InBuilder.Bark.Add(b);
            }
        }
    }

    private static Vector3 tangentAt(Limb InLimb, int InIndex)
    {
        int count = InLimb.Points.Count;
        Vector3 a = InLimb.Points[Mathf.Max(0, InIndex - 1)];
        Vector3 b = InLimb.Points[Mathf.Min(count - 1, InIndex + 1)];
        Vector3 tangent = b - a;
        return tangent.sqrMagnitude > 0.000001f ? tangent.normalized : Vector3.up;
    }

    private static void addCard(MeshData InBuilder, Skeleton InTree, Card InCard, int InSegments, float InGrow)
    {
        int start = InBuilder.Vertices.Count;
        float length = InCard.Length * InGrow;
        float width = InCard.Width * InGrow;
        Vector3 cardNormal = Vector3.Cross(InCard.Side, InCard.Axis).normalized;
        if (cardNormal.y < 0.0f)
        {
            cardNormal = -cardNormal;
        }

        for (int i = 0; i <= InSegments; ++i)
        {
            float t = (float)i / InSegments;
            Vector3 center = InCard.Origin + InCard.Axis * (length * t) + Vector3.down * (InCard.Droop * t * t);
            for (int s = 0; s <= 1; ++s)
            {
                Vector3 position = center + InCard.Side * (width * (s - 0.5f));
                Vector3 radial = position - InTree.CrownCenter;
                radial = new Vector3(radial.x / InTree.CrownRadii.x, radial.y / InTree.CrownRadii.y, radial.z / InTree.CrownRadii.z);
                radial = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector3.up;
                Vector3 normal = Vector3.Lerp(cardNormal, (radial + Vector3.up * 0.35f).normalized, 0.75f).normalized;
                InBuilder.Vertices.Add(position);
                InBuilder.Normals.Add(normal);
                InBuilder.Tangents.Add(new Vector4(InCard.Side.x, InCard.Side.y, InCard.Side.z, 1.0f));
                InBuilder.Uvs.Add(new Vector2(s, t));
                float occlusion = Mathf.Lerp(InCard.Occlusion * 0.85f, InCard.Occlusion, t);
                InBuilder.Colors.Add(new Color(Mathf.Lerp(InCard.SwayBase, InCard.SwayTip, t), t, occlusion, 1.0f));
            }
        }

        for (int i = 0; i < InSegments; ++i)
        {
            int a = start + i * 2;
            InBuilder.Leaves.Add(a);
            InBuilder.Leaves.Add(a + 2);
            InBuilder.Leaves.Add(a + 1);
            InBuilder.Leaves.Add(a + 1);
            InBuilder.Leaves.Add(a + 2);
            InBuilder.Leaves.Add(a + 3);
        }
    }

    private class MeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<Vector2> Uvs = new List<Vector2>();
        public readonly List<Color> Colors = new List<Color>();
        public readonly List<int> Bark = new List<int>();
        public readonly List<int> Leaves = new List<int>();

        public Mesh ToMesh(string InName)
        {
            var mesh = new Mesh();
            mesh.name = InName;
            mesh.indexFormat = Vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Vertices);
            mesh.SetNormals(Normals);
            mesh.SetTangents(Tangents);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.subMeshCount = Leaves.Count > 0 ? 2 : 1;
            mesh.SetTriangles(Bark, 0);
            if (Leaves.Count > 0)
            {
                mesh.SetTriangles(Leaves, 1);
            }

            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
