using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Cơ Chế Tời Bánh Răng Khổng Lồ & Đường Trượt Zipline Co-op Sang Vịnh Thuyền Đắm (Stage 3):
    /// 1. Ba & Con lên sàn Stage3_Landing_Deck.
    /// 2. Bánh răng khổng lồ của tời Zipline bị kẹt bởi Khúc Gỗ (Stage3_Winch_JammingWedge).
    ///    - Nếu Ba đến quay: Báo "Bánh răng đang bị kẹt khúc gỗ! Con cần gỡ vật cản trước mới có thể kích hoạt Zipline."
    ///    - Con bấm [E] nhổ khúc gỗ kẹt ra -> Bánh răng được giải phóng.
    /// 3. Ba tiếp cận giữ [Space / E] dùng sức quay Bánh Răng Khổng Lồ -> Kéo Zipline Trolley vào đường cáp.
    /// 4. Hai cha con cùng bấm [E] bám thanh Zipline ngang:
    ///    - Zipline Trolley bám CHÍNH XÁC 100% trên đường dây cáp thực tế.
    ///    - Thanh cầm ngang (Crossbar) nằm ngang vuông góc hướng bay: Ba cầm bên trái, Con cầm bên phải cạnh nhau.
    ///    - Cả hai giơ 2 tay bám thanh trượt suốt chuyến bay vượt vực sâu và đáp xuống boong Tàu Đắm an toàn!
    /// </summary>
    [SelectionBase]
    public class Stage3ZiplineCoopController : MonoBehaviour
    {
        private static Stage3ZiplineCoopController instance;
        public static Stage3ZiplineCoopController Instance => instance;

        #region Serialized Fields

        [Header("Mechanism & Objects")]
        [Tooltip("Trụ đỡ tời trên sàn cất cánh (Stage3_WinchPost)")]
        [SerializeField] private Transform winchPost;

        [Tooltip("Trống quay tời trên sàn cất cánh (Stage3_Winch_Drum)")]
        [SerializeField] private Transform winchDrum;

        [Tooltip("Bánh răng đá khổng lồ trên bục Zone 3 (Giant_Stone_Gear_Body)")]
        [SerializeField] private Transform giantStoneGear;

        [Tooltip("Khúc cây/chốt kẹt bánh răng đá (Zone3_Gear_LockPin)")]
        [SerializeField] private Transform jammingWedge;

        [Tooltip("Ròng rọc trượt Zipline trên cáp")]
        [SerializeField] private Transform ziplineTrolley;

        [Header("Zipline Destinations")]
        [Tooltip("Điểm tiếp đất an toàn trên boong Tàu Đắm")]
        [SerializeField] private Vector3 landingPoint = new Vector3(39.5f, 5.8f, -53.5f);

        [Header("Interaction Settings")]
        [Tooltip("Khoảng cách tương tác với tời của Ba (mét)")]
        [SerializeField] private float winchInteractDistance = 4.0f;

        [Tooltip("Khoảng cách tương tác với khúc cây kẹt bánh răng đá của Con (mét)")]
        [SerializeField] private float wedgeInteractDistance = 3.8f;

        [Tooltip("Khoảng cách tương tác để đu Zipline (mét)")]
        [SerializeField] private float rideInteractDistance = 3.8f;

        [Header("State Status")]
        [SerializeField] private bool isWinchUnlocked = false;
        [SerializeField] private bool isZiplineReady = false;
        [SerializeField] private bool isRiding = false;
        [SerializeField] private bool hasCompletedRide = false;
        [SerializeField] private float winchProgress = 0f;

        /// <summary>Cha đã quay tời thắng cáp (đã khoá được cáp zipline).</summary>
        public bool IsWinchUnlocked => isWinchUnlocked;
        /// <summary>Cáp đã sẵn sàng để đu.</summary>
        public bool IsZiplineReady => isZiplineReady;
        /// <summary>Đang đu zipline.</summary>
        public bool IsRiding => isRiding;
        /// <summary>Đã đu zipline qua vịnh ít nhất 1 lần (đã sang bờ bên kia).</summary>
        public bool HasCompletedRide => hasCompletedRide;
        /// <summary>Tiến độ quay tời thắng cáp 0..1.</summary>
        public float WinchProgress => winchProgress;

        #endregion

        #region Physical Cable Path Spline (Đo đạc chính xác từ mô hình cáp 3D)

        // 11 điểm tâm đường dây cáp thực tế từ sàn Stage 3 qua tháp Pylon tới Tàu Đắm
        private static readonly Vector3[] CableWaypoints = new Vector3[]
        {
            new Vector3(-20.53f, 13.67f, -69.51f), // Điểm xuất phát trên sàn
            new Vector3(-15.86f, 14.38f, -67.95f),
            new Vector3(-9.11f,  15.91f, -65.70f),
            new Vector3(-2.36f,  18.02f, -63.45f),
            new Vector3(3.82f,   20.28f, -61.44f), // Đỉnh tháp Zipline Pylon
            new Vector3(10.05f,  19.75f, -60.19f),
            new Vector3(16.54f,  18.44f, -59.15f),
            new Vector3(23.03f,  17.45f, -58.12f),
            new Vector3(29.52f,  16.81f, -57.08f),
            new Vector3(36.01f,  16.49f, -56.04f),
            new Vector3(40.77f,  16.47f, -55.28f)  // Điểm neo trên cột buồm Tàu Đắm
        };

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
            var wa = GameObject.Find("Stage3_Winch_Assembly");
            if (wa != null) winchPost = wa.transform;
            else if (winchPost == null)
            {
                var wp = GameObject.Find("Stage3_WinchPost");
                if (wp != null) winchPost = wp.transform;
            }

            var wd = GameObject.Find("Stage3_Winch_Drum");
            if (wd != null) winchDrum = wd.transform;

            if (giantStoneGear == null)
            {
                var sg = GameObject.Find("Giant_Stone_Gear_Body");
                if (sg != null) giantStoneGear = sg.transform;
            }

            if (jammingWedge == null)
            {
                var jw = GameObject.Find("Zone3_Gear_LockPin");
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
#if UNITY_2023_1_OR_NEWER
            cachedControllers = Object.FindObjectsByType<CharacterController>(FindObjectsSortMode.None);
#else
            cachedControllers = Object.FindObjectsOfType<CharacterController>();
#endif
        }

        private void EnsureZiplineTrolleyVisual()
        {
            // Tái tạo lại Trolley mới để đảm bảo kích thước thanh cầm thon gọn, thực tế
            var existingTrolley = GameObject.Find("Stage3_Zipline_Trolley");
            if (existingTrolley != null)
            {
                DestroyImmediate(existingTrolley);
                ziplineTrolley = null;
            }

            var trolleyGo = new GameObject("Stage3_Zipline_Trolley");
            trolleyGo.transform.position = CableWaypoints[0];
            ziplineTrolley = trolleyGo.transform;

            // Load vật liệu chất lượng cao từ blend
            Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
            Material matIron = null;
            Material matBrass = null;
            Material matWood = null;

            if (allMaterials != null)
            {
                foreach (var m in allMaterials)
                {
                    if (m == null) continue;
                    if (m.name == "Mat_IronChain") matIron = m;
                    else if (m.name == "Mat_CopperBrass") matBrass = m;
                    else if (m.name == "Mat_DarkTimber") matWood = m;
                }
            }

            // 1. Cụm 2 bánh xe ròng rọc nhỏ kẹp trên dây cáp
            for (int i = 0; i < 2; i++)
            {
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = "Trolley_Wheel_" + i;
                wheel.transform.SetParent(ziplineTrolley, false);
                wheel.transform.localPosition = new Vector3(0f, 0.08f, (i - 0.5f) * 0.28f);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.20f, 0.04f, 0.20f);
                if (matBrass != null) wheel.GetComponent<MeshRenderer>().sharedMaterial = matBrass;
                var col = wheel.GetComponent<Collider>(); if (col != null) DestroyImmediate(col);
            }

            // 2. Khung càng sắt chữ U thon gọn
            var hanger = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hanger.name = "Trolley_Hanger_Frame";
            hanger.transform.SetParent(ziplineTrolley, false);
            hanger.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            hanger.transform.localScale = new Vector3(0.06f, 0.36f, 0.18f);
            if (matIron != null) hanger.GetComponent<MeshRenderer>().sharedMaterial = matIron;
            var hCol = hanger.GetComponent<Collider>(); if (hCol != null) DestroyImmediate(hCol);

            // 3. Thanh nắm tay ngang (Dual-Grip Crossbar) rộng 1.9m cho hai cha con cùng bám cạnh nhau
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bar.name = "Trolley_Horizontal_Handlebar";
            bar.transform.SetParent(ziplineTrolley, false);
            bar.transform.localPosition = new Vector3(0f, -0.40f, 0f);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            bar.transform.localScale = new Vector3(0.045f, 0.95f, 0.045f);
            if (matIron != null) bar.GetComponent<MeshRenderer>().sharedMaterial = matIron;
            var bCol = bar.GetComponent<Collider>(); if (bCol != null) DestroyImmediate(bCol);

            // Tay nắm bọc gỗ bên trái cho Ba
            var dadGrip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dadGrip.name = "Handle_Grip_Dad";
            dadGrip.transform.SetParent(bar.transform, false);
            dadGrip.transform.localPosition = new Vector3(0f, -0.40f, 0f);
            dadGrip.transform.localScale = new Vector3(1.35f, 0.45f, 1.35f);
            if (matWood != null) dadGrip.GetComponent<MeshRenderer>().sharedMaterial = matWood;
            var dgCol = dadGrip.GetComponent<Collider>(); if (dgCol != null) DestroyImmediate(dgCol);

            // Tay nắm bọc gỗ bên phải cho Con
            var sonGrip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sonGrip.name = "Handle_Grip_Son";
            sonGrip.transform.SetParent(bar.transform, false);
            sonGrip.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            sonGrip.transform.localScale = new Vector3(1.35f, 0.32f, 1.35f);
            if (matWood != null) sonGrip.GetComponent<MeshRenderer>().sharedMaterial = matWood;
            var sgCol = sonGrip.GetComponent<Collider>(); if (sgCol != null) DestroyImmediate(sgCol);

            ziplineTrolley.gameObject.SetActive(false);
        }

        #endregion

        #region Interaction & Gameplay Logic

        private void HandleInteractions()
        {
            if (isRiding || hasCompletedRide) return;

            Vector3 winchPos = winchDrum != null ? winchDrum.position : new Vector3(-26.2f, 15.35f, -71.2f);
            Vector3 jammedPos = jammingWedge != null ? jammingWedge.position : new Vector3(2.15f, 14.65f, -64.55f);

            // 1. Giai đoạn 1: Bánh răng đá khổng lồ trên kia (Giant_Stone_Gear_Body) bị kẹt cây
            if (!isWinchUnlocked)
            {
                // Kiểm tra xem chốt ở GiantGearTurntableController đã mở chưa
                if (GiantGearTurntableController.Instance != null && GiantGearTurntableController.Instance.IsPinRemoved)
                {
                    isWinchUnlocked = true;
                }
                else
                {
                    // Khi Ba tiếp cận tời trên sàn cất cánh lúc bánh răng đá trên kia đang kẹt
                    PlayerRoleVisuals visualsWinch;
                    CharacterController playerNearWinch = GetNearestPlayer(winchPos, winchInteractDistance, out visualsWinch);
                    if (playerNearWinch != null)
                    {
                        bool isDad = visualsWinch != null ? visualsWinch.IsDad : playerNearWinch.name.ToLower().Contains("dad");
                        if (isDad)
                        {
                            ShowPrompt("⚙️ BÁNH RĂNG ĐÁ KHỔNG LỒ (Giant Stone Gear) TRÊN KIA ĐANG BỊ KẸT CÂY! Con cần lên gỡ khúc cây kẹt trước mới có thể kích hoạt Zipline.", new Color(1f, 0.65f, 0.1f));
                        }
                    }

                    // Khi Con lên bục bánh răng đá tiếp cận khúc cây kẹt
                    PlayerRoleVisuals visualsPin;
                    CharacterController playerNearPin = GetNearestPlayer(jammedPos, wedgeInteractDistance, out visualsPin);
                    if (playerNearPin != null)
                    {
                        bool isDad = visualsPin != null ? visualsPin.IsDad : playerNearPin.name.ToLower().Contains("dad");
                        if (!isDad)
                        {
                            ShowPrompt("👶 [E] Nhổ Khúc Cây Kẹt Bánh Răng Đá Khổng Lồ", Color.cyan);
                            if (IsInteractPressed())
                            {
                                UnlockWinch();
                            }
                        }
                        else
                        {
                            ShowPrompt("🚫 Khúc cây kẹt trong khe đá hẹp, chỉ 👶 Con mới gỡ được!", Color.yellow);
                        }
                    }
                    return;
                }
            }

            // 2. Giai đoạn 2: Khúc cây đã gỡ -> Ba lên xoay tời ở sàn cất cánh
            if (!isZiplineReady)
            {
                PlayerRoleVisuals nearestVisuals;
                CharacterController nearestPlayer = GetNearestPlayer(winchPos, winchInteractDistance, out nearestVisuals);

                if (nearestPlayer != null)
                {
                    bool isDad = nearestVisuals != null ? nearestVisuals.IsDad : nearestPlayer.name.ToLower().Contains("dad");

                    if (!isDad)
                    {
                        ShowPrompt("🚫 Cần tời kéo cáp rất nặng! Cần sức mạnh của 🧔 Ba để quay!", Color.red);
                    }
                    else
                    {
                        ShowPrompt($"🧔 [Giữ SPACE / E] Dùng sức quay tời kích hoạt Zipline ({Mathf.RoundToInt(winchProgress * 100)}%)", Color.green);

                        if (IsInteractHeld())
                        {
                            PerformWinchCranking();
                        }
                    }
                }
                return;
            }

            // 3. Giai đoạn 3: Zipline đã sẵn sàng, cả hai cùng bám thanh đu dây
            if (isZiplineReady && !hasCompletedRide)
            {
                PlayerRoleVisuals nearestVisuals;
                CharacterController nearestPlayer = GetNearestPlayer(CableWaypoints[0], rideInteractDistance, out nearestVisuals);

                if (nearestPlayer != null)
                {
                    ShowPrompt("✨ [E] Hai Cha Con Cùng Nhau Bám Thanh Zipline Đu Sang Thuyền Đắm!", Color.yellow);

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
            UISoundManager.PlayConfirm();

            StartCoroutine(RemoveJammingWedgeRoutine());

            ShowPrompt("✨ ĐÃ GỠ KẸT BÁNH RĂNG ĐÁ! Ba hãy lên quay tời trên sàn cất cánh để kích hoạt Zipline!", Color.green, 5.0f);
            Debug.Log("[Stage3Zipline] Khúc cây kẹt bánh răng đá khổng lồ đã được Con gỡ ra thành công!");
        }

        private IEnumerator RemoveJammingWedgeRoutine()
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

            // Xoay trống tời trên sàn cất cánh
            if (winchDrum != null)
            {
                winchDrum.Rotate(Vector3.forward, 150f * Time.deltaTime, Space.Self);
            }

            // Bánh răng đá khổng lồ trên kia cũng xoay nhè nhẹ theo truyền động
            if (giantStoneGear != null)
            {
                giantStoneGear.Rotate(Vector3.up, 25f * Time.deltaTime, Space.World);
            }

            winchProgress += Time.deltaTime / 3.5f;

            if (winchProgress >= 1f)
            {
                isZiplineReady = true;
                winchProgress = 1f;

                if (ziplineTrolley != null)
                {
                    ziplineTrolley.gameObject.SetActive(true);
                    ziplineTrolley.position = CableWaypoints[0];
                }

                UISoundManager.PlaySuccess();
                ShowPrompt("⚓ ZIPLINE ĐÃ SẴN SÀNG! Tiến lại gần và nhấn [E] để cùng Con đu sang Tàu Đắm!", Color.green, 5.0f);
                Debug.Log("[Stage3Zipline] Bánh răng đá đã kích hoạt Zipline vào bệ xuất phát thành công!");
            }
        }

        #endregion

        #region Cable Mathematics & Spline Interpolation

        /// <summary>
        /// Tính toán tọa độ và hướng tiếp tuyến chính xác 100% trên đường dây cáp thực tế.
        /// </summary>
        public static void EvaluateCable(float t, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            t = Mathf.Clamp01(t);
            int count = CableWaypoints.Length;
            float scaledT = t * (count - 1);
            int idx = Mathf.Clamp(Mathf.FloorToInt(scaledT), 0, count - 2);
            float u = scaledT - idx;

            // Đa thức Catmull-Rom cho độ mượt hoàn hảo qua các điểm neo
            Vector3 p0 = CableWaypoints[Mathf.Max(0, idx - 1)];
            Vector3 p1 = CableWaypoints[idx];
            Vector3 p2 = CableWaypoints[idx + 1];
            Vector3 p3 = CableWaypoints[Mathf.Min(count - 1, idx + 2)];

            position = 0.5f * (
                (2f * p1) +
                (-p0 + p2) * u +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * (u * u) +
                (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u * u)
            );

            // Đạo hàm tìm hướng bay tiếp tuyến (Tangent)
            Vector3 tangent = 0.5f * (
                (-p0 + p2) +
                2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * u +
                3f * (-p0 + 3f * p1 - 3f * p2 + p3) * (u * u)
            );

            forward = tangent.normalized;
            if (forward == Vector3.zero) forward = Vector3.right;

            // Trục ngang Right vuông góc với cáp: Thanh cầm ngang sẽ nằm dọc theo trục này
            right = Vector3.Cross(Vector3.up, forward).normalized;
            up = Vector3.Cross(forward, right).normalized;
        }

        #endregion

        #region Cinematic Zipline Ride Sequence (Đu Zipline Ngang, 2 Tay Giơ Cao Bám Thanh)

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

            // Tạm khóa CharacterController, CoreMovement và CoreAnimator để không bị ghi đè hoạt ảnh và di chuyển
            var dadCoreAnim = dadCc != null ? dadCc.GetComponentInChildren<CoreAnimator>() : null;
            var sonCoreAnim = sonCc != null ? sonCc.GetComponentInChildren<CoreAnimator>() : null;
            if (dadCoreAnim != null) dadCoreAnim.enabled = false;
            if (sonCoreAnim != null) sonCoreAnim.enabled = false;

            var dadMove = dadCc != null ? dadCc.GetComponent<CoreMovement>() : null;
            var sonMove = sonCc != null ? sonCc.GetComponent<CoreMovement>() : null;
            if (dadMove != null) dadMove.enabled = false;
            if (sonMove != null) sonMove.enabled = false;

            if (dadCc != null) dadCc.enabled = false;
            if (sonCc != null) sonCc.enabled = false;

            // Bật và đóng băng tư thế nhảy trên không giơ 2 tay lên trời (InAir motion Jump_InAir)
            Animator dadAnim = dadCc != null ? dadCc.GetComponentInChildren<Animator>() : null;
            Animator sonAnim = sonCc != null ? sonCc.GetComponentInChildren<Animator>() : null;

            if (dadAnim != null)
            {
                dadAnim.Play("InAir", 0, 0.40f);
                dadAnim.speed = 0f; // Đóng băng động tác giơ 2 tay lên trời nắm thanh
            }
            if (sonAnim != null)
            {
                sonAnim.Play("InAir", 0, 0.40f);
                sonAnim.speed = 0f; // Đóng băng động tác giơ 2 tay lên trời nắm thanh
            }

            // Tạm ngắt CinemachineBrain để camera điện ảnh điều khiển góc máy
            if (cinemachineBrainComponent != null)
            {
                cinemachineBrainComponent.enabled = false;
            }

            if (ziplineTrolley != null)
            {
                ziplineTrolley.gameObject.SetActive(true);
            }

            UISoundManager.PlaySuccess();

            float totalRideDuration = 6.2f;
            float rideTimer = 0f;

            while (rideTimer < totalRideDuration)
            {
                rideTimer += Time.deltaTime;
                float t = Mathf.Clamp01(rideTimer / totalRideDuration);

                // Tính tọa độ bám cáp chính xác 100%
                Vector3 cablePos, forwardDir, rightDir, upDir;
                EvaluateCable(t, out cablePos, out forwardDir, out rightDir, out upDir);

                // Cập nhật vị trí và góc xoay của xe trượt Trolley khớp hoàn hảo trên cáp
                if (ziplineTrolley != null)
                {
                    ziplineTrolley.position = cablePos;
                    ziplineTrolley.rotation = Quaternion.LookRotation(forwardDir, upDir);

                    // Đồng bộ vị trí và góc xoay của Ba & Con bám thanh ngang bằng TransformPoint (Tương thích 100% Netcode)
                    // Thanh cầm rộng 1.9m: Ba cầm bên trái (-0.38m), Con cầm bên phải (+0.35m) cạnh nhau tự nhiên
                    if (dadCc != null)
                    {
                        dadCc.transform.position = ziplineTrolley.TransformPoint(new Vector3(-0.38f, -2.72f, -0.15f));
                        dadCc.transform.rotation = ziplineTrolley.rotation * Quaternion.Euler(6f, 0f, 0f);
                    }
                    if (sonCc != null)
                    {
                        sonCc.transform.position = ziplineTrolley.TransformPoint(new Vector3(0.35f, -1.49f, -0.15f));
                        sonCc.transform.rotation = ziplineTrolley.rotation * Quaternion.Euler(6f, 0f, 0f);
                    }
                }

                // Camera điện ảnh góc nhìn nghiêng bao quát toàn cảnh vượt vực hồ
                if (mainCamera != null)
                {
                    Vector3 camOffset = -forwardDir * 4.5f + rightDir * 3.8f + Vector3.up * 1.6f;
                    mainCamera.transform.position = cablePos + camOffset;
                    mainCamera.transform.LookAt(cablePos - Vector3.up * 0.6f);
                }

                yield return null;
            }

            // Giai đoạn tiếp đất an toàn trên boong Tàu Đắm
            float dropDuration = 0.90f;
            float dropTimer = 0f;
            Vector3 dadStartDrop = dadCc != null ? dadCc.transform.position : CableWaypoints[CableWaypoints.Length - 1];
            Vector3 sonStartDrop = sonCc != null ? sonCc.transform.position : CableWaypoints[CableWaypoints.Length - 1];
            Vector3 dadLandPos = landingPoint + new Vector3(-0.75f, 0f, 0f);
            Vector3 sonLandPos = landingPoint + new Vector3(0.75f, 0f, 0f);

            while (dropTimer < dropDuration)
            {
                dropTimer += Time.deltaTime;
                float dt = Mathf.SmoothStep(0f, 1f, dropTimer / dropDuration);

                if (dadCc != null) dadCc.transform.position = Vector3.Lerp(dadStartDrop, dadLandPos, dt);
                if (sonCc != null) sonCc.transform.position = Vector3.Lerp(sonStartDrop, sonLandPos, dt);

                if (mainCamera != null)
                {
                    mainCamera.transform.position = Vector3.Lerp(mainCamera.transform.position, landingPoint + new Vector3(-4.5f, 3.2f, -4.5f), dt);
                    mainCamera.transform.LookAt(landingPoint + Vector3.up * 1f);
                }

                yield return null;
            }

            // Tiếp đất: Mở lại tốc độ hoạt ảnh và chuyển sang JumpLand
            if (dadAnim != null)
            {
                dadAnim.speed = 1f;
                dadAnim.CrossFade("JumpLand", 0.1f);
            }
            if (sonAnim != null)
            {
                sonAnim.speed = 1f;
                sonAnim.CrossFade("JumpLand", 0.1f);
            }

            if (dadCoreAnim != null) dadCoreAnim.enabled = true;
            if (sonCoreAnim != null) sonCoreAnim.enabled = true;

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

            if (dadMove != null) dadMove.enabled = true;
            if (sonMove != null) sonMove.enabled = true;

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

            GUI.depth = -100;
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleCenter;
            boxStyle.fontSize = 20;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = promptColor;

            float width = 850f;
            float height = 60f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 110f;

            GUI.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0.88f);
            GUI.Box(new Rect(x, y, width, height), currentPrompt, boxStyle);
        }

        #endregion
    }
}
