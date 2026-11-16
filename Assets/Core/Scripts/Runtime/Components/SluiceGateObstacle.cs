using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(LevelTwoFeedback))]
    public sealed class SluiceGateObstacle : NetworkBehaviour
    {
        public Transform gate;
        public Transform winch;
        public Transform valve;
        public BoxCollider crawl;
        public Transform crawlDoor;
        public RiverSlalomGate lastSlalom;
        public float fullLift = 5f;
        private readonly NetworkVariable<int> phase = new NetworkVariable<int>();
        private readonly NetworkVariable<float> lift = new NetworkVariable<float>();
        private readonly NetworkVariable<NetworkObjectReference> holder = new NetworkVariable<NetworkObjectReference>();
        private Vector3 closedPosition, crawlClosedPosition;
        private float heartbeat, nextSend;
        private CorePlayerManager dad, enteredChild, crossedChild;
        private LevelOneBracePose brace;
        public int Phase => phase.Value;
        public float Lift => lift.Value;
        public bool Open => Phase == 2;
        public bool ChildTraversed => crossedChild != null;
        public void RestoreCheckpoint(bool opened)
        {
            if (!IsServer) return;
            phase.Value = opened ? 2 : 0; lift.Value = opened ? fullLift : 0;
            holder.Value = default; dad = enteredChild = crossedChild = null;
            gate.localPosition = closedPosition + Vector3.up * lift.Value;
            crawlDoor.localPosition = crawlClosedPosition + Vector3.up * (opened ? 1.5f : 0f);
        }

        private void Awake() { if (gate != null) closedPosition = gate.localPosition; if (crawlDoor != null) crawlClosedPosition = crawlDoor.localPosition; }
        public override void OnNetworkSpawn() { closedPosition = gate.localPosition; crawlClosedPosition = crawlDoor.localPosition; }
        public bool SetWinchHeld(CorePlayerManager actor, bool held)
        {
            if (!IsServer || Open || !LevelOnePlayer.IsAlive(actor) || !LevelOnePlayer.IsDad(actor)) return false;
            if (!held) { if (dad == actor) Release(); return true; }
            if (!LevelTwoActors.Near(actor, winch, 2.4f) || (lastSlalom != null && !lastSlalom.Passed)) return false;
            dad = actor; heartbeat = Time.time; phase.Value = 1; holder.Value = actor.NetworkObject;
            return true;
        }
        public bool TryOpenValve(CorePlayerManager actor)
        {
            if (!IsServer || Phase != 1 || !LevelTwoActors.Near(actor, valve) || LevelOnePlayer.IsDad(actor) || actor != crossedChild) return false;
            phase.Value = 2; holder.Value = default; dad = null; OpenedRpc(); return true;
        }
        private void Release() { if (!Open) phase.Value = 0; dad = null; holder.Value = default; }

        public void ObserveCrawl(CorePlayerManager actor)
        {
            if (!IsServer || Phase != 1 || lift.Value < 0.45f || !LevelTwoActors.OnFoot(actor) || LevelOnePlayer.IsDad(actor)) return;
            Vector3 p = crawl.transform.InverseTransformPoint(actor.transform.position);
            if (crawl.bounds.Contains(actor.transform.position + Vector3.up * 0.2f) && p.z < 0) enteredChild = actor;
            if (enteredChild == actor && p.z > 2.3f && Mathf.Abs(p.x) < 0.75f) crossedChild = actor;
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                if (Phase == 1 && (Time.time - heartbeat > 0.5f || !LevelTwoActors.Near(dad, winch, 2.4f))) Release();
                bool occupied = false;
                foreach (var actor in LevelTwoActors.All)
                {
                    ObserveCrawl(actor);
                    occupied |= LevelTwoActors.OnFoot(actor) && crawl.bounds.Contains(actor.transform.position + Vector3.up * 0.2f);
                }
                // Never crush a child on a lost heartbeat; opening still requires Dad to return.
                float target = Open ? fullLift : Phase == 1 || occupied ? 0.5f : 0;
                lift.Value = Mathf.MoveTowards(lift.Value, target, Time.deltaTime * 2f);
                if (Phase == 0 && !occupied) { enteredChild = null; crossedChild = null; }
            }
            gate.localPosition = closedPosition + Vector3.up * lift.Value;
            crawlDoor.localPosition = crawlClosedPosition + Vector3.up * Mathf.Min(lift.Value * 2.4f, 1.5f);
            PresentBrace();
            var local = LevelTwoActors.Local;
            if (local == null || Keyboard.current == null) return;
            if (LevelOnePlayer.IsDad(local) && Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + 0.12f;
                if (LevelTwoActors.Near(local, winch, 2.4f) || Phase == 1) WinchRpc(Keyboard.current.eKey.isPressed);
            }
            else if (!LevelOnePlayer.IsDad(local) && Keyboard.current.eKey.wasPressedThisFrame && LevelTwoActors.Near(local, valve)) ValveRpc();
        }
        private void PresentBrace()
        {
            if (Phase == 1 && holder.Value.TryGet(out var obj))
            {
                if (brace == null) { brace = obj.gameObject.AddComponent<LevelOneBracePose>(); brace.Begin(obj.GetComponent<CorePlayerManager>()); }
            }
            else if (brace != null) { Destroy(brace); brace = null; }
        }
        public override void OnNetworkDespawn() { if (brace != null) Destroy(brace); }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void WinchRpc(bool held, RpcParams rpc = default) => SetWinchHeld(LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId), held);
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ValveRpc(RpcParams rpc = default) => TryOpenValve(LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId));
        [Rpc(SendTo.Everyone)]
        private void OpenedRpc() => GetComponent<LevelTwoFeedback>().Play(0.25f);
        private void OnGUI()
        {
            var local = LevelTwoActors.Local;
            if (local == null || Vector3.Distance(local.transform.position, winch.position) > 20) return;
            GUI.Box(new Rect(20, 85, 650, 60), Open ? "Thủy môn đã mở. Cùng trở lại thuyền." :
                "Ba: giữ [E] tại tời. Con: bò qua cửa phụ, tới van và bấm [E].\nBa phải tiếp tục giữ tời cho tới khi Con mở van.");
        }
    }
}
