using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// CPU 텍스처 캔버스 (에디터 전용). 안티앨리어싱 선/잎 모양을 "over" 합성으로 그리고 PNG 로 저장한다.
/// 좌표는 픽셀, (0,0) = 왼쪽 아래 (Unity 텍스처 UV 와 같다).
/// </summary>
public class FoliageCanvas
{
    public readonly int Width;
    public readonly int Height;
    private readonly Color[] Pixels;

    public FoliageCanvas(int InWidth, int InHeight, Color InClear)
    {
        Width = InWidth;
        Height = InHeight;
        Pixels = new Color[InWidth * InHeight];
        for (int i = 0; i < Pixels.Length; ++i)
        {
            Pixels[i] = InClear;
        }
    }

    public Color Get(int InX, int InY)
    {
        return Pixels[wrap(InY, Height) * Width + wrap(InX, Width)];
    }

    public void Set(int InX, int InY, Color InColor)
    {
        Pixels[InY * Width + InX] = InColor;
    }

    /// <summary>굵기 InWidth(px) 선분. 끝으로 갈수록 InEndWidth 로 가늘어진다.</summary>
    public void Line(Vector2 InFrom, Vector2 InTo, float InWidth, float InEndWidth, Color InColor)
    {
        float pad = Mathf.Max(InWidth, InEndWidth) * 0.5f + 1.5f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(InFrom.x, InTo.x) - pad));
        int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(InFrom.x, InTo.x) + pad));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(InFrom.y, InTo.y) - pad));
        int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(InFrom.y, InTo.y) + pad));
        Vector2 segment = InTo - InFrom;
        float lengthSq = Mathf.Max(0.0001f, segment.sqrMagnitude);
        for (int y = y0; y <= y1; ++y)
        {
            for (int x = x0; x <= x1; ++x)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - InFrom, segment) / lengthSq);
                float distance = Vector2.Distance(p, InFrom + segment * t);
                float half = Mathf.Lerp(InWidth, InEndWidth, t) * 0.5f;
                float coverage = Mathf.Clamp01(half + 0.5f - distance);
                if (coverage > 0.0f)
                {
                    blend(x, y, InColor, coverage);
                }
            }
        }
    }

    /// <summary>
    /// 잎 하나. InBase 에서 InDirection 으로 InLength 만큼, 최대 폭 InWidth (끝이 뾰족하고 밑이 둥근 모양).
    /// 가운데 잎맥을 밝게, 한쪽 면을 살짝 어둡게 칠해 접힌 입체감을 준다.
    /// </summary>
    public void Leaf(Vector2 InBase, Vector2 InDirection, float InLength, float InWidth, Color InColor)
    {
        Vector2 axis = InDirection.normalized;
        var side = new Vector2(-axis.y, axis.x);
        float reach = Mathf.Max(InLength, InWidth) + 2.0f;
        int x0 = Mathf.Max(0, Mathf.FloorToInt(InBase.x - reach));
        int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(InBase.x + reach));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(InBase.y - reach));
        int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(InBase.y + reach));
        for (int y = y0; y <= y1; ++y)
        {
            for (int x = x0; x <= x1; ++x)
            {
                Vector2 local = new Vector2(x + 0.5f, y + 0.5f) - InBase;
                float along = Vector2.Dot(local, axis) / InLength;
                if (along < -0.02f || along > 1.02f)
                {
                    continue;
                }

                float across = Vector2.Dot(local, side);
                float t = Mathf.Clamp01(along);
                float half = InWidth * 0.5f * Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.75f)), 0.85f);
                float coverage = Mathf.Clamp01(half - Mathf.Abs(across) + 0.5f);
                if (coverage <= 0.0f)
                {
                    continue;
                }

                float edge = half > 0.01f ? Mathf.Abs(across) / half : 1.0f;
                float shade = 0.82f + 0.28f * (1.0f - edge * edge) + (across < 0.0f ? -0.07f : 0.03f);
                float rib = Mathf.Clamp01(1.0f - Mathf.Abs(across) / 0.9f) * 0.18f * (1.0f - t);
                var color = new Color(InColor.r * shade + rib, InColor.g * shade + rib, InColor.b * shade + rib * 0.5f, 1.0f);
                blend(x, y, color, coverage);
            }
        }
    }

    /// <summary>투명 픽셀의 RGB 를 불투명 픽셀 평균색으로 채운다 (밉맵에서 검은 테두리 방지).</summary>
    public void FillTransparentColor()
    {
        Color sum = Color.clear;
        int count = 0;
        for (int i = 0; i < Pixels.Length; ++i)
        {
            if (Pixels[i].a > 0.5f)
            {
                sum += Pixels[i];
                ++count;
            }
        }

        if (count == 0)
        {
            return;
        }

        Color mean = sum / count;
        for (int i = 0; i < Pixels.Length; ++i)
        {
            float a = Pixels[i].a;
            if (a < 1.0f)
            {
                var c = Color.Lerp(mean, Pixels[i], a);
                Pixels[i] = new Color(c.r, c.g, c.b, a);
            }
        }
    }

    /// <summary>PNG 로 저장하고 임포트 설정을 맞춘다. InNormalMap 이면 노멀맵, InAlphaTest 면 알파 커버리지 보존 밉맵.</summary>
    public Texture2D Save(string InPath, bool InNormalMap, bool InAlphaTest, bool InRepeat)
    {
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, InNormalMap == true);
        texture.SetPixels(Pixels);
        texture.Apply();
        File.WriteAllBytes(InPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(InPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(InPath);
        importer.textureType = InNormalMap == true ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = InNormalMap == false;
        importer.alphaIsTransparency = InAlphaTest;
        importer.mipmapEnabled = true;
        importer.mipMapsPreserveCoverage = InAlphaTest;
        importer.alphaTestReferenceValue = 0.45f;
        importer.wrapMode = InRepeat == true ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.anisoLevel = 4;
        importer.maxTextureSize = Mathf.Max(Width, Height);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(InPath);
    }

    /// <summary>높이 캔버스(R 채널, 타일링) → 탄젠트 공간 노멀맵 캔버스.</summary>
    public FoliageCanvas ToNormalMap(float InStrength)
    {
        var normal = new FoliageCanvas(Width, Height, Color.clear);
        for (int y = 0; y < Height; ++y)
        {
            for (int x = 0; x < Width; ++x)
            {
                float dx = (Get(x + 1, y).r - Get(x - 1, y).r) * InStrength;
                float dy = (Get(x, y + 1).r - Get(x, y - 1).r) * InStrength;
                var n = new Vector3(-dx, -dy, 1.0f).normalized;
                normal.Set(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f));
            }
        }

        return normal;
    }

    private void blend(int InX, int InY, Color InColor, float InCoverage)
    {
        int index = InY * Width + InX;
        Color dst = Pixels[index];
        float a = InCoverage * InColor.a;
        float outA = a + dst.a * (1.0f - a);
        if (outA <= 0.0001f)
        {
            return;
        }

        Color rgb = (InColor * a + dst * dst.a * (1.0f - a)) / outA;
        Pixels[index] = new Color(rgb.r, rgb.g, rgb.b, outA);
    }

    private static int wrap(int InValue, int InSize)
    {
        int value = InValue % InSize;
        return value < 0 ? value + InSize : value;
    }

    // ------------------------------------------------------------------ tileable noise

    /// <summary>주기 InPeriod 로 타일링되는 값 노이즈 (0~1).</summary>
    public static float TileNoise(float InX, float InY, int InPeriod, int InSeed)
    {
        int x0 = Mathf.FloorToInt(InX);
        int y0 = Mathf.FloorToInt(InY);
        float tx = InX - x0;
        float ty = InY - y0;
        tx = tx * tx * (3.0f - 2.0f * tx);
        ty = ty * ty * (3.0f - 2.0f * ty);
        float a = hash(x0, y0, InPeriod, InSeed);
        float b = hash(x0 + 1, y0, InPeriod, InSeed);
        float c = hash(x0, y0 + 1, InPeriod, InSeed);
        float d = hash(x0 + 1, y0 + 1, InPeriod, InSeed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    /// <summary>타일링 fbm. InU, InV 는 0~1 (타일 한 장).</summary>
    public static float TileFbm(float InU, float InV, int InBasePeriod, int InOctaves, int InSeed)
    {
        float value = 0.0f;
        float amplitude = 0.5f;
        int period = InBasePeriod;
        for (int i = 0; i < InOctaves; ++i)
        {
            value += TileNoise(InU * period, InV * period, period, InSeed + i * 31) * amplitude;
            period *= 2;
            amplitude *= 0.5f;
        }

        return value;
    }

    private static float hash(int InX, int InY, int InPeriod, int InSeed)
    {
        int x = ((InX % InPeriod) + InPeriod) % InPeriod;
        int y = ((InY % InPeriod) + InPeriod) % InPeriod;
        uint h = (uint)(x * 374761393 + y * 668265263 + InSeed * 982451653);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;
        return (h & 0xFFFFFF) / (float)0xFFFFFF;
    }
}
