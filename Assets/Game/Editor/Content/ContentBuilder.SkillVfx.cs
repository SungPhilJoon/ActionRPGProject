using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 스킬/상태이상 연출 — SpecialSkillsEffectsPack (GAPH 셰이더: Integrated / Distortion / Decal, URP 서브셰이더 포함).
/// 원본 프리팹은 데모용이라 크기가 수십 m 이므로, 파티클 모양 반지름으로 실제 크기를 재서 목표 반지름에 맞춘다.
/// 원본 이펙트의 보조 스크립트가 풀 재사용과 맞지 않아(Start 1회 초기화, 끝나면 자기 Destroy),
/// 래퍼(PooledEffect)가 꺼낼 때마다 원본을 새로 만든다. 원본 에셋은 수정하지 않는다.
/// 크기 미세 조정은 CueTable.Scale 로 한다 (코드 수정 불필요).
/// </summary>
public static partial class ContentBuilder
{
    private const string GaphSet1 = External + "/SpecialSkillsEffectsPack/AllEffects/EffectsSet_1(NotScriptBased)/Effects/";
    private const string GaphSet2 = External + "/SpecialSkillsEffectsPack/AllEffects/EffectsSet_2(ScriptBased)/Effects/";
    /// <summary>지면형 상태 표시는 CueSpawner 가 머리 높이(1m)에 붙이므로 발밑으로 내린다.</summary>
    private static readonly Vector3 FeetOffset = new Vector3(0.0f, -1.0f, 0.0f);

    [MenuItem("Game/Setup/3b. Build Skill VFX Only", priority = 3)]
    public static void BuildSkillVfxOnly()
    {
        buildMaterials();
        buildSkillVfx();
        AssetDatabase.SaveAssets();
        Debug.Log("[Content] skill vfx built");
    }

    private static void buildSkillVfx()
    {
        // 타격 / 무기 스킬
        buildEffect("VFX/Skill/SwordSlash", GaphSet2 + "Effect_06_MassiveSlash/Effect_06_Parts/Effect_06_Slash", 1.8f, false, Vector3.up * 0.2f, Vector3.zero);
        buildEffect("VFX/Skill/HitImpact", GaphSet1 + "Effect_20_RapidFire/Effect_20_Base/Effect_20_HitEffects", 0.6f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/Whirlwind", GaphSet1 + "Effect_22_WindCyclone/Effect_22_WindCyclone", 3.2f, false, Vector3.zero, Vector3.zero);

        // 주문
        buildEffect("VFX/Skill/VeilBlood", GaphSet1 + "Effect_14_MadnessBloodBoom/Effect_14_MadnessBloodBoom", 1.4f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/FireBlast", GaphSet1 + "Effect_45_BlastFlame/Effect_45_BlastFlame", 1.8f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/BloodRiteAura", GaphSet1 + "Effect_06_BloodFlood/Effect_06_BloodFlood", 1.5f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Skill/BloodNova", GaphSet1 + "Effect_14_MadnessBloodBoom/Effect_14_MadnessBloodBoom", 4.0f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/IceNova", GaphSet1 + "Effect_25_IceField/Effect_25_IceField", 3.5f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/LightningBurst", GaphSet1 + "Effect_52_LightningField/Effect_52_LightningField", 1.8f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/CrimsonStorm", GaphSet1 + "Effect_06_BloodFlood/Effect_06_BloodFlood", 5.0f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/DarkBurst", GaphSet1 + "Effect_27_ExposeOfDarkness/Effect_27_ExposeOfDarkness", 1.4f, false, Vector3.zero, Vector3.zero);
        buildEffect("VFX/Skill/WraithHit", GaphSet1 + "Effect_47_PreciseShot/Effect_47_Base/Effect_47_HitEffects", 0.9f, false, Vector3.zero, Vector3.zero);

        // 상태이상 (지속형: 대상에 붙어 있다가 이펙트가 빠지면 회수 → 반복 재생)
        buildEffect("VFX/Status/Frozen", GaphSet1 + "Effect_49_IceBlockCrash/Effect_49_Base(IceBlockCrash)/Effect_49_IceBlock", 1.0f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Status/Ignite", GaphSet1 + "Effect_30_MagmaStrike/Effect_30_MagmaField", 0.8f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Status/Condemn", GaphSet1 + "Effect_43_DarkChainSwamp/Effect_43_DarkChainSwamp", 0.9f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Status/Weaken", GaphSet1 + "Effect_46_PoisonSmoke/Effect_46_PoisonSmoke", 0.8f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Status/Chill", GaphSet1 + "Effect_25_IceField/Effect_25_IceField", 0.8f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Status/Static", GaphSet1 + "Effect_52_LightningField/Effect_52_LightningField", 0.8f, true, FeetOffset, Vector3.zero);
        buildEffect("VFX/Status/Leech", GaphSet1 + "Effect_06_BloodFlood/Effect_06_BloodFlood", 0.8f, true, FeetOffset, Vector3.zero);

        // 발사체 (Projectile 컴포넌트가 루트를 움직이고, 이펙트는 자식)
        buildEffectProjectile("Projectiles/FireBall", GaphSet1 + "Effect_24_FireBall/Effect_24_FireBall", 0.5f, new Color(1.0f, 0.45f, 0.1f));
        buildEffectProjectile("Projectiles/BallLightning", GaphSet1 + "Effect_04_ChargeShot/Effect_04_ChargeShot", 0.7f, new Color(0.7f, 0.55f, 1.0f));
        buildEffectProjectile("Projectiles/CorruptedSkull", GaphSet1 + "Effect_27_ExposeOfDarkness/Effect_27_ExposeOfDarkness", 0.5f, new Color(0.35f, 0.85f, 0.35f));
        buildEffectProjectile("Projectiles/WraithSpear", GaphSet1 + "Effect_47_PreciseShot/Effect_47_Base/Effect_47_TrailParticle", 0.4f, new Color(0.5f, 0.95f, 0.9f));
        buildEffectProjectile("Projectiles/Shockwave", GaphSet1 + "Effect_08_GroundSlash/Effect_08_GroundSlash", 1.0f, new Color(1.0f, 0.3f, 0.2f));
    }

    /// <summary>원본 이펙트를 감싼 풀용 프리팹. InTargetRadius(m) 에 맞게 자식 배율을 계산한다.</summary>
    private static void buildEffect(string InAddress, string InSourcePath, float InTargetRadius, bool InLoop, Vector3 InOffset, Vector3 InEuler)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(InSourcePath + ".prefab");
        if (source == null)
        {
            Debug.LogWarning("[Content] effect missing: " + InSourcePath);
            return;
        }

        var root = new GameObject(System.IO.Path.GetFileName(InAddress));
        var effect = root.AddComponent<PooledEffect>();
        effect.Source = source;
        effect.ChildScale = InTargetRadius / measureEffectRadius(source);
        effect.ChildOffset = InOffset;
        effect.ChildEuler = InEuler;
        effect.Loop = InLoop;
        savePrefab(root, InAddress);
    }

    private static void buildEffectProjectile(string InAddress, string InSourcePath, float InTargetRadius, Color InLight)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(InSourcePath + ".prefab");
        if (source == null)
        {
            Debug.LogWarning("[Content] effect missing: " + InSourcePath);
            return;
        }

        var root = new GameObject(System.IO.Path.GetFileName(InAddress));
        var effect = root.AddComponent<PooledEffect>();
        effect.Source = source;
        effect.ChildScale = InTargetRadius / measureEffectRadius(source);
        effect.Loop = true;
        addPointLight(root.transform, Vector3.zero, InLight, 4.0f, 2.5f);
        savePrefab(root, InAddress);
    }

    /// <summary>
    /// 이펙트의 대략적인 반지름 (원본 좌표계). 파티클 Shape 반지름과 시작 크기의 절반 중 큰 값.
    /// 이펙트 팩의 파티클은 모두 ScalingMode.Hierarchy 라서 루트 배율로 전체 크기가 같이 줄어든다.
    /// </summary>
    private static float measureEffectRadius(GameObject InSource)
    {
        float radius = 0.0f;
        foreach (var system in InSource.GetComponentsInChildren<ParticleSystem>(true))
        {
            float scale = system.transform.lossyScale.x;
            var shape = system.shape;
            if (shape.enabled == true)
            {
                radius = Mathf.Max(radius, shape.radius * scale);
            }

            radius = Mathf.Max(radius, system.main.startSize.constantMax * scale * 0.5f);
        }

        return Mathf.Max(0.01f, radius);
    }
}
