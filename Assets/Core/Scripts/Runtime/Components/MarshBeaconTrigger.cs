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
        public Transform pedestal;
        public Transform itemSocket;
        public LighthouseLightBeam beam;
        public LighthouseTurnWheel turnWheel;
        public MarshBeaconTrigger pairedBeacon;
        private readonly NetworkVariable<bool> lit = new NetworkVariable<bool>();
        private CorePlayerManager launchedChild;
        private float launchDeadline;
        public bool Lit => lit.Value;
        public bool RequiredItemSocketed => requiredItem != null && requiredItem.IsSocketed;

        private void Awake()
        {
            EnforceAuthoritativeTransforms();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnforceAuthoritativeTransforms();
        }

        public void EnforceAuthoritativeTransforms()
        {
            bool isWest = (requiredItem != null && requiredItem.itemType == CoopCarryableItem.ItemType.IgnitionCrank) ||
                          name.StartsWith("West");

            Vector3 targetPedestal = isWest ? new Vector3(84.6f, 1.3f, 175.8f) : new Vector3(115.4f, 1.3f, 189.2f);
            Vector3 targetSocket = isWest ? new Vector3(84.6f, 1.7f, 175.8f) : new Vector3(115.4f, 1.7f, 189.2f);
            Vector3 targetWheel = isWest ? new Vector3(85.2f, 1.6f, 175.8f) : new Vector3(114.8f, 1.6f, 189.2f);
            Vector3 targetLamp = isWest ? new Vector3(82.99f, 7.56f, 174.99f) : new Vector3(117.01f, 7.56f, 190.01f);

            if (pedestal == null)
            {
                pedestal = transform.Find(isWest ? "West_BeaconPedestal" : "East_BeaconPedestal") ??
                           transform.Find(isWest ? "West_BeaconConsole" : "East_BeaconConsole");
            }
            if (pedestal != null) pedestal.position = targetPedestal;
            if (itemSocket != null) itemSocket.position = targetSocket;

            if (turnWheel != null)
            {
                if (turnWheel.interactionPoint != null) turnWheel.interactionPoint.position = targetWheel;
                if (turnWheel.lanternHead != null) turnWheel.lanternHead.position = targetLamp;
            }

            if (beam != null && beam.emitter != null) beam.emitter.position = targetLamp;
        }

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
            if (!IsSpawned || local == null || requiredItem == null ||
                requiredItem.itemType != CoopCarryableItem.ItemType.IgnitionCrank ||
                local.transform.position.z < 150f || local.transform.position.z > 215f) return;

            var east = pairedBeacon;
            if (east == null)
            {
                foreach (var candidate in FindObjectsByType<MarshBeaconTrigger>(FindObjectsSortMode.None))
                    if (candidate != this && candidate.requiredItem != null &&
                        candidate.requiredItem.itemType == CoopCarryableItem.ItemType.RefractionPrism)
                    {
                        east = candidate;
                        pairedBeacon = candidate;
                        break;
                    }
            }
            bool eastSocketed = east != null && east.RequiredItemSocketed;
            bool beaconsLit = Lit && east != null && east.Lit;
            string objective = !RequiredItemSocketed
                ? "MỤC TIÊU 1: Con nhặt Tay Quay [F] ở bờ đá, đem gắn vào console dưới chân Tháp Tây."
                : !eastSocketed
                    ? "MỤC TIÊU 2: Ba nâng giữ thân cây, Con chui qua hốc nhặt Lăng Kính Cổ [F], gắn vào console Tháp Đông."
                    : !beaconsLit
                        ? "MỤC TIÊU 3: Cầm vô-lăng [F], lăn chuột lên/xuống (hoặc A/D) để căn tia sáng vào Mắt Ngọc Cối Xay!"
                        : "HOÀN THÀNH: Hai hải đăng đã sáng rực, sương mù tan biến! Cùng lên thuyền vượt cối xay.";
            LighthouseHud.DrawObjective(objective);
        }
    }

    internal static class LighthouseHud
    {
        private static GUIStyle hintStyle;
        private static GUIStyle objectiveStyle;

        public static void DrawWorldHint(Vector3 worldPosition, string message)
        {
            var camera = Camera.main;
            if (camera == null || string.IsNullOrEmpty(message)) return;

            Vector3 screen = camera.WorldToScreenPoint(worldPosition + Vector3.up * 1.25f);
            if (screen.z <= 0) return;

            hintStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            hintStyle.normal.textColor = new Color(1f, 0.84f, 0.2f);
            Vector2 size = hintStyle.CalcSize(new GUIContent(message));
            size.x = Mathf.Min(size.x + 28f, Screen.width - 20f);
            size.y = Mathf.Max(size.y + 12f, 38f);
            GUI.Box(new Rect(Mathf.Clamp(screen.x - size.x * 0.5f, 10f, Screen.width - size.x - 10f),
                Screen.height - screen.y - size.y * 0.5f, size.x, size.y), message, hintStyle);
        }

        public static void DrawObjective(string message)
        {
            objectiveStyle ??= new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
                padding = new RectOffset(22, 22, 10, 10)
            };
            objectiveStyle.normal.textColor = Color.white;
            float width = Mathf.Min(1000f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - width) * 0.5f, 18f, width, 66f), message, objectiveStyle);
        }
    }
}
