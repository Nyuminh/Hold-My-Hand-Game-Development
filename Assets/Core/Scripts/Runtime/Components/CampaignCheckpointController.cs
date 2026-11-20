using System.Collections;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class CampaignCheckpointController : NetworkBehaviour
    {
        public int level = 2;
        private readonly NetworkVariable<int> checkpoint = new NetworkVariable<int>();
        private readonly NetworkVariable<int> boatHealth = new NetworkVariable<int>(100);
        private CoopWoodenBoat boat;
        private ChaseBossController boss;
        private SluiceGateObstacle sluice;
        private GiantWatermillObstacle mill;
        private LevelTwoClimaxManager climax;
        private RiverSlalomGate[] gates;
        private MarshBeaconTrigger[] beacons;
        private bool ready, restoring;
        private float nextRestart, nextDamage;
        public int Checkpoint => checkpoint.Value;
        public int BoatHealth => boatHealth.Value;
        public bool Ready => ready && !restoring;
        public override void OnNetworkSpawn()
        {
            boat = FindAnyObjectByType<CoopWoodenBoat>();
            boss = FindAnyObjectByType<ChaseBossController>();
            sluice = FindAnyObjectByType<SluiceGateObstacle>();
            mill = FindAnyObjectByType<GiantWatermillObstacle>();
            climax = FindAnyObjectByType<LevelTwoClimaxManager>();
            gates = FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None);
            beacons = FindObjectsByType<MarshBeaconTrigger>(FindObjectsSortMode.None);
            checkpoint.OnValueChanged += SaveCheckpoint;
            if (IsServer) StartCoroutine(StartAtCheckpoint());
            else SaveCheckpoint(0, checkpoint.Value);
        }
        public override void OnNetworkDespawn() { checkpoint.OnValueChanged -= SaveCheckpoint; }
        private void SaveCheckpoint(int oldValue, int value) => CampaignProgress.Save(level, value);
        private IEnumerator StartAtCheckpoint()
        {
            int start = CampaignProgress.ConsumeStart(level);
            // A durable completion record is not a live victory in a new attempt.
            if (level == 2 && start >= 6) start = 5;
            // Let GameManager finish assigning cameras/spawn points before relocating the crew.
            while (!HasBothRoles()) yield return null;
            yield return new WaitForSeconds(1f);
            if (level == 1)
            {
                if (start > 0) Restore(start);
                else { checkpoint.Value = 0; SaveCheckpoint(0, 0); }
            }
            else
            {
                if (start > 0) Restore(start);
                else Restore(0);
            }
            ready = true;
        }
        private bool HasBothRoles() => LevelTwoActors.All.Any(LevelOnePlayer.IsDad) &&
            LevelTwoActors.All.Any(p => !LevelOnePlayer.IsDad(p));
        private void Update()
        {
            if (!IsServer || !Ready) return;
            int next = checkpoint.Value;
            if (level == 1 && boss != null) next = boss.CheckpointIndex;
            if (level == 2)
            {
                if (next == 0 && gates.Length > 0 && gates.All(g => g.Passed)) next = 1;
                if (next == 1 && sluice.Open) next = 2;
                if (next == 2 && boat.HasCoopCrew && boat.transform.position.z >= 145f && boat.transform.position.z < 160f) next = 3;
                if (next == 3 && beacons.Length == 2 && beacons.All(b => b.Lit)) next = 4;
                if (next == 4 && mill.Passed) next = 5;
                // A confirmed altar success is terminal even if a previous stage's
                // positional checkpoint was missed. Never overwrite its saved 6.
                if (climax.Completed) next = 6;
            }
            if (next > checkpoint.Value) { checkpoint.Value = next; boatHealth.Value = 100; }
        }
        public void RequestRestart(bool fromBeginning) { if (IsSpawned) RestartRpc(fromBeginning); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RestartRpc(bool fromBeginning, RpcParams rpc = default)
        {
            if (!NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var client) || client.PlayerObject == null) return;
            if (!ready || restoring || Time.unscaledTime < nextRestart) return;
            nextRestart = Time.unscaledTime + 3f;
            Restore(fromBeginning ? 0 : checkpoint.Value);
        }
        public void BoatImpact(float speed)
        {
            if (!IsServer || level != 2 || !Ready || !boat.HasCoopCrew || Time.time < nextDamage || speed < 0.35f) return;
            nextDamage = Time.time + 1f;
            boatHealth.Value = Mathf.Max(0, boatHealth.Value - Mathf.Clamp(Mathf.CeilToInt(speed * 6), 15, 40));
            if (boatHealth.Value == 0) StartCoroutine(RecoverWreck());
        }
        private IEnumerator RecoverWreck()
        {
            restoring = true;
            yield return new WaitForSeconds(0.8f);
            Restore(checkpoint.Value);
        }
        private void Restore(int index)
        {
            restoring = true;
            checkpoint.Value = CampaignProgress.Clamp(level, index);
            // Also save when replay selects the already-current checkpoint.
            SaveProgressRpc(checkpoint.Value);
            if (level == 1) { boss.RestoreCampaignCheckpoint(checkpoint.Value); restoring = false; return; }
            foreach (var gate in gates) gate.RestoreCheckpoint(index >= 1);
            sluice.RestoreCheckpoint(index >= 2);
            foreach (var beacon in beacons) beacon.RestoreCheckpoint(index >= 4);
            mill.RestoreCheckpoint(index >= 5);
            // Restoring/replaying the final checkpoint must re-arm the altar.
            climax.RestoreCheckpoint(index >= 5, false);
            boatHealth.Value = 100;
            nextDamage = Time.time + 3f;
            RestoreCrewRpc(index);
            StartCoroutine(FinishRestore(index));
        }
        [Rpc(SendTo.Everyone)]
        private void SaveProgressRpc(int index) => CampaignProgress.Save(level, index);
        public static Vector3 BoatPosition(int index) => index <= 0 ? new Vector3(97.2f, .53f, 34.2f) :
            index == 1 ? new Vector3(104f, .53f, 69f) : index == 2 ? new Vector3(100f, .53f, 85f) :
            index == 3 ? new Vector3(100f, .53f, 148f) : index == 4 ? new Vector3(100f, .53f, 211f) : new Vector3(100f, .53f, 242f);
        [Rpc(SendTo.Everyone)]
        private void RestoreCrewRpc(int index)
        {
            InGameEscapeMenuManager.Instance?.CloseMenu();
            boat.ResetForCheckpoint(BoatPosition(index));
            foreach (var actor in LevelTwoActors.All)
            {
                if (!actor.IsOwner) continue;
                LevelOnePlayer.ReleaseCoop(actor);
                bool dad = LevelOnePlayer.IsDad(actor);
                Vector3 point = index == 0 ? new Vector3(94, 1.15f, dad ? 30 : 32) :
                    index == 1 ? new Vector3(dad ? 108 : 109.5f, 1.15f, dad ? 70 : 71) :
                    index >= 6 ? new Vector3(dad ? 99 : 101, 1.25f, 253) : BoatPosition(index) + Vector3.up;
                actor.CoreMovement.ResetMovementForces();
                actor.CoreMovement.SetPosition(point);
                actor.CoreStats.ModifyStat(StatKeys.Health, actor.CoreStats.GetMaxValue(StatKeys.Health));
                actor.PlayerState.SetLifeState(PlayerLifeState.Respawned);
                if (actor.TryGetComponent<CoopBoostThrowAbility>(out var boost)) boost.enabled = true;
            }
        }
        private IEnumerator FinishRestore(int index)
        {
            yield return null;
            yield return null;
            if (index >= 2 && index < 6) boat.MountCrewAtCheckpoint();
            restoring = false;
        }
        private void OnGUI()
        {
            if (!IsSpawned || level != 2) return;
            GUI.Box(new Rect(Screen.width - 310, 20, 290, 66),
                "Độ bền thuyền: " + BoatHealth + "/100\n" + CampaignProgress.Description(level, Checkpoint) +
                (BoatHealth == 0 ? " · Đang cứu hộ..." : " · ESC: chơi lại"));
        }
    }
}
