using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ Chế Co-op "Cầu Rút Cổ Khu Vịnh" (Cove Drawbridge) — chốt chặn giữa Khu Vịnh và Khu Mỏ:
    ///
    /// CỐT TRUYỆN:
    /// Bến cảng cổ trong vịnh đã bị bỏ hoang hàng trăm năm. Cầu rút bằng đá nối sang mỏ quặng đã bị
    /// "Chốt Khóa Sắt" ghim chết, còn "Dây Xích Giữ Cầu" bị kẹt nứt treo lủng lẳng chắn ngang lối đi.
    /// Hai cha con phải khôi phục cầu rút để lên đường vào Khu Mỏ:
    ///
    /// PHA 1 — 👶 CON (chui lọt khe hẹp dưới mái che):
    ///   Dưới mái che gỗ của cơ quang tời có một khe hẹp chỉ vừa con bò vào.
    ///   Con chui vào khe, bấm [E] nhổ "Chốt Khóa Sắt" (Cove_Drawbridge_ReleasePin) ra.
    ///   Khi chốt vừa nhổ: dây xích giữ cầu rơi xuống vực, cơ quang tời được giải phóng.
    ///
    /// PHA 2 — 🧔 BA (quay tời nặng):
    ///   Ba đứng cạnh trụ tời, giữ [E / SPACE] dùng sức quay trống tời (Cove_Drawbridge_WinchDrum).
    ///   Đối trọng mất trọng lượng -> cầu rút từ 37° hạ dần xuống phẳng (-30°) để hai cha con đi qua.
    ///
    /// PHA 3 — Hai cha con cùng bước qua cầu phẳng sang bãi đá (Trestle) để vào KHU MỎ.
    ///
    /// Toàn bộ hình học được đo đạc 100% bằng raycast trên scene thật:
    ///   - Bản lề (trục xoay Z thế giới):   H = (-17.30, 15.05, -73.40)
    ///   - Cầu rút (Cove_Drawbridge_Deck):   dựng 37° chắn lối, hạ -30° thì phẳng ~14.3m
    ///   - Đối trọng (Cove_Drawbridge_Counterweight), dây xích giữ cầu (SuspBridge_Release_Chain).
    /// </summary>
    [SelectionBase]
    public class CoveDrawbridgeCoopController : MonoBehaviour
    {
        private static CoveDrawbridgeCoopController instance;
        public static CoveDrawbridgeCoopController Instance => instance;

        #region Serialized Fields

        [Header("Cơ quang tời (pha 2 — Cha quay)")]
        [Tooltip("Trống tời quay (Cove_Drawbridge_WinchDrum)")]
        [SerializeField] private Transform winchDrum;

        [Tooltip("Tay quay tời để Cha nắm (Cove_Drawbridge_CrankHandle)")]
        [SerializeField] private Transform crankHandle;

        [Header("Chốt khóa sắt (pha 1 — Con nhổ)")]
        [Tooltip("Chốt khóa sắt (Cove_Drawbridge_ReleasePin)")]
        [SerializeField] private Transform releasePin;

        [Tooltip("Cần gạt nhỏ bên trong khe (Cove_Drawbridge_PawlLever)")]
        [SerializeField] private Transform pawlLever;

        [Header("Cầu rút & đối trọng")]
        [Tooltip("Mặt cầu rút (Cove_Drawbridge_Deck)")]
        [SerializeField] private Transform drawbridgeDeck;

        [Tooltip("Đối trọng đá (Cove_Drawbridge_Counterweight)")]
        [SerializeField] private Transform counterweight;

        [Tooltip("Dây xích giữ cầu rút (SuspBridge_Release_Chain) - trải đúng trên khoảng trống cầu")]
        [SerializeField] private Transform holdingChain;

        [Header("Xích nối tời với cầu")]
        [Tooltip("Xích bên phải (Cove_Drawbridge_Chain_R)")]
        [SerializeField] private Transform chainR;

        [Tooltip("Xích bên trái (Cove_Drawbridge_Chain_L)")]
        [SerializeField] private Transform chainL;

        [Header("Vị trí tương tác (đo đạc từ scene)")]
        [Tooltip("Vị trí khe hẹp để Con chui vào nhổ chốt (mép nam thap East)")]
        [SerializeField] private Vector3 pawlPos = new Vector3(-11.00f, 15.05f, -74.60f);

        [Tooltip("Vị trí trụ tời để Cha quay")]
        [SerializeField] private Vector3 winchPos = new Vector3(-9.55f, 16.05f, -74.80f);

        [Header("Thông số cơ chế")]
        [Tooltip("Góc cầu lúc dựng (độ, so với pose gốc)")]
        [SerializeField] private float raisedAngle = 0f;

        [Tooltip("Góc cầu khi hạ phẳng (độ)")]
        [SerializeField] private float openAngle = -30f;

        [Tooltip("Thời gian quay tời để mở khóa cầu (giây)")]
        [SerializeField] private float crankDuration = 3.2f;

        [Tooltip("Thời gian hạ cầu (giây)")]
        [SerializeField] private float lowerDuration = 3.0f;

        [Tooltip("Khoảng cách tương tác (mét)")]
        [SerializeField] private float interactDistance = 3.4f;

        [Header("Trạng thái")]
        [SerializeField] private bool isPinReleased = false;
        [SerializeField] private bool isBridgeOpened = false;
        [SerializeField] private float crankProgress = 0f;

        #endregion

        #region Private Fields

        /// <summary>Trục bản lề thế giới — đo trực tiếp từ mesh Cove_Drawbridge_Hinge.</summary>
        private static readonly Vector3 HingePoint = new Vector3(-17.30f, 15.05f, -73.40f);

        private Quaternion m_DeckInitRot;
        private Vector3 m_DeckInitPos;
        private Quaternion m_CounterInitRot;
        private Vector3 m_CounterInitPos;

        /// <summary>Đầu móc cầu (2 điểm) ở pose dựng ban đầu — lưu trong LOCAL SPACE của deck để xoay theo cầu.</summary>
        private Vector3 m_TipLocalR;
        private Vector3 m_TipLocalL;

        private float m_CurrentAngle;
        private bool m_IsCranking;
        private Coroutine m_BridgeRoutine;

        private CharacterController[] cachedControllers;
        private float lastUpdateTime = 0f;

        private string currentPrompt = "";
        private float promptTimer = 0f;
        private Color promptColor = Color.yellow;

        private Light m_SignalLamp;
        private bool m_SignalLampCached;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(this);
                return;
            }
            instance = this;

            AutoFindReferences();
        }

        private void Start()
        {
            CachePlayers();

            if (drawbridgeDeck != null)
            {
                m_DeckInitRot = drawbridgeDeck.rotation;
                m_DeckInitPos = drawbridgeDeck.position;

                // Đầu móc cầu: 2 điểm ở mép đông (max.x) của mesh, lưu LOCAL để xoay theo cầu
                var b = drawbridgeDeck.GetComponentInChildren<Renderer>().bounds;
                Vector3 tipWorldR = new Vector3(b.max.x, b.max.y, -72.57f);
                Vector3 tipWorldL = new Vector3(b.max.x, b.max.y, -74.24f);
                m_TipLocalR = drawbridgeDeck.transform.InverseTransformPoint(tipWorldR);
                m_TipLocalL = drawbridgeDeck.transform.InverseTransformPoint(tipWorldL);
            }

            if (counterweight != null)
            {
                m_CounterInitRot = counterweight.rotation;
                m_CounterInitPos = counterweight.position;
            }

            m_CurrentAngle = raisedAngle;
            ApplyBridgeAngle(raisedAngle);

            if (!isPinReleased) ApplyHoldingChainState(true);
        }

        private void Update()
        {
            if (Time.time - lastUpdateTime > 1.0f)
            {
                CachePlayers();
                lastUpdateTime = Time.time;
            }

            if (promptTimer > 0f)
            {
                promptTimer -= Time.deltaTime;
                if (promptTimer <= 0f) currentPrompt = "";
            }

            HandleInteractions();
            UpdateChainVisuals();
        }

        #endregion

        #region Auto Find References

        public void AutoFindReferences()
        {
            if (drawbridgeDeck == null)
            {
                var d = GameObject.Find("Cove_Drawbridge_Deck");
                if (d != null) drawbridgeDeck = d.transform;
            }

            if (counterweight == null)
            {
                var c = GameObject.Find("Cove_Drawbridge_Counterweight");
                if (c != null) counterweight = c.transform;
            }

            if (holdingChain == null)
            {
                var h = GameObject.Find("SuspBridge_Release_Chain");
                if (h != null) holdingChain = h.transform;
            }

            if (winchDrum == null)
            {
                var w = GameObject.Find("Cove_Drawbridge_WinchDrum");
                if (w != null) winchDrum = w.transform;
            }

            if (crankHandle == null)
            {
                var k = GameObject.Find("Cove_Drawbridge_CrankHandle");
                if (k != null) crankHandle = k.transform;
            }

            if (releasePin == null)
            {
                var p = GameObject.Find("Cove_Drawbridge_ReleasePin");
                if (p != null) releasePin = p.transform;
            }

            if (pawlLever == null)
            {
                var l = GameObject.Find("Cove_Drawbridge_PawlLever");
                if (l != null) pawlLever = l.transform;
            }

            if (chainR == null)
            {
                var r = GameObject.Find("Cove_Drawbridge_Chain_R");
                if (r != null) chainR = r.transform;
            }

            if (chainL == null)
            {
                var l2 = GameObject.Find("Cove_Drawbridge_Chain_L");
                if (l2 != null) chainL = l2.transform;
            }
        }

        private void CachePlayers()
        {
#if UNITY_2023_1_OR_NEWER
            cachedControllers = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
#else
            cachedControllers = Object.FindObjectsOfType<CharacterController>();
#endif
        }

        #endregion

        #region Gameplay

        private void HandleInteractions()
        {
            // ---- ĐÃ MỞ CẦU: chỉ còn dẫn đường ----
            if (isBridgeOpened)
            {
                PlayerRoleVisuals visOpen;
                var nearOpen = GetNearestPlayer(winchPos, 9f, out visOpen);
                if (nearOpen != null)
                {
                    ShowPrompt("✅ Cầu rút đã hạ phẳng! Hai Cha Con cùng đi qua sang bãi đá để vào KHU MỎ.", Color.green, 0.4f);
                }
                return;
            }

            // ---- PHA 1: CON nhổ chốt khóa sắt trong khe hẹp ----
            if (!isPinReleased)
            {
                PlayerRoleVisuals visPawl;
                var playerAtPawl = GetNearestPlayer(pawlPos, interactDistance, out visPawl);
                if (playerAtPawl != null)
                {
                    bool isDad = visPawl != null ? visPawl.IsDad : playerAtPawl.name.ToLower().Contains("dad");
                    if (isDad)
                    {
                        ShowPrompt("🚫 Khe dưới mái che quá hẹp — chỉ 👶 Con mới chui lọt vào nhổ chốt!", new Color(1f, 0.75f, 0.2f));
                    }
                    else
                    {
                        ShowPrompt("👶 [E] Nhổ CHỐT KHÓA SẮT khỏi cơ quang tời", Color.cyan);
                        if (IsInteractPressed())
                        {
                            StartCoroutine(ReleasePinRoutine());
                        }
                    }
                    return;
                }

                // Không ai ở khe: nhắc nhở nếu đứng gần cơ chế
                PlayerRoleVisuals visW0;
                var nearW0 = GetNearestPlayer(winchPos, 7f, out visW0);
                if (nearW0 != null)
                {
                    ShowPrompt("⚙️ Cầu rút bị CHỐT KHÓA SẮT ghim chết. 👶 Con cần chui vào khe hẹp dưới mái che để nhổ chốt!", new Color(1f, 0.65f, 0.1f));
                }
                return;
            }

            // ---- PHA 2: CHA quay tời ----
            if (!isBridgeOpened)
            {
                PlayerRoleVisuals visWinch;
                var playerAtWinch = GetNearestPlayer(winchPos, interactDistance + 1.2f, out visWinch);
                if (playerAtWinch != null)
                {
                    bool isDad = visWinch != null ? visWinch.IsDad : playerAtWinch.name.ToLower().Contains("dad");
                    if (!isDad)
                    {
                        ShowPrompt("🚫 Trống tời quá nặng! Cần sức mạnh của 🧔 Ba để quay!", Color.red);
                    }
                    else
                    {
                        ShowPrompt($"🧔 [Giữ SPACE / E] Dùng sức quay tời mở cầu rút ({Mathf.RoundToInt(crankProgress * 100)}%)", Color.green);
                        if (IsInteractHeld())
                        {
                            PerformCranking();
                        }
                    }
                }
            }
        }

        /// <summary>Cha quay trống tời — quay đều, càng quay càng chắc.</summary>
        private void PerformCranking()
        {
            if (isBridgeOpened) return;
            m_IsCranking = true;

            if (winchDrum != null) winchDrum.Rotate(Vector3.forward, 210f * Time.deltaTime, Space.Self);
            if (crankHandle != null) crankHandle.Rotate(Vector3.forward, 210f * Time.deltaTime, Space.Self);

            crankProgress = Mathf.Clamp01(crankProgress + Time.deltaTime / crankDuration);

            if (crankProgress >= 1f)
            {
                crankProgress = 1f;
                m_IsCranking = false;
                LowerBridgeRoutine();
            }
        }

        /// <summary>Con nhổ chốt khóa -> dây xích giữ cầu rơi xuống vực.</summary>
        private IEnumerator ReleasePinRoutine()
        {
            isPinReleased = true;
            UISoundManager.PlayConfirm();
            ShowPrompt("⚙️ CHỐT KHÓA ĐÃ NHỔ RA! Dây xích giữ cầu đang rơi xuống vực...", Color.yellow, 3.0f);
            Debug.Log("[CoveDrawbridge] Con đã nhổ chốt khóa sắt, cơ quang tời được giải phóng!");

            // Chốt nhổ ra, bay lệch
            if (releasePin != null)
            {
                Vector3 p0 = releasePin.position;
                Quaternion r0 = releasePin.rotation;
                float dur = 0.7f, e = 0f;
                while (e < dur)
                {
                    e += Time.deltaTime;
                    float t = e / dur;
                    releasePin.position = p0 + new Vector3(0f, Mathf.Sin(t * Mathf.PI) * 0.8f, t * 1.6f);
                    releasePin.rotation = r0 * Quaternion.Euler(t * 220f, t * 140f, 0f);
                    releasePin.localScale = Vector3.Lerp(releasePin.localScale, Vector3.one * 0.2f, Time.deltaTime * 3f);
                    yield return null;
                }
                releasePin.gameObject.SetActive(false);
            }

            // Cần gạt nhỏ bật ra
            if (pawlLever != null)
            {
                Quaternion l0 = pawlLever.localRotation;
                Quaternion l1 = l0 * Quaternion.Euler(0f, 0f, -38f);
                float e2 = 0f, d2 = 0.4f;
                while (e2 < d2)
                {
                    e2 += Time.deltaTime;
                    pawlLever.localRotation = Quaternion.Slerp(l0, l1, e2 / d2);
                    yield return null;
                }
                pawlLever.localRotation = l1;
            }

            // Dây xích giữ cầu rơi xuống vực
            if (holdingChain != null)
            {
                var cols = holdingChain.GetComponentsInChildren<Collider>();
                foreach (var c in cols) c.enabled = false;

                Vector3 cp = holdingChain.position;
                float e3 = 0f, d3 = 1.6f;
                while (e3 < d3)
                {
                    e3 += Time.deltaTime;
                    float t = e3 / d3;
                    holdingChain.position = cp + Vector3.down * (t * t * 13f);
                    holdingChain.Rotate(Vector3.forward, 22f * Time.deltaTime, Space.World);
                    yield return null;
                }
                holdingChain.gameObject.SetActive(false);
            }

            UISoundManager.PlaySuccess();
            ShowPrompt("✨ CƠ QUANG TỜI ĐÃ ĐƯỢC GIẢI PHÓNG! 🧔 Ba hãy quay tời để hạ cầu rút!", Color.green, 5.0f);
        }

        /// <summary>Hạ cầu rút từ 37° xuống phẳng (-30°) — đối trọng mất trọng lượng.</summary>
        private void LowerBridgeRoutine()
        {
            if (m_BridgeRoutine != null) StopCoroutine(m_BridgeRoutine);
            m_BridgeRoutine = StartCoroutine(LowerBridgeInner());
        }

        private IEnumerator LowerBridgeInner()
        {
            UISoundManager.PlayConfirm();
            ShowPrompt("⚙️ ĐỐI TRỌNG MẤT TRỌNG LƯỢNG — CẦU RÚT ĐANG HẠ XUỐNG!", Color.yellow, 3.0f);

            float startAngle = m_CurrentAngle;
            float e = 0f;

            while (e < lowerDuration)
            {
                e += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, e / lowerDuration);
                m_CurrentAngle = Mathf.Lerp(startAngle, openAngle, t);
                ApplyBridgeAngle(m_CurrentAngle);

                // Rung nhẹ khi cầu chạm đất
                float rumble = (1f - t) * Mathf.Sin(e * 26f) * 0.035f;
                if (drawbridgeDeck != null) drawbridgeDeck.position += new Vector3(0f, rumble, 0f);

                yield return null;
            }

            ApplyBridgeAngle(openAngle);
            isBridgeOpened = true;

            UISoundManager.PlaySuccess();
            ShowPrompt("✅ CẦU RÚT ĐÃ HẠ PHẲNG! Hai Cha Con cùng đi qua sang bãi đá để vào KHU MỎ!", Color.green, 7.0f);
            Debug.Log("[CoveDrawbridge] Cầu rút đã hạ phẳng, đường vào Khu Mỏ đã mở!");
            SetSignalLamp(Color.green);
        }

        /// <summary>Áp dụng góc cầu: xoay quanh trục bản lề thế giới Z, giữ nguyên pose gốc FBX.</summary>
        private void ApplyBridgeAngle(float angle)
        {
            Quaternion rz = Quaternion.Euler(0f, 0f, angle);

            if (drawbridgeDeck != null)
            {
                drawbridgeDeck.rotation = rz * m_DeckInitRot;
                drawbridgeDeck.position = HingePoint + rz * (m_DeckInitPos - HingePoint);
            }

            if (counterweight != null)
            {
                counterweight.rotation = rz * m_CounterInitRot;
                counterweight.position = HingePoint + rz * (m_CounterInitPos - HingePoint);
            }
        }

        private void ApplyHoldingChainState(bool held)
        {
            if (holdingChain == null) return;
            var cols = holdingChain.GetComponentsInChildren<Collider>();
            foreach (var c in cols) c.enabled = held;
        }

        /// <summary>Xích nối trống tời với đầu móc cầu — bám sát 100% khi cầu hạ.</summary>
        private void UpdateChainVisuals()
        {
            if (drawbridgeDeck == null || winchDrum == null) return;

            // Đầu móc cầu tính bằng transform thực tế của deck (đã xoay theo góc cầu)
            Vector3 tipR = drawbridgeDeck.transform.TransformPoint(m_TipLocalR);
            Vector3 tipL = drawbridgeDeck.transform.TransformPoint(m_TipLocalL);

            Vector3 drumR = winchDrum.position + new Vector3(0f, 0f, 0.55f);
            Vector3 drumL = winchDrum.position + new Vector3(0f, 0f, -0.55f);

            StretchChain(chainR, drumR, tipR);
            StretchChain(chainL, drumL, tipL);
        }

        private void StretchChain(Transform chain, Vector3 a, Vector3 b)
        {
            if (chain == null) return;
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = b - a;
            chain.position = mid;
            if (dir.sqrMagnitude > 0.001f) chain.rotation = Quaternion.LookRotation(dir);
            float len = dir.magnitude;
            if (len > 0.01f) chain.localScale = new Vector3(chain.localScale.x, chain.localScale.y, len);
        }

        private void SetSignalLamp(Color c)
        {
            if (!m_SignalLampCached)
            {
                var lampGo = GameObject.Find("Cove_Drawbridge_SignalLamp");
                if (lampGo != null) m_SignalLamp = lampGo.GetComponent<Light>();
                m_SignalLampCached = true;
            }
            if (m_SignalLamp != null) m_SignalLamp.color = c;
        }

        #endregion

        #region Helpers & Input

        private CharacterController GetNearestPlayer(Vector3 targetPos, float maxDist, out PlayerRoleVisuals roleVisuals)
        {
            roleVisuals = null;
            if (cachedControllers == null) return null;

            CharacterController nearest = null;
            float minDistSq = maxDist * maxDist;

            for (int i = 0; i < cachedControllers.Length; i++)
            {
                var cc = cachedControllers[i];
                if (cc == null || !cc.gameObject.activeInHierarchy) continue;

                float distSq = (cc.transform.position - targetPos).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    nearest = cc;
                    roleVisuals = cc.GetComponentInParent<PlayerRoleVisuals>();
                }
            }

            return nearest;
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame)
                    return true;
            }
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                    Gamepad.current.buttonWest.wasPressedThisFrame)
                    return true;
            }
            return false;
#else
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private bool IsInteractHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.isPressed ||
                    Keyboard.current.spaceKey.isPressed ||
                    Keyboard.current.enterKey.isPressed)
                    return true;
            }
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonSouth.isPressed ||
                    Gamepad.current.buttonWest.isPressed)
                    return true;
            }
            return false;
#else
            return Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space);
#endif
        }

        private void ShowPrompt(string text, Color col, float duration = 0.25f)
        {
            currentPrompt = text;
            promptColor = col;
            promptTimer = duration;
        }

        #endregion

        #region GUI Rendering

        private void OnGUI()
        {
            DrawObjectivePanel();

            if (string.IsNullOrEmpty(currentPrompt) || promptTimer <= 0f) return;

            GUI.depth = -100;
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleCenter;
            boxStyle.fontSize = 19;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = promptColor;

            float width = 860f, height = 56f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 170f;

            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.88f);
            GUI.Box(new Rect(x, y, width, height), currentPrompt, boxStyle);
        }

        /// <summary>Bảng mục tiêu 2 pha của Cầu Rút (góc phải trên).</summary>
        private void DrawObjectivePanel()
        {
            GUI.depth = -100;
            var titleStyle = new GUIStyle(GUI.skin.box);
            titleStyle.fontSize = 16;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);

            var itemStyle = new GUIStyle(GUI.skin.label);
            itemStyle.fontSize = 15;
            itemStyle.alignment = TextAnchor.MiddleLeft;

            float w = 320f, h = 82f;
            float x = Screen.width - w - 18f, y = 18f;
            GUI.Box(new Rect(x, y, w, h), "🌉 MỤC TIÊU CẦU RÚT KHU VỊNH", titleStyle);

            DrawObjectiveLine(itemStyle, x, y + 30f, isPinReleased,
                "1. Con chui khe → nhổ chốt khóa sắt");
            DrawObjectiveLine(itemStyle, x, y + 56f, isBridgeOpened,
                "2. Cha quay tời → hạ cầu sang Khu Mỏ");
        }

        private void DrawObjectiveLine(GUIStyle style, float x, float y, bool done, string text)
        {
            style.normal.textColor = done ? new Color(0.45f, 0.95f, 0.5f) : new Color(0.85f, 0.87f, 0.92f);
            GUI.Label(new Rect(x + 12f, y, 300f, 24f), (done ? "✔ " : "○ ") + text, style);
        }

        #endregion
    }
}
