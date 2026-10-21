using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>지형: 높이, 레이어 페인팅, 디테일(풀), 장식 나무.</summary>
public static partial class WorldBuilder
{
    private const string TerrainSample = External + "/TerrainSampleAssets";

    /// <summary>레이어 인덱스 (아래 createLayers 순서와 같다).</summary>
    private const int LayerGrass = 0;
    private const int LayerMoss = 1;
    private const int LayerSoil = 2;
    private const int LayerRock = 3;
    private const int LayerDry = 4;
    private const int LayerMud = 5;
    private const int LayerSand = 6;
    private const int LayerSnow = 7;
    private const int LayerCount = 8;

    private static float[,] Erosion;

    private static Terrain buildTerrain(List<FlatZone> InZones)
    {
        string dataPath = EditorPaths.Generated + "/World/TerrainData.asset";
        AssetDatabase.DeleteAsset(dataPath);
        var data = new TerrainData();
        data.heightmapResolution = HeightRes;
        data.size = new Vector3(Size, MaxHeight, Size);
        // Unity 2022.2+ 기본값은 Coverage 모드(0~255 비율)라서 인스턴스 개수(1~4)를 쓰는 이 빌더와 맞지 않는다 (Issues.md 19).
        data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
        data.SetDetailResolution(512, 16);
        data.alphamapResolution = 512;
        data.baseMapResolution = 1024;

        Erosion = readHeightmap(TerrainSample + "/Textures/Heightmaps/Errosion01_Heightmap.tif", 256);

        var heights = new float[HeightRes, HeightRes];
        for (int y = 0; y < HeightRes; ++y)
        {
            for (int x = 0; x < HeightRes; ++x)
            {
                float wx = (float)x / (HeightRes - 1) * Size;
                float wz = (float)y / (HeightRes - 1) * Size;
                heights[y, x] = Mathf.Clamp01(sampleHeight(wx, wz, InZones) / MaxHeight);
            }
        }

        data.SetHeights(0, 0, heights);
        data.terrainLayers = createLayers();
        AssetDatabase.CreateAsset(data, dataPath);

        paintSplat(data);
        addDetails(data);
        addTrees(data, InZones);

        var go = Terrain.CreateTerrainGameObject(data);
        go.name = "Terrain";
        go.layer = LayerId.Ground;
        go.isStatic = true;
        var terrain = go.GetComponent<Terrain>();
        terrain.drawInstanced = true;
        terrain.detailObjectDistance = 70.0f;
        terrain.detailObjectDensity = 0.8f;
        terrain.treeDistance = 400.0f;
        terrain.treeBillboardDistance = 450.0f;
        terrain.heightmapPixelError = 5.0f;
        terrain.basemapDistance = 250.0f;
        terrain.shadowCastingMode = ShadowCastingMode.TwoSided;
        return terrain;
    }

    /// <summary>
    /// 월드 좌표 높이(m).
    ///  기본 8m + FBM 언덕 + 동쪽 고원(+8m) + 가장자리 산맥(TerrainSampleAssets 침식 높이맵으로 능선 디테일)
    ///  호수 분지는 수면(4m) 아래로 파고, 스폰 지역은 주변 높이로 평탄화.
    /// </summary>
    private static float sampleHeight(float InX, float InZ, List<FlatZone> InZones)
    {
        float hills = fbm(InX * 0.008f, InZ * 0.008f, 4) * 14.0f - 4.0f;
        float detail = (Mathf.PerlinNoise(InX * 0.05f + 31.0f, InZ * 0.05f) - 0.5f) * 1.6f;

        float highland = Mathf.Clamp01((InX - 320.0f) / 60.0f) * 8.0f;

        float edge = Mathf.Min(Mathf.Min(InX, Size - InX), Mathf.Min(InZ, Size - InZ));
        float mountainWeight = Mathf.Clamp01((70.0f - edge) / 55.0f);
        mountainWeight = mountainWeight * mountainWeight * (3.0f - 2.0f * mountainWeight);
        float erosion = sampleErosion(InX / Size, InZ / Size);
        float mountain = mountainWeight * (35.0f + erosion * 60.0f + fbm(InX * 0.02f, InZ * 0.02f, 3) * 15.0f);

        float height = WorldRegions.BaseHeight + Mathf.Max(-3.0f, hills) + detail + highland + mountain;

        // 호수 분지
        float lakeDistance = Vector2.Distance(new Vector2(InX, InZ), WorldRegions.Lake);
        if (lakeDistance < WorldRegions.LakeRadius + 14.0f)
        {
            float t = Mathf.Clamp01((lakeDistance - WorldRegions.LakeRadius * 0.5f) / (WorldRegions.LakeRadius + 14.0f - WorldRegions.LakeRadius * 0.5f));
            float bed = WorldRegions.WaterLevel - 3.0f;
            height = Mathf.Lerp(bed, height, t * t);
        }

        // 평탄화 구역 (스폰·캠프). Height < 0 이면 기본 높이.
        float flatWeight = 0.0f;
        float flatTarget = WorldRegions.BaseHeight + 1.0f;
        foreach (var zone in InZones)
        {
            float d = Vector2.Distance(new Vector2(InX, InZ), zone.Center);
            float w = 1.0f - Mathf.Clamp01((d - zone.Radius) / 18.0f);
            if (w > flatWeight)
            {
                flatWeight = w;
                flatTarget = zone.Height > 0.0f ? zone.Height : WorldRegions.BaseHeight + 1.0f + (zone.Center.x > 330.0f ? 6.0f : 0.0f);
            }
        }

        flatWeight *= 1.0f - mountainWeight;
        return Mathf.Lerp(height, flatTarget + detail * 0.3f, flatWeight);
    }

    private static float fbm(float InX, float InY, int InOctaves)
    {
        float value = 0.0f;
        float amplitude = 0.5f;
        float frequency = 1.0f;
        for (int i = 0; i < InOctaves; ++i)
        {
            value += Mathf.PerlinNoise(InX * frequency + Seed * 0.13f, InY * frequency + Seed * 0.07f) * amplitude;
            frequency *= 2.0f;
            amplitude *= 0.5f;
        }

        return value;
    }

    private static float sampleErosion(float InU, float InV)
    {
        if (Erosion == null)
        {
            return 0.5f;
        }

        int res = Erosion.GetLength(0);
        float fx = Mathf.Repeat(InU * 1.7f, 1.0f) * (res - 1);
        float fy = Mathf.Repeat(InV * 1.7f, 1.0f) * (res - 1);
        int x0 = Mathf.FloorToInt(fx);
        int y0 = Mathf.FloorToInt(fy);
        int x1 = Mathf.Min(x0 + 1, res - 1);
        int y1 = Mathf.Min(y0 + 1, res - 1);
        float tx = fx - x0;
        float ty = fy - y0;
        float a = Mathf.Lerp(Erosion[y0, x0], Erosion[y0, x1], tx);
        float b = Mathf.Lerp(Erosion[y1, x0], Erosion[y1, x1], tx);
        return Mathf.Lerp(a, b, ty);
    }

    /// <summary>외부 높이맵(TIF)을 읽기 가능 설정 변경 없이 GPU Blit → ReadPixels 로 읽는다.</summary>
    private static float[,] readHeightmap(string InPath, int InResolution)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(InPath);
        if (texture == null)
        {
            Debug.LogWarning("[World] heightmap missing: " + InPath);
            return null;
        }

        var rt = RenderTexture.GetTemporary(InResolution, InResolution, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        Graphics.Blit(texture, rt);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        var readable = new Texture2D(InResolution, InResolution, TextureFormat.RGBAFloat, false, true);
        readable.ReadPixels(new Rect(0, 0, InResolution, InResolution), 0, 0);
        readable.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);

        var pixels = readable.GetPixels();
        Object.DestroyImmediate(readable);
        var result = new float[InResolution, InResolution];
        float min = float.MaxValue;
        float max = float.MinValue;
        for (int i = 0; i < pixels.Length; ++i)
        {
            min = Mathf.Min(min, pixels[i].r);
            max = Mathf.Max(max, pixels[i].r);
        }

        float range = Mathf.Max(0.0001f, max - min);
        for (int y = 0; y < InResolution; ++y)
        {
            for (int x = 0; x < InResolution; ++x)
            {
                result[y, x] = (pixels[y * InResolution + x].r - min) / range;
            }
        }

        return result;
    }

    private static TerrainLayer[] createLayers()
    {
        string folder = TerrainSample + "/TerrainLayers/";
        string[] names = { "Grass_A", "Grass_Moss", "Soil_Rocks", "Rock", "Grass_Dry", "Muddy", "Sand", "Snow" };
        var layers = new TerrainLayer[names.Length];
        for (int i = 0; i < names.Length; ++i)
        {
            layers[i] = AssetDatabase.LoadAssetAtPath<TerrainLayer>(folder + names[i] + "_TerrainLayer.terrainlayer");
            if (layers[i] == null)
            {
                Debug.LogWarning("[World] terrain layer missing: " + names[i]);
            }
        }

        return layers;
    }

    private static void paintSplat(TerrainData InData)
    {
        int res = InData.alphamapResolution;
        var maps = new float[res, res, LayerCount];
        var weights = new float[LayerCount];
        for (int y = 0; y < res; ++y)
        {
            for (int x = 0; x < res; ++x)
            {
                float nx = (float)x / (res - 1);
                float nz = (float)y / (res - 1);
                float steep = InData.GetSteepness(nx, nz);
                float height = InData.GetInterpolatedHeight(nx, nz);
                float wx = nx * Size;
                float wz = nz * Size;
                var p = new Vector2(wx, wz);
                float noise = Mathf.PerlinNoise(wx * 0.04f, wz * 0.04f);

                for (int i = 0; i < LayerCount; ++i)
                {
                    weights[i] = 0.0f;
                }

                weights[LayerGrass] = 1.0f;
                weights[LayerMoss] = Mathf.Clamp01((1.0f - Vector2.Distance(p, WorldRegions.DunleyWoods) / 120.0f) * 1.6f) * (0.5f + noise);
                weights[LayerDry] = Mathf.Clamp01((wx - 300.0f) / 80.0f) * (0.6f + noise * 0.6f);
                weights[LayerMud] = Mathf.Clamp01((1.0f - Vector2.Distance(p, WorldRegions.SerpentMarsh) / 70.0f) * 1.8f) * noise * 1.5f;

                float lakeDistance = Vector2.Distance(p, WorldRegions.Lake);
                weights[LayerSand] = Mathf.Clamp01(1.0f - Mathf.Abs(lakeDistance - WorldRegions.LakeRadius) / 7.0f) + (height < WorldRegions.WaterLevel + 0.5f ? 1.0f : 0.0f);

                float camp = Mathf.Max(campWeight(p, WorldRegions.RuinedHamlet, 30.0f), campWeight(p, WorldRegions.TrollRidge, 26.0f));
                camp = Mathf.Max(camp, campWeight(p, WorldRegions.MinotaurArena, 28.0f));
                camp = Mathf.Max(camp, campWeight(p, WorldRegions.StartGlade, 14.0f) * 0.6f);
                weights[LayerSoil] = camp * (0.6f + noise * 0.8f) + Mathf.Clamp01((steep - 14.0f) / 10.0f) * 0.5f;

                weights[LayerRock] = Mathf.Clamp01((steep - 26.0f) / 10.0f) * 3.0f;
                weights[LayerSnow] = Mathf.Clamp01((height - 78.0f) / 12.0f) * (1.0f - Mathf.Clamp01((steep - 40.0f) / 10.0f)) * 3.0f;

                float sum = 0.0f;
                for (int i = 0; i < LayerCount; ++i)
                {
                    sum += weights[i];
                }

                for (int i = 0; i < LayerCount; ++i)
                {
                    maps[y, x, i] = weights[i] / sum;
                }
            }
        }

        InData.SetAlphamaps(0, 0, maps);
    }

    private static float campWeight(Vector2 InPoint, Vector2 InCenter, float InRadius)
    {
        return Mathf.Clamp01(1.0f - Vector2.Distance(InPoint, InCenter) / InRadius);
    }

    private static void addDetails(TerrainData InData)
    {
        // (프리팹, 최소/최대 크기, 놓일 레이어 마스크) — TerrainSampleAssets 풀 세트
        var specs = new (string Name, float Min, float Max)[]
        {
            ("Grass_B", 1.0f, 1.6f), ("Grass_C", 1.0f, 1.6f), ("Grass_D", 0.9f, 1.4f),
            ("Fern_A", 1.0f, 1.8f), ("Heather_A", 0.8f, 1.3f), ("GrassDry_C", 1.0f, 1.6f), ("Plant_A", 1.0f, 1.5f),
        };

        var prototypes = new List<DetailPrototype>();
        foreach (var spec in specs)
        {
            var prefab = makeUrpDetail(spec.Name);
            if (prefab == null)
            {
                continue;
            }

            var prototype = new DetailPrototype();
            prototype.usePrototypeMesh = true;
            prototype.prototype = prefab;
            prototype.renderMode = DetailRenderMode.VertexLit;
            prototype.useInstancing = true;
            prototype.minWidth = spec.Min;
            prototype.maxWidth = spec.Max;
            prototype.minHeight = spec.Min;
            prototype.maxHeight = spec.Max;
            prototype.noiseSpread = 0.4f;
            prototype.alignToGround = 0.6f;
            prototypes.Add(prototype);
        }

        InData.detailPrototypes = prototypes.ToArray();
        int res = InData.detailResolution;
        int alphaRes = InData.alphamapResolution;
        var alphas = InData.GetAlphamaps(0, 0, alphaRes, alphaRes);
        for (int layer = 0; layer < prototypes.Count; ++layer)
        {
            var map = new int[res, res];
            for (int y = 0; y < res; ++y)
            {
                for (int x = 0; x < res; ++x)
                {
                    int ax = x * alphaRes / res;
                    int ay = y * alphaRes / res;
                    float grass = alphas[ay, ax, LayerGrass];
                    float moss = alphas[ay, ax, LayerMoss];
                    float dry = alphas[ay, ax, LayerDry];
                    float noise = Mathf.PerlinNoise(x * 0.09f + layer * 17.0f, y * 0.09f);
                    float density;
                    switch (layer)
                    {
                        case 0:
                        case 1:
                            density = (grass + moss * 0.5f) * noise * 3.0f;
                            break;
                        case 2:
                            density = grass * (noise > 0.55f ? 2.0f : 0.0f);
                            break;
                        case 3:
                            density = moss * (noise > 0.5f ? 2.0f : 0.3f);
                            break;
                        case 4:
                            density = dry * noise * 2.0f;
                            break;
                        case 5:
                            density = dry * noise * 3.0f;
                            break;
                        default:
                            density = (grass + moss) * (noise > 0.78f ? 1.0f : 0.0f);
                            break;
                    }

                    map[y, x] = Mathf.FloorToInt(density);
                }
            }

            InData.SetDetailLayer(0, 0, layer, map);
        }
    }

    /// <summary>장식 나무 (사실적인 나무 에셋이 없어 Kenney 나무 사용 — AssetRequests.md).</summary>
    private static void addTrees(TerrainData InData, List<FlatZone> InZones)
    {
        var models = new[] { "tree_pineTallB", "tree_pineRoundC", "tree_detailed_dark", "tree_fat_darkh", "tree_default_dark" };
        var prototypes = new List<TreePrototype>();
        foreach (var name in models)
        {
            var prefab = ContentBuilder.GetRecoloredTree(name);
            if (prefab != null)
            {
                prototypes.Add(new TreePrototype { prefab = prefab });
            }
        }

        InData.treePrototypes = prototypes.ToArray();
        var random = new System.Random(Seed);
        var instances = new List<TreeInstance>();
        for (int i = 0; i < 14000 && prototypes.Count > 0; ++i)
        {
            float x = (float)random.NextDouble() * Size;
            float z = (float)random.NextDouble() * Size;
            var p = new Vector2(x, z);
            float nx = x / Size;
            float nz = z / Size;
            float steep = InData.GetSteepness(nx, nz);
            float height = InData.GetInterpolatedHeight(nx, nz);
            if (steep > 32.0f || height > 75.0f || height < WorldRegions.WaterLevel + 1.0f)
            {
                continue;
            }

            float forest = Mathf.Clamp01(1.0f - Vector2.Distance(p, WorldRegions.DunleyWoods) / 110.0f);
            float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(z, Size - z));
            float mountainForest = Mathf.Clamp01((90.0f - edge) / 40.0f);
            float chance = Mathf.Max(forest * 0.75f, mountainForest * 0.5f) * Mathf.PerlinNoise(x * 0.03f, z * 0.03f) * 3.0f + 0.03f;
            if (random.NextDouble() > chance)
            {
                continue;
            }

            bool blocked = Vector2.Distance(p, WorldRegions.Lake) < WorldRegions.LakeRadius + 6.0f;
            foreach (var zone in InZones)
            {
                if (Vector2.Distance(p, zone.Center) < zone.Radius + 4.0f)
                {
                    blocked = true;
                    break;
                }
            }

            if (blocked == true)
            {
                continue;
            }

            float scale = 4.5f + (float)random.NextDouble() * 3.0f;
            var instance = new TreeInstance();
            instance.prototypeIndex = random.Next(prototypes.Count);
            instance.position = new Vector3(nx, 0.0f, nz);
            instance.widthScale = scale;
            instance.heightScale = scale;
            instance.rotation = (float)random.NextDouble() * Mathf.PI * 2.0f;
            instance.color = Color.white;
            instance.lightmapColor = Color.white;
            instances.Add(instance);
        }

        InData.SetTreeInstances(instances.ToArray(), true);
    }

    /// <summary>
    /// TerrainSampleAssets 풀 프리팹의 메시/텍스처로 URP Lit(알파 클립, 양면) 디테일 프리팹을 만든다.
    /// 원본 Shader Graph(TerrainGrass)는 HDRP 기준 설정(디퓨전 프로파일 등)이라 이 프로젝트 URP 의 Terrain Detail 에서 그려지지 않았다 (Issues.md 19).
    /// </summary>
    private static GameObject makeUrpDetail(string InName)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{TerrainSample}/Prefabs/{InName}.prefab");
        if (source == null)
        {
            return null;
        }

        var filter = source.GetComponentInChildren<MeshFilter>();
        var renderer = source.GetComponentInChildren<MeshRenderer>();
        if (filter == null || renderer == null)
        {
            return null;
        }

        string folder = EditorPaths.Generated + "/World/Detail";
        Framework.Editor.EditorAssetUtil.EnsureFolder(folder);
        var sourceMaterial = renderer.sharedMaterial;
        string materialPath = $"{folder}/M_{sourceMaterial.name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, materialPath);
        }

        var shader = sourceMaterial.shader;
        for (int i = 0; i < shader.GetPropertyCount(); ++i)
        {
            if (shader.GetPropertyType(i) != UnityEngine.Rendering.ShaderPropertyType.Texture)
            {
                continue;
            }

            var texture = sourceMaterial.GetTexture(shader.GetPropertyName(i));
            if (texture == null)
            {
                continue;
            }

            if (texture.name.Contains("BaseColor") == true)
            {
                material.SetTexture("_BaseMap", texture);
            }
            else if (texture.name.Contains("Normal") == true)
            {
                material.SetTexture("_BumpMap", texture);
                material.EnableKeyword("_NORMALMAP");
            }
        }

        material.SetColor("_BaseColor", Color.white);
        material.SetFloat("_AlphaClip", 1.0f);
        material.SetFloat("_Cutoff", 0.45f);
        material.EnableKeyword("_ALPHATEST_ON");
        material.SetFloat("_Cull", 0.0f);
        material.SetFloat("_Smoothness", 0.1f);
        material.renderQueue = (int)RenderQueue.AlphaTest;
        material.enableInstancing = true;
        EditorUtility.SetDirty(material);

        var go = new GameObject(InName);
        go.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
        var meshRenderer = go.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{folder}/{InName}.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }
}
