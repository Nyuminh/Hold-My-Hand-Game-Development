using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>Client-local introduction. Never changes network ownership or session time scale.</summary>
    public sealed class TutorialIntroCutscene : MonoBehaviour
    {
        [SerializeField] private Transform[] flyoverWaypoints;
        [SerializeField] private Transform[] lakeWaypoints;
        [SerializeField] private Vector3 courseFocus = new Vector3(0, 1, 55);
        [SerializeField] private Vector3 lakeFocus = new Vector3(0, 3.5f, 133);
        [SerializeField] private float returnBlendDuration = 0.8f;

        public bool IsPlaying { get; private set; }
        private static TutorialIntroCutscene s_Active;
        public static bool IsAnyPlaying => s_Active != null && s_Active.IsPlaying;
        private bool m_HasPlayed;
        private bool m_Returning;
        private bool m_SkipRequested;
        private bool m_PlayerLocked;
        private NetworkObject m_Player;
        private CoreMovement m_Movement;
        private CorePlayerManager m_PlayerManager;
        private CoreCameraController m_PlayerCamera;
        private bool m_MovementWasEnabled;
        private bool m_InputWasEnabled;
        private bool m_Panorama;
        private string m_Title;
        private Vector3 m_PanoramaFrom, m_PanoramaTo;
        private CinemachineCamera m_Camera;
        private CinemachineBrain m_Brain;
        private CinemachineBlendDefinition m_PreviousBlend;
        private bool m_PreviousIgnoreTimeScale;
        private UIDocument m_Hud;
        private UIDocument m_Overlay;
        private readonly Dictionary<VisualElement, StyleEnum<DisplayStyle>> m_HiddenHuds = new Dictionary<VisualElement, StyleEnum<DisplayStyle>>();
        private readonly Dictionary<Behaviour, bool> m_DisabledAbilities = new Dictionary<Behaviour, bool>();

        public void Play(NetworkObject player, bool multiplayer, GameObject companion, UIDocument hud)
        {
            if (m_HasPlayed || !isActiveAndEnabled || player == null || Camera.main == null) return;
            if (!ValidRoute(flyoverWaypoints) || !ValidRoute(lakeWaypoints))
            {
                Debug.LogWarning("[TutorialIntro] Missing scene waypoints; leaving gameplay available.", this);
                return;
            }
            m_HasPlayed = true;
            m_Player = player;
            m_Hud = hud;
            StartCoroutine(PlayRoutine(multiplayer, companion));
        }

        private static bool ValidRoute(Transform[] route) => route != null && route.Length == 2 && route[0] != null && route[1] != null;

        internal void PlayPanorama(NetworkObject player, UIDocument hud, string title, Vector3 focus, Vector3 from, Vector3 to)
        {
            m_HasPlayed = true;
            m_Panorama = true;
            m_Title = title;
            courseFocus = focus;
            m_PanoramaFrom = from;
            m_PanoramaTo = to;
            m_Player = player;
            m_Hud = hud;
            StartCoroutine(PlayRoutine(false, null));
        }

        private IEnumerator PlayRoutine(bool multiplayer, GameObject companion)
        {
            IsPlaying = true;
            s_Active = this;
            try
            {
                m_Movement = m_Player.GetComponent<CoreMovement>();
                m_PlayerManager = m_Player.GetComponent<CorePlayerManager>();
                m_PlayerCamera = m_Player.GetComponent<CoreCameraController>();
                m_MovementWasEnabled = m_Movement != null && m_Movement.IsMovementEnabled;
                m_InputWasEnabled = m_PlayerManager != null && m_PlayerManager.MovementInputEnabled;
                m_PlayerLocked = true;
                m_PlayerManager?.SetMovementInputEnabled(false);
                if (m_Movement != null && !m_Panorama) m_Movement.IsMovementEnabled = false;
                m_PlayerCamera?.SetCutsceneInputBlocked(true);
                DisableAbility(m_Player.GetComponent<CoopHoldHandAbility>());
                DisableAbility(m_Player.GetComponent<CoopBoostThrowAbility>());
                if (m_Panorama && FindAnyObjectByType<LevelThreeManager>() is LevelThreeManager level && level.objective)
                    DisableAbility(level.objective.canvas);
                CreateOverlay();

                // A network connection callback can precede PlayerObject replication by several frames.
                Transform partner = companion != null ? companion.transform : null;
                float deadline = Time.realtimeSinceStartup + 20f;
                while (multiplayer && partner == null)
                {
                    if (m_SkipRequested) yield break;
                    var network = NetworkManager.Singleton;
                    if (m_Player == null || network == null || !network.IsConnectedClient) yield break;
                    foreach (var obj in network.SpawnManager.SpawnedObjectsList)
                        if (obj != null && obj.IsPlayerObject && obj != m_Player) { partner = obj.transform; break; }
                    if (Time.realtimeSinceStartup >= deadline)
                    {
                        Debug.LogWarning("[TutorialIntro] Partner spawn timed out; restored gameplay.", this);
                        yield break;
                    }
                    yield return null;
                }
                if (m_Player == null) yield break;

                m_Brain = Camera.main.GetComponent<CinemachineBrain>();
                if (m_Brain == null) yield break;
                m_PreviousBlend = m_Brain.DefaultBlend;
                m_PreviousIgnoreTimeScale = m_Brain.IgnoreTimeScale;
                m_Brain.IgnoreTimeScale = true;
                m_Brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, returnBlendDuration);
                var cameraObject = new GameObject("Intro Camera (local)");
                cameraObject.SetActive(false);
                m_Camera = cameraObject.AddComponent<CinemachineCamera>();
                m_Camera.Priority = 1000;
                m_Camera.Lens.FieldOfView = 55;
                Vector3 focus = (m_Player.transform.position + (partner != null ? partner.position : m_Player.transform.position)) * 0.5f + Vector3.up;
                Vector3 orbitStart = Quaternion.Euler(0, 42, 0) * Vector3.back * 5;
                SetPose(focus + orbitStart + Vector3.up * 2.5f, focus);
                cameraObject.SetActive(true);

                float start = Time.realtimeSinceStartup;
                float duration = m_Panorama ? 4.5f : 9.5f;
                while (!m_SkipRequested && m_Player != null && m_Player.IsSpawned && Time.realtimeSinceStartup - start < duration)
                {
                    float elapsed = Time.realtimeSinceStartup - start;
                    if (m_Panorama)
                    {
                        focus = m_Player.transform.position + Vector3.up;
                        float orbit = Mathf.SmoothStep(0, 1, elapsed / 2f);
                        Vector3 offset = Quaternion.Euler(0, -100 * orbit, 0) * orbitStart;
                        Vector3 close = focus + offset + Vector3.up * 2.5f;
                        float reveal = Mathf.SmoothStep(0, 1, (elapsed - 2f) / 2.5f);
                        // Cut to the wide shot rather than flying through scenery between locations.
                        if (elapsed < 2f) SetPose(close, focus);
                        else SetPose(Vector3.Lerp(m_PanoramaFrom, m_PanoramaTo, reveal), courseFocus);
                    }
                    else if (elapsed < 3f)
                    {
                        float angle = Mathf.Lerp(-45, 70, Mathf.SmoothStep(0, 1, elapsed / 3f)) * Mathf.Deg2Rad;
                        SetPose(focus + new Vector3(Mathf.Sin(angle) * 6, 3, -Mathf.Cos(angle) * 6), focus);
                    }
                    else if (elapsed < 6.5f) Fly(flyoverWaypoints, courseFocus, (elapsed - 3f) / 3.5f);
                    else Fly(lakeWaypoints, lakeFocus, (elapsed - 6.5f) / 3f);
                    yield return null;
                }
                BeginReturn();
                float returnStart = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - returnStart < returnBlendDuration) yield return null;
            }
            finally { Restore(); }
        }

        private void DisableAbility(Behaviour ability)
        {
            if (ability == null) return;
            m_DisabledAbilities[ability] = ability.enabled;
            ability.enabled = false;
        }

        private void Fly(Transform[] route, Vector3 focus, float t) => SetPose(Vector3.Lerp(route[0].position, route[1].position, Mathf.SmoothStep(0, 1, t)), focus);
        private void SetPose(Vector3 position, Vector3 focus) => m_Camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));

        private void Update()
        {
            if (!IsPlaying || m_Returning) return;
#if ENABLE_INPUT_SYSTEM
            bool skip = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)) ||
                        (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame);
#else
            bool skip = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0);
#endif
            if (skip) Skip();
        }

        public void Skip()
        {
            if (!IsPlaying || m_Returning) return;
            m_SkipRequested = true;
            BeginReturn();
        }

        private void BeginReturn()
        {
            if (m_Returning) return;
            m_Returning = true;
            UnlockPlayer();
            if (m_Camera != null) m_Camera.Priority = -1000;
            if (m_Overlay != null) m_Overlay.rootVisualElement.style.display = DisplayStyle.None;
            RestoreHuds();
        }

        private void UnlockPlayer()
        {
            if (!m_PlayerLocked) return;
            m_PlayerLocked = false;
            if (m_Movement != null)
            {
                m_Movement.SetMoveInput(Vector2.zero);
                m_Movement.SetSprintState(false);
                if (!m_Panorama) m_Movement.IsMovementEnabled = m_MovementWasEnabled;
            }
            m_PlayerManager?.SetMovementInputEnabled(m_InputWasEnabled);
            m_PlayerCamera?.SetCutsceneInputBlocked(false);
            foreach (var ability in m_DisabledAbilities) if (ability.Key != null) ability.Key.enabled = ability.Value;
            m_DisabledAbilities.Clear();
        }

        private void CreateOverlay()
        {
            if (m_Hud == null || m_Hud.panelSettings == null) return;
            // The solo companion can retain a UIDocument after its CoreHUD is removed.
            // Hide scene HUD roots as well as the local player's own document.
            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var hudRoot = document.rootVisualElement;
                if (hudRoot == null) continue;
                m_HiddenHuds[hudRoot] = hudRoot.style.display;
                hudRoot.style.display = DisplayStyle.None;
            }
            var overlayObject = new GameObject("Tutorial Intro Letterbox");
            m_Overlay = overlayObject.AddComponent<UIDocument>();
            m_Overlay.panelSettings = m_Hud.panelSettings;
            m_Overlay.sortingOrder = m_Hud.sortingOrder + 100;
            var root = m_Overlay.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            root.style.position = Position.Absolute;
            root.style.left = root.style.right = root.style.top = root.style.bottom = 0;
            foreach (bool top in new[] { true, false })
            {
                var bar = new VisualElement { pickingMode = PickingMode.Ignore };
                bar.style.position = Position.Absolute;
                bar.style.left = bar.style.right = 0;
                bar.style.height = new Length(12, LengthUnit.Percent);
                if (top) bar.style.top = 0; else bar.style.bottom = 0;
                bar.style.backgroundColor = Color.black;
                root.Add(bar);
                if (!top || m_Panorama)
                {
                    var label = new Label(top ? m_Title : "[Space/ESC] Để bỏ qua  •  Chuột trái") { pickingMode = PickingMode.Ignore };
                    label.style.color = Color.white;
                    label.style.fontSize = top ? 28 : 20;
                    label.style.unityFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    label.style.unityTextAlign = top ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight;
                    label.style.flexGrow = 1;
                    if (!top) label.style.paddingRight = 30;
                    bar.Add(label);
                }
            }
        }

        private void Restore()
        {
            if (!IsPlaying) return;
            UnlockPlayer();
            if (m_Camera != null) { m_Camera.gameObject.SetActive(false); Destroy(m_Camera.gameObject); }
            if (m_Brain != null) { m_Brain.DefaultBlend = m_PreviousBlend; m_Brain.IgnoreTimeScale = m_PreviousIgnoreTimeScale; }
            if (m_Overlay != null) Destroy(m_Overlay.gameObject);
            RestoreHuds();
            IsPlaying = false;
            if (s_Active == this) s_Active = null;
        }

        private void OnDisable() { StopAllCoroutines(); Restore(); }

        private void RestoreHuds()
        {
            foreach (var hud in m_HiddenHuds) hud.Key.style.display = hud.Value;
            m_HiddenHuds.Clear();
        }
    }
}
