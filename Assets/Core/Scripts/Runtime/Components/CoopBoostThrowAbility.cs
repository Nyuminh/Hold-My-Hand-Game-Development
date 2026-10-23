using System;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Kiệu Vai & Ném Lên Cao (Boost & Throw Jump):
    /// - 🧔 Ba (Dad) với thể hình to lớn có thể nâng hoặc làm bệ đỡ phóng 👶 Con (Child) lên cao.
    /// - Giúp Con vượt qua các bờ tường cao 4-5 mét hoặc nhảy qua khe nứt rộng.
    /// - Sau khi lên cao, Con có thể giật đòn bẩy hoặc thả dây leo xuống đón Ba.
    /// </summary>
    [RequireComponent(typeof(CoreMovement))]
    public class CoopBoostThrowAbility : NetworkBehaviour
    {
        #region Serialized Fields

        [Header("Thông số Phóng (Boost Parameters)")]
        [Tooltip("Lực phóng theo phương thẳng đứng (mét/giây)")]
        [SerializeField] private float verticalBoostVelocity = 11.5f;

        [Tooltip("Lực phóng theo phương ngang (hướng nhìn của Ba)")]
        [SerializeField] private float forwardBoostForce = 6.0f;

        [Tooltip("Khoảng cách tối đa để thực hiện kiệu phóng (mét)")]
        [SerializeField] private float maxBoostDistance = 2.5f;

        [Tooltip("Thời gian hồi giữa các lần phóng (giây)")]
        [SerializeField] private float boostCooldown = 2.0f;

        [Tooltip("Thể lực Ba tiêu hao mỗi lần phóng con")]
        [SerializeField] private float dadStaminaCost = 15.0f;

        [Header("Phím bấm")]
        [Tooltip("Phím thực hiện kiệu phóng")]
        [SerializeField] private KeyCode boostKey = KeyCode.F;

        #endregion

        #region Private Fields

        private CoreMovement m_Motor;
        private PlayerRoleVisuals m_RoleVisuals;
        private CoreStatsHandler m_StatsHandler;
        private float m_CooldownTimer = 0f;
        private Transform m_PartnerTransform;
        private CoopBoostThrowAbility m_PartnerBoostAbility;
        private bool m_CanBoostPartner = false;

        #endregion

        #region Properties

        public bool IsDad => m_RoleVisuals != null ? m_RoleVisuals.IsDad : gameObject.name.ToLower().Contains("dad");

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            m_Motor = GetComponent<CoreMovement>();
            m_RoleVisuals = GetComponent<PlayerRoleVisuals>();
            m_StatsHandler = GetComponent<CoreStatsHandler>();
        }

        private void Update()
        {
            if (m_CooldownTimer > 0f)
            {
                m_CooldownTimer -= Time.deltaTime;
            }

            if (!IsOwner) return;

            FindPartnerIfNeeded();

            if (m_PartnerTransform == null)
            {
                m_CanBoostPartner = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, m_PartnerTransform.position);
            m_CanBoostPartner = dist <= maxBoostDistance && m_CooldownTimer <= 0f;

            // Kiểm tra phím bấm F
            bool boostPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
            {
                boostPressed = true;
            }
#else
            if (Input.GetKeyDown(boostKey) || Input.GetKeyDown(KeyCode.F))
            {
                boostPressed = true;
            }
#endif

            if (boostPressed && m_CanBoostPartner)
            {
                ExecuteBoost();
            }
        }

        #endregion

        #region Boost Logic

        private void ExecuteBoost()
        {
            if (m_PartnerTransform == null) return;

            // Trường hợp 1: Người chơi đang điều khiển Ba (Dad) -> Phóng Con lên
            if (IsDad)
            {
                if (m_StatsHandler != null && !m_StatsHandler.TryConsumeStat(Animator.StringToHash("Stamina"), dadStaminaCost))
                {
                    Debug.LogWarning("[CoopBoost] Ba không đủ thể lực để kiệu phóng con!");
                    return;
                }

                m_CooldownTimer = boostCooldown;
                Vector3 launchDirection = transform.forward;
                
                if (m_PartnerBoostAbility != null)
                {
                    // Nếu là người chơi remote qua mạng -> Gửi RPC
                    if (m_PartnerBoostAbility.IsSpawned && !m_PartnerBoostAbility.IsOwner)
                    {
                        m_PartnerBoostAbility.ReceiveBoostLaunchRpc(launchDirection, verticalBoostVelocity, forwardBoostForce);
                    }
                    else // Nếu là dummy bot hoặc cùng máy chủ cục bộ -> Phóng trực tiếp
                    {
                        m_PartnerBoostAbility.ApplyLaunchLocally(launchDirection, verticalBoostVelocity, forwardBoostForce);
                    }
                }
                else
                {
                    // Fallback nếu partner chỉ có CoreMovement
                    var partnerMotor = m_PartnerTransform.GetComponent<CoreMovement>();
                    if (partnerMotor != null)
                    {
                        partnerMotor.SetVerticalVelocity(verticalBoostVelocity);
                        partnerMotor.ApplyExternalForce(launchDirection * forwardBoostForce, ForceMode.Impulse);
                    }
                }

                UISoundManager.PlayClick();
                Debug.Log("[CoopBoost] 🧔 Ba đã kiệu phóng 👶 Con lên cao!");
            }
            // Trường hợp 2: Người chơi đang điều khiển Con (Child) -> Nhờ Ba phóng
            else
            {
                m_CooldownTimer = boostCooldown;
                Vector3 launchDirection = m_PartnerTransform.forward;
                
                ApplyLaunchLocally(launchDirection, verticalBoostVelocity, forwardBoostForce);
                UISoundManager.PlayClick();
                Debug.Log("[CoopBoost] 👶 Con đã đạp lên vai Ba bật nhảy lên cao!");
            }
        }

        [Rpc(SendTo.Owner)]
        public void ReceiveBoostLaunchRpc(Vector3 forwardDirection, float verticalVelocity, float forwardForce)
        {
            ApplyLaunchLocally(forwardDirection, verticalVelocity, forwardForce);
        }

        public void ApplyLaunchLocally(Vector3 forwardDirection, float verticalVelocity, float forwardForce)
        {
            if (m_Motor == null) m_Motor = GetComponent<CoreMovement>();
            if (m_Motor == null) return;

            // Đặt vận tốc bay bổng lên trời
            m_Motor.SetVerticalVelocity(verticalVelocity);

            // Bổ sung lực đẩy theo phương ngang
            Vector3 horizForce = new Vector3(forwardDirection.x, 0f, forwardDirection.z).normalized * forwardForce;
            m_Motor.ApplyExternalForce(horizForce, ForceMode.Impulse);

            UISoundManager.PlayClick();
        }

        private void FindPartnerIfNeeded()
        {
            if (m_PartnerTransform != null) return;

            // 1. Quét AllPlayers
            foreach (var player in PlayerRoleVisuals.AllPlayers)
            {
                if (player != null && player.gameObject != this.gameObject)
                {
                    m_PartnerTransform = player.transform;
                    m_PartnerBoostAbility = player.GetComponent<CoopBoostThrowAbility>();
                    return;
                }
            }

            // 2. Fallback: Quét các component CoopBoostThrowAbility trong Scene
            var allBoostAbilities = FindObjectsByType<CoopBoostThrowAbility>(FindObjectsSortMode.None);
            foreach (var ability in allBoostAbilities)
            {
                if (ability != null && ability.gameObject != this.gameObject)
                {
                    m_PartnerTransform = ability.transform;
                    m_PartnerBoostAbility = ability;
                    return;
                }
            }

            // 3. Fallback: Quét CoreMovement trong Scene
            var allMotors = FindObjectsByType<CoreMovement>(FindObjectsSortMode.None);
            foreach (var motor in allMotors)
            {
                if (motor != null && motor.gameObject != this.gameObject)
                {
                    m_PartnerTransform = motor.transform;
                    m_PartnerBoostAbility = motor.GetComponent<CoopBoostThrowAbility>();
                    return;
                }
            }
        }

        #endregion

        #region Debug UI

        private void OnGUI()
        {
            if (!IsOwner) return;

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            boxStyle.normal.textColor = Color.white;

            if (m_CanBoostPartner)
            {
                string prompt = IsDad 
                    ? "🚀 Bấm [F] để Kiệu Phóng Con Lên Cao (Boost Jump)"
                    : "🚀 Bấm [F] để Nhảy Lên Vai Ba Phóng Lên";

                GUI.color = new Color(0.2f, 1f, 0.4f, 0.95f);
                GUI.Box(new Rect(Screen.width / 2f - 220, Screen.height - 165, 440, 42), prompt, boxStyle);
            }
        }

        #endregion
    }
}
