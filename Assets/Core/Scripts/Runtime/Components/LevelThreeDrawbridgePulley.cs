using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    public sealed class LevelThreeDrawbridgePulley : NetworkBehaviour
    {
        public Transform hinge, launch, landing, pulley;
        readonly NetworkVariable<double> openedAt = new(-1);
        public bool Open => openedAt.Value >= 0;
        public void Restore(bool open) { if (IsServer) openedAt.Value = open ? 0 : -1; }

        void Update()
        {
            if (!IsSpawned) return;
            float t = Open ? Mathf.Clamp01((float)(NetworkManager.ServerTime.Time - openedAt.Value) / 1.2f) : 0;
            hinge.localRotation = Quaternion.Euler(Mathf.Lerp(85, 0, Mathf.SmoothStep(0, 1, t)), 0, 0);
            var actor = LevelTwoActors.Local;
            if (!actor) return;
            if (LevelOnePlayer.IsDad(actor) && actor.TryGetComponent<CoopBoostThrowAbility>(out var boost))
                boost.AssistedLandingTarget = !Open && LevelTwoActors.Near(actor, launch, 2.8f) ? landing : null;
            if (!Open && LevelTwoActors.Near(actor, pulley) && Keyboard.current?.eKey.wasPressedThisFrame == true) RequestTurn();
        }

        public void RequestTurn() { if (IsSpawned) TurnRpc(); }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void TurnRpc(RpcParams rpc = default)
        {
            var actor = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (!Open && LevelTwoActors.Near(actor, pulley) && !LevelOnePlayer.IsDad(actor))
                openedAt.Value = NetworkManager.ServerTime.Time;
        }
    }
}
