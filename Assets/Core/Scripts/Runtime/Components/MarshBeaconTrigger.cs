using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(LevelTwoFeedback))]
    public sealed class MarshBeaconTrigger : NetworkBehaviour
    {
        public Transform throwPad;
        public Transform landing;
        public GameObject crystal;
        public GameObject flame;
        public Light beaconLight;
        public CoopCarryableItem requiredItem;
        public Transform itemSocket;
        public LighthouseLightBeam beam;
        public LighthouseTurnWheel turnWheel;
        private readonly NetworkVariable<bool> lit = new NetworkVariable<bool>();
        private CorePlayerManager launchedChild;
        private float launchDeadline;
        public bool Lit => lit.Value;
        public bool RequiredItemSocketed => requiredItem != null && requiredItem.IsSocketed;
        public void RestoreCheckpoint(bool value) { if (IsServer) { lit.Value = value; launchedChild = null; launchDeadline = 0; } }
        public void SetLit(bool value)
        {
            if (!IsServer || lit.Value == value) return;
            lit.Value = value;
            if (value) IgnitedRpc();
        }
        public void NotifyItemSocketed(CoopCarryableItem item)
        {
            if (!IsServer || item == null || item != requiredItem) return;
            // The socket powers the mechanism; the watermill sensor completes it.
        }

        public void NotifyLaunch(NetworkObject child)
        {
            if (IsSpawned && child != null) LaunchRpc(child);
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void LaunchRpc(NetworkObjectReference childReference, RpcParams rpc = default)
        {
            var dad = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (childReference.TryGet(out var child)) RegisterLaunch(dad, child.GetComponent<CorePlayerManager>());
        }
        public bool RegisterLaunch(CorePlayerManager dad, CorePlayerManager child)
        {
            if (!IsServer || requiredItem != null || Lit || !LevelTwoActors.Near(dad, throwPad, 2.5f) || !LevelOnePlayer.IsDad(dad) ||
                !LevelOnePlayer.IsAlive(child) || LevelOnePlayer.IsDad(child) || Vector3.Distance(dad.transform.position, child.transform.position) > 4) return false;
            launchedChild = child; launchDeadline = Time.time + 10; return true;
        }
        public bool TryCollect(CorePlayerManager child)
        {
            if (!IsServer || requiredItem != null || Lit || child == null || child != launchedChild || Time.time > launchDeadline ||
                !LevelTwoActors.Near(child, landing, 1.8f) || child.CoreMovement == null || !child.CoreMovement.IsGrounded) return false;
            lit.Value = true; IgnitedRpc(); return true;
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer && launchedChild != null) TryCollect(launchedChild);
            if (crystal != null) crystal.SetActive(!RequiredItemSocketed);
            if (flame != null) flame.SetActive(Lit || RequiredItemSocketed);
            if (beaconLight != null) beaconLight.enabled = Lit || RequiredItemSocketed;
            var local = LevelTwoActors.Local;
            if (requiredItem == null && local != null && local.TryGetComponent<CoopBoostThrowAbility>(out var boost) && LevelOnePlayer.IsDad(local))
            {
                if (!Lit && LevelTwoActors.Near(local, throwPad, 2.5f)) boost.AssistedLandingTarget = landing;
                else if (boost.AssistedLandingTarget == landing) boost.AssistedLandingTarget = null;
            }
        }
        public override void OnNetworkDespawn()
        {
            var local = LevelTwoActors.Local;
            if (local != null && local.TryGetComponent<CoopBoostThrowAbility>(out var boost) && boost.AssistedLandingTarget == landing)
                boost.AssistedLandingTarget = null;
        }
        [Rpc(SendTo.Everyone)]
        private void IgnitedRpc() => GetComponent<LevelTwoFeedback>().Play();
        private void OnGUI()
        {
            var local = LevelTwoActors.Local;
            Transform prompt = itemSocket != null ? itemSocket : throwPad;
            if (Lit || local == null || prompt == null || Vector3.Distance(local.transform.position, prompt.position) > 10) return;
            GUI.Box(new Rect(20, 85, 650, 60), requiredItem != null
                ? (!RequiredItemSocketed ? "Nhặt vật phẩm bằng [E], thả bằng [G], rồi mang tới ổ gắn của hải đăng."
                    : "Dùng vô-lăng [E], xoay A/D để căn tia sáng; [E] hoặc Space để rời vô-lăng.")
                : "Ba và Con lên bệ cạnh lá sen. Ba giữ rồi thả [F] để ném Con.");
        }
    }
}
