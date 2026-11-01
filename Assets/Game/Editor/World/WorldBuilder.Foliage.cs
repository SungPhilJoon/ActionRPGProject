using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 장식 나무/덤불 배치 (절차 나무 TreeFactory, GameObject + LODGroup).
/// Terrain Tree 대신 GameObject 로 두는 이유: 나무마다 카메라 가림 숨김(CameraOccluder)과 건축 시 제거(FoliageInstance)가 필요하다.
/// 분포 (레퍼런스 Documentation/images/WorldMap): 노이즈 숲 덩어리 + Dunley Woods + 산자락 침엽수림, 초원엔 드문드문 활엽수,
/// 물가엔 자작나무, 습지·동쪽 건조지대엔 고사목. 스폰/캠프 평탄화 구역과 물 위는 비운다.
/// 2026-10-31 리디자인: 숲 밀도를 크게 낮추고(폐허가 주인공) 길 · 폐허 둘레는 비운다. 가장자리 산자락 숲(걸을 수 없는 띠)은 유지.
/// </summary>
public static partial class WorldBuilder
{
    private const float TreeSpacing = 4.2f;
    private const float BushSpacing = 5.5f;
    private const float FoliageChunk = 64.0f;

    private static FoliageRoot buildFoliage(Terrain InTerrain, List<FlatZone> InZones)
    {
        var rootObject = new GameObject("[Foliage]");
        rootObject.isStatic = true;
        var root = rootObject.AddComponent<FoliageRoot>();
        var chunks = new Dictionary<int, Transform>();
        var instances = new List<FoliageInstance>();
        var random = new System.Random(Seed + 21);
        var data = InTerrain.terrainData;

        var species = new List<GameObject>[(int)ETreeSpecies.Dead + 1];
        for (int s = 0; s < species.Length; ++s)
        {
            species[s] = TreeFactory.GetVariants((ETreeSpecies)s);
        }

        var weights = new float[species.Length];
        int cells = Mathf.FloorToInt(Size / TreeSpacing);
        for (int gz = 0; gz < cells; ++gz)
        {
            for (int gx = 0; gx < cells; ++gx)
            {
                float x = (gx + (float)random.NextDouble()) * TreeSpacing;
                float z = (gz + (float)random.NextDouble()) * TreeSpacing;
                double roll = random.NextDouble();
                double pick = random.NextDouble();
                var p = new Vector2(x, z);
                float height;
                if (canPlant(data, InZones, p, 3.0f, 40.0f, 84.0f, out height) == false)
                {
                    continue;
                }

                float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
                float mountain = Mathf.Clamp01((95.0f - edge) / 45.0f);
                float forest = Mathf.Clamp01((fbm(x * 0.011f + 7.0f, z * 0.011f + 3.0f, 3) - 0.44f) / 0.12f);
                float woods = Mathf.Clamp01(1.0f - Vector2.Distance(p, WorldRegions.DunleyWoods) / 95.0f);
                float wet = waterProximity(p, 14.0f);
                float marsh = Mathf.Clamp01(1.0f - Vector2.Distance(p, WorldRegions.SerpentMarsh) / 60.0f);
                float dryEast = Mathf.Clamp01((x - 310.0f) / 80.0f);
                float alpine = Mathf.Clamp01((height - 30.0f) / 25.0f);

                float density = Mathf.Max(Mathf.Max(forest * 0.3f, woods * 0.45f), Mathf.Max(mountain * 0.6f, wet * 0.18f)) + 0.012f;
                if (roll > density)
                {
                    continue;
                }

                weights[(int)ETreeSpecies.Spruce] = 0.25f + mountain * 1.6f + alpine * 2.0f + dryEast * 0.4f;
                weights[(int)ETreeSpecies.Broadleaf] = (1.0f - mountain) * (1.0f - alpine) * (0.9f + woods * 0.6f);
                weights[(int)ETreeSpecies.Birch] = 0.12f + wet * 1.4f + marsh * 0.4f;
                weights[(int)ETreeSpecies.Dead] = 0.03f + dryEast * 0.08f + marsh * 0.2f;
                int chosen = pickWeighted(weights, (float)pick);
                var variants = species[chosen];
                if (variants.Count == 0)
                {
                    continue;
                }

                var prefab = variants[random.Next(variants.Count)];
                float scale = (0.78f + (float)random.NextDouble() * 0.45f) * (1.0f - alpine * 0.25f);
                instances.Add(plantFoliage(InTerrain, chunks, rootObject.transform, prefab, p, scale, (float)random.NextDouble() * 360.0f));
            }
        }

        int treeCount = instances.Count;
        int bushCells = Mathf.FloorToInt(Size / BushSpacing);
        for (int gz = 0; gz < bushCells; ++gz)
        {
            for (int gx = 0; gx < bushCells; ++gx)
            {
                float x = (gx + (float)random.NextDouble()) * BushSpacing;
                float z = (gz + (float)random.NextDouble()) * BushSpacing;
                double roll = random.NextDouble();
                var p = new Vector2(x, z);
                float height;
                if (canPlant(data, InZones, p, 1.5f, 30.0f, 70.0f, out height) == false)
                {
                    continue;
                }

                float forest = Mathf.Clamp01((fbm(x * 0.011f + 7.0f, z * 0.011f + 3.0f, 3) - 0.42f) / 0.14f);
                float edgeOfForest = forest * (1.0f - forest) * 4.0f;
                float dryEast = Mathf.Clamp01((x - 300.0f) / 80.0f);
                if (roll > 0.05f + edgeOfForest * 0.3f)
                {
                    continue;
                }

                // 0,1 = 초록 덤불, 2,3 = 마른 덤불
                int index = random.NextDouble() < dryEast * 0.8f ? 2 + random.Next(2) : random.Next(2);
                var prefab = TreeFactory.GetBush(Mathf.Min(index, TreeFactory.BushCount - 1));
                if (prefab != null)
                {
                    instances.Add(plantFoliage(InTerrain, chunks, rootObject.transform, prefab, p, 0.8f + (float)random.NextDouble() * 0.6f, (float)random.NextDouble() * 360.0f));
                }
            }
        }

        root.Instances = instances.ToArray();
        Debug.Log($"[World] foliage: trees {treeCount}, bushes {instances.Count - treeCount}");
        return root;
    }

    /// <summary>나무를 심을 수 있는지: 경사/고도 제한, 물 위, 스폰·캠프 평탄화 구역, 맵 가장자리.</summary>
    private static bool canPlant(TerrainData InData, List<FlatZone> InZones, Vector2 InPoint, float InClearance, float InMaxSteep, float InMaxHeight, out float OutHeight)
    {
        OutHeight = 0.0f;
        if (InPoint.x < 3.0f || InPoint.y < 3.0f || InPoint.x > Size - 3.0f || InPoint.y > Size - 3.0f)
        {
            return false;
        }

        float nx = InPoint.x / Size;
        float nz = InPoint.y / Size;
        OutHeight = InData.GetInterpolatedHeight(nx, nz);
        if (InData.GetSteepness(nx, nz) > InMaxSteep || OutHeight > InMaxHeight || OutHeight < WorldRegions.WaterLevel + 0.6f)
        {
            return false;
        }

        if (isWater(InPoint, InClearance) == true)
        {
            return false;
        }

        if (clearOfRoads(InPoint, InClearance + 1.5f) == false || clearOfRuins(InPoint, InClearance) == false)
        {
            return false;
        }

        foreach (var zone in InZones)
        {
            if (Vector2.Distance(InPoint, zone.Center) < zone.Radius + InClearance)
            {
                return false;
            }
        }

        return true;
    }

    private static int pickWeighted(float[] InWeights, float InRoll)
    {
        float total = 0.0f;
        for (int i = 0; i < InWeights.Length; ++i)
        {
            total += InWeights[i];
        }

        float target = InRoll * total;
        for (int i = 0; i < InWeights.Length; ++i)
        {
            target -= InWeights[i];
            if (target <= 0.0f)
            {
                return i;
            }
        }

        return InWeights.Length - 1;
    }

    private static FoliageInstance plantFoliage(Terrain InTerrain, Dictionary<int, Transform> InChunks, Transform InRoot, GameObject InPrefab, Vector2 InPoint, float InScale, float InYaw)
    {
        int cx = Mathf.FloorToInt(InPoint.x / FoliageChunk);
        int cz = Mathf.FloorToInt(InPoint.y / FoliageChunk);
        int key = cx * 1000 + cz;
        Transform chunk;
        if (InChunks.TryGetValue(key, out chunk) == false)
        {
            chunk = new GameObject($"Chunk_{cx}_{cz}").transform;
            chunk.SetParent(InRoot, false);
            InChunks.Add(key, chunk);
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(InPrefab, chunk);
        // 밑동이 경사면에서 뜨지 않게 살짝 묻는다.
        instance.transform.position = onGround(InTerrain, InPoint) + Vector3.down * 0.2f;
        instance.transform.rotation = Quaternion.Euler(0.0f, InYaw, 0.0f);
        instance.transform.localScale = Vector3.one * InScale;
        var foliage = instance.GetComponent<FoliageInstance>();
        foliage.Radius *= InScale;
        return foliage;
    }
}
