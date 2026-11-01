using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 폐허: 나무 대신 맵의 주된 볼거리 (2026-10-31 리디자인). ASTROFISH 석조 부품(아치 · 지하 벽 · 모서리 벽 · 기둥)과 반쯤 묻힌 건물로 만든다.
///  - 폐허 무리(RuinCount): 길 · 물 · 스폰 구역 · 지역 랜드마크 · 산 띠를 피해 고르게 흩뿌린다. 모양 5종(무너진 벽 / 성문 / 무너진 집 / 열주 / 탑 잔해)
///  - 길가 이정표: 길을 따라 RoadMarkerSpacing 마다 길 옆에 기둥 · 아치 · 벽 조각 → 길이 눈에 띈다
/// 위치는 지형을 만들기 전에 정해(planRuins) 나무가 피하고(RuinSites) 바닥을 흙으로 칠한다. 모든 부품은 콜라이더가 있어 그늘 · NavMesh 장애물이 된다.
/// 블록아웃(MCastle_Ruins)은 UV 가 없어 쓰지 않는다.
/// </summary>
public static partial class WorldBuilder
{
    private const string Masonry = Astro + "/_Assets/Masonary/Prfbs/";
    private const int RuinCount = 34;
    private const float RuinSpacing = 26.0f;
    private const float RuinClearRadius = 10.0f;
    private const float RoadMarkerSpacing = 42.0f;

    private enum ERuinShape
    {
        BrokenWall,
        Gate,
        CollapsedHouse,
        Colonnade,
        Tower,
        Count,
    }

    private struct RuinSite
    {
        public Vector2 Center;
        public float Yaw;
        public ERuinShape Shape;
        /// <summary>길가 이정표 (한 조각, 길 쪽을 향함).</summary>
        public bool Marker;
    }

    private static readonly string[] SunkenHouses =
    {
        "/Medieval_Building_House1.prefab", "/Medieval_Building_House2.prefab", "/Medieval_Building_Composite_B.prefab", "/Medieval_Building_Composite_E.prefab",
    };

    private static readonly string[] Towers = { "/Medieval_Building_StoneLookoutTower.prefab", "/Medieval_Building_RepunTower.prefab", "/Medieval_Building_StrangeTower.prefab" };

    /// <summary>폐허 위치 (지형 생성 전에 정한다).</summary>
    private static List<RuinSite> RuinSites;

    /// <summary>랜드마크가 있는 지역 중심과 반경 — 폐허 무리를 두지 않는다.</summary>
    private static readonly Vector4[] LandmarkAreas =
    {
        new Vector4(300.0f, 215.0f, 44.0f, 0.0f), new Vector4(250.0f, 385.0f, 42.0f, 0.0f),
        new Vector4(390.0f, 430.0f, 36.0f, 0.0f), new Vector4(256.0f, 96.0f, 28.0f, 0.0f),
    };

    private static void planRuins(List<FlatZone> InZones)
    {
        var sites = new List<RuinSite>();
        var random = new System.Random(Seed + 77);

        // 길가 이정표: 길 샘플을 따라 일정 간격, 좌우 번갈아.
        int side = 1;
        for (int r = 0; r < RoadPaths.Count; ++r)
        {
            var path = RoadPaths[r];
            float walked = RoadMarkerSpacing * 0.5f;
            for (int i = 1; i < path.Count; ++i)
            {
                var a = new Vector2(path[i - 1].x, path[i - 1].z);
                var b = new Vector2(path[i].x, path[i].z);
                walked += Vector2.Distance(a, b);
                if (walked < RoadMarkerSpacing)
                {
                    continue;
                }

                walked = 0.0f;
                var dir = (b - a).normalized;
                var normal = new Vector2(-dir.y, dir.x) * side;
                side = -side;
                var p = b + normal * (RoadHalfWidths[r] + 4.0f);
                if (isRuinSpotFree(p, InZones, sites, 8.0f, 2.5f) == false)
                {
                    continue;
                }

                // 길 쪽을 바라보게.
                float yaw = Mathf.Atan2(-normal.x, -normal.y) * Mathf.Rad2Deg;
                sites.Add(new RuinSite { Center = p, Yaw = yaw, Shape = random.Next(3) == 0 ? ERuinShape.Gate : ERuinShape.Colonnade, Marker = true });
            }
        }

        // 폐허 무리: 후보를 뽑아 조건을 만족하면 채택.
        for (int attempt = 0; attempt < 4000 && countClusters(sites) < RuinCount; ++attempt)
        {
            var p = new Vector2(60.0f + (float)random.NextDouble() * (Size - 120.0f), 60.0f + (float)random.NextDouble() * (Size - 120.0f));
            if (isRuinSpotFree(p, InZones, sites, RuinSpacing, 6.0f) == false || slopeAt(p, InZones) > 0.3f)
            {
                continue;
            }

            bool nearLandmark = false;
            foreach (var area in LandmarkAreas)
            {
                nearLandmark |= Vector2.Distance(p, new Vector2(area.x, area.y)) < area.z;
            }

            if (nearLandmark == true)
            {
                continue;
            }

            var shape = (ERuinShape)random.Next((int)ERuinShape.Count);
            sites.Add(new RuinSite { Center = p, Yaw = (float)random.NextDouble() * 360.0f, Shape = shape, Marker = false });
        }

        RuinSites = sites;
        Debug.Log($"[World] ruins planned: {countClusters(sites)} clusters, {sites.Count - countClusters(sites)} road markers");
    }

    private static int countClusters(List<RuinSite> InSites)
    {
        int count = 0;
        foreach (var site in InSites)
        {
            count += site.Marker == true ? 0 : 1;
        }

        return count;
    }

    private static bool isRuinSpotFree(Vector2 InPoint, List<FlatZone> InZones, List<RuinSite> InSites, float InSpacing, float InRoadClearance)
    {
        float edge = Mathf.Min(Mathf.Min(InPoint.x, Size - InPoint.x), Mathf.Min(InPoint.y, Size - InPoint.y));
        if (edge < 62.0f || isWater(InPoint, 8.0f) == true || clearOfRoads(InPoint, InRoadClearance) == false)
        {
            return false;
        }

        foreach (var zone in InZones)
        {
            if (Vector2.Distance(InPoint, zone.Center) < zone.Radius + 4.0f)
            {
                return false;
            }
        }

        foreach (var site in InSites)
        {
            if (Vector2.Distance(InPoint, site.Center) < InSpacing)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>지형 기울기 근사 (높이 차 / 거리) — 지형이 아직 없어서 sampleHeight 로 잰다.</summary>
    private static float slopeAt(Vector2 InPoint, List<FlatZone> InZones)
    {
        const float d = 4.0f;
        float dx = sampleHeight(InPoint.x + d, InPoint.y, InZones) - sampleHeight(InPoint.x - d, InPoint.y, InZones);
        float dz = sampleHeight(InPoint.x, InPoint.y + d, InZones) - sampleHeight(InPoint.x, InPoint.y - d, InZones);
        return Mathf.Sqrt(dx * dx + dz * dz) / (2.0f * d);
    }

    /// <summary>폐허 바닥 흙 가중치 (지형 레이어).</summary>
    private static float ruinGroundWeight(Vector2 InPoint)
    {
        if (RuinSites == null)
        {
            return 0.0f;
        }

        float best = 0.0f;
        foreach (var site in RuinSites)
        {
            if (site.Marker == true)
            {
                continue;
            }

            best = Mathf.Max(best, 1.0f - Mathf.Clamp01(Vector2.Distance(InPoint, site.Center) / RuinClearRadius));
        }

        return best;
    }

    /// <summary>나무 · 덤불이 폐허를 덮지 않게.</summary>
    private static bool clearOfRuins(Vector2 InPoint, float InClearance)
    {
        if (RuinSites == null)
        {
            return true;
        }

        foreach (var site in RuinSites)
        {
            float radius = site.Marker == true ? 3.0f : RuinClearRadius;
            if (Vector2.Distance(InPoint, site.Center) < radius + InClearance)
            {
                return false;
            }
        }

        return true;
    }

    // ------------------------------------------------------------------ build

    private static void buildRuins(Terrain InTerrain)
    {
        var root = group("Ruins");
        var random = new System.Random(Seed + 78);
        foreach (var site in RuinSites)
        {
            var cluster = new GameObject(site.Marker == true ? "RoadMarker" : $"Ruin_{site.Shape}").transform;
            cluster.SetParent(root, false);
            if (site.Marker == true)
            {
                buildRoadMarker(InTerrain, cluster, site, random);
                continue;
            }

            switch (site.Shape)
            {
                case ERuinShape.BrokenWall:
                    buildBrokenWall(InTerrain, cluster, site, random);
                    break;
                case ERuinShape.Gate:
                    buildGate(InTerrain, cluster, site, random);
                    break;
                case ERuinShape.CollapsedHouse:
                    buildCollapsedHouse(InTerrain, cluster, site, random);
                    break;
                case ERuinShape.Colonnade:
                    buildColonnade(InTerrain, cluster, site, random, 5);
                    break;
                default:
                    buildTowerRuin(InTerrain, cluster, site, random);
                    break;
            }

            scatterRubble(InTerrain, cluster, site.Center, random, 3 + random.Next(3));
        }
    }

    /// <summary>폐허 로컬 좌표 (yaw 회전) → 월드 XZ.</summary>
    private static Vector2 ruinPoint(RuinSite InSite, float InX, float InZ)
    {
        var offset = Quaternion.Euler(0.0f, InSite.Yaw, 0.0f) * new Vector3(InX, 0.0f, InZ);
        return InSite.Center + new Vector2(offset.x, offset.z);
    }

    private static void buildBrokenWall(Terrain InTerrain, Transform InParent, RuinSite InSite, System.Random InRandom)
    {
        // 지하 벽 3~4 조각을 줄지어, 하나는 빠지고 하나는 낮게 묻는다 → 무너진 성벽.
        int pieces = 3 + InRandom.Next(2);
        int gap = InRandom.Next(pieces);
        for (int i = 0; i < pieces; ++i)
        {
            if (i == gap)
            {
                continue;
            }

            float sink = InRandom.NextDouble() < 0.3 ? 0.9f : 0.1f;
            place(InTerrain, InParent, Masonry + "Medieval_Masonary_CellarWall.prefab", ruinPoint(InSite, (i - pieces * 0.5f) * 2.9f, 0.0f), InSite.Yaw, 1.3f, sink, true);
        }

        place(InTerrain, InParent, Masonry + "Medieval_Masonary_WallCorner.prefab", ruinPoint(InSite, pieces * 1.5f + 0.5f, 1.2f), InSite.Yaw + 90.0f, 1.1f, 0.1f, true);
    }

    private static void buildGate(Terrain InTerrain, Transform InParent, RuinSite InSite, System.Random InRandom)
    {
        place(InTerrain, InParent, Masonry + "Medieval_Masonary_WallDoorArch.prefab", ruinPoint(InSite, 0.0f, 0.0f), InSite.Yaw, 1.4f, 0.1f, true);
        place(InTerrain, InParent, Masonry + "Medieval_Masonary_ShortSideWall_A.prefab", ruinPoint(InSite, -3.6f, 0.0f), InSite.Yaw, 1.3f, 0.2f, true);
        if (InRandom.NextDouble() < 0.6)
        {
            place(InTerrain, InParent, Masonry + "Medieval_Masonary_ShortSideWall_B.prefab", ruinPoint(InSite, 3.6f, 0.0f), InSite.Yaw, 1.3f, 0.6f, true);
        }
    }

    private static void buildCollapsedHouse(Terrain InTerrain, Transform InParent, RuinSite InSite, System.Random InRandom)
    {
        // 집을 깊이 묻어 지붕 · 윗층만 남은 것처럼, 옆에 무너진 벽.
        string house = SunkenHouses[InRandom.Next(SunkenHouses.Length)];
        place(InTerrain, InParent, AstroBuildings + house, InSite.Center, InSite.Yaw, 1.0f, 2.4f, false);
        place(InTerrain, InParent, Masonry + "Medieval_Masonary_CellarWall.prefab", ruinPoint(InSite, 6.5f, -3.0f), InSite.Yaw + 15.0f, 1.2f, 0.3f, true);
        place(InTerrain, InParent, Astro + "/_Assets/Fences/Prfbs/Medieval_Fences_Broken.prefab", ruinPoint(InSite, -6.0f, 4.0f), InSite.Yaw - 30.0f, 1.0f, 0.0f, true);
        if (InRandom.NextDouble() < 0.5)
        {
            place(InTerrain, InParent, Astro + "/_Assets/Containers/Prfbs/Medieval_Barrel.prefab", ruinPoint(InSite, 4.0f, 4.5f), (float)InRandom.NextDouble() * 360.0f, 1.0f, 0.0f, true);
        }
    }

    private static void buildColonnade(Terrain InTerrain, Transform InParent, RuinSite InSite, System.Random InRandom, int InCount)
    {
        // 기둥 줄: 일부는 빠지고 일부는 반쯤 묻혀 부러진 것처럼.
        string column = Astro + "/_Assets/PltfrmSprts/Prfbs/Medieval_PortchColumn_Medium.prefab";
        for (int i = 0; i < InCount; ++i)
        {
            if (InRandom.NextDouble() < 0.25)
            {
                continue;
            }

            float sink = InRandom.NextDouble() < 0.4 ? 1.1f : 0.0f;
            place(InTerrain, InParent, column, ruinPoint(InSite, (i - (InCount - 1) * 0.5f) * 3.0f, 0.0f), InSite.Yaw, 1.2f, sink, true);
        }
    }

    private static void buildTowerRuin(Terrain InTerrain, Transform InParent, RuinSite InSite, System.Random InRandom)
    {
        string tower = Towers[InRandom.Next(Towers.Length)];
        place(InTerrain, InParent, AstroBuildings + tower, InSite.Center, InSite.Yaw, 1.0f, 3.0f, false);
        place(InTerrain, InParent, Masonry + "Medieval_Masonary_WallCorner_Cropped.prefab", ruinPoint(InSite, -5.5f, 3.0f), InSite.Yaw + 180.0f, 1.2f, 0.2f, true);
    }

    private static void buildRoadMarker(Terrain InTerrain, Transform InParent, RuinSite InSite, System.Random InRandom)
    {
        if (InSite.Shape == ERuinShape.Gate)
        {
            place(InTerrain, InParent, Masonry + "Medieval_Masonary_WallDoorArch.prefab", InSite.Center, InSite.Yaw + 90.0f, 1.0f, 0.4f, true);
            return;
        }

        buildColonnade(InTerrain, InParent, InSite, InRandom, 2);
    }

    private static void scatterRubble(Terrain InTerrain, Transform InParent, Vector2 InCenter, System.Random InRandom, int InCount)
    {
        for (int i = 0; i < InCount; ++i)
        {
            double angle = InRandom.NextDouble() * Mathf.PI * 2.0;
            double radius = 3.0 + InRandom.NextDouble() * 6.0;
            var p = InCenter + new Vector2((float)(System.Math.Cos(angle) * radius), (float)(System.Math.Sin(angle) * radius));
            string name = SmallRocks[InRandom.Next(SmallRocks.Length)];
            place(InTerrain, InParent, $"{Tales}/{name}.prefab", p, (float)InRandom.NextDouble() * 360.0f, 0.18f + (float)InRandom.NextDouble() * 0.2f, 0.2f, true);
        }
    }
}
