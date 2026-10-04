using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace BotsBolts.Editor
{
    // Existing Editor sessions can retain the controller imported before CycleRate
    // was introduced. Refresh that asset before touching its serialized contents.
    [InitializeOnLoad]
    public static class RobotAnimationUpgrade
    {
        private const string Path = "Assets/Project/Art/Characters/Robot/Robot.controller";

        static RobotAnimationUpgrade()
        {
            EditorApplication.delayCall += EnsureCurrent;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode ||
                    state == PlayModeStateChange.EnteredEditMode) EnsureCurrent();
            };
        }

        public static void EnsureCurrent()
        {
            if (EditorApplication.isPlaying) return;
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
            if (controller == null || IsCurrent(controller)) return;

            AssetDatabase.ImportAsset(Path,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Path);
            if (controller == null || IsCurrent(controller)) return;

            // Also migrate older controller assets without rebuilding the robot,
            // replacing states, or changing references held by the player prefab.
            var parameter = controller.parameters.FirstOrDefault(p => p.name == "CycleRate");
            if (parameter != null && parameter.type != AnimatorControllerParameterType.Float)
            {
                Debug.LogError("[BotsBolts] Robot CycleRate must be a Float parameter.", controller);
                return;
            }
            if (parameter == null)
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = "CycleRate", type = AnimatorControllerParameterType.Float, defaultFloat = 1
                });
            foreach (AnimatorControllerLayer layer in controller.layers)
                foreach (ChildAnimatorState child in layer.stateMachine.states)
                    if (child.state.name == "Locomotion")
                    {
                        child.state.speedParameter = "CycleRate";
                        child.state.speedParameterActive = true;
                        EditorUtility.SetDirty(child.state);
                    }
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssetIfDirty(controller);
        }

        private static bool IsCurrent(AnimatorController controller)
        {
            return controller.parameters.Any(p => p.name == "CycleRate" &&
                       p.type == AnimatorControllerParameterType.Float) &&
                   controller.layers.SelectMany(l => l.stateMachine.states)
                       .Where(s => s.state.name == "Locomotion")
                       .All(s => s.state.speedParameterActive && s.state.speedParameter == "CycleRate");
        }
    }
}
