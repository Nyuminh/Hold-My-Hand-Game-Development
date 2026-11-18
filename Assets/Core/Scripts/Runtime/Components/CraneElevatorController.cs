using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển Cơ Chế Cần Cẩu Đối Trọng & Thang Máy (Crane Counterweight & Elevator Lift).
    /// Hỗ trợ Netcode for GameObjects (không dùng SetParent trên NetworkObject để tránh lỗi Invalid parenting).
    /// </summary>
    [SelectionBase]
    public class CraneElevatorController : NetworkBehaviour
    {
        private static CraneElevatorController instance;
        public static CraneElevatorController Instance => instance;

        [Header("Crane & Counterweight Objects")]
        [Tooltip("Transform của quả cầu đá khổng lồ (Crane_Stone_Ball)")]
        [SerializeField] private Transform stoneBall;

        [Tooltip("Transform của dây xích treo bóng (Crane_Chain)")]
        [SerializeField] private Transform stoneBallChain;

        [Tooltip("Transform của bánh răng tời quay (Crane_Cogwheel)")]
        [SerializeField] private Transform cogwheel;

        [Tooltip("Transform của cần gạt lẫy tương tác (Crane_Release_Lever)")]
        [SerializeField] private Transform leverHandle;

        [Tooltip("Transform của đĩa áp lực màu đỏ (Pressure_Plate_Pad)")]
        [SerializeField] private Transform pressurePlatePad;

        [Header("Elevator Platform & Structures")]
        [Tooltip("Sàn thang máy di động (Crane_Lift_Platform)")]
        [SerializeField] private Transform liftPlatform;

        [Tooltip("Sàn đón ở bờ cao nguyên Zone 3 (Zone3_Landing_Deck)")]
        [SerializeField] private Transform zone3LandingDeck;

        [Tooltip("Ván cầu bước từ Trụ đá Islet lên sàn thang máy")]
        [SerializeField] private Transform isletBoardingPlank;

        [Header("Cable Visuals (LineRenderers)")]
        [Tooltip("Dây cáp ray dẫn hướng trái")]
        [SerializeField] private LineRenderer guideCableLeft;

        [Tooltip("Dây cáp ray dẫn hướng phải")]
        [SerializeField] private LineRenderer guideCableRight;

        [Header("Movement & Animation Settings")]
        [Tooltip("Vị trí local Y của bóng khi đang treo trên cần cẩu")]
        [SerializeField] private float ballHighLocalY = 3.30f;

        [Tooltip("Vị trí local Y của bóng khi đè lên đĩa đỏ")]
        [SerializeField] private float ballLandedLocalY = -0.75f;

        [Tooltip("Vị trí local Y đĩa áp lực bình thường (chưa lún)")]
        [SerializeField] private float padNormalLocalY = 0.00f;

        [Tooltip("Vị trí local Y đĩa áp lực khi bị đè lún xuống")]
        [SerializeField] private float padPressedLocalY = -0.15f;

        [Tooltip("Vị trí sàn thang máy ở trạm đón Trụ đá trung tâm")]
        [SerializeField] private Vector3 liftLowPosition = new Vector3(-1.0f, 10.65f, -24.8f);

        [Tooltip("Vị trí sàn thang máy ở trạm cập bến bờ cao nguyên Zone 3")]
        [SerializeField] private Vector3 liftHighPosition = new Vector3(-1.0f, 13.90f, -45.0f);

        [Tooltip("Thời gian bóng rơi tự do xuống đĩa đỏ (giây)")]
        [SerializeField] private float ballDropDuration = 0.85f;

        [Tooltip("Thời gian thang máy bay nâng lên Zone 3 (giây)")]
        [SerializeField] private float liftTravelDuration = 3.8f;

        [Header("Interaction Settings")]
        [Tooltip("Khoảng cách người chơi đứng gần cần gạt/bánh răng để tương tác (mét)")]
        [SerializeField] private float interactDistance = 4.5f;

        [Tooltip("Vị trí điểm tương tác (cần gạt tại bục cần cẩu)")]
        [SerializeField] private Vector3 interactPoint = new Vector3(-9.4f, 7.8f, -14.2f);

        [Header("Crane Capstan Winch (Bánh Xoay Ròng Rọc & Tiêu Hao Thể Lực)")]
        [Tooltip("Transform bánh xoay ròng rọc (Capstan_Wheel)")]
        [SerializeField] private Transform capstanWheel;

        [Tooltip("Dây cáp tời nối từ bánh xoay lên cần cẩu tới quả cầu đá (Crane_Winch_Rope)")]
        [SerializeField] private LineRenderer craneWinchRope;

        [Tooltip("Vị trí tương tác ròng rọc tời của Ba")]
        [SerializeField] private Vector3 winchInteractPoint = new Vector3(-9.50f, 9.00f, -14.00f);

        [Tooltip("Tốc độ tiêu hao thể lực mỗi giây khi Ba quay ròng rọc")]
        [SerializeField] private float staminaDrainPerSec = 16.0f;

        [Tooltip("Thời gian quay ròng rọc liên tục để hạ hết quả cầu đá (giây)")]
        [SerializeField] private float crankDuration = 4.5f;

        [Tooltip("Tốc độ quay hình ảnh của bánh ròng rọc (độ/giây)")]
        [SerializeField] private float wheelSpinSpeed = 220.0f;

        [SerializeField] private float currentCrankProgress = 0f;
        public float CurrentCrankProgress => currentCrankProgress;

        [Header("Zone 3 Return Winch / Lever")]
        [Tooltip("Transform cần gạt tời tại bờ Zone 3 (Zone3_Return_Lever)")]
        [SerializeField] private Transform zone3LeverHandle;

        [Tooltip("Transform trống tời / bánh xích Zone 3")]
        [SerializeField] private Transform zone3WinchDrum;

        [Tooltip("Vị trí điểm tương tác cần tời Zone 3")]
        [SerializeField] private Vector3 zone3InteractPoint = new Vector3(0.8f, 14.2f, -45.8f);

        [Header("Zone 3 Co-op Puzzle (Boulder & Seesaw Lock)")]
        [Tooltip("Tảng đá khổng lồ để Con đẩy làm đối trọng (Pushable_Boulder)")]
        [SerializeField] private Transform pushableBoulder;

        [Tooltip("Đòn bẩy bập bênh Zone 3 (Zone3_Seesaw_Beam)")]
        [SerializeField] private Transform seesawBeam;

        [Tooltip("Máng đón đá ở đầu bập bênh (Zone3_Seesaw_Cradle)")]
        [SerializeField] private Transform seesawCradle;

        [Tooltip("Chốt khóa hãm cần tời Zone 3 (Winch_Lock_Clamp)")]
        [SerializeField] private Transform winchLockClamp;

        [Tooltip("Đèn chỉ báo trạng thái khóa (Winch_Lock_Pin: Đỏ=Khóa, Vàng=Chờ Cha, Xanh=Mở)")]
        [SerializeField] private Transform winchLockPin;

        [Header("Zone 3 Electrical Box Puzzle")]
        [Tooltip("Hộp tủ điện Zone 3 (Zone3_Electrical_Box)")]
        [SerializeField] private Transform zone3ElectricalBox;
        [Tooltip("Đèn LED trạng thái trên hộp điện (Box_Status_LED)")]
        [SerializeField] private MeshRenderer boxStatusLedRenderer;

        private bool isWirePuzzleOpen = false;
        private bool[] wireConnected = new bool[4];
        private int draggingWire = -1;
        private Vector2 dragCurrentPos;
        private int hoveredRightSlot = -1;
        private float sparkTimer = 0f;
        private Vector2 sparkPos;
        private float buzzTimer = 0f;
        private readonly int[] rightTerminalMapping = new int[] { 3, 1, 0, 2 }; // Slot 0: Pink (3), Slot 1: Blue (1), Slot 2: Red (0), Slot 3: Yellow (2)
        private string wirePuzzleMessage = "";
        private Transform winchDrumTransform;
        private Transform doorHingeTransform;

        public enum ElevatorStage
        {
            ReadyForFirstRide = 0,          // Khởi đầu: Cha gạt cần ở bục Cần cẩu đưa Con sang Zone 3
            MovingToZone3_First = 1,        // Thang đang chở Con bay sang Zone 3
            ConAtZone3_NeedsBoulder = 2,    // Con đã đến Zone 3: Cần tời bị khóa, Con cần giải đố đẩy tảng đá vào bập bênh
            BoulderMoving = 3,              // Tảng đá đang lăn vào bập bênh làm đối trọng sơ cấp
            BoulderPlaced_WaitingDad = 4,   // Con đã đặt đá: Cha cần nhả bánh răng cần cẩu bên kia vực để mở hoàn toàn chốt tời
            BothUnlocked_ReadyReturn = 5,   // Chốt khóa đã mở! Con kéo cần tời để gửi thang về Islet đón Cha
            ReturningToIslet = 6,           // Thang đang chạy ngược về Islet đón Cha
            ThangAtIslet_ReadyDad = 7,      // Thang đã về đón Cha, Con kéo tời đưa Cha sang Zone 3
            MovingToZone3_Second = 8,       // Thang đang chở Cha bay sang Zone 3
            BothCompleted = 9               // Cả hai Cha Con đã đoàn tụ trên Zone 3!
        }

        
        [Header("Netcode Synchronization")]
        private readonly NetworkVariable<ElevatorStage> netStage = new NetworkVariable<ElevatorStage>(
            ElevatorStage.ReadyForFirstRide,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private readonly NetworkVariable<float> netCrankProgress = new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            netStage.OnValueChanged += OnStageNetworkChanged;
            netCrankProgress.OnValueChanged += OnCrankProgressNetworkChanged;

            if (IsServer)
            {
                netStage.Value = currentStage;
                netCrankProgress.Value = currentCrankProgress;
            }
            else
            {
                currentStage = netStage.Value;
                currentCrankProgress = netCrankProgress.Value;
                ApplyStagePose(currentStage);
                ApplyCrankVisuals(currentCrankProgress);
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            netStage.OnValueChanged -= OnStageNetworkChanged;
            netCrankProgress.OnValueChanged -= OnCrankProgressNetworkChanged;
        }

        private void OnStageNetworkChanged(ElevatorStage oldVal, ElevatorStage newVal)
        {
            currentStage = newVal;
            ApplyStagePose(newVal);
        }

        private void OnCrankProgressNetworkChanged(float oldVal, float newVal)
        {
            currentCrankProgress = newVal;
            ApplyCrankVisuals(newVal);
        }

        public void ApplyCrankVisuals(float progress)
        {
            if (stoneBall != null)
            {
                Vector3 lp = stoneBall.localPosition;
                lp.y = Mathf.Lerp(ballHighLocalY, ballLandedLocalY, progress);
                stoneBall.localPosition = lp;
            }

            if (liftPlatform != null)
            {
                liftPlatform.position = Vector3.Lerp(liftLowPosition, liftHighPosition, progress);
            }

            if (progress >= 1f && pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padPressedLocalY;
                pressurePlatePad.localPosition = lp;
            }

            UpdateCables();
        }

        public void ApplyStagePose(ElevatorStage stage)
        {
            switch (stage)
            {
                case ElevatorStage.ReadyForFirstRide:
                    SetToInitialPose();
                    break;
                case ElevatorStage.ConAtZone3_NeedsBoulder:
                    PreviewConAtZone3();
                    break;
                case ElevatorStage.BoulderPlaced_WaitingDad:
                    if (pushableBoulder != null) pushableBoulder.position = cradleTargetWorldPos;
                    if (seesawBeam != null) seesawBeam.localRotation = seesawInitialRot * Quaternion.Euler(6.5f, 0f, 0f);
                    if (winchLockPin != null)
                    {
                        winchLockPin.localPosition = pinInitialPos + new Vector3(0f, 0.08f, 0f);
                        SetLockPinMaterial(matYellowIndicator);
                    }
                    break;
                case ElevatorStage.BothUnlocked_ReadyReturn:
                    if (winchLockClamp != null) winchLockClamp.localPosition = clampInitialPos + new Vector3(0f, 0.16f, 0f);
                    if (winchLockPin != null)
                    {
                        winchLockPin.localPosition = pinInitialPos + new Vector3(0f, 0.22f, 0f);
                        SetLockPinMaterial(matGreenIndicator);
                    }
                    break;
                case ElevatorStage.ThangAtIslet_ReadyDad:
                    PreviewThangAtIslet();
                    break;
                case ElevatorStage.BothCompleted:
                    PreviewBothCompleted();
                    break;
            }
            UpdateCables();
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpdateCrankProgressServerRpc(float deltaProgress)
        {
            float newProg = Mathf.Clamp01(netCrankProgress.Value + deltaProgress);
            netCrankProgress.Value = newProg;
            if (newProg >= 1f && netStage.Value == ElevatorStage.ReadyForFirstRide)
            {
                netStage.Value = ElevatorStage.ConAtZone3_NeedsBoulder;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestRollBoulderServerRpc()
        {
            if (netStage.Value == ElevatorStage.ConAtZone3_NeedsBoulder && !IsMoving)
            {
                netStage.Value = ElevatorStage.BoulderMoving;
                StartRollBoulderClientRpc();
            }
        }

        [ClientRpc]
        private void StartRollBoulderClientRpc()
        {
            if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
            activeMotionRoutine = StartCoroutine(RollBoulderRoutine());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestDadReleaseClutchServerRpc()
        {
            if (netStage.Value == ElevatorStage.BoulderPlaced_WaitingDad && !IsMoving)
            {
                StartDadReleaseClutchClientRpc();
            }
        }

        [ClientRpc]
        private void StartDadReleaseClutchClientRpc()
        {
            if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
            activeMotionRoutine = StartCoroutine(DadReleaseClutchRoutine());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestReturnToIsletServerRpc()
        {
            if (netStage.Value == ElevatorStage.BothUnlocked_ReadyReturn && !IsMoving)
            {
                netStage.Value = ElevatorStage.ReturningToIslet;
                StartReturnToIsletClientRpc();
            }
        }

        [ClientRpc]
        private void StartReturnToIsletClientRpc()
        {
            if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
            activeMotionRoutine = StartCoroutine(ReturnToIsletRoutine());
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSecondRideServerRpc()
        {
            if (netStage.Value == ElevatorStage.ThangAtIslet_ReadyDad && !IsMoving)
            {
                netStage.Value = ElevatorStage.MovingToZone3_Second;
                StartSecondRideClientRpc();
            }
        }

        [ClientRpc]
        private void StartSecondRideClientRpc()
        {
            if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
            activeMotionRoutine = StartCoroutine(SecondRideRoutine());
        }

        [ServerRpc(RequireOwnership = false)]
        public void CompleteWirePuzzleServerRpc()
        {
            netStage.Value = ElevatorStage.BothUnlocked_ReadyReturn;
            ApplyWirePuzzleCompleteClientRpc();
        }

        [ClientRpc]
        private void ApplyWirePuzzleCompleteClientRpc()
        {
            if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
            activeMotionRoutine = StartCoroutine(WirePuzzleSuccessRoutine());
        }

        [Header("Co-op Elevator Progression")]
        [SerializeField] private ElevatorStage currentStage = ElevatorStage.ReadyForFirstRide;
        public ElevatorStage CurrentStage => currentStage;

        public bool IsActivated => currentStage != ElevatorStage.ReadyForFirstRide;
        public bool IsElevatorAtTop => currentStage == ElevatorStage.ConAtZone3_NeedsBoulder ||
                                       currentStage == ElevatorStage.BoulderMoving ||
                                       currentStage == ElevatorStage.BoulderPlaced_WaitingDad ||
                                       currentStage == ElevatorStage.BothUnlocked_ReadyReturn ||
                                       currentStage == ElevatorStage.BothCompleted;
        public bool IsMoving => currentStage == ElevatorStage.MovingToZone3_First ||
                                currentStage == ElevatorStage.BoulderMoving ||
                                currentStage == ElevatorStage.ReturningToIslet ||
                                currentStage == ElevatorStage.MovingToZone3_Second;

        private Vector3 ballInitialPos;
        private Vector3 chainInitialScale;
        private Vector3 chainInitialPos;
        private Vector3 padInitialPos;
        private Quaternion leverInitialRot;
        private Quaternion zone3LeverInitialRot;
        private Vector3 boulderInitialPos;
        private Quaternion boulderInitialRot;
        private Quaternion seesawInitialRot;
        private Vector3 clampInitialPos;
        private Vector3 pinInitialPos;
        private Vector3 cradleTargetWorldPos = new Vector3(1.50f, 14.80f, -47.67f);

        private Material matRedIndicator;
        private Material matYellowIndicator;
        private Material matGreenIndicator;

        private Coroutine activeMotionRoutine;
        private string currentPrompt = "";
        private Camera mainCamera;

        private void Awake()
        {
            if (instance == null) instance = this;
            CacheInitialTransforms();
        }

        private void Start()
        {
            CacheInitialTransforms();
            SetToInitialPose();
            UpdateCables();
        }

        private void Update()
        {
            if (isWirePuzzleOpen)
            {
                HandleWirePuzzleKeyboardInput();
            }
            if (IsNetworkActive && !IsMoving && currentStage == ElevatorStage.ReadyForFirstRide)
            {
                if (Mathf.Abs(currentCrankProgress - netCrankProgress.Value) > 0.002f)
                {
                    currentCrankProgress = Mathf.MoveTowards(currentCrankProgress, netCrankProgress.Value, Time.deltaTime * 3f);
                    ApplyCrankVisuals(currentCrankProgress);
                }
            }
            CheckPlayerProximityAndInput();
            UpdateCables();
        }

        private void CacheInitialTransforms()
        {
            if (stoneBall != null) ballInitialPos = stoneBall.position;
            if (stoneBallChain != null)
            {
                chainInitialPos = stoneBallChain.position;
                chainInitialScale = stoneBallChain.localScale;
            }
            if (pressurePlatePad != null) padInitialPos = pressurePlatePad.position;
            if (leverHandle != null) leverInitialRot = leverHandle.localRotation;
            if (zone3LeverHandle != null) zone3LeverInitialRot = zone3LeverHandle.localRotation;
            if (mainCamera == null) mainCamera = Camera.main;

            if (zone3LeverHandle == null)
            {
                GameObject z3Hinge = GameObject.Find("Winch_Lever_Hinge");
                if (z3Hinge != null)
                {
                    zone3LeverHandle = z3Hinge.transform;
                    zone3LeverInitialRot = zone3LeverHandle.localRotation;
                }
            }
            if (zone3WinchDrum == null)
            {
                GameObject drum = GameObject.Find("Winch_Drum");
                if (drum != null) zone3WinchDrum = drum.transform;
            }

            // Auto-cache Crane Capstan Winch
            if (capstanWheel == null)
            {
                GameObject wheel = GameObject.Find("Capstan_Wheel");
                if (wheel != null) capstanWheel = wheel.transform;
            }
            if (craneWinchRope == null)
            {
                GameObject rope = GameObject.Find("Crane_Winch_Rope");
                if (rope != null) craneWinchRope = rope.GetComponent<LineRenderer>();
            }

            // Auto-cache Zone 3 Electrical Box & Winch Drum
            if (zone3ElectricalBox == null)
            {
                GameObject eb = GameObject.Find("Zone3_Electrical_Box");
                if (eb != null)
                {
                    zone3ElectricalBox = eb.transform;
                    Transform led = eb.transform.Find("Box_Status_LED");
                    if (led != null) boxStatusLedRenderer = led.GetComponent<MeshRenderer>();
                }
            }
            if (doorHingeTransform == null && zone3ElectricalBox != null)
            {
                doorHingeTransform = zone3ElectricalBox.Find("Door_Hinge_Pivot");
            }
            if (winchDrumTransform == null)
            {
                GameObject d = GameObject.Find("Winch_Winding_Drum");
                if (d != null) winchDrumTransform = d.transform;
            }

            // Auto-cache Zone 3 Puzzle objects
            if (pushableBoulder == null)
            {
                GameObject b = GameObject.Find("Pushable_Boulder");
                if (b != null) pushableBoulder = b.transform;
            }
            if (seesawBeam == null)
            {
                GameObject bm = GameObject.Find("Zone3_Seesaw_Beam");
                if (bm != null) seesawBeam = bm.transform;
            }
            if (seesawCradle == null)
            {
                GameObject cr = GameObject.Find("Zone3_Seesaw_Cradle");
                if (cr != null)
                {
                    seesawCradle = cr.transform;
                    cradleTargetWorldPos = seesawCradle.position + new Vector3(0f, 0.75f, 0f);
                }
            }
            if (winchLockClamp == null)
            {
                GameObject cl = GameObject.Find("Winch_Lock_Clamp");
                if (cl != null) winchLockClamp = cl.transform;
            }
            if (winchLockPin == null)
            {
                GameObject pin = GameObject.Find("Winch_Lock_Pin");
                if (pin != null) winchLockPin = pin.transform;
            }

            if (pushableBoulder != null)
            {
                boulderInitialPos = pushableBoulder.position;
                boulderInitialRot = pushableBoulder.rotation;
            }
            if (seesawBeam != null) seesawInitialRot = seesawBeam.localRotation;
            if (winchLockClamp != null) clampInitialPos = winchLockClamp.localPosition;
            if (winchLockPin != null) pinInitialPos = winchLockPin.localPosition;

#if UNITY_EDITOR
            if (matRedIndicator == null) matRedIndicator = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Art/Materials/Mat_Wire_Red.mat");
            if (matYellowIndicator == null) matYellowIndicator = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Art/Materials/Mat_Wire_Yellow.mat");
            if (matGreenIndicator == null) matGreenIndicator = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Art/Materials/Mat_Wire_Green.mat");
#endif
        }

        private void SetLockPinMaterial(Material mat)
        {
            if (winchLockPin != null && mat != null)
            {
                var mr = winchLockPin.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = mat;
            }
        }

        /// <summary>
        /// Đặt trạng thái ban đầu: bóng treo trên cao, thang máy ở Trụ Islet, chốt tời khóa đỏ.
        /// </summary>
        [ContextMenu("Reset To Initial Pose")]
        public void SetToInitialPose()
        {
            currentStage = ElevatorStage.ReadyForFirstRide;
            currentCrankProgress = 0f;

            if (stoneBall != null)
            {
                Vector3 lp = stoneBall.localPosition;
                lp.y = ballHighLocalY;
                stoneBall.localPosition = lp;
            }

            if (stoneBallChain != null)
            {
                stoneBallChain.position = chainInitialPos;
                stoneBallChain.localScale = chainInitialScale;
            }

            if (pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padNormalLocalY;
                pressurePlatePad.localPosition = lp;
            }

            if (liftPlatform != null)
            {
                liftPlatform.position = liftLowPosition;
            }

            if (leverHandle != null)
            {
                leverHandle.localRotation = leverInitialRot;
            }

            if (zone3LeverHandle != null)
            {
                zone3LeverHandle.localRotation = zone3LeverInitialRot;
            }

            isWirePuzzleOpen = false;
            wireConnected = new bool[4];
            draggingWire = -1;
            hoveredRightSlot = -1;
            wirePuzzleMessage = "";
            if (boxStatusLedRenderer != null && matRedIndicator != null)
            {
                boxStatusLedRenderer.sharedMaterial = matRedIndicator;
            }

            if (doorHingeTransform != null)
            {
                doorHingeTransform.localEulerAngles = new Vector3(0f, -115f, 0f);
            }
            if (zone3ElectricalBox != null)
            {
                foreach (Transform child in zone3ElectricalBox)
                {
                    if (child.name.StartsWith("Wire_")) child.gameObject.SetActive(true);
                }
            }

            // Đặt lại tảng đá và bập bênh Zone 3
            if (pushableBoulder != null && boulderInitialPos != Vector3.zero)
            {
                pushableBoulder.position = boulderInitialPos;
                pushableBoulder.rotation = boulderInitialRot;
            }
            if (seesawBeam != null)
            {
                seesawBeam.localRotation = seesawInitialRot;
            }
            if (winchLockClamp != null)
            {
                winchLockClamp.localPosition = clampInitialPos;
            }
            if (winchLockPin != null)
            {
                winchLockPin.localPosition = pinInitialPos;
                SetLockPinMaterial(matRedIndicator);
            }

            UpdateCables();
        }

        /// <summary>
        /// Xem trước Trạng thái: Con đã sang Zone 3, Cần tời bị khóa cần giải đố đá.
        /// </summary>
        [ContextMenu("Preview Con At Zone 3")]
        public void PreviewConAtZone3()
        {
            currentStage = ElevatorStage.ConAtZone3_NeedsBoulder;
            currentCrankProgress = 1f;

            if (stoneBall != null)
            {
                Vector3 lp = stoneBall.localPosition;
                lp.y = ballLandedLocalY;
                stoneBall.localPosition = lp;
            }

            if (pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padPressedLocalY;
                pressurePlatePad.localPosition = lp;
            }

            if (liftPlatform != null)
            {
                liftPlatform.position = liftHighPosition;
            }

            if (leverHandle != null)
            {
                leverHandle.localRotation = leverInitialRot * Quaternion.Euler(45f, 0f, 0f);
            }

            SetLockPinMaterial(matRedIndicator);
            UpdateCables();
        }

        /// <summary>
        /// Xem trước Trạng thái 4: Thang đã quay về Islet đón Cha, Con ở Zone 3 chuẩn bị kéo thang.
        /// </summary>
        [ContextMenu("Preview Thang At Islet (Ready For Dad)")]
        public void PreviewThangAtIslet()
        {
            currentStage = ElevatorStage.ThangAtIslet_ReadyDad;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.ThangAtIslet_ReadyDad;

            if (stoneBall != null)
            {
                Vector3 lp = stoneBall.localPosition;
                lp.y = ballHighLocalY;
                stoneBall.localPosition = lp;
            }

            if (pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padNormalLocalY;
                pressurePlatePad.localPosition = lp;
            }

            if (liftPlatform != null)
            {
                liftPlatform.position = liftLowPosition;
            }

            UpdateCables();
        }

        /// <summary>
        /// Xem trước Trạng thái 6: Cả hai Cha Con đã sang Zone 3 an toàn.
        /// </summary>
        [ContextMenu("Preview Both Completed")]
        public void PreviewBothCompleted()
        {
            currentStage = ElevatorStage.BothCompleted;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BothCompleted;

            if (stoneBall != null)
            {
                Vector3 lp = stoneBall.localPosition;
                lp.y = ballLandedLocalY;
                stoneBall.localPosition = lp;
            }

            if (pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padPressedLocalY;
                pressurePlatePad.localPosition = lp;
            }

            if (liftPlatform != null)
            {
                liftPlatform.position = liftHighPosition;
            }

            UpdateCables();
        }

        /// <summary>
        /// Kiểm tra khoảng cách người chơi đến Cần gạt lẫy hoặc Cần tời Zone 3, hiển thị UI Prompt và bắt phím tương tác.
        /// </summary>
        private void CheckPlayerProximityAndInput()
        {
            currentPrompt = "";

            if (IsMoving)
            {
                if (currentStage == ElevatorStage.MovingToZone3_First)
                    currentPrompt = "⚙️ Thang máy đang đưa Con bay sang Zone 3...";
                else if (currentStage == ElevatorStage.ReturningToIslet)
                    currentPrompt = "🔄 Thang máy đang quay lại Trụ đá Islet đón Cha...";
                else if (currentStage == ElevatorStage.MovingToZone3_Second)
                    currentPrompt = "⚙️ Thang máy đang đưa Cha bay sang Zone 3...";
                return;
            }

            if (currentStage == ElevatorStage.BothCompleted)
            {
                currentPrompt = "🎉 Hai Cha Con đã đoàn tụ trên Cao Nguyên Zone 3! Hãy cùng tiếp tục vượt ải!";
                return;
            }

            List<Transform> players = GetAllPlayerTransforms();
            if (players.Count == 0) return;

            // 1. GIAI ĐOẠN 0: Cha xoay bánh ròng rọc tời để hạ quả cầu đá đưa Con sang
            if (currentStage == ElevatorStage.ReadyForFirstRide)
            {
                Vector3 craneP = capstanWheel != null ? capstanWheel.position : winchInteractPoint;
                Transform dadPlayer = FindDadPlayer();
                Transform closestPlayer = null;
                float closestDist = float.MaxValue;
                foreach (var p in players)
                {
                    float d = Vector3.Distance(p.position, craneP);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        closestPlayer = p;
                    }
                }

                bool isNearCrane = closestDist <= interactDistance;

                if (isNearCrane)
                {
                    bool isActorDad = IsPlayerDad(closestPlayer);
                    if (!isActorDad)
                    {
                        currentPrompt = "<color=#FFAA00>⚠️ [Con] Ròng rọc này quá nặng so với Con! Chỉ có Ba mới đủ sức quay ròng rọc!</color>";
                    }
                    else
                    {
                        Transform actor = (dadPlayer != null && Vector3.Distance(dadPlayer.position, craneP) <= interactDistance) ? dadPlayer : closestPlayer;

                    if (IsInteractHeld())
                    {
                        // Tiêu hao thể lực của Ba khi quay tời
                        bool hasStamina = TryDrainDadStamina(actor, staminaDrainPerSec * Time.deltaTime);

                        if (!hasStamina)
                        {
                            currentPrompt = "<color=#FF6666>⚠️ [HẾT THỂ LỰC] Ba đã mệt! Hãy dừng lại một lát để hồi phục thể lực!</color>";
                        }
                        else
                        {
                            // Ba quay mặt về phía bánh ròng rọc
                            if (actor != null)
                            {
                                Vector3 lookDir = (craneP - actor.position);
                                lookDir.y = 0f;
                                if (lookDir.sqrMagnitude > 0.01f)
                                {
                                    actor.rotation = Quaternion.Slerp(actor.rotation, Quaternion.LookRotation(lookDir), Time.deltaTime * 6f);
                                }
                            }

                            // Xoay bánh ròng rọc và bánh răng
                            if (capstanWheel != null)
                            {
                                capstanWheel.Rotate(Vector3.right, wheelSpinSpeed * Time.deltaTime, Space.Self);
                            }
                            if (cogwheel != null)
                            {
                                cogwheel.Rotate(Vector3.forward, wheelSpinSpeed * Time.deltaTime, Space.Self);
                            }

                            if (IsNetworkActive)
                            {
                                UpdateCrankProgressServerRpc(Time.deltaTime / crankDuration);
                                int pct = (int)(netCrankProgress.Value * 100f);
                                currentPrompt = $"⚙️ <color=#FFDD44>👨 [Ba] [GIỮ SPACE / E] Đang quay ròng rọc: {pct}% (Hao tổn thể lực...)</color>";
                            }
                            else
                            {
                                currentCrankProgress += Time.deltaTime / crankDuration;
                                currentCrankProgress = Mathf.Clamp01(currentCrankProgress);
                                ApplyCrankVisuals(currentCrankProgress);

                                int pct = (int)(currentCrankProgress * 100f);
                                currentPrompt = $"⚙️ <color=#FFDD44>👨 [Ba] [GIỮ SPACE / E] Đang quay ròng rọc: {pct}% (Hao tổn thể lực...)</color>";

                                if (currentCrankProgress >= 1f)
                                {
                                    currentStage = ElevatorStage.ConAtZone3_NeedsBoulder;
                                    SetLockPinMaterial(matRedIndicator);
                                    Debug.Log("<color=green>[CraneElevator] RẦM! CẠCH! Quả cầu đá đã đè chặt đĩa áp lực, Con đã sang bờ Zone 3 an toàn!</color>");
                                    currentPrompt = "<color=#66FF66>✅ [HOÀN THÀNH] Đã đưa Con sang Zone 3! Máy tời mất điện, Con hãy kiểm tra hộp điện bên cạnh!</color>";
                                }
                            }
                        }
                    }
                    else
                    {
                        if (currentCrankProgress > 0f)
                        {
                            int pct = (int)(currentCrankProgress * 100f);
                            currentPrompt = $"⚙️ 👨 [Ba] [GIỮ SPACE / E] Tiếp tục quay ròng rọc tời (Đã hạ: {pct}%)";
                        }
                        else
                        {
                            currentPrompt = "👨 [Ba] [GIỮ SPACE / E] Xoay ròng rọc tời để hạ quả cầu đá đưa Con sang Zone 3!";
                        }
                    }
                    }
                }
            }
            // 2. GIAI ĐOẠN 2: Con sang Zone 3 -> Con đẩy tảng đá đối trọng hoặc kiểm tra hộp điện Zone 3
            else if (currentStage == ElevatorStage.ConAtZone3_NeedsBoulder)
            {
                Vector3 boulderP = pushableBoulder != null ? pushableBoulder.position : new Vector3(-3.5f, 15.4f, -54f);
                Vector3 boxP = zone3ElectricalBox != null ? zone3ElectricalBox.position : new Vector3(1.50f, 14.50f, -46.20f);
                Vector3 z3P = zone3LeverHandle != null ? zone3LeverHandle.position : zone3InteractPoint;
                bool nearBoulder = false;
                bool nearBox = false;
                bool nearWinch = false;

                foreach (var p in players)
                {
                    if (Vector3.Distance(p.position, boulderP) <= interactDistance + 1.5f)
                    {
                        nearBoulder = true;
                    }
                    if (Vector3.Distance(p.position, boxP) <= interactDistance + 1.2f)
                    {
                        nearBox = true;
                    }
                    if (Vector3.Distance(p.position, z3P) <= interactDistance)
                    {
                        nearWinch = true;
                    }
                }

                if (nearBoulder)
                {
                    currentPrompt = "👉 [Con] Nhấn [F / E] ĐẨY TẢNG ĐÁ ĐỐI TRỌNG VÀO NÔI ĐỂ NÂNG THANG ĐÓN BA!";
                    if (IsInteractPressed() || IsInteractHeld())
                    {
                        TriggerRollBoulder();
                        return;
                    }
                }

                if (isWirePuzzleOpen)
                {
                    currentPrompt = "⚡ [BẢNG NỐI DÂY ĐIỆN] Nối đúng 4 cặp dây cùng màu để cấp nguồn cho máy tời! (Phím [1]-[4] nối nhanh, [ESC] để đóng)";
                }
                else if (nearBox)
                {
                    currentPrompt = "👦 [Con] Nhấn [E] MỞ BẢNG ĐIỀU KHIỂN NỐI DÂY CẤP NGUỒN CHO MÁY TỜI!";
                    if (IsInteractPressed())
                    {
                        OpenWirePuzzle();
                        return;
                    }
                }
                else if (nearWinch)
                {
                    currentPrompt = "🔒 [Cần Tời Mất Điện] Hộp điện bên cạnh bị chập đứt dây! Con hãy qua kiểm tra hộp điện!";
                }
                else
                {
                    currentPrompt = "👦 [Con] Hãy đến hộp điện cạnh cần tời ở Zone 3 để đấu nối cấp nguồn!";
                }
            }
            // 3. GIAI ĐOẠN 4: Con đã đặt đá -> Cần Cha bên bục Cần cẩu nhả chốt hãm cáp
            else if (currentStage == ElevatorStage.BoulderPlaced_WaitingDad)
            {
                Vector3 craneP = capstanWheel != null ? capstanWheel.position : winchInteractPoint;
                Vector3 z3P = zone3LeverHandle != null ? zone3LeverHandle.position : zone3InteractPoint;
                bool nearCrane = false;
                bool nearWinch = false;

                foreach (var p in players)
                {
                    if (Vector3.Distance(p.position, craneP) <= interactDistance + 1f ||
                        (cogwheel != null && Vector3.Distance(p.position, cogwheel.position) <= interactDistance + 1f))
                    {
                        nearCrane = true;
                    }
                    if (Vector3.Distance(p.position, z3P) <= interactDistance)
                    {
                        nearWinch = true;
                    }
                }

                if (nearCrane)
                {
                    Transform nearPlayer = null;
                    foreach (var p in players)
                    {
                        if (Vector3.Distance(p.position, craneP) <= interactDistance + 1f ||
                            (cogwheel != null && Vector3.Distance(p.position, cogwheel.position) <= interactDistance + 1f))
                        {
                            nearPlayer = p;
                            break;
                        }
                    }

                    if (!IsPlayerDad(nearPlayer))
                    {
                        currentPrompt = "<color=#FFAA00>⚠️ [Con] Chốt hãm cáp này của Ba bên cần cẩu! Chỉ có Ba mới nhả được!</color>";
                    }
                    else
                    {
                        currentPrompt = "👨 [Ba] [GIỮ SPACE / E] Xoay bánh ròng rọc nhả chốt hãm cáp cho Con!";
                        if (IsInteractHeld() || IsInteractPressed())
                        {
                            TriggerDadReleaseClutch();
                            return;
                        }
                    }
                }
                else if (nearWinch)
                {
                    currentPrompt = "⏳ [Chờ Phối Hợp] Chốt đối trọng đã cài! Đang chờ Ba nhả bánh ròng rọc bên Cần cẩu...";
                }
                else
                {
                    currentPrompt = "👨 [Ba] Hãy qua bục Cần cẩu xoay bánh ròng rọc nhả phanh cáp để mở khóa tời cho Con!";
                }
            }
            // 4. GIAI ĐOẠN 5: Cả 2 đã mở khóa thành công -> Con kéo cần tời gọi thang về Islet đón Cha!
            else if (currentStage == ElevatorStage.BothUnlocked_ReadyReturn)
            {
                Vector3 z3P = zone3LeverHandle != null ? zone3LeverHandle.position : zone3InteractPoint;
                bool isNearZ3 = false;
                foreach (var p in players)
                {
                    if (Vector3.Distance(p.position, z3P) <= interactDistance ||
                        (zone3LandingDeck != null && Vector3.Distance(p.position, zone3LandingDeck.position) <= interactDistance + 1f))
                    {
                        isNearZ3 = true;
                        break;
                    }
                }

                if (isNearZ3)
                {
                    currentPrompt = "👦 [Con] KHÓA ĐÃ MỞ! Nhấn [E] KÉO CẦN TỜI -> GỬI THANG VỀ ĐÓN CHA!";
                    if (IsInteractPressed())
                    {
                        TriggerReturnToIslet();
                        return;
                    }
                }
            }
            // 5. GIAI ĐOẠN 7: Thang đã về đón Cha -> Con ở Zone 3 kéo tời HOẶC Cha trong thang báo hiệu sang
            else if (currentStage == ElevatorStage.ThangAtIslet_ReadyDad)
            {
                Vector3 z3P = zone3LeverHandle != null ? zone3LeverHandle.position : zone3InteractPoint;
                Vector3 carriageP = liftPlatform != null ? liftPlatform.position : liftLowPosition;

                bool nearZ3 = false;
                bool nearCarriage = false;

                foreach (var p in players)
                {
                    if (Vector3.Distance(p.position, z3P) <= interactDistance) nearZ3 = true;
                    if (Vector3.Distance(p.position, carriageP) <= interactDistance) nearCarriage = true;
                }

                if (nearZ3)
                {
                    currentPrompt = "👦 [Con] Nhấn [E] KÉO CẦN TỜI -> ĐƯA CHA SANG ZONE 3!";
                    if (IsInteractPressed())
                    {
                        TriggerSecondRide();
                        return;
                    }
                }

                if (nearCarriage)
                {
                    currentPrompt = "👨 [Cha] Nhấn [E] LÊN THANG -> BAY SANG ZONE 3 VỚI CON!";
                    if (IsInteractPressed())
                    {
                        TriggerSecondRide();
                        return;
                    }
                }
            }
        }

        private List<Transform> GetAllPlayerTransforms()
        {
            List<Transform> list = new List<Transform>();
            GameObject dad = GameObject.Find("Dad");
            if (dad != null) list.Add(dad.transform);

            GameObject son = GameObject.Find("Son");
            if (son != null) list.Add(son.transform);

            GameObject child = GameObject.Find("The Child(Clone)");
            if (child != null && !list.Contains(child.transform)) list.Add(child.transform);

            GameObject dummy = GameObject.Find("Child_Companion_Dummy");
            if (dummy != null && !list.Contains(dummy.transform)) list.Add(dummy.transform);

            GameObject[] pTags = GameObject.FindGameObjectsWithTag("Player");
            if (pTags != null)
            {
                foreach (var p in pTags)
                {
                    if (!list.Contains(p.transform)) list.Add(p.transform);
                }
            }

            if (list.Count == 0 && mainCamera != null)
            {
                list.Add(mainCamera.transform);
            }

            return list;
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame ||
                    Keyboard.current.rKey.wasPressedThisFrame ||
                    Keyboard.current.fKey.wasPressedThisFrame ||
                    Keyboard.current.spaceKey.wasPressedThisFrame ||
                    Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonNorth.wasPressedThisFrame))
            {
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.F))
            {
                return true;
            }
#endif
            return false;
        }

        private bool IsInteractHeld()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.spaceKey.isPressed ||
                    Keyboard.current.eKey.isPressed ||
                    Keyboard.current.fKey.isPressed ||
                    Keyboard.current.enterKey.isPressed)
                {
                    return true;
                }
            }

            if (Gamepad.current != null && (Gamepad.current.buttonWest.isPressed || Gamepad.current.buttonSouth.isPressed))
            {
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.F))
            {
                return true;
            }
#endif
            return false;
        }

        public static bool IsPlayerDad(Transform playerTransform)
        {
            if (playerTransform == null) return false;
            var visuals = playerTransform.GetComponentInChildren<PlayerRoleVisuals>();
            if (visuals != null) return visuals.IsDad;
            var boost = playerTransform.GetComponentInChildren<CoopBoostThrowAbility>();
            if (boost != null) return boost.IsDad;
            string n = playerTransform.gameObject.name.ToLower();
            if (n.Contains("dad")) return true;
            if (n.Contains("child") || n.Contains("son")) return false;
            return Lobby3DManager.AmIDad;
        }

        private Transform FindDadPlayer()
        {
            GameObject dad = GameObject.Find("Dad(Clone)");
            if (dad != null) return dad.transform;
            dad = GameObject.Find("Dad");
            if (dad != null) return dad.transform;
            var managers = FindObjectsOfType<CorePlayerManager>();
            foreach (var m in managers)
            {
                if (IsPlayerDad(m.transform))
                    return m.transform;
            }
            return null;
        }

        private bool TryDrainDadStamina(Transform actor, float amount)
        {
            if (actor == null) return true;
            var stats = actor.GetComponent<CoreStatsHandler>();
            if (stats == null) stats = actor.GetComponentInParent<CoreStatsHandler>();
            if (stats == null) stats = actor.GetComponentInChildren<CoreStatsHandler>();
            if (stats != null)
            {
                return stats.TryConsumeStat(Animator.StringToHash("Stamina"), amount);
            }
            return true;
        }

        /// <summary>
        /// Kích hoạt lượt 1: Cha gạt cần -> Quả cầu đá rơi -> Thang đưa Con sang Zone 3!
        /// </summary>
        [ContextMenu("Trigger First Ride (Send Child)")]
        public void TriggerFirstRide()
        {
            if (currentStage != ElevatorStage.ReadyForFirstRide || IsMoving) return;
            if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
            activeMotionRoutine = StartCoroutine(FirstRideRoutine());
        }

        private IEnumerator FirstRideRoutine()
        {
            currentStage = ElevatorStage.MovingToZone3_First;
            Debug.Log("<color=yellow>[CraneElevator] LƯỢT 1: Bắt đầu xoay ròng rọc tời hạ đối trọng đưa Con bay sang Zone 3!</color>");

            float elapsed = 0f;
            float startProgress = currentCrankProgress;
            while (elapsed < crankDuration)
            {
                elapsed += Time.deltaTime;
                currentCrankProgress = Mathf.Lerp(startProgress, 1f, elapsed / crankDuration);

                if (capstanWheel != null)
                {
                    capstanWheel.Rotate(Vector3.right, wheelSpinSpeed * Time.deltaTime, Space.Self);
                }
                if (cogwheel != null)
                {
                    cogwheel.Rotate(Vector3.forward, wheelSpinSpeed * Time.deltaTime, Space.Self);
                }

                if (stoneBall != null)
                {
                    Vector3 lp = stoneBall.localPosition;
                    lp.y = Mathf.Lerp(ballHighLocalY, ballLandedLocalY, currentCrankProgress);
                    stoneBall.localPosition = lp;
                }

                if (liftPlatform != null)
                {
                    liftPlatform.position = Vector3.Lerp(liftLowPosition, liftHighPosition, currentCrankProgress);
                }

                UpdateCables();
                yield return null;
            }

            currentCrankProgress = 1f;
            if (stoneBall != null)
            {
                Vector3 lp = stoneBall.localPosition;
                lp.y = ballLandedLocalY;
                stoneBall.localPosition = lp;
            }
            if (liftPlatform != null) liftPlatform.position = liftHighPosition;
            if (pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padPressedLocalY;
                pressurePlatePad.localPosition = lp;
            }

            currentStage = ElevatorStage.ConAtZone3_NeedsBoulder;
            SetLockPinMaterial(matRedIndicator);
            UpdateCables();
            Debug.Log("<color=green>[CraneElevator] CẠCH! Quả cầu đá đè chặt đĩa áp lực, Con đã sang bờ Zone 3 an toàn!</color>");
        }

        /// <summary>
        /// Kích hoạt Con đẩy tảng đá Pushable_Boulder lăn vào máng Zone3_Seesaw_Cradle
        /// </summary>
        [ContextMenu("Trigger Roll Boulder (Con)")]
        public void TriggerRollBoulder()
        {
            if (currentStage != ElevatorStage.ConAtZone3_NeedsBoulder || IsMoving) return;
            if (IsNetworkActive)
            {
                RequestRollBoulderServerRpc();
            }
            else
            {
                if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
                activeMotionRoutine = StartCoroutine(RollBoulderRoutine());
            }
        }

        private IEnumerator RollBoulderRoutine()
        {
            currentStage = ElevatorStage.BoulderMoving;
            Debug.Log("<color=yellow>[CraneElevator] Con dùng hết sức đẩy tảng đá lăn vào bập bênh làm đối trọng!</color>");

            Vector3 startPos = pushableBoulder != null ? pushableBoulder.position : new Vector3(-3.5f, 15.4f, -54f);
            Vector3 endPos = cradleTargetWorldPos;
            Quaternion beamStartRot = seesawBeam != null ? seesawBeam.localRotation : Quaternion.identity;
            Quaternion beamEndRot = seesawInitialRot * Quaternion.Euler(6.5f, 0f, 0f);

            float duration = 1.4f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (pushableBoulder != null)
                {
                    // Lăn đá theo đường cong nhẹ
                    Vector3 currentPos = Vector3.Lerp(startPos, endPos, smoothT);
                    currentPos.y += Mathf.Sin(smoothT * Mathf.PI) * 0.25f;
                    pushableBoulder.position = currentPos;

                    // Xoay lăn khối đá
                    pushableBoulder.Rotate(Vector3.right, 360f * Time.deltaTime * 1.5f, Space.World);
                }

                // Khi đá lăn gần đến máng (t > 0.6), bập bênh nghiêng đè xuống
                if (seesawBeam != null && t > 0.4f)
                {
                    float beamT = Mathf.Clamp01((t - 0.4f) / 0.6f);
                    seesawBeam.localRotation = Quaternion.Slerp(beamStartRot, beamEndRot, Mathf.SmoothStep(0f, 1f, beamT));
                }

                yield return null;
            }

            if (pushableBoulder != null) pushableBoulder.position = endPos;
            if (seesawBeam != null) seesawBeam.localRotation = beamEndRot;

            // Mở chốt sơ cấp: chốt nâng lên nhẹ, chuyển sang màu Vàng
            if (winchLockPin != null)
            {
                winchLockPin.localPosition = pinInitialPos + new Vector3(0f, 0.08f, 0f);
                SetLockPinMaterial(matYellowIndicator);
            }

            currentStage = ElevatorStage.BoulderPlaced_WaitingDad;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BoulderPlaced_WaitingDad;
            Debug.Log("<color=cyan>[CraneElevator] RẦM! Tảng đá đã lọt vào máng bập bênh, chốt sơ cấp bật mở (Đèn Vàng)! Hãy báo hiệu cho Cha bên Cần cẩu vặn nhả bánh răng để mở khóa 100%!</color>");
        }

        /// <summary>
        /// Kích hoạt Cha vặn bánh răng Cần cẩu để nhả phanh cáp giải phóng khóa tời Zone 3
        /// </summary>
        [ContextMenu("Trigger Dad Release Clutch (Cha)")]
        public void TriggerDadReleaseClutch()
        {
            if (currentStage != ElevatorStage.BoulderPlaced_WaitingDad || IsMoving) return;
            if (IsNetworkActive)
            {
                RequestDadReleaseClutchServerRpc();
            }
            else
            {
                if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
                activeMotionRoutine = StartCoroutine(DadReleaseClutchRoutine());
            }
        }

        private IEnumerator DadReleaseClutchRoutine()
        {
            Debug.Log("<color=yellow>[CraneElevator] Cha dùng lực vặn bánh răng cần cẩu nhả chốt hãm cáp xuyên hẻm vực!</color>");

            float duration = 0.9f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                if (cogwheel != null)
                {
                    cogwheel.Rotate(Vector3.forward, 360f * Time.deltaTime * 2.5f, Space.Self);
                }
                if (leverHandle != null)
                {
                    leverHandle.Rotate(Vector3.right, Mathf.Sin(elapsed * 20f) * 2f, Space.Self);
                }

                yield return null;
            }

            // Hoàn tất mở khóa: chốt bung lên, đèn chuyển sang Xanh lục!
            if (winchLockClamp != null)
            {
                winchLockClamp.localPosition = clampInitialPos + new Vector3(0f, 0.16f, 0f);
            }
            if (winchLockPin != null)
            {
                winchLockPin.localPosition = pinInitialPos + new Vector3(0f, 0.22f, 0f);
                SetLockPinMaterial(matGreenIndicator);
            }

            currentStage = ElevatorStage.BothUnlocked_ReadyReturn;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BothUnlocked_ReadyReturn;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BothUnlocked_ReadyReturn;
            Debug.Log("<color=green>[CraneElevator] TÁCH! Khóa hãm đã bung ra hoàn toàn (Đèn Xanh Lục)! Cần tời Zone 3 đã sẵn sàng. Con hãy kéo cần tời đón Cha!</color>");
        }

        /// <summary>
        /// Kích hoạt lượt 2: Con ở Zone 3 quay tời -> Thang máy chạy ngược về Trụ Islet đón Cha!
        /// </summary>
        [ContextMenu("Trigger Return To Islet (Send Elevator Back)")]
        public void TriggerReturnToIslet()
        {
            if (currentStage != ElevatorStage.BothUnlocked_ReadyReturn || IsMoving) return;
            if (IsNetworkActive)
            {
                RequestReturnToIsletServerRpc();
            }
            else
            {
                if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
                activeMotionRoutine = StartCoroutine(ReturnToIsletRoutine());
            }
        }

        private IEnumerator ReturnToIsletRoutine()
        {
            currentStage = ElevatorStage.ReturningToIslet;
            Debug.Log("<color=yellow>[CraneElevator] LƯỢT 2: Con quay cần tời Zone 3, kéo thang máy quay về Trụ Islet đón Cha!</color>");

            // 0. Xoay cần tời Zone 3
            if (zone3LeverHandle != null)
            {
                float leverElapsed = 0f;
                Quaternion startRot = zone3LeverHandle.localRotation;
                Quaternion endRot = zone3LeverInitialRot * Quaternion.Euler(35f, 0f, 0f);
                while (leverElapsed < 0.25f)
                {
                    leverElapsed += Time.deltaTime;
                    zone3LeverHandle.localRotation = Quaternion.Slerp(startRot, endRot, leverElapsed / 0.25f);
                    yield return null;
                }
            }

            // 1. Thang máy trượt ngược từ Zone 3 về Islet + Quả cầu đá được kéo nhẹ nhàng lên lại
            Vector3 liftStart = liftHighPosition;
            Vector3 liftEnd = liftLowPosition;

            Vector3 ballLocalStart = stoneBall != null ? stoneBall.localPosition : Vector3.zero;
            Vector3 ballLocalEnd = ballLocalStart;
            ballLocalEnd.y = ballHighLocalY;

            float elapsed = 0f;
            while (elapsed < liftTravelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / liftTravelDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (liftPlatform != null)
                {
                    liftPlatform.position = Vector3.Lerp(liftStart, liftEnd, smoothT);
                }

                // Bóng đá nâng dần lên
                if (stoneBall != null)
                {
                    stoneBall.localPosition = Vector3.Lerp(ballLocalStart, ballLocalEnd, smoothT);
                }

                // Trống tời Zone 3 và Bánh răng cần cẩu quay ngược
                if (zone3WinchDrum != null)
                {
                    zone3WinchDrum.Rotate(Vector3.up, 360f * Time.deltaTime * 2f, Space.Self);
                }
                if (cogwheel != null)
                {
                    cogwheel.Rotate(Vector3.forward, -360f * Time.deltaTime * 1.5f, Space.Self);
                }

                // Co lại xích
                if (stoneBallChain != null && stoneBall != null)
                {
                    float currentDrop = ballLocalEnd.y - stoneBall.localPosition.y;
                    Vector3 s = chainInitialScale;
                    s.y = chainInitialScale.y + (currentDrop * 0.25f);
                    stoneBallChain.localScale = s;
                }

                // Đĩa đỏ nảy trở lại khi bóng nhấc lên
                if (pressurePlatePad != null && smoothT > 0.1f)
                {
                    Vector3 lp = pressurePlatePad.localPosition;
                    lp.y = padNormalLocalY;
                    pressurePlatePad.localPosition = lp;
                }

                UpdateCables();
                yield return null;
            }

            if (liftPlatform != null) liftPlatform.position = liftEnd;
            if (stoneBall != null) stoneBall.localPosition = ballLocalEnd;

            // Đặt lại góc cần tời Zone 3
            if (zone3LeverHandle != null)
            {
                zone3LeverHandle.localRotation = zone3LeverInitialRot;
            }

            currentStage = ElevatorStage.ThangAtIslet_ReadyDad;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.ThangAtIslet_ReadyDad;
            UpdateCables();

            Debug.Log("<color=green>[CraneElevator] Thang máy đã cập bến Trụ Islet! Cha hãy bước lên thang và kích hoạt để sang Zone 3!</color>");
        }

        /// <summary>
        /// Kích hoạt lượt 3: Cha đã lên thang máy -> Đưa Cha sang Zone 3 đoàn tụ cùng Con!
        /// </summary>
        [ContextMenu("Trigger Second Ride (Send Dad)")]
        public void TriggerSecondRide()
        {
            if (currentStage != ElevatorStage.ThangAtIslet_ReadyDad || IsMoving) return;
            if (IsNetworkActive)
            {
                RequestSecondRideServerRpc();
            }
            else
            {
                if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
                activeMotionRoutine = StartCoroutine(SecondRideRoutine());
            }
        }

        private IEnumerator SecondRideRoutine()
        {
            currentStage = ElevatorStage.MovingToZone3_Second;
            Debug.Log("<color=yellow>[CraneElevator] LƯỢT 3: Cha đã lên thang! Cần tời kéo thang đưa Cha sang Zone 3 đoàn tụ cùng Con!</color>");

            // 0. Xoay cần tời Zone 3
            if (zone3LeverHandle != null)
            {
                float leverElapsed = 0f;
                Quaternion startRot = zone3LeverHandle.localRotation;
                Quaternion endRot = zone3LeverInitialRot * Quaternion.Euler(45f, 0f, 0f);
                while (leverElapsed < 0.25f)
                {
                    leverElapsed += Time.deltaTime;
                    zone3LeverHandle.localRotation = Quaternion.Slerp(startRot, endRot, leverElapsed / 0.25f);
                    yield return null;
                }
            }

            // 1. Quả cầu đá rơi lại xuống đĩa đỏ
            Vector3 ballLocalStart = stoneBall != null ? stoneBall.localPosition : Vector3.zero;
            Vector3 ballLocalEnd = ballLocalStart;
            ballLocalEnd.y = ballLandedLocalY;

            float elapsed = 0f;
            while (elapsed < ballDropDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / ballDropDuration);
                float curvedT = t * t;

                if (stoneBall != null)
                {
                    stoneBall.localPosition = Vector3.Lerp(ballLocalStart, ballLocalEnd, curvedT);
                }

                if (cogwheel != null)
                {
                    cogwheel.Rotate(Vector3.forward, 360f * Time.deltaTime * (1f + curvedT * 2f), Space.Self);
                }

                UpdateCables();
                yield return null;
            }

            if (stoneBall != null) stoneBall.localPosition = ballLocalEnd;
            if (pressurePlatePad != null)
            {
                Vector3 lp = pressurePlatePad.localPosition;
                lp.y = padPressedLocalY;
                pressurePlatePad.localPosition = lp;
            }

            yield return new WaitForSeconds(0.2f);

            // 2. Thang máy đưa Cha sang Zone 3
            Vector3 liftStart = liftLowPosition;
            Vector3 liftEnd = liftHighPosition;

            elapsed = 0f;
            while (elapsed < liftTravelDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / liftTravelDuration);
                float smoothT = Mathf.SmoothStep(0f, 1f, t);

                if (liftPlatform != null)
                {
                    liftPlatform.position = Vector3.Lerp(liftStart, liftEnd, smoothT);
                }

                UpdateCables();
                yield return null;
            }

            if (liftPlatform != null) liftPlatform.position = liftEnd;

            currentStage = ElevatorStage.BothCompleted;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BothCompleted;
            UpdateCables();

            Debug.Log("<color=green>[CraneElevator] THÀNH CÔNG RỰC RỠ! Cả hai Cha Con đã đoàn tụ trên Cao Nguyên Zone 3!</color>");
        }

        /// <summary>
        /// Cập nhật vị trí các sợi dây cáp ray dẫn hướng đôi (LineRenderer) nối từ trụ tháp Islet sang Zone 3.
        /// </summary>
        public void UpdateCables()
        {
            Vector3 isletLeft = new Vector3(-2.20f, 12.60f, -23.20f);
            Vector3 isletRight = new Vector3(0.20f, 12.60f, -23.20f);
            Vector3 zone3Left = new Vector3(-2.20f, 16.40f, -46.80f);
            Vector3 zone3Right = new Vector3(0.20f, 16.40f, -46.80f);

            if (guideCableLeft != null)
            {
                guideCableLeft.positionCount = 2;
                guideCableLeft.SetPosition(0, isletLeft);
                guideCableLeft.SetPosition(1, zone3Left);
            }

            if (guideCableRight != null)
            {
                guideCableRight.positionCount = 2;
                guideCableRight.SetPosition(0, isletRight);
                guideCableRight.SetPosition(1, zone3Right);
            }

            UpdateCraneWinchRope();
        }

        public void UpdateCraneWinchRope()
        {
            if (craneWinchRope != null)
            {
                craneWinchRope.positionCount = 4;
                Vector3 pDrum = (winchDrumTransform != null)
                    ? winchDrumTransform.position + new Vector3(0f, 0.22f, 0.10f)
                    : new Vector3(-10.76f, 10.00f, -13.52f);

                Vector3 pTopPulley = new Vector3(-10.50f, 18.40f, -13.00f);
                Vector3 pTipPulley = new Vector3(-6.80f, 18.25f, -13.00f);
                Vector3 pBall = new Vector3(-6.80f, 17.50f, -13.00f);
                if (stoneBall != null)
                {
                    float ballCenterY = 13.20f + stoneBall.localPosition.y;
                    pBall = new Vector3(-6.80f, ballCenterY + 1.0f, -13.00f);
                }

                craneWinchRope.SetPosition(0, pDrum);
                craneWinchRope.SetPosition(1, pTopPulley);
                craneWinchRope.SetPosition(2, pTipPulley);
                craneWinchRope.SetPosition(3, pBall);
            }
        }

        public void OpenWirePuzzle()
        {
            isWirePuzzleOpen = true;
            wirePuzzleMessage = "";
            draggingWire = -1;
            hoveredRightSlot = -1;
            sparkTimer = 0f;
            buzzTimer = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Debug.Log("<color=cyan>[CraneElevator] Con đã mở bảng điều khiển mạch điện Zone 3 (Cơ chế Kéo Thả Dây)!</color>");
        }

        public void CloseWirePuzzle()
        {
            isWirePuzzleOpen = false;
            draggingWire = -1;
            hoveredRightSlot = -1;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            if (isWirePuzzleOpen)
            {
                CloseWirePuzzle();
            }
        }

        private void HandleWirePuzzleKeyboardInput()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) ConnectWireDirectly(0);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) ConnectWireDirectly(1);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) ConnectWireDirectly(2);
                if (Keyboard.current.digit4Key.wasPressedThisFrame) ConnectWireDirectly(3);
                if (Keyboard.current.escapeKey.wasPressedThisFrame) CloseWirePuzzle();
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Alpha1)) ConnectWireDirectly(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) ConnectWireDirectly(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) ConnectWireDirectly(2);
            if (Input.GetKeyDown(KeyCode.Alpha4)) ConnectWireDirectly(3);
            if (Input.GetKeyDown(KeyCode.Escape)) CloseWirePuzzle();
#endif
        }

        private void ConnectWireDirectly(int wireIdx)
        {
            if (wireIdx < 0 || wireIdx >= 4) return;
            wireConnected[wireIdx] = true;
            string[] names = { "ĐỎ", "XANH", "VÀNG", "HỒNG" };
            wirePuzzleMessage = $"<color=#55FF55>⚡ CẠCH! Đã cắm chốt dây {names[wireIdx]}!</color>";
            CheckAllWiresConnected();
        }

        private void CheckAllWiresConnected()
        {
            for (int i = 0; i < 4; i++)
            {
                if (!wireConnected[i]) return;
            }

            wirePuzzleMessage = "<color=#33FF55>🎉 TOÀN BỘ MẠCH ĐIỆN ĐÃ ĐƯỢC KẾT NỐI! NGUỒN CẦN TỜI ĐÃ PHỤC HỒI! 🎉</color>";
            if (IsNetworkActive)
            {
                CompleteWirePuzzleServerRpc();
            }
            else
            {
                if (activeMotionRoutine != null) StopCoroutine(activeMotionRoutine);
                activeMotionRoutine = StartCoroutine(WirePuzzleSuccessRoutine());
            }
        }

        private IEnumerator WirePuzzleSuccessRoutine()
        {
            yield return new WaitForSeconds(0.8f);
            CloseWirePuzzle();

            // 1. Cất các sợi dây điện lòi ra vào bên trong tủ
            if (zone3ElectricalBox != null)
            {
                foreach (Transform child in zone3ElectricalBox)
                {
                    if (child.name.StartsWith("Wire_"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            // 2. Đóng êm ái cánh cửa tủ điện Cabinet_Door
            if (doorHingeTransform != null)
            {
                Quaternion startRot = doorHingeTransform.localRotation;
                Quaternion endRot = Quaternion.identity;
                float closeElapsed = 0f;
                float closeDuration = 0.5f;

                while (closeElapsed < closeDuration)
                {
                    closeElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(closeElapsed / closeDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);
                    doorHingeTransform.localRotation = Quaternion.Slerp(startRot, endRot, smoothT);
                    yield return null;
                }
                doorHingeTransform.localRotation = endRot;
            }

            // 3. Đổi đèn LED sang xanh lá
            if (boxStatusLedRenderer != null && matGreenIndicator != null)
            {
                boxStatusLedRenderer.sharedMaterial = matGreenIndicator;
            }

            // 4. Bật mở chốt khóa tời
            if (winchLockClamp != null)
            {
                winchLockClamp.localPosition = clampInitialPos + new Vector3(0f, 0.25f, 0f);
            }
            if (winchLockPin != null)
            {
                winchLockPin.localPosition = pinInitialPos + new Vector3(0f, 0.35f, 0f);
                SetLockPinMaterial(matGreenIndicator);
            }

            currentStage = ElevatorStage.BothUnlocked_ReadyReturn;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BothUnlocked_ReadyReturn;
            if (IsNetworkActive && IsServer) netStage.Value = ElevatorStage.BothUnlocked_ReadyReturn;
            Debug.Log("<color=green>[CraneElevator] Cạch! Cửa tủ điện đã đóng kín, mạch điện đã thông! Con hãy gạt cần tời để gọi thang về đón Ba!</color>");
        }

        // ==========================================
        // GOOSE GOOSE DUCK / AMONG US WIRE MINIGAME
        // ==========================================
        private static void DrawThickLine(Vector2 pointA, Vector2 pointB, Color color, float width)
        {
            Vector2 d = pointB - pointA;
            float length = d.magnitude;
            if (length < 0.5f) return;

            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, pointA);
            Color oldCol = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(pointA.x, pointA.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.color = oldCol;
            GUIUtility.RotateAroundPivot(-angle, pointA);
        }

        private static Vector2 GetCubicBezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float u = 1f - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0;
            p += 3f * uu * t * p1;
            p += 3f * u * tt * p2;
            p += ttt * p3;
            return p;
        }

        private void DrawBezierWire(Vector2 startPos, Vector2 endPos, Color wireColor, bool isDragging)
        {
            float dx = Mathf.Max(25f, Mathf.Abs(endPos.x - startPos.x) * 0.45f);
            float sag = Mathf.Clamp(Mathf.Abs(endPos.x - startPos.x) * 0.08f, 4f, 24f);
            Vector2 p1 = new Vector2(startPos.x + dx, startPos.y + sag * 0.5f);
            Vector2 p2 = new Vector2(endPos.x - dx, endPos.y + sag * 0.5f);

            int segments = 16;
            Vector2 prevPoint = startPos;

            for (int s = 1; s <= segments; s++)
            {
                float t = s / (float)segments;
                Vector2 currentPoint = GetCubicBezierPoint(startPos, p1, p2, endPos, t);

                // Lớp vỏ bọc cao su dày bên ngoài
                DrawThickLine(prevPoint, currentPoint, new Color(0.08f, 0.08f, 0.11f, 0.96f), 15f);
                // Lớp màu dây cao su chính
                DrawThickLine(prevPoint, currentPoint, wireColor, 10f);
                // Vệt sáng 3D bóng trên thân dây
                Color highlight = Color.Lerp(wireColor, Color.white, 0.42f);
                DrawThickLine(prevPoint, currentPoint, highlight, 3.2f);

                prevPoint = currentPoint;
            }

            // Vẽ đầu cắm kim loại (Male Connector Plug)
            DrawConnectorPlug(endPos, wireColor, isDragging);
        }

        private void DrawConnectorPlug(Vector2 tipPos, Color wireColor, bool isDragging)
        {
            Color oldCol = GUI.color;
            float plugW = 22f;
            float plugH = 15f;

            // Vỏ cắm kim loại xám công nghiệp
            Rect plugRect = new Rect(tipPos.x - plugW * 0.5f, tipPos.y - plugH * 0.5f, plugW, plugH);
            GUI.color = new Color(0.28f, 0.32f, 0.38f, 1f);
            GUI.DrawTexture(plugRect, Texture2D.whiteTexture);

            // Vành kim loại sáng
            GUI.color = new Color(0.50f, 0.55f, 0.65f, 1f);
            GUI.DrawTexture(new Rect(plugRect.x, plugRect.y, 3f, plugH), Texture2D.whiteTexture);

            // Vạch sơn màu nhận diện trên đầu cắm
            GUI.color = wireColor;
            GUI.DrawTexture(new Rect(tipPos.x - 3f, tipPos.y - plugH * 0.5f, 7f, plugH), Texture2D.whiteTexture);

            // Chấu cắm đồng nhô ra phía trước
            GUI.color = new Color(1f, 0.78f, 0.25f, 1f);
            GUI.DrawTexture(new Rect(tipPos.x + plugW * 0.5f, tipPos.y - 3.5f, 7f, 7f), Texture2D.whiteTexture);

            GUI.color = oldCol;
        }

        private void DrawSocketBlock(Rect rect, Color wireColor, string name, bool isLeft, bool isConnected, bool isHovered)
        {
            Color oldCol = GUI.color;

            // Khối nền cọc kim loại
            GUI.color = isHovered ? new Color(0.32f, 0.42f, 0.52f, 1f) : new Color(0.16f, 0.18f, 0.22f, 1f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);

            // Viền sáng nếu đang hover
            if (isHovered)
            {
                GUI.color = new Color(0.3f, 0.9f, 1f, 0.9f);
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y + rect.height, rect.width + 4f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y, 2f, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x + rect.width, rect.y, 2f, rect.height), Texture2D.whiteTexture);
            }

            // Dải màu nhận diện của cọc
            float stripeW = 9f;
            float stripeX = isLeft ? rect.x : (rect.x + rect.width - stripeW);
            GUI.color = wireColor;
            GUI.DrawTexture(new Rect(stripeX, rect.y, stripeW, rect.height), Texture2D.whiteTexture);

            // Đèn LED trạng thái trên cọc (Đặc biệt cho cọc B bên phải)
            if (!isLeft)
            {
                float ledSize = 11f;
                float ledX = rect.x + 12f;
                float ledY = rect.y + (rect.height - ledSize) * 0.5f;

                GUI.color = isConnected ? new Color(0.2f, 1f, 0.35f, 1f) : new Color(0.35f, 0.10f, 0.10f, 1f);
                GUI.DrawTexture(new Rect(ledX, ledY, ledSize, ledSize), Texture2D.whiteTexture);

                if (isConnected)
                {
                    // Hào quang LED xanh phát sáng
                    GUI.color = new Color(0.2f, 1f, 0.35f, 0.38f);
                    GUI.DrawTexture(new Rect(ledX - 4f, ledY - 4f, ledSize + 8f, ledSize + 8f), Texture2D.whiteTexture);
                }
            }

            // Tên màu cọc
            GUI.color = oldCol;
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = isLeft ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight,
                normal = { textColor = isConnected ? wireColor : Color.white }
            };
            float textOffset = isLeft ? 10f : -18f;
            Rect labelRect = new Rect(rect.x + textOffset, rect.y, rect.width - 20f, rect.height);
            GUI.Label(labelRect, name, labelStyle);

            // Lỗ cắm chốt kim loại (Port)
            float portSize = 16f;
            float portX = isLeft ? (rect.x + rect.width - portSize * 0.5f) : (rect.x - portSize * 0.5f);
            float portY = rect.y + (rect.height - portSize) * 0.5f;

            GUI.color = new Color(0.06f, 0.07f, 0.09f, 1f);
            GUI.DrawTexture(new Rect(portX, portY, portSize, portSize), Texture2D.whiteTexture);

            GUI.color = new Color(0.40f, 0.44f, 0.50f, 1f);
            GUI.DrawTexture(new Rect(portX + 3f, portY + 3f, portSize - 6f, portSize - 6f), Texture2D.whiteTexture);

            GUI.color = oldCol;
        }

        private void DrawElectricSparkEffect(Vector2 center)
        {
            Color oldCol = GUI.color;
            float sparkRadius = 26f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f * Mathf.Deg2Rad + (Time.time * 24f);
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 p1 = center + dir * 6f;
                Vector2 p2 = center + dir * (sparkRadius + Mathf.Sin(Time.time * 35f + i) * 7f);
                DrawThickLine(p1, p2, new Color(1f, 0.95f, 0.35f, 0.95f), 2.5f);
            }

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(center.x - 7f, center.y - 7f, 14f, 14f), Texture2D.whiteTexture);
            GUI.color = oldCol;
        }

        private void DrawWireConnectingPuzzleGUI()
        {
            if (!isWirePuzzleOpen) return;

            Event e = Event.current;

            if (sparkTimer > 0f) sparkTimer -= Time.deltaTime;
            if (buzzTimer > 0f) buzzTimer -= Time.deltaTime;

            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float panelW = 760f;
            float panelH = 510f;
            float startX = (Screen.width - panelW) / 2f;
            float startY = (Screen.height - panelH) / 2f;

            // Khung viền kim loại tối kiểu Goose Goose Duck / Among Us
            GUI.color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
            GUI.DrawTexture(new Rect(startX - 6f, startY - 6f, panelW + 12f, panelH + 12f), Texture2D.whiteTexture);

            GUI.color = buzzTimer > 0f ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(0.24f, 0.28f, 0.36f, 1f);
            GUI.DrawTexture(new Rect(startX - 2f, startY - 2f, panelW + 4f, panelH + 4f), Texture2D.whiteTexture);

            GUI.color = new Color(0.08f, 0.09f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(startX, startY, panelW, panelH), Texture2D.whiteTexture);
            GUI.color = prevColor;

            // Đường mạch ngầm bo mạch
            Color traceColor = new Color(0.15f, 0.18f, 0.24f, 0.6f);
            DrawThickLine(new Vector2(startX + 160f, startY + 60f), new Vector2(startX + 160f, startY + panelH - 40f), traceColor, 2f);
            DrawThickLine(new Vector2(startX + panelW - 160f, startY + 60f), new Vector2(startX + panelW - 160f, startY + panelH - 40f), traceColor, 2f);
            DrawThickLine(new Vector2(startX + 160f, startY + 250f), new Vector2(startX + panelW - 160f, startY + 250f), traceColor, 2f);

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.35f, 0.85f, 1f) }
            };
            GUI.Label(new Rect(startX, startY + 12f, panelW, 26f), "⚡ HỆ THỐNG ĐẤU NỐI MẠCH ĐIỆN TỜI // ZONE 3 ⚡", headerStyle);

            int connectedCount = 0;
            for (int k = 0; k < 4; k++) if (wireConnected[k]) connectedCount++;

            GUIStyle tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.75f, 0.80f, 0.88f) }
            };
            GUI.Label(new Rect(startX, startY + 38f, panelW, 22f), "DÙNG CHUỘT NHẤP VÀ KÉO ĐẦU DÂY BÊN A ➔ KÉO DÃN DÂY NỐI VÀO CỌC CÙNG MÀU BÊN B", tipStyle);

            if (GUI.Button(new Rect(startX + panelW - 42f, startY + 10f, 32f, 26f), "✕"))
            {
                CloseWirePuzzle();
                return;
            }

            string[] wireNames = { "ĐỎ", "XANH", "VÀNG", "HỒNG" };
            Color[] wireColors = {
                new Color(1f, 0.22f, 0.18f),   // Đỏ
                new Color(0.12f, 0.52f, 1f),   // Xanh Dương
                new Color(1f, 0.82f, 0.10f),   // Vàng
                new Color(1f, 0.28f, 0.58f)    // Hồng
            };

            float firstY = startY + 85f;
            float spacingY = 92f;
            float socketW = 125f;
            float socketH = 56f;

            float leftBaseX = startX + 35f;
            float leftPortX = leftBaseX + socketW;

            float rightBaseX = startX + panelW - socketW - 35f;
            float rightPortX = rightBaseX;

            Vector2[] leftPorts = new Vector2[4];
            Vector2[] rightPorts = new Vector2[4];

            for (int i = 0; i < 4; i++)
            {
                float py = firstY + i * spacingY + socketH * 0.5f;
                leftPorts[i] = new Vector2(leftPortX, py);
                rightPorts[i] = new Vector2(rightPortX, py);
            }

            Vector2 mousePos = e.mousePosition;

            // Xử lý kéo thả chuột (Drag & Drop)
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (!wireConnected[i])
                    {
                        Rect grabRect = new Rect(leftBaseX, firstY + i * spacingY - 5f, socketW + 45f, socketH + 10f);
                        if (grabRect.Contains(mousePos))
                        {
                            draggingWire = i;
                            dragCurrentPos = mousePos;
                            wirePuzzleMessage = $"Đang kéo dây {wireNames[i]}... Hãy kéo tới cọc cùng màu bên B!";
                            e.Use();
                            break;
                        }
                    }
                }
            }
            else if (e.type == EventType.MouseDrag && draggingWire != -1)
            {
                dragCurrentPos = mousePos;

                // Magnetic Snap khi đưa mút dây gần cọc B bên phải
                hoveredRightSlot = -1;
                for (int r = 0; r < 4; r++)
                {
                    if (Vector2.Distance(dragCurrentPos, rightPorts[r]) <= 42f)
                    {
                        dragCurrentPos = rightPorts[r];
                        hoveredRightSlot = r;
                        break;
                    }
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && e.button == 0 && draggingWire != -1)
            {
                int targetSlot = -1;
                for (int r = 0; r < 4; r++)
                {
                    if (Vector2.Distance(mousePos, rightPorts[r]) <= 48f)
                    {
                        targetSlot = r;
                        break;
                    }
                }

                if (targetSlot != -1)
                {
                    int expectedWire = rightTerminalMapping[targetSlot];
                    if (expectedWire == draggingWire)
                    {
                        wireConnected[draggingWire] = true;
                        sparkPos = rightPorts[targetSlot];
                        sparkTimer = 0.45f;
                        wirePuzzleMessage = $"<color=#55FF55>⚡ CẠCH! Đã cắm chốt dây {wireNames[draggingWire]}!</color>";
                        CheckAllWiresConnected();
                    }
                    else
                    {
                        buzzTimer = 0.4f;
                        wirePuzzleMessage = "<color=#FF4444>❌ ĐOẢN MẠCH! Cắm sai màu cọc điện!</color>";
                    }
                }
                else
                {
                    wirePuzzleMessage = "";
                }

                draggingWire = -1;
                hoveredRightSlot = -1;
                e.Use();
            }

            GUIStyle colTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };
            GUI.Label(new Rect(leftBaseX, firstY - 26f, socketW, 20f), "CỌC PHÁT (A)", colTagStyle);
            GUI.Label(new Rect(rightBaseX, firstY - 26f, socketW, 20f), "CỌC NHẬN (B)", colTagStyle);

            for (int i = 0; i < 4; i++)
            {
                float sy = firstY + i * spacingY;
                Rect blockRect = new Rect(leftBaseX, sy, socketW, socketH);
                DrawSocketBlock(blockRect, wireColors[i], wireNames[i], isLeft: true, isConnected: wireConnected[i], isHovered: (draggingWire == i));
            }

            for (int r = 0; r < 4; r++)
            {
                float sy = firstY + r * spacingY;
                Rect blockRect = new Rect(rightBaseX, sy, socketW, socketH);
                int wireIdx = rightTerminalMapping[r];
                bool isConn = wireConnected[wireIdx];
                bool isHover = (hoveredRightSlot == r);
                DrawSocketBlock(blockRect, wireColors[wireIdx], wireNames[wireIdx], isLeft: false, isConnected: isConn, isHovered: isHover);
            }

            for (int i = 0; i < 4; i++)
            {
                if (wireConnected[i])
                {
                    int rSlot = -1;
                    for (int k = 0; k < 4; k++)
                    {
                        if (rightTerminalMapping[k] == i) { rSlot = k; break; }
                    }
                    if (rSlot != -1)
                    {
                        DrawBezierWire(leftPorts[i], rightPorts[rSlot], wireColors[i], isDragging: false);
                    }
                }
                else if (draggingWire == i)
                {
                    DrawBezierWire(leftPorts[i], dragCurrentPos, wireColors[i], isDragging: true);
                }
                else
                {
                    Vector2 stubTip = leftPorts[i] + new Vector2(32f, 0f);
                    DrawBezierWire(leftPorts[i], stubTip, wireColors[i], isDragging: false);
                }
            }

            if (sparkTimer > 0f)
            {
                DrawElectricSparkEffect(sparkPos);
            }

            float statusY = startY + panelH - 52f;
            GUIStyle statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            if (connectedCount >= 4)
            {
                statusStyle.normal.textColor = new Color(0.25f, 1f, 0.45f);
                GUI.Label(new Rect(startX, statusY, panelW, 26f), "⚡ HOÀN THÀNH TOÀN BỘ MẠCH ĐIỆN! NGUỒN ĐIỆN MÁY TỜI ĐÃ PHỤC HỒI! ⚡", statusStyle);
            }
            else if (!string.IsNullOrEmpty(wirePuzzleMessage))
            {
                GUI.Label(new Rect(startX, statusY, panelW, 26f), wirePuzzleMessage, statusStyle);
            }
            else
            {
                statusStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(startX, statusY, panelW, 26f), $"Tiến độ: {connectedCount}/4 cọc dây đã kết nối", statusStyle);
            }

            if (GUI.Button(new Rect(startX + 35f, startY + panelH - 36f, 160f, 26f), "⚡ Nối Tự Động (Skip)"))
            {
                for (int k = 0; k < 4; k++) wireConnected[k] = true;
                CheckAllWiresConnected();
            }
        }

        private void OnGUI()
        {
            if (isWirePuzzleOpen)
            {
                DrawWireConnectingPuzzleGUI();
            }

            if (!string.IsNullOrEmpty(currentPrompt) && !isWirePuzzleOpen)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.fontSize = 20;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.yellow;
                style.alignment = TextAnchor.MiddleCenter;

                float width = 580f;
                float height = 55f;
                float x = (Screen.width - width) / 2f;
                float y = Screen.height - 130f;

                GUI.Box(new Rect(x, y, width, height), currentPrompt, style);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 targetP = leverHandle != null ? leverHandle.position : interactPoint;
            Gizmos.DrawWireSphere(targetP, interactDistance);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(liftLowPosition, new Vector3(2.8f, 0.3f, 2.8f));
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(liftHighPosition, new Vector3(2.8f, 0.3f, 2.8f));
            Gizmos.DrawLine(liftLowPosition, liftHighPosition);
        }
    }

    /// <summary>
    /// Trigger gắn trên sàn thang máy để di chuyển người chơi theo sàn thang máy.
    /// HOÀN TOÀN KHÔNG DÙNG SetParent() để tương thích 100% với Unity Netcode for GameObjects (tránh lỗi Invalid parenting).
    /// </summary>
    public class ElevatorPassengerCarrier : MonoBehaviour
    {
        [Tooltip("Transform gốc của sàn thang máy để tính delta di chuyển")]
        [SerializeField] private Transform platformRoot;

        private readonly List<CharacterController> passengerCCs = new List<CharacterController>();
        private readonly List<Transform> nonCcPassengers = new List<Transform>();
        private Vector3 lastPlatformPos;

        private void Awake()
        {
            if (platformRoot == null)
            {
                platformRoot = transform.parent != null ? transform.parent : transform;
            }
            lastPlatformPos = platformRoot.position;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Kiểm tra CharacterController (Player Dad/Son)
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                if (!passengerCCs.Contains(cc))
                {
                    passengerCCs.Add(cc);
                    Debug.Log($"<color=cyan>[Elevator] Người chơi '{other.name}' đã bước lên thang máy!</color>");
                }
                return;
            }

            // Nếu nhân vật không có CharacterController (hoặc vật thể khác)
            if (other.CompareTag("Player") || other.name.Contains("Dad") || other.name.Contains("Son"))
            {
                if (!nonCcPassengers.Contains(other.transform))
                {
                    nonCcPassengers.Add(other.transform);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            CharacterController cc = other.GetComponent<CharacterController>();
            if (cc != null && passengerCCs.Contains(cc))
            {
                passengerCCs.Remove(cc);
                Debug.Log($"<color=gray>[Elevator] Người chơi '{other.name}' đã rời khỏi thang máy.</color>");
                return;
            }

            if (nonCcPassengers.Contains(other.transform))
            {
                nonCcPassengers.Remove(other.transform);
            }
        }

        private void FixedUpdate()
        {
            if (platformRoot == null) return;

            Vector3 currentPos = platformRoot.position;
            Vector3 delta = currentPos - lastPlatformPos;

            if (delta.sqrMagnitude > 0.000001f)
            {
                // Di chuyển tất cả CharacterController theo delta sàn
                for (int i = passengerCCs.Count - 1; i >= 0; i--)
                {
                    var cc = passengerCCs[i];
                    if (cc != null && cc.enabled)
                    {
                        cc.Move(delta);
                    }
                    else if (cc == null)
                    {
                        passengerCCs.RemoveAt(i);
                    }
                }

                // Di chuyển các transform khác
                for (int i = nonCcPassengers.Count - 1; i >= 0; i--)
                {
                    var t = nonCcPassengers[i];
                    if (t != null)
                    {
                        t.position += delta;
                    }
                    else
                    {
                        nonCcPassengers.RemoveAt(i);
                    }
                }
            }

            lastPlatformPos = currentPos;
        }
    }
}
