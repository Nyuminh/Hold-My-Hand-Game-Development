using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Chuỗi Cơ Chế Nhiệm Vụ Co-op Trên Tàu Đắm Cổ (Stage 3 Shipwreck):
    /// 1. Giai đoạn 1: Sàn trước (Deck Fore) và Sàn sau (Deck Aft) bị chia cắt bởi vực đứt gãy.
    ///    - Con bám thang dây leo lên Đài Quan Sát (Crow's Nest) trên đỉnh cột buồm.
    ///    - Con bấm [E] tháo dây neo cầu treo -> Cầu gập (Shipwreck_Deck_Bridge) hạ phẳng 0° nối thông 2 sàn tàu!
    /// 2. Giai đoạn 2: Ba & Con bước qua sàn sau (Quarterdeck):
    ///    - Ba dùng sức giữ [Space / E] xoay Bánh Lái Thuyền Trưởng (Captain's Helm) 100%.
    ///    - Trục cơ quan kích hoạt mở toang Nắp Rương Kho Báu Thuyền Trưởng (HMH_West_ChestLid).
    ///    - Mở rương kho báu -> Tiến sang Cầu Cửa Vịnh.
    /// </summary>
    [SelectionBase]
    public class ShipwreckCoopController : MonoBehaviour
    {
        private static ShipwreckCoopController instance;
        public static ShipwreckCoopController Instance => instance;

        #region Serialized Fields

        [Header("Phase 1: Bridge & Mast Climb")]
        [Tooltip("Cầu gỗ gập nối 2 sàn tàu")]
        [SerializeField] private Transform deckBridge;

        [Tooltip("Cần gạt khóa chân buồm (HMH_West_LeverArm)")]
        [SerializeField] private Transform mastBaseLever;

        [Tooltip("Vị trí tương tác trên đài quan sát (Crow's Nest)")]
        [SerializeField] private Vector3 crowsNestInteractPos = new Vector3(42.50f, 16.20f, -55.00f);

        [Header("Phase 2: Steering Wheel & Captain's Chest")]
        [Tooltip("Bánh lái tàu cổ (Ship_SteeringWheel)")]
        [SerializeField] private Transform steeringWheel;

        [Tooltip("Rương kho báu thuyền trưởng (HMH_West_Chest)")]
        [SerializeField] private Transform treasureChest;

        [Tooltip("Nắp rương kho báu (HMH_West_ChestLid)")]
        [SerializeField] private Transform chestLid;

        [Tooltip("Vàng trong rương (HMH_West_ChestGold)")]
        [SerializeField] private Transform chestGold;

        [Tooltip("Chìa khóa vàng thuyền trưởng")]
        [SerializeField] private Transform captainKey;

        [Tooltip("Cầu thang đá cổ ẩn phía sau đuôi tàu")]
        [SerializeField] private Transform hiddenStaircase;

        [Header("Phase 3: Exit Gangplank")]
        [Tooltip("Cầu tàu dẫn xuống bến cảng (Cove_Gangplank_Ship)")]
        [SerializeField] private Transform exitGangplank;

        [Header("Quest State Progress")]
        [SerializeField] private bool isMastUnlocked = false;
        [SerializeField] private bool isBridgeLowered = true;
        [SerializeField] private float wheelTurnProgress = 0f;
        [SerializeField] private bool isWheelCompleted = false;
        [SerializeField] private bool hasKey = false;
        [SerializeField] private bool isChestOpened = false;
        [SerializeField] private bool isGangplankLowered = false;

        /// <summary>Cha đã xoay bánh lái đủ 360° (mở khoá trục nắp rương).</summary>
        public bool IsWheelCompleted => isWheelCompleted;
        /// <summary>Rương kho báu thuyền trưởng đã mở → đã có Chìa khóa vàng.</summary>
        public bool IsChestOpened => isChestOpened;
        /// <summary>Cầu nối 2 sàn tàu đã hạ phẳng (Con đã tháo dây neo ở CrowsNest).</summary>
        public bool IsBridgeLowered => isBridgeLowered;
        /// <summary>Tiến độ xoay bánh lái 0..1.</summary>
        public float WheelTurnProgress => wheelTurnProgress;
        /// <summary>Cầu tàu (gangplank) đã hạ xuống bến cảng chưa.</summary>
        public bool IsGangplankLowered => isGangplankLowered;
        /// <summary>Đã nhận chìa khóa vàng của thuyền trưởng.</summary>
        public bool HasCaptainKey => hasKey;

        #endregion

        #region Private Fields

        private Camera mainCamera;
        private string currentPrompt = "";
        private float promptTimer = 0f;
        private Color promptColor = Color.yellow;

        private CharacterController[] cachedControllers;
        private float lastUpdateTime = 0f;

        private Vector3 ladderBottomPos = new Vector3(42.05f, 5.40f, -54.50f);
        private Vector3 wheelPos = new Vector3(50.12f, 8.25f, -55.20f);
        private Vector3 chestPos = new Vector3(50.40f, 7.10f, -53.30f);
        private bool isClimbing = false;
        private Vector3 hiddenStaircaseRaisedPos;
        private bool hasRecordedStairsPos = false;
        private Quaternion m_WheelInitialRot;
        private bool m_WheelInitRecorded;
        private float m_WheelTurnedTotal;
        private ParticleSystem m_GoldSparkles;
        private Light m_GoldLight;
        [Header("Co-op Tuning (Stage 3 Redesign)")]
        [Tooltip("Bán kính Cha phải đứng giữ chân thang để Con leo được")]
        [SerializeField] private float dadAnchorRadius = 4.0f;
        [Tooltip("Bánh lái tự trôi ngược khi Cha thả tay (độ/giây)")]
        [SerializeField] private float wheelSlipback = 25f;
        private float m_AmbientT;

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

            AutoSetupShipwreckProps();
        }

        private void Start()
        {
            mainCamera = Camera.main;
            CachePlayers();

            // Cầu nối phẳng giữa 2 sàn tàu
            if (deckBridge != null)
            {
                deckBridge.position = new Vector3(43.70f, 5.85f, -55.00f);
                deckBridge.rotation = isBridgeLowered ? Quaternion.Euler(0f, 0f, 0f) : Quaternion.Euler(0f, 0f, 65f);
                var plankCol = deckBridge.GetComponentInChildren<BoxCollider>();
                if (plankCol != null) plankCol.isTrigger = false;
            }

            if (hiddenStaircase != null)
            {
                hiddenStaircaseRaisedPos = hiddenStaircase.position;
                hasRecordedStairsPos = true;

                if (!isChestOpened)
                {
                    hiddenStaircase.position = hiddenStaircaseRaisedPos - Vector3.up * 4.2f;
                    var lights = hiddenStaircase.GetComponentsInChildren<Light>();
                    foreach (var l in lights) l.enabled = false;
                }
            }
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

            HandleShipwreckGameplay();
            TickAmbientLife();
            TickWheelSlipback();
        }

        #endregion

        #region Setup & Props Construction

        public void AutoSetupShipwreckProps()
        {
            var functionalWheel = GameObject.Find("Ship_SteeringWheel_Functional_Pivot");
            if (functionalWheel != null)
            {
                steeringWheel = functionalWheel.transform;
            }

            var tc = GameObject.Find("HMH_West_Chest");
            if (tc != null) treasureChest = tc.transform;

            var cl = GameObject.Find("HMH_West_ChestLid");
            if (cl != null) chestLid = cl.transform;

            var cg = GameObject.Find("HMH_West_ChestGold");
            if (cg != null) chestGold = cg.transform;

            var gp = GameObject.Find("Cove_Gangplank_Ship");
            if (gp != null) exitGangplank = gp.transform;

            var db = GameObject.Find("Shipwreck_Deck_Bridge");
            if (db != null) deckBridge = db.transform;

            var hs = GameObject.Find("Shipwreck_Hidden_Staircase");
            if (hs != null) hiddenStaircase = hs.transform;
        }

        private void EnsureDeckBridgeExists()
        {
            var bridgeGo = GameObject.Find("Shipwreck_Deck_Bridge");
            if (bridgeGo != null)
            {
                deckBridge = bridgeGo.transform;
                return;
            }

            // Tải vật liệu chất lượng cao từ blend
            Material[] allMaterials = Resources.FindObjectsOfTypeAll<Material>();
            Material matWood = null;
            Material matTimber = null;
            Material matIron = null;
            if (allMaterials != null)
            {
                foreach (var m in allMaterials)
                {
                    if (m == null) continue;
                    if (m.name == "Mat_WoodPlank") matWood = m;
                    else if (m.name == "Mat_DarkTimber") matTimber = m;
                    else if (m.name == "Mat_IronChain") matIron = m;
                }
            }

            bridgeGo = new GameObject("Shipwreck_Deck_Bridge");
            // Điểm bản lề trên sàn trước (Deck Fore)
            bridgeGo.transform.position = new Vector3(43.80f, 5.75f, -54.20f);
            // Ban đầu dựng đứng nghiêng 65 độ (chưa hạ)
            bridgeGo.transform.rotation = Quaternion.Euler(0f, 15f, -65f);
            deckBridge = bridgeGo.transform;

            // Mặt cầu gỗ dài 4.2m, rộng 1.8m
            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Bridge_Plank_Surface";
            plank.transform.SetParent(deckBridge, false);
            plank.transform.localPosition = new Vector3(2.10f, 0.08f, 0f);
            plank.transform.localScale = new Vector3(4.20f, 0.16f, 1.80f);
            if (matWood != null) plank.GetComponent<MeshRenderer>().sharedMaterial = matWood;

            // Hai dầm đỡ gỗ bên dưới
            for (int s = -1; s <= 1; s += 2)
            {
                var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.name = "Bridge_Side_Beam_" + (s < 0 ? "L" : "R");
                beam.transform.SetParent(deckBridge, false);
                beam.transform.localPosition = new Vector3(2.10f, -0.10f, s * 0.85f);
                beam.transform.localScale = new Vector3(4.20f, 0.22f, 0.16f);
                if (matTimber != null) beam.GetComponent<MeshRenderer>().sharedMaterial = matTimber;
                var bc = beam.GetComponent<Collider>(); if (bc != null) DestroyImmediate(bc);
            }

            // Xích treo cầu hai bên
            for (int s = -1; s <= 1; s += 2)
            {
                var chain = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chain.name = "Bridge_Hoist_Chain_" + (s < 0 ? "L" : "R");
                chain.transform.SetParent(deckBridge, false);
                chain.transform.localPosition = new Vector3(3.80f, 0.65f, s * 0.85f);
                chain.transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
                chain.transform.localScale = new Vector3(0.04f, 0.70f, 0.04f);
                if (matIron != null) chain.GetComponent<MeshRenderer>().sharedMaterial = matIron;
                var cc = chain.GetComponent<Collider>(); if (cc != null) DestroyImmediate(cc);
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

        #region Gameplay Interactions

        private void HandleShipwreckGameplay()
        {
            // -------------------------------------------------------------
            // GIAI ĐOẠN 1: CHA GIỮ THANG — CON LEO & HẠ CẦU (Co-op thật)
            // -------------------------------------------------------------
            if (!isBridgeLowered)
            {
                // Bước 1.1: Con bám thang dây leo lên đài quan sát (Crow's Nest)
                // ĐIỀU KIỆN MỚI: Cha phải đứng giữ chân thang trong dadAnchorRadius
                PlayerRoleVisuals visualsLadder;
                var playerNearLadder = GetNearestPlayer(ladderBottomPos, 2.8f, out visualsLadder);
                bool dadAnchoring = IsDadNear(ladderBottomPos, dadAnchorRadius);
                if (playerNearLadder != null && !isClimbing)
                {
                    bool isDad = visualsLadder != null ? visualsLadder.IsDad : playerNearLadder.name.ToLower().Contains("dad");
                    if (!isDad)
                    {
                        if (!dadAnchoring)
                        {
                            ShowPrompt("👶 Gọi 🧔 Cha ra giữ chân thang mới leo được! (Cha đứng gần thang)", Color.yellow);
                        }
                        else
                        {
                            ShowPrompt("👶 [E] Bám Thang Leo Lên Đài Quan Sát (Cha đang giữ thang) | 🧔 Giữ nguyên vị trí!", Color.cyan);
                            if (IsInteractPressed())
                            {
                                StartCoroutine(ClimbLadderRoutine(playerNearLadder, true));
                            }
                        }
                    }
                    else
                    {
                        ShowPrompt("🧔 Cha đứng giữ chân thang cho Con leo — Con lại gần thang nhấn [E]!", Color.green);
                    }
                }
                else if (!isClimbing && !dadAnchoring)
                {
                    // Gợi ý khi chưa ai ở thang
                    PlayerRoleVisuals tmpV;
                    var anyNear = GetNearestPlayer(ladderBottomPos, 8f, out tmpV);
                    if (anyNear != null) ShowPrompt("🧔 Cha ra chân thang giữ + 👶 Con trèo lên Đài Quan Sát!", Color.yellow);
                }

                // Bước 1.2: Con trên đài quan sát tháo chốt dây neo hạ cầu
                PlayerRoleVisuals visualsNest;
                var playerInNest = GetNearestPlayer(crowsNestInteractPos, 3.2f, out visualsNest);
                if (playerInNest != null && !isClimbing)
                {
                    bool isDad = visualsNest != null ? visualsNest.IsDad : playerInNest.name.ToLower().Contains("dad");
                    if (!isDad)
                    {
                        ShowPrompt("👶 [E] Tháo Dây Buộc Thả Cầu Treo | [F] Tuột Thang Xuống Boong", Color.cyan);
                        if (IsInteractPressed())
                        {
                            StartCoroutine(LowerDeckBridgeRoutine());
                        }
                        else if (IsFPressed())
                        {
                            StartCoroutine(ClimbLadderRoutine(playerInNest, false));
                        }
                    }
                }
                return;
            }

            // Khi cầu đã hạ, Con ở trên đài quan sát vẫn có thể tuột thang xuống boong tàu
            PlayerRoleVisuals visualsNestDescent;
            var playerInNestDescent = GetNearestPlayer(crowsNestInteractPos, 3.2f, out visualsNestDescent);
            if (playerInNestDescent != null && !isClimbing)
            {
                bool isDad = visualsNestDescent != null ? visualsNestDescent.IsDad : playerInNestDescent.name.ToLower().Contains("dad");
                if (!isDad)
                {
                    ShowPrompt("👶 [E / F] Bám Thang Tuột Xuống Boong Tàu", Color.cyan);
                    if (IsInteractPressed() || IsFPressed())
                    {
                        StartCoroutine(ClimbLadderRoutine(playerInNestDescent, false));
                        return;
                    }
                }
            }

            // -------------------------------------------------------------
            // GIAI ĐOẠN 2: BÁNH LÁI TÀU & MỞ RƯƠNG THUYỀN TRƯỞNG
            // -------------------------------------------------------------
            if (!isChestOpened)
            {
                // Ba dùng sức quay Bánh Lái Tàu Thuyền Trưởng
                if (!isWheelCompleted)
                {
                    PlayerRoleVisuals visuals;
                    var playerNearWheel = GetNearestPlayer(wheelPos, 3.5f, out visuals);
                    if (playerNearWheel != null)
                    {
                        bool isDad = visuals != null ? visuals.IsDad : playerNearWheel.name.ToLower().Contains("dad");
                        if (isDad)
                        {
                            ShowPrompt($"🧔 [Giữ SPACE / E] Dùng sức xoay Bánh Lái Thuyền Trưởng ({Mathf.RoundToInt(wheelTurnProgress * 100)}%)", Color.green);
                            if (IsInteractHeld())
                            {
                                PerformWheelTurning();
                            }
                        }
                        else
                        {
                            ShowPrompt("🚫 Bánh lái thuyền trưởng bị ngâm nước kẹt cứng, cần 🧔 Ba dùng sức xoay!", Color.red);
                        }
                    }
                }
                return;
            }
        }

        private IEnumerator LowerDeckBridgeRoutine()
        {
            UISoundManager.PlayConfirm();
            ShowPrompt("⚙️ DÂY NEO ĐÃ NHẢ! CẦU TREO ĐANG HẠ XUỐNG NỐI HAI SÀN TÀU...", Color.yellow, 3.0f);

            if (deckBridge != null)
            {
                Vector3 startPos = deckBridge.position;
                // Điểm hạ phẳng: Y=5.85 khít giữa Fore (mép 44.10) và Aft (mép 44.67)
                Vector3 endPos = new Vector3(43.70f, 5.85f, -55.00f);
                Quaternion startRot = deckBridge.rotation;
                // Hạ phẳng 0° để đi qua được (giữ yaw gốc)
                Quaternion endRot = Quaternion.Euler(0f, 0f, 0f);
                float dur = 1.8f;
                float elapsed = 0f;

                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                    deckBridge.position = Vector3.Lerp(startPos, endPos, t);
                    deckBridge.rotation = Quaternion.Slerp(startRot, endRot, t);
                    yield return null;
                }
                deckBridge.position = endPos;
                deckBridge.rotation = endRot;
            }

            isBridgeLowered = true;
            UISoundManager.PlaySuccess();
            ShowPrompt("⚓ CẦU TREO ĐÃ HẠ HOÀN TẤT! Hai Cha Con hãy bước qua sàn sau để khám phá!", Color.green, 5.0f);
            Debug.Log("[ShipwreckCoop] Cầu gỗ nối hai sàn tàu đã hạ thành công!");
        }

        private void PerformWheelTurning()
        {
            if (isWheelCompleted) return;

            // Xoay bánh lái đủ 360° quanh trục tâm — giữ rotation gốc FBX (initialRot * offset)
            if (steeringWheel != null)
            {
                if (!m_WheelInitRecorded) { m_WheelInitialRot = steeringWheel.localRotation; m_WheelInitRecorded = true; }
                float step = 130f * Time.deltaTime;
                steeringWheel.localRotation = steeringWheel.localRotation * Quaternion.Euler(0f, 0f, step);
                m_WheelTurnedTotal += step;
            }

            wheelTurnProgress = Mathf.Clamp01(m_WheelTurnedTotal / 360f);

            if (m_WheelTurnedTotal >= 360f && wheelTurnProgress >= 1f)
            {
                wheelTurnProgress = 1f;
                isWheelCompleted = true;
                var keyLight = GameObject.Find("Ship_WOW_KeyLight")?.GetComponent<Light>();
                if (keyLight != null) StartCoroutine(PulseLight(keyLight, 0.5f));

                UISoundManager.PlaySuccess();
                ShowPrompt("✨ TRỤC BÁNH LÁI ĐÃ MỞ TOANG NẮP RƯƠNG KHO BÁU!", Color.green, 5.0f);
                Debug.Log("[ShipwreckCoop] Bánh lái thuyền trưởng đã xoay đủ vòng, kích hoạt mở rương kho báu!");

                StartCoroutine(OpenCaptainChestRoutine());
                StartCoroutine(RaiseHiddenStaircaseRoutine());
            }
        }

        private IEnumerator RaiseHiddenStaircaseRoutine()
        {
            if (hiddenStaircase == null) yield break;

            Vector3 endPos = hasRecordedStairsPos ? hiddenStaircaseRaisedPos : hiddenStaircase.position;
            Vector3 startPos = endPos - Vector3.up * 4.2f;
            hiddenStaircase.position = startPos;

            var lights = hiddenStaircase.GetComponentsInChildren<Light>();
            foreach (var l in lights) l.enabled = false;

            UISoundManager.PlayConfirm();
            ShowPrompt("⚙️ CƠ QUAN BÍ MẬT RUNG CHUYỂN... CẦU THANG ĐÁ ĐANG TRỒI LÊN TỪ VÁCH NÚI!", Color.yellow, 3.5f);

            float dur = 3.2f;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                float rumble = (1f - t) * Mathf.Sin(elapsed * 28f) * 0.04f;
                hiddenStaircase.position = Vector3.Lerp(startPos, endPos, t) + new Vector3(rumble, 0f, rumble);
                yield return null;
            }

            hiddenStaircase.position = endPos;
            foreach (var l in lights)
            {
                l.enabled = true;
                l.intensity = 2.8f;
            }

            UISoundManager.PlaySuccess();
            ShowPrompt("✨ CẦU THANG ĐÁ ẨN ĐÃ TRỒI LÊN HOÀN TẤT! Hai Cha Con hãy leo lên vách núi để tiến sang KHU MỎ!", Color.green, 6.0f);
            Debug.Log("[ShipwreckCoop] Cầu thang đá ẩn đã trồi lên hoàn tất!");
        }

        private IEnumerator ClimbLadderRoutine(CharacterController player, bool climbUp)
        {
            if (player == null || isClimbing) yield break;
            isClimbing = true;

            var coreMove = player.GetComponent<CoreMovement>();
            if (coreMove != null) coreMove.enabled = false;
            player.enabled = false;

            Vector3 startPos = climbUp ? ladderBottomPos : crowsNestInteractPos;
            Vector3 endPos = climbUp ? crowsNestInteractPos : ladderBottomPos;
            float dur = 2.2f;
            float elapsed = 0f;

            UISoundManager.PlayClick();

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / dur;
                // Hiệu ứng lắc lư nhẹ tự nhiên khi bước từng bậc thang
                float sway = Mathf.Sin(t * 14f) * 0.04f;
                Vector3 currentPos = Vector3.Lerp(startPos, endPos, t) + new Vector3(0f, 0f, sway);
                player.transform.position = currentPos;
                yield return null;
            }

            player.transform.position = endPos;
            player.enabled = true;
            if (coreMove != null) coreMove.enabled = true;

            isClimbing = false;
            UISoundManager.PlayConfirm();
        }

        private IEnumerator PulseLight(Light target, float duration)
        {
            float originalIntensity = target.intensity;
            Color originalColor = target.color;
            target.color = new Color(1f, 0.8f, 0.2f);
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.deltaTime;
                target.intensity = originalIntensity + 2f * Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
                yield return null;
            }
            if (target != null) { target.intensity = originalIntensity; target.color = originalColor; }
        }

        private IEnumerator OpenCaptainChestRoutine()
        {
            UISoundManager.PlayConfirm();

            // Mở nắp rương
            if (chestLid != null)
            {
                Quaternion startRot = chestLid.localRotation;
                Quaternion endRot = startRot * Quaternion.Euler(75f, 0f, 0f);
                float dur = 1.2f;
                float elapsed = 0f;

                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                    chestLid.localRotation = Quaternion.Slerp(startRot, endRot, t);
                    yield return null;
                }
            }

            if (chestGold != null)
            {
                chestGold.gameObject.SetActive(true);
                SpawnGoldSparkles(chestGold);
            }

            isChestOpened = true;
            isGangplankLowered = true;
            hasKey = true;
            UISoundManager.PlaySuccess();
            ShowPrompt("🗝️ ĐÃ MỞ RƯƠNG THUYỀN TRƯỞNG & HẠ CẦU TÀU! Hai Cha Con hãy xuống bến cảng đến ĐÀI HẢI ĐĂNG CỔ ĐẠI để sang Màn 1!", Color.green, 8.0f);
            Debug.Log("[ShipwreckCoop] Rương thuyền trưởng đã mở, hạ cầu tàu dẫn lối sang Đài Hải Đăng Cổ Đại!");
        }

        /// <summary>
        /// Bụi vàng phát sáng tại rương (Pha 2) — tạo procedural, tái dùng nếu có sẵn.
        /// </summary>
        private void SpawnGoldSparkles(Transform goldAnchor)
        {
            if (m_GoldSparkles != null) { m_GoldSparkles.Play(); return; }
            var fx = new GameObject("HMH_West_ChestGold_Sparkles");
            fx.transform.SetParent(goldAnchor, false);
            fx.transform.localPosition = Vector3.up * 0.4f;
            var ps = fx.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 1.2f;
            main.startSpeed = 1.2f;
            main.startSize = 0.08f;
            main.maxParticles = 60;
            main.startColor = new Color(1f, 0.85f, 0.2f, 1f);
            var emission = ps.emission;
            emission.rateOverTime = 24f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            var goldMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            goldMat.color = new Color(1f, 0.8f, 0.1f, 0.9f);
            rend.material = goldMat;
            ps.Play();
            m_GoldSparkles = ps;
            // Đèn vàng ấm soi rương
            if (m_GoldLight == null)
            {
                var lightGo = new GameObject("HMH_West_ChestGold_Light");
                lightGo.transform.SetParent(goldAnchor, false);
                lightGo.transform.localPosition = Vector3.up * 1.0f;
                m_GoldLight = lightGo.AddComponent<Light>();
                m_GoldLight.type = LightType.Point;
                m_GoldLight.color = new Color(1f, 0.8f, 0.35f);
                m_GoldLight.intensity = 2.5f;
                m_GoldLight.range = 6f;
            }
            else m_GoldLight.enabled = true;
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

        /// <summary>Cha có đang đứng giữ vị trí quanh điểm neo không (Pha 1: giữ chân thang).</summary>
        private bool IsDadNear(Vector3 targetPos, float maxDist)
        {
            if (cachedControllers == null) return false;
            float maxSq = maxDist * maxDist;
            for (int i = 0; i < cachedControllers.Length; i++)
            {
                var cc = cachedControllers[i];
                if (cc == null || !cc.gameObject.activeInHierarchy) continue;
                bool isDad;
                var vis = cc.GetComponentInParent<PlayerRoleVisuals>();
                if (vis != null) isDad = vis.IsDad;
                else if (cc.GetComponent<CoopBoostThrowAbility>() != null) isDad = cc.GetComponent<CoopBoostThrowAbility>().IsDad;
                else isDad = cc.gameObject.name.ToLower().Contains("dad");
                if (!isDad) continue;
                if ((cc.transform.position - targetPos).sqrMagnitude <= maxSq) return true;
            }
            return false;
        }

        /// <summary>Bánh lái tự trôi ngược khi Cha thả tay (thử thách giữ nhịp).</summary>
        private void TickWheelSlipback()
        {
            if (isWheelCompleted || isChestOpened) return;
            if (m_WheelTurnedTotal <= 0f) return;
            if (IsInteractHeld()) return; // đang giữ thì không trôi
            // Chỉ trôi khi đã có người từng quay (progress > 0) và cầu đã hạ
            if (!isBridgeLowered) return;
            float slip = wheelSlipback * Time.deltaTime;
            m_WheelTurnedTotal = Mathf.Max(0f, m_WheelTurnedTotal - slip);
            wheelTurnProgress = Mathf.Clamp01(m_WheelTurnedTotal / 360f);
            if (steeringWheel != null && slip > 0f)
                steeringWheel.localRotation = steeringWheel.localRotation * Quaternion.Euler(0f, 0f, -slip);
        }

        /// <summary>Sự sống nền: cờ tung bay + đèn nhấp nháy + bọt nước dập dềnh.</summary>
        private void TickAmbientLife()
        {
            m_AmbientT += Time.deltaTime;
            // Cờ CrowNest tung bay
            var flag = GameObject.Find("Ship_Decor_Flag_CrowsNest");
            if (flag != null)
                flag.transform.rotation = Quaternion.Euler(0f, 90f, Mathf.Sin(m_AmbientT * 3f) * 12f);
            // Đèn lồng nhấp nháy ấm
            for (int i = 0; i < 2; i++)
            {
                string ln = i == 0 ? "Ship_Decor_Lantern_Fore_Light" : "Ship_Decor_Lantern_Aft_Light";
                var lgo = GameObject.Find(ln);
                if (lgo != null)
                {
                    var l = lgo.GetComponent<Light>();
                    if (l != null) l.intensity = 3f + Mathf.Sin(m_AmbientT * 5f + i * 2f) * 0.5f;
                }
            }
            // Bọt nước dập dềnh quanh thân tàu
            for (int i = 0; i < 6; i++)
            {
                var foam = GameObject.Find("Ship_Decor_Foam_" + i);
                if (foam == null) continue;
                Vector3 p = foam.transform.position;
                p.y = 1.66f + Mathf.Sin(m_AmbientT * 1.5f + i * 1.1f) * 0.06f;
                foam.transform.position = p;
            }
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

        private bool IsFPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && Gamepad.current.buttonWest.wasPressedThisFrame) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.F);
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
            DrawObjectivePanel();

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

        /// <summary>
        /// Bảng mục tiêu 3 pha (góc trái trên) — giúp người chơi luôn biết đang làm gì.
        /// </summary>
        private bool IsAnyPlayerInShipwreckZone()
        {
            if (cachedControllers == null || cachedControllers.Length == 0) return true;
            for (int i = 0; i < cachedControllers.Length; i++)
            {
                var cc = cachedControllers[i];
                if (cc != null && cc.gameObject.activeInHierarchy && cc.transform.position.x >= 25f)
                    return true;
            }
            return false;
        }

        private void DrawObjectivePanel()
        {
            if (!IsAnyPlayerInShipwreckZone()) return;
            GUI.depth = -100;
            var titleStyle = new GUIStyle(GUI.skin.box);
            titleStyle.fontSize = 16;
            titleStyle.fontStyle = FontStyle.Bold;
            titleStyle.alignment = TextAnchor.MiddleLeft;
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);

            var itemStyle = new GUIStyle(GUI.skin.label);
            itemStyle.fontSize = 15;
            itemStyle.alignment = TextAnchor.MiddleLeft;

            float w = 330f, h = 108f;
            float x = 18f, y = 18f;
            GUI.Box(new Rect(x, y, w, h), "🎯 MỤC TIÊU KHU TÀU ĐẮM", titleStyle);

            bool phase1Done = isBridgeLowered;
            bool phase2Done = isWheelCompleted;
            bool phase3Done = isChestOpened;

            DrawObjectiveLine(itemStyle, x, y + 30f, phase1Done,
                "1. Con leo CrowsNest → hạ cầu nối 2 sàn");
            DrawObjectiveLine(itemStyle, x, y + 56f, phase2Done,
                "2. Cha xoay bánh lái 360° → mở rương");
            DrawObjectiveLine(itemStyle, x, y + 82f, phase3Done,
                "3. Mở rương kho báu → Tiến sang Cầu Cửa Vịnh");
        }

        private void DrawObjectiveLine(GUIStyle style, float x, float y, bool done, string text)
        {
            style.normal.textColor = done ? new Color(0.45f, 0.95f, 0.5f) : new Color(0.85f, 0.87f, 0.92f);
            GUI.Label(new Rect(x + 12f, y, 310f, 24f), (done ? "✔ " : "○ ") + text, style);
        }

        #endregion
    }
}
