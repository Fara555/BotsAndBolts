using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using BotsBolts.Presentation;
using UnityEngine;

namespace BotsBolts.Players
{
    public sealed class NetworkPlayer : NetworkBehaviour
    {
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Renderer body;
        [SerializeField] private GameObject localMarker;
        [SerializeField] private PlayerMotionPresentation presentation;
        private WorkshopCameraCoordinator cameraCoordinator;
        private readonly SyncVar<int> slot = new SyncVar<int>();

        // Called by the server before Spawn; the initial SyncVar travels with the spawn.
        public void ConfigureSlot(int value) => slot.Value = value;

        public override void OnStartClient()
        {
            base.OnStartClient();
            cameraCoordinator = NetworkManager.GetComponent<WorkshopCameraCoordinator>();
            if (presentation != null) presentation.ResetPose();
            var properties = new MaterialPropertyBlock();
            Color color = slot.Value == 0 ? new Color(0.1f, 0.7f, 0.9f) : new Color(1f, 0.65f, 0.15f);
            properties.SetColor("_BaseColor", color);
            body.SetPropertyBlock(properties);
            SetLocalControl();
        }

        public override void OnOwnershipClient(NetworkConnection previousOwner)
        {
            base.OnOwnershipClient(previousOwner);
            SetLocalControl();
        }

        public override void OnStopClient()
        {
            motor.SetLocalControl(false);
            localMarker.SetActive(false);
            if (cameraCoordinator != null) cameraCoordinator.Release(transform);
            if (presentation != null) presentation.ResetPose();
            base.OnStopClient();
        }

        private void SetLocalControl()
        {
            motor.SetLocalControl(IsOwner);
            localMarker.SetActive(IsOwner);
            if (cameraCoordinator == null) return;
            if (IsOwner) cameraCoordinator.Bind(transform, motor);
            else cameraCoordinator.Release(transform);
        }
    }
}
