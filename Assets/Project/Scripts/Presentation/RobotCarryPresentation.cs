using BotsBolts.Interactions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BotsBolts.Presentation
{
    // The existing motion presenter applies this after Animator/body motion, then
    // the battery follows the actual palm sockets. No gameplay state is animated.
    public sealed class RobotCarryPresentation : MonoBehaviour
    {
        [SerializeField, Required] private PlayerInteraction interaction;
        [SerializeField, Required] private Transform body;
        [SerializeField, Required] private Transform leftArm;
        [SerializeField, Required] private Transform rightArm;
        [SerializeField, Required] private Transform leftGrip;
        [SerializeField, Required] private Transform rightGrip;
        [SerializeField] private Quaternion leftBindRotation;
        [SerializeField] private Quaternion rightBindRotation;
        [SerializeField, Range(.02f, .15f)] private float holdLowering = .08f;
        [SerializeField, Range(.05f, .3f)] private float blendTime = .12f;
        private Quaternion animatedLeft, animatedRight;
        private bool poseApplied;
        private float weight;
        private float halfWidth = .24f;
        private float gripHeight = .24f;
        private float gripDepth = -.10f;
        public Transform LeftGrip => leftGrip;
        public Transform RightGrip => rightGrip;
        public float PoseWeight => weight;

        public void RestorePose()
        {
            if (!poseApplied) return;
            if (leftArm != null) leftArm.localRotation = animatedLeft;
            if (rightArm != null) rightArm.localRotation = animatedRight;
            poseApplied = false;
        }

        public void ResetPose()
        {
            RestorePose();
            weight = 0;
        }

        private void OnDisable() => ResetPose();

        public void SnapToCurrentGrip()
        {
            weight = interaction.HeldBattery == null ? 0 : 1;
            if (weight > 0) ApplyPose(0);
            else UpdateCarryPoint();
        }

        public void ApplyPose(float delta)
        {
            NetworkBattery battery = interaction.HeldBattery;
            weight = Mathf.MoveTowards(weight, battery == null ? 0 : 1, delta / blendTime);
            if (weight <= 0) return;
            // The configured item contacts sit on its two sides, near the rear edge.
            // Converting their spacing to Body coordinates also accounts for landing squash.
            if (battery != null)
            {
                halfWidth = battery.GripHalfWidth;
                gripHeight = battery.GripHeight;
                gripDepth = battery.GripDepth;
            }
            float centerY = (leftArm.localPosition.y + rightArm.localPosition.y) * .5f - holdLowering;
            float scaleX = Mathf.Max(.01f, body.TransformVector(Vector3.right).magnitude);
            float spacing = halfWidth / scaleX;
            animatedLeft = leftArm.localRotation;
            animatedRight = rightArm.localRotation;
            AimArm(leftArm, leftGrip, leftBindRotation, centerY, spacing);
            AimArm(rightArm, rightGrip, rightBindRotation, centerY, spacing);
            poseApplied = true;
            UpdateCarryPoint();
        }

        private void UpdateCarryPoint()
        {
            Vector3 center = (leftGrip.position + rightGrip.position) * .5f;
            interaction.CarryPoint.SetPositionAndRotation(center - body.up * gripHeight - body.forward * gripDepth, body.rotation);
        }

        private void AimArm(Transform arm, Transform grip, Quaternion bind, float centerY, float spacing)
        {
            Vector3 bindDirection = bind * Vector3.Scale(arm.localScale, grip.localPosition);
            float x = Mathf.Sign(arm.localPosition.x) * spacing;
            float dx = x - arm.localPosition.x;
            float dy = centerY - arm.localPosition.y;
            float forward = Mathf.Sqrt(Mathf.Max(.001f, bindDirection.sqrMagnitude - dx * dx - dy * dy));
            Vector3 direction = new Vector3(dx, dy, forward);
            Quaternion target = Quaternion.FromToRotation(bindDirection, direction) * bind;
            arm.localRotation = Quaternion.Slerp(arm.localRotation, target, weight);
        }
    }
}
