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
        public float width = 0.85f;

        private LineRenderer line;
        private Transform impactFlare;
        public bool IsEmitting => sourceBeacon != null && sourceBeacon.RequiredItemSocketed &&
            (beamKind == BeamKind.Primary || (prismSocket != null && prismSocket.IsIlluminated));

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.startWidth = width;
            line.endWidth = 1.15f;
            line.useWorldSpace = true;
            line.startColor = new Color(1f, 0.88f, 0.35f, 1f);
            line.endColor = new Color(1f, 0.7f, 0.2f, 0.85f);

            var flare = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flare.name = "BeamImpactFlare";
            flare.transform.SetParent(transform, false);
            flare.transform.localScale = Vector3.one * 0.65f;
            Destroy(flare.GetComponent<Collider>());
            impactFlare = flare.transform;
            flare.GetComponent<Renderer>().sharedMaterial = line.sharedMaterial;
            flare.SetActive(false);
        }

        private void Update()
        {
            if (line == null) return;
            var origin = emitter != null ? emitter : transform;
            bool emitting = IsSpawned && IsEmitting;
            line.enabled = emitting;
            if (impactFlare != null) impactFlare.gameObject.SetActive(false);
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
            if (hitCollider != null && impactFlare != null)
            {
                impactFlare.position = end;
                impactFlare.gameObject.SetActive(true);
            }
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
