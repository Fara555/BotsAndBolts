using System;
using System.Linq;
using BotsBolts.Players;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BotsBolts.Editor
{
    public static class RobotModelBuilder
    {
        private const string Folder = "Assets/Project/Art/Characters/Robot/";

        [MenuItem("Bots & Bolts/Setup/Apply Cartoon Robot")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before updating the robot prefab.");
            string modelPath = Folder + "RobotAnimated.fbx";
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importCameras = importer.importLights = false;
            importer.addCollider = false;
            importer.globalScale = 1;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (ModelImporterClipAnimation clip in clips)
            {
                foreach (string name in new[] { "Idle", "Run", "Leap", "Fall", "Land" })
                    if (clip.name.EndsWith(name, StringComparison.Ordinal)) clip.name = name;
                clip.loopTime = clip.name == "Idle" || clip.name == "Run";
                clip.lockRootRotation = clip.lockRootHeightY = clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = clip.keepOriginalPositionY = clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) throw new InvalidOperationException("Robot model was not imported.");
            string path = "Assets/Project/Prefabs/WorkshopPlayer.prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform visuals = player.transform.Find("Visuals");
                if (visuals == null) throw new InvalidOperationException("Upgrade movement presentation first.");
                // Replace only the visual children; the controller, network identity and marker remain intact.
                foreach (Transform child in visuals.Cast<Transform>().ToArray())
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                GameObject robot = (GameObject)PrefabUtility.InstantiatePrefab(model, visuals);
                robot.name = "Cartoon Robot";
                robot.transform.localPosition = Vector3.down * .7f;
                // Retain the FBX root rotation that converts Blender Z-up into Unity Y-up.
                robot.transform.localScale = Vector3.one;
                Animator animator = robot.GetComponent<Animator>();
                if (animator == null) animator = robot.AddComponent<Animator>();
                animator.runtimeAnimatorController = CreateController(modelPath);
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var presentation = new SerializedObject(player.GetComponent<BotsBolts.Presentation.PlayerMotionPresentation>());
                presentation.FindProperty("animator").objectReferenceValue = animator;
                Transform[] bones = robot.GetComponentsInChildren<Transform>();
                foreach (var pair in new[] { ("head", "Head"), ("leftEye", "Eye_Left"), ("rightEye", "Eye_Right") })
                    presentation.FindProperty(pair.Item1).objectReferenceValue = bones.First(t => t.name == pair.Item2);
                presentation.FindProperty("feedback").objectReferenceValue = ConfigureFeedback(player);
                presentation.ApplyModifiedPropertiesWithoutUndo();
                Renderer[] renderers = robot.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    string finish = renderer.gameObject.name;
                    Color color = finish == "RobotShell" ? Color.white :
                        finish == "RobotTrim" ? new Color(.065f,.105f,.135f) :
                        finish == "RobotVisor" ? new Color(.006f,.022f,.032f) :
                        finish == "RobotEyes" ? new Color(.68f,.97f,1) : new Color(.79f,.85f,.84f);
                    string materialPath = Folder + finish + ".mat";
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = finish };
                        AssetDatabase.CreateAsset(material, materialPath);
                    }
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Smoothness", finish == "RobotVisor" ? .78f : .55f);
                    material.SetFloat("_Metallic", finish == "RobotEyes" ? 0 : .12f);
                    if (finish == "RobotEyes")
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", color * 1.5f);
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    }
                    renderer.sharedMaterial = material;
                    EditorUtility.SetDirty(material);
                }
                Renderer[] shells = renderers.Where(r => r.gameObject.name == "RobotShell").ToArray();
                if (shells.Length != 1 || renderers.Length != 5)
                    throw new InvalidOperationException("Robot export must contain five finish meshes and one color shell.");
                Bounds bounds = renderers[0].bounds;
                foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if (bounds.size.y < 1.3f || bounds.size.y > 1.5f || bounds.min.y < -.05f)
                    throw new InvalidOperationException("Robot import scale or floor origin is incorrect: " + bounds);
                var serialized = new SerializedObject(player.GetComponent<NetworkPlayer>());
                SerializedProperty array = serialized.FindProperty("colorShells");
                array.arraySize = shells.Length;
                for (int i = 0; i < shells.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = shells[i];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ConfigureAntenna(player, bones.First(t => t.name == "Head"));
                CarryPoseSetup.Configure(player);
                PrefabUtility.SaveAsPrefabAsset(player, path);
                Debug.Log("[BotsBolts] Cartoon robot integrated; five finishes, shared mesh, per-player shell color. Bounds: " + bounds);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }

        private static AnimatorController CreateController(string modelPath)
        {
            string path = Folder + "Robot.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            // Preserve the asset GUID when regenerating this dedicated controller.
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset != controller) UnityEngine.Object.DestroyImmediate(asset, true);
            controller.parameters = Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "CycleRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1
            });
            var machine = new AnimatorStateMachine { name = "Robot Motion" };
            AssetDatabase.AddObjectToAsset(machine, controller);
            controller.layers = new[] { new AnimatorControllerLayer { name = "Robot Motion", defaultWeight = 1, stateMachine = machine } };
            AnimationClip Find(string name) => AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
                .FirstOrDefault(c => c.name == name) ?? throw new InvalidOperationException("Robot animation missing: " + name);
            AnimatorState locomotion = machine.AddState("Locomotion");
            var tree = new BlendTree { name = "Idle to Run", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Find("Idle"), 0); tree.AddChild(Find("Run"), .2f);
            locomotion.motion = tree; machine.defaultState = locomotion;
            locomotion.speedParameter = "CycleRate"; locomotion.speedParameterActive = true;
            foreach (string name in new[] { "Leap", "Fall", "Land" }) machine.AddState(name).motion = Find(name);
            AnimatorState land = machine.states.First(s => s.state.name == "Land").state;
            AnimatorStateTransition exit = land.AddTransition(locomotion);
            exit.hasExitTime = true; exit.exitTime = .9f; exit.duration = .12f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void ConfigureAntenna(GameObject player, Transform head)
        {
            string path = Folder + "Antenna.fbx";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.importAnimation = false; importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
            var socket = new GameObject("Antenna Physics");
            socket.transform.position = head.position + player.transform.up * .545f;
            socket.transform.rotation = player.transform.rotation;
            socket.transform.SetParent(head, true);
            var pivot = new GameObject("Antenna Rod").transform;
            pivot.SetParent(socket.transform, false);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), pivot);
            model.transform.localPosition = Vector3.zero;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Folder + (renderer.name == "AntennaTip" ? "RobotIvory.mat" : "RobotTrim.mat"));
            var simulation = socket.AddComponent<BotsBolts.Presentation.RobotAntenna>();
            var serialized = new SerializedObject(simulation);
            serialized.FindProperty("rod").objectReferenceValue = pivot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static BotsBolts.Presentation.RobotFeedback ConfigureFeedback(GameObject player)
        {
            var feedback = player.GetComponent<BotsBolts.Presentation.RobotFeedback>();
            if (feedback == null) feedback = player.AddComponent<BotsBolts.Presentation.RobotFeedback>();
            AudioSource audio = player.GetComponent<AudioSource>();
            if (audio == null) audio = player.AddComponent<AudioSource>();
            audio.playOnAwake = false; audio.loop = false; audio.spatialBlend = .85f;
            audio.volume = .35f; audio.dopplerLevel = 0;
            audio.minDistance = 2; audio.maxDistance = 15;
            Transform existing = player.transform.Find("Robot Dust");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var dustObject = new GameObject("Robot Dust");
            dustObject.transform.SetParent(player.transform, false);
            ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
            var main = dust.main;
            main.playOnAwake = false; main.loop = false; main.duration = .6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.3f, .65f);
            main.startSize = .12f; main.maxParticles = 32;
            main.startColor = new Color(.72f,.83f,.88f,.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = .04f;
            var emission = dust.emission; emission.enabled = false;
            var shape = dust.shape; shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 75; shape.radius = .16f; shape.rotation = new Vector3(-90,0,0);
            var color = dust.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.1f), new GradientAlphaKey(0,1) });
            color.color = gradient;
            var size = dust.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0,.6f,1,1.8f));
            string texturePath = Folder + "Feedback/Puff.png";
            var textureImporter = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            textureImporter.alphaIsTransparency = true; textureImporter.mipmapEnabled = false;
            textureImporter.SaveAndReimport();
            string materialPath = Folder + "Feedback/Dust.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "Robot Dust" };
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Surface",1); material.SetFloat("_Blend",0); material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",5); material.SetFloat("_DstBlend",10);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material);
            dust.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("audioSource").objectReferenceValue = audio;
            serialized.FindProperty("dust").objectReferenceValue = dust;
            foreach (var pair in new[] { ("stepClip","Step"), ("leapClip","Leap"), ("landClip","Land") })
                serialized.FindProperty(pair.Item1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "Feedback/" + pair.Item2 + ".wav");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return feedback;
        }
    }
}
