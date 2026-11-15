using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class GiantWatermillObstacle : NetworkBehaviour
    {
        public Transform wheel;
        public Transform safetyBoom;
        public CoopWoodenBoat boat;
        public MarshBeaconTrigger west, east;
        public float cycleSeconds = 8f;
        public float openingSeconds = 2.5f;
        private readonly NetworkVariable<double> epoch = new NetworkVariable<double>();
        private readonly NetworkVariable<bool> passed = new NetworkVariable<bool>();
        private Vector3 boomClosed;
        private Vector3 lastBoat;
        private bool sampled;
        public bool Passed => passed.Value;
        public bool BeaconsLit => west != null && east != null && west.Lit && east.Lit;
        public double CycleTime => IsSpawned ? (NetworkManager.ServerTime.Time - epoch.Value) % cycleSeconds : Time.time % cycleSeconds;
        public bool IsOpenWindow => BeaconsLit && CycleTime >= cycleSeconds - openingSeconds;
        public override void OnNetworkSpawn() { if (IsServer) epoch.Value = NetworkManager.ServerTime.Time; boomClosed = safetyBoom.position; }
        private void FixedUpdate()
        {
            if (!IsSpawned) return;
            float turning = Mathf.Clamp01((float)CycleTime / (cycleSeconds - openingSeconds));
            // Four paddles turn for 5.5 s, then dwell diagonally clear of the hull for 2.5 s.
            // Server time gives late joiners and both peers the same angular phase.
            wheel.localRotation = Quaternion.Euler(45f + 360f * turning, 0, 0);
            safetyBoom.position = boomClosed + Vector3.up * (IsOpenWindow ? 4f : 0f);
            if (!IsServer || boat == null) return;
            var position = boat.transform.position;
            if (IsOpenWindow && boat.HasCoopCrew && boat.HasRecentStroke && boat.CurrentSpeed >= 6 &&
                position.z > transform.position.z - 10 && position.z < transform.position.z &&
                Vector3.Dot(boat.GetBoatWorldForward(), Vector3.forward) > 0.96f) boat.BeginRiverDash();
            if (sampled) TryPass(lastBoat, position);
            lastBoat = position; sampled = true;
        }
        public bool TryPass(Vector3 from, Vector3 to)
        {
            if (!IsServer || Passed || !IsOpenWindow || boat == null || !boat.HasCoopCrew || !boat.HasRecentStroke ||
                boat.CurrentSpeed < 5 || Vector3.Dot(boat.GetBoatWorldForward(), Vector3.forward) < 0.96f ||
                (to - from).sqrMagnitude > 100 || from.z >= transform.position.z + 2 || to.z < transform.position.z + 2 ||
                Mathf.Abs(to.x - transform.position.x) > 6) return false;
            passed.Value = true; return true;
        }
        private void OnGUI()
        {
            if (boat == null || Mathf.Abs(boat.transform.position.z - transform.position.z) > 18) return;
            GUI.Box(new Rect(20, 85, 650, 60), !BeaconsLit ? "Thắp sáng cả hai hải đăng để mở đường qua cối xay." :
                IsOpenWindow ? "CỬA MỞ! Con giữ thẳng mũi thuyền. Ba nhấn Space liên tục để nước rút!" :
                "Chờ cối xay dừng ở góc chéo. Chu kỳ 8 giây, khoảng trống 2,5 giây.");
        }
    }
}
