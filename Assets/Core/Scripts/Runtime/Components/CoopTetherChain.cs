using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(AudioSource))]
    public sealed class CoopTetherChain : NetworkBehaviour
    {
        public const float Length = 9.5f;
        const float RespawnDelay = 3f;
        public GameObject chainModel;
        public AudioClip clink;
        public AbyssFallTrigger abyss;
        public event System.Action CrewFell;
        readonly NetworkVariable<int> suspended = new(); // 2: Child hanging, 3: Dad has pulled the pair into the abyss
        readonly NetworkVariable<bool> hauling = new();
        public bool IsSuspended => suspended.Value == 2;
        public bool CrewIsFalling => suspended.Value == 3;
        public void RequestHaul() { if (IsSpawned && IsSuspended) HaulRpc(); }
        CorePlayerManager dad, child;
        Transform dadHand, childHand;
        HangingChain chain;
        public bool ChainVisible => chain != null && chain.Visible;
        bool wasTaut;
        float recoverAt;
        float nextSound;

        void Awake()
        {
            if (chainModel) chain = new HangingChain(transform, chainModel, Length);
        }

        public override void OnNetworkDespawn() => chain?.Hide();

        void LateUpdate()
        {
            if (!IsSpawned) return;
            if (!LevelOnePlayer.IsAlive(dad) || !LevelOnePlayer.IsAlive(child))
            {
                dad = child = null;
                foreach (var actor in LevelTwoActors.All)
                {
                    if (!LevelOnePlayer.IsAlive(actor)) continue;
                    if (LevelOnePlayer.IsDad(actor)) dad = actor;
                    else child = actor;
                }
                chain?.Hide();
                if (!dad || !child) { if (IsServer) ClearCatch(); return; }
                dadHand = Hand(dad, HumanBodyBones.RightHand);
                childHand = Hand(child, HumanBodyBones.LeftHand);
            }

            Vector3 a = Anchor(dad, dadHand), b = Anchor(child, childHand);
            float distance = Vector3.Distance(a, b);
            if (IsServer)
            {
                bool dadBelow = abyss.Below(dad.transform), childBelow = abyss.Below(child.transform);
                if (dadBelow && suspended.Value != 3)
                {
                    suspended.Value = 3;
                    hauling.Value = false;
                    recoverAt = Time.time + RespawnDelay;
                }
                if (suspended.Value == 3 && Time.time >= recoverAt)
                {
                    CrewFell?.Invoke();
                    return;
                }
                if (suspended.Value == 0 && childBelow && Supported(dad)) suspended.Value = 2;
                if (distance > Length && !wasTaut && Time.time >= nextSound)
                { ClinkRpc(); nextSound = Time.time + .65f; }
                wasTaut = distance > Length;
            }
            UpdateCrew();
            ConstrainHands(Anchor(child, childHand) - Anchor(dad, dadHand));
            chain?.Step(Anchor(dad, dadHand), Anchor(child, childHand), Time.deltaTime);
        }

        void UpdateCrew()
        {
            if (suspended.Value == 3)
            {
                if (child.IsOwner)
                {
                    var target = dad.transform.position + Vector3.down;
                    child.CoreMovement.SetPosition(Vector3.MoveTowards(child.transform.position, target, 30f * Time.deltaTime), false);
                    child.CoreMovement.SetVerticalVelocity(-9f);
                }
                return;
            }
            if (suspended.Value == 0) return;
            var fallen = child;
            var anchor = dad;
            if (anchor.IsOwner && Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame)) RequestHaul();
            if (!fallen.IsOwner) return;
            var cc = fallen.GetComponent<CharacterController>();
            if (!cc.enabled) return;
            bool taut = Vector3.Distance(Anchor(dad, dadHand), Anchor(child, childHand)) >= Length - .02f;
            if (hauling.Value || (taut && fallen.CoreMovement.VerticalVelocity < 0))
                fallen.CoreMovement.SetVerticalVelocity(0);
            var delta = anchor.transform.position - fallen.transform.position;
            if (hauling.Value)
            {
                // Rise clear of the ledge before crossing its lip.
                Vector3 destination = anchor.transform.position + Vector3.up * .7f;
                if (delta.y > -.25f) destination = new Vector3(fallen.transform.position.x, destination.y, fallen.transform.position.z);
                cc.Move(Vector3.ClampMagnitude(destination - fallen.transform.position, 7f * Time.deltaTime));
            }
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
        static Vector3 Anchor(CorePlayerManager actor, Transform hand) =>
            hand ? hand.position : actor.transform.position + Vector3.up;

        void ConstrainHands(Vector3 span)
        {
            float distance = span.magnitude;
            if (distance <= Length) return;
            var direction = span / distance;
            float excess = distance - Length;
            var first = dad.GetComponent<CharacterController>();
            var second = child.GetComponent<CharacterController>();
            bool moveDad = dad.IsOwner && first.enabled && !IsSuspended;
            bool moveChild = child.IsOwner && second.enabled;
            if (moveDad && moveChild)
            {
                var before = dad.transform.position;
                first.Move(direction * (.5f * excess));
                excess -= Vector3.Dot(dad.transform.position - before, direction);
            }
            if (moveChild) second.Move(-direction * excess);
            if (moveDad)
            {
                span = Anchor(child, childHand) - Anchor(dad, dadHand);
                if (span.magnitude > Length) first.Move(span - span.normalized * Length);
            }
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void HaulRpc(RpcParams rpc = default)
        {
            var anchor = dad;
            if (IsSuspended && anchor && anchor.OwnerClientId == rpc.Receive.SenderClientId && Supported(anchor) && !hauling.Value)
            { hauling.Value = true; ClinkRpc(); }
        }
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void FinishRpc(RpcParams rpc = default)
        {
            var fallen = child;
            if (hauling.Value && fallen && fallen.OwnerClientId == rpc.Receive.SenderClientId && dad && child &&
                Vector3.Distance(dad.transform.position, child.transform.position) < 2) ClearCatch();
        }
        void ClearCatch() { suspended.Value = 0; hauling.Value = false; }
        public void ResetForCheckpoint() { if (IsServer) ClearCatch(); chain?.Hide(); }
        [Rpc(SendTo.Everyone)]
        void ClinkRpc() { if (clink) GetComponent<AudioSource>().PlayOneShot(clink, .65f); }
    }
}
