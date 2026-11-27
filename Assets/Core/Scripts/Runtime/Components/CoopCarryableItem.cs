using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(Collider))]
    public sealed class CoopCarryableItem : NetworkBehaviour
    {
        public enum ItemType { IgnitionCrank, RefractionPrism }

        [SerializeField] public ItemType itemType;
        [SerializeField] public Transform carryOffset;
        [SerializeField] public float heavyMovePenalty = 0.8f;
        [SerializeField] public MarshBeaconTrigger socketOwner;

        private readonly NetworkVariable<NetworkObjectReference> currentHolder = new();
        private readonly NetworkVariable<bool> isSocketed = new();
        private Collider itemCollider;
        private Rigidbody itemBody;
        private bool bodyWasKinematic;
        private bool colliderWasEnabled;

        public bool IsCarried => currentHolder.Value.TryGet(out _);
        public bool IsSocketed => isSocketed.Value;
        public NetworkObject Holder
        {
            get { currentHolder.Value.TryGet(out var holder); return holder; }
        }

        private void Awake()
        {
            itemCollider = GetComponent<Collider>();
            itemBody = GetComponent<Rigidbody>();
            colliderWasEnabled = itemCollider.enabled;
            if (itemBody != null) bodyWasKinematic = itemBody.isKinematic;
        }

        public override void OnNetworkSpawn()
        {
            currentHolder.OnValueChanged += HolderChanged;
            isSocketed.OnValueChanged += SocketChanged;
            ApplyState();
        }

        public override void OnNetworkDespawn()
        {
            currentHolder.OnValueChanged -= HolderChanged;
            isSocketed.OnValueChanged -= SocketChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;
            ApplyState();
            var local = LevelTwoActors.Local;
            if (local == null || Keyboard.current == null) return;

            if (IsHeldBy(local))
            {
                if (Keyboard.current.gKey.wasPressedThisFrame) RequestDropRpc();
                else if (Keyboard.current.eKey.wasPressedThisFrame && CanSocket(local)) RequestSocketRpc();
                return;
            }

            if (!IsCarried && !IsSocketed && Keyboard.current.eKey.wasPressedThisFrame &&
                Vector3.Distance(local.transform.position, transform.position) <= 2.5f && IsClosestPickup(local))
                RequestPickupRpc();
        }

        private void LateUpdate()
        {
            if (!IsSpawned) return;
            if (IsSocketed && socketOwner != null && socketOwner.itemSocket != null)
            {
                transform.SetPositionAndRotation(socketOwner.itemSocket.position, socketOwner.itemSocket.rotation);
                return;
            }
            if (!currentHolder.Value.TryGet(out var holder)) return;
            var anchor = FindCarryAnchor(holder.transform);
            Vector3 offset = carryOffset != null ? carryOffset.localPosition :
                (anchor == holder.transform ? new Vector3(0.35f, 1.1f, 0.25f) : Vector3.zero);
            Quaternion rotation = carryOffset != null ? carryOffset.localRotation : Quaternion.identity;
            transform.SetPositionAndRotation(anchor.TransformPoint(offset), anchor.rotation * rotation);
        }

        public bool TryPickup(CorePlayerManager player)
        {
            if (!IsServer || player == null || !LevelTwoActors.OnFoot(player) || IsCarried || IsSocketed ||
                Vector3.Distance(player.transform.position, transform.position) > 2.5f ||
                FindObjectsByType<CoopCarryableItem>(FindObjectsSortMode.None).Any(i => i != this && i.IsHeldBy(player)))
                return false;

            currentHolder.Value = new NetworkObjectReference(player.NetworkObject);
            SetPhysics(false);
            PickupClientRpc(player.NetworkObjectId);
            return true;
        }

        public void Drop()
        {
            if (!IsServer || !currentHolder.Value.TryGet(out var holder)) return;
            Vector3 position = holder.transform.position + holder.transform.forward * 0.8f + Vector3.up * 0.35f;
            currentHolder.Value = default;
            transform.SetPositionAndRotation(position, Quaternion.identity);
            SetPhysics(true);
            DropClientRpc(position, Quaternion.identity);
        }

        public void InsertIntoSocket(Transform socketTransform)
        {
            if (!IsServer || socketTransform == null || IsSocketed) return;
            currentHolder.Value = default;
            isSocketed.Value = true;
            transform.SetPositionAndRotation(socketTransform.position, socketTransform.rotation);
            SetPhysics(false);
            socketOwner?.NotifyItemSocketed(this);
            SocketClientRpc(socketTransform.position, socketTransform.rotation);
        }

        public bool IsHeldBy(CorePlayerManager player) => player != null && currentHolder.Value.TryGet(out var holder) && holder == player.NetworkObject;

        private bool CanSocket(CorePlayerManager player) => socketOwner != null && socketOwner.requiredItem == this &&
            socketOwner.itemSocket != null && IsHeldBy(player) && Vector3.Distance(player.transform.position, socketOwner.itemSocket.position) < 2f;

        private bool IsClosestPickup(CorePlayerManager player)
        {
            float distance = Vector3.Distance(player.transform.position, transform.position);
            foreach (var item in FindObjectsByType<CoopCarryableItem>(FindObjectsSortMode.None))
                if (item != this && !item.IsCarried && !item.IsSocketed &&
                    Vector3.Distance(player.transform.position, item.transform.position) < distance) return false;
            return true;
        }

        private static Transform FindCarryAnchor(Transform player)
        {
            foreach (var name in new[] { "RightHand", "Hand_R", "mixamorig:RightHand", "Chest", "mixamorig:Spine2" })
            {
                var candidates = player.GetComponentsInChildren<Transform>(true);
                var match = System.Array.Find(candidates, t => t.name == name);
                if (match != null) return match;
            }
            return player;
        }

        private void ApplyState()
        {
            SetPhysics(!IsCarried && !IsSocketed);
        }

        private void SetPhysics(bool enabled)
        {
            if (itemCollider != null) itemCollider.enabled = enabled && colliderWasEnabled;
            if (itemBody != null)
            {
                itemBody.isKinematic = enabled ? bodyWasKinematic : true;
                itemBody.linearVelocity = Vector3.zero;
                itemBody.angularVelocity = Vector3.zero;
            }
        }

        private void HolderChanged(NetworkObjectReference before, NetworkObjectReference after) => ApplyState();
        private void SocketChanged(bool before, bool after) => ApplyState();

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestPickupRpc(RpcParams rpc = default) => TryPickup(LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId));

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestDropRpc(RpcParams rpc = default)
        {
            var sender = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (IsHeldBy(sender)) Drop();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestSocketRpc(RpcParams rpc = default)
        {
            var sender = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (CanSocket(sender)) InsertIntoSocket(socketOwner.itemSocket);
        }

        private void OnGUI()
        {
            var local = LevelTwoActors.Local;
            if (!IsSpawned || local == null || IsSocketed) return;

            if (IsHeldBy(local))
            {
                LighthouseHud.DrawWorldHint(transform.position,
                    CanSocket(local) ? "[E] Gắn vào Ổ cắm" : "[G] Thả xuống");
                return;
            }

            if (!IsCarried && Vector3.Distance(local.transform.position, transform.position) <= 2.5f)
                LighthouseHud.DrawWorldHint(transform.position, itemType == ItemType.IgnitionCrank
                    ? "[E] Nhặt Tay Quay" : "[E] Nhặt Lăng Kính");
        }

        [Rpc(SendTo.Everyone)]
        private void PickupClientRpc(ulong holderId) => ApplyState();

        [Rpc(SendTo.Everyone)]
        private void DropClientRpc(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            ApplyState();
        }

        [Rpc(SendTo.Everyone)]
        private void SocketClientRpc(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            ApplyState();
        }
    }

}
