using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// CƠ CHẾ CO-OP "CẦU ĐẤT LẬT CỬA VỊNH" (Cove Bascule Bridge) — bật cầu nối Khu Vịnh với Khu Mỏ.
    ///
    /// BỐ CỢC (đã đo bằng raycast trên scene thật):
    ///   - Bến gỗ (Trestle_Deck_0) kết thúc ở X = -18.4, mặt sàn Y ≈ 13.47
    ///   - Bệ đá tháp (Cove_Bridge_Tower_East) bắt đầu ở X = -12.2, mặt sàn Y ≈ 13.91
    ///   - Khe hở giữa hai bên: 6.2 m, sàn chênh nhau 0.44 m
    ///
    /// CƠ CHẾ (3 bước, 2 người):
    ///   PHA 1 — 👶 CON chui khe hẹp (tunnel cao 0.78 m, rộng 0.85 m — chỉ Con lọt):
    ///     Con chui vào khe hẹp ở mặt Tây bệ đá, nhổ "CHỐT CHIẾN" (Cove_Drawbridge_ReleasePin).
    ///     Chốt vừa nhổ -> khóa đá trên lồng đối trọng được giải phóng.
    ///
    ///   PHA 2 — 🧔 CHA quay tời (Cove_Drawbridge_WinchDrum) 360°:
    ///     Cha quay trống tời, dây xích cuộn lồng đá đối trọng lên cao.
    ///     Đối trọng nặng lên -> cầu đất lật rơi từ 68° về 0° (nằm phẳng khít giữa bến gỗ và tháp đá).
    ///
    ///   PHA 3 — Hai Cha Con bước qua cầu phẳng lên tháp đá, đi tiếp qua cầu treo sang KHU MỎ.
    /// </summary>
    [SelectionBase]
    public class CoveDrawbridgeCoopController : MonoBehaviour
    {
        private static CoveDrawbridgeCoopController instance;
        public static CoveDrawbridgeCoopController Instance => instance;

        #region Serialized Fields

        [Header("Trục bản lề (cầu đất lật)")]
        [Tooltip("Pivot xoay của cánh cầu (Cove_Drawbridge_Pivot)")]
        [SerializeField] private Transform hingePivot;

        [Tooltip("Cánh cầu (Cove_Drawbridge_Deck) — con của pivot")]
        [SerializeField] private Transform leaf;

        [Header("Pha 1 — Chốt chiến (Con nhổ)")]
        [Tooltip("Chốt chiến sắt trong khe hẹp (Cove_Drawbridge_ReleasePin)")]
        [SerializeField] private Transform releasePin;

        [Tooltip("Vị trí để Con đứng chui vào khe hẹp")]
        [SerializeField] private Vector3 crevicePos = new Vector3(-19.90f, 13.60f, -74.60f);

        [Header("Pha 2 — Quay tời (Cha quay)")]
        [Tooltip("Trống tời (Cove_Drawbridge_WinchDrum)")]
        [SerializeField] private Transform winchDrum;

        [Tooltip("Tay quay (Cove_Drawbridge_CrankHandle)")]
        [SerializeField] private Transform crankHandle;

        [Tooltip("Vị trí Cha đứng quay tời")]
        [SerializeField] private Vector3 winchPos = new Vector3(-21.35f, 13.60f, -73.40f);

        [Header("Đối trọng & xích")]
        [Tooltip("Khối đá đối trọng (Cove_Drawbridge_Counterweight)")]
        [SerializeField] private Transform counterweight;

        [Tooltip("Xích nối đầu cầu với đối trọng (Cove_Drawbridge_Chain_Tip / _Tail)")]
        [SerializeField] private Transform chainToTip;

        [SerializeField] private Transform chainToWeight;

        [Header("Chốt giữ trên tháp (Con đạp để ghim cầu)")]
        [Tooltip("Bàn đạp chốt giữ (Cove_TowerE_HoldLatch)")]
        [SerializeField] private Transform holdLatch;

        [Tooltip("Vị trí chốt giữ trên sàn tháp")]
        [SerializeField] private Vector3 latchPos = new Vector3(-11.00f, 13.95f, -72.90f);

        [Header("Trạng thái")]
        [SerializeField] private bool isPinReleased = false;
        [SerializeField] private bool isBridgeOpened = false;
        [SerializeField] private bool isLatchPressed = false;
        [SerializeField] private float crankProgress = 0f;

        [Header("Thông số")]
        [Tooltip("Góc cầu lúc dựng (độ)")]
        [SerializeField] private float raisedAngle = 68f;

        [Tooltip("Góc cầu khi hạ phẳng (độ)")]
        [SerializeField] private float openAngle = 0f;

        [Tooltip("Thời gian quay tời (giây)")]
        [SerializeField] private float crankDuration = 3.6f;

        [Tooltip("Thời gian hạ cầu (giây)")]
        [SerializeField] private float lowerDuration = 2.6f;

        [Tooltip("Góc cầu khi Cha ĐANG GIỮ cần quay (độ) — cầu giữ phẳng ở 0 độ nhờ lực giữ")]
        [SerializeField] private float holdAngle = 0f;

        [Tooltip("Khoảng cách tương tác (mét)")]
        [SerializeField] private float interactDistance = 3.6f;

        #endregion

        #region Private Fields

        private Vector3 m_WeightStartPos;
        private Vector3 m_WeightEndPos;
        private Vector3 m_TipLocalOffset;
        private Vector3 m_SheaveWorld;

        private Coroutine m_BridgeRoutine;
        private CharacterController[] cachedControllers;
        private float lastUpdateTime = 0f;

        private string currentPrompt = "";
        private float promptTimer = 0f;
        private Color promptColor = Color.yellow;

        private Light m_SignalLamp;
        private bool m_SignalLampCached;

        private bool isDadHoldingCrank = false;
        private float m_DadHoldStartWeightY = 0f;

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

            if (hingePivot != null && leaf != null)
            {
                // Đầu móc cầu: điểm xa nhất theo local +X của cánh cầu
                var rend = leaf.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    Bounds b = rend.bounds;
                    Vector3 tipWorld = new Vector3(b.max.x, b.max.y, b.center.z);
                    m_TipLocalOffset = leaf.InverseTransformPoint(tipWorld);
                }

                // Đối trọng: nằm ở đáy lồng lúc đầu, được kéo lên cao khi quay tời
                if (counterweight != null)
                {
                    m_WeightStartPos = counterweight.position;
                    m_WeightEndPos = m_WeightStartPos + Vector3.up * 2.10f;
                }

                if (hingePivot != null) m_SheaveWorld = hingePivot.position + Vector3.up * 0.10f;

                ApplyState(0f, raisedAngle);
            }

            if (releasePin != null) releasePin.gameObject.SetActive(true);
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
            if (hingePivot == null)
            {
                var p = GameObject.Find("Cove_Drawbridge_Pivot");
                if (p != null) hingePivot = p.transform;
            }

            if (leaf == null)
            {
                var l = GameObject.Find("Cove_Drawbridge_Deck");
                if (l != null) leaf = l.transform;
            }

            if (releasePin == null)
            {
                var r = GameObject.Find("Cove_Drawbridge_ReleasePin");
                if (r != null) releasePin = r.transform;
            }

            if (winchDrum == null)
            {
                var w = GameObject.Find("Cove_Drawbridge_WinchDrum");
                if (w != null) winchDrum = w.transform;
            }

            if (crankHandle == null)
            {
                var c = GameObject.Find("Cove_Drawbridge_CrankHandle");
                if (c != null) crankHandle = c.transform;
            }

            if (counterweight == null)
            {
                var w = GameObject.Find("Cove_Drawbridge_Counterweight");
                if (w != null) counterweight = w.transform;
            }

            if (chainToTip == null)
            {
                var c = GameObject.Find("Cove_Drawbridge_Chain_Tip");
                if (c != null) chainToTip = c.transform;
            }

            if (chainToWeight == null)
            {
                var c = GameObject.Find("Cove_Drawbridge_Chain_Tail");
                if (c != null) chainToWeight = c.transform;
            }
        }

        private void CachePlayers()
        {
            cachedControllers = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
        }

        #endregion

        #region Gameplay

        private void HandleInteractions()
        {
            if (isBridgeOpened)
            {
                PlayerRoleVisuals vOpen;
                var near = GetNearestPlayer(winchPos, 12f, out vOpen);
                if (near != null)
                {
                    ShowPrompt("✅ CẦU ĐẤT LẬT ĐÃ HẠ PHẲNG! Hai Cha Con bước qua cầu lên tháp đá, đi tiếp sang KHU MỎ.", Color.green, 0.5f);
                }
                return;
            }

            // ---- PHA 1: CON nhổ chốt chiến trong khe hẹp ----
            if (!isPinReleased)
            {
                PlayerRoleVisuals vCrevice;
                var child = GetNearestPlayer(crevicePos, interactDistance, out vCrevice);
                if (child != null)
                {
                    bool isDad = vCrevice != null ? vCrevice.IsDad : child.name.ToLower().Contains("dad");
                    if (isDad)
                    {
                        ShowPrompt("🚫 Khe hẹp cao 0.78m — chỉ 👶 Con mới chui lọt vào nhổ chốt!", new Color(1f, 0.72f, 0.2f));
                    }
                    else
                    {
                        ShowPrompt("👶 [E] Nhổ CHỐT CHIẾN khỏi lồng đá đối trọng", Color.cyan);
                        if (IsInteractPressed()) StartCoroutine(ReleasePinRoutine());
                    }
                    return;
                }

                PlayerRoleVisuals vW0;
                var nearW0 = GetNearestPlayer(winchPos, 8f, out vW0);
                if (nearW0 != null)
                {
                    ShowPrompt("⚙️ Cầu bị CHỐT CHIẾN ghim. 👶 Con phải chui vào khe hẹp để nhổ chốt!", new Color(1f, 0.65f, 0.1f));
                }
                return;
            }

            // ---- PHA 2: CHA quay tời (GIỮ => cầu ở góc 0°) ----
            PlayerRoleVisuals vWinch;
            var dad = GetNearestPlayer(winchPos, interactDistance + 1.4f, out vWinch);
            if (dad != null)
            {
                bool isDad = vWinch != null ? vWinch.IsDad : dad.name.ToLower().Contains("dad");
                if (!isDad)
                {
                    ShowPrompt("🚫 Trống tời quá nặng! Cần sức 💪 của 🧔 Ba để quay!", Color.red);
                }
                else
                {
                    // CHA GIỮ CẦN QUAY -> cầu được giữ ở góc 0° (phẳng, đi qua được)
                    if (IsInteractHeld())
                    {
                        if (!isDadHoldingCrank) { isDadHoldingCrank = true; m_DadHoldStartWeightY = counterweight != null ? counterweight.position.y : 0f; }
                        ApplyState(1f, holdAngle);
                        if (winchDrum != null) winchDrum.Rotate(Vector3.forward, 220f * Time.deltaTime, Space.Self);
                        if (crankHandle != null) crankHandle.Rotate(Vector3.forward, 220f * Time.deltaTime, Space.Self);
                        if (counterweight != null)
                        {
                            Vector3 p = counterweight.position;
                            p.y = Mathf.MoveTowards(p.y, m_WeightEndPos.y, 1.2f * Time.deltaTime);
                            counterweight.position = p;
                        }
                        crankProgress = Mathf.Clamp01(crankProgress + Time.deltaTime / crankDuration);
                        ShowPrompt($"🧔 [GIỮ SPACE / E] Đang giữ cầu phẳng! ({Mathf.RoundToInt(crankProgress * 100)}%) — 👶 Con hãy đạp chốt giữ trên tháp!", Color.green);
                    }
                    else
                    {
                        isDadHoldingCrank = false;
                        ShowPrompt($"🧔 [Giữ SPACE / E] Quay tời hạ cầu ({Mathf.RoundToInt(crankProgress * 100)}%) — nhả tay thì cầu sập lại!", new Color(1f, 0.85f, 0.3f));
                        if (crankProgress > 0f) crankProgress = Mathf.Max(0f, crankProgress - Time.deltaTime / crankDuration * 2f);
                        ApplyState(crankProgress, Mathf.Lerp(raisedAngle, holdAngle, crankProgress));
                        if (counterweight != null)
                        {
                            Vector3 p = counterweight.position;
                            p.y = Mathf.MoveTowards(p.y, m_DadHoldStartWeightY, 1.0f * Time.deltaTime);
                            counterweight.position = p;
                        }
                    }
                }
            }

            // ---- PHA 3: CON đạp chốt giữ trên tháp -> hoàn thành vĩnh viễn ----
            if (!isLatchPressed)
            {
                PlayerRoleVisuals vLatch;
                var child = GetNearestPlayer(latchPos, 2.6f, out vLatch);
                if (child != null)
                {
                    bool isDad = vLatch != null ? vLatch.IsDad : child.name.ToLower().Contains("dad");
                    if (isDad)
                    {
                        ShowPrompt("🚫 Bàn đạp chốt giữ quá nhẹ — chỉ 👶 Con đạp mới giữ được cầu!", new Color(1f, 0.72f, 0.2f));
                    }
                    else
                    {
                        ShowPrompt("👶 [E] Đạp CHỐT GIU trên tháp để ghim cầu vĩnh viễn!", Color.cyan);
                        if (IsInteractPressed()) StartCoroutine(PressLatchRoutine());
                    }
                }
            }

            // ---- Đã ghim chốt: cầu ở 0° vĩnh viễn ----
            if (isLatchPressed)
            {
                ApplyState(1f, holdAngle);
                if (counterweight != null) counterweight.position = m_WeightEndPos;
            }
        }

        private IEnumerator ReleasePinRoutine()
        {
            isPinReleased = true;
            UISoundManager.PlayConfirm();
            ShowPrompt("⚙️ CHỐT CHIẾN ĐÃ NHỔ! Lồng đá đối trọng đã được giải phóng...", Color.yellow, 3.0f);
            Debug.Log("[CoveDrawbridge] Con đã nhổ chốt chiến — lồng đối trọng được giải phóng!");

            if (releasePin != null)
            {
                Vector3 p0 = releasePin.position;
                Quaternion r0 = releasePin.rotation;
                float dur = 0.6f, e = 0f;
                while (e < dur)
                {
                    e += Time.deltaTime;
                    float t = e / dur;
                    releasePin.position = p0 + new Vector3(-t * 1.1f, Mathf.Sin(t * Mathf.PI) * 0.5f, 0f);
                    releasePin.rotation = r0 * Quaternion.Euler(0f, 0f, t * 260f);
                    yield return null;
                }
                releasePin.gameObject.SetActive(false);
            }

            UISoundManager.PlaySuccess();
            ShowPrompt("✨ KHÓA ĐÁ ĐÃ VĂNG! 🧔 Ba hãy quay trống tời để kéo cầu hạ xuống!", Color.green, 5.0f);
        }

        private void LowerBridgeRoutine()
        {
            if (m_BridgeRoutine != null) StopCoroutine(m_BridgeRoutine);
            m_BridgeRoutine = StartCoroutine(LowerBridgeInner());
        }

        /// <summary>Con đạp chốt giữ — cầu được ghim vĩnh viễn ở góc 0°.</summary>
        private IEnumerator PressLatchRoutine()
        {
            isLatchPressed = true;
            isBridgeOpened = true;
            isDadHoldingCrank = false;
            UISoundManager.PlaySuccess();
            ShowPrompt("🔒 CHỐT GIU ĐÃ VÀO KHỚP! Cầu được ghim vĩnh viễn — Hai Cha Con cùng bước qua sang KHU MỎ!", Color.green, 8.0f);
            Debug.Log("[CoveDrawbridge] Con đã đạp chốt giữ — cầu cửa vịnh đã mở vĩnh viễn!");

            if (holdLatch != null)
            {
                Vector3 p0 = holdLatch.position;
                Vector3 target = p0 + Vector3.down * 0.09f;
                float dur = 0.35f, e = 0f;
                while (e < dur)
                {
                    e += Time.deltaTime;
                    float t = e / dur;
                    holdLatch.position = Vector3.Lerp(p0, target, Mathf.SmoothStep(0f, 1f, t));
                    yield return null;
                }
            }

            ApplyState(1f, holdAngle);
            if (counterweight != null) counterweight.position = m_WeightEndPos;
            SetSignalLamp(Color.green);
        }

        private IEnumerator LowerBridgeInner()
        {
            UISoundManager.PlayConfirm();
            ShowPrompt("⚙️ XÍCH ĐANG CUỘN — CẦU ĐẤT LẬT ĐANG HẠ XUỐNG!", Color.yellow, 3.0f);

            float startAngle = raisedAngle;
            float startWeightY = m_WeightStartPos.y;
            float e = 0f;

            while (e < lowerDuration)
            {
                e += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, e / lowerDuration);

                float angle = Mathf.Lerp(startAngle, openAngle, t);
                ApplyState(t, angle);

                if (counterweight != null)
                {
                    Vector3 p = counterweight.position;
                    p.y = Mathf.Lerp(startWeightY, m_WeightEndPos.y, t);
                    counterweight.position = p;
                }

                float rumble = (1f - t) * Mathf.Sin(e * 24f) * 0.03f;
                if (leaf != null) leaf.position += new Vector3(0f, rumble, 0f);

                yield return null;
            }

            ApplyState(1f, openAngle);
            if (counterweight != null) counterweight.position = m_WeightEndPos;

            isBridgeOpened = true;
            UISoundManager.PlaySuccess();
            ShowPrompt("✅ CẦU ĐÃ HẠ PHẲNG KHÍT! Hai Cha Con bước qua sang tháp đá để đi vào KHU MỎ!", Color.green, 7.0f);
            Debug.Log("[CoveDrawbridge] Cầu đất lật đã hạ phẳng — đường vào Khu Mỏ đã mở!");
            SetSignalLamp(Color.green);
        }

        /// <summary>Áp dụng góc cánh cầu quanh trục bản lề (trục Z thế giới).</summary>
        private void ApplyState(float t, float angle)
        {
            if (hingePivot != null) hingePivot.rotation = Quaternion.Euler(0f, 0f, -angle);
        }

        private void UpdateChainVisuals()
        {
            if (leaf == null || hingePivot == null) return;

            Vector3 tip = leaf.TransformPoint(m_TipLocalOffset);
            Vector3 sheave = m_SheaveWorld;

            StretchChain(chainToTip, tip, sheave);
            StretchChain(chainToWeight, sheave, counterweight != null ? counterweight.position + Vector3.up * 0.45f : sheave);
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
                var lampGo = GameObject.Find("Cove_SignalLamp");
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

            float width = 900f, height = 56f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 170f;

            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.88f);
            GUI.Box(new Rect(x, y, width, height), currentPrompt, boxStyle);
        }

        private bool IsAnyPlayerInCoveZone()
        {
            if (cachedControllers == null || cachedControllers.Length == 0)
                cachedControllers = FindObjectsOfType<CharacterController>();
            if (cachedControllers != null)
            {
                foreach (var cc in cachedControllers)
                {
                    if (cc != null && cc.transform.position.x <= 28f && cc.transform.position.z <= -60f)
                        return true;
                }
            }
            return false;
        }

        private void DrawObjectivePanel()
        {
            if (!IsAnyPlayerInCoveZone()) return;
            GUI.depth = -100;
            var titleStyle = new GUIStyle(GUI.skin.box);
            titleStyle.fontSize = 16;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = new Color(0.6f, 0.85f, 1f);

            var itemStyle = new GUIStyle(GUI.skin.label);
            itemStyle.fontSize = 15;
            itemStyle.alignment = TextAnchor.MiddleLeft;

            float w = 340f, h = 82f;
            float x = Screen.width - w - 18f, y = 18f;
            GUI.Box(new Rect(x, y, w, h), "🌉 MỤC TIÊU CẦU CỬA VỊNH", titleStyle);

            DrawObjectiveLine(itemStyle, x, y + 30f, isPinReleased,
            "1. Con chui khe hẹp → nhổ chốt chiến");
            DrawObjectiveLine(itemStyle, x, y + 56f, isLatchPressed,
            "2. Cha giữ tời → Con đạp chốt giữ");
        }

        private void DrawObjectiveLine(GUIStyle style, float x, float y, bool done, string text)
        {
            style.normal.textColor = done ? new Color(0.45f, 0.95f, 0.5f) : new Color(0.85f, 0.87f, 0.92f);
            GUI.Label(new Rect(x + 12f, y, 316f, 24f), (done ? "✔ " : "○ ") + text, style);
        }

        #endregion
    }
}
