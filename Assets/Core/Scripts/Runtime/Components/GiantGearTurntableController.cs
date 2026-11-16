using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Bàn Xoay Bánh Răng Đá Khổng Lồ & Cơ Chế Co-op Giải Đố Liên Hoàn (Zone 3):
    /// 1. 👶 Con tiếp cận miệng thân cây rỗng (Zone3_HollowLog_Bore), bấm [E] nhổ rễ cây gai và chui bò qua lòng thân cây.
    /// 2. Con bò tới cửa trước mép vực, tiếp cận Chốt Khóa Kẹt (Zone3_Gear_LockPin), bấm [E] để nhổ chốt.
    /// 3. Chốt bung ra, bánh răng đá phát sáng ngọc Cyan sẵn sàng xoay.
    /// 4. 🧔 Ba đứng bên cần tời (Zone3_Gear_CrankWinch), giữ [Space/E] dùng sức quay cần tời.
    /// 5. Bánh răng đá khổng lồ (Giant_Stone_Gear_Body) xoay 90 độ mượt mà, chở người chơi trên bục xoay an toàn.
    /// 6. Răng đá ăn khớp chuẩn xác với mỏm Cầu Treo (SuspBridge), mở lối sang Zone 4!
    /// </summary>
    [SelectionBase]
    public class GiantGearTurntableController : MonoBehaviour
    {
        private static GiantGearTurntableController instance;
        public static GiantGearTurntableController Instance => instance;

        #region Serialized Fields

        [Header("Target References")]
        [Tooltip("Trục xoay trung tâm của toàn bộ bánh răng đá (Zone3_Gear_Turntable_Pivot)")]
        [SerializeField] private Transform gearTurntablePivot;

        [Tooltip("Thân bánh răng đá chính (Giant_Stone_Gear_Body)")]
        [SerializeField] private Transform gearBody;

        [Tooltip("Lõi phát sáng trung tâm của bánh răng (Gear_Center_Hub)")]
        [SerializeField] private Transform gearCenterHub;

        [Tooltip("Chốt đá/gỗ kẹt khóa bánh răng (Zone3_Gear_LockPin)")]
        [SerializeField] private Transform lockPin;

        [Tooltip("Vị trí tay quay tời của Ba (Zone3_Gear_CrankWinch)")]
        [SerializeField] private Transform crankWinch;

        [Tooltip("Tay quay xoay của cần tời (Crank_Wheel_Handle)")]
        [SerializeField] private Transform crankHandle;

        [Tooltip("Vùng chui thân cây gỗ mục (Zone3_Log_CrawlTrigger)")]
        [SerializeField] private Transform logCrawlTrigger;

        [Tooltip("Rào chắn rễ cây gai miệng hầm (Zone3_Log_VinesBarricade)")]
        [SerializeField] private Transform vinesBarricade;

        [Header("Tunnel Crawl Points")]
        [Tooltip("Điểm cửa vào thân cây phía bờ Ba đứng")]
        [SerializeField] private Vector3 tunnelEntrance = new Vector3(0.13f, 14.65f, -52.5f);

        [Tooltip("Điểm cửa ra thân cây phía bánh răng đá")]
        [SerializeField] private Vector3 tunnelExit = new Vector3(0.13f, 14.65f, -63.7f);

        [Header("Puzzle Settings")]
        [Tooltip("Góc xoay mục tiêu của bàn xoay bánh răng đá (độ)")]
        [SerializeField] private float targetRotationAngle = 90.0f;

        [Tooltip("Thời gian Ba quay tời liên tục để hoàn thành xoay (giây)")]
        [SerializeField] private float crankDurationRequired = 4.5f;

        [Tooltip("Khoảng cách tương tác với chốt gỡ kẹt của Con (mét)")]
        [SerializeField] private float pinInteractDistance = 3.2f;

        [Tooltip("Khoảng cách tương tác với cần tời của Ba (mét)")]
        [SerializeField] private float crankInteractDistance = 3.8f;

        [Tooltip("Khoảng cách tương tác với miệng hầm thân cây (mét)")]
        [SerializeField] private float crawlInteractDistance = 3.2f;

        [Header("State Status")]
        [SerializeField] private bool isVinesCleared = false;
        [SerializeField] private bool isPinRemoved = false;
        [SerializeField] private bool isCompleted = false;
        [SerializeField] private float crankProgress = 0f;
        [SerializeField] private float currentRotationAngle = 0f;

        public bool IsPinRemoved => isPinRemoved;
        public bool IsCompleted => isCompleted;

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private string currentPrompt = "";
        private float promptTimer = 0f;
        private Color promptColor = Color.yellow;

        private Renderer hubRenderer;
        private Material hubMaterial;
        private Color originalHubColor;
        private AudioSource audioSource;

        private CharacterController[] cachedControllers;
        private float lastUpdateTime = 0f;
        private bool isCrawling = false;

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

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0.5f;
            }

            AutoFindReferences();
        }

        private void Start()
        {
            mainCamera = Camera.main;

            if (gearCenterHub != null)
            {
                hubRenderer = gearCenterHub.GetComponent<Renderer>();
                if (hubRenderer != null)
                {
                    hubMaterial = hubRenderer.material;
                    if (hubMaterial.HasProperty("_Color"))
                        originalHubColor = hubMaterial.color;
                    else if (hubMaterial.HasProperty("_BaseColor"))
                        originalHubColor = hubMaterial.GetColor("_BaseColor");
                }
            }

            CachePlayers();
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            // Đếm ngược ẩn prompt nếu không ở gần
            if (promptTimer > 0f)
            {
                promptTimer -= Time.deltaTime;
                if (promptTimer <= 0f) currentPrompt = "";
            }

            // Định kỳ quét người chơi
            if (Time.time - lastUpdateTime > 1.0f)
            {
                CachePlayers();
                lastUpdateTime = Time.time;
            }

            HandleInteractions();
            UpdateVisuals();
        }

        #endregion

        #region Auto Setup & References

        public void AutoFindReferences()
        {
            if (gearTurntablePivot == null)
            {
                var piv = GameObject.Find("Zone3_Gear_Turntable_Pivot");
                if (piv != null) gearTurntablePivot = piv.transform;
            }

            if (gearBody == null)
            {
                var gb = GameObject.Find("Giant_Stone_Gear_Body");
                if (gb != null) gearBody = gb.transform;
            }

            if (gearCenterHub == null)
            {
                var gh = GameObject.Find("Gear_Center_Hub");
                if (gh != null) gearCenterHub = gh.transform;
            }

            if (lockPin == null)
            {
                var lp = GameObject.Find("Zone3_Gear_LockPin");
                if (lp != null) lockPin = lp.transform;
            }

            if (crankWinch == null)
            {
                var cw = GameObject.Find("Zone3_Gear_CrankWinch");
                if (cw != null) crankWinch = cw.transform;
            }

            if (crankHandle == null && crankWinch != null)
            {
                var h = crankWinch.Find("Crank_Wheel_Handle");
                if (h != null) crankHandle = h;
            }

            if (logCrawlTrigger == null)
            {
                var lt = GameObject.Find("Zone3_Log_CrawlTrigger");
                if (lt != null) logCrawlTrigger = lt.transform;
            }

            if (vinesBarricade == null)
            {
                var vb = GameObject.Find("Zone3_Log_VinesBarricade");
                if (vb != null) vinesBarricade = vb.transform;
            }
        }

        private void CachePlayers()
        {
            cachedControllers = FindObjectsOfType<CharacterController>();
        }

        #endregion

        #region Interaction & Gameplay Logic

        private void HandleInteractions()
        {
            if (isCrawling) return;

            // 1. Tương tác với Miệng Hầm Thân Cây (Crawl Entrance / Exit)
            HandleTunnelInteraction();

            // 2. Tương tác với Chốt Khóa Kẹt của Con (Lock Pin)
            HandleLockPinInteraction();

            // 3. Tương tác với Cần Tời Xoay của Ba (Crank Winch)
            HandleCrankWinchInteraction();
        }

        private void HandleTunnelInteraction()
        {
            // Kiểm tra tương tác ở Cửa Vào (phía bờ Ba)
            PlayerRoleVisuals entranceVisuals;
            CharacterController nearEntrancePlayer = GetNearestPlayer(tunnelEntrance, crawlInteractDistance, out entranceVisuals);
            if (nearEntrancePlayer != null)
            {
                bool isDad = entranceVisuals != null ? entranceVisuals.IsDad : nearEntrancePlayer.name.ToLower().Contains("dad");

                if (isDad)
                {
                    ShowPrompt("🚫 🧔 Ba quá to lớn, không thể chui qua thân cây này! Hãy để 👶 Con chui vào mở chốt!", Color.red);
                }
                else
                {
                    if (!isVinesCleared)
                    {
                        ShowPrompt("👶 [E] Nhổ Cành Rễ Gai & Khom Người Chui Qua Thân Cây", Color.cyan);
                        if (IsInteractPressed())
                        {
                            ClearVinesAndCrawl(nearEntrancePlayer, true);
                        }
                    }
                    else
                    {
                        ShowPrompt("👶 [E] Khom Người Chui Bò Qua Thân Cây Rỗng", Color.cyan);
                        if (IsInteractPressed())
                        {
                            StartCoroutine(CrawlTraverseCoroutine(nearEntrancePlayer, tunnelEntrance, tunnelExit));
                        }
                    }
                }
                return;
            }

            // Kiểm tra tương tác ở Cửa Ra (phía mép bánh răng) để chui quay lại nếu muốn
            PlayerRoleVisuals exitVisuals;
            CharacterController nearExitPlayer = GetNearestPlayer(tunnelExit, crawlInteractDistance * 0.85f, out exitVisuals);
            if (nearExitPlayer != null && isPinRemoved)
            {
                bool isDad = exitVisuals != null ? exitVisuals.IsDad : nearExitPlayer.name.ToLower().Contains("dad");
                if (!isDad)
                {
                    ShowPrompt("👶 [E] Khom Người Chui Bò Quay Lại Bờ Ba", Color.cyan);
                    if (IsInteractPressed())
                    {
                        StartCoroutine(CrawlTraverseCoroutine(nearExitPlayer, tunnelExit, tunnelEntrance));
                    }
                }
            }
        }

        private void ClearVinesAndCrawl(CharacterController child, bool goForward)
        {
            isVinesCleared = true;
            UISoundManager.PlayClick();

            if (vinesBarricade != null)
            {
                StartCoroutine(AnimateVinesClear());
            }

            StartCoroutine(CrawlTraverseCoroutine(child, tunnelEntrance, tunnelExit));
        }

        private IEnumerator AnimateVinesClear()
        {
            if (vinesBarricade == null) yield break;

            Vector3 origPos = vinesBarricade.position;
            float dur = 0.5f;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                vinesBarricade.position = origPos + Vector3.up * (t * 1.2f) + Vector3.right * (t * 0.8f);
                vinesBarricade.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                yield return null;
            }
            vinesBarricade.gameObject.SetActive(false);
        }

        private IEnumerator CrawlTraverseCoroutine(CharacterController child, Vector3 fromPoint, Vector3 toPoint)
        {
            isCrawling = true;
            child.enabled = false; // Tạm ngắt CharacterController để lướt mượt trong ống

            Vector3 origScale = child.transform.localScale;
            Vector3 crawlScale = new Vector3(origScale.x * 1.05f, origScale.y * 0.55f, origScale.z * 1.15f);

            float duration = 1.6f;
            float timer = 0f;

            // Xoay mặt theo hướng chui
            Vector3 crawlDir = (toPoint - fromPoint).normalized;
            if (crawlDir.sqrMagnitude > 0.01f)
            {
                child.transform.rotation = Quaternion.LookRotation(crawlDir, Vector3.up);
            }

            while (timer < duration)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, timer / duration);

                // Di chuyển dọc thân cây
                child.transform.position = Vector3.Lerp(fromPoint, toPoint, t);

                // Dáng khom người dẹp xuống
                float poseT = Mathf.Sin(t * Mathf.PI);
                child.transform.localScale = Vector3.Lerp(origScale, crawlScale, poseT);

                yield return null;
            }

            child.transform.position = toPoint;
            child.transform.localScale = origScale;
            child.enabled = true;
            isCrawling = false;

            UISoundManager.PlayClick();
            Debug.Log("[GiantGearTurntable] 👶 Con đã chui bò qua thân cây thành công!");
        }

        private void HandleLockPinInteraction()
        {
            if (isPinRemoved || lockPin == null) return;

            Vector3 pinPos = lockPin.position;
            PlayerRoleVisuals nearestVisuals;
            CharacterController nearestPlayer = GetNearestPlayer(pinPos, pinInteractDistance, out nearestVisuals);

            if (nearestPlayer != null)
            {
                bool isDad = nearestVisuals != null ? nearestVisuals.IsDad : nearestPlayer.name.ToLower().Contains("dad");

                if (isDad)
                {
                    ShowPrompt("🚫 Chốt kẹt nằm trong khe hẹp, chỉ 👶 Con mới gỡ được!", Color.red);
                }
                else
                {
                    ShowPrompt("👶 [E] Nhổ Chốt Gỗ Kẹt Bánh Răng Đá", Color.cyan);

                    if (IsInteractPressed())
                    {
                        RemoveLockPin();
                    }
                }
            }
        }

        private void RemoveLockPin()
        {
            isPinRemoved = true;
            UISoundManager.PlayClick();

            // Hiệu ứng nhổ chốt: bay lên và biến mất
            StartCoroutine(AnimatePinEject());

            ShowPrompt("✨ ĐÃ NHẢ CHỐT KHÓA! 🧔 Ba hãy quay cần tời bên bờ để xoay bánh răng đá!", Color.cyan, 4.0f);
            Debug.Log("[GiantGearTurntable] 👶 Con đã gỡ chốt kẹt bánh răng thành công!");
        }

        private IEnumerator AnimatePinEject()
        {
            if (lockPin == null) yield break;

            Vector3 startPos = lockPin.position;
            Quaternion startRot = lockPin.rotation;
            float timer = 0f;
            float dur = 0.6f;

            while (timer < dur)
            {
                timer += Time.deltaTime;
                float t = timer / dur;
                lockPin.position = startPos + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.5f) + Vector3.back * (t * 1.2f);
                lockPin.rotation = startRot * Quaternion.Euler(t * 360f, t * 180f, 0f);
                yield return null;
            }

            lockPin.gameObject.SetActive(false);
        }

        private void HandleCrankWinchInteraction()
        {
            if (isCompleted || crankWinch == null) return;

            Vector3 winchPos = crankWinch.position;
            PlayerRoleVisuals nearestVisuals;
            CharacterController nearestPlayer = GetNearestPlayer(winchPos, crankInteractDistance, out nearestVisuals);

            if (nearestPlayer != null)
            {
                bool isDad = nearestVisuals != null ? nearestVisuals.IsDad : nearestPlayer.name.ToLower().Contains("dad");

                if (!isPinRemoved)
                {
                    ShowPrompt("🔒 Bánh răng đá đang bị kẹt cứng! Hãy để 👶 Con chui qua thân cây nhổ chốt!", Color.yellow);
                }
                else
                {
                    if (!isDad)
                    {
                        ShowPrompt("🚫 Cần tời đá quá nặng! Cần sức mạnh của 🧔 Ba để quay!", Color.red);
                    }
                    else
                    {
                        ShowPrompt($"🧔 [Giữ SPACE / E] Dùng sức quay cần tời ({Mathf.RoundToInt(crankProgress * 100)}%)", Color.green);

                        if (IsInteractHeld())
                        {
                            PerformCranking();
                        }
                    }
                }
            }
        }

        private void PerformCranking()
        {
            if (isCompleted) return;

            // Xoay tay quay tời
            if (crankHandle != null)
            {
                crankHandle.Rotate(Vector3.forward, 360f * Time.deltaTime, Space.Self);
            }

            // Tăng tiến độ
            float deltaProgress = Time.deltaTime / crankDurationRequired;
            crankProgress = Mathf.Clamp01(crankProgress + deltaProgress);

            float targetAngle = crankProgress * targetRotationAngle;
            float deltaAngle = targetAngle - currentRotationAngle;

            if (Mathf.Abs(deltaAngle) > 0.001f && gearTurntablePivot != null)
            {
                // Xoay bàn xoay
                gearTurntablePivot.Rotate(Vector3.up, deltaAngle, Space.World);
                currentRotationAngle = targetAngle;

                // Xoay nhân vật đứng trên bàn xoay (Moving Platform Rotation)
                RotatePlayersOnTurntable(deltaAngle);
            }

            // Kiểm tra hoàn thành
            if (crankProgress >= 1f && !isCompleted)
            {
                CompleteTurntablePuzzle();
            }
        }

        private void RotatePlayersOnTurntable(float deltaAngle)
        {
            if (gearTurntablePivot == null || cachedControllers == null) return;

            Vector3 pivotCenter = gearTurntablePivot.position;
            float gearRadius = 4.2f;

            for (int i = 0; i < cachedControllers.Length; i++)
            {
                var cc = cachedControllers[i];
                if (cc == null || !cc.gameObject.activeInHierarchy) continue;

                Vector3 pPos = cc.transform.position;
                Vector3 diff = pPos - pivotCenter;
                diff.y = 0f;

                // Nếu người chơi đang đứng trên mặt bánh răng (bán kính < 4.2m và độ cao Y ~ 14-16m)
                if (diff.magnitude <= gearRadius && pPos.y >= pivotCenter.y - 0.5f && pPos.y <= pivotCenter.y + 2.5f)
                {
                    cc.transform.RotateAround(pivotCenter, Vector3.up, deltaAngle);
                }
            }
        }

        private void CompleteTurntablePuzzle()
        {
            isCompleted = true;
            crankProgress = 1f;

            if (gearTurntablePivot != null)
            {
                gearTurntablePivot.rotation = Quaternion.Euler(0f, targetRotationAngle, 0f);
            }

            UISoundManager.PlaySuccess();
            ShowPrompt("🎉 BÀN XOAY BÁNH RĂNG ĐÃ KẾT NỐI THÀNH CÔNG! Hai Cha Con có thể cùng nhau qua cầu!", Color.yellow, 6.0f);
            Debug.Log("[GiantGearTurntable] 🎉 Hoàn thành bàn xoay bánh răng đá! Lối qua cầu treo đã mở!");
        }

        private void UpdateVisuals()
        {
            // Hiệu ứng nhấp nháy phát sáng lõi Cyan khi đã gỡ chốt
            if (isPinRemoved && hubMaterial != null)
            {
                float pulse = 0.5f + Mathf.PingPong(Time.time * 2.5f, 0.5f);
                Color glowColor = isCompleted ? Color.green * 1.5f : new Color(0f, 1f, 1f) * (1.2f + pulse);

                if (hubMaterial.HasProperty("_Color"))
                    hubMaterial.color = glowColor;
                else if (hubMaterial.HasProperty("_BaseColor"))
                    hubMaterial.SetColor("_BaseColor", glowColor);
            }
        }

        #endregion

        #region Helpers & Input (Input System Safe)

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
                    Keyboard.current.fKey.wasPressedThisFrame ||
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
            return Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F);
#endif
        }

        private bool IsInteractHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.isPressed ||
                    Keyboard.current.spaceKey.isPressed ||
                    Keyboard.current.fKey.isPressed ||
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
            return Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.F);
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
            if (string.IsNullOrEmpty(currentPrompt) || promptTimer <= 0f) return;

            GUIStyle style = new GUIStyle(GUI.skin.box);
            style.fontSize = 20;
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = promptColor;
            style.alignment = TextAnchor.MiddleCenter;

            float width = 680f;
            float height = 50f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 110f;

            GUI.Box(new Rect(x, y, width, height), currentPrompt, style);
        }

        #endregion
    }
}
