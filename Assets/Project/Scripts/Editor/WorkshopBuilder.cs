using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BotsBolts.Editor
{
    // Build authored assets as they are; regeneration is an explicit authoring action.
    public static class WorkshopBuilder
    {
        public const string ScenePath = "Assets/Project/Scenes/Workshop.unity";

        [MenuItem("Bots & Bolts/Setup/Open Workshop")]
        public static void OpenWorkshop()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Bots & Bolts/Build/Windows Build")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play Mode before building.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
                throw new InvalidOperationException("The Workshop scene is missing.");
            var scene = EditorSceneManager.GetSceneByPath(ScenePath);
            if (scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save the Workshop scene before building.");

            Directory.CreateDirectory("Builds/Workshop");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Workshop/BotsBolts.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Workshop build failed: " + report.summary.result);
            Debug.Log("[BotsBolts] Windows build succeeded.");
        }
    }
}