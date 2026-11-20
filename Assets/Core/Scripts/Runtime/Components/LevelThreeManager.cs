using System.Linq;
using UnityEngine.UI;
using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class LevelThreeManager : NetworkBehaviour
    {
        public CoopTetherChain tether;
        public LevelThreeDrawbridgePulley bridge;
        public LevelThreeCryptCrawlGate gate;
        public CollapsingPlatform[] pillars;
        public DualAltarPortal altar;
        public Transform[] checkpoints;
        public Text objective;
        readonly NetworkVariable<int> progress = new(-1);
        bool ready;
        float recoverAfter;
        int start;
        public int Checkpoint => progress.Value;
        static readonly string[] Objectives = {
            "Ba giữ F để ném Con qua vực. Con nhấn E ở tời để hạ cầu.",
            "Con chui qua khe bên phải rồi nhấn E ở cần gạt để mở cổng cho Ba.",
            "Cùng vượt năm cọc đá. Mỗi cọc sụp sau 1,5 giây.",
            "Né bánh răng và con lắc. Ba đứng bệ trái, Con đứng bệ phải.",
            "Hai cha con đã mở Cổng Ánh Sáng. Hoàn thành Màn 3!"
        };
        public override void OnNetworkSpawn()
        {
            progress.OnValueChanged += Present;
            if (IsServer) { start = Mathf.Min(3, CampaignProgress.ConsumeStart(3)); tether.CrewFell += Recover; }
            Present(-1, progress.Value);
        }
        public override void OnNetworkDespawn()
        {
            progress.OnValueChanged -= Present;
            tether.CrewFell -= Recover;
        }
        void Present(int oldValue, int value)
        {
            objective.text = value < 0 ? "Đang chờ đủ Ba và Con…" : Objectives[value];
            if (value < 0) return;
            if (value == 4) PlayerPrefs.SetInt("HMH.Level3.Completed", 1);
            CampaignProgress.Save(3, value);
        }
        void Update()
        {
            if (!IsServer || !IsSpawned) return;
            if (!ready)
            {
                if (!LevelTwoActors.All.Any(LevelOnePlayer.IsDad) || !LevelTwoActors.All.Any(p => !LevelOnePlayer.IsDad(p))) return;
                ready = true;
                Restore(start);
                return;
            }
            if (Time.time < recoverAfter) return;
            switch (progress.Value)
            {
                case 0: if (bridge.Open && BothBeyond(15)) progress.Value = 1; break;
                case 1: if (gate.Open && BothBeyond(36)) progress.Value = 2; break;
                case 2: if (BothBeyond(64)) { progress.Value = 3; altar.Armed = true; } break;
                case 3: if (altar.Lit) progress.Value = 4; break;
            }
        }
        static bool BothBeyond(float z)
        {
            bool dad = false, child = false;
            foreach (var actor in LevelTwoActors.All)
                if (actor.transform.position.z >= z && actor.transform.position.y >= -.25f)
                {
                    if (LevelOnePlayer.IsDad(actor)) dad = true; else child = true;
                }
            return dad && child;
        }
        void Recover() { if (ready && Time.time >= recoverAfter) Restore(progress.Value); }
        public void RequestRestart(bool fromBeginning) => RestartRpc(fromBeginning);
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void RestartRpc(bool fromBeginning, RpcParams rpc = default)
        {
            if (!ready || Time.time < recoverAfter || !LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId)) return;
            Restore(fromBeginning ? 0 : Mathf.Min(progress.Value, 3));
        }
        void Restore(int index)
        {
            recoverAfter = Time.time + 1;
            progress.Value = index;
            tether.ResetForCheckpoint();
            bridge.Restore(index >= 1);
            gate.Restore(index >= 2);
            foreach (var platform in pillars) platform.Restore();
            altar.Armed = index >= 3;
            altar.Restore(index == 4);
            PlaceCrewRpc(index);
        }
        [Rpc(SendTo.Everyone)]
        void PlaceCrewRpc(int index)
        {
            foreach (var actor in LevelTwoActors.All)
            {
                if (!actor.IsOwner) continue;
                LevelOnePlayer.ReleaseCoop(actor);
                LevelOnePlayer.Respawn(actor, checkpoints[index * 2 + (LevelOnePlayer.IsDad(actor) ? 0 : 1)]);
            }
        }
    }
}
