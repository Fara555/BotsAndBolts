using UnityEngine;

namespace BotsBolts.Interactions
{
    public sealed class BatterySource : MonoBehaviour
    {
        [SerializeField] private Transform interactionPoint;
        public Vector3 InteractionPoint => interactionPoint.position;
    }
}
