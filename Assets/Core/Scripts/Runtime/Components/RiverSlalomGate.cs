using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(BoxCollider), typeof(LevelTwoFeedback))]
    public sealed class RiverSlalomGate : NetworkBehaviour
    {
        public CoopWoodenBoat boat;
        public RiverSlalomGate previous;
        public int gateNumber = 1;
        private readonly NetworkVariable<bool> passed = new NetworkVariable<bool>();
        private bool localPassed;
        private Vector3 lastPosition;
        private bool sampled;
        public void RestoreCheckpoint(bool value) { if (IsServer) { passed.Value = value; sampled = false; } }
        public bool Passed => IsSpawned ? passed.Value : localPassed;

        private void FixedUpdate()
        {
            if (boat == null || (IsSpawned && !IsServer)) return;
            var now = boat.transform.position;
            if (sampled) TryCross(lastPosition, now);
            lastPosition = now;
            sampled = true;
        }

        public bool TryCross(Vector3 from, Vector3 to)
        {
            if ((IsSpawned && !IsServer) || Passed || boat == null || !boat.HasCoopCrew || (previous != null && !previous.Passed)) return false;
            // Respawn/teleport must not award gates skipped along the relocation segment.
            if ((to - from).sqrMagnitude > 100f) return false;
            var a = transform.InverseTransformPoint(from);
            var b = transform.InverseTransformPoint(to);
            if (a.z >= 0f || b.z < 0f) return false;
            var crossing = Vector3.Lerp(a, b, -a.z / (b.z - a.z));
            var box = GetComponent<BoxCollider>();
            // Keep the entire 1.7 m hull between the buoys, rather than just its centre.
            float margin = 0.9f / Mathf.Abs(transform.lossyScale.x);
            if (Mathf.Abs(crossing.x - box.center.x) > box.size.x * 0.5f - margin ||
                Mathf.Abs(crossing.y - box.center.y) > box.size.y * 0.5f) return false;
            if (IsSpawned) { passed.Value = true; PassedRpc(); }
            else { localPassed = true; GetComponent<LevelTwoFeedback>().Play(); }
            return true;
        }

        [Rpc(SendTo.Everyone)]
        private void PassedRpc() => GetComponent<LevelTwoFeedback>().Play();

        private void OnGUI()
        {
            if (Passed || (previous != null && !previous.Passed) || boat == null || !boat.HasCoopCrew) return;
            GUI.Box(new Rect(20, 20, 590, 58), "Cổng phao " + gateNumber + "/3\nBa: nhấn Space để chèo. Con: A/D bẻ cần lái ngược hướng rẽ.");
        }
    }
}
