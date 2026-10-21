using System.Collections.Generic;
using System.IO;
using ActionRPG.Abilities;
using ActionRPG.AI;
using ActionRPG.Character;
using ActionRPG.Combat;
using ActionRPG.Core;
using ActionRPG.Core.AbilitySystem;
using ActionRPG.InputSystem;
using ActionRPG.Networking;
using ActionRPG.Skills;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace ActionRPG.EditorTools
{
    /// <summary>
    /// 플레이어 / 적 / 화살 프리팹을 프리미티브 메시로 만들어 준다.
    /// 최종 아트로 교체하기 전에도 바로 플레이해서 시스템을 확인할 수 있게 하는 것이 목적이다.
    /// </summary>
    public static class SamplePrefabGenerator
    {
        private const string PrefabRoot = "Assets/Prefabs";
        private const string CharacterPath = PrefabRoot + "/Characters";
        private const string EnemyPath = PrefabRoot + "/Enemies";
        private const string ProjectilePath = PrefabRoot + "/Projectiles";
        private const string ResourcePath = "Assets/Resources";
        private const string DataRoot = "Assets/Data";

        [MenuItem("ActionRPG/레이어 설정 추가", priority = 1)]
        public static void SetupLayers()
        {
            AddLayer(GameConstants.Layers.Player);
            AddLayer(GameConstants.Layers.Enemy);
            AddLayer(GameConstants.Layers.Ground);
            AddLayer(GameConstants.Layers.Interactable);
            Debug.Log("[ActionRPG] 레이어를 확인/추가했습니다: Player, Enemy, Ground, Interactable");
        }

        [MenuItem("ActionRPG/샘플 프리팹 생성 (플레이어 · 적 · 화살)", priority = 11)]
        public static void GeneratePrefabs()
        {
            SetupLayers();
            foreach (var path in new[] { PrefabRoot, CharacterPath, EnemyPath, ProjectilePath })
                EnsureFolder(path);

            var arrow = CreateArrowPrefab();
            var player = CreatePlayerPrefab(arrow);
            var melee = CreateEnemyPrefab("Enemy_GoblinWarrior", "EN_GoblinWarrior", new Color(0.35f, 0.6f, 0.3f), null);
            var archer = CreateEnemyPrefab("Enemy_GoblinArcher", "EN_GoblinArcher", new Color(0.3f, 0.5f, 0.6f), arrow);
            var boss = CreateEnemyPrefab("Enemy_OrcChieftain", "EN_OrcChieftain", new Color(0.55f, 0.25f, 0.25f), null, scale: 1.5f);

            CreateNetworkPrefabsList(new[] { player, melee, archer, boss, arrow });

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[ActionRPG] 샘플 프리팹 생성 완료. NetworkManager 의 Player Prefab 에 PlayerCharacter 를, " +
                      "Network Prefabs Lists 에 ActionRPGNetworkPrefabs 를 지정하세요.");
            Selection.activeObject = player;
        }

        // ---------------------------------------------------------------- 플레이어

        private static GameObject CreatePlayerPrefab(GameObject arrowPrefab)
        {
            string path = $"{CharacterPath}/PlayerCharacter.prefab";

            var root = new GameObject("PlayerCharacter");
            root.layer = LayerMask.NameToLayer(GameConstants.Layers.Player);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);
            controller.slopeLimit = 50f;
            controller.stepOffset = 0.35f;

            // 보이는 몸통
            var body = CreateCapsuleVisual(root.transform, "Visual", new Color(0.85f, 0.8f, 0.7f));
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);

            // 무기 소켓
            var swordRoot = new GameObject("SwordRoot");
            swordRoot.transform.SetParent(root.transform, false);
            swordRoot.transform.localPosition = new Vector3(0.35f, 1.0f, 0.15f);
            var blade = CreateBoxVisual(swordRoot.transform, "Blade", new Color(0.75f, 0.78f, 0.85f), new Vector3(0.08f, 1.0f, 0.08f));
            blade.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            var bowRoot = new GameObject("BowRoot");
            bowRoot.transform.SetParent(root.transform, false);
            bowRoot.transform.localPosition = new Vector3(-0.3f, 1.1f, 0.1f);
            var bowLimb = CreateBoxVisual(bowRoot.transform, "Limb", new Color(0.5f, 0.35f, 0.2f), new Vector3(0.06f, 1.2f, 0.06f));
            bowLimb.transform.localPosition = Vector3.zero;
            bowRoot.SetActive(false);

            var meleeOrigin = new GameObject("MeleeOrigin");
            meleeOrigin.transform.SetParent(root.transform, false);
            meleeOrigin.transform.localPosition = new Vector3(0f, 1.0f, 0.6f);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(root.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 1.4f, 0.5f);

            // 컴포넌트 (ASC 를 Health 보다 앞에 둔다. OnNetworkSpawn 이 이 순서로 불리므로 어트리뷰트가 먼저 준비된다.)
            root.AddComponent<NetworkObject>();
            root.AddComponent<ClientNetworkTransform>();
            root.AddComponent<CharacterMotor>();
            var asc = root.AddComponent<AbilitySystemComponent>();
            var health = root.AddComponent<Health>();
            root.AddComponent<CharacterAnimatorDriver>();
            var weapons = root.AddComponent<WeaponController>();
            var caster = root.AddComponent<SkillCaster>();
            root.AddComponent<PlayerInteractor>();
            root.AddComponent<InputReader>();
            var playerController = root.AddComponent<PlayerController>();
            root.AddComponent<PlayerNetworkIdentity>();

            // 직렬화 필드 연결
            WireAbilitySystem(asc, "AS_Player", GameplayEffectReplicationMode.Mixed);
            SetEnum(health, "faction", (int)Faction.Player);
            SetFloat(health, "despawnDelay", 0f);   // 플레이어는 죽어도 사라지지 않는다

            WireWeapons(weapons, swordRoot, bowRoot, meleeOrigin.transform, muzzle.transform);
            WireBasicAttack(arrowPrefab);

            var deck = AssetDatabase.LoadAssetAtPath<SkillDeckPreset>(DataRoot + "/Skills/DECK_Swordsman.asset");
            if (deck != null) SetObject(caster, "fallbackDeck", deck);

            SetObject(playerController, "inputReader", root.GetComponent<InputReader>());

            var prefab = SaveAndCleanup(root, path);
            return prefab;
        }

        /// <summary>ASC 에 시작 AbilitySet 과 복제 모드를 넣는다 (플레이어 Mixed, AI Minimal — 언리얼 권장값).</summary>
        private static void WireAbilitySystem(AbilitySystemComponent asc, string abilitySetName, GameplayEffectReplicationMode mode)
        {
            var set = AssetDatabase.LoadAssetAtPath<AbilitySet>($"{DataRoot}/Abilities/{abilitySetName}.asset");
            if (set == null)
                Debug.LogWarning($"[ActionRPG] {abilitySetName} 이 없습니다. 샘플 콘텐츠 생성을 먼저 실행하세요.");
            else
                SetObjectArray(asc, "startupAbilitySets", new Object[] { set });
            SetEnum(asc, "replicationMode", (int)mode);
        }

        private static void WireWeapons(WeaponController weapons, GameObject swordRoot, GameObject bowRoot, Transform meleeOrigin, Transform muzzle)
        {
            var so = new SerializedObject(weapons);
            var array = so.FindProperty("weapons");
            array.arraySize = 2;

            var sword = array.GetArrayElementAtIndex(0);
            sword.FindPropertyRelative("Type").enumValueIndex = (int)WeaponType.Sword;
            sword.FindPropertyRelative("Root").objectReferenceValue = swordRoot;
            sword.FindPropertyRelative("MeleeOrigin").objectReferenceValue = meleeOrigin;
            sword.FindPropertyRelative("ProjectileMuzzle").objectReferenceValue = null;

            var bow = array.GetArrayElementAtIndex(1);
            bow.FindPropertyRelative("Type").enumValueIndex = (int)WeaponType.Bow;
            bow.FindPropertyRelative("Root").objectReferenceValue = bowRoot;
            bow.FindPropertyRelative("MeleeOrigin").objectReferenceValue = meleeOrigin;
            bow.FindPropertyRelative("ProjectileMuzzle").objectReferenceValue = muzzle;

            so.FindProperty("startingWeapon").enumValueIndex = (int)WeaponType.Sword;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>평타 어빌리티(GA_BasicAttack)의 활 프로필에 화살 프리팹을 꽂는다.</summary>
        private static void WireBasicAttack(GameObject arrowPrefab)
        {
            var ability = AssetDatabase.LoadAssetAtPath<GA_BasicAttack>($"{DataRoot}/Abilities/GA_BasicAttack.asset");
            if (ability == null) return;
            for (int i = 0; i < ability.Profiles.Length; i++)
                if (ability.Profiles[i].Weapon == WeaponType.Bow) ability.Profiles[i].ProjectilePrefab = arrowPrefab;
            EditorUtility.SetDirty(ability);
        }

        // ---------------------------------------------------------------- 적

        private static GameObject CreateEnemyPrefab(string prefabName, string definitionName, Color color, GameObject arrowPrefab, float scale = 1f)
        {
            string path = $"{EnemyPath}/{prefabName}.prefab";

            var root = new GameObject(prefabName);
            root.layer = LayerMask.NameToLayer(GameConstants.Layers.Enemy);

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f * scale;
            controller.radius = 0.35f * scale;
            controller.center = new Vector3(0f, 0.9f * scale, 0f);

            var visual = CreateCapsuleVisual(root.transform, "Visual", color);
            visual.transform.localPosition = new Vector3(0f, 0.9f * scale, 0f);
            visual.transform.localScale = Vector3.one * scale;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.35f * scale;
            agent.height = 1.8f * scale;
            agent.speed = 3.4f;
            agent.angularSpeed = 720f;
            agent.acceleration = 20f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = false;

            root.AddComponent<NetworkObject>();
            root.AddComponent<NetworkTransform>();   // 적은 서버 권한
            root.AddComponent<CharacterMotor>();
            var asc = root.AddComponent<AbilitySystemComponent>();
            var health = root.AddComponent<Health>();
            root.AddComponent<CharacterAnimatorDriver>();
            var brain = root.AddComponent<EnemyBrain>();

            WireAbilitySystem(asc, "AS_Enemy", GameplayEffectReplicationMode.Minimal);
            SetEnum(health, "faction", (int)Faction.Enemy);
            SetFloat(health, "despawnDelay", 4f);

            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>($"{DataRoot}/Enemies/{definitionName}.asset");
            if (definition != null)
            {
                SetObject(brain, "definition", definition);

                // 원거리 적이면 공격 패턴에 화살 프리팹을 꽂아 준다.
                if (arrowPrefab != null)
                {
                    var so = new SerializedObject(definition);
                    var attacks = so.FindProperty("Attacks");
                    for (int i = 0; i < attacks.arraySize; i++)
                        attacks.GetArrayElementAtIndex(i).FindPropertyRelative("ProjectilePrefab").objectReferenceValue = arrowPrefab;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            SetObject(brain, "agent", agent);

            return SaveAndCleanup(root, path);
        }

        // ---------------------------------------------------------------- 화살

        private static GameObject CreateArrowPrefab()
        {
            string path = $"{ProjectilePath}/Arrow.prefab";

            var root = new GameObject("Arrow");
            var shaft = CreateBoxVisual(root.transform, "Shaft", new Color(0.8f, 0.7f, 0.45f), new Vector3(0.05f, 0.05f, 0.8f));
            shaft.transform.localPosition = Vector3.zero;

            root.AddComponent<NetworkObject>();
            var projectile = root.AddComponent<Projectile>();

            SetFloat(projectile, "defaultSpeed", 30f);
            SetFloat(projectile, "gravity", 6f);
            SetFloat(projectile, "lifetime", 5f);
            SetFloat(projectile, "hitRadius", 0.25f);
            SetObject(projectile, "trailRoot", shaft);

            return SaveAndCleanup(root, path);
        }

        // ---------------------------------------------------------------- 네트워크 프리팹 목록

        private static void CreateNetworkPrefabsList(IEnumerable<GameObject> prefabs)
        {
            string path = $"{ResourcePath}/ActionRPGNetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                AssetDatabase.CreateAsset(list, path);
            }

            var so = new SerializedObject(list);
            var array = so.FindProperty("List");
            array.ClearArray();

            int index = 0;
            foreach (var prefab in prefabs)
            {
                if (prefab == null) continue;
                array.InsertArrayElementAtIndex(index);
                var element = array.GetArrayElementAtIndex(index);
                element.FindPropertyRelative("Override").enumValueIndex = 0;
                element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
                index++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(list);
        }

        // ---------------------------------------------------------------- 유틸

        private static GameObject CreateCapsuleVisual(Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = name;
            go.transform.SetParent(parent, false);
            Object.DestroyImmediate(go.GetComponent<Collider>());   // 판정은 CharacterController 가 한다
            ApplyColor(go, color);
            return go;
        }

        private static GameObject CreateBoxVisual(Transform parent, string name, Color color, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            ApplyColor(go, color);
            return go;
        }

        private static void ApplyColor(GameObject go, Color color)
        {
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            // URP 기본 셰이더를 쓰되, 없으면 렌더러 기본 머티리얼을 그대로 둔다.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;

            var material = new Material(shader) { color = color };
            renderer.sharedMaterial = material;
        }

        private static GameObject SaveAndCleanup(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static T[] LoadAll<T>(string folder) where T : Object
        {
            if (!AssetDatabase.IsValidFolder(folder)) return new T[0];

            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
            var results = new T[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                results[i] = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return results;
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) return;
            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string field, int value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) return;
            property.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObject(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) return;
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) return;

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>TagManager 를 직접 수정해 레이어를 추가한다. 이미 있으면 건드리지 않는다.</summary>
        private static void AddLayer(string layerName)
        {
            if (LayerMask.NameToLayer(layerName) >= 0) return;

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (asset == null || asset.Length == 0)
            {
                Debug.LogError("[ActionRPG] TagManager.asset 을 열 수 없습니다. 레이어를 직접 추가하세요.");
                return;
            }

            var tagManager = new SerializedObject(asset[0]);
            var layers = tagManager.FindProperty("layers");

            // 0~7 은 Unity 예약 레이어라 8번부터 찾는다.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var element = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(element.stringValue)) continue;

                element.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return;
            }

            Debug.LogError($"[ActionRPG] 빈 레이어 슬롯이 없어 '{layerName}' 를 추가하지 못했습니다.");
        }
    }
}
