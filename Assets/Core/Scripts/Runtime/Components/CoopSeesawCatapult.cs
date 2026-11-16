using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Co-op "Đại Bác Bập Bênh" (Seesaw Catapult Launch):
    /// - Con đứng ở đầu thấp của tấm ván bập bênh (Child Seat Point).
    /// - Ba leo lên mỏm đá cao dộng toàn bộ thân người nặng 85kg xuống đầu cao (Dad Slam Point).
    /// - Tấm ván đập mạnh xuống (Heavy Thud + Camera Shake + Dust VFX), hất tung Con bay bổng lên gờ vách đá cao!
    /// - Con tiếp đất an toàn trên đỉnh cao và kích hoạt đường cho Ba đi qua.
    /// </summary>
    [SelectionBase]
    public class CoopSeesawCatapult : MonoBehaviour
    {
        [Header("Seesaw Transforms")]
        [Tooltip("Trục xoay tấm ván bập bênh")]
        [SerializeField] private Transform seesawBeam;

        [Tooltip("Điểm Con đứng chuẩn bị đón lực đẩy")]
        [SerializeField] private Transform childSeatPoint;

        [Tooltip("Điểm Ba tiếp đất dộng lực đòn bẩy")]
        [SerializeField] private Transform dadSlamPoint;

        [Tooltip("Vị trí gờ cao Con được phóng tới")]
        [SerializeField] private Transform launchTargetLanding;

        [Header("Catapult Settings")]
        [Tooltip("Thời gian Con bay trên không (giây)")]
        [SerializeField] private float flightDuration = 1.35f;

        [Tooltip("Độ cao đỉnh parabol khi bay (mét)")]
        [SerializeField] private float arcPeakHeight = 5.5f;

        [Tooltip("Góc nghiêng ban đầu của bập bênh")]
        [SerializeField] private float tiltAngleInitial = -15f;

        [Tooltip("Góc nghiêng sau khi Ba dộng xuống")]
        [SerializeField] private float tiltAngleSlammed = 18f;

        [Header("State Status")]
        [SerializeField] private bool isChildSeated = false;
        [SerializeField] private bool isLaunched = false;
        [SerializeField] private bool isCompleted = false;

        public bool IsCompleted => isCompleted;
        public bool IsChildSeated => isChildSeated;

        private CharacterController m_SeatedChild;
        private Light m_GuideLight;

        private void Awake()
        {
            if (seesawBeam != null)
            {
                seesawBeam.localRotation = Quaternion.Euler(tiltAngleInitial, 0f, 0f);
            }
            SetupVisualAffordance();
        }

        private void SetupVisualAffordance()
        {
            GameObject lightObj = new GameObject("Catapult_Affordance_Light");
            lightObj.transform.SetParent(transform);
            lightObj.transform.position = (childSeatPoint != null ? childSeatPoint.position : transform.position) + Vector3.up * 2.5f;
            m_GuideLight = lightObj.AddComponent<Light>();
            m_GuideLight.type = LightType.Point;
            m_GuideLight.color = new Color(1.0f, 0.75f, 0.3f);
            m_GuideLight.range = 6f;
            m_GuideLight.intensity = 1.8f;
        }

        private void Update()
        {
            if (isCompleted || isLaunched) return;

            // 1. Kiểm tra Con bước vào vị trí ngồi trên bập bênh
            if (!isChildSeated)
            {
                CheckChildEnteringSeat();
            }
            else
            {
                // Khi Con đã ngồi, kiểm tra Ba dộng xuống đầu bên kia
                CheckDadSlamAction();
            }
        }

        private void CheckChildEnteringSeat()
        {
            if (childSeatPoint == null) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if (Vector3.Distance(p.transform.position, childSeatPoint.position) <= 2.2f)
                {
                    bool isDad = p.name.ToLower().Contains("dad");
                    if (p.TryGetComponent<PlayerRoleVisuals>(out var visuals))
                    {
                        isDad = visuals.IsDad;
                    }

                    if (!isDad) // Là Con
                    {
                        isChildSeated = true;
                        m_SeatedChild = p.GetComponent<CharacterController>();
                        Debug.Log("<color=cyan>[Catapult] Con đã đứng vào bập bênh! Ba hãy nhảy dộng từ mỏm đá cao xuống!</color>");
                        break;
                    }
                }
            }
        }

        private void CheckDadSlamAction()
        {
            if (dadSlamPoint == null) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                bool isDad = p.name.ToLower().Contains("dad");
                if (p.TryGetComponent<PlayerRoleVisuals>(out var visuals))
                {
                    isDad = visuals.IsDad;
                }

                if (isDad)
                {
                    float dist = Vector3.Distance(p.transform.position, dadSlamPoint.position);
                    if (dist <= 2.5f)
                    {
                        // Ba nhấn [Space] hoặc [F] khi ở điểm dộng bập bênh
                        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E))
                        {
                            TriggerCatapultLaunch();
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Kích hoạt cú dộng toàn thân và hất tung con vút lên cao!
        /// </summary>
        [ContextMenu("Trigger Catapult Launch")]
        public void TriggerCatapultLaunch()
        {
            if (isLaunched || isCompleted) return;
            StartCoroutine(CatapultRoutine());
        }

        private IEnumerator CatapultRoutine()
        {
            isLaunched = true;
            Debug.Log("<color=yellow>[Catapult] RẦM! Ba dộng toàn lực xuống bập bênh! Hất tung Con bay vút lên trời!</color>");

            // 1. Xoay tức thì bập bênh theo lực giáng
            if (seesawBeam != null)
            {
                float slamElapsed = 0f;
                Quaternion startRot = seesawBeam.localRotation;
                Quaternion endRot = Quaternion.Euler(tiltAngleSlammed, 0f, 0f);
                while (slamElapsed < 0.12f)
                {
                    slamElapsed += Time.deltaTime;
                    seesawBeam.localRotation = Quaternion.Slerp(startRot, endRot, slamElapsed / 0.12f);
                    yield return null;
                }
                seesawBeam.localRotation = endRot;
            }

            // 2. Phóng Con theo đường parabol bay bổng
            if (m_SeatedChild != null && launchTargetLanding != null)
            {
                Vector3 startPos = m_SeatedChild.transform.position;
                Vector3 endPos = launchTargetLanding.position;
                float elapsed = 0f;

                // Tắt CharacterController tạm thời để interpolate mượt mà
                m_SeatedChild.enabled = false;

                while (elapsed < flightDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / flightDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);

                    Vector3 currentPos = Vector3.Lerp(startPos, endPos, smoothT);
                    // Đường cong parabol lên cao rồi hạ dần
                    currentPos.y += Mathf.Sin(t * Mathf.PI) * arcPeakHeight;

                    m_SeatedChild.transform.position = currentPos;
                    // Xoay nhẹ nhân vật tạo cảm giác nhào lộn trên không
                    m_SeatedChild.transform.Rotate(Vector3.right, 240f * Time.deltaTime, Space.Self);

                    yield return null;
                }

                m_SeatedChild.transform.position = endPos;
                m_SeatedChild.transform.rotation = Quaternion.LookRotation(launchTargetLanding.forward);
                m_SeatedChild.enabled = true;
            }

            isCompleted = true;
            if (m_GuideLight != null) m_GuideLight.color = Color.green;
            Debug.Log("<color=green>[Catapult] HOÀN THÀNH! Con đã tiếp đất an toàn trên đỉnh cao rực rỡ!</color>");
        }
    }
}
