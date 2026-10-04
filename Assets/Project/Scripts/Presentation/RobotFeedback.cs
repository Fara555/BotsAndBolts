using UnityEngine;

namespace BotsBolts.Presentation
{
    public sealed class RobotFeedback : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip stepClip;
        [SerializeField] private AudioClip leapClip;
        [SerializeField] private AudioClip landClip;
        [SerializeField] private ParticleSystem dust;
        private float nextBrakeTime;

        public void ResetFeedback()
        {
            nextBrakeTime = 0;
            if (audioSource != null) audioSource.Stop();
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

        public void Land(float strength)
        {
            if (audioSource != null && landClip != null)
            {
                audioSource.pitch = 1.05f - strength * .12f;
                audioSource.PlayOneShot(landClip, strength * .65f);
            }
            EmitDust(Mathf.RoundToInt(6 * strength), .13f * strength);
        }

        public void Brake()
        {
            if (Time.time < nextBrakeTime) return;
            nextBrakeTime = Time.time + .25f;
            EmitDust(3, .08f);
        }

        private void EmitDust(int count, float size)
        {
            if (dust == null) return;
            var emit = new ParticleSystem.EmitParams { position = new Vector3(transform.position.x, .06f, transform.position.z), startSize = size };
            dust.Emit(emit, count);
        }
    }
}
