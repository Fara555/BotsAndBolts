using FishNet.Object;
using FishNet.Object.Synchronizing;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BotsBolts.Interactions
{
    // Only carrier and resting position replicate; carried visuals follow the observed player.
    [DefaultExecutionOrder(100)]
    public sealed class NetworkBattery : NetworkBehaviour
    {
        [SerializeField] private Collider pickupCollider;
        [SerializeField, Min(.05f)] private float gripHalfWidth = .24f;
        [SerializeField, Min(0)] private float gripHeight = .24f;
        [SerializeField] private float gripDepth = -.10f;
        private readonly SyncVar<NetworkObject> carrier = new SyncVar<NetworkObject>();
        private readonly SyncVar<Vector3> restingPosition = new SyncVar<Vector3>();
        private PlayerInteraction carrierInteraction;
        [ShowInInspector, ReadOnly] public NetworkObject Carrier => carrier.Value;
        public Vector3 InteractionPoint => restingPosition.Value + Vector3.up * 0.25f;
        public bool IsAvailable => Carrier == null;
        public float GripHalfWidth => gripHalfWidth;
        public float GripHeight => gripHeight;
        public float GripDepth => gripDepth;

        public void InitializeRestingPosition(Vector3 position) => restingPosition.Value = position;

        public bool TryTake(PlayerInteraction player)
        {
            if (!IsServerInitialized || !IsAvailable || player.HeldBattery != null) return false;
            carrier.Value = player.NetworkObject;
            player.SetHeldBattery(this);
            return true;
        }

        public void Release(PlayerInteraction player, Vector3 position)
        {
            if (!IsServerInitialized || Carrier != player.NetworkObject) return;
            restingPosition.Value = position;
            carrier.Value = null;
            player.SetHeldBattery(null);
            ApplyPose();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkManager.GetComponent<WorkshopInteractions>().Register(this);
        }

        public override void OnStopServer()
        {
            if (NetworkManager != null)
            {
                var workshop = NetworkManager.GetComponent<WorkshopInteractions>();
                if (workshop != null) workshop.Unregister(this);
            }
            base.OnStopServer();
        }

        private void LateUpdate() => ApplyPose();

        private void ApplyPose()
        {
            if ((!IsClientInitialized && !IsServerInitialized) || pickupCollider == null) return;
            pickupCollider.enabled = IsAvailable;
            if (Carrier == null)
            {
                carrierInteraction = null;
                transform.SetPositionAndRotation(restingPosition.Value, Quaternion.identity);
                return;
            }
            if (carrierInteraction == null || carrierInteraction.NetworkObject != Carrier)
                carrierInteraction = Carrier.GetComponent<PlayerInteraction>();
            if (carrierInteraction != null)
                transform.SetPositionAndRotation(carrierInteraction.CarryPoint.position, carrierInteraction.CarryPoint.rotation);
        }
    }
}
