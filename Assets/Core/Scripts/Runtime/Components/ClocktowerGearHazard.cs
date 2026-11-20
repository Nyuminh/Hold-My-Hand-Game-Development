using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class ClocktowerGearHazard : NetworkBehaviour
    {
        public Transform gear, pendulum;
        public Collider bob, blade;
        Collider[] contacts;
        float immuneUntil;
        void Awake()
        {
            contacts = GetComponentsInChildren<Collider>();
        }
        void Update()
        {
            if (!IsSpawned) return;
            float t = (float)(NetworkManager.ServerTime.Time % 3600);
            if (gear) gear.localRotation = Quaternion.Euler(0, 120 * t, 0);
            if (pendulum) pendulum.localRotation = Quaternion.Euler(0, 0, 48 * Mathf.Sin(t * 1.8f));
            var actor = LevelTwoActors.Local;
            if (!actor || Time.time < immuneUntil) return;
            if (!actor.TryGetComponent<CharacterController>(out var capsule) || !capsule.enabled) return;
            foreach (var part in contacts)
            {
                if (!part || !part.enabled || !part.gameObject.activeInHierarchy || !Physics.ComputePenetration(capsule, capsule.transform.position,
                    capsule.transform.rotation, part, part.transform.position, part.transform.rotation, out _, out _)) continue;
                immuneUntil = Time.time + 1.2f;
                // Send the player across the edge even at the turnaround of the pendulum.
                var pivot = gear ? gear : pendulum;
                float centreX = pivot ? pivot.position.x : part.bounds.center.x;
                float side = actor.transform.position.x >= centreX ? 1 : -1;
                actor.CoreMovement.SetVerticalVelocity(8f);
                actor.CoreMovement.ApplyExternalForce(Vector3.right * (side * 58f), ForceMode.Impulse);
                break;
            }
        }
    }
}
