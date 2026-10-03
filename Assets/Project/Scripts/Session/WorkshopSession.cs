using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using VContainer.Unity;

namespace BotsBolts.Session
{
    public enum SessionStatus { Offline, Connecting, Connected, Disconnecting }

    // A scene-scoped service owns transport subscriptions and server-side player admission.
    public sealed class WorkshopSession : IStartable, IDisposable
    {
        private readonly NetworkManager network;
        private readonly WorkshopSettings settings;
        private readonly NetworkObject playerPrefab;
        private readonly Transform[] spawnPoints;
        private readonly Dictionary<int, int> playerSlots = new Dictionary<int, int>(2);
        private CancellationTokenSource connectCancellation;
        private bool intentionalDisconnect;
        private bool startLocalClient;
        private bool subscribed;

        public SessionStatus Status { get; private set; }
        public string Message { get; private set; } = "Create a workshop or connect to a host.";
        public bool IsHost => network.ServerManager.Started;
        public event Action Changed;

        public WorkshopSession(NetworkManager network, WorkshopSettings settings,
            NetworkObject playerPrefab, Transform[] spawnPoints)
        {
            this.network = network;
            this.settings = settings;
            this.playerPrefab = playerPrefab;
            this.spawnPoints = spawnPoints;
        }

        public void Start()
        {
            if (subscribed) return;
            network.ClientManager.OnClientConnectionState += OnClientState;
            network.ServerManager.OnServerConnectionState += OnServerState;
            network.ServerManager.OnRemoteConnectionState += OnRemoteState;
            network.SceneManager.OnClientLoadedStartScenes += OnLoadedScenes;
            network.TransportManager.Transport.SetPort(settings.Port);
            network.TransportManager.Transport.SetMaximumClients(2);
            network.TimeManager.SetTickRate(30);
            subscribed = true;
        }

        public void Host()
        {
            if (Status != SessionStatus.Offline) return;
            BeginConnection();
            startLocalClient = true;
            network.TransportManager.Transport.SetClientAddress("127.0.0.1");
            if (!network.ServerManager.StartConnection()) FailConnection("Could not start the host. Check the port.");
        }

        public void Join(string address)
        {
            if (Status != SessionStatus.Offline) return;
            if (string.IsNullOrWhiteSpace(address))
            {
                SetStatus(SessionStatus.Offline, "Enter the host address.");
                return;
            }
            BeginConnection();
            network.TransportManager.Transport.SetClientAddress(address.Trim());
            if (!network.ClientManager.StartConnection()) FailConnection("Could not connect to the host.");
        }

        public void Leave()
        {
            if (Status == SessionStatus.Offline) return;
            intentionalDisconnect = true;
            startLocalClient = false;
            CancelConnectionTimeout();
            SetStatus(SessionStatus.Disconnecting, "Disconnecting…");
            StopTransport();
        }

        private void BeginConnection()
        {
            intentionalDisconnect = false;
            CancelConnectionTimeout();
            connectCancellation = new CancellationTokenSource();
            SetStatus(SessionStatus.Connecting, "Connecting…");
            WaitForConnection(connectCancellation.Token).Forget(Debug.LogException);
        }

        private async UniTask WaitForConnection(CancellationToken token)
        {
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(settings.ConnectionTimeout),
                ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow();
            if (!cancelled && Status == SessionStatus.Connecting)
                FailConnection("Connection timed out. Check the host address, port and available player slot.");
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started)
            {
                CancelConnectionTimeout();
                SetStatus(SessionStatus.Connected, IsHost ? "Workshop hosted — waiting for a partner." : "Connected to the workshop.");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                CancelConnectionTimeout();
                startLocalClient = false;
                if (network.ServerManager.Started) network.ServerManager.StopConnection(true);
                string message = intentionalDisconnect ? "Disconnected. You can create or join another workshop." :
                    "Connection lost or refused. The host may have left or the workshop may be full.";
                SetStatus(SessionStatus.Offline, message);
            }
        }

        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Started && startLocalClient)
            {
                startLocalClient = false;
                if (!network.ClientManager.StartConnection()) FailConnection("The host started, but its local player could not connect.");
            }
            else if (args.ConnectionState == LocalConnectionState.Stopped)
            {
                playerSlots.Clear();
                startLocalClient = false;
                if (Status != SessionStatus.Offline)
                {
                    CancelConnectionTimeout();
                    network.ClientManager.StopConnection();
                    SetStatus(SessionStatus.Offline, intentionalDisconnect ? "Workshop closed." : "The host stopped. Check the port and try again.");
                }
            }
        }

        private void OnLoadedScenes(NetworkConnection connection, bool asServer)
        {
            if (!asServer || playerSlots.ContainsKey(connection.ClientId)) return;
            int slot = FindFreeSlot();
            if (slot < 0)
            {
                connection.Disconnect(true);
                return;
            }
            playerSlots.Add(connection.ClientId, slot);
            Transform spawn = spawnPoints[slot];
            NetworkObject player = UnityEngine.Object.Instantiate(playerPrefab, spawn.position, spawn.rotation);
            player.GetComponent<Players.NetworkPlayer>().ConfigureSlot(slot);
            network.ServerManager.Spawn(player, connection);
            network.SceneManager.AddOwnerToDefaultScene(player);
            Debug.Log($"[BotsBolts] Spawned player {connection.ClientId} in slot {slot}.");
        }

        private int FindFreeSlot()
        {
            for (int slot = 0; slot < 2; slot++)
                if (!playerSlots.ContainsValue(slot)) return slot;
            return -1;
        }

        private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                playerSlots.Remove(connection.ClientId);
                // FishNet despawns owned objects immediately after this callback.
                Debug.Log($"[BotsBolts] Released player {connection.ClientId}.");
            }
        }

        private void FailConnection(string reason)
        {
            intentionalDisconnect = true;
            startLocalClient = false;
            CancelConnectionTimeout();
            StopTransport();
            SetStatus(SessionStatus.Offline, reason);
        }

        private void StopTransport()
        {
            network.ClientManager.StopConnection();
            network.ServerManager.StopConnection(true);
        }

        private void SetStatus(SessionStatus status, string message)
        {
            Status = status;
            Message = message;
            Changed?.Invoke();
        }

        private void CancelConnectionTimeout()
        {
            if (connectCancellation == null) return;
            connectCancellation.Cancel();
            connectCancellation.Dispose();
            connectCancellation = null;
        }

        public void Dispose()
        {
            CancelConnectionTimeout();
            if (network != null && subscribed)
            {
                network.ClientManager.OnClientConnectionState -= OnClientState;
                network.ServerManager.OnServerConnectionState -= OnServerState;
                network.ServerManager.OnRemoteConnectionState -= OnRemoteState;
                network.SceneManager.OnClientLoadedStartScenes -= OnLoadedScenes;
                StopTransport();
            }
            subscribed = false;
            playerSlots.Clear();
            Changed = null;
        }
    }
}
