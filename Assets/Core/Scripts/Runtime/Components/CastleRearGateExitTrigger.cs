using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Trigger zone placed on the rear moat stone bridge (Moat_Stone_Bridge_Rear).
    /// Detects when players reach the rear exit after unlocking the rear gate to trigger victory.
    /// </summary>
    public class CastleRearGateExitTrigger : MonoBehaviour
    {
        private bool triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;

            var player = other.GetComponentInParent<CorePlayerManager>();
            if (player == null) return;

            var quest = CastleCourtyardQuestManager.Instance;
            if (quest != null && quest.CurrentStep == CourtyardQuestStep.RearGateUnlocked)
            {
                triggered = true;
                quest.ReachExit();
            }
        }
    }
}
