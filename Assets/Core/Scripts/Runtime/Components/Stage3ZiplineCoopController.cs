using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Cơ Chế Tời & Đường Trượt Zipline Co-op Sang Vịnh Thuyền Đắm (Stage 3):
    /// 1. Ba & Con lên sàn Stage3_Landing_Deck.
    /// 2. Bánh răng tời Zipline bị kẹt bởi Khúc Gỗ / Chốt Nêm (Stage3_Winch_JammingWedge).
    /// 3. Con tiếp cận bấm [E] nhổ chốt nêm ra -> Tay cầm quay tời vẫn giữ nguyên sẵn sàng cho Ba.
    /// 4. Ba giữ [Space / E] dùng sức quay tời, kéo ròng rọc cáp Zipline vào vị trí xuất phát.
    /// 5. Bấm [E] kích hoạt đoạn Cinematic: Cả hai Cha Con cùng nhảy lên, 2 tay giơ cao bám thanh trượt Zipline, lướt vút qua vực sâu và hồ nước sang đáp xuống boong Tàu Đắm (Shipwreck)!
    /// </summary>
    [SelectionBase]
    public class Stage3ZiplineCoopController : MonoBehaviour
    {
        private static Stage3ZiplineCoopController instance;
        public static Stage3ZiplineCoopController Instance => instance;

        #region Serialized Fields

        [Header("Winch & Mechanism Objects")]
        [Tooltip("Trụ đỡ tời (Stage3_WinchPost)")]
        [SerializeField] private Transform winchPost;

        [Tooltip("Bánh răng / tang tời Zipline (Stage3_Winch_Drum)")]
        [SerializeField] private Transform winchDrum;

        [Tooltip("Tay quay của tời (Stage3_Winch_Grip)")]
        [SerializeField] private Transform winchGrip;

        [Tooltip("Khúc gỗ chèn kẹt tời (Stage3_Winch_JammingWedge)")]
        [SerializeField] private Transform jammingWedge;

        [Tooltip("Ròng rọc trượt Zipline trên cáp")]
        [SerializeField] private Transform ziplineTrolley;

        [Header("Zipline Waypoints")]
        [Tooltip("Điểm xuất phát trên sàn Stage3_Landing_Deck")]
        [SerializeField] private Vector3 launchPoint = new Vector3(-23.5f, 15.8f, -70.5f);

        [Tooltip("Điểm giữa tại tháp Zipline_Pylon")]
        [SerializeField] private Vector3 midPylonPoint = new Vector3(5.0f, 18.2f, -61.0f);

        [Tooltip("Điểm neo trên đài quan sát Tàu Đắm")]
        [SerializeField] private Vector3 shipAnchorPoint = new Vector3(42.68f, 16.54f, -55.08f);

        [Tooltip("Điểm tiếp đất an toàn trên boong Tàu Đắm")]
        [SerializeField] private Vector3 landingPoint = new Vector3(39.5f, 5.8f, -53.5f);

        [Header("Interaction Settings")]
        [Tooltip("Khoảng cách tương tác với tời của Ba (mét)")]
        [SerializeField] private float winchInteractDistance = 4.0f;

        [Tooltip("Khoảng cách tương tác với khúc gỗ chèn của Con (mét)")]
        [SerializeField] private float wedgeInteractDistance = 3.5f;

        [Tooltip("Khoảng cách tương tác để đu Zipline (mét)")]
        [SerializeField] private float rideInteractDistance = 3.8f;

        [Header("State Status")]
        [SerializeField] private bool isWinchUnlocked = false;
        [SerializeField] private bool isZiplineReady = false;
        [SerializeField] private bool isRiding = false;
        [SerializeField] private bool hasCompletedRide = false;
        [SerializeField] private float winchProgress = 0f;

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private string currentPrompt = "";
        private float promptTimer = 0f;
        private Color promptColor = Color.yellow;

        private CharacterController[] cachedControllers;
        private float lastUpdateTime = 0f;
        private Behaviour cinemachineBrainComponent;

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
            mainCamera = Camera.main;
            FindCinemachineBrain();
            CachePlayers();
            EnsureZiplineTrolleyVisual();
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            if (promptTimer > 0f)
            {
                promptTimer -= Time.deltaTime;
                if (promptTimer <= 0f) currentPrompt = "";
            }

            if (Time.time - lastUpdateTime > 1.0f)
            {
                CachePlayers();
                lastUpdateTime = Time.time;
            }

            HandleInteractions();
        }

        #endregion

        #region Auto Setup & References

        public void AutoFindReferences()
        {
            if (winchPost == null)
            {
                var wp = GameObject.Find("Stage3_WinchPost");
                if (wp != null) winchPost = wp.transform;
            }

            if (winchDrum == null)
            {
                var wd = GameObject.Find("Stage3_Winch_Drum");
                if (wd != null) winchDrum = wd.transform;
            }

            if (winchGrip == null)
            {
                var wg = GameObject.Find("Stage3_Winch_Grip");
                if (wg != null) winchGrip = wg.transform;
            }

            if (jammingWedge == null)
            {
                var jw = GameObject.Find("Stage3_Winch_JammingWedge");
                if (jw != null) jammingWedge = jw.transform;
            }

            var tr = GameObject.Find("Stage3_Zipline_Trolley");
            if (tr != null) ziplineTrolley = tr.transform;
        }

        private void FindCinemachineBrain()
        {
            if (mainCamera != null)
            {
                var comps = mainCamera.GetComponents<Behaviour>();
                foreach (var c in comps)
                {
                    if (c.GetType().Name.Contains("CinemachineBrain"))
                    {
                        cinemachineBrainComponent = c;
                        break;
                    }
                }
            }
        }

        private void CachePlayers()
        {
            cachedControllers = FindObjectsOfType<CharacterController>();
        }

        private void EnsureZiplineTrolleyVisual()
        {
            if (ziplineTrolley != null) return;

            var trolleyGo = new GameObject("Stage3_Zipline_Trolley");
            trolleyGo.transform.position = launchPoint;
            ziplineTrolley = trolleyGo.transform;

            // Bánh xe ròng rọc kép kẹp cáp
            for (int i = 0; i < 2; i++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Trolley_Wheel_" + i;
                wheel.transform.SetParent(ziplineTrolley, false);
                wheel.transform.localPosition = new Vector3((i - 0.5f) * 0.35f, 0.35f, 0f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.45f, 0.12f, 0.45f);
                var wRend = wheel.GetComponent<Renderer>();
                wRend.sharedMaterial.color = new Color(0.85f, 0.65f, 0.2f);
                var wCol = wheel.GetComponent<Collider>();
                if (wCol != null) DestroyImmediate(wCol);
            }

            // Càng kim loại chữ A
            var hanger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hanger.name = "Trolley_Hanger";
            hanger.transform.SetParent(ziplineTrolley, false);
            hanger.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            hanger.transform.localScale = new Vector3(0.45f, 0.65f, 0.12f);
            var hRend = hanger.GetComponent<Renderer>();
            hRend.sharedMaterial.color = new Color(0.2f, 0.2f, 0.25f);
            var hCol = hanger.GetComponent<Collider>();
            if (hCol != null) DestroyImmediate(hCol);

            // Thanh nắm tay ngang (Handle Bar) - để 2 người cùng giơ tay bám
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "Trolley_HandleBar";
            bar.transform.SetParent(ziplineTrolley, false);
            bar.transform.localPosition = new Vector3(0f, -0.45f, 0f);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bar.transform.localScale = new Vector3(0.12f, 1.1f, 0.12f);
            var bRend = bar.GetComponent<Renderer>();
            bRend.sharedMaterial.color = new Color(0.9f, 0.75f, 0.3f);
            var bCol = bar.GetComponent<Collider>();
            if (bCol != null) DestroyImmediate(bCol);

            ziplineTrolley.gameObject.SetActive(false);
        }

        #endregion

        #region Interaction & Gameplay Logic

        private void HandleInteractions()
        {
            if (isRiding || hasCompletedRide) return;

            Vector3 winchPos = winchDrum != null ? winchDrum.position : new Vector3(-26.6f, 15.5f, -71.2f);

            // 1. Giai đoạn 1: Bánh răng tời Zipline bị kẹt khúc gỗ / chốt nêm
            if (!isWinchUnlocked)
            {
                PlayerRoleVisuals nearestVisuals;
                CharacterController nearestPlayer = GetNearestPlayer(winchPos, wedgeInteractDistance, out nearestVisuals);

                if (nearestPlayer != null)
                {
                    bool isDad = nearestVisuals != null ? nearestVisuals.IsDad : nearestPlayer.name.ToLower().Contains("dad");

                    if (isDad)
                    {
                        ShowPrompt("🔒 Tời Zipline bị kẹt khúc gỗ chèn bánh răng! Hãy để 👶 Con gỡ ra!", Color.yellow);
                    }
                    else
                    {
                        ShowPrompt("👶 [E] Nhổ Khúc Gỗ Chèn Kẹt Bánh Răng Tời", Color.cyan);

                        if (IsInteractPressed())
                        {
                            UnlockWinch();
                        }
                    }
                }
                return;
            }

            // 2. Giai đoạn 2: Ba quay cần tời Zipline
            if (!isZiplineReady)
            {
                PlayerRoleVisuals nearestVisuals;
                CharacterController nearestPlayer = GetNearestPlayer(winchPos, winchInteractDistance, out nearestVisuals);

                if (nearestPlayer != null)
                {
                    bool isDad = nearestVisuals != null ? nearestVisuals.IsDad : nearestPlayer.name.ToLower().Contains("dad");

                    if (!isDad)
                    {
                        ShowPrompt("🚫 Cần tời Zipline quá nặng! Cần sức mạnh của 🧔 Ba để quay!", Color.red);
                    }
                    else
                    {
                        ShowPrompt($"🧔 [Giữ SPACE / E] Dùng sức quay cần tời Zipline ({Mathf.RoundToInt(winchProgress * 100)}%)", Color.green);

                        if (IsInteractHeld())
                        {
                            PerformWinchCranking();
                        }
                    }
                }
                return;
            }

            // 3. Giai đoạn 3: Zipline đã sẵn sàng, cả hai cùng đu dây
            if (isZiplineReady && !hasCompletedRide)
            {
                PlayerRoleVisuals nearestVisuals;
                CharacterController nearestPlayer = GetNearestPlayer(launchPoint, rideInteractDistance, out nearestVisuals);

                if (nearestPlayer != null)
                {
                    ShowPrompt("✨ [E] Hai Cha Con Cùng Nhau Đu Zipline Sang Thuyền Đắm!", Color.yellow);

                    if (IsInteractPressed())
                    {
                        StartCoroutine(PlayZiplineCinematicRide());
                    }
                }
            }
        }

        private void UnlockWinch()
        {
            isWinchUnlocked = true;
            UISoundManager.PlayClick();

            if (jammingWedge != null)
            {
                StartCoroutine(AnimateWedgeEject());
            }

            ShowPrompt("✨ ĐÃ NHỔ KHÚC GỖ KẸT! 🧔 Ba hãy quay cần tời để kéo ròng rọc cáp!", Color.cyan, 4.0f);
            Debug.Log("[Stage3Zipline] 👶 Con đã gỡ khúc gỗ chèn tời Zipline thành công!");
        }

        private IEnumerator AnimateWedgeEject()
        {
            if (jammingWedge == null) yield break;

            Vector3 startPos = jammingWedge.position;
            Quaternion startRot = jammingWedge.rotation;
            float dur = 0.65f;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                jammingWedge.position = startPos + Vector3.up * (Mathf.Sin(t * Mathf.PI) * 1.6f) + Vector3.left * (t * 2.2f);
                jammingWedge.rotation = startRot * Quaternion.Euler(t * 360f, t * 180f, 0f);
                jammingWedge.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
                yield return null;
            }

            jammingWedge.gameObject.SetActive(false);
        }

        private void PerformWinchCranking()
        {
            if (isZiplineReady) return;

            // Xoay tang tời và tay quay
            if (winchDrum != null)
            {
                winchDrum.Rotate(Vector3.up, 360f * Time.deltaTime, Space.Self);
            }

            winchProgress += Time.deltaTime / 4.0f;

            if (winchProgress >= 1f)
            {
                isZiplineReady = true;
                winchProgress = 1f;

                if (ziplineTrolley != null)
                {
                    ziplineTrolley.gameObject.SetActive(true);
                    ziplineTrolley.position = launchPoint;
                }

                UISoundManager.PlaySuccess();
                ShowPrompt("🎉 RÒNG RỌC ZIPLINE ĐÃ VÀO VỊ TRÍ! Hai Cha Con có thể cùng nhau đu cáp sang thuyền!", Color.yellow, 5.0f);
                Debug.Log("[Stage3Zipline] 🎉 Zipline đã được kéo căng và vào vị trí xuất phát!");
            }
        }

        #endregion

        #region Cinematic Zipline Ride Sequence (Nhảy lên giơ 2 tay bám thanh)

        private IEnumerator PlayZiplineCinematicRide()
        {
            isRiding = true;
            currentPrompt = "";

            CharacterController dadCc = null;
            CharacterController sonCc = null;

            if (cachedControllers != null)
            {
                foreach (var cc in cachedControllers)
                {
                    if (cc == null) continue;
                    var vis = cc.GetComponentInParent<PlayerRoleVisuals>();
                    bool isDad = vis != null ? vis.IsDad : cc.name.ToLower().Contains("dad");
                    if (isDad && dadCc == null) dadCc = cc;
                    else if (!isDad && sonCc == null) sonCc = cc;
                }
            }

            // Tạm khóa CharacterController để di chuyển mượt mà
            if (dadCc != null) dadCc.enabled = false;
            if (sonCc != null) sonCc.enabled = false;

            // Tìm Animator để bật tư thế giơ 2 tay lên trời (Jump_InAir)
            Animator dadAnim = dadCc != null ? dadCc.GetComponentInChildren<Animator>() : null;
            Animator sonAnim = sonCc != null ? sonCc.GetComponentInChildren<Animator>() : null;

            if (dadAnim != null) dadAnim.CrossFade("Jump_InAir", 0.15f);
            if (sonAnim != null) sonAnim.CrossFade("Jump_InAir", 0.15f);

            // Tạm ngắt CinemachineBrain để điều khiển camera góc rộng điện ảnh
            if (cinemachineBrainComponent != null)
            {
                cinemachineBrainComponent.enabled = false;
            }

            if (ziplineTrolley != null)
            {
                ziplineTrolley.gameObject.SetActive(true);
            }

            UISoundManager.PlaySuccess();

            // Độ cao bám tay: Thanh ngang ở trolley Y -0.45m
            // Ba cao 1.85m -> Gốc chân Ba ở -2.25m để 2 tay giơ cao bám trúng thanh ngang
            // Con cao 1.20m -> Gốc chân Con ở -1.65m để 2 tay giơ cao bám trúng thanh ngang
            float totalRideDuration = 6.0f;
            float rideTimer = 0f;

            while (rideTimer < totalRideDuration)
            {
                rideTimer += Time.deltaTime;
                float t = Mathf.Clamp01(rideTimer / totalRideDuration);

                // Đường bay dọc cáp
                Vector3 currentZiplinePos;
                if (t < 0.45f)
                {
                    float subT = Mathf.SmoothStep(0f, 1f, t / 0.45f);
                    currentZiplinePos = Vector3.Lerp(launchPoint, midPylonPoint, subT);
                }
                else
                {
                    float subT = Mathf.SmoothStep(0f, 1f, (t - 0.45f) / 0.55f);
                    currentZiplinePos = Vector3.Lerp(midPylonPoint, shipAnchorPoint, subT);
                }

                // Cập nhật vị trí ròng rọc
                if (ziplineTrolley != null)
                {
                    ziplineTrolley.position = currentZiplinePos;
                }

                // Hướng mặt bay theo chiều cáp
                Quaternion faceRot = Quaternion.LookRotation(new Vector3(1f, -0.2f, 0.15f), Vector3.up);

                // Ba & Con giơ 2 tay bám 2 bên thanh ngang Zipline
                if (dadCc != null)
                {
                    dadCc.transform.position = currentZiplinePos + new Vector3(-0.35f, -2.25f, 0f);
                    dadCc.transform.rotation = faceRot;
                }
                if (sonCc != null)
                {
                    sonCc.transform.position = currentZiplinePos + new Vector3(0.35f, -1.65f, 0f);
                    sonCc.transform.rotation = faceRot;
                }

                // Camera điện ảnh góc nhìn nghiêng bao quát toàn bộ thanh trượt & 2 cha con
                if (mainCamera != null)
                {
                    Vector3 camOffset = new Vector3(-5.5f, -0.8f, -4.5f);
                    mainCamera.transform.position = currentZiplinePos + camOffset;
                    mainCamera.transform.LookAt(currentZiplinePos + new Vector3(0f, -1.2f, 0f));
                }

                yield return null;
            }

            // Giai đoạn tiếp đất an toàn trên boong Tàu Đắm
            float dropDuration = 0.85f;
            float dropTimer = 0f;
            Vector3 dadStartDrop = dadCc != null ? dadCc.transform.position : shipAnchorPoint;
            Vector3 sonStartDrop = sonCc != null ? sonCc.transform.position : shipAnchorPoint;
            Vector3 dadLandPos = landingPoint + new Vector3(-0.65f, 0f, 0f);
            Vector3 sonLandPos = landingPoint + new Vector3(0.65f, 0f, 0f);

            while (dropTimer < dropDuration)
            {
                dropTimer += Time.deltaTime;
                float dt = Mathf.SmoothStep(0f, 1f, dropTimer / dropDuration);

                if (dadCc != null) dadCc.transform.position = Vector3.Lerp(dadStartDrop, dadLandPos, dt);
                if (sonCc != null) sonCc.transform.position = Vector3.Lerp(sonStartDrop, sonLandPos, dt);

                if (mainCamera != null)
                {
                    mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, landingPoint + new Vector3(-4.5f, 3.0f, -4.5f), dt);
                    mainCamera.transform.LookAt(landingPoint + Vector3.up * 1f);
                }

                yield return null;
            }

            // Tiếp đất: Đổi hoạt ảnh sang JumpLand / Idle
            if (dadAnim != null) dadAnim.CrossFade("JumpLand", 0.1f);
            if (sonAnim != null) sonAnim.CrossFade("JumpLand", 0.1f);

            if (dadCc != null)
            {
                dadCc.transform.position = dadLandPos;
                dadCc.enabled = true;
            }
            if (sonCc != null)
            {
                sonCc.transform.position = sonLandPos;
                sonCc.enabled = true;
            }

            // Trả lại camera tự do
            if (cinemachineBrainComponent != null)
            {
                cinemachineBrainComponent.enabled = true;
            }

            isRiding = false;
            hasCompletedRide = true;

            UISoundManager.PlaySuccess();
            ShowPrompt("⚓ ĐÃ ĐÁP XUỐNG BOONG TÀU ĐẮM! Hai Cha Con đã sang tới Vịnh Thuyền Đắm an toàn!", Color.green, 6.0f);
            Debug.Log("[Stage3Zipline] ⚓ Hai Cha Con đã đáp xuống boong Tàu Đắm thành công!");
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

            float width = 720f;
            float height = 50f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 110f;

            GUI.Box(new Rect(x, y, width, height), currentPrompt, style);
        }

        #endregion
    }
}
