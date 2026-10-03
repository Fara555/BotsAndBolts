using BotsBolts.Players;
using BotsBolts.Session;
using UnityEngine;

namespace BotsBolts.Presentation
{
    [RequireComponent(typeof(Camera))]
    public sealed class WorkshopFollowCamera : MonoBehaviour
    {
        [SerializeField] private WorkshopSettings settings;
        private Camera view;
        private Transform target;
        private PlayerMotor motor;
        private Vector3 overviewPosition;
        private Quaternion overviewRotation;
        private float overviewSize;
        private Vector3 followPosition;
        private Vector3 followVelocity;
        private float sizeVelocity;
        public Transform Target => target;

        private void Awake()
        {
            view = GetComponent<Camera>();
            overviewPosition = transform.position;
            overviewRotation = transform.rotation;
            overviewSize = view.orthographicSize;
            followPosition = transform.position;
        }

        public void Follow(Transform player, PlayerMotor playerMotor)
        {
            if (target == player) return;
            target = player;
            motor = playerMotor;
            followPosition = transform.position;
            followVelocity = Vector3.zero;
            sizeVelocity = 0;
        }

        public void Release(Transform player)
        {
            if (target != player) return;
            target = null;
            motor = null;
        }

        private void LateUpdate()
        {
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            Vector3 desiredPosition = overviewPosition;
            Quaternion desiredRotation = overviewRotation;
            float desiredSize = overviewSize;
            if (target != null)
            {
                Vector3 anchor = target.position;
                // Keep the floor stable while retaining a little vertical response to a leap.
                anchor.y = 0.5f + Mathf.Clamp(anchor.y, 0, 1.5f) * settings.CameraVerticalFollow;
                Vector3 lookAhead = motor != null
                    ? Vector3.ClampMagnitude(motor.PlanarVelocity * settings.CameraLookAhead, 1.2f) : Vector3.zero;
                desiredPosition = anchor + lookAhead + settings.CameraOffset;
                desiredRotation = Quaternion.LookRotation(-settings.CameraOffset);
                desiredSize = settings.CameraSize;
            }
            followPosition = Vector3.SmoothDamp(followPosition, desiredPosition, ref followVelocity,
                settings.CameraDamping, Mathf.Infinity, delta);
            transform.SetPositionAndRotation(followPosition, Quaternion.Slerp(transform.rotation,
                desiredRotation, 1f - Mathf.Exp(-delta / settings.CameraDamping)));
            view.orthographicSize = Mathf.SmoothDamp(view.orthographicSize, desiredSize,
                ref sizeVelocity, settings.CameraDamping, Mathf.Infinity, delta);
        }
    }
}
