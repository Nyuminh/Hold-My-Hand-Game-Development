using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Chuỗi Cơ Chế Nhiệm Vụ Co-op "Tàu Hoa Tiêu Sao Bắc Đẩu & Ngọn Hải Đăng Mặt Trời" (Stage 3 Shipwreck):
    /// 1. Cốt truyện: Con tàu hoa tiêu huyền thoại của Người Đi Trước bị mắc cạn trong Vịnh Cove.
    ///    Lối lên Tháp Canh bị phong ấn bởi sương mù và viên ngọc cổ trên vách đá.
    /// 2. Giai đoạn 1 (Sự tin tưởng & Đối trọng buồm):
    ///    - Ba đứng ghì bệ đối trọng chân buồm để kéo căng thang dây.
    ///    - Con trèo lên Đài Quan Sát (Crow's Nest), thu thập Thấu Kính Mặt Trời và gạt chốt thả cầu nối 2 sàn tàu.
    /// 3. Giai đoạn 2 (Bệ gương quang học & Tia sáng mặt trời):
    ///    - Con mang thấu kính xuống sàn sau lắp vào Bệ Gương Quang Học (Solar Pedestal).
    ///    - Ba dùng sức xoay Bánh Lái Tàu điều chỉnh góc chùm tia sáng mặt trời quét qua vịnh biển.
    ///    - Khi tia sáng chiếu thẳng vào Viên Ngọc Phong Ấn trên vách đá Tháp Canh: Phong ấn vỡ tung, tháp bừng sáng!
    /// 4. Giai đoạn 3 (Mở rương kho báu & Hạ cầu ván sang Tháp Canh):
    ///    - Rương Thuyền Trưởng bật mở, giải phóng Chìa Khóa Cổ Thành (Castle Sun Key).
    ///    - Cầu ván từ đuôi tàu hạ phẳng nối thẳng sang thềm đá Tháp Canh, dãy đuốc vách đá thắp sáng chỉ đường.
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

        [Tooltip("Cần gạt / bệ đối trọng chân buồm")]
        [SerializeField] private Transform mastBaseLever;

        [Tooltip("Vị trí tương tác trên đài quan sát (Crow's Nest)")]
        [SerializeField] private Vector3 crowsNestInteractPos = new Vector3(42.50f, 16.20f, -55.00f);

        [Header("Phase 2: Steering Wheel & Solar Beacon")]
        [Tooltip("Bánh lái tàu cổ (Ship_SteeringWheel)")]
        [SerializeField] private Transform steeringWheel;

        [Tooltip("Bệ đài gương quang học hội tụ ánh sáng trên sàn sau")]
        [SerializeField] private Transform solarPedestal;

        [Tooltip("Thấu kính pha lê mặt trời")]
        [SerializeField] private Transform solarLens;

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
        [SerializeField] private bool isBridgeLowered = false;
        [SerializeField] private bool hasSolarLens = false;
        [SerializeField] private bool isLensInstalled = false;
        [SerializeField] private float wheelTurnProgress = 0f;
        [SerializeField] private bool isWheelCompleted = false;
        [SerializeField] private bool isBeaconAligned = false;
        [SerializeField] private bool hasKey = false;
        [SerializeField] private bool isChestOpened = false;
        [SerializeField] private bool isGangplankLowered = false;

        /// <summary>Cầu nối 2 sàn tàu đã hạ phẳng.</summary>
        public bool IsBridgeLowered => isBridgeLowered;
        /// <summary>Con đã nhặt Thấu Kính Mặt Trời trên đài quan sát.</summary>
        public bool HasSolarLens => hasSolarLens;
        /// <summary>Thấu kính đã được lắp vào bệ gương trên sàn sau.</summary>
        public bool IsLensInstalled => isLensInstalled;
        /// <summary>Tiến độ xoay bánh lái 0..1.</summary>
        public float WheelTurnProgress => wheelTurnProgress;
        /// <summary>Cha đã xoay bánh lái đủ 100%.</summary>
        public bool IsWheelCompleted => isWheelCompleted;
        /// <summary>Tia sáng mặt trời đã kích hoạt phong ấn Tháp Canh.</summary>
        public bool IsBeaconAligned => isBeaconAligned;
        /// <summary>Rương kho báu thuyền trưởng đã mở → đã có Chìa khóa vàng.</summary>
        public bool IsChestOpened => isChestOpened;
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
        private Vector3 pedestalPos = new Vector3(49.84f, 7.82f, -55.80f);
        private Vector3 wheelPos = new Vector3(50.12f, 8.25f, -55.20f);
        private Vector3 chestPos = new Vector3(50.40f, 7.10f, -53.30f);
        private Vector3 cliffRunePos = new Vector3(25.60f, 10.50f, -73.40f);

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
        [SerializeField] private float dadAnchorRadius = 4.2f;
        [Tooltip("Bánh lái tự trôi ngược khi Cha thả tay (độ/giây)")]
        [SerializeField] private float wheelSlipback = 20f;
        private float m_AmbientT;

        // Visual Props Procedural
        private GameObject m_CounterweightPlateGo;
        private GameObject m_SolarLensPickupGo;
        private GameObject m_SolarPedestalGo;
        private LineRenderer m_SolarBeamLine;
        private Light m_SolarBeamLight;
        private GameObject m_CliffRuneGo;
        private Light m_CliffRuneLight;
        private List<GameObject> m_AscentTorches = new List<GameObject>();

        private bool m_HasPlayedIntroDialogue = false;

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

            // Khởi tạo cầu gỗ gập: ban đầu dựng nghiêng 65° nếu chưa giải xong
            if (deckBridge == null) EnsureDeckBridgeExists();
            if (deckBridge != null)
            {
                deckBridge.position = new Vector3(43.70f, 5.85f, -55.00f);
                deckBridge.rotation = isBridgeLowered ? Quaternion.Euler(0f, 0f, 0f) : Quaternion.Euler(0f, 15f, -65f);
                var plankCol = deckBridge.GetComponentInChildren<BoxCollider>();
                if (plankCol != null) plankCol.isTrigger = false;
            }

            // Dựng các đạo cụ trực quan phong phú
            BuildProceduralProps();

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
            UpdateSolarBeamVisuals();
        }

        #endregion

        #region Setup & Props Construction

        public void AutoSetupShipwreckProps()
        {
            var functionalWheel = GameObject.Find("Ship_SteeringWheel_Functional_Pivot");
            if (functionalWheel != null) steeringWheel = functionalWheel.transform;

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

            bridgeGo = new GameObject("Shipwreck_Deck_Bridge");
            bridgeGo.transform.position = new Vector3(43.70f, 5.85f, -55.00f);
            bridgeGo.transform.rotation = isBridgeLowered ? Quaternion.Euler(0f, 0f, 0f) : Quaternion.Euler(0f, 15f, -65f);
            deckBridge = bridgeGo.transform;

            var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plank.name = "Bridge_Plank_Surface";
            plank.transform.SetParent(deckBridge, false);
            plank.transform.localPosition = new Vector3(2.10f, 0.08f, 0f);
            plank.transform.localScale = new Vector3(4.20f, 0.16f, 1.80f);
        }

        private void BuildProceduralProps()
        {
            Material matGold = CreateGlowMaterial("Mat_Ship_GoldSun", new Color(1f, 0.82f, 0.2f), 2.2f);
            Material matIron = CreateStandardMaterial("Mat_Ship_DarkIron", new Color(0.2f, 0.22f, 0.25f), 0.7f);
            Material matWood = CreateStandardMaterial("Mat_Ship_SturdyTimber", new Color(0.42f, 0.28f, 0.16f), 0.1f);
            Material matCyan = CreateGlowMaterial("Mat_Ship_CyanCrystal", new Color(0.2f, 0.85f, 1f), 2.5f);

            // 1. Điểm đứng ghì đối trọng chân cột buồm (Mast Counterweight Stand)
            if (m_CounterweightPlateGo == null)
            {
                m_CounterweightPlateGo = new GameObject("Mast_Counterweight_Plate");
                m_CounterweightPlateGo.transform.position = ladderBottomPos;

                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "Center_Gold_Rune";
                ring.transform.SetParent(m_CounterweightPlateGo.transform, false);
                ring.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                ring.transform.localScale = new Vector3(1.2f, 0.02f, 1.2f);
                ring.GetComponent<MeshRenderer>().sharedMaterial = matGold;
                DestroyImmediate(ring.GetComponent<Collider>());
            }

            // 2. Thấu Kính Mặt Trời trên đài quan sát (Solar Lens Pickup)
            if (m_SolarLensPickupGo == null && !hasSolarLens)
            {
                m_SolarLensPickupGo = new GameObject("Ship_Solar_Lens_Pickup");
                m_SolarLensPickupGo.transform.position = crowsNestInteractPos + Vector3.up * 0.4f;

                var crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crystal.name = "Lens_Crystal";
                crystal.transform.SetParent(m_SolarLensPickupGo.transform, false);
                crystal.transform.localScale = new Vector3(0.55f, 0.85f, 0.55f);
                crystal.GetComponent<MeshRenderer>().sharedMaterial = matGold;
                DestroyImmediate(crystal.GetComponent<Collider>());

                var l = m_SolarLensPickupGo.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.85f, 0.3f);
                l.range = 6.0f;
                l.intensity = 3.0f;
            }

            // 3. Khớp gắn Thấu Kính Mặt Trời trên Bánh Lái Tàu (Ship Helm Binnacle)
            if (m_SolarPedestalGo == null)
            {
                var helmBinnacle = GameObject.Find("Helm_Binnacle");
                if (helmBinnacle != null)
                {
                    m_SolarPedestalGo = helmBinnacle;
                }
                else
                {
                    m_SolarPedestalGo = new GameObject("Ship_Solar_Pedestal");
                    m_SolarPedestalGo.transform.position = pedestalPos;
                }

                // Thấu kính khi lắp vào bệ bánh lái
                var socket = m_SolarPedestalGo.transform.Find("Mounted_Lens");
                GameObject socketGo = socket != null ? socket.gameObject : null;
                if (socketGo == null)
                {
                    socketGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    socketGo.name = "Mounted_Lens";
                    socketGo.transform.SetParent(m_SolarPedestalGo.transform, false);
                    socketGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
                    socketGo.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
                    socketGo.GetComponent<MeshRenderer>().sharedMaterial = matGold;
                    DestroyImmediate(socketGo.GetComponent<Collider>());
                }
                socketGo.SetActive(isLensInstalled);

                // LineRenderer cho Tia Sáng Mặt Trời
                m_SolarBeamLine = m_SolarPedestalGo.GetComponent<LineRenderer>();
                if (m_SolarBeamLine == null) m_SolarBeamLine = m_SolarPedestalGo.AddComponent<LineRenderer>();
                m_SolarBeamLine.positionCount = 2;
                m_SolarBeamLine.startWidth = 0.22f;
                m_SolarBeamLine.endWidth = 0.45f;
                m_SolarBeamLine.material = matGold;
                m_SolarBeamLine.startColor = new Color(1f, 0.9f, 0.4f, 0.95f);
                m_SolarBeamLine.endColor = new Color(1f, 0.75f, 0.1f, 0.6f);
                m_SolarBeamLine.enabled = isLensInstalled;

                m_SolarBeamLight = m_SolarPedestalGo.GetComponent<Light>();
                if (m_SolarBeamLight == null) m_SolarBeamLight = m_SolarPedestalGo.AddComponent<Light>();
                m_SolarBeamLight.type = LightType.Spot;
                m_SolarBeamLight.color = new Color(1f, 0.88f, 0.4f);
                m_SolarBeamLight.range = 45f;
                m_SolarBeamLight.spotAngle = 24f;
                m_SolarBeamLight.intensity = isLensInstalled ? 5.0f : 0f;
                m_SolarBeamLight.enabled = isLensInstalled;
            }

            // 4. Viên Ngọc Mặt Trời Phong Ấn Trên Vách Núi Tháp Canh (Cliff Rune)
            if (m_CliffRuneGo == null)
            {
                m_CliffRuneGo = new GameObject("Watchtower_Cliff_Sun_Rune");
                m_CliffRuneGo.transform.position = cliffRunePos;

                var runeDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                runeDisc.name = "Rune_Disc";
                runeDisc.transform.SetParent(m_CliffRuneGo.transform, false);
                runeDisc.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                runeDisc.transform.localScale = new Vector3(2.4f, 0.25f, 2.4f);
                runeDisc.GetComponent<MeshRenderer>().sharedMaterial = matIron;

                var runeCore = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                runeCore.name = "Rune_Core_Gem";
                runeCore.transform.SetParent(m_CliffRuneGo.transform, false);
                runeCore.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                runeCore.GetComponent<MeshRenderer>().sharedMaterial = isBeaconAligned ? matGold : matCyan;

                m_CliffRuneLight = m_CliffRuneGo.AddComponent<Light>();
                m_CliffRuneLight.type = LightType.Point;
                m_CliffRuneLight.color = isBeaconAligned ? new Color(1f, 0.85f, 0.3f) : new Color(0.2f, 0.85f, 1f);
                m_CliffRuneLight.range = isBeaconAligned ? 18f : 8f;
                m_CliffRuneLight.intensity = isBeaconAligned ? 5.5f : 1.5f;
            }

            // 5. Cầu ván gỗ nối mạn tàu sang thềm đá Tháp Canh
            EnsureExitGangplankSetup(matWood, matIron);
        }

        private void EnsureExitGangplankSetup(Material matWood, Material matIron)
        {
            var gp = GameObject.Find("Cove_Gangplank_Ship");
            if (gp == null)
            {
                gp = new GameObject("Cove_Gangplank_Ship");
            }

            // Đặt cầu ván bắc từ mép boong sau tàu (46.2f, 5.8f, -56.5f) vươn sang gờ đá bờ (38.5f, 3.8f, -63.5f)
            gp.transform.position = new Vector3(45.50f, 5.80f, -56.80f);
            gp.transform.rotation = isGangplankLowered ? Quaternion.Euler(14f, 40f, 0f) : Quaternion.Euler(75f, 40f, 0f);

            if (gp.transform.childCount == 0)
            {
                var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "Gangplank_Plank_Surface";
                plank.transform.SetParent(gp.transform, false);
                plank.transform.localPosition = new Vector3(0f, -0.05f, 4.5f);
                plank.transform.localScale = new Vector3(2.2f, 0.22f, 9.5f);
                if (matWood != null) plank.GetComponent<MeshRenderer>().sharedMaterial = matWood;

                for (int s = -1; s <= 1; s += 2)
                {
                    var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    beam.name = "Gangplank_Rail_" + (s < 0 ? "L" : "R");
                    beam.transform.SetParent(gp.transform, false);
                    beam.transform.localPosition = new Vector3(s * 1.05f, 0.45f, 4.5f);
                    beam.transform.localScale = new Vector3(0.14f, 0.14f, 9.5f);
                    if (matIron != null) beam.GetComponent<MeshRenderer>().sharedMaterial = matIron;
                    DestroyImmediate(beam.GetComponent<Collider>());
                }
            }
            exitGangplank = gp.transform;
        }

        private Material CreateStandardMaterial(string name, Color col, float roughness)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = name;
            mat.color = col;
            mat.SetFloat("_Smoothness", 1f - roughness);
            return mat;
        }

        private Material CreateGlowMaterial(string name, Color emissiveCol, float boost)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = name;
            mat.color = emissiveCol;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emissiveCol * boost);
            return mat;
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
            // Lời thoại mở đầu khi hai cha con bước vào khu vực tàu đắm
            if (!m_HasPlayedIntroDialogue && IsAnyPlayerInShipwreckZone())
            {
                m_HasPlayedIntroDialogue = true;
                ShowPrompt("👨 Ba: 'Con nhìn kìa! Tàu Hoa Tiêu Sao Bắc Đẩu năm xưa! Lối lên tháp canh bị phong ấn rồi, ta phải kích hoạt Ngọn Hải Đăng Mặt Trời!'", Color.cyan, 6.0f);
            }

            // -------------------------------------------------------------
            // GIAI ĐOẠN 1: BA ĐÈ BỆ ĐỐI TRỌNG — CON LEO LẤY THẤU KÍNH & HẠ CẦU
            // -------------------------------------------------------------
            if (!isBridgeLowered)
            {
                PlayerRoleVisuals visualsLadder;
                var playerNearLadder = GetNearestPlayer(ladderBottomPos, 3.2f, out visualsLadder);
                bool dadAnchoring = IsDadNear(ladderBottomPos, dadAnchorRadius);

                // Hiệu ứng bệ đối trọng lún xuống khi Ba đứng lên
                if (m_CounterweightPlateGo != null)
                {
                    Vector3 targetPlatePos = ladderBottomPos + (dadAnchoring ? Vector3.down * 0.08f : Vector3.zero);
                    m_CounterweightPlateGo.transform.position = Vector3.Lerp(m_CounterweightPlateGo.transform.position, targetPlatePos, Time.deltaTime * 6f);
                }

                if (playerNearLadder != null && !isClimbing)
                {
                    bool isDad = visualsLadder != null ? visualsLadder.IsDad : playerNearLadder.name.ToLower().Contains("dad");
                    if (!isDad)
                    {
                        if (!dadAnchoring)
                        {
                            ShowPrompt("👶 Gọi 👨 Ba ra đứng đè bệ đối trọng chân buồm mới leo được!", Color.yellow);
                        }
                        else
                        {
                            ShowPrompt("👶 [E] Bám Thang Dây Leo Lên Đài Quan Sát (Ba đang ghì chặt chân buồm!)", Color.cyan);
                            if (IsInteractPressed())
                            {
                                StartCoroutine(ClimbLadderRoutine(playerNearLadder, true));
                            }
                        }
                    }
                    else
                    {
                        ShowPrompt("👨 Ba đứng đè bệ đối trọng giữ chân thang — Con mau lại gần thang nhấn [E] leo lên!", Color.green);
                    }
                }
                else if (!isClimbing && !dadAnchoring)
                {
                    PlayerRoleVisuals tmpV;
                    var anyNear = GetNearestPlayer(ladderBottomPos, 9f, out tmpV);
                    if (anyNear != null)
                        ShowPrompt("👨 Ba ra đè bệ đối trọng chân buồm + 👶 Con leo lên Đài Quan Sát!", Color.yellow);
                }

                // Con trên đài quan sát (Crow's Nest): Nhặt Thấu Kính Mặt Trời và hạ cầu
                PlayerRoleVisuals visualsNest;
                var playerInNest = GetNearestPlayer(crowsNestInteractPos, 3.5f, out visualsNest);
                if (playerInNest != null && !isClimbing)
                {
                    bool isDad = visualsNest != null ? visualsNest.IsDad : playerInNest.name.ToLower().Contains("dad");
                    if (!isDad)
                    {
                        ShowPrompt("👶 [E] Nhặt Thấu Kính Mặt Trời & Tháo Dây Neo Hạ Cầu | [F] Tuột Thang Xuống", Color.cyan);
                        if (IsInteractPressed())
                        {
                            CollectSolarLensAndLowerBridge();
                        }
                        else if (IsFPressed())
                        {
                            StartCoroutine(ClimbLadderRoutine(playerInNest, false));
                        }
                    }
                }
                return;
            }

            // Khi cầu đã hạ, Con ở trên đài quan sát vẫn có thể tuột xuống boong
            PlayerRoleVisuals visualsNestDescent;
            var playerInNestDescent = GetNearestPlayer(crowsNestInteractPos, 3.5f, out visualsNestDescent);
            if (playerInNestDescent != null && !isClimbing)
            {
                bool isDad = visualsNestDescent != null ? visualsNestDescent.IsDad : playerInNestDescent.name.ToLower().Contains("dad");
                if (!isDad)
                {
                    ShowPrompt("👶 [E / F] Bám Thang Dây Tuột Xuống Boong Tàu Gặp Ba", Color.cyan);
                    if (IsInteractPressed() || IsFPressed())
                    {
                        StartCoroutine(ClimbLadderRoutine(playerInNestDescent, false));
                        return;
                    }
                }
            }

            // -------------------------------------------------------------
            // GIAI ĐOẠN 2: LẮP THẤU KÍNH & XOAY BÁNH LÁI QUANG HỌC
            // -------------------------------------------------------------
            if (!isLensInstalled)
            {
                // Con đến gần Bệ Đài Gương để lắp Thấu Kính Mặt Trời
                PlayerRoleVisuals vPedestal;
                var pNearPedestal = GetNearestPlayer(pedestalPos, 3.2f, out vPedestal);
                if (pNearPedestal != null)
                {
                    bool isDad = vPedestal != null ? vPedestal.IsDad : pNearPedestal.name.ToLower().Contains("dad");
                    if (!isDad)
                    {
                        if (hasSolarLens)
                        {
                            ShowPrompt("👶 [E] Lắp Thấu Kính Pha Lê Mặt Trời vào Bệ Gương Quang Học!", Color.yellow);
                            if (IsInteractPressed())
                            {
                                InstallSolarLens();
                            }
                        }
                        else
                        {
                            ShowPrompt("⚠️ Cần Thấu Kính Mặt Trời từ đỉnh Cột Buồm để lắp vào bệ gương!", Color.red);
                        }
                    }
                    else
                    {
                        ShowPrompt("👨 Nhường 👦 Con đem Thấu Kính Mặt Trời lại lắp vào bệ gương!", Color.yellow);
                    }
                }
                else
                {
                    // Hướng dẫn nếu chưa lắp
                    PlayerRoleVisuals vAft;
                    var anyAft = GetNearestPlayer(wheelPos, 8f, out vAft);
                    if (anyAft != null)
                        ShowPrompt("👦 Con đem Thấu Kính Mặt Trời lại bệ gương quang học [E] để kích hoạt tia sáng!", Color.yellow);
                }
                return;
            }

            // Sau khi thấu kính đã lắp: Ba xoay Bánh Lái Tàu điều chỉnh Tia Sáng Mặt Trời
            if (!isBeaconAligned)
            {
                PlayerRoleVisuals visuals;
                var playerNearWheel = GetNearestPlayer(wheelPos, 3.5f, out visuals);
                if (playerNearWheel != null)
                {
                    bool isDad = visuals != null ? visuals.IsDad : playerNearWheel.name.ToLower().Contains("dad");
                    if (isDad)
                    {
                        ShowPrompt($"👨 [Giữ SPACE / E] Xoay Bánh Lái Tàu điều chỉnh Tia Sáng Mặt Trời ({Mathf.RoundToInt(wheelTurnProgress * 100)}%)", Color.green);
                        if (IsInteractHeld())
                        {
                            PerformWheelTurning();
                        }
                    }
                    else
                    {
                        ShowPrompt("👦 Ba ơi! Bánh lái rất nặng, ba mau dùng sức xoay bánh lái hướng tia sáng vào vách núi đi ba!", Color.yellow);
                    }
                }
                else
                {
                    PlayerRoleVisuals vPrompt;
                    var anyPlayer = GetNearestPlayer(wheelPos, 10f, out vPrompt);
                    if (anyPlayer != null)
                        ShowPrompt("👨 Ba mau lên bục thuyền trưởng xoay Bánh Lái Tàu hướng tia sáng vào Viên Ngọc Phong Ấn!", Color.green);
                }
                return;
            }

            // -------------------------------------------------------------
            // GIAI ĐOẠN 3: NHẶT CHÌA KHÓA & TIẾN LÊN THÁP CANH
            // -------------------------------------------------------------
            if (isChestOpened && !hasKey)
            {
                PlayerRoleVisuals vChest;
                var pNearChest = GetNearestPlayer(chestPos, 3.5f, out vChest);
                if (pNearChest != null)
                {
                    ShowPrompt("🔑 [E] Thu Thập Chìa Khóa Cổ Thành để mở Cổng Lâu Đài!", Color.yellow);
                    if (IsInteractPressed())
                    {
                        if (CastleKeyPickup.Instance != null)
                        {
                            CastleKeyPickup.Instance.CollectKey();
                        }
                        hasKey = true;
                        ShowPrompt("✨ ĐÃ NHẬN CHÌA KHÓA CỔ THÀNH! Cầu ván đã hạ, mau tiến lên Tháp Canh!", Color.green, 6.0f);
                    }
                }
            }
        }

        private void CollectSolarLensAndLowerBridge()
        {
            hasSolarLens = true;
            if (m_SolarLensPickupGo != null)
            {
                Destroy(m_SolarLensPickupGo);
                m_SolarLensPickupGo = null;
            }

            UISoundManager.PlayConfirm();
            ShowPrompt("✨ 👦 Con: 'Con lấy được Thấu Kính Mặt Trời rồi! Dây neo mở rồi ba ơi!'", Color.yellow, 4.0f);

            StartCoroutine(LowerDeckBridgeRoutine());
        }

        private IEnumerator LowerDeckBridgeRoutine()
        {
            UISoundManager.PlayConfirm();

            if (deckBridge != null)
            {
                Vector3 startPos = deckBridge.position;
                Vector3 endPos = new Vector3(43.70f, 5.85f, -55.00f);
                Quaternion startRot = deckBridge.rotation;
                Quaternion endRot = Quaternion.Euler(0f, 0f, 0f);
                float dur = 2.0f;
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
            ShowPrompt("⚓ CẦU NỐI ĐÃ HẠ HOÀN TẤT! Con mau tuột thang xuống đem Thấu Kính sang bệ gương buồng lái!", Color.green, 5.0f);
            Debug.Log("[ShipwreckCoop] Cầu gỗ nối hai sàn tàu đã hạ thành công!");
        }

        private void InstallSolarLens()
        {
            isLensInstalled = true;
            UISoundManager.PlayConfirm();

            // Hiển thị thấu kính trên bệ đài gương
            if (m_SolarPedestalGo != null)
            {
                var dish = m_SolarPedestalGo.transform.Find("Reflector_Dish");
                if (dish != null)
                {
                    var socket = dish.Find("Mounted_Lens");
                    if (socket != null) socket.gameObject.SetActive(true);
                }

                if (m_SolarBeamLine != null) m_SolarBeamLine.enabled = true;
                if (m_SolarBeamLight != null)
                {
                    m_SolarBeamLight.enabled = true;
                    m_SolarBeamLight.intensity = 5.0f;
                }
            }

            ShowPrompt("✨ 👦 Con: 'Thấu kính đã vào khớp rồi ba! Ba xoay bánh lái điều chỉnh tia sáng về phía viên ngọc vách núi!'", Color.green, 6.0f);
        }

        private void PerformWheelTurning()
        {
            if (isBeaconAligned) return;

            if (steeringWheel != null)
            {
                if (!m_WheelInitRecorded) { m_WheelInitialRot = steeringWheel.localRotation; m_WheelInitRecorded = true; }
                float step = 110f * Time.deltaTime;
                steeringWheel.localRotation = steeringWheel.localRotation * Quaternion.Euler(0f, 0f, step);
                m_WheelTurnedTotal += step;
            }

            wheelTurnProgress = Mathf.Clamp01(m_WheelTurnedTotal / 360f);

            if (m_WheelTurnedTotal >= 360f && wheelTurnProgress >= 1f)
            {
                wheelTurnProgress = 1f;
                isWheelCompleted = true;
                isBeaconAligned = true;

                TriggerSolarBeaconClimax();
            }
        }

        private void TriggerSolarBeaconClimax()
        {
            UISoundManager.PlaySuccess();

            // 1. Kích hoạt Viên Ngọc Phong Ấn trên vách đá Tháp Canh
            if (m_CliffRuneGo != null)
            {
                var core = m_CliffRuneGo.transform.Find("Rune_Core_Gem");
                if (core != null)
                {
                    var rend = core.GetComponent<MeshRenderer>();
                    if (rend != null) rend.sharedMaterial = CreateGlowMaterial("Mat_CliffRune_Active", new Color(1f, 0.9f, 0.3f), 4.5f);
                }
                if (m_CliffRuneLight != null)
                {
                    m_CliffRuneLight.color = new Color(1f, 0.9f, 0.35f);
                    m_CliffRuneLight.range = 25f;
                    m_CliffRuneLight.intensity = 8.0f;
                }
            }

            ShowPrompt("☀️ TIA SÁNG MẶT TRỜI ĐÃ PHÁ VỠ PHONG ẤN VÁCH ĐÁ! RƯƠNG THUYỀN TRƯỞNG ĐANG MỞ!", Color.green, 6.0f);
            Debug.Log("[ShipwreckCoop] Tia sáng mặt trời đã kết nối thành công với viên ngọc vách đá Tháp Canh!");

            // 2. Mở Rương Thuyền Trưởng
            StartCoroutine(OpenCaptainChestRoutine());

            // 3. Hạ Cầu Ván Gangplank nối sang bờ đá Tháp Canh
            StartCoroutine(LowerExitGangplankRoutine());
        }

        private void UpdateSolarBeamVisuals()
        {
            if (!isLensInstalled || m_SolarBeamLine == null || m_SolarPedestalGo == null) return;

            Vector3 origin = m_SolarPedestalGo.transform.position + Vector3.up * 0.25f;

            // Góc quét theo tiến độ xoay bánh lái (từ quét lệch sang khóa mục tiêu vào cliffRunePos)
            float t = isBeaconAligned ? 1.0f : wheelTurnProgress;
            Vector3 sweepTarget = Vector3.Lerp(cliffRunePos + new Vector3(15f, -4f, 10f), cliffRunePos, Mathf.SmoothStep(0f, 1f, t));

            m_SolarBeamLine.SetPosition(0, origin);
            m_SolarBeamLine.SetPosition(1, sweepTarget);

            if (m_SolarBeamLight != null)
            {
                m_SolarBeamLight.transform.position = origin;
                m_SolarBeamLight.transform.rotation = Quaternion.LookRotation((sweepTarget - origin).normalized);
            }
        }

        private IEnumerator LowerExitGangplankRoutine()
        {
            if (exitGangplank == null) yield break;

            UISoundManager.PlayConfirm();
            Quaternion startRot = exitGangplank.rotation;
            Quaternion endRot = Quaternion.Euler(14f, 40f, 0f);
            float dur = 2.4f;
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / dur);
                exitGangplank.rotation = Quaternion.Slerp(startRot, endRot, t);
                yield return null;
            }
            exitGangplank.rotation = endRot;
            isGangplankLowered = true;

            // Thắp sáng dàn đuốc trên vách đá Tháp Canh
            LightUpCliffTorches();

            ShowPrompt("✨ CẦU VÁN ĐÃ HẠ! LỐI LÊN THÁP CANH ĐÃ RỰC SÁNG! Hai Cha Con hãy tiến lên Tháp Canh!", Color.green, 8.0f);
        }

        private void LightUpCliffTorches()
        {
            Vector3[] torchCoords = new Vector3[]
            {
                new Vector3(34.5f, 5.2f, -65.5f),
                new Vector3(29.0f, 6.8f, -70.5f),
                new Vector3(25.6f, 9.2f, -73.4f),
                new Vector3(21.0f, 12.5f, -73.5f)
            };

            for (int i = 0; i < torchCoords.Length; i++)
            {
                var torch = new GameObject($"Ship_Cliff_Torch_{i}");
                torch.transform.position = torchCoords[i];

                var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "Torch_Post";
                post.transform.SetParent(torch.transform, false);
                post.transform.localScale = new Vector3(0.2f, 0.8f, 0.2f);
                post.transform.localPosition = new Vector3(0f, 0.8f, 0f);
                DestroyImmediate(post.GetComponent<Collider>());

                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "Torch_Flame";
                flame.transform.SetParent(torch.transform, false);
                flame.transform.localPosition = new Vector3(0f, 1.7f, 0f);
                flame.transform.localScale = new Vector3(0.35f, 0.45f, 0.35f);
                flame.GetComponent<MeshRenderer>().sharedMaterial = CreateGlowMaterial("Mat_Torch_Flame", new Color(1f, 0.65f, 0.1f), 3.0f);
                DestroyImmediate(flame.GetComponent<Collider>());

                var l = torch.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.7f, 0.2f);
                l.range = 8.0f;
                l.intensity = 2.8f;

                m_AscentTorches.Add(torch);
            }
        }

        private IEnumerator OpenCaptainChestRoutine()
        {
            UISoundManager.PlayConfirm();

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
            ActivateCaptainGoldKey();
            UISoundManager.PlaySuccess();
            Debug.Log("[ShipwreckCoop] Rương thuyền trưởng đã mở, kho báu phát sáng!");
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

        private void ActivateCaptainGoldKey()
        {
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name != "Castle_Sun_Key") continue;
                    child.gameObject.SetActive(true);
                    return;
                }
            }
        }

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

            if (m_GoldLight == null)
            {
                var lightGo = new GameObject("HMH_West_ChestGold_Light");
                lightGo.transform.SetParent(goldAnchor, false);
                lightGo.transform.localPosition = Vector3.up * 1.0f;
                m_GoldLight = lightGo.AddComponent<Light>();
                m_GoldLight.type = LightType.Point;
                m_GoldLight.color = new Color(1f, 0.8f, 0.35f);
                m_GoldLight.intensity = 2.8f;
                m_GoldLight.range = 6f;
            }
            else m_GoldLight.enabled = true;
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

        private void TickWheelSlipback()
        {
            if (isBeaconAligned || isChestOpened) return;
            if (m_WheelTurnedTotal <= 0f) return;
            if (IsInteractHeld()) return;
            if (!isLensInstalled) return;

            float slip = wheelSlipback * Time.deltaTime;
            m_WheelTurnedTotal = Mathf.Max(0f, m_WheelTurnedTotal - slip);
            wheelTurnProgress = Mathf.Clamp01(m_WheelTurnedTotal / 360f);
            if (steeringWheel != null && slip > 0f)
                steeringWheel.localRotation = steeringWheel.localRotation * Quaternion.Euler(0f, 0f, -slip);
        }

        private void TickAmbientLife()
        {
            m_AmbientT += Time.deltaTime;
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

            float width = 920f;
            float height = 64f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 110f;

            GUI.backgroundColor = new Color(0.04f, 0.05f, 0.08f, 0.90f);
            GUI.Box(new Rect(x, y, width, height), currentPrompt, boxStyle);
        }

        private bool IsAnyPlayerInShipwreckZone()
        {
            if (cachedControllers == null || cachedControllers.Length == 0) return true;
            for (int i = 0; i < cachedControllers.Length; i++)
            {
                var cc = cachedControllers[i];
                if (cc != null && cc.gameObject.activeInHierarchy && cc.transform.position.x >= 24f)
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

            float w = 360f, h = 126f;
            float x = 18f, y = 18f;
            GUI.Box(new Rect(x, y, w, h), "🎯 HẢI ĐĂNG TÀU HOA TIÊU SAO BẮC ĐẨU", titleStyle);

            bool p1 = isBridgeLowered;
            bool p2 = isLensInstalled;
            bool p3 = isBeaconAligned;
            bool p4 = isChestOpened;

            DrawObjectiveLine(itemStyle, x, y + 28f, p1, "1. Ba ghì đối trọng → Con leo lấy Thấu Kính");
            DrawObjectiveLine(itemStyle, x, y + 52f, p2, "2. Con lắp Thấu Kính vào bệ gương quang học");
            DrawObjectiveLine(itemStyle, x, y + 76f, p3, "3. Ba xoay bánh lái chiếu tia sáng mở tháp");
            DrawObjectiveLine(itemStyle, x, y + 100f, p4, "4. Nhặt Chìa Khóa Cổ Thành → Lên Tháp Canh");
        }

        private void DrawObjectiveLine(GUIStyle style, float x, float y, bool done, string text)
        {
            style.normal.textColor = done ? new Color(0.45f, 0.95f, 0.5f) : new Color(0.85f, 0.87f, 0.92f);
            GUI.Label(new Rect(x + 12f, y, 340f, 22f), (done ? "✔ " : "○ ") + text, style);
        }

        #endregion
    }
}
