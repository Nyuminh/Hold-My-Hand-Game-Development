using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class ClocktowerGearHazard : NetworkBehaviour
    {
        public Transform gear, pendulum;
        public Collider bob, blade;
        float immuneUntil;
        void Update()
        {
            if (!IsSpawned) return;
            float t = (float)(NetworkManager.ServerTime.Time % 3600);
            gear.localRotation = Quaternion.Euler(0, 120 * t, 0);
            pendulum.localRotation = Quaternion.Euler(0, 0, 48 * Mathf.Sin(t * 1.8f));
            var actor = LevelTwoActors.Local;
            if (!actor || Time.time < immuneUntil) return;
            Vector3 centre = actor.transform.position + Vector3.up * .8f;
            if ((bob.ClosestPoint(centre)-centre).sqrMagnitude > .36f && (blade.ClosestPoint(centre)-centre).sqrMagnitude > .36f) return;
            immuneUntil = Time.time + 1.2f;
            Vector3 side = actor.transform.position.x > 0 ? Vector3.right : Vector3.left;
            actor.CoreMovement.ApplyExternalForce(side * 12, ForceMode.Impulse);
        }
    }
}
