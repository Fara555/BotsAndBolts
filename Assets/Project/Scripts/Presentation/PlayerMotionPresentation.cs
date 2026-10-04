using UnityEngine;

namespace BotsBolts.Presentation
{
    public sealed class PlayerMotionPresentation : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform groundMarker;
        [SerializeField] private Animator animator;
        [SerializeField] private Transform head;
        [SerializeField] private Transform leftEye;
        [SerializeField] private Transform rightEye;
        [SerializeField] private RobotFeedback feedback;
        private RobotAnimationPlayback playback;
        private float animationSpeed;
        private Vector3 previousPosition;
        private Vector3 basePosition;
        private float gait;
        private bool wasAirborne;
        private bool initialized;
        private Vector3 filteredVelocity;
        private Vector2 lean, leanVelocity;
        private float compression, compressionVelocity, turnRate, previousYaw, verticalVelocity;
        private float headYaw, headPitch, eyeReaction;
        private int previousStep;
        private bool boneOffsetsApplied;
        private Quaternion animatedHead;
        private Vector3 animatedLeftEye, animatedRightEye;
        private BotsBolts.Players.PlayerMotor motor;
        private CharacterController controller;

        private void Awake()
        {
            motor = GetComponent<BotsBolts.Players.PlayerMotor>();
            controller = GetComponent<CharacterController>();
            playback = new RobotAnimationPlayback(animator);
        }

        public void ResetPose()
        {
            if (visualRoot == null) return;
            RestoreBonePose();
            previousPosition = transform.position;
            if (!initialized) basePosition = visualRoot.localPosition;
            initialized = true;
            wasAirborne = false;
            gait = 0;
            animationSpeed = 0;
            filteredVelocity = Vector3.zero;
            lean = leanVelocity = Vector2.zero;
            compression = compressionVelocity = turnRate = verticalVelocity = 0;
            headYaw = headPitch = eyeReaction = 0;
            previousYaw = transform.eulerAngles.y;
            previousStep = 0;
            if (feedback != null) feedback.ResetFeedback();
            playback?.Reset();
            visualRoot.localPosition = basePosition;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        private void OnEnable() => ResetPose();
        private void Update() => RestoreBonePose();
        private void OnDisable() => RestoreBonePose();

        private void RestoreBonePose()
        {
            if (!boneOffsetsApplied) return;
            if (head != null) head.localRotation = animatedHead;
            if (leftEye != null) leftEye.localScale = animatedLeftEye;
            if (rightEye != null) rightEye.localScale = animatedRightEye;
            boneOffsetsApplied = false;
        }

        private void LateUpdate()
        {
            if (visualRoot == null) return;
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            Vector3 velocity = (transform.position - previousPosition) / delta;
            previousPosition = transform.position;
            // Discontinuities such as a respawn must not create a cosmetic impact.
            if (velocity.sqrMagnitude > 400) { ResetPose(); return; }
            float previousVertical = verticalVelocity;
            verticalVelocity = Mathf.Lerp(verticalVelocity, velocity.y, 1 - Mathf.Exp(-20 * delta));
            velocity.y = 0;
            Vector3 oldVelocity = filteredVelocity;
            filteredVelocity = Vector3.Lerp(filteredVelocity, velocity, 1 - Mathf.Exp(-14 * delta));
            Vector3 acceleration = Vector3.ClampMagnitude((filteredVelocity - oldVelocity) / delta, 35);
            Vector3 localAcceleration = transform.InverseTransformDirection(acceleration);
            float speed = Mathf.Min(filteredVelocity.magnitude, 10);
            float rawTurn = Mathf.DeltaAngle(previousYaw, transform.eulerAngles.y) / delta;
            previousYaw = transform.eulerAngles.y;
            turnRate = Mathf.Lerp(turnRate, Mathf.Clamp(rawTurn, -540, 540), 1 - Mathf.Exp(-12 * delta));
            bool hasSurface = Physics.Raycast(transform.position + Vector3.up * .1f, Vector3.down,
                out RaycastHit surface, 3, ~0, QueryTriggerInteraction.Ignore);
            bool airborne = controller != null && controller.enabled && motor != null
                ? !motor.IsGrounded : !hasSurface || surface.distance > .24f;
            animationSpeed = Mathf.Lerp(animationSpeed, speed, 1 - Mathf.Exp(-12 * delta));
            bool launched = controller != null && controller.enabled && motor != null
                ? motor.IsForwardJump : verticalVelocity > .5f;
            RobotMotionTransition transition = playback.Update(animationSpeed, velocity.magnitude,
                airborne, wasAirborne, launched, verticalVelocity);
            if (transition == RobotMotionTransition.Leap)
            {
                compressionVelocity += 1.5f;
                if (feedback != null) feedback.Leap();
            }
            else if (transition == RobotMotionTransition.Land)
            {
                float strength = Mathf.Clamp(-previousVertical / 6, .35f, 1.2f);
                compressionVelocity += strength * 2.4f;
                eyeReaction = .7f;
                if (feedback != null) feedback.Land(strength);
            }
            wasAirborne = airborne;
            gait += speed * delta * 1.75f;
            gait = playback.GaitPhase(gait, speed);
            UpdateBody(delta, speed, airborne, localAcceleration);
            UpdateHeadAndEyes(delta, airborne);
            UpdateFeedback(speed, airborne, acceleration);
            UpdateGroundMarker(hasSurface, surface);
        }

        private void UpdateBody(float delta, float speed, bool airborne, Vector3 localAcceleration)
        {
            float running = Mathf.Clamp01(speed / 4.5f);
            float pitch = airborne ? (playback.IsFalling ? 26 : 40) : speed * 1.3f + Mathf.Clamp(localAcceleration.z * .45f, -9, 9);
            float roll = airborne ? 0 : -turnRate * .018f * running - localAcceleration.x * .2f + Mathf.Sin(gait) * 2.8f * running;
            Vector2 target = new Vector2(pitch, Mathf.Clamp(roll, -12, 12));
            // Substeps keep the underdamped spring stable even after a long frame.
            int steps = Mathf.CeilToInt(Mathf.Min(delta, .1f) / .0125f);
            float step = Mathf.Min(delta, .1f) / steps;
            for (int i = 0; i < steps; i++)
            {
                leanVelocity += ((target - lean) * 180 - leanVelocity * 15) * step;
                lean += leanVelocity * step;
                compressionVelocity += (-compression * 240 - compressionVelocity * 13) * step;
                compression += compressionVelocity * step;
            }
            float squash = Mathf.Clamp(compression, -.065f, .16f);
            visualRoot.localRotation = Quaternion.Euler(lean.x, 0, lean.y);
            visualRoot.localScale = new Vector3(1 + squash * .45f, 1 - squash, 1 + squash * .45f);
            // Counter the pelvis pivot so squash stays attached to the floor.
            visualRoot.localPosition = basePosition + Vector3.down * (basePosition.y * squash);
        }

        private void UpdateHeadAndEyes(float delta, bool airborne)
        {
            headYaw = Mathf.Lerp(headYaw, Mathf.Clamp(turnRate * .025f, -12, 12), 1 - Mathf.Exp(-8 * delta));
            headPitch = Mathf.Lerp(headPitch, -lean.x * .28f, 1 - Mathf.Exp(-7 * delta));
            if (head != null)
            {
                animatedHead = head.localRotation;
                Vector3 up = head.parent.InverseTransformDirection(transform.up);
                Vector3 right = head.parent.InverseTransformDirection(transform.right);
                head.localRotation = Quaternion.AngleAxis(headYaw, up) * Quaternion.AngleAxis(headPitch, right) * head.localRotation;
            }
            eyeReaction = Mathf.MoveTowards(eyeReaction, 0, delta * 5);
            float eyeScale = airborne ? .72f : 1 + eyeReaction * .25f;
            if (leftEye != null) animatedLeftEye = leftEye.localScale;
            if (rightEye != null) animatedRightEye = rightEye.localScale;
            ReactEye(leftEye, eyeScale); ReactEye(rightEye, eyeScale);
            boneOffsetsApplied = true;
        }

        private void UpdateFeedback(float speed, bool airborne, Vector3 acceleration)
        {
            int footstep = Mathf.FloorToInt(gait / Mathf.PI);
            if (footstep != previousStep && !airborne && speed > 1 && feedback != null) feedback.Step(footstep);
            previousStep = footstep;
            if (!airborne && speed > 1.2f && Vector3.Dot(acceleration, filteredVelocity.normalized) < -12 && feedback != null)
                feedback.Brake();
        }

        private void UpdateGroundMarker(bool hasSurface, RaycastHit surface)
        {
            if (groundMarker != null)
            {
                Vector3 markerPosition = groundMarker.localPosition;
                if (hasSurface) markerPosition.y = surface.point.y + .035f - transform.position.y;
                groundMarker.localPosition = markerPosition;
            }
        }

        private static void ReactEye(Transform eye, float scale)
        {
            if (eye == null) return;
            Vector3 value = eye.localScale;
            value.y *= scale;
            eye.localScale = value;
        }
    }
}
