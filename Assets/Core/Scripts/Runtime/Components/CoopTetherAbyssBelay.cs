using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Co-op "Dây Buộc Sinh Mệnh & Đu Đáy Vực" (Tether Rope & Abyss Belay):
    /// - Ba cắm trụ chân vững chắc trên mỏm vách đá làm "Mỏ neo sống" (Anchor).
    /// - Con buộc dây thừng tụt xuống họng vực sâu để tiếp cận then chốt cơ quan dưới gầm vách đá.
    /// - Con đung đưa người (A/D Swing) như con lắc giữa không trung để với tới then chốt.
    /// - Khi chạm then chốt, Con nhấn [F] nhổ chốt mở đường; Ba bên trên gồng sức kéo dây đưa Con trở lại mặt đất!
    /// </summary>
    [SelectionBase]
    public class CoopTetherAbyssBelay : MonoBehaviour
    {
        [Header("Anchor & Gorge Setup")]
        [Tooltip("Điểm Ba đứng cắm trụ giữ dây trên mỏm đá")]
        [SerializeField] private Transform dadAnchorPoint;

        [Tooltip("Điểm Con bắt đầu tụt xuống mép vực")]
        [SerializeField] private Transform cliffEdgeDropPoint;

        [Tooltip("Cơ quan then chốt dưới gầm vực cần Con đu tới gạt")]
        [SerializeField] private Transform abyssMechanismLatch;

        [Tooltip("Cầu hoặc cửa đá mở ra sau khi nhổ chốt")]
        [SerializeField] private Transform unlockedBridgeOrGate;

        [Header("Tether Cable Visual")]
        [SerializeField] private LineRenderer tetherRopeLine;
        [SerializeField] private Color ropeColor = new Color(0.85f, 0.7f, 0.45f);

        [Header("Rope Physics & Swing Settings")]
        [SerializeField] private float maxRopeLength = 8.5f;
        [SerializeField] private float minRopeLength = 2.0f;
        [SerializeField] private float swingForce = 4.5f;
        [SerializeField] private float currentRopeLength = 5.0f;

        [Header("State Status")]
        [SerializeField] private bool isTetherActive = false;
        [SerializeField] private bool isLatchUnlocked = false;
        [SerializeField] private bool isCompleted = false;

        public bool IsCompleted => isCompleted;
        public bool IsLatchUnlocked => isLatchUnlocked;

        private CharacterController m_RappellingChild;
        private Transform m_AnchorDad;
        private float m_SwingAngle = 0f;
        private float m_SwingVelocity = 0f;

        private void Awake()
        {
            SetupRopeRenderer();
        }

        private void SetupRopeRenderer()
        {
            if (tetherRopeLine == null)
            {
                tetherRopeLine = gameObject.AddComponent<LineRenderer>();
                tetherRopeLine.startWidth = 0.06f;
                tetherRopeLine.endWidth = 0.06f;
                tetherRopeLine.material = new Material(Shader.Find("Sprites/Default"));
                tetherRopeLine.startColor = ropeColor;
                tetherRopeLine.endColor = ropeColor;
                tetherRopeLine.positionCount = 2;
                tetherRopeLine.enabled = false;
            }
        }

        private void Update()
        {
            if (isCompleted) return;

            if (!isTetherActive)
            {
                CheckInitiateTether();
            }
            else
            {
                UpdateTetherPhysics();
                CheckLatchInteraction();
            }
        }

        private void CheckInitiateTether()
        {
            if (dadAnchorPoint == null || cliffEdgeDropPoint == null) return;

            var players = GameObject.FindGameObjectsWithTag("Player");
            bool dadAtAnchor = false;
            GameObject childObj = null;

            foreach (var p in players)
            {
                bool isDad = p.name.ToLower().Contains("dad");
                if (p.TryGetComponent<PlayerRoleVisuals>(out var visuals))
                {
                    isDad = visuals.IsDad;
                }

                if (isDad && Vector3.Distance(p.transform.position, dadAnchorPoint.position) <= 2.5f)
                {
                    dadAtAnchor = true;
                    m_AnchorDad = p.transform;
                }
                else if (!isDad && Vector3.Distance(p.transform.position, cliffEdgeDropPoint.position) <= 2.5f)
                {
                    childObj = p;
                }
            }

            // Khi cả hai cha con đã sẵn sàng tại mép vực
            if (dadAtAnchor && childObj != null)
            {
                bool isInteract = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                {
                    isInteract = true;
                }
                if (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame))
                {
                    isInteract = true;
                }
#else
                isInteract = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#endif
                if (isInteract)
                {
                    StartTetherRappelling(childObj.GetComponent<CharacterController>());
                }
            }
        }

        private void StartTetherRappelling(CharacterController child)
        {
            isTetherActive = true;
            m_RappellingChild = child;
            currentRopeLength = 4.5f;
            m_SwingAngle = 0f;
            m_SwingVelocity = 0f;

            if (tetherRopeLine != null) tetherRopeLine.enabled = true;
            if (m_RappellingChild != null) m_RappellingChild.enabled = false;

            Debug.Log("<color=cyan>[Tether] Ba đã ghì chặt dây neo! Con tụt xuống vực. Dùng A/D đu đưa người, S/W thả/thu dây!</color>");
        }

        private void UpdateTetherPhysics()
        {
            if (m_RappellingChild == null || m_AnchorDad == null) return;

            // Điều khiển thả/thu dây (W/S)
            float vertInput = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) vertInput += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) vertInput -= 1f;
            }
            if (Gamepad.current != null)
            {
                vertInput += Gamepad.current.leftStick.y.ReadValue();
            }
#else
            vertInput = Input.GetAxisRaw("Vertical");
#endif
            currentRopeLength += vertInput * 2.5f * Time.deltaTime;
            currentRopeLength = Mathf.Clamp(currentRopeLength, minRopeLength, maxRopeLength);

            // Điều khiển đu đưa người (A/D) theo con lắc vật lý
            float horizInput = 0f;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) horizInput -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) horizInput += 1f;
            }
            if (Gamepad.current != null)
            {
                horizInput += Gamepad.current.leftStick.x.ReadValue();
            }
#else
            horizInput = Input.GetAxisRaw("Horizontal");
#endif
            m_SwingVelocity += horizInput * swingForce * Time.deltaTime;
            // Lực kéo phục hồi về trung tâm (trọng lực con lắc)
            m_SwingVelocity -= Mathf.Sin(m_SwingAngle * Mathf.Deg2Rad) * 14f * Time.deltaTime;
            // Ma sát không khí
            m_SwingVelocity *= Mathf.Clamp01(1.0f - (0.8f * Time.deltaTime));
            m_SwingAngle += m_SwingVelocity * Time.deltaTime * 50f;
            m_SwingAngle = Mathf.Clamp(m_SwingAngle, -55f, 55f);

            // Tính vị trí 3D của Con dưới đầu dây
            Vector3 anchorPos = m_AnchorDad.position + Vector3.up * 0.8f;
            Vector3 swingDir = Quaternion.AngleAxis(m_SwingAngle, Vector3.forward) * Vector3.down;
            Vector3 childTargetPos = anchorPos + swingDir * currentRopeLength;

            m_RappellingChild.transform.position = childTargetPos;
            m_RappellingChild.transform.rotation = Quaternion.LookRotation(Vector3.forward, -swingDir);

            // Cập nhật đường dây thừng LineRenderer
            if (tetherRopeLine != null)
            {
                tetherRopeLine.SetPosition(0, anchorPos);
                tetherRopeLine.SetPosition(1, childTargetPos);
            }
        }

        private void CheckLatchInteraction()
        {
            if (isLatchUnlocked || abyssMechanismLatch == null || m_RappellingChild == null) return;

            float dist = Vector3.Distance(m_RappellingChild.transform.position, abyssMechanismLatch.position);
            if (dist <= 2.2f)
            {
                // Khi Con đu tới gần chốt cơ quan và bấm [F] / [E] / [Space]
                bool isInteract = false;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null && (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                {
                    isInteract = true;
                }
                if (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame))
                {
                    isInteract = true;
                }
#else
                isInteract = Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#endif
                if (isInteract)
                {
                    UnlockLatchAndHaulUp();
                }
            }
        }

        private void UnlockLatchAndHaulUp()
        {
            isLatchUnlocked = true;
            Debug.Log("<color=green>[Tether] TÁCH! Con đã nhổ thành công chốt then dưới đáy vực! Ba kéo Con lên an toàn!</color>");

            // Mở cầu hoặc hạ cổng
            if (unlockedBridgeOrGate != null)
            {
                unlockedBridgeOrGate.gameObject.SetActive(true);
            }

            StartCoroutine(HaulChildUpRoutine());
        }

        private IEnumerator HaulChildUpRoutine()
        {
            Vector3 startPos = m_RappellingChild.transform.position;
            Vector3 topPos = cliffEdgeDropPoint != null ? cliffEdgeDropPoint.position : m_AnchorDad.position + Vector3.right * 1.5f;
            float haulElapsed = 0f;
            float haulDuration = 1.2f;

            while (haulElapsed < haulDuration)
            {
                haulElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(haulElapsed / haulDuration);
                m_RappellingChild.transform.position = Vector3.Lerp(startPos, topPos, Mathf.SmoothStep(0f, 1f, t));
                if (tetherRopeLine != null && m_AnchorDad != null)
                {
                    tetherRopeLine.SetPosition(0, m_AnchorDad.position + Vector3.up * 0.8f);
                    tetherRopeLine.SetPosition(1, m_RappellingChild.transform.position);
                }
                yield return null;
            }

            m_RappellingChild.transform.position = topPos;
            m_RappellingChild.transform.rotation = Quaternion.identity;
            m_RappellingChild.enabled = true;

            if (tetherRopeLine != null) tetherRopeLine.enabled = false;
            isCompleted = true;
            Debug.Log("<color=yellow>[Tether] HOÀN THÀNH! Cả hai Cha Con đoàn tụ an toàn trên đỉnh vách đá!</color>");
        }
    }
}
