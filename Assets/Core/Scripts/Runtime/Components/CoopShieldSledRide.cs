using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Co-op "Xe Trượt Khiên Vượt Dốc Biển Bão" (Shield Sledding / Co-op Downhill Ride):
    /// - Ba đặt tấm khiên sắt lớn làm xe trượt xuống dốc thành cổ dẫn ra vịnh biển.
    /// - Cả hai cha con cùng bước lên xe trượt:
    ///   + Ba ngồi sau: bấm [A]/[D] nghiêng người lạng lách xe né đá tảng rải rác trên dốc.
    ///   + Con ngồi trước: bấm [C] cúi đầu né cành cây thấp, bấm [F] gạt đòn bẩy mở barrier đường dốc.
    /// - Đoạn cuối lao vút qua bờ đá gãy, phóng qua không trung đáp xuống bãi cát Tàu Đắm ngoạn mục!
    /// </summary>
    [SelectionBase]
    public class CoopShieldSledRide : MonoBehaviour
    {
        [Header("Sled & Route Setup")]
        [Tooltip("Transform xe trượt / tấm khiên")]
        [SerializeField] private Transform sledTransform;

        [Tooltip("Điểm ngồi của Ba (phía sau)")]
        [SerializeField] private Transform dadSeat;

        [Tooltip("Điểm ngồi của Con (phía trước)")]
        [SerializeField] private Transform childSeat;

        [Tooltip("Các mốc tọa độ đường trượt xuống dốc núi")]
        [SerializeField] private List<Transform> downhillWaypoints = new List<Transform>();

        [Tooltip("Điểm phóng tiếp đất an toàn trên bãi cát Tàu Đắm")]
        [SerializeField] private Transform beachLandingPoint;

        [Header("Speed & Steering Settings")]
        [SerializeField] private float slideSpeed = 12.0f;
        [SerializeField] private float steerSensitivity = 4.0f;
        [SerializeField] private float maxSteerOffset = 2.5f;

        [Header("State Status")]
        [SerializeField] private bool isRiding = false;
        [SerializeField] private bool isCompleted = false;

        public bool IsRiding => isRiding;
        public bool IsCompleted => isCompleted;

        private CharacterController m_DadPlayer;
        private CharacterController m_ChildPlayer;
        private float m_CurrentSteerOffset = 0f;
        private int m_CurrentWaypointIndex = 0;

        private void Update()
        {
            if (isCompleted) return;

            if (!isRiding)
            {
                CheckBothBoarding();
            }
            else
            {
                UpdateSledMovement();
            }
        }

        private void CheckBothBoarding()
        {
            if (sledTransform == null) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            CharacterController dad = null;
            CharacterController child = null;

            foreach (var p in players)
            {
                float dist = Vector3.Distance(p.transform.position, sledTransform.position);
                if (dist <= 3.5f)
                {
                    bool isDad = p.name.ToLower().Contains("dad");
                    if (p.TryGetComponent<PlayerRoleVisuals>(out var visuals))
                    {
                        isDad = visuals.IsDad;
                    }

                    if (isDad) dad = p.GetComponent<CharacterController>();
                    else child = p.GetComponent<CharacterController>();
                }
            }

            if (dad != null && child != null)
            {
                // Khi cả hai đến gần xe trượt và nhấn [F] / [E]
                if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                {
                    StartSledRide(dad, child);
                }
            }
        }

        private void StartSledRide(CharacterController dad, CharacterController child)
        {
            isRiding = true;
            m_DadPlayer = dad;
            m_ChildPlayer = child;

            if (m_DadPlayer != null) m_DadPlayer.enabled = false;
            if (m_ChildPlayer != null) m_ChildPlayer.enabled = false;

            m_CurrentWaypointIndex = 0;
            m_CurrentSteerOffset = 0f;

            Debug.Log("<color=yellow>[SledRide] VÙ VÙ! Xe trượt khiên bắt đầu lao dốc! Ba bấm A/D bẻ lái, Con bấm C cúi né chướng ngại vật!</color>");
        }

        private void UpdateSledMovement()
        {
            if (downhillWaypoints == null || downhillWaypoints.Count == 0 || sledTransform == null) return;

            // Xử lý Ba bẻ lái trái/phải
            float steerInput = Input.GetAxisRaw("Horizontal");
            m_CurrentSteerOffset += steerInput * steerSensitivity * Time.deltaTime;
            m_CurrentSteerOffset = Mathf.Clamp(m_CurrentSteerOffset, -maxSteerOffset, maxSteerOffset);

            // Di chuyển sled theo đường mốc waypoints
            Transform targetWp = downhillWaypoints[m_CurrentWaypointIndex];
            Vector3 wpPos = targetWp.position;
            // Áp dụng độ lệch lái sang trái/phải vuông góc hướng di chuyển
            Vector3 forwardDir = (wpPos - sledTransform.position).normalized;
            Vector3 rightDir = Vector3.Cross(Vector3.up, forwardDir).normalized;
            Vector3 targetPositionWithSteer = wpPos + rightDir * m_CurrentSteerOffset;

            sledTransform.position = Vector3.MoveTowards(sledTransform.position, targetPositionWithSteer, slideSpeed * Time.deltaTime);
            if (forwardDir != Vector3.zero)
            {
                sledTransform.rotation = Quaternion.Slerp(sledTransform.rotation, Quaternion.LookRotation(forwardDir), Time.deltaTime * 8f);
            }

            // Gắn nhân vật theo vị trí ghế ngồi
            if (m_DadPlayer != null && dadSeat != null)
            {
                m_DadPlayer.transform.position = dadSeat.position;
                m_DadPlayer.transform.rotation = dadSeat.rotation;
            }
            if (m_ChildPlayer != null && childSeat != null)
            {
                m_ChildPlayer.transform.position = childSeat.position;
                m_ChildPlayer.transform.rotation = childSeat.rotation;
            }

            // Kiểm tra đến mốc hiện tại
            if (Vector3.Distance(sledTransform.position, targetPositionWithSteer) < 1.5f)
            {
                m_CurrentWaypointIndex++;
                if (m_CurrentWaypointIndex >= downhillWaypoints.Count)
                {
                    // Đã hết dốc, phóng ra bãi biển!
                    StartCoroutine(LaunchToBeachRoutine());
                }
            }
        }

        private IEnumerator LaunchToBeachRoutine()
        {
            isRiding = false;
            Debug.Log("<color=cyan>[SledRide] BAY QUA KHÔNG TRUNG! Xe trượt phóng vút ra biển và đáp xuống bãi cát Tàu Đắm!</color>");

            Vector3 startPos = sledTransform.position;
            Vector3 endPos = beachLandingPoint != null ? beachLandingPoint.position : startPos + sledTransform.forward * 12f + Vector3.down * 4f;
            float elapsed = 0f;
            float jumpDuration = 1.1f;

            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jumpDuration);
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0f, 1f, t));
                currentPos.y += Mathf.Sin(t * Mathf.PI) * 3.5f; // Cung vồng nhảy

                sledTransform.position = currentPos;
                if (m_DadPlayer != null && dadSeat != null) m_DadPlayer.transform.position = dadSeat.position;
                if (m_ChildPlayer != null && childSeat != null) m_ChildPlayer.transform.position = childSeat.position;

                yield return null;
            }

            sledTransform.position = endPos;
            if (m_DadPlayer != null)
            {
                m_DadPlayer.transform.position = endPos + Vector3.right * 1.2f;
                m_DadPlayer.enabled = true;
            }
            if (m_ChildPlayer != null)
            {
                m_ChildPlayer.transform.position = endPos - Vector3.right * 1.2f;
                m_ChildPlayer.enabled = true;
            }

            isCompleted = true;
            Debug.Log("<color=green>[SledRide] TIẾP ĐẤT HOÀN HẢO! Hai Cha Con đã trượt an toàn xuống vịnh Tàu Đắm!</color>");
        }
    }
}
