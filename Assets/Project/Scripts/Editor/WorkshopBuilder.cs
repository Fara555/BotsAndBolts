using System;
using System.IO;
using BotsBolts.Players;
using BotsBolts.Session;
using BotsBolts.Presentation;
using FishNet.Component.Transforming;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using FishNet.Transporting.Tugboat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BotsBolts.Editor
{
    public static class WorkshopBuilder
    {
        public const string ScenePath = "Assets/Project/Scenes/Workshop.unity";
        private const string Root = "Assets/Project";

        [MenuItem("Bots & Bolts/Setup/Create Workshop")]
        public static void CreateWorkshop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before creating the workshop.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("[BotsBolts] Workshop already exists. Open it via Bots & Bolts/Setup/Open Workshop.");
                return;
            }

            Directory.CreateDirectory(Root + "/Prefabs");
            Directory.CreateDirectory(Root + "/Art/Materials");
            Directory.CreateDirectory(Root + "/Configs");
            AssetDatabase.Refresh();

            WorkshopSettings settings = AssetDatabase.LoadAssetAtPath<WorkshopSettings>(Root + "/Configs/WorkshopSettings.asset");
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<WorkshopSettings>();
                AssetDatabase.CreateAsset(settings, Root + "/Configs/WorkshopSettings.asset");
            }
            Material floor = MaterialAsset("Floor", new Color(0.18f, 0.27f, 0.3f));
            Material walls = MaterialAsset("Walls", new Color(0.08f, 0.14f, 0.19f));
            Material workbench = MaterialAsset("Workbench", new Color(0.3f, 0.43f, 0.46f));
            Material playerMaterial = MaterialAsset("Player", Color.white);
            Material markerMaterial = MaterialAsset("LocalMarker", new Color(0.25f, 1, 0.55f));
            NetworkObject playerPrefab = CreatePlayer(settings, playerMaterial, markerMaterial);
            var prefabs = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>(Root + "/Configs/WorkshopPrefabs.asset");
            if (prefabs == null)
            {
                prefabs = ScriptableObject.CreateInstance<SinglePrefabObjects>();
                AssetDatabase.CreateAsset(prefabs, Root + "/Configs/WorkshopPrefabs.asset");
            }
            prefabs.Clear();
            prefabs.AddObject(playerPrefab, true, false);
            EditorUtility.SetDirty(prefabs);

            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(Root + "/Scenes/Gameplay.unity");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                CreateRoom(floor, walls, workbench);
                Camera workshopCamera = CreateCameraAndLight();
                Transform[] spawns = { SpawnPoint("Player 1 Spawn", new Vector3(-1.5f, 0.05f, -1)),
                    SpawnPoint("Player 2 Spawn", new Vector3(1.5f, 0.05f, -1)) };

                var networkRoot = new GameObject("Workshop Network");
                networkRoot.AddComponent<Tugboat>();
                var manager = networkRoot.AddComponent<NetworkManager>();
                manager.SpawnablePrefabs = prefabs;
                SetBool(manager, "_dontDestroyOnLoad", false);
                SetBool(manager, "_runInBackground", true);
                ConfigureFollowCamera(workshopCamera, manager, settings);

                SessionPanel panel = CreatePanel();
                var scope = new GameObject("Workshop Lifetime").AddComponent<WorkshopLifetimeScope>();
                SetObject(scope, "networkManager", manager);
                SetObject(scope, "settings", settings);
                SetObject(scope, "playerPrefab", playerPrefab);
                SetObject(scope, "panel", panel);
                new GameObject("Development Network Checks").AddComponent<Development.NetworkSmokeProbe>();
                var serializedScope = new SerializedObject(scope);
                SerializedProperty spawnArray = serializedScope.FindProperty("spawnPoints");
                spawnArray.arraySize = 2;
                for (int i = 0; i < 2; i++) spawnArray.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
                serializedScope.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BotsBolts] Created Workshop scene, player prefab, settings and prefab collection.");
        }

        private static NetworkObject CreatePlayer(WorkshopSettings settings, Material material, Material markerMaterial)
        {
            string path = Root + "/Prefabs/WorkshopPlayer.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<NetworkObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("WorkshopPlayer");
            try
            {
                var controller = root.AddComponent<CharacterController>();
                controller.height = 1.4f;
                controller.radius = 0.35f;
                controller.center = new Vector3(0, 0.7f, 0);
                controller.stepOffset = 0.2f;
                controller.enabled = false;
                var input = root.AddComponent<PlayerInputReader>();
                SetObject(input, "actions", AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"));
                var motor = root.AddComponent<PlayerMotor>();
                SetObject(motor, "settings", settings);
                SetObject(motor, "input", input);
                var body = Primitive("Body", PrimitiveType.Capsule, new Vector3(0, 0.7f, 0), new Vector3(0.7f, 0.7f, 0.7f), material, root.transform, false);
                Primitive("Face", PrimitiveType.Cube, new Vector3(0, 1.15f, 0.32f), new Vector3(0.4f, 0.16f, 0.08f), markerMaterial, root.transform, false);
                var marker = Primitive("Local player marker", PrimitiveType.Cylinder, new Vector3(0, 0.035f, 0), new Vector3(0.95f, 0.015f, 0.95f), markerMaterial, root.transform, false);
                marker.SetActive(false);
                root.AddComponent<NetworkObject>();
                var networkTransform = root.AddComponent<NetworkTransform>();
                SetBool(networkTransform, "_clientAuthoritative", true);
                SetBool(networkTransform, "_synchronizeScale", false);
                SetInt(networkTransform, "_componentConfiguration", 1);
                SetInt(networkTransform, "_interval", 1);
                var player = root.AddComponent<NetworkPlayer>();
                SetObject(player, "motor", motor);
                SetObject(player, "body", body.GetComponent<Renderer>());
                SetObject(player, "localMarker", marker);
                ConfigureMotionPresentation(root);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
                return prefab.GetComponent<NetworkObject>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void CreateRoom(Material floor, Material walls, Material bench)
        {
            var room = new GameObject("Workshop Layout");
            Primitive("Floor", PrimitiveType.Cube, new Vector3(0, -0.15f, 0), new Vector3(14, 0.3f, 10), floor, room.transform);
            Primitive("Back wall", PrimitiveType.Cube, new Vector3(0, 0.6f, 5), new Vector3(14.4f, 1.2f, 0.3f), walls, room.transform);
            Primitive("Front rail", PrimitiveType.Cube, new Vector3(0, 0.15f, -5), new Vector3(14.4f, 0.3f, 0.3f), walls, room.transform);
            Primitive("Left wall", PrimitiveType.Cube, new Vector3(-7, 0.6f, 0), new Vector3(0.3f, 1.2f, 10), walls, room.transform);
            Primitive("Right wall", PrimitiveType.Cube, new Vector3(7, 0.6f, 0), new Vector3(0.3f, 1.2f, 10), walls, room.transform);
            Primitive("Workbench placeholder", PrimitiveType.Cube, new Vector3(-3.8f, 0.5f, 2.8f), new Vector3(3, 1, 1.2f), bench, room.transform);
            Primitive("Station placeholder", PrimitiveType.Cube, new Vector3(3.8f, 0.5f, 2.8f), new Vector3(2, 1, 1.2f), bench, room.transform);
        }

        private static Camera CreateCameraAndLight()
        {
            var camera = new GameObject("Workshop Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 14, -11);
            camera.transform.LookAt(new Vector3(0, 0, 0.5f));
            camera.orthographic = true;
            camera.orthographicSize = 8;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.06f, 0.1f);
            camera.gameObject.AddComponent<AudioListener>();
            var light = new GameObject("Workshop Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.intensity = 1.7f;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.55f, 0.65f);
            return camera;
        }

        private static void ConfigureMotionPresentation(GameObject root)
        {
            Transform visuals = root.transform.Find("Visuals");
            if (visuals == null)
            {
                visuals = new GameObject("Visuals").transform;
                visuals.SetParent(root.transform, false);
                visuals.localPosition = Vector3.up * 0.7f;
                root.transform.Find("Body").SetParent(visuals, true);
                root.transform.Find("Face").SetParent(visuals, true);
            }
            var presentation = root.GetComponent<PlayerMotionPresentation>();
            if (presentation == null) presentation = root.AddComponent<PlayerMotionPresentation>();
            SetObject(presentation, "visualRoot", visuals);
            SetObject(presentation, "groundMarker", root.transform.Find("Local player marker"));
            SetObject(root.GetComponent<NetworkPlayer>(), "presentation", presentation);
        }

        private static void ConfigureFollowCamera(Camera camera, NetworkManager manager, WorkshopSettings settings)
        {
            var follow = camera.GetComponent<WorkshopFollowCamera>();
            if (follow == null) follow = camera.gameObject.AddComponent<WorkshopFollowCamera>();
            SetObject(follow, "settings", settings);
            var coordinator = manager.GetComponent<WorkshopCameraCoordinator>();
            if (coordinator == null) coordinator = manager.gameObject.AddComponent<WorkshopCameraCoordinator>();
            SetObject(coordinator, "followCamera", follow);
        }

        [MenuItem("Bots & Bolts/Setup/Upgrade Movement and Camera")]
        public static void UpgradeMovementAndCamera()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before upgrading movement.");
            CreateWorkshop();
            string prefabPath = Root + "/Prefabs/WorkshopPlayer.prefab";
            GameObject playerRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                ConfigureMotionPresentation(playerRoot);
                PrefabUtility.SaveAsPrefabAsset(playerRoot, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(playerRoot); }

            if (Application.isBatchMode && string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene(Root + "/Scenes/Gameplay.unity");
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (wasLoaded && scene.isDirty)
                throw new InvalidOperationException("Save the Workshop scene before applying the upgrade.");
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Camera camera = null;
                NetworkManager manager = null;
                Transform room = null;
                SessionPanel sessionPanel = null;
                EventSystem uiEvents = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out Camera foundCamera)) camera = foundCamera;
                    if (root.TryGetComponent(out NetworkManager foundManager)) manager = foundManager;
                    if (root.TryGetComponent(out SessionPanel foundPanel)) sessionPanel = foundPanel;
                    if (root.TryGetComponent(out EventSystem foundEvents)) uiEvents = foundEvents;
                    if (root.name == "Workshop Layout") room = root.transform;
                    foreach (Text text in root.GetComponentsInChildren<Text>(true))
                        if (text.text.StartsWith("Move:")) text.text = "Move: WASD / arrows / left stick\nForward leap: Space / gamepad A";
                }
                if (camera == null || manager == null || room == null)
                    throw new InvalidOperationException("Workshop camera, network root or layout is missing.");
                var settings = AssetDatabase.LoadAssetAtPath<WorkshopSettings>(Root + "/Configs/WorkshopSettings.asset");
                ConfigureFollowCamera(camera, manager, settings);
                if (sessionPanel != null && uiEvents != null) SetObject(sessionPanel, "eventSystem", uiEvents);
                // The low front rail remains visually open, but a leap must not leave the test floor.
                if (room.Find("Front leap boundary") == null)
                {
                    var boundary = new GameObject("Front leap boundary");
                    boundary.transform.SetParent(room, false);
                    boundary.transform.localPosition = new Vector3(0, 1.25f, -5);
                    boundary.AddComponent<BoxCollider>().size = new Vector3(14.4f, 2.5f, 0.3f);
                }
                EditorUtility.SetDirty(settings);
                EditorSceneManager.SaveScene(scene);
            }
            finally { if (!wasLoaded) EditorSceneManager.CloseScene(scene, true); }
            AssetDatabase.SaveAssets();
            Debug.Log("[BotsBolts] Upgraded player presentation, local follow camera and leap boundary.");
        }

        private static SessionPanel CreatePanel()
        {
            var canvas = new GameObject("Session UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var eventSystem = new GameObject("Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            GameObject card = UiObject("Connection Panel", canvas.transform, new Vector2(20, -20), new Vector2(390, 300));
            card.AddComponent<Image>().color = new Color(0.04f, 0.08f, 0.12f, 0.95f);
            Label("BOTS & BOLTS", card.transform, new Vector2(20, -14), new Vector2(350, 40), 28);
            Label("Workshop prototype · 1–2 players", card.transform, new Vector2(20, -57), new Vector2(350, 30), 17);
            InputField address = CreateAddressField(card.transform);
            Button host = Button("Host", card.transform, new Vector2(20, -143), new Vector2(108, 42));
            Button join = Button("Connect", card.transform, new Vector2(140, -143), new Vector2(108, 42));
            Button leave = Button("Leave", card.transform, new Vector2(260, -143), new Vector2(108, 42));
            Text status = Label("", card.transform, new Vector2(20, -198), new Vector2(350, 80), 17);
            Label("Move: WASD / arrows / left stick\nGreen ring = your player", canvas.transform, new Vector2(20, -640), new Vector2(450, 60), 19);
            var panel = canvas.AddComponent<SessionPanel>();
            SetObject(panel, "hostAddress", address);
            SetObject(panel, "hostButton", host);
            SetObject(panel, "joinButton", join);
            SetObject(panel, "leaveButton", leave);
            SetObject(panel, "status", status);
            SetObject(panel, "eventSystem", eventSystem.GetComponent<EventSystem>());
            return panel;
        }

        private static InputField CreateAddressField(Transform parent)
        {
            GameObject root = UiObject("Host address", parent, new Vector2(20, -96), new Vector2(348, 36));
            root.AddComponent<Image>().color = new Color(0.14f, 0.22f, 0.28f);
            Text text = Label("127.0.0.1", root.transform, new Vector2(10, -2), new Vector2(326, 32), 20);
            var field = root.AddComponent<InputField>();
            field.textComponent = text;
            field.text = "127.0.0.1";
            field.lineType = InputField.LineType.SingleLine;
            field.characterLimit = 253;
            return field;
        }

        private static Button Button(string title, Transform parent, Vector2 position, Vector2 size)
        {
            GameObject root = UiObject(title, parent, position, size);
            root.AddComponent<Image>().color = new Color(0.14f, 0.4f, 0.5f);
            var button = root.AddComponent<Button>();
            Text text = Label(title, root.transform, Vector2.zero, size, 20);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Text Label(string content, Transform parent, Vector2 position, Vector2 size, int fontSize)
        {
            var text = UiObject(content.Length == 0 ? "Status" : content, parent, position, size).AddComponent<Text>();
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = new Color(0.9f, 0.97f, 1);
            text.raycastTarget = false;
            return text;
        }

        private static GameObject UiObject(string title, Transform parent, Vector2 position, Vector2 size)
        {
            var root = new GameObject(title, typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return root;
        }

        private static Transform SpawnPoint(string title, Vector3 position)
        {
            var point = new GameObject(title).transform;
            point.position = position;
            return point;
        }

        private static GameObject Primitive(string title, PrimitiveType type, Vector3 position, Vector3 scale,
            Material material, Transform parent, bool collision = true)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            result.name = title;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(result.GetComponent<Collider>());
            return result;
        }

        private static Material MaterialAsset(string title, Color color)
        {
            string path = Root + "/Art/Materials/" + title + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is unavailable.");
            var material = new Material(shader) { name = title };
            material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetObject(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(UnityEngine.Object target, string property, bool value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(UnityEngine.Object target, string property, int value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("Bots & Bolts/Setup/Open Workshop")]
        public static void OpenWorkshop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Bots & Bolts/Build/Windows Test Build")]
        public static void BuildWindows()
        {
            UpgradeMovementAndCamera();
            Directory.CreateDirectory("Builds/Workshop");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Workshop/BotsBolts.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Workshop build failed: " + report.summary.result);
            Debug.Log("[BotsBolts] Windows test build succeeded.");
        }
    }
}
