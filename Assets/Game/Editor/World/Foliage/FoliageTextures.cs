using UnityEditor;
using UnityEngine;

/// <summary>
/// 절차 식생/물 텍스처 (Art/Generated/World/Foliage/Textures). 잎 카드 텍스처 규약: 밑동 = 아래 가운데, 끝 = 위.
///  - 가문비 가지(바늘잎 스프레이), 활엽수 잎 뭉치, 자작 잎 뭉치, 자작 껍질(+노멀)
///  - 물 노멀맵(타일링), 물 거품 노이즈(타일링)
/// 나무껍질(소나무/참나무/고사목)은 Poly Haven CC0 실사 텍스처를 쓴다 (Art/ThirdParty/PolyHaven/Bark).
/// </summary>
public static class FoliageTextures
{
    public static string Folder
    {
        get { return EditorPaths.Generated + "/World/Foliage/Textures"; }
    }

    public static Texture2D SpruceSpray
    {
        get { return getOrBuild("T_SpruceSpray.png", buildSpruceSpray); }
    }

    public static Texture2D BroadleafCluster
    {
        get { return getOrBuild("T_BroadleafCluster.png", () => buildLeafCluster(1101, 46, 70.0f, 40.0f, new Color(0.20f, 0.31f, 0.09f), new Color(0.30f, 0.40f, 0.11f), false)); }
    }

    public static Texture2D BirchCluster
    {
        get { return getOrBuild("T_BirchCluster.png", () => buildLeafCluster(2203, 70, 44.0f, 27.0f, new Color(0.33f, 0.44f, 0.12f), new Color(0.47f, 0.54f, 0.16f), true)); }
    }

    public static Texture2D BirchBark
    {
        get { return getOrBuild("T_BirchBark.png", () => buildBirchBark(false)); }
    }

    public static Texture2D BirchBarkNormal
    {
        get { return getOrBuild("T_BirchBark_N.png", () => buildBirchBark(true)); }
    }

    public static Texture2D WaterNormal
    {
        get { return getOrBuild("T_WaterNormal.png", buildWaterNormal); }
    }

    public static Texture2D WaterFoam
    {
        get { return getOrBuild("T_WaterFoam.png", buildFoam); }
    }

    /// <summary>모든 절차 텍스처를 지우고 다시 만든다 (모양을 바꾼 뒤).</summary>
    [MenuItem("Game/World/Rebuild Foliage Textures", priority = 40)]
    public static void RebuildAll()
    {
        AssetDatabase.DeleteAsset(Folder);
        var touch = new Object[] { SpruceSpray, BroadleafCluster, BirchCluster, BirchBark, BirchBarkNormal, WaterNormal, WaterFoam };
        Debug.Log("[Foliage] textures rebuilt: " + touch.Length);
    }

    private static Texture2D getOrBuild(string InFile, System.Func<Texture2D> InBuild)
    {
        string path = Folder + "/" + InFile;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture != null)
        {
            return texture;
        }

        Framework.Editor.EditorAssetUtil.EnsureFolder(Folder);
        return InBuild();
    }

    // ------------------------------------------------------------------ spruce

    private static Texture2D buildSpruceSpray()
    {
        const int size = 512;
        var random = new System.Random(3301);
        var canvas = new FoliageCanvas(size, size, new Color(0.1f, 0.18f, 0.1f, 0.0f));
        var twigColor = new Color(0.22f, 0.16f, 0.10f);
        var start = new Vector2(size * 0.5f, 6.0f);
        var tip = new Vector2(size * 0.5f + 6.0f, size - 10.0f);

        // 곁가지: 밑동 쪽은 짧고, 35% 지점이 가장 길고, 끝으로 갈수록 짧아진다 (납작한 가문비 가지 모양).
        var twigs = new System.Collections.Generic.List<Vector2[]>();
        twigs.Add(new[] { start, tip });
        for (float t = 0.08f; t < 0.93f; t += 0.045f)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float envelope = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.05f)), 0.8f) * (1.0f - t * 0.35f);
                float length = size * 0.36f * envelope * (0.75f + (float)random.NextDouble() * 0.35f);
                float angle = (52.0f + (float)random.NextDouble() * 14.0f) * Mathf.Deg2Rad;
                Vector2 from = Vector2.Lerp(start, tip, t + side * 0.01f);
                var direction = new Vector2(Mathf.Sin(angle) * side, Mathf.Cos(angle));
                twigs.Add(new[] { from, from + direction * length });
            }
        }

        for (int i = 0; i < twigs.Count; ++i)
        {
            canvas.Line(twigs[i][0], twigs[i][1], i == 0 ? 6.0f : 3.0f, 1.2f, twigColor);
        }

        // 바늘잎: 가지마다 3px 간격, 양쪽으로 비스듬히. 가지 끝(새순)은 밝은 초록.
        for (int i = 0; i < twigs.Count; ++i)
        {
            Vector2 from = twigs[i][0];
            Vector2 to = twigs[i][1];
            Vector2 axis = (to - from).normalized;
            var normal = new Vector2(-axis.y, axis.x);
            float length = Vector2.Distance(from, to);
            for (float d = 4.0f; d < length; d += 2.6f)
            {
                float t = d / length;
                for (int side = -1; side <= 1; side += 2)
                {
                    float needle = (13.0f + (float)random.NextDouble() * 8.0f) * (i == 0 ? 1.2f : 1.0f) * (1.0f - t * 0.35f);
                    float spread = 0.55f + (float)random.NextDouble() * 0.5f;
                    Vector2 direction = (axis * (1.0f - spread * 0.5f) + normal * side * spread).normalized;
                    float fresh = Mathf.Clamp01((t - 0.72f) / 0.28f) * (float)random.NextDouble();
                    float tone = 0.85f + (float)random.NextDouble() * 0.3f;
                    var dark = new Color(0.07f * tone, 0.16f * tone, 0.09f * tone);
                    var light = new Color(0.22f, 0.34f, 0.13f);
                    Vector2 root = from + axis * d;
                    canvas.Line(root, root + direction * needle, 2.4f, 0.9f, Color.Lerp(dark, light, fresh));
                }
            }
        }

        canvas.FillTransparentColor();
        return canvas.Save(Folder + "/T_SpruceSpray.png", false, true, false);
    }

    // ------------------------------------------------------------------ leaf clusters

    /// <summary>
    /// 잎 뭉치: 밑동에서 위로 휘는 줄기 + 곁줄기 끝마다 잎. 뒤쪽 잎을 먼저(어둡게), 앞 잎을 나중(밝게) 그린다.
    /// InDrooping 이면 곁줄기가 옆/아래로 늘어진다 (자작).
    /// </summary>
    private static Texture2D buildLeafCluster(int InSeed, int InLeafCount, float InLeafLength, float InLeafWidth, Color InDark, Color InLight, bool InDrooping)
    {
        const int size = 512;
        var random = new System.Random(InSeed);
        var canvas = new FoliageCanvas(size, size, new Color(InDark.r, InDark.g, InDark.b, 0.0f));
        var stemColor = new Color(0.25f, 0.20f, 0.12f);
        var center = new Vector2(size * 0.5f, size * 0.55f);
        float crown = size * 0.40f;

        canvas.Line(new Vector2(size * 0.5f, 0.0f), center, 7.0f, 3.0f, stemColor);

        for (int i = 0; i < InLeafCount; ++i)
        {
            float order = (float)i / InLeafCount;
            // 왕관 모양 영역 안에 잎 끝이 퍼지도록, 줄기 위 한 점에서 바깥으로 향하게 놓는다.
            float angle = (float)random.NextDouble() * Mathf.PI * 2.0f;
            float radius = Mathf.Sqrt((float)random.NextDouble()) * crown * 0.72f;
            var target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.9f) * radius;
            var attach = Vector2.Lerp(center, target, 0.55f);
            var stemFrom = Vector2.Lerp(new Vector2(size * 0.5f, size * 0.2f), center, (float)random.NextDouble());
            canvas.Line(stemFrom, attach, 2.6f, 1.4f, stemColor);

            Vector2 direction = target - attach;
            if (InDrooping == true)
            {
                direction += Vector2.down * crown * 0.35f;
            }

            if (direction.sqrMagnitude < 1.0f)
            {
                direction = Vector2.up;
            }

            float length = InLeafLength * (0.75f + (float)random.NextDouble() * 0.5f);
            float width = InLeafWidth * (0.8f + (float)random.NextDouble() * 0.35f);
            float tone = 0.8f + (float)random.NextDouble() * 0.35f;
            Color color = Color.Lerp(InDark, InLight, order * 0.7f + (float)random.NextDouble() * 0.3f) * tone;
            color.a = 1.0f;
            canvas.Leaf(attach, direction, length, width, color);
        }

        canvas.FillTransparentColor();
        return canvas.Save(Folder + (InDrooping == true ? "/T_BirchCluster.png" : "/T_BroadleafCluster.png"), false, true, false);
    }

    // ------------------------------------------------------------------ birch bark

    private static Texture2D buildBirchBark(bool InNormal)
    {
        const int size = 512;
        var random = new System.Random(4407);
        var height = new FoliageCanvas(size, size, Color.white);
        var albedo = new FoliageCanvas(size, size, Color.white);
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float n = FoliageCanvas.TileFbm(u, v * 0.25f, 8, 4, 77);
                float streak = FoliageCanvas.TileFbm(u * 0.5f, v, 16, 3, 91);
                float tone = 0.78f + n * 0.2f + (streak - 0.5f) * 0.06f;
                albedo.Set(x, y, new Color(tone * 0.98f, tone * 0.97f, tone * 0.92f, 1.0f));
                height.Set(x, y, new Color(0.6f + n * 0.2f, 0, 0, 1));
            }
        }

        // 피목(가로 검은 줄) + 큰 검은 얼룩
        var mark = new Color(0.12f, 0.11f, 0.10f, 1.0f);
        for (int i = 0; i < 140; ++i)
        {
            var from = new Vector2((float)random.NextDouble() * size, (float)random.NextDouble() * size);
            float length = 12.0f + (float)random.NextDouble() * 60.0f;
            float thickness = 1.5f + (float)random.NextDouble() * 3.5f;
            var to = from + new Vector2(length, ((float)random.NextDouble() - 0.5f) * 4.0f);
            albedo.Line(from, to, thickness, thickness * 0.5f, mark);
            height.Line(from, to, thickness, thickness * 0.5f, Color.black);
        }

        for (int i = 0; i < 10; ++i)
        {
            var c = new Vector2((float)random.NextDouble() * size, (float)random.NextDouble() * size);
            float w = 30.0f + (float)random.NextDouble() * 60.0f;
            albedo.Line(c, c + new Vector2(w, 6.0f), 10.0f + (float)random.NextDouble() * 14.0f, 4.0f, new Color(0.16f, 0.14f, 0.12f, 0.85f));
            height.Line(c, c + new Vector2(w, 6.0f), 10.0f, 4.0f, new Color(0.1f, 0, 0, 1));
        }

        if (InNormal == true)
        {
            return height.ToNormalMap(6.0f).Save(Folder + "/T_BirchBark_N.png", true, false, true);
        }

        return albedo.Save(Folder + "/T_BirchBark.png", false, false, true);
    }

    // ------------------------------------------------------------------ water

    private static Texture2D buildWaterNormal()
    {
        const int size = 512;
        var height = new FoliageCanvas(size, size, Color.black);
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float h = FoliageCanvas.TileFbm(u, v, 4, 5, 5);
                h += Mathf.Sin((u * 3.0f + v * 2.0f) * Mathf.PI * 2.0f) * 0.08f;
                h += Mathf.Sin((u * -2.0f + v * 5.0f) * Mathf.PI * 2.0f + 1.7f) * 0.05f;
                h += Mathf.Sin((u * 7.0f + v * -4.0f) * Mathf.PI * 2.0f + 0.4f) * 0.03f;
                height.Set(x, y, new Color(h, 0, 0, 1));
            }
        }

        return height.ToNormalMap(18.0f).Save(Folder + "/T_WaterNormal.png", true, false, true);
    }

    private static Texture2D buildFoam()
    {
        const int size = 256;
        var foam = new FoliageCanvas(size, size, Color.black);
        for (int y = 0; y < size; ++y)
        {
            for (int x = 0; x < size; ++x)
            {
                float u = (float)x / size;
                float v = (float)y / size;
                float n = FoliageCanvas.TileFbm(u, v, 8, 4, 19);
                float cells = Mathf.Abs(FoliageCanvas.TileFbm(u, v, 16, 2, 23) - 0.5f) * 2.0f;
                float value = Mathf.Clamp01(n * 1.3f - cells * 0.6f + 0.15f);
                foam.Set(x, y, new Color(value, value, value, 1));
            }
        }

        return foam.Save(Folder + "/T_WaterFoam.png", false, false, true);
    }
}
