using BotsBolts.Players;
using UnityEngine;

namespace BotsBolts.Presentation
{
    // Each NetworkManager belongs to one peer. Only its owned player may bind the local view.
    public sealed class WorkshopCameraCoordinator : MonoBehaviour
    {
        [SerializeField] private WorkshopFollowCamera followCamera;
        public WorkshopFollowCamera FollowCamera => followCamera;
        public void Bind(Transform player, PlayerMotor motor) => followCamera.Follow(player, motor);
        public void Release(Transform player) => followCamera.Release(player);
    }
}
