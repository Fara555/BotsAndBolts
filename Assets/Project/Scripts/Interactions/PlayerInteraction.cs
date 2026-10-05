using BotsBolts.Players;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace BotsBolts.Interactions
{
    public sealed class PlayerInteraction : NetworkBehaviour
    {
        [SerializeField, Required] private PlayerInputReader input;
        [SerializeField, Required] private Transform carryPoint;
        [SerializeField, Required] private GameObject hintCanvas;
        [SerializeField, Required] private Text hint;
        [SerializeField, Required] private Transform targetMarker;
        private readonly SyncVar<NetworkObject> held = new SyncVar<NetworkObject>();
        private readonly Collider[] candidates = new Collider[48];
        private WorkshopInteractions workshop;
        private NetworkBattery selectedBattery;
        private bool selectedSource;
        private float nextRequestTime;
        private float nextLocalRequestTime;
        private string rejection;
        private float rejectionUntil;
        public Transform CarryPoint => carryPoint;
        [ShowInInspector, ReadOnly] public NetworkBattery HeldBattery => held.Value == null ? null : held.Value.GetComponent<NetworkBattery>();

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            workshop = NetworkManager.GetComponent<WorkshopInteractions>();
            if (workshop == null) throw new System.InvalidOperationException("Workshop interaction references are missing.");
        }

        public void SetHeldBattery(NetworkBattery battery)
        {
            if (IsServerInitialized) held.Value = battery == null ? null : battery.NetworkObject;
        }

        public override void OnStopServer()
        {
            NetworkBattery battery = HeldBattery;
            if (battery != null && workshop != null && !workshop.IsShuttingDown)
                battery.Release(this, workshop.DisconnectDrop(this));
            base.OnStopServer();
        }

        public override void OnOwnershipServer(NetworkConnection previousOwner)
        {
            base.OnOwnershipServer(previousOwner);
            if (HeldBattery != null) HeldBattery.Release(this, workshop.DisconnectDrop(this));
        }

        private void Update()
        {
            bool local = IsClientInitialized && IsOwner;
            hintCanvas.SetActive(local);
            if (!local) { targetMarker.gameObject.SetActive(false); return; }
            SelectTarget();
            string action = HeldBattery != null ? "Put down battery" : selectedSource ? "Take new battery" :
                selectedBattery != null ? "Take battery" : "Find a battery or its supply";
            string text = Time.unscaledTime < rejectionUntil ? rejection :
                (HeldBattery != null || selectedSource || selectedBattery != null ? input.InteractBinding + "  ·  " : "") + action;
            if (hint.text != text) hint.text = text;
            if (input.InteractPressed && Time.unscaledTime >= nextLocalRequestTime &&
                (HeldBattery != null || selectedSource || selectedBattery != null))
            {
                nextLocalRequestTime = Time.unscaledTime + workshop.Settings.InteractionCooldown;
                RequestInteraction(selectedSource, selectedBattery == null ? null : selectedBattery.NetworkObject, held.Value);
            }
        }

        private void SelectTarget()
        {
            selectedBattery = null;
            selectedSource = false;
            float best = float.MaxValue;
            Vector3 point = default;
            if (HeldBattery == null)
            {
                BatterySource source = workshop.Source;
                if (workshop.CanReach(this, source.InteractionPoint, source.transform))
                {
                    selectedSource = true;
                    point = source.InteractionPoint;
                    best = (point - transform.position).sqrMagnitude;
                }
                int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up * .7f,
                    workshop.Settings.InteractionRange, candidates, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
                for (int i = 0; i < count; i++)
                {
                    var battery = candidates[i].GetComponentInParent<NetworkBattery>();
                    if (battery == null || !battery.IsClientInitialized || !battery.IsAvailable) continue;
                    float distance = (battery.InteractionPoint - transform.position).sqrMagnitude;
                    if (distance >= best || !workshop.CanReach(this, battery.InteractionPoint, battery.transform)) continue;
                    best = distance;
                    point = battery.InteractionPoint;
                    selectedBattery = battery;
                    selectedSource = false;
                }
            }
            targetMarker.gameObject.SetActive(selectedSource || selectedBattery != null);
            if (targetMarker.gameObject.activeSelf) targetMarker.position = point + Vector3.up * .4f;
        }

        // Ownership is required by FishNet. expectedHeld makes retried/stale commands harmless.
        [ServerRpc]
        private void RequestInteraction(bool useSource, NetworkObject target, NetworkObject expectedHeld)
        {
            if (Time.unscaledTime < nextRequestTime) return;
            nextRequestTime = Time.unscaledTime + workshop.Settings.InteractionCooldown;
            if (held.Value != expectedHeld) { Reject(Owner, "Hands changed — try again."); return; }
            NetworkBattery battery = HeldBattery;
            if (battery != null)
            {
                if (!workshop.TryFindDrop(this, out Vector3 position)) { Reject(Owner, "No free space to put the battery."); return; }
                battery.Release(this, position);
                return;
            }
            if (useSource)
            {
                if (!workshop.TrySupply(this)) Reject(Owner, "Supply unavailable — pick up an existing battery.");
                return;
            }
            battery = target == null ? null : target.GetComponent<NetworkBattery>();
            if (battery == null || !battery.IsServerInitialized || battery.NetworkManager != NetworkManager ||
                !workshop.CanReach(this, battery.InteractionPoint, battery.transform) || !battery.TryTake(this))
                Reject(Owner, "Battery unavailable or out of reach.");
        }

        [TargetRpc]
        private void Reject(NetworkConnection connection, string message)
        {
            rejection = message;
            rejectionUntil = Time.unscaledTime + 1.5f;
        }
    }
}
