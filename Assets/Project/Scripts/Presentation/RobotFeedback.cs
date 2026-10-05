using UnityEngine;

namespace BotsBolts.Presentation
{
    public sealed class RobotFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip stepClip;
        [SerializeField] private AudioClip leapClip;
        [SerializeField] private AudioClip landClip;
        [SerializeField] private RobotDustEffect dust;
        private float nextBrakeTime;

        public void ResetFeedback()
        {
            nextBrakeTime = 0;
            if (audioSource != null) audioSource.Stop();
            if (dust != null) dust.Clear();
        }

        public void Step(int index)
        {
            if (audioSource == null || stepClip == null) return;
            audioSource.pitch = (index & 1) == 0 ? 1.03f : .96f;
            audioSource.PlayOneShot(stepClip, .4f);
        }

        public void Leap()
        {
            if (audioSource != null && leapClip != null)
            {
                audioSource.pitch = 1;
                audioSource.PlayOneShot(leapClip, .55f);
            }
        }

        public void Land(float strength, Vector3 point, Vector3 normal)
        {
            if (audioSource != null && landClip != null)
            {
                audioSource.pitch = 1.05f - strength * .12f;
                audioSource.PlayOneShot(landClip, strength * .65f);
            }
            nextBrakeTime = Time.time + .25f;
            if (dust != null) dust.Emit(point, normal, transform.forward, strength, false);
        }

        public void Brake(Vector3 point, Vector3 normal, Vector3 direction)
        {
            if (Time.time < nextBrakeTime) return;
            nextBrakeTime = Time.time + .25f;
            if (dust != null) dust.Emit(point, normal, direction, .7f, true);
        }
    }
}
