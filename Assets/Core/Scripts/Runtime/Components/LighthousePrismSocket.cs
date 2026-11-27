using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(Collider))]
    public sealed class LighthousePrismSocket : MonoBehaviour
    {
        public MarshBeaconTrigger beacon;
        public float graceSeconds = 0.12f;
        private float illuminatedUntil;
        public bool IsIlluminated => beacon != null && beacon.RequiredItemSocketed && Time.time <= illuminatedUntil;

        public void NotifyBeamHit(bool hit)
        {
            if (hit && beacon != null && beacon.RequiredItemSocketed) illuminatedUntil = Time.time + graceSeconds;
        }
    }
}
