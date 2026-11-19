using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public class DialogTriggerZone : MonoBehaviour
    {
        public string speaker;
        [TextArea] public string line;
        public float duration = 3.5f;
        private bool triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered || !other.CompareTag("Player")) return;
            triggered = true;
            PlayScene3ObjectiveDirector.Instance?.ShowDialog(speaker, line, duration);
        }
    }
}
