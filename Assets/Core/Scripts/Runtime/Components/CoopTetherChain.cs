using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(LineRenderer), typeof(AudioSource))]
    public sealed class CoopTetherChain : NetworkBehaviour
    {
        public const float Length = 9.5f;
        public AudioClip clink;
        public AbyssFallTrigger abyss;
        public event System.Action CrewFell;
        readonly NetworkVariable<int> suspended = new(); // 1 Dad, 2 Child
        readonly NetworkVariable<bool> hauling = new();
        public bool IsSuspended => suspended.Value != 0;
        public void RequestHaul() { if (IsSpawned && IsSuspended) HaulRpc(); }
        CorePlayerManager dad, child;
        Transform dadHand, childHand;
        LineRenderer chain;
        bool wasTaut, failed;
        float nextSound;

        void Awake()
        {
            chain = GetComponent<LineRenderer>();
            chain.enabled = false;
            chain.positionCount = 17;
            chain.useWorldSpace = true;
            chain.startWidth = chain.endWidth = .045f;
            chain.startColor = chain.endColor = new Color(.46f, .48f, .52f);
        }

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!LevelOnePlayer.IsAlive(dad) || !LevelOnePlayer.IsAlive(child))
            {
                dad = child = null;
                foreach (var actor in LevelTwoActors.All)
                    if (LevelOnePlayer.IsDad(actor)) dad = actor;
                    else child = actor;
                chain.enabled = dad && child;
                if (!chain.enabled) { if (IsServer) ClearCatch(); return; }
                dadHand = Hand(dad, HumanBodyBones.RightHand);
                childHand = Hand(child, HumanBodyBones.LeftHand);
            }
            Vector3 a = dadHand ? dadHand.position : dad.transform.position + Vector3.up;
            Vector3 b = childHand ? childHand.position : child.transform.position + Vector3.up;
            float distance = Vector3.Distance(dad.transform.position, child.transform.position);
            float sag = Mathf.Clamp((Length - Vector3.Distance(a, b)) * .14f, 0, .78f);
            for (int i = 0; i < chain.positionCount; i++)
            {
                float t = (float)i / (chain.positionCount - 1);
                chain.SetPosition(i, Vector3.Lerp(a, b, t) + Vector3.down * (4 * t * (1 - t) * sag));
            }
            if (IsServer)
            {
                bool dadBelow = abyss.Below(dad.transform), childBelow = abyss.Below(child.transform);
                if (dadBelow && childBelow)
                {
                    ClearCatch();
                    if (!failed) { failed = true; CrewFell?.Invoke(); }
                    return;
                }
                failed = false;
                if (suspended.Value == 0)
                {
                    if (dadBelow && Supported(child)) suspended.Value = 1;
                    if (childBelow && Supported(dad)) suspended.Value = 2;
                }
                if (distance > Length && !wasTaut && Time.time >= nextSound)
                { ClinkRpc(); nextSound = Time.time + .65f; }
                wasTaut = distance > Length;
            }
            if (suspended.Value == 0)
            {
                if (distance > Length)
                {
                    Pull(dad, child, distance);
                    Pull(child, dad, distance);
                }
                return;
            }
            var fallen = suspended.Value == 1 ? dad : child;
            var anchor = fallen == dad ? child : dad;
            if (anchor.IsOwner && Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)) RequestHaul();
            if (!fallen.IsOwner) return;
            var cc = fallen.GetComponent<CharacterController>();
            if (!cc.enabled) return;
            fallen.CoreMovement.SetVerticalVelocity(0);
            var delta = anchor.transform.position - fallen.transform.position;
            if (hauling.Value)
            {
                // Rise clear of the ledge before crossing its lip.
                Vector3 destination = anchor.transform.position + Vector3.up * .7f;
                if (delta.y > -.25f) destination = new Vector3(fallen.transform.position.x, destination.y, fallen.transform.position.z);
                cc.Move(Vector3.ClampMagnitude(destination - fallen.transform.position, 7f * Time.deltaTime));
            }
            else if (delta.magnitude > Length) cc.Move(delta.normalized * Mathf.Min(delta.magnitude - Length, 12 * Time.deltaTime));
            if (hauling.Value && delta.magnitude < 1.35f) FinishRpc();
        }

        static Transform Hand(CorePlayerManager actor, HumanBodyBones bone)
        {
            foreach (var animator in actor.GetComponentsInChildren<Animator>())
                if (animator.isHuman && animator.GetBoneTransform(bone) is Transform hand) return hand;
            return null;
        }
        static bool Supported(CorePlayerManager actor) => Physics.Raycast(actor.transform.position + Vector3.up * .15f,
            Vector3.down, .65f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        static void Pull(CorePlayerManager actor, CorePlayerManager partner, float distance)
        {
            if (!actor.IsOwner || !actor.TryGetComponent<CharacterController>(out var cc) || !cc.enabled) return;
            cc.Move((partner.transform.position - actor.transform.position).normalized *
                (Mathf.Min(7, 5 * (distance - Length)) * Time.deltaTime));
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void HaulRpc(RpcParams rpc = default)
        {
            var anchor = suspended.Value == 1 ? child : dad;
            if (suspended.Value != 0 && anchor && anchor.OwnerClientId == rpc.Receive.SenderClientId && Supported(anchor) && !hauling.Value)
            { hauling.Value = true; ClinkRpc(); }
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void FinishRpc(RpcParams rpc = default)
        {
            var fallen = suspended.Value == 1 ? dad : child;
            if (hauling.Value && fallen && fallen.OwnerClientId == rpc.Receive.SenderClientId && dad && child &&
                Vector3.Distance(dad.transform.position, child.transform.position) < 2) ClearCatch();
        }
        void ClearCatch() { suspended.Value = 0; hauling.Value = false; }
        public void ResetForCheckpoint() { if (IsServer) { ClearCatch(); failed = false; } }
        [Rpc(SendTo.Everyone)]
        void ClinkRpc() { if (clink) GetComponent<AudioSource>().PlayOneShot(clink, .65f); }
    }
}
