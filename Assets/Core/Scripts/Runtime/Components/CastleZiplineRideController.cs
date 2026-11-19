using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Đu Dây Cáp Treo Từ Tháp Canh Sang Lâu Đài (DefenseTower to Castle Zipline).
    /// Cho phép Ba & Con trượt dây cáp băng qua hẻm núi vực sâu để đáp xuống Sân Cổng Lâu Đài.
    /// </summary>
    public class CastleZiplineRideController : NetworkBehaviour
    {
        public static CastleZiplineRideController Instance { get; private set; }

        [Header("Cable Endpoints")]
        [SerializeField] private Transform launchPoint;
        [SerializeField] private Transform landPoint;
        [SerializeField] private Transform trolleyHandle;

        [Header("Zipline Settings")]
        [SerializeField] private float rideDuration = 2.8f;
        [SerializeField] private float triggerRadius = 3.2f;

        [Header("Audio & Effects")]
        [SerializeField] private AudioSource ziplineAudio;
        [SerializeField] private ParticleSystem windParticles;

        private bool isLocalRiding = false;
        private Transform activeRider = null;
        private Vector3 startRiderPos;
        private float rideProgress = 0f;
        private bool showPrompt = false;
        private CharacterController riderController;
        private Rigidbody riderBody;
        private bool controllerWasEnabled;
        private bool bodyWasKinematic;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (launchPoint == null)
            {
                var lp = transform.Find("LaunchPoint");
                if (lp != null) launchPoint = lp;
            }
            if (landPoint == null)
            {
                var lp = GameObject.Find("Castle_Zipline_LandingPad");
                if (lp != null) landPoint = lp.transform;
            }
            if (trolleyHandle == null)
            {
                var th = transform.Find("Zipline_Trolley");
                if (th != null) trolleyHandle = th;
            }
        }

        private void Update()
        {
            if (!isLocalRiding)
            {
                CheckNearbyPlayer();
            }
            else
            {
                UpdateRideMotion();
            }
        }

        private IEnumerator ShakeCamera(Transform cameraTransform)
        {
            Vector3 original = cameraTransform.localPosition;
            cameraTransform.localPosition = original + UnityEngine.Random.insideUnitSphere * 0.05f;
            yield return null;
            if (cameraTransform != null) cameraTransform.localPosition = original;
        }

        private void CheckNearbyPlayer()
        {
            Vector3 center = launchPoint != null ? launchPoint.position : transform.position;
            var cols = Physics.OverlapSphere(center, triggerRadius);
            Transform foundPlayer = null;

            foreach (var col in cols)
            {
                if (col.isTrigger) continue;
                Transform p = col.transform;
                while (p != null)
                {
                    string n = p.name.ToLower();
                    var net = p.GetComponent<NetworkObject>();
                    if ((n.Contains("dad") || n.Contains("son") || n.Contains("child") || n.Contains("player") || n.Contains("character")) && (net == null || !net.IsSpawned || net.IsOwner))
                    {
                        foundPlayer = p;
                        break;
                    }
                    p = p.parent;
                }
                if (foundPlayer != null) break;
            }

            if (foundPlayer != null)
            {
                showPrompt = true;
#if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null && (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame || UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame))
#else
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
#endif
                {
                    StartRide(foundPlayer);
                }
            }
            else
            {
                showPrompt = false;
            }
        }

        public void StartRide(Transform player)
        {
            if (isLocalRiding || player == null || launchPoint == null || landPoint == null) return;
            var net = player.GetComponentInParent<NetworkObject>();
            if (net != null && net.IsSpawned && !net.IsOwner) return;
            activeRider = player;
            isLocalRiding = true;
            rideProgress = 0f;
            startRiderPos = activeRider.position;

            // Disable character controller while on zipline
            riderController = activeRider.GetComponent<CharacterController>();
            controllerWasEnabled = riderController != null && riderController.enabled;
            if (riderController != null) riderController.enabled = false;

            riderBody = activeRider.GetComponent<Rigidbody>();
            bodyWasKinematic = riderBody != null && riderBody.isKinematic;
            if (riderBody != null) riderBody.isKinematic = true;

            if (ziplineAudio != null) ziplineAudio.Play();
            if (windParticles != null) windParticles.Play();
            if (Camera.main != null) StartCoroutine(ShakeCamera(Camera.main.transform));

            Debug.Log("<color=cyan>🪢 [CastleZipline] BẮT ĐẦU ĐU DÂY TRƯỢT SANG LÂU ĐÀI!</color>");
        }

        private void UpdateRideMotion()
        {
            if (activeRider == null)
            {
                isLocalRiding = false;
                return;
            }

            rideProgress += Time.deltaTime / Mathf.Max(0.5f, rideDuration);
            float t = Mathf.Clamp01(rideProgress);

            // Smooth cubic ease-in-out
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            Vector3 startP = startRiderPos;
            Vector3 endP = landPoint.position + Vector3.up * 0.2f;
            Vector3 control = (startP + endP) * 0.5f + Vector3.down * 2.4f;
            float inverse = 1f - smoothT;
            Vector3 currentP = inverse * inverse * startP + 2f * inverse * smoothT * control + smoothT * smoothT * endP;

            activeRider.position = currentP;

            if (trolleyHandle != null)
            {
                trolleyHandle.position = currentP + Vector3.up * 1.5f;
            }

            if (t >= 1f)
            {
                FinishRide();
            }
        }

        private void FinishRide()
        {
            isLocalRiding = false;
            if (activeRider != null)
            {
                Vector3 landP = landPoint != null ? landPoint.position + Vector3.up * 0.2f : activeRider.position;
                activeRider.position = landP;
                var movement = activeRider.GetComponent<CoreMovement>();
                if (movement != null) { movement.SetPosition(landP); movement.ResetMovementForces(); }
                if (riderController != null) riderController.enabled = controllerWasEnabled;
                if (riderBody != null)
                {
                    riderBody.isKinematic = bodyWasKinematic;
                    if (!bodyWasKinematic && launchPoint != null && landPoint != null)
                        riderBody.AddForce((landPoint.position - launchPoint.position).normalized * 2f, ForceMode.Impulse);
                }
                activeRider = null;
                riderController = null;
                riderBody = null;
            }

            // Reset trolley handle back to top after a delay
            StartCoroutine(ResetTrolleyCoroutine());
            Debug.Log("<color=green>🏰 [CastleZipline] ĐÁP XUỐNG SÂN LÂU ĐÀI AN TOÀN!</color>");
        }

        private IEnumerator ResetTrolleyCoroutine()
        {
            yield return new WaitForSeconds(1.5f);
            if (trolleyHandle != null && launchPoint != null)
            {
                trolleyHandle.position = launchPoint.position;
            }
        }

        private void OnGUI()
        {
            if (showPrompt && !isLocalRiding)
            {
                GUIStyle promptStyle = new GUIStyle(GUI.skin.box);
                promptStyle.fontSize = 18;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.normal.textColor = Color.yellow;
                promptStyle.alignment = TextAnchor.MiddleCenter;

                float w = 480f;
                float h = 50f;
                float x = (Screen.width - w) * 0.5f;
                float y = Screen.height - 180f;

                GUI.Box(new Rect(x, y, w, h), "🪢 Nhấn [E] hoặc [SPACE] để Đu Dây sang Lâu Đài", promptStyle);
            }
        }
    }
}
