using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Tâm lý chần chừ & sợ hãi của Đứa Con (Child Fear & Hesitation Behavior):
    /// - Khi một mình bước vào vùng nguy hiểm (HesitationHazardZone):
    ///   + Tốc độ đi chậm lại rõ rệt (chần chừ, lo lắng).
    ///   + Hiện tín hiệu gọi Ba ("Con cần Ba nắm tay").
    /// - Ngay khi Ba tới bấm [H] nắm tay:
    ///   + Nỗi sợ biến mất tức thì, tốc độ hồi phục và tăng thêm 15% hứng khởi khi chạy cùng Ba.
    ///   + Thể hiện đúng thông điệp: "Chỉ cần nắm tay Ba, con không còn sợ hãi gì nữa."
    /// </summary>
    public class ChildFearHesitation : MonoBehaviour
    {
        private CoreMovement m_Motor;
        private CoopHoldHandAbility m_HoldAbility;
        private PlayerRoleVisuals m_RoleVisuals;

        private float m_OriginalMoveSpeed = 4.0f;
        private float m_OriginalSprintSpeed = 6.0f;
        private HesitationHazardZone m_CurrentZone = null;
        private bool m_IsFrightened = false;

        private GameObject m_FearIndicatorObj;
        private Light m_FearPulseLight;

        public bool IsFrightened => m_IsFrightened;

        private void Awake()
        {
            m_Motor = GetComponent<CoreMovement>();
            m_HoldAbility = GetComponent<CoopHoldHandAbility>();
            m_RoleVisuals = GetComponent<PlayerRoleVisuals>();

            if (m_Motor != null)
            {
                m_OriginalMoveSpeed = m_Motor.moveSpeed;
                m_OriginalSprintSpeed = m_Motor.sprintSpeed;
            }

            CreateFearIndicator();
        }

        private void CreateFearIndicator()
        {
            // Indicator trên đầu Con (tín hiệu cần Ba)
            m_FearIndicatorObj = new GameObject("Child_FearIndicator");
            m_FearIndicatorObj.transform.SetParent(this.transform, false);
            m_FearIndicatorObj.transform.localPosition = new Vector3(0f, 1.8f, 0f);

            m_FearPulseLight = m_FearIndicatorObj.AddComponent<Light>();
            m_FearPulseLight.type = LightType.Point;
            m_FearPulseLight.color = new Color(0.4f, 0.7f, 1.0f); // Xanh nhạt ngập ngừng
            m_FearPulseLight.range = 2.2f;
            m_FearPulseLight.intensity = 0f;
            m_FearPulseLight.enabled = false;
        }

        public void EnterHazardZone(HesitationHazardZone zone)
        {
            // Chỉ tác dụng lên Đứa Con (nếu là Ba thì không sợ)
            if (m_RoleVisuals != null && m_RoleVisuals.IsDad) return;

            m_CurrentZone = zone;
        }

        public void ExitHazardZone(HesitationHazardZone zone)
        {
            if (m_CurrentZone == zone)
            {
                m_CurrentZone = null;
                ResetSpeedToNormal();
            }
        }

        private void Update()
        {
            // Nếu là Ba -> Không có hiệu ứng này
            if (m_RoleVisuals != null && m_RoleVisuals.IsDad)
            {
                if (m_IsFrightened) ResetSpeedToNormal();
                return;
            }

            if (m_CurrentZone != null)
            {
                bool isHoldingHands = m_HoldAbility != null && m_HoldAbility.IsHoldingHands;

                if (!isHoldingHands)
                {
                    // Con đang một mình ở vùng nguy hiểm -> Sợ hãi
                    m_IsFrightened = true;
                    if (m_Motor != null)
                    {
                        m_Motor.moveSpeed = m_OriginalMoveSpeed * m_CurrentZone.FearSpeedMultiplier;
                        m_Motor.sprintSpeed = m_OriginalSprintSpeed * m_CurrentZone.FearSpeedMultiplier;
                    }

                    // Nhấp nháy ánh sáng lo lắng
                    if (m_FearPulseLight != null)
                    {
                        m_FearPulseLight.enabled = true;
                        float pulse = 0.5f + Mathf.Sin(Time.time * 4f) * 0.4f;
                        m_FearPulseLight.intensity = pulse;
                    }
                }
                else
                {
                    // Đã nắm tay Ba -> An tâm, tăng tốc dũng cảm!
                    m_IsFrightened = false;
                    if (m_Motor != null)
                    {
                        m_Motor.moveSpeed = m_OriginalMoveSpeed * 1.15f;
                        m_Motor.sprintSpeed = m_OriginalSprintSpeed * 1.1f;
                    }

                    if (m_FearPulseLight != null && m_FearPulseLight.enabled)
                    {
                        m_FearPulseLight.enabled = false;
                    }
                }
            }
            else
            {
                if (m_IsFrightened)
                {
                    ResetSpeedToNormal();
                }
            }
        }

        private void ResetSpeedToNormal()
        {
            m_IsFrightened = false;
            if (m_Motor != null)
            {
                m_Motor.moveSpeed = m_OriginalMoveSpeed;
                m_Motor.sprintSpeed = m_OriginalSprintSpeed;
            }

            if (m_FearPulseLight != null)
            {
                m_FearPulseLight.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (m_FearIndicatorObj != null)
            {
                Destroy(m_FearIndicatorObj);
            }
        }
    }
}
