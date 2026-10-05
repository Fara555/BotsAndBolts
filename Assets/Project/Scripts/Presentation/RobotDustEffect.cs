using UnityEngine;
using UnityEngine.VFX;

namespace BotsBolts.Presentation
{
    [RequireComponent(typeof(VisualEffect))]
    public sealed class RobotDustEffect : MonoBehaviour
    {
        [SerializeField] private VisualEffect effect;
        private static readonly int Burst = Shader.PropertyToID("DustBurst");
        private static readonly int Position = Shader.PropertyToID("BurstPosition");
        private static readonly int Direction = Shader.PropertyToID("BurstDirection");
        private static readonly int Normal = Shader.PropertyToID("GroundNormal");
        private static readonly int Strength = Shader.PropertyToID("BurstStrength");
        private static readonly int Brake = Shader.PropertyToID("IsBrake");
        private static readonly int Clouds = Shader.PropertyToID("CloudCount");
        private static readonly int Wisps = Shader.PropertyToID("WispCount");
        private static readonly int Ground = Shader.PropertyToID("GroundCount");

        private void Awake()
        {
            if (effect == null) effect = GetComponent<VisualEffect>();
        }

        public void Clear()
        {
            if (effect == null) return;
            effect.SetFloat(Clouds, 0);
            effect.SetFloat(Wisps, 0);
            effect.SetFloat(Ground, 0);
            effect.Reinit();
            effect.Stop();
        }

        public void Emit(Vector3 point, Vector3 normal, Vector3 direction, float strength, bool braking)
        {
            if (effect == null || effect.visualEffectAsset == null) return;
            float weight = Mathf.Clamp(strength, .35f, 1.2f);
            direction = Vector3.ProjectOnPlane(direction, normal).normalized;
            effect.SetVector3(Position, point);
            effect.SetVector3(Normal, normal);
            effect.SetVector3(Direction, direction);
            effect.SetFloat(Strength, braking ? .7f : Mathf.Lerp(.7f, 1.15f, weight / 1.2f));
            effect.SetFloat(Brake, braking ? 1 : 0);
            effect.SetFloat(Clouds, braking ? 3 : Mathf.RoundToInt(6 * weight));
            effect.SetFloat(Wisps, braking ? 1 : Mathf.RoundToInt(2 * weight));
            effect.SetFloat(Ground, braking ? 1 : Mathf.RoundToInt(2 * weight));
            effect.SendEvent(Burst);
        }
    }
}
