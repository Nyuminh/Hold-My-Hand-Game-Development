using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Bệ nhún bật nhảy trong Tutorial: khi người chơi dẫm lên sẽ bị phóng lên cao.
    /// </summary>
    public class TutorialJumpPad : MonoBehaviour
    {
        [Tooltip("Lực bật nhảy theo phương thẳng đứng")]
        [SerializeField] private float launchVelocity = 15f;

        [Tooltip("Âm thanh khi bật nhảy (nếu có)")]
        [SerializeField] private AudioSource audioSource;

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<CoreMovement>(out var movement))
            {
                movement.SetVerticalVelocity(launchVelocity);
                if (audioSource != null) audioSource.Play();
                Debug.Log($"[TutorialJumpPad] Đã phóng người chơi '{other.name}' lên cao với vận tốc {launchVelocity}!");
            }
            else
            {
                var parentMov = other.GetComponentInParent<CoreMovement>();
                if (parentMov != null)
                {
                    parentMov.SetVerticalVelocity(launchVelocity);
                    if (audioSource != null) audioSource.Play();
                    Debug.Log($"[TutorialJumpPad] Đã phóng người chơi '{parentMov.name}' lên cao với vận tốc {launchVelocity}!");
                }
            }
        }
    }
}
