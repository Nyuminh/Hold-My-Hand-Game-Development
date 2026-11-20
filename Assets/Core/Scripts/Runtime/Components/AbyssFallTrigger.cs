using UnityEngine;

namespace Blocks.Gameplay.Core
{
    // A half-space also catches players that leave the course sideways at high speed.
    public sealed class AbyssFallTrigger : MonoBehaviour
    {
        public AnimationCurve courseElevation;
        public bool Below(Transform actor) => actor.position.y < transform.position.y +
            (courseElevation == null ? 0 : courseElevation.Evaluate(actor.position.z));
    }
}
