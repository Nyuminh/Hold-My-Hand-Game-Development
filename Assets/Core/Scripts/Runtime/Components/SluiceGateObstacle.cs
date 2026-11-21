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
        private Vector3 closedPosition, crawlClosedPosition;
        private CorePlayerManager enteredChild, crossedChild;
        private const float PushDistance = 2.8f;
        public int Phase => phase.Value;
        public float Lift => lift.Value;
        public bool Open => Phase == 2;
        public bool ChildTraversed => crossedChild != null;
        public void RestoreCheckpoint(bool opened)
        {
            if (!IsServer) return;
            phase.Value = opened ? 2 : 0; lift.Value = opened ? fullLift : 0;
            enteredChild = crossedChild = null;
            gate.localPosition = closedPosition + Vector3.up * lift.Value;
            crawlDoor.localPosition = RockTarget;
        }
        private Vector3 RockTarget => crawlClosedPosition + Vector3.right * (Phase > 0 ? PushDistance : 0f);
        private void Awake()
        {
            if (gate) closedPosition = gate.localPosition;
            if (crawlDoor) crawlClosedPosition = crawlDoor.localPosition;
        }
        public override void OnNetworkSpawn()
        {
            crawlDoor.localPosition = RockTarget;
            gate.localPosition = closedPosition + Vector3.up * lift.Value;
        }
        public bool TryPushRock(CorePlayerManager actor)
        {
            if (!IsServer || Phase != 0 || !LevelTwoActors.Near(actor, crawlDoor, 2.8f) ||
                !LevelOnePlayer.IsDad(actor) || (lastSlalom && !lastSlalom.Passed)) return false;
            phase.Value = 1;
            return true;
        }
        public bool TryOpenValve(CorePlayerManager actor)
        {
            if (!IsServer || Phase != 1 || !LevelTwoActors.Near(actor, valve) || LevelOnePlayer.IsDad(actor) || actor != crossedChild) return false;
            phase.Value = 2; OpenedRpc(); return true;
        }

        public void ObserveCrawl(CorePlayerManager actor)
        {
            if (!IsServer || Phase != 1 || Vector3.Distance(crawlDoor.localPosition, RockTarget) > .05f || !LevelTwoActors.OnFoot(actor) || LevelOnePlayer.IsDad(actor)) return;
            Vector3 p = crawl.transform.InverseTransformPoint(actor.transform.position);
            if (crawl.bounds.Contains(actor.transform.position + Vector3.up * 0.2f) && p.z < 0) enteredChild = actor;
            if (enteredChild == actor && p.z > 2.3f && Mathf.Abs(p.x) < 0.75f) crossedChild = actor;
        }
        private void Update()
        {
            if (!IsSpawned) return;
            if (IsServer)
            {
                if (Phase == 1)
                    foreach (var actor in LevelTwoActors.All) ObserveCrawl(actor);
                lift.Value = Mathf.MoveTowards(lift.Value, Open ? fullLift : 0, Time.deltaTime * 2f);
            }
            gate.localPosition = closedPosition + Vector3.up * lift.Value;
            crawlDoor.localPosition = Vector3.MoveTowards(crawlDoor.localPosition, RockTarget, 5 * Time.deltaTime);
            var actorLocal = LevelTwoActors.Local;
            if (!actorLocal || Keyboard.current == null || !Keyboard.current.fKey.wasPressedThisFrame) return;
            if (Phase == 0 && LevelOnePlayer.IsDad(actorLocal) && LevelTwoActors.Near(actorLocal, crawlDoor, 2.8f)) PushRpc();
            if (Phase == 1 && !LevelOnePlayer.IsDad(actorLocal) && LevelTwoActors.Near(actorLocal, valve)) ValveRpc();
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void PushRpc(RpcParams rpc = default) => TryPushRock(LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId));
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ValveRpc(RpcParams rpc = default) => TryOpenValve(LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId));
        [Rpc(SendTo.Everyone)]
        private void OpenedRpc() => GetComponent<LevelTwoFeedback>().Play(0.25f);
        private void OnGUI()
        {
            var local = LevelTwoActors.Local;
            if (local == null || Vector3.Distance(local.transform.position, winch.position) > 20) return;
            GUI.Box(new Rect(20, 85, 620, 38), Open ? "Thủy môn đã mở. Cùng trở lại thuyền." :
                Phase == 0 ? "Ba đến gần tảng đá trước lối chui và nhấn [F] để đẩy sang bên." :
                "Con chui qua lỗ, đến van bên kia và nhấn [F] để mở thủy môn.");
        }
    }
}
