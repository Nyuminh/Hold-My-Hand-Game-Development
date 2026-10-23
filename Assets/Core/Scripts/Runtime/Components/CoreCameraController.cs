using UnityEngine;
using Unity.Netcode;
using Unity.Cinemachine;
using System.Collections.Generic;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Manages the player's camera rig, handling look input and switching between different camera modes.
    /// This component works with Cinemachine to control the active virtual camera by adjusting priorities.
    /// It should be attached to the player prefab and will only activate for the local owner.
    /// </summary>
    public class CoreCameraController : NetworkBehaviour
    {
        #region Fields & Properties

        [Header("Camera Setup")]
        [Tooltip("List of camera mode prefabs to instantiate. Each should have a CoreCameraMode component.")]
        [SerializeField] private List<CoreCameraMode> cameraModePrefabs = new List<CoreCameraMode>();
        [Tooltip("The transform the camera should follow and look at. If null, this component's transform is used.")]
        [SerializeField] private Transform lookTarget;
        [Tooltip("The name of the camera mode to activate on spawn.")]
        [SerializeField] private string initialModeName = "FreeLook";

        [Header("Default Look Settings")]
        [Tooltip("Whether to process look input from the input handler.")]
        [SerializeField] private bool enableLookInput = true;
        [Tooltip("Default sensitivity for look input (used when camera doesn't override).")]
        [SerializeField] private float defaultLookSensitivity = 1.0f;
        [Tooltip("Default maximum angle in degrees the camera can look up or down (used when camera doesn't override).")]
        [SerializeField] private float defaultVerticalLookLimit = 70.0f;

        [Header("Smooth Look & Pitch Clamping")]
        [Tooltip("Góc nhìn ngước lên tối đa (độ âm, camera hạ thấp gần đất)")]
        [SerializeField] private float minVerticalLookAngle = -28.0f;
        [Tooltip("Góc nhìn cúi xuống tối đa (độ dương, camera nâng lên cao)")]
        [SerializeField] private float maxVerticalLookAngle = 48.0f;
        [Tooltip("Thời gian làm mượt chuyển động chuột (Smooth Damp Time, giây)")]
        [SerializeField] private float lookSmoothTime = 0.035f;

        [Header("Third Person Framing & Aim Zoom")]
        [Tooltip("Khoảng cách camera mặc định của Con (mét)")]
        [SerializeField] private float defaultCameraDistance = 3.6f;
        [Tooltip("Khoảng cách camera mặc định riêng của Ba (mét, xa hơn để thấy toàn thân và chân)")]
        [SerializeField] private float dadCameraDistance = 5.2f;
        [Tooltip("Khoảng cách camera khi ngắm / gồng ném OTS (mét)")]
        [SerializeField] private float aimCameraDistance = 2.0f;
        [Tooltip("Độ lệch vai mặc định của Con")]
        [SerializeField] private Vector3 defaultShoulderOffset = new Vector3(0.18f, 0.04f, 0f);
        [Tooltip("Độ lệch vai mặc định của Ba (hạ thấp để góc nhìn thấy được cả chân khi nhìn ngang)")]
        [SerializeField] private Vector3 dadDefaultShoulderOffset = new Vector3(0.18f, -0.22f, 0f);
        [Tooltip("Độ lệch vai phải khi ngắm OTS của Ba (Hạ thấp xuống đúng xương vai, tránh bị cao quá đầu)")]
        [SerializeField] private Vector3 dadAimShoulderOffset = new Vector3(0.68f, -0.16f, 0f);
        [Tooltip("Độ lệch vai phải khi ngắm OTS của Con")]
        [SerializeField] private Vector3 childAimShoulderOffset = new Vector3(0.55f, 0.05f, 0f);
        [Tooltip("Thời gian chuyển mượt mà giữa góc nhìn thường và ngắm OTS (giây)")]
        [SerializeField] private float zoomSmoothTime = 0.18f;

        [Header("Wall Collision Avoidance")]
        [Tooltip("Bật cơ chế chống camera xuyên tường")]
        [SerializeField] private bool enableWallCollision = true;
        [Tooltip("Bán kính sphere cast kiểm tra vật cản tường")]
        [SerializeField] private float collisionSphereRadius = 0.22f;
        [Tooltip("Khoảng cách tối thiểu tới nhân vật khi chạm tường")]
        [SerializeField] private float minCollisionDistance = 0.70f;
        [Tooltip("Layer kiểm tra va chạm tường")]
        [SerializeField] private LayerMask obstacleLayers = ~0;

        [Header("Listening to Events")]
        [Tooltip("Event that provides look input from the CoreInputHandler.")]
        [SerializeField] private Vector2Event onLookInput;

        /// <summary>
        /// Gets the current horizontal rotation angle of the look target.
        /// </summary>
        public float CurrentHorizontalLookAngle => m_CurrentHorizontalLookAngle;

        /// <summary>
        /// Gets the currently active player rotation coupling mode, determined by the active camera mode.
        /// </summary>
        public CoreMovement.CouplingMode CurrentPlayerRotationMode { get; private set; }

        /// <summary>
        /// Gets the currently active camera mode.
        /// </summary>
        public CoreCameraMode ActiveCameraMode { get; private set; }

        /// <summary>
        /// Checks if OTS right-shoulder aim zoom is currently active.
        /// </summary>
        public bool IsAiming => m_IsAiming;

        // Internal state for look angles and sensitivity.
        private float m_TargetVerticalLookAngle;
        private float m_TargetHorizontalLookAngle;
        private float m_CurrentVerticalLookAngle;
        private float m_CurrentHorizontalLookAngle;
        private float m_HorizontalLookVelocity;
        private float m_VerticalLookVelocity;
        private float m_LookSensitivity;
        private float m_VerticalLookLimit;

        // OTS Aim Zoom & Wall Collision state.
        private bool m_IsAiming = false;
        private float m_CurrentCameraDistance = 3.6f;
        private float m_DistanceVelocity;
        private Vector3 m_CurrentShoulderOffset = new Vector3(0.18f, 0.08f, 0f);
        private Vector3 m_ShoulderOffsetVelocity;
        private CinemachineThirdPersonFollow m_ActiveThirdPersonFollow;

        // Runtime references.
        private CinemachineBrain m_CinemachineBrain;
        private readonly List<CoreCameraMode> m_RegisteredCameraModes = new List<CoreCameraMode>();
        // Tracks which camera modes were instantiated by this controller and should be destroyed on despawn.
        private readonly HashSet<CoreCameraMode> m_InstantiatedCameraModes = new HashSet<CoreCameraMode>();

        #endregion

        #region Unity & Network Lifecycle

        private void Awake()
        {
            m_LookSensitivity = defaultLookSensitivity;
            m_VerticalLookLimit = defaultVerticalLookLimit;

            if (lookTarget == null)
            {
                lookTarget = transform;
            }

            if(onLookInput == null && enableLookInput)
            {
                Debug.LogError("[CoreCameraController] Look input event is not assigned.", this);
            }

            // Initialize horizontal angle based on the initial rotation of the player object.
            m_CurrentHorizontalLookAngle = transform.rotation.eulerAngles.y;
            m_TargetHorizontalLookAngle = m_CurrentHorizontalLookAngle;
            m_CurrentVerticalLookAngle = 0f;
            m_TargetVerticalLookAngle = 0f;

            m_CurrentCameraDistance = defaultCameraDistance;
            m_CurrentShoulderOffset = defaultShoulderOffset;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // Tuyệt đối không khởi tạo Cinemachine camera cho Bạn Đồng Hành (Dummy Companion)
            if (GetComponent<SoloCompanionDummy>() != null)
            {
                enabled = false;
                return;
            }

            // Chỉ kích hoạt cho nhân vật PlayerObject thật của LocalClient
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null)
            {
                var localPlayerObj = NetworkManager.Singleton.LocalClient.PlayerObject;
                if (localPlayerObj != null && localPlayerObj.gameObject != this.gameObject)
                {
                    enabled = false;
                    return;
                }
            }

            // This component is only active for the local player who owns this object.
            if (IsOwner)
            {
                SetupCinemachine();
                SwitchCameraMode(initialModeName);
                if (enableLookInput)
                {
                    onLookInput.RegisterListener(SetLookInput);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                // Only destroy camera modes that were instantiated by this controller.
                // External/scene cameras are not destroyed.
                foreach (var mode in m_InstantiatedCameraModes)
                {
                    if (mode != null)
                    {
                        Destroy(mode.gameObject);
                    }
                }
                m_InstantiatedCameraModes.Clear();
                m_RegisteredCameraModes.Clear();

                if (enableLookInput)
                {
                    onLookInput.UnregisterListener(SetLookInput);
                }
            }
            base.OnNetworkDespawn();
        }

        private void LateUpdate()
        {
            // We update the camera rotation in LateUpdate to ensure all character movement for the frame has been processed.
            // This prevents visual jitter.
            if (lookTarget != null && IsOwner && enableLookInput)
            {
                // Smoothly damp horizontal look angle
                m_CurrentHorizontalLookAngle = Mathf.SmoothDampAngle(
                    m_CurrentHorizontalLookAngle,
                    m_TargetHorizontalLookAngle,
                    ref m_HorizontalLookVelocity,
                    lookSmoothTime
                );

                // Smoothly damp vertical look angle
                m_CurrentVerticalLookAngle = Mathf.SmoothDamp(
                    m_CurrentVerticalLookAngle,
                    m_TargetVerticalLookAngle,
                    ref m_VerticalLookVelocity,
                    lookSmoothTime
                );

                Quaternion horizontalRotation = Quaternion.Euler(0f, m_CurrentHorizontalLookAngle, 0f);
                Quaternion verticalRotation = Quaternion.Euler(m_CurrentVerticalLookAngle, 0f, 0f);
                lookTarget.rotation = horizontalRotation * verticalRotation;

                // Smooth OTS Aim Zoom & Wall Collision Avoidance
                UpdateCameraFramingAndCollision();
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Kích hoạt hoặc hủy chế độ phóng to lệch vai phải (Over-The-Shoulder Aim Zoom) khi gồng ném hoặc ngắm.
        /// </summary>
        /// <param name="isAiming">True để zoom sát vai phải, False để trả về góc nhìn mặc định.</param>
        public void SetAimZoom(bool isAiming)
        {
            m_IsAiming = isAiming;
        }

        /// <summary>
        /// Sets the look input, updating the horizontal and vertical look angles.
        /// </summary>
        /// <param name="lookInput">The 2D vector from the input device.</param>
        public void SetLookInput(Vector2 lookInput)
        {
            if (!IsOwner || !enableLookInput) return;

            m_TargetHorizontalLookAngle += lookInput.x * m_LookSensitivity;

            // Clamp vertical angle between min and max bounds to prevent camera clipping through floor or flipping overhead.
            float minLimit = (minVerticalLookAngle < 0f) ? minVerticalLookAngle : -m_VerticalLookLimit;
            float maxLimit = (maxVerticalLookAngle > 0f) ? maxVerticalLookAngle : m_VerticalLookLimit;

            m_TargetVerticalLookAngle = Mathf.Clamp(
                m_TargetVerticalLookAngle - (lookInput.y * m_LookSensitivity),
                minLimit,
                maxLimit
            );
        }

        /// <summary>
        /// Sets the look sensitivity at runtime.
        /// </summary>
        /// <param name="sensitivity">The new sensitivity value.</param>
        public void SetLookSensitivity(float sensitivity)
        {
            m_LookSensitivity = sensitivity;
        }

        /// <summary>
        /// Switches the active camera mode by changing Cinemachine virtual camera priorities.
        /// </summary>
        /// <param name="modeName">The name of the camera mode to activate.</param>
        /// <returns>True if the mode was found and switched successfully, false otherwise.</returns>
        public bool SwitchCameraMode(string modeName)
        {
            CoreCameraMode targetMode = m_RegisteredCameraModes.Find(m => m.ModeName == modeName);
            if (targetMode == null)
            {
                Debug.LogWarning($"Camera mode '{modeName}' not found.");
                return false;
            }

            if (targetMode.CinemachineCamera == null)
            {
                Debug.LogWarning($"CinemachineCamera for mode '{modeName}' is not available.");
                return false;
            }

            // Update the active camera mode reference.
            ActiveCameraMode = targetMode;

            // Update the current player rotation mode.
            CurrentPlayerRotationMode = targetMode.PlayerRotationMode;

            // Apply look settings from the camera mode or use defaults.
            if (targetMode.OverrideLookSettings)
            {
                m_LookSensitivity = targetMode.LookSensitivity;
                m_VerticalLookLimit = targetMode.VerticalLookLimit;
            }
            else
            {
                m_LookSensitivity = defaultLookSensitivity;
                m_VerticalLookLimit = defaultVerticalLookLimit;
            }

            // Update active CinemachineThirdPersonFollow reference
            m_ActiveThirdPersonFollow = targetMode.CinemachineCamera != null 
                ? targetMode.CinemachineCamera.GetComponent<CinemachineThirdPersonFollow>() 
                : null;
            if (m_ActiveThirdPersonFollow != null)
            {
                m_ActiveThirdPersonFollow.CameraDistance = m_CurrentCameraDistance;
                m_ActiveThirdPersonFollow.ShoulderOffset = m_CurrentShoulderOffset;
            }

            // Set priorities: the target camera gets active priority, all others get inactive.
            foreach (var mode in m_RegisteredCameraModes)
            {
                if (mode != null)
                {
                    mode.SetActive(mode == targetMode);
                }
            }

            return true;
        }

        /// <summary>
        /// Registers a camera mode at runtime by instantiating from a prefab.
        /// The instantiated camera will be destroyed when the player despawns.
        /// For scene cameras that should not be destroyed, use RegisterCameraModeInstance instead.
        /// </summary>
        /// <param name="cameraModePrefab">The camera mode prefab to instantiate and register.</param>
        /// <returns>The registered CoreCameraMode instance, or null if registration failed.</returns>
        public CoreCameraMode RegisterCameraMode(CoreCameraMode cameraModePrefab)
        {
            if (cameraModePrefab == null)
            {
                Debug.LogWarning("[CoreCameraController] Cannot register a null camera mode prefab.", this);
                return null;
            }

            // Validate no duplicate mode names exist to prevent ambiguous SwitchCameraMode calls.
            if (m_RegisteredCameraModes.Exists(m => m.ModeName == cameraModePrefab.ModeName))
            {
                Debug.LogWarning($"[CoreCameraController] A camera mode with name '{cameraModePrefab.ModeName}' is already registered. Duplicate names are not allowed.", this);
                return null;
            }

            // Always instantiate the prefab to avoid modifying the original asset.
            CoreCameraMode modeToRegister = Instantiate(cameraModePrefab);

            // Set up targets and add to the registered list.
            modeToRegister.SetTargets(lookTarget);
            m_RegisteredCameraModes.Add(modeToRegister);
            m_InstantiatedCameraModes.Add(modeToRegister);

            // Ensure the newly registered mode is inactive by default.
            modeToRegister.SetActive(false);

            return modeToRegister;
        }

        /// <summary>
        /// Registers an existing in-scene camera mode instance. Use this for cameras already placed in the scene.
        /// </summary>
        /// <param name="cameraModeInstance">The camera mode instance to register.</param>
        /// <returns>True if registration was successful, false otherwise.</returns>
        public bool RegisterCameraModeInstance(CoreCameraMode cameraModeInstance)
        {
            if (cameraModeInstance == null)
            {
                Debug.LogWarning("[CoreCameraController] Cannot register a null camera mode instance.", this);
                return false;
            }

            if (m_RegisteredCameraModes.Contains(cameraModeInstance))
            {
                Debug.LogWarning($"[CoreCameraController] Camera mode instance '{cameraModeInstance.ModeName}' is already registered.", this);
                return false;
            }

            // Validate no duplicate mode names exist to prevent ambiguous SwitchCameraMode calls.
            if (m_RegisteredCameraModes.Exists(m => m.ModeName == cameraModeInstance.ModeName))
            {
                Debug.LogWarning($"[CoreCameraController] A camera mode with name '{cameraModeInstance.ModeName}' is already registered. Duplicate names are not allowed.", this);
                return false;
            }

            // Set up targets and add to the registered list.
            cameraModeInstance.SetTargets(lookTarget);
            m_RegisteredCameraModes.Add(cameraModeInstance);

            // Ensure the newly registered mode is inactive by default.
            cameraModeInstance.SetActive(false);

            return true;
        }

        /// <summary>
        /// Unregisters a camera mode at runtime.
        /// </summary>
        /// <param name="cameraMode">The camera mode to unregister.</param>
        /// <param name="destroy">If true, destroys the camera mode GameObject. If false, only removes from the list.</param>
        /// <returns>True if the mode was found and unregistered, false otherwise.</returns>
        public bool UnregisterCameraMode(CoreCameraMode cameraMode, bool destroy = false)
        {
            if (cameraMode == null)
            {
                Debug.LogWarning("[CoreCameraController] Cannot unregister a null camera mode.", this);
                return false;
            }

            if (!m_RegisteredCameraModes.Contains(cameraMode))
            {
                Debug.LogWarning($"[CoreCameraController] Camera mode '{cameraMode.ModeName}' is not registered.", this);
                return false;
            }

            // If this was the active camera mode, we need to switch to another one.
            if (ActiveCameraMode == cameraMode)
            {
                // Find another mode to switch to before clearing the active mode.
                CoreCameraMode fallbackMode = null;
                foreach (var mode in m_RegisteredCameraModes)
                {
                    if (mode != cameraMode && mode != null)
                    {
                        fallbackMode = mode;
                        break;
                    }
                }

                // Switch to the fallback mode if found, otherwise clear the active mode.
                if (fallbackMode != null)
                {
                    SwitchCameraMode(fallbackMode.ModeName);
                }
                else
                {
                    ActiveCameraMode = null;
                    CurrentPlayerRotationMode = CoreMovement.CouplingMode.Decoupled;
                }
            }

            m_RegisteredCameraModes.Remove(cameraMode);
            m_InstantiatedCameraModes.Remove(cameraMode);

            if (destroy && cameraMode != null)
            {
                Destroy(cameraMode.gameObject);
            }

            return true;
        }

        /// <summary>
        /// Unregisters a camera mode by name.
        /// </summary>
        /// <param name="modeName">The name of the camera mode to unregister.</param>
        /// <param name="destroy">If true, destroys the camera mode GameObject. If false, only removes from the list.</param>
        /// <returns>True if the mode was found and unregistered, false otherwise.</returns>
        public bool UnregisterCameraMode(string modeName, bool destroy = false)
        {
            CoreCameraMode targetMode = m_RegisteredCameraModes.Find(m => m.ModeName == modeName);
            if (targetMode == null)
            {
                Debug.LogWarning($"[CoreCameraController] Camera mode '{modeName}' not found for unregistration.", this);
                return false;
            }

            return UnregisterCameraMode(targetMode, destroy);
        }

        /// <summary>
        /// Gets all currently registered camera modes.
        /// </summary>
        /// <returns>A read-only list of registered camera modes.</returns>
        public IReadOnlyList<CoreCameraMode> GetRegisteredCameraModes()
        {
            return m_RegisteredCameraModes.AsReadOnly();
        }

        /// <summary>
        /// Checks if a camera mode with the specified name is registered.
        /// </summary>
        /// <param name="modeName">The name of the camera mode to check.</param>
        /// <returns>True if registered, false otherwise.</returns>
        public bool IsCameraModeRegistered(string modeName)
        {
            return m_RegisteredCameraModes.Exists(m => m.ModeName == modeName);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the Cinemachine setup by finding the brain, instantiating camera prefabs,
        /// and linking all camera modes to their targets.
        /// </summary>
        private void SetupCinemachine()
        {
            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogError("No main camera found in the scene. [CoreCameraController] requires a main camera.", this);
                return;
            }

            // Ensure a CinemachineBrain exists on the main camera.
            m_CinemachineBrain = mainCam.GetComponent<CinemachineBrain>();
            if (m_CinemachineBrain == null)
            {
                m_CinemachineBrain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            }

            if (cameraModePrefabs.Count == 0)
            {
                Debug.LogError("[CoreCameraController] No camera mode prefabs assigned.", this);
                return;
            }

            // Instantiate each camera mode prefab and set up targets.
            foreach (var prefab in cameraModePrefabs)
            {
                if (prefab == null)
                {
                    Debug.LogWarning("Null camera mode prefab in [CoreCameraController]", this);
                    continue;
                }

                // Validate no duplicate mode names exist to prevent ambiguous SwitchCameraMode calls.
                if (m_RegisteredCameraModes.Exists(m => m.ModeName == prefab.ModeName))
                {
                    Debug.LogWarning($"[CoreCameraController] Duplicate camera mode name '{prefab.ModeName}' found in prefab list. Skipping duplicate.", this);
                    continue;
                }

                CoreCameraMode instance = Instantiate(prefab);
                instance.SetTargets(lookTarget);
                m_RegisteredCameraModes.Add(instance);
                m_InstantiatedCameraModes.Add(instance);
            }
        }

        /// <summary>
        /// Cập nhật khoảng cách camera và độ lệch vai mượt mà, đồng thời kiểm tra tránh xuyên tường bằng SphereCast.
        /// </summary>
        private void UpdateCameraFramingAndCollision()
        {
            if (m_ActiveThirdPersonFollow == null)
            {
                if (ActiveCameraMode != null && ActiveCameraMode.CinemachineCamera != null)
                {
                    m_ActiveThirdPersonFollow = ActiveCameraMode.CinemachineCamera.GetComponent<CinemachineThirdPersonFollow>();
                }
                if (m_ActiveThirdPersonFollow == null) return;
            }

            // Xác định vai trò nhân vật để lấy độ cao vai chuẩn xác (Ba to lớn hơn Con)
            bool isDad = gameObject.name.ToLower().Contains("dad");
            var boostAbility = GetComponent<CoopBoostThrowAbility>();
            if (boostAbility != null) isDad = boostAbility.IsDad;

            Vector3 activeAimShoulder = isDad ? dadAimShoulderOffset : childAimShoulderOffset;
            Vector3 activeDefaultShoulder = isDad ? dadDefaultShoulderOffset : defaultShoulderOffset;
            float baseDistance = isDad ? dadCameraDistance : defaultCameraDistance;

            // Khoảng cách và độ lệch vai mục tiêu tùy thuộc vào trạng thái ngắm OTS
            float targetDistance = m_IsAiming ? aimCameraDistance : baseDistance;
            Vector3 targetShoulderOffset = m_IsAiming ? activeAimShoulder : activeDefaultShoulder;

            // Kiểm tra vật cản (tường, trần, vách đá)
            float safeDistance = targetDistance;
            if (enableWallCollision && lookTarget != null)
            {
                Vector3 targetLookPos = lookTarget.position;
                Vector3 camDirection = -lookTarget.forward;
                Vector3 shoulderWorld = lookTarget.right * targetShoulderOffset.x + lookTarget.up * targetShoulderOffset.y;
                Vector3 rayOrigin = targetLookPos + shoulderWorld * 0.5f;

                LayerMask mask = obstacleLayers & ~(LayerMask.GetMask("Player", "Ignore Raycast"));

                if (Physics.SphereCast(rayOrigin, collisionSphereRadius, camDirection, out RaycastHit hit, targetDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    if (!hit.transform.IsChildOf(transform) && !transform.IsChildOf(hit.transform))
                    {
                        safeDistance = Mathf.Clamp(hit.distance - 0.12f, minCollisionDistance, targetDistance);
                    }
                }
            }

            // Smooth Damp Camera Distance
            m_CurrentCameraDistance = Mathf.SmoothDamp(
                m_CurrentCameraDistance,
                safeDistance,
                ref m_DistanceVelocity,
                zoomSmoothTime
            );
            m_ActiveThirdPersonFollow.CameraDistance = m_CurrentCameraDistance;

            // Smooth Damp Shoulder Offset
            m_CurrentShoulderOffset = Vector3.SmoothDamp(
                m_CurrentShoulderOffset,
                targetShoulderOffset,
                ref m_ShoulderOffsetVelocity,
                zoomSmoothTime
            );
            m_ActiveThirdPersonFollow.ShoulderOffset = m_CurrentShoulderOffset;
        }

        #endregion
    }
}
