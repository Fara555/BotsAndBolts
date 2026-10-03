using Sirenix.OdinInspector;
using UnityEngine;

namespace BotsBolts.Session
{
    [CreateAssetMenu(menuName = "Bots & Bolts/Workshop Settings")]
    public sealed class WorkshopSettings : ScriptableObject
    {
        [Title("Connection"), SerializeField, MinValue(1)] private int port = 7770;
        [SerializeField, Range(5, 60)] private float connectionTimeout = 12f;
        [Title("Movement"), SerializeField, Range(1, 10)] private float moveSpeed = 4.5f;
        [SerializeField, Range(90, 1080)] private float turnSpeed = 720f;
        [SerializeField, Range(5, 80)] private float acceleration = 28f;
        [SerializeField, Range(5, 100)] private float braking = 38f;
        [SerializeField, Range(0.03f, 0.3f)] private float turnSmoothing = 0.09f;
        [Title("Forward leap"), SerializeField, Range(0.2f, 1.1f)] private float leapHeight = 0.8f;
        [SerializeField, Range(3, 12)] private float leapSpeed = 7.5f;
        [SerializeField, Range(10, 50)] private float gravity = 25f;
        [SerializeField, Range(0, 0.5f)] private float airSteering = 0.2f;
        [SerializeField, Range(0.3f, 2), Tooltip("Minimum time between leap starts; landing is also required.")] private float leapCooldown = 0.7f;
        [SerializeField, Range(0, 0.2f), Tooltip("Grace period after leaving the ground, in seconds.")] private float coyoteTime = 0.08f;
        [SerializeField, Range(0, 0.2f), Tooltip("How long a recent press may wait for landing, in seconds.")] private float jumpBuffer = 0.12f;
        [Title("Local follow camera"), SerializeField] private Vector3 cameraOffset = new Vector3(0, 10, -8);
        [SerializeField, Range(3, 9)] private float cameraSize = 5.6f;
        [SerializeField, Range(0.03f, 0.6f), Tooltip("Larger values give the camera more trailing motion.")] private float cameraDamping = 0.2f;
        [SerializeField, Range(0, 0.5f), Tooltip("Movement velocity multiplied by this time gives the camera's look-ahead.")] private float cameraLookAhead = 0.16f;
        [SerializeField, Range(0, 1)] private float cameraVerticalFollow = 0.2f;

        public ushort Port => (ushort)Mathf.Clamp(port, 1, ushort.MaxValue);
        public float ConnectionTimeout => connectionTimeout;
        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;
        public float Acceleration => acceleration;
        public float Braking => braking;
        public float TurnSmoothing => turnSmoothing;
        public float LeapHeight => leapHeight;
        public float LeapSpeed => leapSpeed;
        public float Gravity => gravity;
        public float AirSteering => airSteering;
        public float LeapCooldown => leapCooldown;
        public float CoyoteTime => coyoteTime;
        public float JumpBuffer => jumpBuffer;
        public Vector3 CameraOffset => cameraOffset;
        public float CameraSize => cameraSize;
        public float CameraDamping => cameraDamping;
        public float CameraLookAhead => cameraLookAhead;
        public float CameraVerticalFollow => cameraVerticalFollow;

        private void OnValidate()
        {
            port = Mathf.Clamp(port, 1, ushort.MaxValue);
            connectionTimeout = Mathf.Clamp(connectionTimeout, 5, 60);
            moveSpeed = Mathf.Clamp(moveSpeed, 1, 10);
            turnSpeed = Mathf.Clamp(turnSpeed, 90, 1080);
            acceleration = Mathf.Clamp(acceleration, 5, 80);
            braking = Mathf.Clamp(braking, 5, 100);
            turnSmoothing = Mathf.Clamp(turnSmoothing, 0.03f, 0.3f);
            leapHeight = Mathf.Clamp(leapHeight, 0.2f, 1.1f);
            leapSpeed = Mathf.Clamp(leapSpeed, 3, 12);
            gravity = Mathf.Clamp(gravity, 10, 50);
            airSteering = Mathf.Clamp(airSteering, 0, 0.5f);
            leapCooldown = Mathf.Clamp(leapCooldown, 0.3f, 2);
            coyoteTime = Mathf.Clamp(coyoteTime, 0, 0.2f);
            jumpBuffer = Mathf.Clamp(jumpBuffer, 0, 0.2f);
            cameraDamping = Mathf.Clamp(cameraDamping, 0.03f, 0.6f);
            cameraLookAhead = Mathf.Clamp(cameraLookAhead, 0, 0.5f);
            cameraVerticalFollow = Mathf.Clamp01(cameraVerticalFollow);
            cameraSize = Mathf.Clamp(cameraSize, 3, 9);
            cameraOffset.y = Mathf.Max(4, cameraOffset.y);
            if (new Vector2(cameraOffset.x, cameraOffset.z).sqrMagnitude < 4) cameraOffset.z = -8;
        }
    }
}
