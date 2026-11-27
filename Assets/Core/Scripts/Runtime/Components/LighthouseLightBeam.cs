using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(LineRenderer))]
    public sealed class LighthouseLightBeam : NetworkBehaviour
    {
        public enum BeamKind { Primary, Refracted }

        public BeamKind beamKind;
        public Transform emitter;
        public MarshBeaconTrigger sourceBeacon;
        public LighthousePrismSocket prismSocket;
        public LuminaMillSensor millSensor;
        public float maxDistance = 80f;
        public LayerMask layerMask = ~0;
        public float width = 0.35f;

        private LineRenderer line;
        public bool IsEmitting => sourceBeacon != null && sourceBeacon.RequiredItemSocketed &&
            (beamKind == BeamKind.Primary || (prismSocket != null && prismSocket.IsIlluminated));

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = line.endWidth = width;
            line.useWorldSpace = true;
            line.startColor = new Color(1f, 0.83f, 0.3f, 1f);
            line.endColor = new Color(1f, 0.65f, 0.15f, 0.6f);
        }

        private void Update()
        {
            if (line == null) return;
            var origin = emitter != null ? emitter : transform;
            bool emitting = IsSpawned && IsEmitting;
            line.enabled = emitting;
            if (!emitting)
            {
                if (IsServer && beamKind == BeamKind.Refracted && millSensor != null) millSensor.NotifySensorActivated(false);
                return;
            }

            Vector3 start = origin.position;
            Vector3 end = start + origin.forward * maxDistance;
            Collider hitCollider = null;
            var hits = Physics.RaycastAll(start, origin.forward, maxDistance, layerMask, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(origin) || hit.collider.GetComponentInParent<CorePlayerManager>() != null) continue;
                end = hit.point; hitCollider = hit.collider; break;
            }
            line.SetPosition(0, start); line.SetPosition(1, end);
            if (!IsServer) return;

            if (beamKind == BeamKind.Primary)
            {
                var socket = hitCollider != null ? hitCollider.GetComponentInParent<LighthousePrismSocket>() : null;
                if (prismSocket != null) prismSocket.NotifyBeamHit(socket == prismSocket);
            }
            else if (millSensor != null)
            {
                var sensor = hitCollider != null ? hitCollider.GetComponentInParent<LuminaMillSensor>() : null;
                millSensor.NotifySensorActivated(sensor == millSensor);
            }
        }
    }
}
