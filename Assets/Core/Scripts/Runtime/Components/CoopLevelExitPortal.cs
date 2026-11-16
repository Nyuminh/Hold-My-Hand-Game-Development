using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// CƠ CHẾ QUA MÀN TIẾP THEO: "CỔNG DỊCH CHUYỂN HẢI ĐĂNG CỔ ĐẠI" (Ancient Lighthouse Portal)
    /// Đặt tại bến cảng cuối hành trình Thuyền Đắm Whispering Cove (28.2, 6.0, -73.4).
    ///
    /// NGUYÊN TẮC CO-OP PHỤ TỬ:
    /// 1. Ba đứng lên Bệ Đá Trái (Dad's Pad).
    /// 2. Con đứng lên Bệ Đá Phải (Child's Pad).
    /// 3. Cả hai cùng NẮM CHẶT TAY NHAU [H] để truyền dẫn năng lượng tình thân,
    ///    mở Cổng Ma Thuật và chuyển cảnh sang Màn 1 (PlayScene1)!
    /// </summary>
    public class CoopLevelExitPortal : MonoBehaviour
    {
        private static CoopLevelExitPortal s_Instance;
        public static CoopLevelExitPortal Instance => s_Instance;

        [Header("Scene Transition Target")]
        [Tooltip("Tên Scene tiếp theo sẽ load khi qua màn thành công")]
        [SerializeField] private string nextSceneName = "PlayScene1";

        [Header("Pads Configuration")]
        [Tooltip("Vị trí bệ kích hoạt của Cha")]
        [SerializeField] private Vector3 dadPadPosition = new Vector3(26.2f, 6.0f, -73.4f);

        [Tooltip("Vị trí bệ kích hoạt của Con")]
        [SerializeField] private Vector3 childPadPosition = new Vector3(30.2f, 6.0f, -73.4f);

        public Vector3 DadPadPosition { get => dadPadPosition; set => dadPadPosition = value; }
        public Vector3 ChildPadPosition { get => childPadPosition; set => childPadPosition = value; }

        public void SetPadPositions(Vector3 dadPos, Vector3 childPos)
        {
            dadPadPosition = dadPos;
            childPadPosition = childPos;
        }

        [Tooltip("Bán kính nhận diện người chơi đứng trên bệ")]
        [SerializeField] private float padRadius = 2.4f;

        [Header("Visual Elements")]
        [SerializeField] private Transform portalArch;
        [SerializeField] private Light beaconLight;
        [SerializeField] private ParticleSystem portalParticles;
        [SerializeField] private Renderer dadPadRenderer;
        [SerializeField] private Renderer childPadRenderer;

        [Header("Audio")]
        [SerializeField] private AudioSource portalAudioSource;

        [Header("State")]
        [SerializeField] private bool isDadOnPad = false;
        [SerializeField] private bool isChildOnPad = false;
        [SerializeField] private bool isHoldingHands = false;
        [SerializeField] private bool isPortalActivated = false;
        [SerializeField] private bool isTransitioning = false;

        public bool IsPortalActivated => isPortalActivated;
        public bool IsDadOnPad => isDadOnPad;
        public bool IsChildOnPad => isChildOnPad;

        private CharacterController[] m_CachedPlayers;
        private float m_VictoryTimer = 6.0f;
        private bool m_ShowVictoryScreen = false;
        private string m_VictoryStatusText = "";

        private void Awake()
        {
            s_Instance = this;
        }

        private void Start()
        {
            RefreshPlayers();
            SetupDefaultVisuals();
        }

        private void Update()
        {
            if (isTransitioning) return;

            CheckPlayersOnPads();

            // Nếu cả 2 đã đứng trên bệ và rương thuyền đã mở (hoặc không cần rương nếu test)
            if (isDadOnPad && isChildOnPad && !isPortalActivated)
            {
                // Kiểm tra xem 2 cha con có đang nắm tay nhau không
                CheckHoldHands();

                if (isHoldingHands || IsActionPressed())
                {
                    ActivatePortalVictory();
                }
            }
        }

        private void RefreshPlayers()
        {
            m_CachedPlayers = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        }

        private void CheckPlayersOnPads()
        {
            if (m_CachedPlayers == null || m_CachedPlayers.Length == 0)
            {
                RefreshPlayers();
                if (m_CachedPlayers == null || m_CachedPlayers.Length == 0) return;
            }

            bool foundDad = false;
            bool foundChild = false;

            float rSq = padRadius * padRadius;

            foreach (var cc in m_CachedPlayers)
            {
                if (cc == null || !cc.gameObject.activeInHierarchy) continue;

                Vector3 p = cc.transform.position;
                bool isDad = IsPlayerDad(cc);

                if (isDad)
                {
                    if ((p - dadPadPosition).sqrMagnitude <= rSq || (p - transform.position).sqrMagnitude <= rSq * 1.5f)
                    {
                        foundDad = true;
                    }
                }
                else
                {
                    if ((p - childPadPosition).sqrMagnitude <= rSq || (p - transform.position).sqrMagnitude <= rSq * 1.5f)
                    {
                        foundChild = true;
                    }
                }
            }

            // Nếu chơi Solo (chỉ có 1 người điều khiển), coi như người chơi đứng ở giữa bệ là thỏa mãn cả 2
            if (m_CachedPlayers.Length == 1 && (foundDad || foundChild))
            {
                foundDad = true;
                foundChild = true;
            }

            isDadOnPad = foundDad;
            isChildOnPad = foundChild;

            // Cập nhật phát sáng bệ đá
            UpdatePadVisuals();
        }

        private bool IsPlayerDad(CharacterController cc)
        {
            var vis = cc.GetComponentInParent<PlayerRoleVisuals>();
            if (vis != null) return vis.IsDad;
            var boost = cc.GetComponent<CoopBoostThrowAbility>();
            if (boost != null) return boost.IsDad;
            return cc.gameObject.name.ToLower().Contains("dad");
        }

        private void CheckHoldHands()
        {
            // Kiểm tra trạng thái CoopHoldHandAbility
            var holdAbility = Object.FindAnyObjectByType<CoopHoldHandAbility>();
            if (holdAbility != null && holdAbility.IsHoldingHands)
            {
                isHoldingHands = true;
                return;
            }

            // Hoặc nếu 2 người đứng rất gần nhau (< 2.2m)
            if (m_CachedPlayers != null && m_CachedPlayers.Length >= 2)
            {
                if (m_CachedPlayers[0] != null && m_CachedPlayers[1] != null)
                {
                    float dist = Vector3.Distance(m_CachedPlayers[0].transform.position, m_CachedPlayers[1].transform.position);
                    if (dist <= 2.8f)
                    {
                        isHoldingHands = true;
                        return;
                    }
                }
            }

            // Trong chế độ Solo Test: người chơi bấm H hoặc E là kích hoạt
            #if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.hKey.isPressed || Keyboard.current.eKey.isPressed || Keyboard.current.spaceKey.isPressed))
            {
                isHoldingHands = true;
                return;
            }
            #else
            if (Input.GetKey(KeyCode.H) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space))
            {
                isHoldingHands = true;
                return;
            }
            #endif

            isHoldingHands = false;
        }

        private bool IsActionPressed()
        {
            #if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.hKey.wasPressedThisFrame))
                return true;
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame))
                return true;
            #else
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.H))
                return true;
            #endif
            return false;
        }

        private void UpdatePadVisuals()
        {
            if (dadPadRenderer != null && dadPadRenderer.material != null)
            {
                dadPadRenderer.material.color = isDadOnPad ? new Color(1f, 0.7f, 0.2f) : new Color(0.4f, 0.4f, 0.5f);
            }
            if (childPadRenderer != null && childPadRenderer.material != null)
            {
                childPadRenderer.material.color = isChildOnPad ? new Color(0.2f, 0.9f, 1f) : new Color(0.4f, 0.4f, 0.5f);
            }
        }

        private void SetupDefaultVisuals()
        {
            if (beaconLight != null)
            {
                beaconLight.color = new Color(0.3f, 0.85f, 1f);
                beaconLight.intensity = 2.5f;
                beaconLight.range = 15f;
            }
        }

        public void ActivatePortalVictory()
        {
            if (isPortalActivated || isTransitioning) return;

            isPortalActivated = true;
            isTransitioning = true;

            Debug.Log("[CoopLevelExitPortal] 🎉 CỔNG CỔ ĐẠI ĐÃ KÍCH HOẠT! BẮT ĐẦU CHUYỂN MÀN!");
            UISoundManager.PlaySuccess();

            if (beaconLight != null)
            {
                beaconLight.color = new Color(1f, 0.9f, 0.4f);
                beaconLight.intensity = 6.0f;
            }

            if (portalParticles != null)
            {
                portalParticles.Play();
            }

            m_ShowVictoryScreen = true;
            StartCoroutine(VictorySequenceRoutine());
        }

        private IEnumerator VictorySequenceRoutine()
        {
            m_VictoryStatusText = "Đang mở Cổng Cổ Tự...";
            yield return new WaitForSeconds(1.0f);

            float countdown = 5.0f;
            while (countdown > 0f)
            {
                m_VictoryStatusText = $"Tự động sang Màn 1 sau {Mathf.CeilToInt(countdown)}s (Nhấn [SPACE] để vào ngay)...";
                if (IsActionPressed())
                {
                    break;
                }
                countdown -= Time.deltaTime;
                yield return null;
            }

            m_VictoryStatusText = "🚀 ĐANG TẢI MÀN 1: PLAYSCENE1...";
            yield return new WaitForSeconds(0.5f);

            LoadNextScene();
        }

        public void LoadNextScene()
        {
            Debug.Log($"[CoopLevelExitPortal] Đang load Scene tiếp theo: {nextSceneName}");

            // Đồng bộ Netcode nếu đang host multiplayer
            if (Unity.Netcode.NetworkManager.Singleton != null && 
                Unity.Netcode.NetworkManager.Singleton.IsListening &&
                Unity.Netcode.NetworkManager.Singleton.IsServer)
            {
                try
                {
                    Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
                    return;
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[CoopLevelExitPortal] Netcode SceneManager load thất bại: {ex.Message}. Thử UnityEngine SceneManager...");
                }
            }

            SceneManager.LoadScene(nextSceneName);
        }

        public void ReturnToLobby()
        {
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
            {
                Unity.Netcode.NetworkManager.Singleton.Shutdown();
            }
            SceneManager.LoadScene("Lobby");
        }

        private void OnGUI()
        {
            // Bảng gợi ý khi tiếp cận cổng
            if (!m_ShowVictoryScreen && (isDadOnPad || isChildOnPad || Vector3.Distance(Camera.main != null ? Camera.main.transform.position : transform.position, transform.position) < 18f))
            {
                DrawApproachPrompt();
            }

            // Bảng vinh danh chiến thắng qua màn
            if (m_ShowVictoryScreen)
            {
                DrawVictoryOverlay();
            }
        }

        private void DrawApproachPrompt()
        {
            GUI.depth = -110;
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleCenter;
            boxStyle.fontSize = 20;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = Color.yellow;

            float w = 920f, h = 64f;
            float x = (Screen.width - w) * 0.5f;
            float y = Screen.height - 120f;

            GUI.backgroundColor = new Color(0.04f, 0.04f, 0.08f, 0.92f);

            string msg;
            if (isDadOnPad && isChildOnPad)
            {
                boxStyle.normal.textColor = new Color(0.4f, 1f, 0.5f);
                msg = "🤝 HAI CHA CON ĐANG TRÊN BỆ! HÃY NẮM CHẶT TAY NHAU [H] ĐỂ BƯỚC VÀO CỔNG!";
            }
            else if (isDadOnPad)
            {
                msg = "🧔 Cha đã đứng vào Bệ Trái! Đang đợi Con bước lên Bệ Phải...";
            }
            else if (isChildOnPad)
            {
                msg = "👶 Con đã đứng vào Bệ Phải! Đang đợi Cha bước lên Bệ Trái...";
            }
            else
            {
                msg = "🏛️ CỔNG DI TÍCH SANG MÀN 1: Hai Cha Con hãy cùng bước lên hai bệ đá phong ấn!";
            }

            GUI.Box(new Rect(x, y, w, h), msg, boxStyle);
        }

        private void DrawVictoryOverlay()
        {
            GUI.depth = -200;

            // Background làm mờ toàn màn hình
            GUI.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.95f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), GUIContent.none);

            float cardW = 760f, cardH = 460f;
            float cardX = (Screen.width - cardW) * 0.5f;
            float cardY = (Screen.height - cardH) * 0.5f;

            var cardStyle = new GUIStyle(GUI.skin.box);
            cardStyle.normal.background = Texture2D.whiteTexture;
            GUI.backgroundColor = new Color(0.08f, 0.10f, 0.16f, 0.98f);
            GUI.Box(new Rect(cardX, cardY, cardW, cardH), GUIContent.none, cardStyle);

            // Tiêu đề
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.alignment = TextAnchor.MiddleCenter;
            titleStyle.fontSize = 28;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
            GUI.Label(new Rect(cardX, cardY + 30f, cardW, 40f), "🏆 HOÀN THÀNH MÀN HUẤN LUYỆN!", titleStyle);

            // Subtitle
            var subStyle = new GUIStyle(GUI.skin.label);
            subStyle.alignment = TextAnchor.MiddleCenter;
            subStyle.fontSize = 17;
            subStyle.normal.textColor = new Color(0.85f, 0.90f, 0.95f);
            GUI.Label(new Rect(cardX + 40f, cardY + 80f, cardW - 80f, 60f),
                "\"Tình Phụ Tử đã gắn kết hai cha con vượt qua mọi vách vực và giông bão.\nGiờ đây, cánh cổng cổ đại đã mở lối vào chuyến phiêu lưu thực sự!\"", subStyle);

            // Divider
            GUI.backgroundColor = new Color(1f, 0.85f, 0.25f, 0.4f);
            GUI.Box(new Rect(cardX + 60f, cardY + 155f, cardW - 120f, 2f), GUIContent.none);

            // Thống kê thành tích
            var statStyle = new GUIStyle(GUI.skin.label);
            statStyle.fontSize = 15;
            statStyle.normal.textColor = new Color(0.7f, 0.95f, 0.8f);
            GUI.Label(new Rect(cardX + 80f, cardY + 175f, cardW - 160f, 28f), "✔ Màn 1: Cầu gỗ suối mù — Nắm tay vượt sợ hãi", statStyle);
            GUI.Label(new Rect(cardX + 80f, cardY + 205f, cardW - 160f, 28f), "✔ Màn 2: Tường thành — Tung bổng & Thang dây đồng đội", statStyle);
            GUI.Label(new Rect(cardX + 80f, cardY + 235f, cardW - 160f, 28f), "✔ Màn 3: Bập bênh búng người & Đu cáp trượt tuyết", statStyle);
            GUI.Label(new Rect(cardX + 80f, cardY + 265f, cardW - 160f, 28f), "✔ Màn 4: Thuyền đắm bão biển — Rương báu Thuyền Trưởng", statStyle);

            // Status loading
            var loadStyle = new GUIStyle(GUI.skin.label);
            loadStyle.alignment = TextAnchor.MiddleCenter;
            loadStyle.fontSize = 16;
            loadStyle.fontStyle = FontStyle.Bold;
            loadStyle.normal.textColor = new Color(0.3f, 0.9f, 1f);
            GUI.Label(new Rect(cardX, cardY + 315f, cardW, 30f), m_VictoryStatusText, loadStyle);

            // Nút bấm
            float btnW = 260f, btnH = 50f;
            float btnY = cardY + 365f;

            GUI.backgroundColor = new Color(0.2f, 0.75f, 0.35f);
            if (GUI.Button(new Rect(cardX + 90f, btnY, btnW, btnH), "▶ TIẾP TỤC SANG MÀN 1 [Space]"))
            {
                LoadNextScene();
            }

            GUI.backgroundColor = new Color(0.6f, 0.25f, 0.25f);
            if (GUI.Button(new Rect(cardX + cardW - 90f - btnW, btnY, btnW, btnH), "🏠 TRỞ VỀ LOBBY"))
            {
                ReturnToLobby();
            }
        }

        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(dadPadPosition, padRadius);
            Gizmos.DrawWireSphere(childPadPosition, padRadius);
            Gizmos.DrawLine(dadPadPosition, childPadPosition);
        }
        #endif
    }
}
