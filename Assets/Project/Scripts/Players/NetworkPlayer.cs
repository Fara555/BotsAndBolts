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
        [SerializeField] private Renderer[] colorShells;
        [SerializeField] private GameObject localMarker;
        [SerializeField] private PlayerMotionPresentation presentation;
        private WorkshopCameraCoordinator cameraCoordinator;
        private readonly SyncVar<int> slot = new SyncVar<int>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color FirstPlayerColor = new Color(.1f, .7f, .9f);
        private static readonly Color SecondPlayerColor = new Color(1, .65f, .15f);

        // Called by the server before Spawn; the initial SyncVar travels with the spawn.
        public void ConfigureSlot(int value) => slot.Value = value;

        public override void OnStartClient()
        {
            base.OnStartClient();
            cameraCoordinator = NetworkManager.GetComponent<WorkshopCameraCoordinator>();
            if (presentation != null) presentation.ResetPose();
            var properties = new MaterialPropertyBlock();
            properties.SetColor(BaseColor, slot.Value == 0 ? FirstPlayerColor : SecondPlayerColor);
            foreach (Renderer shell in colorShells)
                if (shell != null) shell.SetPropertyBlock(properties);
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
