using System.Collections.Generic;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class RiverCurrentZone : MonoBehaviour
    {
        public Vector3 acceleration = new Vector3(0f, 0f, 1.8f);
        private readonly HashSet<CoopWoodenBoat> boats = new HashSet<CoopWoodenBoat>();
        private void FixedUpdate() => boats.Clear();
        private void OnTriggerStay(Collider other)
        {
            var boat = other.GetComponentInParent<CoopWoodenBoat>();
            if (boat != null && boats.Add(boat)) boat.AddExternalDriftForce(acceleration, Time.fixedDeltaTime);
        }
    }
}
