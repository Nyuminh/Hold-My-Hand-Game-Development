using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    public sealed class LevelThreeCryptCrawlGate : NetworkBehaviour
    {
        public Transform gate, lever;
        public BoxCollider crawl;
        readonly NetworkVariable<bool> open = new();
        ulong crawler = ulong.MaxValue;
        Vector3 closed;
        public bool Open => open.Value;
        void Awake() => closed = gate.localPosition;
        public void Restore(bool value) { if (IsServer) { open.Value = value; crawler = ulong.MaxValue; } }

        void Update()
        {
            if (!IsSpawned) return;
            gate.localPosition = Vector3.MoveTowards(gate.localPosition, closed + Vector3.up * (Open ? 4.5f : 0), 4 * Time.deltaTime);
            if (Open) return;
            if (IsServer)
                foreach (var actor in LevelTwoActors.All)
                    if (!LevelOnePlayer.IsDad(actor) && crawl.bounds.Contains(actor.transform.position + Vector3.up * .4f)) crawler = actor.NetworkObjectId;
            if (LevelTwoActors.Near(LevelTwoActors.Local, lever) && Keyboard.current?.eKey.wasPressedThisFrame == true) RequestUnlock();
        }

        public void RequestUnlock() { if (IsSpawned) UnlockRpc(); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void UnlockRpc(RpcParams rpc = default)
        {
            var actor = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (LevelTwoActors.Near(actor, lever) && !LevelOnePlayer.IsDad(actor) && crawler == actor.NetworkObjectId) open.Value = true;
        }
    }
}
