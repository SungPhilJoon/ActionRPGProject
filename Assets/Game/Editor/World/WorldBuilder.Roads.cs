using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

/// <summary>
/// 흙길: 지역을 잇는 길 9개 (제어점 → Catmull-Rom). 걷는 길을 지형 · 지도 · NavMesh 에 모두 반영한다.
///  - 높이: 길을 따라 지형 높이를 이동 평균 + 경사 제한(RoadMaxGrade)으로 다듬은 프로필 → 길폭 안은 그 높이, 갓길(RoadShoulder)에서 원래 지형으로 섞는다.
///    고원(트롤 능선 · 동쪽 고원)도 길이 완만한 오르막이 되어 드나들 수 있다.
///  - 지형 레이어: 흙(Soil), 풀 디테일 없음. 나무 · 폐허는 길에서 떨어져 놓인다.
///  - NavMesh: 길 위는 Road 영역(비용 1) → 퀘스트 길 안내가 길을 따라간다 (NavAreaId, QuestNavigator).
/// 길은 강 · 호수를 건너지 않게 잡았다 (다리 없음). 문서: World.md 2.1
/// </summary>
public static partial class WorldBuilder
{
    private struct RoadDef
    {
        public Vector2[] Points;
        public float HalfWidth;

        public RoadDef(float InHalfWidth, params Vector2[] InPoints)
        {
            HalfWidth = InHalfWidth;
            Points = InPoints;
        }
    }

    private static readonly RoadDef[] RoadDefs =
    {
        // 시작 → 초원 → 폐허 마을 (중심 길)
        new RoadDef(3.5f, new Vector2(256.0f, 104.0f), new Vector2(248.0f, 135.0f), new Vector2(232.0f, 165.0f), new Vector2(262.0f, 192.0f), new Vector2(300.0f, 208.0f)),
        // 초원 → 던리 숲 (호수 남쪽)
        new RoadDef(3.0f, new Vector2(232.0f, 165.0f), new Vector2(196.0f, 190.0f), new Vector2(160.0f, 212.0f), new Vector2(130.0f, 230.0f)),
        // 폐허 마을 → 뱀 늪 (호수 동쪽)
        new RoadDef(3.0f, new Vector2(300.0f, 222.0f), new Vector2(262.0f, 252.0f), new Vector2(228.0f, 282.0f), new Vector2(190.0f, 302.0f)),
        // 뱀 늪 → 트롤 능선
        new RoadDef(3.0f, new Vector2(190.0f, 302.0f), new Vector2(212.0f, 340.0f), new Vector2(240.0f, 370.0f), new Vector2(250.0f, 385.0f)),
        // 폐허 마을 → 트롤 능선
        new RoadDef(3.5f, new Vector2(300.0f, 222.0f), new Vector2(292.0f, 280.0f), new Vector2(272.0f, 335.0f), new Vector2(255.0f, 375.0f)),
        // 폐허 마을 → 바위 고원
        new RoadDef(3.5f, new Vector2(300.0f, 215.0f), new Vector2(345.0f, 212.0f), new Vector2(390.0f, 220.0f)),
        // 바위 고원 → 구리 골짜기
        new RoadDef(3.0f, new Vector2(390.0f, 220.0f), new Vector2(418.0f, 282.0f), new Vector2(430.0f, 345.0f)),
        // 구리 골짜기 → 미노타우로스 투기장
        new RoadDef(3.0f, new Vector2(430.0f, 345.0f), new Vector2(418.0f, 392.0f), new Vector2(395.0f, 422.0f)),
        // 트롤 능선 → 미노타우로스 투기장
        new RoadDef(3.5f, new Vector2(262.0f, 392.0f), new Vector2(315.0f, 410.0f), new Vector2(370.0f, 425.0f)),
    };

    private const float RoadStep = 3.0f;
    private const float RoadShoulder = 7.0f;
    /// <summary>길 경사 한계 (높이 / 수평 거리). 0.14 ≈ 8°.</summary>
    private const float RoadMaxGrade = 0.14f;
    private const float RoadCell = 16.0f;
    /// <summary>NavMesh Road 영역 상자 하나가 덮는 길 샘플 수.</summary>
    private const int RoadVolumeSamples = 4;

    /// <summary>길 샘플 (x, 높이, z) — 길마다 이어진 목록.</summary>
    private static List<List<Vector3>> RoadPaths;
    private static List<float> RoadHalfWidths;
    /// <summary>격자 칸 → (길 번호, 구간 번호). 지형 높이 513² 를 계산할 때 모든 구간을 훑지 않도록.</summary>
    private static Dictionary<int, List<Vector2Int>> RoadGrid;

    /// <summary>길 높이 프로필 계산. sampleHeight(길 없음)를 쓰므로 buildTerrain 에서 강 다음, 높이 계산 전에 호출한다.</summary>
    private static void prepareRoads(List<FlatZone> InZones)
    {
        RoadPaths = null;
        RoadGrid = null;
        var paths = new List<List<Vector3>>();
        var widths = new List<float>();
        foreach (var road in RoadDefs)
        {
            var points = densify(road.Points, RoadStep);
            var heights = new float[points.Count];
            for (int i = 0; i < points.Count; ++i)
            {
                heights[i] = sampleHeight(points[i].x, points[i].y, InZones);
            }

            smoothProfile(heights);
            var path = new List<Vector3>(points.Count);
            for (int i = 0; i < points.Count; ++i)
            {
                path.Add(new Vector3(points[i].x, heights[i], points[i].y));
            }

            paths.Add(path);
            widths.Add(road.HalfWidth);
        }

        var grid = new Dictionary<int, List<Vector2Int>>();
        for (int r = 0; r < paths.Count; ++r)
        {
            float reach = widths[r] + RoadShoulder;
            for (int i = 1; i < paths[r].Count; ++i)
            {
                var a = paths[r][i - 1];
                var b = paths[r][i];
                int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - reach) / RoadCell);
                int x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + reach) / RoadCell);
                int z0 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - reach) / RoadCell);
                int z1 = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + reach) / RoadCell);
                for (int cz = z0; cz <= z1; ++cz)
                {
                    for (int cx = x0; cx <= x1; ++cx)
                    {
                        List<Vector2Int> list;
                        int key = roadCellKey(cx, cz);
                        if (grid.TryGetValue(key, out list) == false)
                        {
                            list = new List<Vector2Int>();
                            grid.Add(key, list);
                        }

                        list.Add(new Vector2Int(r, i));
                    }
                }
            }
        }

        RoadPaths = paths;
        RoadHalfWidths = widths;
        RoadGrid = grid;
    }

    /// <summary>이동 평균 3회 → 앞뒤 두 번 경사 제한 (오르막 · 내리막 모두 RoadMaxGrade 이하).</summary>
    private static void smoothProfile(float[] InHeights)
    {
        int count = InHeights.Length;
        var buffer = new float[count];
        const int radius = 4;
        for (int pass = 0; pass < 3; ++pass)
        {
            for (int i = 0; i < count; ++i)
            {
                float sum = 0.0f;
                int n = 0;
                for (int k = Mathf.Max(0, i - radius); k <= Mathf.Min(count - 1, i + radius); ++k)
                {
                    sum += InHeights[k];
                    n += 1;
                }

                buffer[i] = sum / n;
            }

            System.Array.Copy(buffer, InHeights, count);
        }

        float limit = RoadMaxGrade * RoadStep;
        for (int i = 1; i < count; ++i)
        {
            InHeights[i] = Mathf.Clamp(InHeights[i], InHeights[i - 1] - limit, InHeights[i - 1] + limit);
        }

        for (int i = count - 2; i >= 0; --i)
        {
            InHeights[i] = Mathf.Clamp(InHeights[i], InHeights[i + 1] - limit, InHeights[i + 1] + limit);
        }
    }

    private static int roadCellKey(int InX, int InZ)
    {
        return (InX + 1000) * 4096 + (InZ + 1000);
    }

    /// <summary>InPoint 에서 가장 가까운 길: 중심선까지 거리, 그 지점 길 높이, 길 반폭. 길 근처(반폭 + 갓길)가 아니면 false.</summary>
    private static bool sampleRoad(Vector2 InPoint, out float OutDistance, out float OutHeight, out float OutHalfWidth)
    {
        OutDistance = float.MaxValue;
        OutHeight = 0.0f;
        OutHalfWidth = 0.0f;
        if (RoadGrid == null)
        {
            return false;
        }

        List<Vector2Int> segments;
        int key = roadCellKey(Mathf.FloorToInt(InPoint.x / RoadCell), Mathf.FloorToInt(InPoint.y / RoadCell));
        if (RoadGrid.TryGetValue(key, out segments) == false)
        {
            return false;
        }

        for (int s = 0; s < segments.Count; ++s)
        {
            var path = RoadPaths[segments[s].x];
            int i = segments[s].y;
            var a = new Vector2(path[i - 1].x, path[i - 1].z);
            var b = new Vector2(path[i].x, path[i].z);
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(InPoint - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float distance = Vector2.Distance(InPoint, a + ab * t);
            if (distance < OutDistance)
            {
                OutDistance = distance;
                OutHeight = Mathf.Lerp(path[i - 1].y, path[i].y, t);
                OutHalfWidth = RoadHalfWidths[segments[s].x];
            }
        }

        return OutDistance < OutHalfWidth + RoadShoulder;
    }

    /// <summary>길 가중치 (길 안 1 → 갓길 끝 0, 부드러운 곡선).</summary>
    private static float roadWeight(Vector2 InPoint)
    {
        float distance;
        float height;
        float halfWidth;
        if (sampleRoad(InPoint, out distance, out height, out halfWidth) == false)
        {
            return 0.0f;
        }

        float t = Mathf.Clamp01((distance - halfWidth) / RoadShoulder);
        return 1.0f - t * t * (3.0f - 2.0f * t);
    }

    /// <summary>sampleHeight 마지막 단계: 길 높이 프로필로 지형을 다듬는다 (길 위는 살짝 파서 바퀴 자국처럼).</summary>
    private static float applyRoad(float InX, float InZ, float InHeight)
    {
        float distance;
        float height;
        float halfWidth;
        var point = new Vector2(InX, InZ);
        if (sampleRoad(point, out distance, out height, out halfWidth) == false)
        {
            return InHeight;
        }

        float t = Mathf.Clamp01((distance - halfWidth) / RoadShoulder);
        float weight = 1.0f - t * t * (3.0f - 2.0f * t);
        return Mathf.Lerp(InHeight, height - 0.12f, weight);
    }

    /// <summary>InPoint 가 길에서 InClearance 이상 떨어져 있는가 (나무 · 폐허 배치).</summary>
    private static bool clearOfRoads(Vector2 InPoint, float InClearance)
    {
        float distance;
        float height;
        float halfWidth;
        sampleRoad(InPoint, out distance, out height, out halfWidth);
        if (distance == float.MaxValue)
        {
            return true;
        }

        return distance > halfWidth + InClearance;
    }

    // ------------------------------------------------------------------ navmesh areas

    /// <summary>
    /// NavMesh 영역 상자: 길 = Road, 호수 · 강 · 맵 가장자리 산 띠 = Not Walkable (걸을 수 없음 → 플레이어도 못 들어감).
    /// NavMeshSurface 가 굽기 전에 씬에 둔다.
    /// </summary>
    private static void buildNavAreas(Terrain InTerrain, Transform InParent)
    {
        var root = new GameObject("NavAreas").transform;
        root.SetParent(InParent, false);

        for (int r = 0; r < RoadPaths.Count; ++r)
        {
            var path = RoadPaths[r];
            for (int i = 0; i < path.Count - 1; i += RoadVolumeSamples)
            {
                int j = Mathf.Min(path.Count - 1, i + RoadVolumeSamples);
                // 이웃 상자와 겹치게 길이에 여유를 둔다.
                navBox(root, "Road", NavAreaId.Road, path[i], path[j], RoadHalfWidths[r] * 2.0f, 2.0f);
            }
        }

        // 호수: 원을 가로 띠 상자로 채운다 (상자는 원보다 크면 물가까지 막으므로 줄마다 현 길이만큼).
        const float strip = 4.0f;
        float lakeRadius = WorldRegions.LakeRadius + 1.0f;
        for (float dz = -lakeRadius; dz < lakeRadius; dz += strip)
        {
            float mid = dz + strip * 0.5f;
            float chord = Mathf.Sqrt(Mathf.Max(0.0f, lakeRadius * lakeRadius - mid * mid)) * 2.0f;
            if (chord < 1.0f)
            {
                continue;
            }

            var center = new Vector3(WorldRegions.Lake.x, WorldRegions.WaterLevel, WorldRegions.Lake.y + mid);
            navBox(root, "Lake", NavAreaId.NotWalkable, center - Vector3.right * chord * 0.5f, center + Vector3.right * chord * 0.5f, strip, 0.0f);
        }

        if (RiverPath != null)
        {
            for (int i = 0; i < RiverPath.Count - 1; i += 2)
            {
                int j = Mathf.Min(RiverPath.Count - 1, i + 2);
                navBox(root, "River", NavAreaId.NotWalkable, RiverPath[i], RiverPath[j], RiverHalfWidth[i] * 2.0f + 1.0f, 1.0f);
            }
        }

        // 가장자리 산 띠: 산자락 바위 · 숲은 배경이다.
        const float border = 50.0f;
        float half = Size * 0.5f;
        navBorder(root, new Vector3(half, 0.0f, border * 0.5f), new Vector3(Size, 0.0f, border));
        navBorder(root, new Vector3(half, 0.0f, Size - border * 0.5f), new Vector3(Size, 0.0f, border));
        navBorder(root, new Vector3(border * 0.5f, 0.0f, half), new Vector3(border, 0.0f, Size));
        navBorder(root, new Vector3(Size - border * 0.5f, 0.0f, half), new Vector3(border, 0.0f, Size));
    }

    /// <summary>InFrom → InTo 를 덮는 회전 상자 (높이 30m: 지형 기복을 모두 포함).</summary>
    private static void navBox(Transform InParent, string InName, int InArea, Vector3 InFrom, Vector3 InTo, float InWidth, float InExtraLength)
    {
        var flat = new Vector3(InTo.x - InFrom.x, 0.0f, InTo.z - InFrom.z);
        if (flat.sqrMagnitude < 0.01f)
        {
            return;
        }

        var go = new GameObject(InName);
        go.transform.SetParent(InParent, false);
        go.transform.SetPositionAndRotation((InFrom + InTo) * 0.5f, Quaternion.LookRotation(flat.normalized, Vector3.up));
        var volume = go.AddComponent<NavMeshModifierVolume>();
        volume.area = InArea;
        volume.center = Vector3.zero;
        volume.size = new Vector3(InWidth, 30.0f, flat.magnitude + InExtraLength);
    }

    private static void navBorder(Transform InParent, Vector3 InCenter, Vector3 InSize)
    {
        var go = new GameObject("Border");
        go.transform.SetParent(InParent, false);
        go.transform.position = new Vector3(InCenter.x, MaxHeight * 0.5f, InCenter.z);
        var volume = go.AddComponent<NavMeshModifierVolume>();
        volume.area = NavAreaId.NotWalkable;
        volume.size = new Vector3(InSize.x, MaxHeight + 20.0f, InSize.z);
    }
}
