using System;
using System.Linq;
using BotsBolts.Interactions;
using BotsBolts.Presentation;
using UnityEditor;
using UnityEngine;

namespace BotsBolts.Editor
{
    public static class CarryPoseSetup
    {
        [MenuItem("Bots & Bolts/Setup/Apply Hand Carry Pose")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
            const string path = "Assets/Project/Prefabs/WorkshopPlayer.prefab";
            GameObject player = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Configure(player);
                PrefabUtility.SaveAsPrefabAsset(player, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(player); }
            AssetDatabase.SaveAssets();
        }

        internal static void Configure(GameObject player)
        {
            var interaction = player.GetComponent<PlayerInteraction>();
            if (interaction == null) return;
            var bones = player.GetComponentsInChildren<Transform>(true);
            Transform body = bones.Single(t => t.name == "Body");
            Transform left = bones.Single(t => t.name == "Arm_Left");
            Transform right = bones.Single(t => t.name == "Arm_Right");
            if (left.parent != body || right.parent != body) throw new InvalidOperationException("The mechanical carry pose requires the existing Body/Arm rig.");
            var carry = player.GetComponent<RobotCarryPresentation>();
            if (carry == null) carry = player.AddComponent<RobotCarryPresentation>();
            // Palm contacts are authored against the model's bind geometry, not a
            // floating root offset. The imported rig mirrors its left/right names.
            Transform Contact(Transform arm, string name)
            {
                Transform contact = arm.Find(name);
                if (contact != null) return contact;
                contact = new GameObject(name).transform;
                contact.SetParent(arm, false);
                Vector3 bindPoint = new Vector3(Mathf.Sign(arm.localPosition.x) * .285f, .405f, .055f);
                contact.position = player.transform.TransformPoint(bindPoint);
                return contact;
            }
            var serialized = new SerializedObject(carry);
            serialized.FindProperty("interaction").objectReferenceValue = interaction;
            serialized.FindProperty("body").objectReferenceValue = body;
            serialized.FindProperty("leftArm").objectReferenceValue = left;
            serialized.FindProperty("rightArm").objectReferenceValue = right;
            serialized.FindProperty("leftGrip").objectReferenceValue = Contact(left, "Left Palm Contact");
            serialized.FindProperty("rightGrip").objectReferenceValue = Contact(right, "Right Palm Contact");
            // Preserve authored bind rotations on repeated calls.
            serialized.FindProperty("leftBindRotation").quaternionValue = left.localRotation;
            serialized.FindProperty("rightBindRotation").quaternionValue = right.localRotation;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Transform a = (Transform)serialized.FindProperty("leftGrip").objectReferenceValue;
            Transform b = (Transform)serialized.FindProperty("rightGrip").objectReferenceValue;
            interaction.CarryPoint.SetPositionAndRotation((a.position + b.position) * .5f - body.up * .24f + body.forward * .10f, body.rotation);
            var motion = new SerializedObject(player.GetComponent<PlayerMotionPresentation>());
            motion.FindProperty("carry").objectReferenceValue = carry;
            motion.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
