using System.Collections.Generic;
using System.IO;
using ActionRPG.Character;
using ActionRPG.Core;
using ActionRPG.Dungeon;
using ActionRPG.Networking;
using ActionRPG.Skills;
using ActionRPG.Tutorial;
using ActionRPG.UI;
using TMPro;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ActionRPG.EditorTools
{
    /// <summary>
    /// 메인 메뉴 / 튜토리얼 / 던전 세 씬을 한 번에 만들어 Build Settings 에 등록한다.
    /// 여기서 만든 씬은 최종 아트가 아니라 시스템 검증용 뼈대다.
    /// </summary>
    public static class SampleSceneGenerator
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string PrefabRoot = "Assets/Prefabs";
        private const string DataRoot = "Assets/Data";
        private const string ResourceRoot = "Assets/Resources";

        [MenuItem("ActionRPG/샘플 씬 생성 (메뉴 · 튜토리얼 · 던전)", priority = 12)]
        public static void GenerateScenes()
        {
            if (!EditorUtility.DisplayDialog(
                    "샘플 씬 생성",
                    "MainMenu / Tutorial / Dungeon_01 씬을 만들고 Build Settings 에 등록합니다.\n같은 이름의 씬이 있으면 덮어씁니다. 계속할까요?",
                    "생성", "취소"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(SceneFolder);

            var player = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Characters/PlayerCharacter.prefab");
            if (player == null)
            {
                EditorUtility.DisplayDialog("먼저 프리팹을 만드세요",
                    "PlayerCharacter 프리팹이 없습니다.\nActionRPG > 샘플 콘텐츠 생성 -> 샘플 프리팹 생성 순서로 먼저 실행하세요.", "확인");
                return;
            }

            if (TMP_Settings.defaultFontAsset == null)
                Debug.LogWarning("[ActionRPG] TextMeshPro 기본 폰트가 없습니다. Window > TextMeshPro > Import TMP Essential Resources 를 먼저 실행하세요.");

            string menuPath = BuildMainMenuScene(player);
            string tutorialPath = BuildTutorialScene(player);
            string dungeonPath = BuildDungeonScene(player);

            RegisterBuildScenes(menuPath, tutorialPath, dungeonPath);

            EditorSceneManager.OpenScene(menuPath);
            Debug.Log("[ActionRPG] 샘플 씬 생성 완료. MainMenu 씬에서 재생하세요.");
        }

        // ---------------------------------------------------------------- 메인 메뉴

        private static string BuildMainMenuScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateCamera(new Vector3(0f, 2f, -6f), Quaternion.Euler(10f, 0f, 0f), clearColor: new Color(0.05f, 0.06f, 0.09f));
            CreateEventSystem();
            CreateNetworkStack(playerPrefab, autoStartHost: false);

            var canvas = UIBuilder.CreateCanvas("MenuCanvas");
            var menu = canvas.gameObject.AddComponent<MainMenuView>();

            // --- 메인 패널 ---
            var mainPanel = UIBuilder.CreatePanel(canvas.transform, "MainPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620f, 720f), UIBuilder.PanelColor);

            UIBuilder.CreateText(mainPanel, "Title", "무너진 성채", 58f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(560f, 80f)).color = UIBuilder.AccentColor;

            UIBuilder.CreateText(mainPanel, "Subtitle", "4인 협동 액션 RPG", 24f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(560f, 40f));

            UIBuilder.CreateText(mainPanel, "NameLabel", "이름", 22f, TextAlignmentOptions.Left,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -180f), new Vector2(480f, 30f));
            var nameField = UIBuilder.CreateInputField(mainPanel, "NameField", "모험가",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -215f), new Vector2(480f, 56f));

            UIBuilder.CreateText(mainPanel, "AddressLabel", "접속 주소 / 포트", 22f, TextAlignmentOptions.Left,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -285f), new Vector2(480f, 30f));
            var addressField = UIBuilder.CreateInputField(mainPanel, "AddressField", GameConstants.DefaultAddress,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-70f, -320f), new Vector2(340f, 56f));
            var portField = UIBuilder.CreateInputField(mainPanel, "PortField", GameConstants.DefaultPort.ToString(),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(150f, -320f), new Vector2(120f, 56f));

            var tutorialButton = UIBuilder.CreateButton(mainPanel, "TutorialButton", "튜토리얼 시작",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(480f, 64f), out _);
            var hostButton = UIBuilder.CreateButton(mainPanel, "HostButton", "방 만들기 (호스트)",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -474f), new Vector2(480f, 64f), out _);
            var joinButton = UIBuilder.CreateButton(mainPanel, "JoinButton", "참가하기",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -548f), new Vector2(480f, 64f), out _);
            var deckButton = UIBuilder.CreateButton(mainPanel, "DeckButton", "덱 편집",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-122f, -622f), new Vector2(236f, 64f), out _);
            var quitButton = UIBuilder.CreateButton(mainPanel, "QuitButton", "종료",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(122f, -622f), new Vector2(236f, 64f), out _);

            var statusText = UIBuilder.CreateText(canvas.transform, "StatusText", string.Empty, 24f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(900f, 40f));
            statusText.color = new Color(1f, 0.6f, 0.5f);

            var connectingPanel = UIBuilder.CreatePanel(canvas.transform, "ConnectingPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(520f, 200f), UIBuilder.PanelColor);
            UIBuilder.CreateText(connectingPanel, "Label", "접속 중...", 32f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(480f, 60f));
            connectingPanel.gameObject.SetActive(false);

            // --- 덱 편집 패널 ---
            var deckPanel = BuildDeckBuilderPanel(canvas.transform, menu);
            deckPanel.SetActive(false);

            // --- 바인딩 ---
            var so = new SerializedObject(menu);
            so.FindProperty("nameField").objectReferenceValue = nameField;
            so.FindProperty("addressField").objectReferenceValue = addressField;
            so.FindProperty("portField").objectReferenceValue = portField;
            so.FindProperty("tutorialButton").objectReferenceValue = tutorialButton;
            so.FindProperty("hostButton").objectReferenceValue = hostButton;
            so.FindProperty("joinButton").objectReferenceValue = joinButton;
            so.FindProperty("deckButton").objectReferenceValue = deckButton;
            so.FindProperty("quitButton").objectReferenceValue = quitButton;
            so.FindProperty("mainPanel").objectReferenceValue = mainPanel.gameObject;
            so.FindProperty("deckPanel").objectReferenceValue = deckPanel;
            so.FindProperty("connectingPanel").objectReferenceValue = connectingPanel.gameObject;
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.ApplyModifiedPropertiesWithoutUndo();

            string path = $"{SceneFolder}/{GameConstants.Scenes.MainMenu}.unity";
            SaveSceneWithNetworkIds(scene, path);
            return path;
        }

        private static GameObject BuildDeckBuilderPanel(Transform parent, MainMenuView menu)
        {
            var panelRect = UIBuilder.CreatePanel(parent, "DeckPanel", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1500f, 880f), UIBuilder.PanelColor);
            var view = panelRect.gameObject.AddComponent<DeckBuilderView>();

            UIBuilder.CreateText(panelRect, "Title", "덱 편집 — 스킬 4개를 골라 출전한다", 34f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -40f), new Vector2(900f, 50f)).color = UIBuilder.AccentColor;

            // 후보 목록 (스크롤 없이 그리드로. 스킬이 늘면 ScrollRect 로 바꾼다)
            var libraryRect = UIBuilder.CreatePanel(panelRect, "Library", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -110f), new Vector2(880f, 560f), new Color(0.04f, 0.05f, 0.07f, 0.8f));
            var grid = libraryRect.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(200f, 120f);
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(14, 14, 14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            // 덱 슬롯 4칸
            var deckRow = UIBuilder.CreatePanel(panelRect, "DeckSlots", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -700f), new Vector2(880f, 150f), new Color(0.04f, 0.05f, 0.07f, 0.8f));
            var deckLayout = deckRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            deckLayout.spacing = 16f;
            deckLayout.padding = new RectOffset(16, 16, 14, 14);
            deckLayout.childForceExpandWidth = true;
            deckLayout.childForceExpandHeight = true;

            var cardPrefab = LoadOrCreateCardPrefab();
            var deckSlots = new SkillCardView[GameConstants.SkillSlotCount];
            for (int i = 0; i < deckSlots.Length; i++)
            {
                deckSlots[i] = (SkillCardView)PrefabUtility.InstantiatePrefab(cardPrefab, deckRow);
                deckSlots[i].name = $"DeckSlot{i}";
            }

            // 상세 패널
            var detail = UIBuilder.CreatePanel(panelRect, "Detail", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -110f), new Vector2(520f, 560f), new Color(0.04f, 0.05f, 0.07f, 0.8f));
            var detailIcon = UIBuilder.CreateImage(detail, "Icon", Color.white, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(120f, 120f));
            detailIcon.enabled = false;
            var detailName = UIBuilder.CreateText(detail, "Name", string.Empty, 32f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(460f, 46f));
            var detailDesc = UIBuilder.CreateText(detail, "Description", string.Empty, 22f, TextAlignmentOptions.TopLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(460f, 140f));
            var detailStats = UIBuilder.CreateText(detail, "Stats", string.Empty, 22f, TextAlignmentOptions.TopLeft,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(460f, 160f));

            var costText = UIBuilder.CreateText(panelRect, "Cost", "코스트 0 / 8", 26f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -700f), new Vector2(480f, 40f));
            var warningText = UIBuilder.CreateText(panelRect, "Warning", string.Empty, 22f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -745f), new Vector2(480f, 40f));
            warningText.color = new Color(1f, 0.6f, 0.4f);

            var saveButton = UIBuilder.CreateButton(panelRect, "SaveButton", "저장",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 40f), new Vector2(200f, 60f), out _);
            var clearButton = UIBuilder.CreateButton(panelRect, "ClearButton", "비우기",
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-260f, 40f), new Vector2(200f, 60f), out _);
            var closeButton = UIBuilder.CreateButton(panelRect, "CloseButton", "돌아가기",
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 40f), new Vector2(220f, 60f), out _);
            closeButton.onClick.AddListener(menu.ShowMain);

            var so = new SerializedObject(view);
            so.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
            so.FindProperty("libraryParent").objectReferenceValue = libraryRect;

            var slotsProp = so.FindProperty("deckSlots");
            slotsProp.arraySize = deckSlots.Length;
            for (int i = 0; i < deckSlots.Length; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = deckSlots[i];

            so.FindProperty("detailNameText").objectReferenceValue = detailName;
            so.FindProperty("detailDescriptionText").objectReferenceValue = detailDesc;
            so.FindProperty("detailStatsText").objectReferenceValue = detailStats;
            so.FindProperty("detailIcon").objectReferenceValue = detailIcon;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.FindProperty("saveButton").objectReferenceValue = saveButton;
            so.FindProperty("clearButton").objectReferenceValue = clearButton;
            so.FindProperty("closeButton").objectReferenceValue = closeButton;
            so.FindProperty("warningText").objectReferenceValue = warningText;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panelRect.gameObject;
        }

        /// <summary>스킬 카드 프리팹. 후보 목록과 덱 슬롯 양쪽에서 쓴다.</summary>
        private static SkillCardView LoadOrCreateCardPrefab()
        {
            string path = $"{PrefabRoot}/UI/SkillCard.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<SkillCardView>(path);
            if (existing != null) return existing;

            EnsureFolder($"{PrefabRoot}/UI");

            var rect = UIBuilder.CreatePanel(null, "SkillCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(200f, 120f), new Color(0.14f, 0.15f, 0.2f, 0.95f));
            var card = rect.gameObject.AddComponent<SkillCardView>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();

            var icon = UIBuilder.CreateImage(rect, "Icon", Color.white, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(64f, 64f));
            icon.enabled = false;

            var nameText = UIBuilder.CreateText(rect, "Name", "스킬", 20f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(84f, -10f), new Vector2(104f, 60f));
            var costText = UIBuilder.CreateText(rect, "Cost", "1", 20f, TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-10f, 8f), new Vector2(60f, 30f));
            costText.color = UIBuilder.AccentColor;
            var slotLabel = UIBuilder.CreateText(rect, "SlotLabel", string.Empty, 22f, TextAlignmentOptions.TopRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -8f), new Vector2(50f, 30f));

            var selected = UIBuilder.CreateImage(rect, "Selected", new Color(1f, 0.85f, 0.4f, 0.25f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 120f));
            selected.gameObject.SetActive(false);
            var inDeck = UIBuilder.CreateImage(rect, "InDeck", new Color(0.3f, 0.8f, 0.5f, 0.9f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(12f, 12f));
            inDeck.gameObject.SetActive(false);
            var empty = UIBuilder.CreateText(rect, "EmptyState", "비어 있음", 20f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 40f));
            empty.color = new Color(1f, 1f, 1f, 0.35f);

            var so = new SerializedObject(card);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("costText").objectReferenceValue = costText;
            so.FindProperty("slotLabel").objectReferenceValue = slotLabel;
            so.FindProperty("button").objectReferenceValue = button;
            so.FindProperty("selectedMarker").objectReferenceValue = selected.gameObject;
            so.FindProperty("inDeckMarker").objectReferenceValue = inDeck.gameObject;
            so.FindProperty("emptyState").objectReferenceValue = empty.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, path);
            Object.DestroyImmediate(rect.gameObject);
            return prefab.GetComponent<SkillCardView>();
        }

        // ---------------------------------------------------------------- 튜토리얼

        private static string BuildTutorialScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            var ground = CreateGround("Ground", Vector3.zero, new Vector3(6f, 1f, 6f), new Color(0.32f, 0.36f, 0.28f));
            CreateCamera(new Vector3(0f, 4f, -8f), Quaternion.Euler(20f, 0f, 0f), addRig: true);
            CreateEventSystem();

            var networkManager = CreateNetworkStack(playerPrefab, autoStartHost: false);
            BuildHud();

            var marker = new GameObject("Marker_Exit");
            marker.transform.position = new Vector3(0f, 0f, 22f);

            var directorGo = new GameObject("TutorialDirector");
            var director = directorGo.AddComponent<TutorialDirector>();

            var sequence = AssetDatabase.LoadAssetAtPath<TutorialSequence>($"{DataRoot}/Tutorial/TUT_Basics.asset");
            var so = new SerializedObject(director);
            so.FindProperty("sequence").objectReferenceValue = sequence;
            so.FindProperty("autoStartHost").boolValue = true;

            var markers = so.FindProperty("markers");
            markers.arraySize = 1;
            markers.GetArrayElementAtIndex(0).objectReferenceValue = marker.transform;
            so.FindProperty("groundMask").intValue = 1 << LayerMask.NameToLayer(GameConstants.Layers.Ground);
            so.ApplyModifiedPropertiesWithoutUndo();

            // 튜토리얼 스텝이 소환할 적
            var goblin = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Enemies/Enemy_GoblinWarrior.prefab");
            var fightStep = AssetDatabase.LoadAssetAtPath<DefeatEnemiesStep>($"{DataRoot}/Tutorial/TS_08_Fight.asset");
            if (fightStep != null && goblin != null)
            {
                fightStep.EnemyPrefab = goblin;
                EditorUtility.SetDirty(fightStep);
            }

            BuildNavMesh(ground);
            CreateSpawnSystem(new[] { Vector3.zero });

            string path = $"{SceneFolder}/{GameConstants.Scenes.Tutorial}.unity";
            SaveSceneWithNetworkIds(scene, path);
            return path;
        }

        // ---------------------------------------------------------------- 던전

        private static string BuildDungeonScene(GameObject playerPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            var ground = CreateGround("DungeonFloor", new Vector3(0f, 0f, 30f), new Vector3(5f, 1f, 9f), new Color(0.26f, 0.25f, 0.28f));
            CreateCamera(new Vector3(0f, 4f, -8f), Quaternion.Euler(20f, 0f, 0f), addRig: true);
            CreateEventSystem();

            CreateNetworkStack(playerPrefab, autoStartHost: true);
            BuildHud();

            var spawnPositions = new[]
            {
                new Vector3(-2f, 0f, -2f), new Vector3(2f, 0f, -2f),
                new Vector3(-2f, 0f, -6f), new Vector3(2f, 0f, -6f)
            };
            CreateSpawnSystem(spawnPositions);

            var managerGo = new GameObject("DungeonManager");
            managerGo.AddComponent<NetworkObject>();
            var manager = managerGo.AddComponent<DungeonManager>();

            var melee = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Enemies/Enemy_GoblinWarrior.prefab");
            var archer = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Enemies/Enemy_GoblinArcher.prefab");
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabRoot}/Enemies/Enemy_OrcChieftain.prefab");

            var room1 = CreateRoom("Room_01_Entrance", new Vector3(0f, 0f, 14f), new Vector3(24f, 6f, 16f),
                new[] { (melee, 2), (archer, 1) });
            var room2 = CreateRoom("Room_02_Boss", new Vector3(0f, 0f, 44f), new Vector3(28f, 6f, 20f),
                new[] { (boss, 1) });

            var so = new SerializedObject(manager);
            var rooms = so.FindProperty("rooms");
            rooms.arraySize = 2;
            rooms.GetArrayElementAtIndex(0).objectReferenceValue = room1;
            rooms.GetArrayElementAtIndex(1).objectReferenceValue = room2;
            so.ApplyModifiedPropertiesWithoutUndo();

            BuildNavMesh(ground);

            string path = $"{SceneFolder}/{GameConstants.Scenes.Dungeon01}.unity";
            SaveSceneWithNetworkIds(scene, path);
            return path;
        }

        private static DungeonRoom CreateRoom(string name, Vector3 center, Vector3 size, (GameObject prefab, int count)[] enemies)
        {
            var go = new GameObject(name);
            go.transform.position = center;

            var trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size;

            go.AddComponent<NetworkObject>();
            var room = go.AddComponent<DungeonRoom>();

            // 스폰 지점
            var points = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject($"Spawn_{i}");
                point.transform.SetParent(go.transform, false);
                float angle = 90f * i;
                point.transform.localPosition = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * (size.z * 0.25f);
                points.Add(point.transform);
            }

            var checkpoint = new GameObject("Checkpoint");
            checkpoint.transform.SetParent(go.transform, false);
            checkpoint.transform.localPosition = new Vector3(0f, 0f, -size.z * 0.4f);

            var so = new SerializedObject(room);
            so.FindProperty("roomId").stringValue = name;
            so.FindProperty("startOnTriggerEnter").boolValue = true;
            so.FindProperty("checkpoint").objectReferenceValue = checkpoint.transform;

            var spawnPoints = so.FindProperty("spawnPoints");
            spawnPoints.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = points[i];

            // 웨이브 한 개에 지정된 적들을 모두 넣는다.
            var waves = so.FindProperty("waves");
            waves.arraySize = 1;
            var wave = waves.GetArrayElementAtIndex(0);
            wave.FindPropertyRelative("Name").stringValue = "웨이브 1";
            wave.FindPropertyRelative("DelaySeconds").floatValue = 1f;

            var entries = wave.FindPropertyRelative("Entries");
            int valid = 0;
            foreach (var e in enemies) if (e.prefab != null) valid++;
            entries.arraySize = valid;

            int index = 0;
            foreach (var e in enemies)
            {
                if (e.prefab == null) continue;
                var entry = entries.GetArrayElementAtIndex(index++);
                entry.FindPropertyRelative("EnemyPrefab").objectReferenceValue = e.prefab;
                entry.FindPropertyRelative("OverrideDefinition").objectReferenceValue = null;
                entry.FindPropertyRelative("Count").intValue = e.count;
                entry.FindPropertyRelative("SpawnPointIndices").arraySize = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return room;
        }

        // ---------------------------------------------------------------- 공통 조각

        private static GameObject CreateCamera(Vector3 position, Quaternion rotation, bool addRig = false, Color? clearColor = null)
        {
            var go = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            go.transform.SetPositionAndRotation(position, rotation);

            var camera = go.GetComponent<Camera>();
            if (clearColor.HasValue)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = clearColor.Value;
            }

            if (addRig) go.AddComponent<PlayerCameraRig>();
            return go;
        }

        private static void CreateLighting()
        {
            var go = new GameObject("DirectionalLight", typeof(Light));
            var light = go.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(45f, 35f, 0f);
        }

        private static GameObject CreateGround(string name, Vector3 center, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.position = center;
            go.transform.localScale = scale;
            go.layer = LayerMask.NameToLayer(GameConstants.Layers.Ground);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);

            var renderer = go.GetComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null) renderer.sharedMaterial = new Material(shader) { color = color };

            return go;
        }

        private static void BuildNavMesh(GameObject ground)
        {
            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();
        }

        private static void CreateEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.name = "EventSystem";
        }

        /// <summary>NetworkManager + UnityTransport + ConnectionManager 를 한 오브젝트에 얹는다.</summary>
        private static NetworkManager CreateNetworkStack(GameObject playerPrefab, bool autoStartHost)
        {
            var go = new GameObject("NetworkBootstrap");
            var networkManager = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            transport.SetConnectionData(GameConstants.DefaultAddress, GameConstants.DefaultPort, "0.0.0.0");
            go.AddComponent<ConnectionManager>();

            // 씬을 옮겨도 NetworkManager 는 살아남는다. 중복을 정리해 줄 가드가 필요하다.
            var guard = go.AddComponent<NetworkBootstrapGuard>();
            var guardSo = new SerializedObject(guard);
            guardSo.FindProperty("autoStartHostWhenAlone").boolValue = autoStartHost;
            guardSo.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(networkManager);
            so.FindProperty("NetworkConfig.NetworkTransport").objectReferenceValue = transport;
            if (playerPrefab != null)
            {
                so.FindProperty("NetworkConfig.PlayerPrefab").objectReferenceValue = playerPrefab;
                so.FindProperty("NetworkConfig.EnableSceneManagement").boolValue = true;
            }

            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>($"{ResourceRoot}/ActionRPGNetworkPrefabs.asset");
            if (prefabsList != null)
            {
                var lists = so.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
                lists.arraySize = 1;
                lists.GetArrayElementAtIndex(0).objectReferenceValue = prefabsList;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return networkManager;
        }

        private static void CreateSpawnSystem(Vector3[] positions)
        {
            var go = new GameObject("PlayerSpawnSystem");
            var system = go.AddComponent<PlayerSpawnSystem>();

            var points = new List<Transform>();
            for (int i = 0; i < positions.Length; i++)
            {
                var point = new GameObject($"SpawnPoint_{i}");
                point.transform.SetParent(go.transform, false);
                point.transform.position = positions[i];
                points.Add(point.transform);
            }

            var so = new SerializedObject(system);
            var array = so.FindProperty("spawnPoints");
            array.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = points[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>인게임 HUD 를 통째로 만든다.</summary>
        private static void BuildHud()
        {
            var canvas = UIBuilder.CreateCanvas("HUDCanvas");
            var hud = canvas.gameObject.AddComponent<HUDController>();
            canvas.gameObject.AddComponent<DamageNumberSpawner>();

            // 체력 / 마나
            var healthFill = UIBuilder.CreateBar(canvas.transform, "HealthBar", UIBuilder.HealthColor,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(420f, 28f), out _);
            var healthText = UIBuilder.CreateText(canvas.transform, "HealthText", "100 / 100", 20f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 120f), new Vector2(420f, 28f));

            var manaFill = UIBuilder.CreateBar(canvas.transform, "ManaBar", UIBuilder.ManaColor,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 88f), new Vector2(420f, 20f), out _);
            var manaText = UIBuilder.CreateText(canvas.transform, "ManaText", "100 / 100", 16f, TextAlignmentOptions.Center,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 88f), new Vector2(420f, 20f));

            // 스킬 슬롯
            var slotRow = UIBuilder.CreatePanel(canvas.transform, "SkillBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(460f, 110f));
            var layout = slotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            string[] keys = { "Q", "E", "R", "F" };
            var slots = new SkillSlotView[GameConstants.SkillSlotCount];
            for (int i = 0; i < slots.Length; i++) slots[i] = BuildSkillSlot(slotRow, keys[i]);

            // 목표 / 대사 / 상호작용
            var objectivePanel = UIBuilder.CreatePanel(canvas.transform, "ObjectivePanel", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-40f, -40f), new Vector2(460f, 110f), UIBuilder.PanelColor);
            UIBuilder.CreateText(objectivePanel, "Header", "목표", 20f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(200f, 26f)).color = UIBuilder.AccentColor;
            var objectiveText = UIBuilder.CreateText(objectivePanel, "Text", string.Empty, 24f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -42f), new Vector2(428f, 60f));

            var dialoguePanel = UIBuilder.CreatePanel(canvas.transform, "DialoguePanel", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 200f), new Vector2(1100f, 120f), UIBuilder.PanelColor);
            var dialogueText = UIBuilder.CreateText(dialoguePanel, "Text", string.Empty, 26f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 100f));
            dialoguePanel.gameObject.SetActive(false);

            var interactPrompt = UIBuilder.CreateText(canvas.transform, "InteractPrompt", string.Empty, 26f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(600f, 40f));
            interactPrompt.color = UIBuilder.AccentColor;
            interactPrompt.gameObject.SetActive(false);

            var deathOverlay = UIBuilder.CreatePanel(canvas.transform, "DeathOverlay", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1920f, 1080f), new Color(0.4f, 0f, 0f, 0.35f));
            UIBuilder.CreateText(deathOverlay, "Text", "쓰러졌다\n동료가 일으켜 주기를 기다린다", 44f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 200f));
            deathOverlay.gameObject.SetActive(false);

            // 파티 프레임
            var partyParent = UIBuilder.CreatePanel(canvas.transform, "PartyFrames", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(280f, 400f));
            var partyLayout = partyParent.gameObject.AddComponent<VerticalLayoutGroup>();
            partyLayout.spacing = 8f;
            partyLayout.childForceExpandHeight = false;
            partyLayout.childControlHeight = false;
            var partyFramePrefab = LoadOrCreatePartyFramePrefab();

            var so = new SerializedObject(hud);
            so.FindProperty("healthFill").objectReferenceValue = healthFill;
            so.FindProperty("healthText").objectReferenceValue = healthText;
            so.FindProperty("manaFill").objectReferenceValue = manaFill;
            so.FindProperty("manaText").objectReferenceValue = manaText;

            var slotsProp = so.FindProperty("skillSlots");
            slotsProp.arraySize = slots.Length;
            for (int i = 0; i < slots.Length; i++)
                slotsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

            so.FindProperty("objectiveText").objectReferenceValue = objectiveText;
            so.FindProperty("objectivePanel").objectReferenceValue = objectivePanel.gameObject;
            so.FindProperty("dialogueText").objectReferenceValue = dialogueText;
            so.FindProperty("dialoguePanel").objectReferenceValue = dialoguePanel.gameObject;
            so.FindProperty("partyFramePrefab").objectReferenceValue = partyFramePrefab;
            so.FindProperty("partyFrameParent").objectReferenceValue = partyParent;
            so.FindProperty("deathOverlay").objectReferenceValue = deathOverlay.gameObject;
            so.FindProperty("interactPrompt").objectReferenceValue = interactPrompt;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SkillSlotView BuildSkillSlot(Transform parent, string key)
        {
            var rect = UIBuilder.CreatePanel(parent, $"Slot_{key}", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(96f, 96f), new Color(0.12f, 0.13f, 0.17f, 0.95f));

            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 96f;
            layoutElement.preferredHeight = 96f;

            var view = rect.gameObject.AddComponent<SkillSlotView>();
            var group = rect.gameObject.AddComponent<CanvasGroup>();

            var icon = UIBuilder.CreateImage(rect, "Icon", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            icon.enabled = false;

            var overlay = UIBuilder.CreateImage(rect, "CooldownOverlay", new Color(0f, 0f, 0f, 0.65f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));
            overlay.type = Image.Type.Filled;
            overlay.fillMethod = Image.FillMethod.Radial360;
            overlay.fillOrigin = (int)Image.Origin360.Top;
            overlay.fillClockwise = false;
            overlay.fillAmount = 0f;

            var cooldownText = UIBuilder.CreateText(rect, "CooldownText", string.Empty, 28f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 40f));
            var keyLabel = UIBuilder.CreateText(rect, "KeyLabel", key, 18f, TextAlignmentOptions.BottomRight,
                new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-6f, 4f), new Vector2(30f, 24f));
            keyLabel.color = UIBuilder.AccentColor;

            var empty = UIBuilder.CreateImage(rect, "EmptyState", new Color(1f, 1f, 1f, 0.06f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f));

            var so = new SerializedObject(view);
            so.FindProperty("iconImage").objectReferenceValue = icon;
            so.FindProperty("cooldownOverlay").objectReferenceValue = overlay;
            so.FindProperty("cooldownText").objectReferenceValue = cooldownText;
            so.FindProperty("keyLabel").objectReferenceValue = keyLabel;
            so.FindProperty("emptyState").objectReferenceValue = empty.gameObject;
            so.FindProperty("canvasGroup").objectReferenceValue = group;
            so.ApplyModifiedPropertiesWithoutUndo();

            return view;
        }

        private static PartyFrameView LoadOrCreatePartyFramePrefab()
        {
            string path = $"{PrefabRoot}/UI/PartyFrame.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<PartyFrameView>(path);
            if (existing != null) return existing;

            EnsureFolder($"{PrefabRoot}/UI");

            var rect = UIBuilder.CreatePanel(null, "PartyFrame", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(260f, 56f), new Color(0.1f, 0.11f, 0.15f, 0.9f));
            var view = rect.gameObject.AddComponent<PartyFrameView>();

            var layoutElement = rect.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 56f;

            var nameText = UIBuilder.CreateText(rect, "Name", "동료", 20f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -6f), new Vector2(200f, 24f));
            var fill = UIBuilder.CreateBar(rect, "HealthBar", UIBuilder.HealthColor,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 8f), new Vector2(236f, 16f), out _);
            var dead = UIBuilder.CreateText(rect, "DeadMarker", "쓰러짐", 18f, TextAlignmentOptions.TopRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f, -6f), new Vector2(80f, 24f));
            dead.color = new Color(1f, 0.4f, 0.4f);
            dead.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("nameText").objectReferenceValue = nameText;
            so.FindProperty("healthFill").objectReferenceValue = fill;
            so.FindProperty("deadMarker").objectReferenceValue = dead.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, path);
            Object.DestroyImmediate(rect.gameObject);
            return prefab.GetComponent<PartyFrameView>();
        }

        // ---------------------------------------------------------------- 유틸

        /// <summary>
        /// 저장 전 씬의 NetworkObject 는 GlobalObjectId 가 null 이라 GlobalObjectIdHash 가 0 으로 남는다.
        /// 한 번 저장해 씬 GUID 를 만든 뒤 다시 열어 OnValidate 로 해시를 생성하고, 그 결과를 다시 저장한다.
        /// </summary>
        private static void SaveSceneWithNetworkIds(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.SaveScene(scene, path);

            var reopened = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            EditorSceneManager.MarkSceneDirty(reopened);
            EditorSceneManager.SaveScene(reopened);

            var seen = new HashSet<uint>();
            foreach (var root in reopened.GetRootGameObjects())
            {
                foreach (var networkObject in root.GetComponentsInChildren<NetworkObject>(true))
                {
                    uint hash = new SerializedObject(networkObject).FindProperty("GlobalObjectIdHash").uintValue;
                    if (hash == 0 || !seen.Add(hash))
                        Debug.LogError($"[ActionRPG] '{path}' 의 {networkObject.name} GlobalObjectIdHash 가 잘못되었습니다 ({hash}).", networkObject);
                }
            }
        }

        private static void RegisterBuildScenes(params string[] paths)
        {
            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var path in paths)
            {
                if (existing.Exists(s => s.path == path)) continue;
                existing.Add(new EditorBuildSettingsScene(path, true));
            }
            EditorBuildSettings.scenes = existing.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
