using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject))]
    public abstract class LevelOneObstacle : NetworkBehaviour
    {
        public ChaseBossController boss;
        public BoxCollider paceZone;
        public Transform exit;
        public int checkpointAfter;
        protected readonly NetworkVariable<int> phase = new NetworkVariable<int>();
        public int Phase => phase.Value;
        public abstract bool Solved { get; }
        protected virtual int SolvedPhase => 2;
        protected virtual bool UsesInteract => true;
        protected abstract string Hint(CorePlayerManager player);
        public abstract bool TryInteract(CorePlayerManager player);

        public override void OnNetworkSpawn()
        {
            if (boss != null) boss.CheckpointReset += ResetForCheckpoint;
        }

        public override void OnNetworkDespawn()
        {
            if (boss != null) { boss.CheckpointReset -= ResetForCheckpoint; boss.SetObstacleActive(this, false); }
            Cleanup();
        }

        protected virtual void Cleanup() { }

        protected virtual void ResetForCheckpoint(int index)
        {
            Cleanup();
            if (IsServer) phase.Value = index >= checkpointAfter ? SolvedPhase : 0;
        }

        protected virtual void Update()
        {
            if (!IsSpawned || boss == null) return;
            if (IsServer)
            {
                bool occupied = false;
                foreach (var player in boss.Players)
                    occupied |= LevelOnePlayer.IsAlive(player) && Contains(player.transform.position);
                boss.SetObstacleActive(this, !Solved && occupied && boss.State == ChaseState.Chasing);
                if (Solved && BothBeyondExit()) boss.SetCheckpoint(checkpointAfter);
                ServerTick();
            }
            if (UsesInteract && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame && boss.State == ChaseState.Chasing)
            {
                var local = LocalPlayer();
                if (local != null && Contains(local.transform.position)) InteractRpc();
            }
            Present();
        }

        protected virtual void ServerTick() { }
        protected virtual void Present() { }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void InteractRpc(RpcParams rpcParams = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out var client) || client.PlayerObject == null) return;
            var actor = client.PlayerObject.GetComponent<CorePlayerManager>();
            if (CanInteract(actor)) TryInteract(actor);
        }

        protected bool CanInteract(CorePlayerManager actor) => IsServer && boss != null && boss.State == ChaseState.Chasing &&
            LevelOnePlayer.IsAlive(actor) && Contains(actor.transform.position);

        public bool Contains(Vector3 point)
        {
            if (paceZone == null) return false;
            Vector3 p = paceZone.transform.InverseTransformPoint(point) - paceZone.center;
            Vector3 half = paceZone.size * 0.5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.y) <= half.y && Mathf.Abs(p.z) <= half.z;
        }

        protected bool Near(CorePlayerManager actor, Transform point, float radius = 2.5f) =>
            actor != null && point != null && Vector3.Distance(actor.transform.position, point.position) <= radius;

        protected CorePlayerManager FindRole(bool dad)
        {
            foreach (var player in boss.Players)
                if (LevelOnePlayer.IsAlive(player) && LevelOnePlayer.IsDad(player) == dad) return player;
            return null;
        }

        protected CorePlayerManager LocalPlayer()
        {
            foreach (var player in boss.Players)
                if (player != null && player.IsOwner && LevelOnePlayer.IsAlive(player) && Contains(player.transform.position)) return player;
            return null;
        }

        protected bool BothBeyondExit()
        {
            if (exit == null) return false;
            var dad = FindRole(true); var child = FindRole(false);
            return dad != null && child != null && Vector3.Dot(dad.transform.position - exit.position, exit.forward) >= 0 &&
                Vector3.Dot(child.transform.position - exit.position, exit.forward) >= 0;
        }

        protected virtual void OnGUI()
        {
            if (!IsSpawned || boss == null || boss.State != ChaseState.Chasing) return;
            var local = LocalPlayer();
            if (local == null) return;
            var style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            GUI.Box(new Rect(Screen.width / 2f - 330, Screen.height - 125, 660, 70), Hint(local), style);
        }
    }
}
