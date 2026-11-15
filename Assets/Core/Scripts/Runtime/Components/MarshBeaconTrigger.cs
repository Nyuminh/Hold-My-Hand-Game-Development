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
        private readonly NetworkVariable<bool> lit = new NetworkVariable<bool>();
        private CorePlayerManager launchedChild;
        private float launchDeadline;
        public bool Lit => lit.Value;
        public void RestoreCheckpoint(bool value) { if (IsServer) { lit.Value = value; launchedChild = null; launchDeadline = 0; } }

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
            if (!IsServer || Lit || !LevelTwoActors.Near(dad, throwPad, 2.5f) || !LevelOnePlayer.IsDad(dad) ||
                !LevelOnePlayer.IsAlive(child) || LevelOnePlayer.IsDad(child) || Vector3.Distance(dad.transform.position, child.transform.position) > 4) return false;
            launchedChild = child; launchDeadline = Time.time + 10; return true;
        }
        public bool TryCollect(CorePlayerManager child)
        {
            if (!IsServer || Lit || child == null || child != launchedChild || Time.time > launchDeadline ||
                !LevelTwoActors.Near(child, landing, 1.8f) || child.CoreMovement == null || !child.CoreMovement.IsGrounded) return false;
            lit.Value = true; IgnitedRpc(); return true;
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer && launchedChild != null) TryCollect(launchedChild);
            crystal.SetActive(!Lit); flame.SetActive(Lit); beaconLight.enabled = Lit;
            var local = LevelTwoActors.Local;
            if (local != null && local.TryGetComponent<CoopBoostThrowAbility>(out var boost) && LevelOnePlayer.IsDad(local))
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
            if (Lit || local == null || Vector3.Distance(local.transform.position, throwPad.position) > 10) return;
            GUI.Box(new Rect(20, 85, 650, 60), "Ba và Con lên bệ cạnh lá sen. Ba giữ rồi thả [F] để ném Con.\nCon đáp xuống lá sen nhặt Ngọc Ánh Sáng, thắp hải đăng.");
        }
    }
}
