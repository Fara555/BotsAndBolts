using FishNet.Managing;
using FishNet.Object;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BotsBolts.Session
{
    public sealed class WorkshopLifetimeScope : LifetimeScope
    {
        [SerializeField, Required] private NetworkManager networkManager;
        [SerializeField, Required] private WorkshopSettings settings;
        [SerializeField, Required] private NetworkObject playerPrefab;
        [SerializeField, Required] private Transform[] spawnPoints;
        [SerializeField, Required] private SessionPanel panel;

        protected override void Configure(IContainerBuilder builder)
        {
            if (networkManager == null || settings == null || playerPrefab == null ||
                panel == null || spawnPoints == null || spawnPoints.Length != 2 ||
                spawnPoints[0] == null || spawnPoints[1] == null)
                throw new System.InvalidOperationException("Workshop requires settings, network manager, player, panel and two spawn points.");

            builder.RegisterInstance(networkManager);
            builder.RegisterInstance(settings);
            builder.RegisterEntryPoint<WorkshopSession>().AsSelf()
                .WithParameter(playerPrefab).WithParameter(spawnPoints);
            builder.RegisterComponent(panel);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            builder.RegisterComponentInHierarchy<Development.NetworkSmokeProbe>();
#endif
        }
    }
}
