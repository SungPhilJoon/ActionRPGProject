using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 물: 북서쪽 산에서 호수로 흘러드는 강 + 호수 (Game/Water 셰이더).
///  1) 강 중심선(제어점 → Catmull-Rom)을 따라 "강을 파기 전" 지형 높이를 샘플해 수면 높이 프로필을 만든다
///     (하류로 갈수록 내려가기만 하도록 단조 감소, 끝은 호수 수면).
///  2) sampleHeight 가 마지막에 carveRiver 로 강바닥(수면 - 깊이)과 강둑 경사를 만든다.
///  3) 수면 리본 메시: 정점 색에 흐름 방향/속도/급류 거품을 굽는다 (Water.shader 규약).
///  4) WaterBody 컴포넌트에 같은 모양을 저장 → 런타임 스폰이 물속을 피한다.
/// </summary>
public static partial class WorldBuilder
{
    /// <summary>강 제어점 (x, z). 채집/몬스터 지역을 피해 Dunley Woods 북쪽 참나무 숲 서쪽 가장자리를 지난다. 문서: World.md 2절</summary>
    private static readonly Vector2[] RiverControl =
    {
        new Vector2(46.0f, 482.0f), new Vector2(70.0f, 442.0f), new Vector2(86.0f, 402.0f), new Vector2(96.0f, 362.0f),
        new Vector2(110.0f, 326.0f), new Vector2(132.0f, 298.0f), new Vector2(152.0f, 280.0f), new Vector2(166.0f, 266.0f),
    };

    private const float RiverDepth = 1.3f;
    private const float RiverBank = 10.0f;
    private const float RiverStep = 3.0f;
    private const float RiverSourceHalfWidth = 3.0f;
    private const float RiverMouthHalfWidth = 6.0f;
    private const float MouthBlend = 30.0f;

    private static List<Vector3> RiverPath;
    private static List<float> RiverHalfWidth;
    private static Rect RiverRect;

    // ------------------------------------------------------------------ profile

    /// <summary>강 수면 프로필 계산. sampleHeight(강 없음)를 쓰므로 buildTerrain 전에 호출한다.</summary>
    private static void prepareRiver(List<FlatZone> InZones)
    {
        RiverPath = null;
        var points = densify(RiverControl, RiverStep);
        var path = new List<Vector3>();
        var widths = new List<float>();
        float water = float.MaxValue;
        for (int i = 0; i < points.Count; ++i)
        {
            float t = (float)i / (points.Count - 1);
            float raw = sampleHeight(points[i].x, points[i].y, InZones) - 1.0f;
            water = Mathf.Max(WorldRegions.WaterLevel, Mathf.Min(water, raw));
            path.Add(new Vector3(points[i].x, water, points[i].y));
            widths.Add(Mathf.Lerp(RiverSourceHalfWidth, RiverMouthHalfWidth, t));
        }

        // 계단처럼 보이지 않게 이동 평균 후 다시 단조 감소를 강제한다.
        for (int pass = 0; pass < 3; ++pass)
        {
            for (int i = 1; i < path.Count - 1; ++i)
            {
                var p = path[i];
                p.y = (path[i - 1].y + p.y + path[i + 1].y) / 3.0f;
                path[i] = p;
            }
        }

        for (int i = 1; i < path.Count; ++i)
        {
            var p = path[i];
            p.y = Mathf.Max(WorldRegions.WaterLevel, Mathf.Min(p.y, path[i - 1].y));
            path[i] = p;
        }

        // 하구: 호수 수면 원 바깥 MouthBlend(m) 구간에서 호수 수면 높이로 내려가 리본과 호수가 같은 높이에서 만난다.
        for (int i = 0; i < path.Count; ++i)
        {
            var p = path[i];
            float lakeDistance = Vector2.Distance(new Vector2(p.x, p.z), WorldRegions.Lake) - LakeSurfaceRadius;
            p.y = Mathf.Lerp(WorldRegions.WaterLevel, p.y, Mathf.Clamp01(lakeDistance / MouthBlend));
            path[i] = p;
        }

        float minX = float.MaxValue;
        float minZ = float.MaxValue;
        float maxX = float.MinValue;
        float maxZ = float.MinValue;
        foreach (var p in path)
        {
            minX = Mathf.Min(minX, p.x);
            minZ = Mathf.Min(minZ, p.z);
            maxX = Mathf.Max(maxX, p.x);
            maxZ = Mathf.Max(maxZ, p.z);
        }

        float pad = RiverMouthHalfWidth + RiverBank + 2.0f;
        RiverRect = Rect.MinMaxRect(minX - pad, minZ - pad, maxX + pad, maxZ + pad);
        RiverHalfWidth = widths;
        RiverPath = path;
    }

    private static List<Vector2> densify(Vector2[] InControl, float InStep)
    {
        var result = new List<Vector2>();
        for (int i = 0; i < InControl.Length - 1; ++i)
        {
            Vector2 p0 = InControl[Mathf.Max(0, i - 1)];
            Vector2 p1 = InControl[i];
            Vector2 p2 = InControl[i + 1];
            Vector2 p3 = InControl[Mathf.Min(InControl.Length - 1, i + 2)];
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(p1, p2) / InStep));
            for (int s = 0; s < steps; ++s)
            {
                result.Add(catmullRom(p0, p1, p2, p3, (float)s / steps));
            }
        }

        result.Add(InControl[InControl.Length - 1]);
        return result;
    }

    private static Vector2 catmullRom(Vector2 InP0, Vector2 InP1, Vector2 InP2, Vector2 InP3, float InT)
    {
        float t2 = InT * InT;
        float t3 = t2 * InT;
        return 0.5f * (2.0f * InP1 + (InP2 - InP0) * InT + (2.0f * InP0 - 5.0f * InP1 + 4.0f * InP2 - InP3) * t2 + (3.0f * InP1 - InP0 - 3.0f * InP2 + InP3) * t3);
    }

    /// <summary>XZ 점에서 강 중심선까지의 거리, 그 지점 수면 높이, 반폭. 강이 없거나 멀면 false.</summary>
    private static bool sampleRiver(Vector2 InPoint, out float OutDistance, out float OutWater, out float OutHalfWidth)
    {
        OutDistance = float.MaxValue;
        OutWater = 0.0f;
        OutHalfWidth = 0.0f;
        if (RiverPath == null || RiverRect.Contains(InPoint) == false)
        {
            return false;
        }

        for (int i = 0; i < RiverPath.Count - 1; ++i)
        {
            var a = new Vector2(RiverPath[i].x, RiverPath[i].z);
            var b = new Vector2(RiverPath[i + 1].x, RiverPath[i + 1].z);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(InPoint - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            float distance = Vector2.Distance(InPoint, a + ab * t);
            if (distance < OutDistance)
            {
                OutDistance = distance;
                OutWater = Mathf.Lerp(RiverPath[i].y, RiverPath[i + 1].y, t);
                OutHalfWidth = Mathf.Lerp(RiverHalfWidth[i], RiverHalfWidth[i + 1], t);
            }
        }

        return OutDistance < OutHalfWidth + RiverBank;
    }

    /// <summary>강바닥 + 강둑. 둑은 높은 지형이면 깎아 계곡을, 낮은 지형이면 돋워 제방을 만든다 (호수 근처는 깎기만).</summary>
    private static float carveRiver(float InX, float InZ, float InHeight)
    {
        float distance;
        float water;
        float halfWidth;
        var p = new Vector2(InX, InZ);
        if (sampleRiver(p, out distance, out water, out halfWidth) == false)
        {
            return InHeight;
        }

        bool nearLake = Vector2.Distance(p, WorldRegions.Lake) < WorldRegions.LakeRadius + 16.0f;
        if (distance < halfWidth)
        {
            float u = distance / halfWidth;
            float bed = water - RiverDepth * (1.0f - u * u) - 0.1f;
            return Mathf.Min(InHeight, bed);
        }

        float t = Mathf.Clamp01((distance - halfWidth) / RiverBank);
        float s = t * t * (3.0f - 2.0f * t);
        float bank = water + 0.25f + (distance - halfWidth) * 0.35f;
        float blended = Mathf.Lerp(bank, InHeight, s);
        return nearLake == true ? Mathf.Min(InHeight, blended) : blended;
    }

    /// <summary>XZ 점이 강/호수 물 위(InMargin 만큼 넓혀서)인지. 나무·풀 배치 제외용.</summary>
    private static bool isWater(Vector2 InPoint, float InMargin)
    {
        if (Vector2.Distance(InPoint, WorldRegions.Lake) < WorldRegions.LakeRadius + InMargin)
        {
            return true;
        }

        float distance;
        float water;
        float halfWidth;
        return sampleRiver(InPoint, out distance, out water, out halfWidth) == true && distance < halfWidth + InMargin;
    }

    /// <summary>강가/호숫가 가까움 0~1 (InRange m 안). 모래·자작나무·습지 풀 분포에 쓴다.</summary>
    private static float waterProximity(Vector2 InPoint, float InRange)
    {
        float lake = 1.0f - Mathf.Clamp01((Vector2.Distance(InPoint, WorldRegions.Lake) - WorldRegions.LakeRadius) / InRange);
        float distance;
        float water;
        float halfWidth;
        float river = 0.0f;
        if (sampleRiver(InPoint, out distance, out water, out halfWidth) == true)
        {
            river = 1.0f - Mathf.Clamp01((distance - halfWidth) / InRange);
        }

        return Mathf.Max(lake, river);
    }

    // ------------------------------------------------------------------ meshes

    private static void buildWater(Terrain InTerrain)
    {
        var material = waterMaterial();
        var root = new GameObject("[Water]").transform;

        if (RiverPath != null)
        {
            // 호수 수면 원과 겹쳐 두 번 블렌딩되지 않게 호수 가장자리 앞에서 리본을 끝낸다.
            int end = RiverPath.Count;
            for (int i = 0; i < RiverPath.Count; ++i)
            {
                if (Vector2.Distance(new Vector2(RiverPath[i].x, RiverPath[i].z), WorldRegions.Lake) < LakeSurfaceRadius)
                {
                    end = i + 1;
                    break;
                }
            }

            var river = new GameObject("River");
            river.transform.SetParent(root, false);
            river.AddComponent<MeshFilter>().sharedMesh = saveWaterMesh(buildRiverMesh(end), "RiverSurface");
            setupWaterRenderer(river.AddComponent<MeshRenderer>(), material);
            var body = river.AddComponent<WaterBody>();
            body.Path = RiverPath.ToArray();
            body.HalfWidths = RiverHalfWidth.ToArray();
        }

        var lake = new GameObject("Lake");
        lake.transform.SetParent(root, false);
        lake.transform.position = new Vector3(WorldRegions.Lake.x, WorldRegions.WaterLevel, WorldRegions.Lake.y);
        lake.AddComponent<MeshFilter>().sharedMesh = saveWaterMesh(buildDiscMesh(LakeSurfaceRadius, 48), "LakeSurface");
        setupWaterRenderer(lake.AddComponent<MeshRenderer>(), material);
        var lakeBody = lake.AddComponent<WaterBody>();
        lakeBody.Path = new[] { lake.transform.position };
        lakeBody.HalfWidths = new[] { WorldRegions.LakeRadius + 2.0f };
    }

    private static float LakeSurfaceRadius
    {
        // 호수 분지 물가(수면 4m 와 지형이 만나는 곳)가 반지름 약 31m 라 조금만 넘게 잡는다.
        get { return WorldRegions.LakeRadius + 5.0f; }
    }

    private static Mesh buildRiverMesh(int InCount)
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        const int columns = 4;
        for (int i = 0; i < InCount; ++i)
        {
            Vector3 previous = RiverPath[Mathf.Max(0, i - 1)];
            Vector3 next = RiverPath[Mathf.Min(RiverPath.Count - 1, i + 1)];
            Vector3 flow = next - previous;
            float run = new Vector2(flow.x, flow.z).magnitude;
            float slope = run > 0.001f ? (previous.y - next.y) / run : 0.0f;
            Vector3 direction = new Vector3(flow.x, 0.0f, flow.z).normalized;
            Vector3 side = new Vector3(-direction.z, 0.0f, direction.x);
            float halfWidth = RiverHalfWidth[i] + 1.5f;
            float mouth = 1.0f - Mathf.Clamp01((float)(i - (InCount - 6)) / 6.0f);
            float speed = Mathf.Clamp01(0.35f + slope * 6.0f) * mouth;
            float rapids = Mathf.Clamp01((slope - 0.05f) * 6.0f) * mouth;
            for (int c = 0; c <= columns; ++c)
            {
                float u = (float)c / columns * 2.0f - 1.0f;
                vertices.Add(RiverPath[i] + side * (u * halfWidth));
                // 가장자리는 느리게 흐른다.
                float edge = 1.0f - u * u * 0.6f;
                colors.Add(new Color(direction.x * 0.5f + 0.5f, direction.z * 0.5f + 0.5f, speed * edge, rapids * edge));
            }
        }

        int stride = columns + 1;
        for (int i = 0; i < InCount - 1; ++i)
        {
            for (int c = 0; c < columns; ++c)
            {
                int a = i * stride + c;
                int b = a + stride;
                triangles.Add(a);
                triangles.Add(b);
                triangles.Add(a + 1);
                triangles.Add(a + 1);
                triangles.Add(b);
                triangles.Add(b + 1);
            }
        }

        var mesh = new Mesh();
        mesh.name = "RiverSurface";
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        fixWinding(mesh);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Mesh buildDiscMesh(float InRadius, int InSegments)
    {
        var vertices = new List<Vector3> { Vector3.zero };
        var colors = new List<Color> { new Color(0.5f, 0.5f, 0.0f, 0.0f) };
        var triangles = new List<int>();
        for (int i = 0; i < InSegments; ++i)
        {
            float angle = (float)i / InSegments * Mathf.PI * 2.0f;
            vertices.Add(new Vector3(Mathf.Cos(angle), 0.0f, Mathf.Sin(angle)) * InRadius);
            colors.Add(new Color(0.5f, 0.5f, 0.0f, 0.0f));
            triangles.Add(0);
            triangles.Add(1 + (i + 1) % InSegments);
            triangles.Add(1 + i);
        }

        var mesh = new Mesh();
        mesh.name = "LakeSurface";
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        fixWinding(mesh);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>수면은 위에서 보이도록 삼각형 법선이 +Y 가 되게 맞춘다 (Cull Back).</summary>
    private static void fixWinding(Mesh InMesh)
    {
        var vertices = InMesh.vertices;
        var triangles = InMesh.triangles;
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 normal = Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]);
            if (normal.y < 0.0f)
            {
                int swap = triangles[i + 1];
                triangles[i + 1] = triangles[i + 2];
                triangles[i + 2] = swap;
            }
        }

        InMesh.triangles = triangles;
    }

    private static Mesh saveWaterMesh(Mesh InMesh, string InName)
    {
        string folder = EditorPaths.Generated + "/World/Water";
        EditorAssetUtil.EnsureFolder(folder);
        string path = $"{folder}/{InName}.asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(InMesh, path);
        return InMesh;
    }

    private static void setupWaterRenderer(MeshRenderer InRenderer, Material InMaterial)
    {
        InRenderer.sharedMaterial = InMaterial;
        InRenderer.shadowCastingMode = ShadowCastingMode.Off;
        InRenderer.receiveShadows = true;
        InRenderer.gameObject.isStatic = true;
    }

    private static Material waterMaterial()
    {
        string folder = EditorPaths.Generated + "/World/Water";
        EditorAssetUtil.EnsureFolder(folder);
        string path = folder + "/M_Water.mat";
        var shader = Shader.Find("Game/Water");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.shader = shader;
        material.SetTexture("_NormalMap", FoliageTextures.WaterNormal);
        material.SetTexture("_FoamMap", FoliageTextures.WaterFoam);
        material.SetColor("_ShallowColor", new Color(0.62f, 0.8f, 0.72f));
        material.SetColor("_DeepColor", new Color(0.05f, 0.15f, 0.16f));
        material.SetFloat("_DepthDistance", 2.2f);
        EditorUtility.SetDirty(material);
        return material;
    }
}
