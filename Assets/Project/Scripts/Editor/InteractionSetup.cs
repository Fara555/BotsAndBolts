using System;
using BotsBolts.Interactions;
using BotsBolts.Players;
using BotsBolts.Session;
using FishNet.Managing;
using FishNet.Managing.Object;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BotsBolts.Editor
{
    // Explicit, repeatable authoring upgrade. Ordinary builds never regenerate content.
    public static class InteractionSetup
    {
        private const string BatteryPath = "Assets/Project/Prefabs/WorkshopBattery.prefab";

        [MenuItem("Bots & Bolts/Setup/Apply Battery Interactions")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            var loaded = EditorSceneManager.GetSceneByPath(WorkshopBuilder.ScenePath);
            if (loaded.isLoaded && loaded.isDirty) throw new InvalidOperationException("Save Workshop before applying interactions.");
            var settings = AssetDatabase.LoadAssetAtPath<WorkshopSettings>("Assets/Project/Configs/WorkshopSettings.asset");
            Material casing = Material("BatteryCasing", new Color(.10f, .25f, .32f));
            Material charge = Material("BatteryPanel", new Color(.3f, .85f, .55f));
            Material terminal = Material("BatteryTerminal", new Color(1f, .75f, .2f));
            Material marker = Material("InteractionMarker", new Color(1, .85f, .2f));
            NetworkObject battery = CreateBattery(casing, charge, terminal);
            UpgradePlayer(marker);
            var collection = AssetDatabase.LoadAssetAtPath<SinglePrefabObjects>("Assets/Project/Configs/WorkshopPrefabs.asset");
            collection.AddObject(battery, true);
            EditorUtility.SetDirty(collection);
            var previous = EditorSceneManager.GetActiveScene();
            var scene = loaded.isLoaded ? loaded : EditorSceneManager.OpenScene(WorkshopBuilder.ScenePath, OpenSceneMode.Additive);
            try
            {
                NetworkManager network = Find<NetworkManager>(scene);
                var workshop = network.GetComponent<WorkshopInteractions>();
                if (workshop == null) workshop = Undo.AddComponent<WorkshopInteractions>(network.gameObject);
                BatterySource source = Find<BatterySource>(scene);
                if (source == null)
                {
                    GameObject root = new GameObject("Battery Supply");
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                    Undo.RegisterCreatedObjectUndo(root, "Create battery supply");
                    root.transform.position = new Vector3(-3.8f, 1.04f, 2.8f);
                    source = root.AddComponent<BatterySource>();
                    Part(root.transform, "Tray", PrimitiveType.Cube, new Vector3(0, .07f, 0), new Vector3(1.3f, .14f, .8f), casing, true);
                    Part(root.transform, "Battery display", PrimitiveType.Cube, new Vector3(0, .35f, 0), new Vector3(.5f, .42f, .32f), charge, false);
                    var point = new GameObject("Interaction Point").transform;
                    point.SetParent(root.transform, false);
                    point.localPosition = new Vector3(0, .2f, -.42f);
                    Set(source, "interactionPoint", point);
                }
                GameObject floor = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var transform in root.GetComponentsInChildren<Transform>())
                        if (transform.name == "Floor") floor = transform.gameObject;
                if (floor == null) throw new InvalidOperationException("Workshop floor is missing.");
                Set(workshop, "settings", settings);
                Set(workshop, "source", source);
                Set(workshop, "batteryPrefab", battery);
                Set(workshop, "floor", floor.GetComponent<Collider>());
                Undo.RecordObject(workshop, "Configure interactions");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            finally
            {
                if (!loaded.isLoaded) EditorSceneManager.CloseScene(scene, true);
                if (previous.isLoaded) EditorSceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[BotsBolts] Battery interaction assets integrated.");
        }

        private static NetworkObject CreateBattery(Material casing, Material panel, Material terminal)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(BatteryPath);
            if (existing != null) return existing.GetComponent<NetworkObject>();
            var root = new GameObject("WorkshopBattery");
            try
            {
                root.AddComponent<NetworkObject>();
                var battery = root.AddComponent<NetworkBattery>();
                var collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, .25f, 0);
                collider.size = new Vector3(.5f, .5f, .34f);
                Part(root.transform, "Casing", PrimitiveType.Cube, new Vector3(0, .22f, 0), new Vector3(.48f, .44f, .32f), casing, false);
                Part(root.transform, "Charge panel", PrimitiveType.Cube, new Vector3(0, .24f, -.165f), new Vector3(.3f, .24f, .025f), panel, false);
                Part(root.transform, "Positive terminal", PrimitiveType.Cylinder, new Vector3(-.12f, .47f, 0), new Vector3(.12f, .04f, .12f), terminal, false);
                Part(root.transform, "Negative terminal", PrimitiveType.Cylinder, new Vector3(.12f, .47f, 0), new Vector3(.12f, .04f, .12f), terminal, false);
                Set(battery, "pickupCollider", collider);
                return PrefabUtility.SaveAsPrefabAsset(root, BatteryPath).GetComponent<NetworkObject>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void UpgradePlayer(Material markerMaterial)
        {
            const string path = "Assets/Project/Prefabs/WorkshopPlayer.prefab";
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.GetComponent<PlayerInteraction>() != null)
                {
                    CarryPoseSetup.Configure(root);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    return;
                }
                var interaction = root.AddComponent<PlayerInteraction>();
                var carry = new GameObject("Carry Point").transform;
                carry.SetParent(root.transform, false);
                carry.localPosition = new Vector3(0, .85f, 1f);
                GameObject canvasRoot = new GameObject("Interaction HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
                canvasRoot.transform.SetParent(root.transform, false);
                canvasRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvasRoot.GetComponent<Canvas>().sortingOrder = 20;
                var scaler = canvasRoot.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720);
                GameObject box = new GameObject("Prompt", typeof(RectTransform), typeof(Image));
                box.transform.SetParent(canvasRoot.transform, false);
                var rect = (RectTransform)box.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(0, 25);
                rect.sizeDelta = new Vector2(540, 54);
                box.GetComponent<Image>().color = new Color(.05f, .1f, .15f, .92f);
                box.GetComponent<Image>().raycastTarget = false;
                GameObject label = new GameObject("Action", typeof(RectTransform), typeof(Text));
                label.transform.SetParent(box.transform, false);
                var textRect = (RectTransform)label.transform;
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                Text text = label.GetComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
                text.raycastTarget = false;
                canvasRoot.SetActive(false);
                var marker = Part(root.transform, "Interaction Target", PrimitiveType.Sphere, Vector3.zero,
                    new Vector3(.18f, .18f, .18f), markerMaterial, false);
                marker.SetActive(false);
                Set(interaction, "input", root.GetComponent<PlayerInputReader>());
                Set(interaction, "carryPoint", carry);
                Set(interaction, "hintCanvas", canvasRoot);
                Set(interaction, "hint", text);
                Set(interaction, "targetMarker", marker.transform);
                CarryPoseSetup.Configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Material Material(string name, Color color)
        {
            const string folder = "Assets/Project/Art/Items";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Project/Art", "Items");
            string path = folder + "/" + name + ".mat";
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result != null) return result;
            result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Smoothness", .25f);
            AssetDatabase.CreateAsset(result, path);
            return result;
        }

        private static GameObject Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name; part.transform.SetParent(parent, false);
            part.transform.localPosition = position; part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            return part;
        }

        private static T Find<T>(UnityEngine.SceneManagement.Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                T component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }

        private static void Set(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            Undo.RecordObject(target, "Configure battery interactions");
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
        }
    }
}
