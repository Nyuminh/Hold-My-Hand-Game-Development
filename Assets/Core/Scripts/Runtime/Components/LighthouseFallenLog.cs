using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LighthouseFallenLog : NetworkBehaviour
    {
        public Transform trunk;
        public Transform dadGrip;
        public CoopCrawlVolume crawl;
        public float liftHeight = 1.1f;
        private readonly NetworkVariable<ulong> liftingDad = new(ulong.MaxValue);
        private Vector3 closedPosition;
        public bool IsLifted => liftingDad.Value != ulong.MaxValue;

        private void Awake() { if (trunk != null) closedPosition = trunk.localPosition; }

        private void Update()
        {
            if (!IsSpawned) return;
            var local = LevelTwoActors.Local;
            if (local != null && LevelOnePlayer.IsDad(local) && LevelTwoActors.Near(local, dadGrip, 2.5f) &&
                Keyboard.current?.eKey.wasPressedThisFrame == true) ToggleLiftRpc();

            if (IsServer && liftingDad.Value != ulong.MaxValue)
            {
                var dad = LevelTwoActors.Sender(NetworkManager, liftingDad.Value);
                if (!LevelTwoActors.Near(dad, dadGrip, 3f)) liftingDad.Value = ulong.MaxValue;
            }
            if (trunk != null)
                trunk.localPosition = Vector3.MoveTowards(trunk.localPosition,
                    closedPosition + Vector3.up * (IsLifted ? liftHeight : 0), Time.deltaTime * 2.5f);
            if (crawl != null) crawl.gameObject.SetActive(IsLifted);
        }

        public bool TryLift(CorePlayerManager dad)
        {
            if (!IsServer || !LevelOnePlayer.IsDad(dad) || !LevelTwoActors.Near(dad, dadGrip, 2.5f)) return false;
            liftingDad.Value = dad.OwnerClientId;
            return true;
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ToggleLiftRpc(RpcParams rpc = default)
        {
            var dad = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (!LevelOnePlayer.IsDad(dad) || !LevelTwoActors.Near(dad, dadGrip, 2.5f)) return;
            liftingDad.Value = liftingDad.Value == rpc.Receive.SenderClientId ? ulong.MaxValue : rpc.Receive.SenderClientId;
        }

        private void OnGUI()
        {
            var local = LevelTwoActors.Local;
            if (!IsSpawned || local == null || dadGrip == null) return;

            if (LevelOnePlayer.IsDad(local) && LevelTwoActors.Near(local, dadGrip, 2.5f))
                LighthouseHud.DrawWorldHint(dadGrip.position, IsLifted ? "[E] Hạ thân cây" : "[E] Nâng thân cây cổ thụ");
            else if (!LevelOnePlayer.IsDad(local) && IsLifted && trunk != null &&
                     Vector3.Distance(local.transform.position, trunk.position) <= 4f)
                LighthouseHud.DrawWorldHint(trunk.position, "Ba đang nâng cây! Hãy chui vào trong");
        }
    }
}
