using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    public static class LevelTwoActors
    {
        public static IEnumerable<CorePlayerManager> All
        {
            get
            {
                var network = NetworkManager.Singleton;
                if (network == null || !network.IsListening) yield break;
                foreach (var obj in network.SpawnManager.SpawnedObjectsList)
                    if (obj != null && obj.TryGetComponent<CorePlayerManager>(out var actor) && LevelOnePlayer.IsAlive(actor)) yield return actor;
            }
        }
        public static bool OnFoot(CorePlayerManager actor) => LevelOnePlayer.IsAlive(actor) &&
            actor.TryGetComponent<CharacterController>(out var controller) && controller.enabled;
        public static bool Near(CorePlayerManager actor, Transform point, float radius = 2f) =>
            OnFoot(actor) && point != null && Vector3.Distance(actor.transform.position, point.position) < radius;
        public static CorePlayerManager Sender(NetworkManager network, ulong clientId) =>
            network.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null
                ? client.PlayerObject.GetComponent<CorePlayerManager>() : null;
        public static CorePlayerManager Local => NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null
            ? NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<CorePlayerManager>() : null;
    }
}
