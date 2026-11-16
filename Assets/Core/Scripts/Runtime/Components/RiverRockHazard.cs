using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(Collider))]
    public sealed class RiverRockHazard : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            var boat = collision.collider.GetComponentInParent<CoopWoodenBoat>();
            if (boat != null) boat.ApplyRockImpact((boat.transform.position - transform.position).normalized);
        }
    }
}
