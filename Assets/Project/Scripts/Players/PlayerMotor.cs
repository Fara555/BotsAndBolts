using BotsBolts.Session;
using UnityEngine;

namespace BotsBolts.Players
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [SerializeField] private WorkshopSettings settings;
        [SerializeField] private PlayerInputReader input;
        private CharacterController controller;
        private bool localControl;
        private float verticalSpeed;
        private Vector3 horizontalVelocity;
        private Vector3 actualPlanarVelocity;
        private Vector3 leapDirection;
        private float turnVelocity;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpBufferedUntil = float.NegativeInfinity;
        private float nextLeapTime;
        public bool IsForwardJump { get; private set; }
        public bool IsGrounded => controller != null && controller.enabled && controller.isGrounded && verticalSpeed <= 0;
        public Vector3 PlanarVelocity => actualPlanarVelocity;

        private void Awake() => controller = GetComponent<CharacterController>();

        public void SetLocalControl(bool value)
        {
            localControl = value;
            verticalSpeed = 0;
            horizontalVelocity = Vector3.zero;
            actualPlanarVelocity = Vector3.zero;
            turnVelocity = 0;
            lastGroundedTime = jumpBufferedUntil = float.NegativeInfinity;
            nextLeapTime = 0;
            IsForwardJump = false;
            // FishNet stop callbacks can run while Unity is destroying scene components.
            if (controller != null) controller.enabled = value;
            if (input != null) input.SetLocalControl(value);
        }

        private void Update()
        {
            if (!localControl || !controller.enabled) return;
            Tick(input.Movement, input.JumpPressed, Time.time, Time.deltaTime);
        }

        private void Tick(Vector2 movement, bool jumpPressed, float now, float delta)
        {
            if (delta <= 0) return;
            Vector3 viewForward = -settings.CameraOffset;
            viewForward.y = 0;
            viewForward.Normalize();
            Vector3 viewRight = Vector3.Cross(Vector3.up, viewForward);
            Vector3 direction = viewRight * movement.x + viewForward * movement.y;
            bool grounded = IsGrounded;
            if (grounded)
            {
                lastGroundedTime = now;
                verticalSpeed = -2f;
                IsForwardJump = false;
            }
            if (jumpPressed) jumpBufferedUntil = now + settings.JumpBuffer;
            if (!IsForwardJump && now <= jumpBufferedUntil && now >= nextLeapTime &&
                now - lastGroundedTime <= settings.CoyoteTime)
            {
                leapDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
                horizontalVelocity = leapDirection * settings.LeapSpeed;
                verticalSpeed = Mathf.Sqrt(2f * settings.Gravity * settings.LeapHeight);
                nextLeapTime = now + settings.LeapCooldown;
                jumpBufferedUntil = lastGroundedTime = float.NegativeInfinity;
                IsForwardJump = true;
            }

            Vector3 desiredVelocity = direction * settings.MoveSpeed;
            if (IsForwardJump)
            {
                Vector3 steeredDirection = direction.sqrMagnitude > 0.01f
                    ? Vector3.Lerp(leapDirection, direction.normalized, settings.AirSteering).normalized : leapDirection;
                desiredVelocity = steeredDirection * settings.LeapSpeed;
            }
            float acceleration = direction.sqrMagnitude < 0.01f && !IsForwardJump ? settings.Braking : settings.Acceleration;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, desiredVelocity, acceleration * delta);
            verticalSpeed = Mathf.Max(verticalSpeed - settings.Gravity * delta, -30f);
            Vector3 previousPosition = transform.position;
            CollisionFlags collision = controller.Move((horizontalVelocity + Vector3.up * verticalSpeed) * delta);
            actualPlanarVelocity = (transform.position - previousPosition) / delta;
            actualPlanarVelocity.y = 0;
            if ((collision & CollisionFlags.Above) != 0 && verticalSpeed > 0) verticalSpeed = 0;
            if ((collision & CollisionFlags.Below) != 0 && verticalSpeed < 0)
            {
                verticalSpeed = -2f;
                IsForwardJump = false;
            }
            Vector3 facing = IsForwardJump ? horizontalVelocity : direction;
            if (facing.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
                float yaw = Mathf.SmoothDampAngle(transform.eulerAngles.y, angle, ref turnVelocity,
                    settings.TurnSmoothing, settings.TurnSpeed, delta);
                transform.rotation = Quaternion.Euler(0, yaw, 0);
            }
        }
    }
}
