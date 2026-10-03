using UnityEngine;

namespace BotsBolts.Presentation
{
    public sealed class PlayerMotionPresentation : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Transform groundMarker;
        private Vector3 previousPosition;
        private Vector3 basePosition;
        private float gait;
        private float landing;
        private bool wasAirborne;
        private bool initialized;

        public void ResetPose()
        {
            if (visualRoot == null) return;
            previousPosition = transform.position;
            if (!initialized) basePosition = visualRoot.localPosition;
            initialized = true;
            wasAirborne = false;
            landing = gait = 0;
            visualRoot.localPosition = basePosition;
            visualRoot.localRotation = Quaternion.identity;
            visualRoot.localScale = Vector3.one;
        }

        private void OnEnable() => ResetPose();

        private void LateUpdate()
        {
            if (visualRoot == null) return;
            float delta = Time.deltaTime;
            if (delta <= 0) return;
            Vector3 velocity = (transform.position - previousPosition) / delta;
            previousPosition = transform.position;
            velocity.y = 0;
            float speed = Mathf.Min(velocity.magnitude, 10);
            bool airborne = transform.position.y > 0.16f;
            if (wasAirborne && !airborne) landing = 1;
            wasAirborne = airborne;
            landing = Mathf.MoveTowards(landing, 0, delta * 6);
            gait += speed * delta * 3;
            float blend = 1f - Mathf.Exp(-18f * delta);
            float pitch = airborne ? 42 : speed * 1.2f;
            float sway = airborne ? 0 : Mathf.Sin(gait) * Mathf.Min(speed, 4) * 0.7f;
            visualRoot.localRotation = Quaternion.Slerp(visualRoot.localRotation, Quaternion.Euler(pitch, 0, sway), blend);
            Vector3 scale = new Vector3(1 + landing * 0.12f, 1 - landing * 0.18f, 1 + landing * 0.12f);
            visualRoot.localScale = Vector3.Lerp(visualRoot.localScale, scale, blend);
            float bob = airborne ? 0 : Mathf.Abs(Mathf.Sin(gait)) * Mathf.Min(speed, 4) * 0.009f;
            visualRoot.localPosition = Vector3.Lerp(visualRoot.localPosition, basePosition + Vector3.up * bob, blend);
            if (groundMarker != null)
            {
                Vector3 markerPosition = groundMarker.localPosition;
                markerPosition.y = 0.035f - transform.position.y;
                groundMarker.localPosition = markerPosition;
            }
        }
    }
}
