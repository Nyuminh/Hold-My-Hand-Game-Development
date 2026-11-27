using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(AudioSource))]
    public sealed class CoopTetherChain : NetworkBehaviour
    {
        // Rút ngắn dây xích 3m (từ 9.5m xuống 6.5m) theo yêu cầu người dùng
        public const float Length = 6.5f;
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
                bool dadBelow = abyss != null && abyss.Below(dad.transform);
                bool childBelow = abyss != null && abyss.Below(child.transform);
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

            // Cập nhật Animation kéo dây (Pulling A Rope) trên Ba
            UpdateDadPullAnimation();
        }

        void UpdateDadPullAnimation()
        {
            if (dad == null) return;
            var dadAnimator = dad.GetComponentInChildren<CoreAnimator>();
            if (dadAnimator != null)
            {
                dadAnimator.SetPullingRope(hauling.Value);
            }

            // Xoay mặt Ba hướng về phía Con khi đang kéo
            if (hauling.Value && child != null)
            {
                Vector3 toChild = child.transform.position - dad.transform.position;
                toChild.y = 0;
                if (toChild.sqrMagnitude > 0.05f)
                {
                    dad.transform.rotation = Quaternion.Slerp(dad.transform.rotation, Quaternion.LookRotation(toChild), 10f * Time.deltaTime);
                }
            }
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

            // Nhấn hoặc giữ phím E/F để Ba gồng sức kéo Con lên
            if (anchor.IsOwner && Keyboard.current != null &&
                (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.eKey.isPressed || Keyboard.current.fKey.wasPressedThisFrame))
            {
                RequestHaul();
            }

            if (!fallen.IsOwner) return;
            var cc = fallen.GetComponent<CharacterController>();
            if (!cc.enabled) return;

            // Tính điểm neo thực tế (nếu dây vắt qua mép vực thì điểm neo là mép đá)
            Vector3 anchorPos = Anchor(dad, dadHand);
            if (Physics.Linecast(anchorPos, Anchor(child, childHand), out var ledgeHit, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && ledgeHit.collider is not CharacterController)
            {
                anchorPos = ledgeHit.point;
            }

            float usedOnPlatform = Vector3.Distance(Anchor(dad, dadHand), anchorPos);
            float maxHangLength = Mathf.Max(1.2f, Length - usedOnPlatform);

            if (!hauling.Value)
            {
                // ─── Hình 1: Đưa con về phương thẳng đứng theo trọng lực vật lý ───
                // Trọng tâm treo cân bằng là điểm thẳng đứng ngay dưới điểm neo
                Vector3 plumbLineOffset = new Vector3(anchorPos.x - fallen.transform.position.x, 0, anchorPos.z - fallen.transform.position.z);
                
                // Kéo dạt về phương thẳng đứng tự nhiên như con lắc
                float swingDamping = 6.0f;
                cc.Move(plumbLineOffset * (swingDamping * Time.deltaTime));

                // Tác dụng trọng lực rơi xuống cho đến khi đạt độ dài tối đa của xích
                float currentDistFromAnchor = Vector3.Distance(anchorPos, fallen.transform.position);
                if (currentDistFromAnchor < maxHangLength - 0.05f)
                {
                    cc.Move(Vector3.down * (12.0f * Time.deltaTime));
                }
                else
                {
                    // Khi xích đã căng thẳng đứng: ghìm vị trí chắc chắn
                    Vector3 radial = (fallen.transform.position - anchorPos).normalized;
                    Vector3 targetPos = anchorPos + radial * maxHangLength;
                    cc.Move(targetPos - fallen.transform.position);
                    fallen.CoreMovement.SetVerticalVelocity(0);
                }
            }
            else
            {
                // ─── Đang gồng sức kéo Con lên (Hauling) ───
                var delta = anchor.transform.position - fallen.transform.position;
                Vector3 destination = anchor.transform.position + Vector3.up * 0.7f;
                if (delta.y > -0.25f)
                {
                    destination = new Vector3(fallen.transform.position.x, destination.y, fallen.transform.position.z);
                }
                cc.Move(Vector3.ClampMagnitude(destination - fallen.transform.position, 6.5f * Time.deltaTime));

                if (delta.magnitude < 1.35f)
                {
                    FinishRpc();
                }
            }
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

        /// <summary>
        /// Giảm độ giật/thòng đột ngột của xích khi di chuyển trên mặt đất:
        /// Áp dụng lực đàn hồi mượt mà thay vì giật giật 100% trong 1 frame đơn lẻ.
        /// </summary>
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

            // Lực kéo đàn hồi êm dịu, chống giật mạnh đột ngột
            float pullSpeed = Mathf.Clamp(excess * 7.5f, 2.0f, 16.0f);
            float pullAmount = Mathf.Min(excess, pullSpeed * Time.deltaTime + 0.10f * excess);

            if (moveDad && moveChild)
            {
                var before = dad.transform.position;
                float halfPull = pullAmount * 0.5f;
                first.Move(direction * halfPull);
                excess -= Vector3.Dot(dad.transform.position - before, direction);
                second.Move(-direction * halfPull);
            }
            else if (moveChild)
            {
                second.Move(-direction * pullAmount);
            }
            else if (moveDad)
            {
                first.Move(direction * pullAmount);
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void HaulRpc(RpcParams rpc = default)
        {
            var anchor = dad;
            if (IsSuspended && anchor && anchor.OwnerClientId == rpc.Receive.SenderClientId && Supported(anchor) && !hauling.Value)
            {
                hauling.Value = true;
                ClinkRpc();
            }
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void FinishRpc(RpcParams rpc = default)
        {
            var fallen = child;
            if (hauling.Value && fallen && fallen.OwnerClientId == rpc.Receive.SenderClientId && dad && child &&
                Vector3.Distance(dad.transform.position, child.transform.position) < 2)
            {
                ClearCatch();
            }
        }

        void ClearCatch()
        {
            suspended.Value = 0;
            hauling.Value = false;
            UpdateDadPullAnimation();
        }

        public void ResetForCheckpoint()
        {
            if (IsServer) ClearCatch();
            chain?.Hide();
            UpdateDadPullAnimation();
        }

        [Rpc(SendTo.Everyone)]
        void ClinkRpc()
        {
            if (clink) GetComponent<AudioSource>().PlayOneShot(clink, .65f);
        }
    }
}
