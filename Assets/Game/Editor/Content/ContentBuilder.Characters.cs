using System.Collections.Generic;
using System.IO;
using Framework.Editor;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 캐릭터 프리팹 (외부 에셋).
///  - 플레이어: Mixamo Ganfaul (Humanoid, 2026-10-31 unity-chan 에서 교체) + ExplosiveLLC RPG Character(이동/공격) + Crafting(건축/채집) 애니메이션 리타게팅
///  - 몬스터: Magic Pig Games (Medusa / Cobra Snake / Troll / Minotaur) — 각 모델의 Generic 클립으로 컨트롤러 생성
///  - 무기: ExplosiveLLC 2Hand-Sword, Crafting Spear
/// 에셋 이름(클립명/본명/경로)으로 찾는 부분은 에디터 빌더에만 있고, 런타임은 AnimatorParam 해시만 쓴다.
/// </summary>
public static partial class ContentBuilder
{
    private const string External = "Assets/ExternalAssets";
    /// <summary>플레이어 모델 (Humanoid FBX). 바꾸려면 이 경로만 바꾸고 Build Content — 캡슐 크기 · 무기 소켓은 모델에서 계산한다.</summary>
    private const string PlayerModel = External + "/Mixamo/Models/Ganfaul/Ganfaul_Model.fbx";
    private const string RpgRoot = External + "/ExplosiveLLC/RPG Character Mecanim Animation Pack FREE";
    private const string RpgAnim = RpgRoot + "/Animations";
    private const string RpgCharacterPrefab = RpgRoot + "/Prefabs/Character/RPG-Character.prefab";
    private const string RpgSword = RpgRoot + "/Models/Weapons/2Hand-Sword.FBX";
    private const string CraftRoot = External + "/ExplosiveLLC/Crafting Mecanim Animation Pack";
    private const string CraftAnim = CraftRoot + "/Animations";
    private const string CraftSpear = CraftRoot + "/Props/Spear.FBX";

    /// <summary>모델에 딸린 데모 스크립트 중 구 Input 매니저/OnGUI 를 써서 이 프로젝트(Input System 전용)에서 예외를 내는 것 (예전 unity-chan 용, 다른 모델로 바꿔도 안전망으로 둔다).</summary>
    private static readonly HashSet<string> StripModelComponents = new HashSet<string> { "IdleChanger", "FaceUpdate", "IKCtrlRightHand", "RandomWind" };

    private struct AnimSet
    {
        public AnimationClip Idle;
        public AnimationClip Walk;
        public AnimationClip Run;
        public AnimationClip Dead;
        public Dictionary<EAnim, AnimationClip> Actions;
    }

    /// <summary>무기 크기 보정 (체격 비율은 humanScale 로 이미 맞춤). unity-chan 은 0.7 이었다.</summary>
    private const float WeaponScaleTweak = 0.9f;

    // 무기 소켓 (RPG-Character 의 B_R_Hand 프레임을 플레이어 모델 오른손에 옮긴 값). buildPlayer 에서 계산.
    private static Vector3 SocketPosition;
    private static Quaternion SocketRotation = Quaternion.identity;
    private static float SocketScale = 1.0f;

    /// <summary>
    /// 몬스터 클립을 제자리로 (MonsterAuthoring.MotionNode 가 있는 몬스터만).
    /// Generic 리그에 Root node 가 없으면 걷기 클립의 전진 이동이 그대로 뼈에 들어가서, 메시가 NavMeshAgent 루트보다
    /// 앞서 걷다가 클립이 반복될 때 루트로 튀어 돌아온다 (Issues 35).
    /// 기본 몬스터의 이동 뼈 / 클립 1회당 전진량: Medusa Root 1.18m, Cobra base_snake 9.93m, Troll Ogre_root 1.91m, Minotaur Hips 0.97m.
    /// </summary>
    [MenuItem("Game/Setup/3c. Make Monster Animations In-Place", priority = 3)]
    public static void MakeMonsterAnimationsInPlace()
    {
        foreach (var monster in MonsterAuthoringUtil.LoadAll())
        {
            makeInPlace(monster);
        }
    }

    private static void makeInPlace(MonsterAuthoring InMonster)
    {
        if (string.IsNullOrEmpty(InMonster.MotionNode) == false)
        {
            makeInPlace(MonsterAuthoringUtil.GetClipSource(InMonster.Model), InMonster.MotionNode);
        }
    }

    /// <summary>
    /// 클립을 제자리 애니메이션으로: Root node 를 지정하고 XZ 이동을 루트 모션으로 "추출" 한다 (Bake Into Pose 해제).
    /// Animator.applyRootMotion 이 false 라 추출된 이동은 버려지고 메시는 NavMeshAgent 루트에 붙어 있는다 → 이동은 Agent 만 담당.
    /// 높이(Y)와 회전은 포즈에 남겨(Bake) 걸음의 상하 흔들림·몸 비틀림은 유지한다.
    /// 외부 FBX 의 임포트 설정(.meta)만 바꾸고 모델/애니메이션 데이터는 건드리지 않는다.
    /// </summary>
    private static void makeInPlace(string InFbx, string InMotionNode)
    {
        var importer = AssetImporter.GetAtPath(InFbx) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Content] model importer not found: " + InFbx);
            return;
        }

        // 이미 처리된 모델은 다시 임포트하지 않는다 (StateTree Tool 에서 몬스터 하나만 다시 구울 때 대기 시간 방지).
        if (importer.motionNodeName == InMotionNode)
        {
            return;
        }

        var clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
        }

        for (int i = 0; i < clips.Length; ++i)
        {
            clips[i].lockRootPositionXZ = false;
            // 추출 기준 = Root node 자체 (Original). 기본값(무게중심)은 메두사처럼 무게중심이 루트 뼈와 멀면 루트 뼈 이동이 남는다.
            clips[i].keepOriginalPositionXZ = true;
            clips[i].lockRootHeightY = true;
            clips[i].lockRootRotation = true;
        }

        importer.motionNodeName = InMotionNode;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    private static void buildCharacters()
    {
        MakeMonsterAnimationsInPlace();
        buildPlayer();

        // 몬스터: 외형/애니메이션은 MonsterAuthoring 에셋(인스펙터로 편집)이 원본.
        foreach (var monster in MonsterAuthoringUtil.LoadAll())
        {
            buildMonster(monster);
        }

        buildMerchant();
    }

    // ------------------------------------------------------------------ player

    /// <summary>플레이어 프리팹만 다시 만든다 (모델 교체 후). 전체는 3. Build Content.</summary>
    [MenuItem("Game/Setup/3f. Build Player Only", priority = 3)]
    public static void BuildPlayerOnly()
    {
        MaterialUpgradeUtil.ClearCache();
        buildPlayer();
        AssetDatabase.SaveAssets();
    }

    private static void buildPlayer()
    {
        preparePlayerModelImport();
        var root = new GameObject("Player");
        var model = instantiateExternal(PlayerModel, root.transform);
        model.name = "Model";
        foreach (var component in model.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null && StripModelComponents.Contains(component.GetType().Name) == true)
            {
                Object.DestroyImmediate(component);
            }
        }

        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Player");

        var animator = model.GetComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.runtimeAnimatorController = buildController("Player", playerAnimSet(), 0.35f);
        animator.fireEvents = false;

        computeWeaponSocket();
        var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        var socket = new GameObject("WeaponSocket").transform;
        socket.SetParent(hand, false);
        socket.localPosition = SocketPosition;
        socket.localRotation = SocketRotation;
        socket.localScale = Vector3.one * SocketScale * WeaponScaleTweak;

        // 캡슐은 모델 키에 맞춘다 (바인드 포즈 렌더러 높이).
        float height = modelHeight(model);
        var controller = root.AddComponent<CharacterController>();
        controller.height = Mathf.Clamp(height * 0.95f, 1.2f, 2.4f);
        controller.center = new Vector3(0.0f, controller.height * 0.5f + 0.02f, 0.0f);
        controller.radius = 0.35f;
        controller.slopeLimit = 50.0f;
        controller.stepOffset = 0.4f;
        var player = root.AddComponent<PlayerCharacter>();
        player.Radius = 0.45f;
        root.AddComponent<PlayerController>();

        var actorAnimator = root.AddComponent<ActorAnimator>();
        actorAnimator.Animator = animator;
        actorAnimator.WeaponSocket = socket;
        setLayer(root, LayerId.Player);
        // Ganfaul 은 사실적인 텍스처라 툰 대신 일반(몬스터와 같은) 캐릭터 셰이딩.
        CharacterLook.Apply(root, false);
        savePrefab(root, "Characters/Player");
    }

    /// <summary>
    /// 플레이어 FBX 임포트: Humanoid(자체 아바타) + Optimize Game Objects 끔.
    /// Mixamo FBX 를 그대로 넣으면 Optimize 가 켜져 있어 뼈 Transform 이 숨겨지고, 무기를 붙일 오른손 본을 찾을 수 없었다.
    /// </summary>
    private static void preparePlayerModelImport()
    {
        var importer = AssetImporter.GetAtPath(PlayerModel) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("[Content] player model missing: " + PlayerModel);
            return;
        }

        bool changed = importer.animationType != ModelImporterAnimationType.Human || importer.optimizeGameObjects == true
                       || importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel;
        if (changed == false)
        {
            return;
        }

        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.optimizeGameObjects = false;
        importer.SaveAndReimport();
    }

    private static float modelHeight(GameObject InModel)
    {
        var renderers = InModel.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            return 1.7f;
        }

        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds.max.y - InModel.transform.position.y;
    }

    private static AnimSet playerAnimSet()
    {
        var set = new AnimSet();
        set.Idle = clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Idle.FBX");
        set.Walk = clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Walk.FBX");
        set.Run = clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Run-Forward.FBX");
        set.Dead = clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Knockdown1.FBX");
        set.Actions = new Dictionary<EAnim, AnimationClip>
        {
            { EAnim.Attack1, clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack1.FBX") },
            { EAnim.Attack2, clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack2.FBX") },
            { EAnim.Attack3, clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Attack3.FBX") },
            { EAnim.Cast, clip(RpgAnim + "/Unarmed/RPG-Character@Unarmed-Attack-R3.FBX") },
            { EAnim.Kick, clip(RpgAnim + "/Unarmed/RPG-Character@Unarmed-Attack-L2.FBX") },
            { EAnim.Dodge, clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-DiveRoll-Forward1.max.FBX") },
            { EAnim.Hit, clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-GetHit-F1.FBX") },
            { EAnim.Interact, clip(CraftAnim + "/Crafter@Item-Take.FBX") },
            { EAnim.PickUp, clip(CraftAnim + "/Crafter@Item-Pickup.FBX") },
            { EAnim.Build, clip(CraftAnim + "/Crafter@Item-Hammer-Wall.FBX") },
            { EAnim.Chop, clip(CraftAnim + "/Crafter@Chop-Horizontal.FBX") },
            { EAnim.Mine, clip(CraftAnim + "/Crafter@Item-Swing-Vertical.FBX") },
            { EAnim.Gather, clip(CraftAnim + "/Crafter@Gather.FBX") },
        };
        return set;
    }

    /// <summary>
    /// RPG-Character 와 플레이어 모델에 같은 Humanoid 포즈(2Hand-Sword-Idle 0프레임)를 샘플링한 뒤,
    /// 무기(B_R_Hand 원점에 붙은 2Hand-Sword)의 월드 자세를 플레이어 모델 오른손 로컬로 옮긴다.
    /// 본 축 방향이 리그마다 달라도 같은 포즈에서의 상대 자세는 같으므로 무기 방향이 맞는다.
    /// </summary>
    private static void computeWeaponSocket()
    {
        // 저장할 프리팹이 아니라 임시 인스턴스에 포즈를 샘플링한다 (손 로컬 기준 결과는 포즈와 무관).
        var playerModel = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerModel));
        var playerAnimator = playerModel.GetComponent<Animator>();
        var rpgPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RpgCharacterPrefab);
        if (rpgPrefab == null)
        {
            Debug.LogWarning("[Content] RPG-Character prefab missing, weapon socket uses identity");
            Object.DestroyImmediate(playerModel);
            return;
        }

        var rpg = Object.Instantiate(rpgPrefab);
        var rpgAnimator = rpg.GetComponentInChildren<Animator>();
        var poseClip = clip(RpgAnim + "/2Hand-Sword/RPG-Character@2Hand-Sword-Idle.FBX");
        poseClip.SampleAnimation(rpgAnimator.gameObject, 0.0f);
        poseClip.SampleAnimation(playerAnimator.gameObject, 0.0f);

        Transform sword = null;
        foreach (var t in rpg.GetComponentsInChildren<Transform>(true))
        {
            if (t.GetComponent<MeshFilter>() != null && t.name.Contains("Sword") == true)
            {
                sword = t;
                break;
            }
        }

        var theirHand = rpgAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        var ourHand = playerAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (sword == null || theirHand == null || ourHand == null)
        {
            Object.DestroyImmediate(rpg);
            Object.DestroyImmediate(playerModel);
            return;
        }

        float ratio = playerAnimator.humanScale / Mathf.Max(0.01f, rpgAnimator.humanScale);
        Vector3 worldOffset = (sword.position - theirHand.position) * ratio;
        SocketPosition = ourHand.InverseTransformPoint(ourHand.position + worldOffset);
        SocketRotation = Quaternion.Inverse(ourHand.rotation) * sword.rotation;
        SocketScale = sword.lossyScale.x * ratio / Mathf.Max(0.0001f, ourHand.lossyScale.x);
        Object.DestroyImmediate(rpg);
        Object.DestroyImmediate(playerModel);
    }

    // ------------------------------------------------------------------ monsters

    /// <summary>MonsterAuthoring 인스펙터의 "프리팹 빌드": 몬스터 하나만 제자리 처리 → 프리팹/컨트롤러 생성 → 저장.</summary>
    public static GameObject BuildMonster(MonsterAuthoring InMonster)
    {
        MaterialUpgradeUtil.ClearCache();
        makeInPlace(InMonster);
        var prefab = buildMonster(InMonster);
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static GameObject buildMonster(MonsterAuthoring InMonster)
    {
        if (InMonster.Model == null || string.IsNullOrEmpty(InMonster.Address) == true)
        {
            Debug.LogError($"[Content] monster '{InMonster.name}' has no model or address", InMonster);
            return null;
        }

        var root = new GameObject(Path.GetFileName(InMonster.Address));
        var model = instantiateExternal(AssetDatabase.GetAssetPath(InMonster.Model), root.transform);
        model.name = "Model";
        model.transform.localScale = Vector3.one * InMonster.ModelScale;
        stripToVisuals(model);
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Monsters");

        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null)
        {
            animator = model.AddComponent<Animator>();
        }

        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        var set = new AnimSet();
        set.Idle = InMonster.Idle;
        set.Walk = InMonster.Walk != null ? InMonster.Walk : InMonster.Idle;
        set.Dead = InMonster.Dead;
        set.Actions = new Dictionary<EAnim, AnimationClip>();
        for (int i = 0; i < InMonster.Actions.Count; ++i)
        {
            set.Actions[InMonster.Actions[i].Anim] = InMonster.Actions[i].Clip;
        }

        animator.runtimeAnimatorController = buildController("Monster_" + root.name, set, InMonster.WalkThreshold);
        animator.fireEvents = false;

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = InMonster.Radius;
        agent.height = InMonster.AgentHeight;
        agent.speed = 4.0f;
        agent.stoppingDistance = 0.3f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
        var monster = root.AddComponent<MonsterCharacter>();
        monster.Radius = InMonster.Radius;

        var actorAnimator = root.AddComponent<ActorAnimator>();
        actorAnimator.Animator = animator;
        setLayer(root, LayerId.Monster);
        CharacterLook.Apply(root, false);
        return savePrefab(root, InMonster.Address);
    }

    private static void buildMerchant()
    {
        // 상인은 Crafting 팩의 Crafter 캐릭터 (Humanoid) + Crafting Idle.
        var root = new GameObject("Merchant");
        var model = instantiateExternal(CraftRoot + "/Prefabs/Crafter.prefab", root.transform);
        model.name = "Model";
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            // 손에 든 소품(망치·도끼 등)은 숨긴다.
            filter.gameObject.SetActive(false);
        }

        stripToVisuals(model);
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Merchant");
        var animator = model.GetComponentInChildren<Animator>();
        var set = new AnimSet();
        set.Idle = clip(CraftAnim + "/Crafter@Idle.FBX");
        set.Walk = set.Idle;
        set.Dead = set.Idle;
        set.Actions = new Dictionary<EAnim, AnimationClip> { { EAnim.Interact, clip(CraftAnim + "/Crafter@Wave1.FBX") } };
        animator.runtimeAnimatorController = buildController("Merchant", set, 0.5f);
        animator.applyRootMotion = false;
        animator.fireEvents = false;

        var collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0.0f, 0.9f, 0.0f);
        collider.height = 1.8f;
        collider.radius = 0.4f;

        var tent = instantiateModel(nature("tent_detailedOpen"), root.transform, 4.0f);
        tent.transform.localPosition = new Vector3(0.0f, 0.0f, 2.5f);
        tent.transform.localRotation = Quaternion.Euler(0.0f, 180.0f, 0.0f);
        var fire = instantiateModel(nature("campfire_stones"), root.transform, 3.0f);
        fire.transform.localPosition = new Vector3(2.0f, 0.0f, -1.0f);
        addPointLight(fire.transform, Vector3.up * 0.2f, new Color(1.0f, 0.6f, 0.25f), 8.0f, 2.0f);
        root.AddComponent<Merchant>();
        setLayer(root, LayerId.Interactable);
        savePrefab(root, "Characters/Merchant");
    }

    // ------------------------------------------------------------------ weapons

    private static void buildWeapons()
    {
        buildWeapon("Weapons/Sword", RpgSword);
        buildWeapon("Weapons/Spear", CraftSpear);
    }

    /// <summary>ExplosiveLLC 무기는 B_R_Hand 원점 기준으로 모델링되어 있어 소켓(같은 프레임) 원점에 그대로 붙인다.</summary>
    private static void buildWeapon(string InAddress, string InModel)
    {
        var root = new GameObject(Path.GetFileName(InAddress));
        var model = instantiateExternal(InModel, root.transform);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        MaterialUpgradeUtil.UpgradeRenderers(model, EditorPaths.Generated + "/Materials/Weapons");
        savePrefab(root, InAddress);
    }

    // ------------------------------------------------------------------ animator

    private static AnimatorController buildController(string InName, AnimSet InSet, float InWalkThreshold)
    {
        string folder = EditorPaths.Generated + "/Animators";
        EditorAssetUtil.EnsureFolder(folder);
        string path = $"{folder}/{InName}.controller";
        AssetDatabase.DeleteAsset(path);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter(new AnimatorControllerParameter { name = "AttackSpeed", type = AnimatorControllerParameterType.Float, defaultFloat = 1.0f });

        // 모든 EAnim 트리거를 만든다 (상태가 없는 트리거도 "파라미터 없음" 경고 방지용으로 둔다).
        var added = new HashSet<string> { "Speed", "Dead", "AttackSpeed" };
        foreach (EAnim anim in System.Enum.GetValues(typeof(EAnim)))
        {
            if (anim != EAnim.None)
            {
                string trigger = AnimatorParam.TriggerName(anim);
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                added.Add(trigger);
            }
        }

        addAnimationTableParameters(controller, added);

        var machine = controller.layers[0].stateMachine;
        BlendTree tree;
        var locomotion = controller.CreateBlendTreeInController("Locomotion", out tree, 0);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(InSet.Idle, 0.0f);
        tree.AddChild(InSet.Walk, InWalkThreshold);
        if (InSet.Run != null)
        {
            tree.AddChild(InSet.Run, 1.0f);
        }
        else
        {
            tree.AddChild(InSet.Walk, 1.0f);
            var children = tree.children;
            children[children.Length - 1].timeScale = 1.7f;
            tree.children = children;
        }

        machine.defaultState = locomotion;

        foreach (var pair in InSet.Actions)
        {
            if (pair.Value == null)
            {
                continue;
            }

            string trigger = AnimatorParam.TriggerName(pair.Key);
            var state = machine.AddState(trigger);
            state.motion = pair.Value;
            state.speedParameterActive = true;
            state.speedParameter = "AttackSpeed";

            var enter = machine.AddAnyStateTransition(state);
            enter.AddCondition(AnimatorConditionMode.If, 0.0f, trigger);
            enter.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Dead");
            enter.duration = 0.08f;
            enter.canTransitionToSelf = true;

            var exit = state.AddTransition(locomotion);
            exit.hasExitTime = true;
            exit.exitTime = 0.88f;
            exit.duration = 0.15f;
        }

        var dead = machine.AddState("Dead");
        dead.motion = InSet.Dead;
        var toDead = machine.AddAnyStateTransition(dead);
        toDead.AddCondition(AnimatorConditionMode.If, 0.0f, "Dead");
        toDead.canTransitionToSelf = false;
        toDead.duration = 0.12f;
        var revive = dead.AddTransition(locomotion);
        revive.AddCondition(AnimatorConditionMode.IfNot, 0.0f, "Dead");
        revive.duration = 0.2f;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    /// <summary>AnimationTable.Parameters 에만 있는 파라미터도 만든다 (런타임 Play 가 "파라미터 없음" 경고를 내지 않도록).</summary>
    private static void addAnimationTableParameters(AnimatorController InController, HashSet<string> InAdded)
    {
        var tables = AssetDatabase.LoadAssetAtPath<GameTables>(EditorPaths.GameTablesAsset);
        if (tables == null || tables.Animation == null)
        {
            return;
        }

        foreach (var row in tables.Animation.All)
        {
            foreach (var parameter in row.Parameters)
            {
                if (InAdded.Add(parameter.Name) == true)
                {
                    InController.AddParameter(parameter.Name, toControllerType(parameter.Type));
                }
            }
        }
    }

    private static AnimatorControllerParameterType toControllerType(EAnimParamType InType)
    {
        switch (InType)
        {
            case EAnimParamType.Bool:
                return AnimatorControllerParameterType.Bool;
            case EAnimParamType.Float:
                return AnimatorControllerParameterType.Float;
            case EAnimParamType.Int:
                return AnimatorControllerParameterType.Int;
            default:
                return AnimatorControllerParameterType.Trigger;
        }
    }

    /// <summary>FBX 안의 첫 클립 (ExplosiveLLC 는 FBX 하나에 클립 하나).</summary>
    private static AnimationClip clip(string InFbx)
    {
        return clip(InFbx, null);
    }

    private static AnimationClip clip(string InFbx, string InName)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InFbx))
        {
            var animation = asset as AnimationClip;
            if (animation == null || animation.name.StartsWith("__preview") == true)
            {
                continue;
            }

            if (InName == null || animation.name == InName)
            {
                return animation;
            }
        }

        Debug.LogWarning($"[Content] clip '{InName}' not found in {InFbx}");
        return null;
    }

    // ------------------------------------------------------------------ helpers

    private static GameObject instantiateExternal(string InPrefabOrModel, Transform InParent)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(InPrefabOrModel);
        if (source == null)
        {
            Debug.LogError($"[Content] external asset not found: {InPrefabOrModel}");
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.transform.SetParent(InParent, false);
            return placeholder;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.transform.SetParent(InParent, false);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        return instance;
    }

    /// <summary>외부 데모 스크립트/오디오/파티클/콜라이더를 제거하고 Animator 와 렌더러만 남긴다.</summary>
    private static void stripToVisuals(GameObject InModel)
    {
        foreach (var behaviour in InModel.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour != null)
            {
                Object.DestroyImmediate(behaviour);
            }
        }

        foreach (var system in InModel.GetComponentsInChildren<ParticleSystem>(true))
        {
            Object.DestroyImmediate(system.GetComponent<ParticleSystemRenderer>());
            Object.DestroyImmediate(system);
        }

        foreach (var audio in InModel.GetComponentsInChildren<AudioSource>(true))
        {
            Object.DestroyImmediate(audio);
        }

        foreach (var collider in InModel.GetComponentsInChildren<Collider>(true))
        {
            Object.DestroyImmediate(collider);
        }

        foreach (var t in InModel.GetComponentsInChildren<Transform>(true))
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }
    }
}
