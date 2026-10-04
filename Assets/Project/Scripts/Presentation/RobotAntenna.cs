using UnityEngine;

namespace BotsBolts.Presentation
{
    // A constrained tip mass driven by the moving head socket, evaluated after presentation.
    [DefaultExecutionOrder(100)]
    public sealed class RobotAntenna : MonoBehaviour
    {
        [SerializeField] private Transform rod;
        [SerializeField] private float length = .36f;
        [SerializeField] private float stiffness = 115;
        [SerializeField] private float damping = 8;
        private Vector3 tip, velocity, previousAnchor;
        private bool initialized;
        public float Deflection => rod == null ? 0 : Vector3.Angle(transform.up, rod.up);

        public void ResetSimulation()
        {
            previousAnchor = transform.position;
            tip = previousAnchor + transform.up * length * transform.lossyScale.y;
            velocity = Vector3.zero;
            if (rod != null) rod.localRotation = Quaternion.identity;
            initialized = true;
        }

        private void OnEnable() => ResetSimulation();

        private void LateUpdate()
        {
            if (rod == null || Time.deltaTime <= 0) return;
            Vector3 anchor = transform.position;
            float delta = Time.deltaTime;
            if (!initialized || delta > .2f || (anchor - previousAnchor).sqrMagnitude > 1)
            { ResetSimulation(); return; }
            Vector3 anchorVelocity = (anchor - previousAnchor) / delta;
            float distance = length * transform.lossyScale.y;
            Vector3 up = transform.up;
            int steps = Mathf.CeilToInt(delta / .008f);
            float dt = delta / steps;
            for (int i = 0; i < steps; i++)
            {
                Vector3 origin = Vector3.Lerp(previousAnchor, anchor, (i + 1f) / steps);
                Vector3 rest = origin + up * distance;
                velocity += ((rest - tip) * stiffness - (velocity - anchorVelocity) * damping + Physics.gravity * .15f) * dt;
                tip += velocity * dt;
                Vector3 direction = (tip - origin).normalized;
                direction = Vector3.RotateTowards(up, direction, 45 * Mathf.Deg2Rad, 0);
                tip = origin + direction * distance;
                Vector3 relative = velocity - anchorVelocity;
                velocity -= direction * Vector3.Dot(relative, direction);
            }
            rod.rotation = Quaternion.FromToRotation(up, (tip - anchor).normalized) * transform.rotation;
            previousAnchor = anchor;
        }
    }
}
