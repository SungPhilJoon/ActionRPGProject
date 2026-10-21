using Framework.Editor;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 코드 생성 재질/파티클/발사체. 전용 VFX 에셋이 없어서 URP Particles/Unlit 셰이더와
/// 절차적으로 만든 원형 텍스처로 최소한의 연출을 만든다 (AssetRequests.md 에 교체 요청 기재).
/// </summary>
public static partial class ContentBuilder
{
    private static Material ParticleAdditive;
    private static Material ParticleAlpha;
    private static Material ShieldMaterial;
    private static Material CopperMaterial;
    private static Texture2D SoftCircle;

    private static readonly Color BloodRed = new Color(0.85f, 0.05f, 0.1f, 1.0f);

    private static void buildMaterials()
    {
        string folder = EditorPaths.Generated + "/Materials";
        EditorAssetUtil.EnsureFolder(folder);
        SoftCircle = createSoftCircle(EditorPaths.Generated + "/SoftCircle.png");

        ParticleAdditive = createParticleMaterial(folder + "/M_ParticleAdditive.mat", true);
        ParticleAlpha = createParticleMaterial(folder + "/M_ParticleAlpha.mat", false);

        ShieldMaterial = createParticleMaterial(folder + "/M_Shield.mat", true);
        ShieldMaterial.SetColor("_BaseColor", new Color(0.35f, 0.55f, 1.0f, 0.35f));
        ShieldMaterial.mainTexture = null;

        CopperMaterial = loadOrCreate(folder + "/M_Copper.mat", Shader.Find("Universal Render Pipeline/Lit"));
        CopperMaterial.SetColor("_BaseColor", new Color(0.78f, 0.42f, 0.2f));
        CopperMaterial.SetFloat("_Metallic", 0.75f);
        CopperMaterial.SetFloat("_Smoothness", 0.55f);
        EditorUtility.SetDirty(CopperMaterial);
    }

    private static Material loadOrCreate(string InPath, Shader InShader)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(InPath);
        if (material == null)
        {
            material = new Material(InShader);
            AssetDatabase.CreateAsset(material, InPath);
        }

        material.shader = InShader;
        return material;
    }

    private static Material createParticleMaterial(string InPath, bool InAdditive)
    {
        var material = loadOrCreate(InPath, Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        material.SetFloat("_Surface", 1.0f);
        material.SetFloat("_Blend", InAdditive == true ? 2.0f : 0.0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", InAdditive == true ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0.0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetTexture("_BaseMap", SoftCircle);
        material.mainTexture = SoftCircle;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material createEmissive(string InName, Color InColor, float InIntensity)
    {
        string path = $"{EditorPaths.Generated}/Materials/{InName}.mat";
        var material = loadOrCreate(path, Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", InColor);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", InColor * InIntensity);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D createSoftCircle(string InPath)
    {
        if (File.Exists(InPath) == false)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; ++y)
            {
                for (int x = 0; x < size; ++x)
                {
                    float dx = (x + 0.5f) / size * 2.0f - 1.0f;
                    float dy = (y + 0.5f) / size * 2.0f - 1.0f;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1.0f - d);
                    texture.SetPixel(x, y, new Color(1.0f, 1.0f, 1.0f, a * a));
                }
            }

            File.WriteAllBytes(InPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(InPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(InPath);
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(InPath);
    }

    // ------------------------------------------------------------------ particles

    private struct ParticleSpec
    {
        public bool Loop;
        public float Duration;
        public int Burst;
        public float Rate;
        public float Lifetime;
        public float Speed;
        public float Size;
        public Color Start;
        public Color End;
        public ParticleSystemShapeType Shape;
        public float ShapeRadius;
        public float ShapeAngle;
        public float Gravity;
        public bool Additive;
        public Vector3 ShapeRotation;
    }

    private static GameObject createParticle(string InName, ParticleSpec InSpec, Transform InParent = null)
    {
        var go = new GameObject(InName);
        if (InParent != null)
        {
            go.transform.SetParent(InParent, false);
        }

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = InSpec.Loop;
        main.duration = Mathf.Max(0.1f, InSpec.Duration);
        main.startLifetime = new ParticleSystem.MinMaxCurve(InSpec.Lifetime * 0.7f, InSpec.Lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(InSpec.Speed * 0.6f, InSpec.Speed);
        main.startSize = new ParticleSystem.MinMaxCurve(InSpec.Size * 0.6f, InSpec.Size);
        main.startColor = InSpec.Start;
        main.gravityModifier = InSpec.Gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = 200;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;

        var emission = ps.emission;
        emission.rateOverTime = InSpec.Rate;
        if (InSpec.Burst > 0)
        {
            emission.SetBursts(new[] { new ParticleSystem.Burst(0.0f, (short)InSpec.Burst) });
        }

        var shape = ps.shape;
        shape.shapeType = InSpec.Shape;
        shape.radius = Mathf.Max(0.01f, InSpec.ShapeRadius);
        shape.angle = InSpec.ShapeAngle;
        shape.rotation = InSpec.ShapeRotation;

        var color = ps.colorOverLifetime;
        color.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(InSpec.Start, 0.0f), new GradientColorKey(InSpec.End, 1.0f) },
            new[] { new GradientAlphaKey(InSpec.Start.a, 0.0f), new GradientAlphaKey(InSpec.Start.a * 0.8f, 0.5f), new GradientAlphaKey(0.0f, 1.0f) });
        color.color = gradient;

        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1.0f, AnimationCurve.Linear(0.0f, 1.0f, 1.0f, 0.3f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = InSpec.Additive == true ? ParticleAdditive : ParticleAlpha;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private static ParticleSpec burst(int InCount, float InLifetime, float InSpeed, float InSize, Color InStart, Color InEnd, ParticleSystemShapeType InShape, float InRadius)
    {
        var spec = new ParticleSpec();
        spec.Duration = InLifetime;
        spec.Burst = InCount;
        spec.Lifetime = InLifetime;
        spec.Speed = InSpeed;
        spec.Size = InSize;
        spec.Start = InStart;
        spec.End = InEnd;
        spec.Shape = InShape;
        spec.ShapeRadius = InRadius;
        spec.Additive = true;
        return spec;
    }

    private static ParticleSpec loop(float InRate, float InLifetime, float InSpeed, float InSize, Color InStart, Color InEnd, ParticleSystemShapeType InShape, float InRadius)
    {
        var spec = burst(0, InLifetime, InSpeed, InSize, InStart, InEnd, InShape, InRadius);
        spec.Loop = true;
        spec.Duration = 1.0f;
        spec.Rate = InRate;
        return spec;
    }

    private static void buildVfx()
    {
        var slash = burst(18, 0.25f, 7.0f, 0.7f, BloodRed, new Color(0.3f, 0.0f, 0.0f), ParticleSystemShapeType.Cone, 0.4f);
        slash.ShapeAngle = 60.0f;
        slash.ShapeRotation = new Vector3(0.0f, 0.0f, 0.0f);
        savePrefab(createParticle("Slash", slash), "VFX/Slash");

        savePrefab(createParticle("HitSpark", burst(12, 0.3f, 4.5f, 0.35f, new Color(1.0f, 0.9f, 0.8f), BloodRed, ParticleSystemShapeType.Sphere, 0.1f)), "VFX/HitSpark");

        var bloodBurst = burst(50, 0.7f, 6.0f, 0.6f, BloodRed, new Color(0.2f, 0.0f, 0.02f), ParticleSystemShapeType.Circle, 0.4f);
        bloodBurst.ShapeRotation = new Vector3(-90.0f, 0.0f, 0.0f);
        savePrefab(createParticle("BloodBurst", bloodBurst), "VFX/BloodBurst");

        savePrefab(createParticle("DashTrail", burst(24, 0.5f, 1.0f, 0.8f, new Color(0.4f, 0.0f, 0.05f, 0.9f), Color.black, ParticleSystemShapeType.Sphere, 0.5f)), "VFX/DashTrail");

        var shield = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shield.name = "ShieldBubble";
        Object.DestroyImmediate(shield.GetComponent<Collider>());
        shield.transform.localScale = Vector3.one * 2.2f;
        var shieldRenderer = shield.GetComponent<MeshRenderer>();
        shieldRenderer.sharedMaterial = ShieldMaterial;
        shieldRenderer.shadowCastingMode = ShadowCastingMode.Off;
        var shieldRoot = new GameObject("ShieldBubble");
        shield.transform.SetParent(shieldRoot.transform, false);
        savePrefab(shieldRoot, "VFX/ShieldBubble");

        var smoke = loop(26.0f, 1.0f, 1.6f, 0.7f, new Color(1.0f, 0.55f, 0.15f, 0.9f), new Color(0.25f, 0.2f, 0.2f), ParticleSystemShapeType.Sphere, 0.45f);
        smoke.Gravity = -0.3f;
        savePrefab(createParticle("SunSmoke", smoke), "VFX/SunSmoke");

        var heal = burst(30, 0.9f, 2.5f, 0.4f, new Color(0.3f, 1.0f, 0.4f), new Color(0.1f, 0.4f, 0.1f), ParticleSystemShapeType.Circle, 0.6f);
        heal.Gravity = -0.6f;
        heal.ShapeRotation = new Vector3(-90.0f, 0.0f, 0.0f);
        savePrefab(createParticle("Heal", heal), "VFX/Heal");

        var slam = burst(70, 1.0f, 9.0f, 0.9f, new Color(0.7f, 0.55f, 0.4f, 0.9f), new Color(0.3f, 0.25f, 0.2f), ParticleSystemShapeType.Circle, 0.6f);
        slam.ShapeRotation = new Vector3(-90.0f, 0.0f, 0.0f);
        slam.Additive = false;
        savePrefab(createParticle("GroundSlam", slam), "VFX/GroundSlam");

        var feed = loop(40.0f, 0.8f, 2.0f, 0.35f, BloodRed, new Color(0.4f, 0.0f, 0.05f), ParticleSystemShapeType.Sphere, 0.6f);
        feed.Duration = 2.0f;
        feed.Gravity = -0.4f;
        savePrefab(createParticle("FeedBeam", feed), "VFX/FeedBeam");

        savePrefab(createParticle("Empower", loop(18.0f, 0.6f, 0.6f, 0.4f, new Color(1.0f, 0.15f, 0.2f, 0.8f), new Color(0.3f, 0.0f, 0.0f), ParticleSystemShapeType.Sphere, 0.7f)), "VFX/Empower");

        var stunRoot = new GameObject("Stun");
        var stun = createParticle("Stars", loop(12.0f, 0.6f, 0.8f, 0.3f, new Color(1.0f, 0.9f, 0.3f), new Color(1.0f, 0.6f, 0.1f), ParticleSystemShapeType.Circle, 0.4f), stunRoot.transform);
        stun.transform.localPosition = Vector3.up * 1.4f;
        savePrefab(stunRoot, "VFX/Stun");
    }

    // ------------------------------------------------------------------ projectiles

    private static void buildProjectiles()
    {
        buildProjectile("Projectiles/BloodBolt", PrimitiveType.Sphere, new Vector3(0.45f, 0.45f, 0.45f), createEmissive("M_BloodBolt", BloodRed, 4.0f), BloodRed, 0.35f, true);
        buildProjectile("Projectiles/Shockwave", PrimitiveType.Sphere, new Vector3(1.6f, 0.25f, 0.6f), createEmissive("M_Shockwave", new Color(1.0f, 0.25f, 0.2f), 3.0f), new Color(1.0f, 0.3f, 0.2f), 0.6f, false);
        buildProjectile("Projectiles/Arrow", PrimitiveType.Cylinder, new Vector3(0.05f, 0.45f, 0.05f), createEmissive("M_Arrow", new Color(0.6f, 0.5f, 0.35f), 0.2f), new Color(0.9f, 0.9f, 0.8f, 0.5f), 0.08f, false);

        var rockRoot = new GameObject("Rock");
        instantiateModel(nature("rock_smallA"), rockRoot.transform, 2.2f);
        addTrail(rockRoot, new Color(0.6f, 0.5f, 0.4f, 0.6f), 0.4f);
        savePrefab(rockRoot, "Projectiles/Rock");
    }

    private static void buildProjectile(string InAddress, PrimitiveType InPrimitive, Vector3 InScale, Material InMaterial, Color InTrail, float InTrailWidth, bool InLight)
    {
        var root = new GameObject(Path.GetFileName(InAddress));
        var body = GameObject.CreatePrimitive(InPrimitive);
        Object.DestroyImmediate(body.GetComponent<Collider>());
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = InScale;
        if (InPrimitive == PrimitiveType.Cylinder)
        {
            body.transform.localRotation = Quaternion.Euler(90.0f, 0.0f, 0.0f);
        }

        var renderer = body.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = InMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        addTrail(root, InTrail, InTrailWidth);
        if (InLight == true)
        {
            addPointLight(root.transform, Vector3.zero, InTrail, 5.0f, 3.0f);
        }

        savePrefab(root, InAddress);
    }

    private static void addTrail(GameObject InRoot, Color InColor, float InWidth)
    {
        var trail = InRoot.AddComponent<TrailRenderer>();
        trail.time = 0.25f;
        trail.minVertexDistance = 0.1f;
        trail.widthCurve = AnimationCurve.Linear(0.0f, InWidth, 1.0f, 0.0f);
        trail.sharedMaterial = ParticleAdditive;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(InColor, 0.0f), new GradientColorKey(InColor, 1.0f) },
            new[] { new GradientAlphaKey(InColor.a, 0.0f), new GradientAlphaKey(0.0f, 1.0f) });
        trail.colorGradient = gradient;
        trail.shadowCastingMode = ShadowCastingMode.Off;
    }
}
