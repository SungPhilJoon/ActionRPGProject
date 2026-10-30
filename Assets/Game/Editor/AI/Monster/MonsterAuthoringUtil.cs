using System.Collections.Generic;
using Framework.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>MonsterAuthoring 에셋 찾기/생성과 모델 클립 조회.</summary>
public static class MonsterAuthoringUtil
{
    private const string MagicPig = "Assets/ExternalAssets/Magic Pig Games (Infinity PBR)/Characters";

    /// <summary>EditorPaths.Monsters 의 모든 저작 에셋 (CharacterId 순). 하나도 없으면 기본 몬스터 4종을 먼저 만든다.</summary>
    public static List<MonsterAuthoring> LoadAll()
    {
        EnsureDefaults();
        var result = new List<MonsterAuthoring>();
        foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(MonsterAuthoring), new[] { EditorPaths.Monsters }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<MonsterAuthoring>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
            {
                result.Add(asset);
            }
        }

        result.Sort((a, b) => a.CharacterId.CompareTo(b.CharacterId));
        return result;
    }

    public static MonsterAuthoring Create(string InName)
    {
        EditorAssetUtil.EnsureFolder(EditorPaths.Monsters);
        var asset = ScriptableObject.CreateInstance<MonsterAuthoring>();
        AssetDatabase.CreateAsset(asset, AssetDatabase.GenerateUniqueAssetPath($"{EditorPaths.Monsters}/{InName}.asset"));
        return asset;
    }

    /// <summary>모델의 클립 원본 경로 (Avatar 가 들어 있는 FBX). Avatar 가 없으면 모델 에셋 자체.</summary>
    public static string GetClipSource(GameObject InModel)
    {
        if (InModel == null)
        {
            return null;
        }

        var animator = InModel.GetComponentInChildren<Animator>(true);
        if (animator != null && animator.avatar != null)
        {
            return AssetDatabase.GetAssetPath(animator.avatar);
        }

        return AssetDatabase.GetAssetPath(InModel);
    }

    /// <summary>모델 FBX 안의 클립 목록 (미리보기용 __preview 클립 제외).</summary>
    public static List<AnimationClip> GetModelClips(GameObject InModel)
    {
        var clips = new List<AnimationClip>();
        string source = GetClipSource(InModel);
        if (string.IsNullOrEmpty(source) == true)
        {
            return clips;
        }

        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(source))
        {
            var clip = asset as AnimationClip;
            if (clip != null && clip.name.StartsWith("__preview") == false)
            {
                clips.Add(clip);
            }
        }

        clips.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return clips;
    }

    // ------------------------------------------------------------------ defaults

    /// <summary>
    /// 저작 에셋이 하나도 없을 때 기존 몬스터 4종(이전에 ContentBuilder 에 하드코딩되어 있던 값)을 에셋으로 만든다.
    /// 이후에는 에셋이 원본이며 인스펙터(MonsterAuthoringEditor)로 편집한다.
    /// </summary>
    public static void EnsureDefaults()
    {
        if (AssetDatabase.IsValidFolder(EditorPaths.Monsters) == true && AssetDatabase.FindAssets("t:" + nameof(MonsterAuthoring), new[] { EditorPaths.Monsters }).Length > 0)
        {
            return;
        }

        createDefault(2, "Characters/BanditThug", MagicPig + "/Medusa/_Prefabs/Medusa_Humanoid DEMO.prefab", 1.0f, 0.5f, "Root",
            "Idle", "Walk", "Death", new[] { "Attack1", "Attack2", "Attack3", "Taunt", "Attack3", "Dodge", "GotHit" });
        createDefault(3, "Characters/BanditArcher", MagicPig + "/Cobra Snake/_Prefabs/CobraSnake.prefab", 0.55f, 0.9f, "base_snake",
            "AlertIdle", "AlertWalk", "Death", new[] { "Attack1", "Attack2", "Attack3", "Spit1", "Spit2", "Dodge", "GotHit" });
        createDefault(4, "Characters/OrcBrute", MagicPig + "/Troll/_Prefabs/Troll.prefab", 1.0f, 0.9f, "Ogre_root",
            "Idle", "Walk", "Death", new[] { "Attack 1", "Attack 2", "Attack 3", "Taunt", "Jump", "Dodge", "Got Hit" });
        createDefault(5, "Characters/BossGrimgor", MagicPig + "/Minotaur/_Prefabs/Minotaur.prefab", 1.0f, 0.8f, "Hips",
            "Idle", "Walk", "Death", new[] { "Attack1", "Attack2", "Attack3", "Taunt", "RamAttack", "Dodge", "GotHit" });
        AssetDatabase.SaveAssets();
    }

    /// <summary>InActions 순서: Attack1, Attack2, Attack3, Cast, Special1, Dodge, Hit.</summary>
    private static void createDefault(int InCharacterId, string InAddress, string InModel, float InScale, float InRadius, string InMotionNode,
        string InIdle, string InWalk, string InDead, string[] InActions)
    {
        var asset = Create(System.IO.Path.GetFileName(InAddress));
        asset.CharacterId = InCharacterId;
        asset.Address = InAddress;
        asset.Model = AssetDatabase.LoadAssetAtPath<GameObject>(InModel);
        asset.ModelScale = InScale;
        asset.Radius = InRadius;
        asset.MotionNode = InMotionNode;

        var clips = GetModelClips(asset.Model);
        asset.Idle = findClip(clips, InIdle);
        asset.Walk = findClip(clips, InWalk);
        asset.Dead = findClip(clips, InDead);
        EAnim[] order = { EAnim.Attack1, EAnim.Attack2, EAnim.Attack3, EAnim.Cast, EAnim.Special1, EAnim.Dodge, EAnim.Hit };
        for (int i = 0; i < order.Length && i < InActions.Length; ++i)
        {
            asset.SetAction(order[i], findClip(clips, InActions[i]));
        }

        EditorUtility.SetDirty(asset);
    }

    private static AnimationClip findClip(List<AnimationClip> InClips, string InName)
    {
        for (int i = 0; i < InClips.Count; ++i)
        {
            if (InClips[i].name == InName)
            {
                return InClips[i];
            }
        }

        Debug.LogWarning($"[StateTree Tool] clip '{InName}' not found");
        return null;
    }
}
