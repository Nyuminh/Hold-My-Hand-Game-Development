using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Đệm Bật Nhảy Phong Ấn Bay trên không (Sky Hex Rune Jump Pad).
    /// Khi người chơi dẫm lên bệ đá lục giác, bệ sẽ phát sáng, nhún nhẹ và phóng người chơi
    /// theo quỹ đạo parabol chuẩn xác tới tâm bệ tiếp theo (hoặc boong khinh khí cầu Zephyr)!
    /// </summary>
    [SelectionBase]
    public class SkyHexRuneJumpPad : MonoBehaviour
    {
        [Header("Target & Trajectory")]
        [Tooltip("Điểm đáp đích tiếp theo (Transform của bệ kế tiếp hoặc khinh khí cầu)")]
        [SerializeField] private Transform targetLandingPoint;

        [Tooltip("Lực bật thẳng đứng cơ bản (m/s) khi không có target")]
        [SerializeField] private float verticalLaunchForce = 16.5f;

        [Tooltip("Lực đẩy ngang hướng về đích")]
        [SerializeField] private float horizontalLaunchForce = 9.5f;

        [Tooltip("Độ cao vòm parabol cộng thêm so với điểm cao nhất giữa 2 bệ (m)")]
        [SerializeField] private float arcPeakExtraHeight = 2.4f;

        [Header("Juice & Feedback")]
        [SerializeField] private Light padGlowLight;
        [SerializeField] private ParticleSystem launchParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float bounceSquashAmount = 0.35f;

        private Vector3 initialScale;
        private bool isCoolingDown = false;
        private static readonly Dictionary<CoreMovement, float> s_PlayerLastLandTime = new Dictionary<CoreMovement, float>();
        private static readonly HashSet<CoreMovement> s_PlayersInFlight = new HashSet<CoreMovement>();

        public Transform TargetLandingPoint => targetLandingPoint;

        private void Awake()
        {
            initialScale = transform.localScale;
            if (padGlowLight == null) padGlowLight = GetComponentInChildren<Light>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>();
            if (launchParticles == null) launchParticles = GetComponentInChildren<ParticleSystem>();
        }

        public void SetTarget(Transform nextTarget, float vertForce = 16.5f, float horizForce = 9.5f)
        {
            targetLandingPoint = nextTarget;
            verticalLaunchForce = vertForce;
            horizontalLaunchForce = horizForce;
        }

        private void OnTriggerEnter(Collider other)
        {
            TryTriggerPad(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryTriggerPad(other);
        }

        private void TryTriggerPad(Collider other)
        {
            if (isCoolingDown) return;

            CoreMovement movement = other.GetComponent<CoreMovement>();
            if (movement == null) movement = other.GetComponentInParent<CoreMovement>();
            if (movement == null) return;

            if (s_PlayersInFlight.Contains(movement)) return;

            // Cho người chơi 0.28s đứng vững trên bệ vừa đáp xuống trước khi tự động bật tiếp
            if (s_PlayerLastLandTime.TryGetValue(movement, out float lastLand) && Time.time - lastLand < 0.28f)
                return;

            LaunchPlayer(movement);
        }

        private void LaunchPlayer(CoreMovement movement)
        {
            StartCoroutine(CooldownRoutine());
            StartCoroutine(SquashAndStretchRoutine());

            if (audioSource != null) audioSource.Play();
            if (launchParticles != null) launchParticles.Play();
            UISoundManager.PlayConfirm();

            if (targetLandingPoint != null)
            {
                StartCoroutine(ParabolicFlightRoutine(movement, targetLandingPoint));
            }
            else
            {
                movement.SetVerticalVelocity(verticalLaunchForce);
            }
        }

        private IEnumerator ParabolicFlightRoutine(CoreMovement movement, Transform target)
        {
            if (movement == null || target == null) yield break;

            s_PlayersInFlight.Add(movement);

            var cc = movement.GetComponent<CharacterController>();
            Vector3 startPos = movement.transform.position;

            // Tính điểm đáp chuẩn ngay trên mặt trên của bệ đích
            Vector3 endPos = target.position;
            var targetRend = target.GetComponent<Renderer>();
            if (targetRend != null)
            {
                endPos = new Vector3(targetRend.bounds.center.x, targetRend.bounds.max.y + 0.08f, targetRend.bounds.center.z);
            }
            else
            {
                endPos += Vector3.up * 0.25f;
            }

            float horizDist = Vector2.Distance(new Vector2(startPos.x, startPos.z), new Vector2(endPos.x, endPos.z));
            float duration = Mathf.Clamp(0.55f + horizDist * 0.045f, 0.60f, 1.15f);
            float peakY = Mathf.Max(startPos.y, endPos.y) + arcPeakExtraHeight;

            // Xoay hướng nhân vật nhìn về phía bệ đích
            Vector3 flatDir = new Vector3(endPos.x - startPos.x, 0f, endPos.z - startPos.z);
            if (flatDir.sqrMagnitude > 0.01f)
            {
                movement.transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
            }

            float elapsed = 0f;
            while (elapsed < duration && movement != null && target != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / duration);

                // Quỹ đạo Bezier bậc 2 mượt qua đỉnh vòm peakY
                Vector3 currentTargetPos = Vector3.Lerp(startPos, endPos, u);
                float baseLinearY = Mathf.Lerp(startPos.y, endPos.y, u);
                float arcOffset = 4f * u * (1f - u) * (peakY - 0.5f * (startPos.y + endPos.y));
                currentTargetPos.y = baseLinearY + Mathf.Max(0f, arcOffset);

                Vector3 delta = currentTargetPos - movement.transform.position;
                movement.SetVerticalVelocity(0f);

                if (cc != null && cc.enabled)
                {
                    cc.Move(delta);
                }
                else
                {
                    movement.transform.position = currentTargetPos;
                }

                yield return null;
            }

            if (movement != null)
            {
                movement.SetVerticalVelocity(-2f);
                s_PlayerLastLandTime[movement] = Time.time;
            }

            s_PlayersInFlight.Remove(movement);
        }

        private IEnumerator CooldownRoutine()
        {
            isCoolingDown = true;
            if (padGlowLight != null) padGlowLight.intensity *= 2.5f;
            yield return new WaitForSeconds(0.45f);
            if (padGlowLight != null) padGlowLight.intensity /= 2.5f;
            isCoolingDown = false;
        }

        private IEnumerator SquashAndStretchRoutine()
        {
            float t = 0f;
            Vector3 squashScale = new Vector3(initialScale.x * 1.15f, initialScale.y * (1f - bounceSquashAmount), initialScale.z * 1.15f);
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(initialScale, squashScale, t / 0.08f);
                yield return null;
            }

            t = 0f;
            Vector3 stretchScale = new Vector3(initialScale.x * 0.9f, initialScale.y * 1.25f, initialScale.z * 0.9f);
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(squashScale, stretchScale, t / 0.12f);
                yield return null;
            }

            t = 0f;
            while (t < 0.15f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(stretchScale, initialScale, t / 0.15f);
                yield return null;
            }
            transform.localScale = initialScale;
        }
    }
}
