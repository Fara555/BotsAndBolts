using System.Collections.Generic;
using BotsBolts.Session;
using FishNet.Managing;
using FishNet.Object;
using Sirenix.OdinInspector;
using UnityEngine;

namespace BotsBolts.Interactions
{
    // Scene-local composition and shared spatial checks. No global registry or client-written state.
    public sealed class WorkshopInteractions : MonoBehaviour
    {
        [SerializeField, Required] private WorkshopSettings settings;
        [SerializeField, Required] private BatterySource source;
        [SerializeField, Required] private NetworkObject batteryPrefab;
        [SerializeField, Required] private Collider floor;
        private NetworkManager network;
        private readonly HashSet<NetworkBattery> batteries = new HashSet<NetworkBattery>();
        private readonly RaycastHit[] obstructionHits = new RaycastHit[32];
        public WorkshopSettings Settings => settings;
        public BatterySource Source => source;
        public bool IsShuttingDown { get; private set; }
        [ShowInInspector, ReadOnly] public int BatteryCount => batteries.Count;

        private void Awake() => network = GetComponent<NetworkManager>();
        private void OnApplicationQuit() => IsShuttingDown = true;
        public void Register(NetworkBattery battery) => batteries.Add(battery);
        public void Unregister(NetworkBattery battery) => batteries.Remove(battery);

        public bool CanReach(PlayerInteraction player, Vector3 point, Transform target)
        {
            Vector3 origin = player.transform.position + Vector3.up * 0.7f;
            Vector3 offset = point - origin;
            if (offset.sqrMagnitude > settings.InteractionRange * settings.InteractionRange) return false;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, obstructionHits, offset.magnitude,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (count == obstructionHits.Length) return false;
            for (int i = 0; i < count; i++)
            {
                Transform hit = obstructionHits[i].transform;
                if (hit.IsChildOf(player.transform) || hit.IsChildOf(target)) continue;
                return false;
            }
            return true;
        }

        public bool TrySupply(PlayerInteraction player)
        {
            if (!network.ServerManager.Started || player.HeldBattery != null ||
                batteries.Count >= settings.BatteryLimit || !CanReach(player, source.InteractionPoint, source.transform)) return false;
            NetworkObject instance = Instantiate(batteryPrefab, player.CarryPoint.position, Quaternion.identity);
            var battery = instance.GetComponent<NetworkBattery>();
            battery.InitializeRestingPosition(player.transform.position);
            network.ServerManager.Spawn(instance);
            return battery.TryTake(player);
        }

        public bool TryFindDrop(PlayerInteraction player, out Vector3 position)
        {
            position = default;
            if (floor == null || network == null || IsShuttingDown) return false;
            Vector3 origin = player.transform.position;
            // Search nearby floor positions, without accepting any position sent by a client.
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = Quaternion.Euler(0, i * 45, 0) * player.transform.forward * 0.85f;
                Vector3 candidate = origin + offset;
                Ray ray = new Ray(candidate + Vector3.up * 3, Vector3.down);
                if (!floor.Raycast(ray, out RaycastHit ground, 7)) continue;
                candidate = ground.point;
                if (!CanReach(player, candidate + Vector3.up * 0.25f, floor.transform)) continue;
                bool playerOccupied = false;
                foreach (NetworkObject spawned in network.ServerManager.Objects.Spawned.Values)
                {
                    // Remote CharacterControllers are disabled on the server; test their observed position explicitly.
                    if (spawned == player.NetworkObject || spawned.GetComponent<PlayerInteraction>() == null) continue;
                    Vector3 separation = spawned.transform.position - candidate;
                    if (Mathf.Abs(separation.y) < 1.5f && new Vector2(separation.x, separation.z).sqrMagnitude < .65f * .65f)
                    { playerOccupied = true; break; }
                }
                if (playerOccupied) continue;
                if (Physics.CheckBox(candidate + Vector3.up * 0.26f, new Vector3(0.26f, 0.23f, 0.2f),
                    Quaternion.identity, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                position = candidate;
                return true;
            }
            position = default;
            return false;
        }

        public Vector3 DisconnectDrop(PlayerInteraction player)
        {
            if (TryFindDrop(player, out Vector3 position)) return position;
            if (floor == null) return player.transform.position;
            // A disconnected character is about to disappear. Clamp its final position to the workshop floor.
            Bounds bounds = floor.bounds;
            Vector3 at = player.transform.position;
            return new Vector3(Mathf.Clamp(at.x, bounds.min.x + .5f, bounds.max.x - .5f), bounds.max.y,
                Mathf.Clamp(at.z, bounds.min.z + .5f, bounds.max.z - .5f));
        }
    }
}
