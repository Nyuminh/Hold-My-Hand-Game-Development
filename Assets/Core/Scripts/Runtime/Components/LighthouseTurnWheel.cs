using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject))]
    public sealed class LighthouseTurnWheel : NetworkBehaviour
    {
        public Transform interactionPoint;
        public Transform lanternHead;
        public float minimumAngle = -30f;
        public float maximumAngle = 30f;
        public float rotationSpeed = 24f;

        private readonly NetworkVariable<float> currentAngle = new();
        private readonly NetworkVariable<ulong> controller = new(ulong.MaxValue);
        private Quaternion baseRotation;
        private bool localSteering;

        public float CurrentAngle => currentAngle.Value;
        public bool IsControlled => controller.Value != ulong.MaxValue;

        private void Awake()
        {
            if (lanternHead != null) baseRotation = lanternHead.localRotation;
        }

        public override void OnNetworkSpawn()
        {
            currentAngle.OnValueChanged += AngleChanged;
            ApplyAngle(currentAngle.Value);
        }

        public override void OnNetworkDespawn()
        {
            currentAngle.OnValueChanged -= AngleChanged;
            localSteering = false;
        }

        private void Update()
        {
            if (!IsSpawned || Keyboard.current == null) return;
            var local = LevelTwoActors.Local;
            if (local == null) return;

            if (!localSteering)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame && LevelTwoActors.Near(local, interactionPoint != null ? interactionPoint : transform, 2f))
                    RequestControlRpc();
                return;
            }

            if (controller.Value != local.OwnerClientId || !LevelTwoActors.Near(local, interactionPoint != null ? interactionPoint : transform, 2.5f))
            {
                localSteering = false;
                ReleaseControlRpc();
                return;
            }
            if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                localSteering = false;
                ReleaseControlRpc();
                return;
            }

            float input = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f) -
                          (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
            if (input != 0) RotateRpc(input, Mathf.Min(Time.deltaTime, 0.1f));
        }

        public bool TrySetAngle(float angle)
        {
            if (!IsServer) return false;
            currentAngle.Value = Mathf.Clamp(angle, minimumAngle, maximumAngle);
            return true;
        }

        private void ApplyAngle(float angle)
        {
            if (lanternHead != null) lanternHead.localRotation = baseRotation * Quaternion.Euler(0, angle, 0);
        }

        private void AngleChanged(float before, float after) => ApplyAngle(after);

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RequestControlRpc(RpcParams rpc = default)
        {
            var actor = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (controller.Value != ulong.MaxValue || !LevelTwoActors.Near(actor, interactionPoint != null ? interactionPoint : transform, 2f)) return;
            controller.Value = rpc.Receive.SenderClientId;
            ConfirmControlRpc(RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ConfirmControlRpc(RpcParams rpc = default) => localSteering = true;

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void RotateRpc(float input, float deltaTime, RpcParams rpc = default)
        {
            if (controller.Value != rpc.Receive.SenderClientId) return;
            var actor = LevelTwoActors.Sender(NetworkManager, rpc.Receive.SenderClientId);
            if (!LevelTwoActors.Near(actor, interactionPoint != null ? interactionPoint : transform, 2.5f)) { controller.Value = ulong.MaxValue; return; }
            currentAngle.Value = Mathf.Clamp(currentAngle.Value + Mathf.Clamp(input, -1, 1) * rotationSpeed * Mathf.Clamp(deltaTime, 0, 0.1f), minimumAngle, maximumAngle);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ReleaseControlRpc(RpcParams rpc = default)
        {
            if (controller.Value == rpc.Receive.SenderClientId) controller.Value = ulong.MaxValue;
        }

        private void OnGUI()
        {
            var local = LevelTwoActors.Local;
            Transform point = interactionPoint != null ? interactionPoint : transform;
            if (!IsSpawned || local == null) return;

            if (localSteering && controller.Value == local.OwnerClientId)
                LighthouseHud.DrawWorldHint(point.position, "[A/D] Xoay hướng tia sáng · [E/Space] Thoát");
            else if (LevelTwoActors.Near(local, point, 2.5f))
                LighthouseHud.DrawWorldHint(point.position, "[E] Cầm lái vô-lăng");
        }
    }
}
