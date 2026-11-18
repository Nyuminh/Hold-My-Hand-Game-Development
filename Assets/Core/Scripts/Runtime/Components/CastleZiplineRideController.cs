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
                    if (n.Contains("dad") || n.Contains("son") || n.Contains("child") || n.Contains("player") || n.Contains("character"))
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
                if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
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
            if (isLocalRiding) return;
            activeRider = player;
            isLocalRiding = true;
            rideProgress = 0f;
            startRiderPos = activeRider.position;

            // Disable character controller while on zipline
            var cc = activeRider.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            var rb = activeRider.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            if (ziplineAudio != null) ziplineAudio.Play();
            if (windParticles != null) windParticles.Play();

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

            Vector3 startP = launchPoint != null ? launchPoint.position + Vector3.down * 1.5f : startRiderPos;
            Vector3 endP = landPoint != null ? landPoint.position + Vector3.up * 0.2f : new Vector3(1.5f, 24.5f, -102.0f);

            // Slight sag / curve in zipline
            Vector3 currentP = Vector3.Lerp(startP, endP, smoothT);
            float sag = Mathf.Sin(smoothT * Mathf.PI) * 1.2f;
            currentP.y -= sag;

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
                var cc = activeRider.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;

                var rb = activeRider.GetComponent<Rigidbody>();
                if (rb != null) rb.isKinematic = false;

                Vector3 landP = landPoint != null ? landPoint.position + Vector3.up * 0.1f : new Vector3(1.5f, 24.5f, -102.0f);
                activeRider.position = landP;
                activeRider = null;
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
