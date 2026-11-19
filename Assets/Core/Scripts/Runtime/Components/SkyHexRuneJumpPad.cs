using System.Collections;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Đệm Bật Nhảy Phong Ấn Bay trên không (Sky Hex Rune Jump Pad).
    /// Khi người chơi dẫm lên bệ đá lục giác, bệ sẽ phát sáng, nhún nhẹ và phóng người chơi
    /// vút thẳng lên bệ tiếp theo (theo cả phương ngang và phương thẳng đứng) mượt mà và đã tay!
    /// </summary>
    [SelectionBase]
    public class SkyHexRuneJumpPad : MonoBehaviour
    {
        [Header("Target & Trajectory")]
        [Tooltip("Điểm đáp đích tiếp theo (Transform của bệ kế tiếp hoặc khinh khí cầu)")]
        [SerializeField] private Transform targetLandingPoint;

        [Tooltip("Lực bật thẳng đứng cơ bản (m/s)")]
        [SerializeField] private float verticalLaunchForce = 16.5f;

        [Tooltip("Lực đẩy ngang hướng về đích")]
        [SerializeField] private float horizontalLaunchForce = 9.5f;

        [Header("Juice & Feedback")]
        [SerializeField] private Light padGlowLight;
        [SerializeField] private ParticleSystem launchParticles;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private float bounceSquashAmount = 0.35f;

        private Vector3 initialScale;
        private bool isCoolingDown = false;

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
            if (isCoolingDown) return;

            CoreMovement movement = other.GetComponent<CoreMovement>();
            if (movement == null) movement = other.GetComponentInParent<CoreMovement>();

            if (movement != null)
            {
                LaunchPlayer(movement);
            }
        }

        private void LaunchPlayer(CoreMovement movement)
        {
            StartCoroutine(CooldownRoutine());
            StartCoroutine(SquashAndStretchRoutine());

            // 1. Âm thanh và hiệu ứng hạt
            if (audioSource != null) audioSource.Play();
            if (launchParticles != null) launchParticles.Play();

            // 2. Tính toán vector đẩy
            Vector3 pushDirection = Vector3.up;
            if (targetLandingPoint != null)
            {
                Vector3 toTarget = (targetLandingPoint.position - movement.transform.position);
                toTarget.y = 0; // Chỉ lấy hướng mặt phẳng ngang
                if (toTarget.sqrMagnitude > 0.1f)
                {
                    pushDirection = (toTarget.normalized * horizontalLaunchForce) + (Vector3.up * verticalLaunchForce);
                }
                else
                {
                    pushDirection = Vector3.up * verticalLaunchForce;
                }
            }
            else
            {
                pushDirection = Vector3.up * verticalLaunchForce;
            }

            // 3. Phóng nhân vật
            movement.SetVerticalVelocity(verticalLaunchForce);
            Vector3 horizForce = new Vector3(pushDirection.x, 0, pushDirection.z);
            movement.ApplyExternalForce(horizForce, ForceMode.Impulse);

            Debug.Log($"[SkyHexRuneJumpPad] Đã phóng {movement.name} về phía {targetLandingPoint?.name} với lực {pushDirection}!");
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
            // Nhún xuống (squash)
            float t = 0f;
            Vector3 squashScale = new Vector3(initialScale.x * 1.15f, initialScale.y * (1f - bounceSquashAmount), initialScale.z * 1.15f);
            while (t < 0.08f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(initialScale, squashScale, t / 0.08f);
                yield return null;
            }

            // Bật nảy lên (stretch)
            t = 0f;
            Vector3 stretchScale = new Vector3(initialScale.x * 0.9f, initialScale.y * 1.25f, initialScale.z * 0.9f);
            while (t < 0.12f)
            {
                t += Time.deltaTime;
                transform.localScale = Vector3.Lerp(squashScale, stretchScale, t / 0.12f);
                yield return null;
            }

            // Hồi phục lại trạng thái ban đầu
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
