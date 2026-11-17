using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Narrative & Visual Affordance for Tutorial Stage 3 ("Bập Bênh Vực Thẳm"):
    /// - Guiding amber light onto the elevator platform when Dad is winching at the islet dock.
    /// - Atmospheric beacon highlighting the counterweight boulder when Child lands on Zone 3.
    /// - Interaction trigger allowing Child to push the counterweight boulder into the cradle.
    /// - Smooth flow coordinating Dad's clutch release, lift return, and joyous reunion.
    /// </summary>
    [SelectionBase]
    public class CraneElevatorAffordance : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CraneElevatorController craneController;
        [SerializeField] private Transform boulderTransform;
        [SerializeField] private Transform liftPlatformTransform;
        [SerializeField] private Transform zone3LeverTransform;

        [Header("Affordance Visuals")]
        [SerializeField] private Color beaconColor = new Color(1.0f, 0.78f, 0.35f, 1.0f); // Warm amber
        [SerializeField] private float interactRadius = 3.5f;

        private Light m_BoulderBeacon;
        private Light m_PlatformBeacon;
        private bool m_BoulderPushed = false;
        private bool m_ClutchReleased = false;
        private bool m_ReturnTriggered = false;
        private bool m_ReunionCelebrated = false;

        private void Awake()
        {
            if (craneController == null)
            {
                craneController = GetComponent<CraneElevatorController>();
                if (craneController == null)
                {
                    craneController = FindFirstObjectByType<CraneElevatorController>();
                }
            }

            SetupAffordanceLights();
        }

        private void SetupAffordanceLights()
        {
            // Boulder spotlight guide
            GameObject boulderLightObj = new GameObject("Affordance_Boulder_Light");
            boulderLightObj.transform.SetParent(transform);
            boulderLightObj.transform.position = new Vector3(-3.5f, 18.0f, -54.0f);
            boulderLightObj.transform.rotation = Quaternion.Euler(60f, 0f, 0f);

            m_BoulderBeacon = boulderLightObj.AddComponent<Light>();
            m_BoulderBeacon.type = LightType.Spot;
            m_BoulderBeacon.spotAngle = 45f;
            m_BoulderBeacon.range = 10f;
            m_BoulderBeacon.color = beaconColor;
            m_BoulderBeacon.intensity = 0f; // Enabled when Child arrives at Zone 3

            // Lift boarding light guide
            GameObject platformLightObj = new GameObject("Affordance_Platform_Light");
            platformLightObj.transform.SetParent(transform);
            platformLightObj.transform.position = new Vector3(-1.0f, 14.5f, -24.8f);
            platformLightObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            m_PlatformBeacon = platformLightObj.AddComponent<Light>();
            m_PlatformBeacon.type = LightType.Spot;
            m_PlatformBeacon.spotAngle = 50f;
            m_PlatformBeacon.range = 8f;
            m_PlatformBeacon.color = beaconColor;
            m_PlatformBeacon.intensity = 2.0f; // Active at start to invite Child onto lift
        }

        private void Update()
        {
            if (craneController == null) return;

            var stage = craneController.CurrentStage;

            // Pulse platform beacon during first ride
            if (stage == CraneElevatorController.ElevatorStage.ReadyForFirstRide)
            {
                if (m_PlatformBeacon != null)
                {
                    float pulse = 1.8f + Mathf.Sin(Time.time * 3f) * 0.7f;
                    m_PlatformBeacon.intensity = pulse;
                }
            }
            else
            {
                if (m_PlatformBeacon != null && m_PlatformBeacon.intensity > 0.05f)
                {
                    m_PlatformBeacon.intensity = Mathf.Lerp(m_PlatformBeacon.intensity, 0f, Time.deltaTime * 3f);
                }
            }

            // Child at Zone 3: Illuminate boulder and detect proximity push
            if (stage == CraneElevatorController.ElevatorStage.ConAtZone3_NeedsBoulder)
            {
                if (m_BoulderBeacon != null)
                {
                    float pulse = 2.5f + Mathf.Sin(Time.time * 4f) * 0.8f;
                    m_BoulderBeacon.intensity = Mathf.Lerp(m_BoulderBeacon.intensity, pulse, Time.deltaTime * 4f);
                }

                if (!m_BoulderPushed)
                {
                    Vector3 bPos = boulderTransform != null ? boulderTransform.position : new Vector3(-3.5f, 15.4f, -54f);
                    if (IsAnyPlayerNear(bPos, interactRadius))
                    {
                        if (IsInteractPressed())
                        {
                            m_BoulderPushed = true;
                            craneController.TriggerRollBoulder();
                        }
                    }
                }
            }
            else
            {
                if (m_BoulderBeacon != null && m_BoulderBeacon.intensity > 0.05f)
                {
                    m_BoulderBeacon.intensity = Mathf.Lerp(m_BoulderBeacon.intensity, 0f, Time.deltaTime * 3f);
                }
            }

            // Stage: BoulderPlaced_WaitingDad -> Dad releases clutch across chasm
            if (stage == CraneElevatorController.ElevatorStage.BoulderPlaced_WaitingDad && !m_ClutchReleased)
            {
                Vector3 craneDockPos = new Vector3(-9.4f, 7.8f, -14.2f);
                if (IsAnyPlayerNear(craneDockPos, interactRadius + 1.5f))
                {
                    if (IsInteractPressed())
                    {
                        m_ClutchReleased = true;
                        craneController.TriggerDadReleaseClutch();
                    }
                }
            }

            // Stage: BothUnlocked_ReadyReturn -> Child pulls lever to return elevator to Islet
            if (stage == CraneElevatorController.ElevatorStage.BothUnlocked_ReadyReturn && !m_ReturnTriggered)
            {
                Vector3 leverPos = zone3LeverTransform != null ? zone3LeverTransform.position : new Vector3(0.8f, 14.2f, -45.8f);
                if (IsAnyPlayerNear(leverPos, interactRadius))
                {
                    if (IsInteractPressed())
                    {
                        m_ReturnTriggered = true;
                        craneController.TriggerReturnToIslet();
                    }
                }
            }

            // Stage: BothCompleted -> Reunion celebration
            if (stage == CraneElevatorController.ElevatorStage.BothCompleted && !m_ReunionCelebrated)
            {
                m_ReunionCelebrated = true;
                StartCoroutine(ReunionCelebrationRoutine());
            }
        }

        private IEnumerator ReunionCelebrationRoutine()
        {
            // Spawn temporary celebratory warm light at Zone 3 reunion landing
            GameObject reunionFx = new GameObject("Reunion_WarmGlow");
            reunionFx.transform.position = new Vector3(-1.0f, 15.0f, -46.0f);
            Light rLight = reunionFx.AddComponent<Light>();
            rLight.type = LightType.Point;
            rLight.color = new Color(1.0f, 0.85f, 0.45f);
            rLight.range = 12f;
            rLight.intensity = 0f;

            float elapsed = 0f;
            while (elapsed < 3f)
            {
                elapsed += Time.deltaTime;
                rLight.intensity = Mathf.PingPong(elapsed * 2f, 3.5f);
                yield return null;
            }

            Destroy(reunionFx);
        }

        private bool IsAnyPlayerNear(Vector3 targetPos, float radius)
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                if (Vector3.Distance(p.transform.position, targetPos) <= radius)
                    return true;
            }
            return false;
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                    return true;
            }
            if (Gamepad.current != null)
            {
                if (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame)
                    return true;
            }
            return false;
#else
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
                return true;

            return false;
#endif
        }
    }
}
