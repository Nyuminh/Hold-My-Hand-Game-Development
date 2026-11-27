using UnityEngine;

namespace Blocks.Gameplay.Core
{
    // Verlet particles carry inertia. Both the simulated pitch and the iron mesh size stay fixed.
    internal sealed class HangingChain
    {
        const int Links = 48;
        const float Radius = .055f;
        readonly Vector3[] points = new Vector3[Links + 1];
        readonly Vector3[] previous = new Vector3[Links + 1];
        readonly Vector3[] surface = new Vector3[Links + 1];
        readonly Vector3[] normal = new Vector3[Links + 1];
        readonly Transform[] links = new Transform[Links];
        readonly GameObject root;
        readonly float length, pitch;
        readonly Vector3 meshOffset;
        float accumulator;
        const float Dt = 1f / 60;
        public bool Visible => root.activeSelf;

        public HangingChain(Transform parent, GameObject model, float length)
        {
            this.length = length;
            pitch = length / Links;
            root = new GameObject("Iron Chain");
            root.transform.SetParent(parent, false);
            var source = model.GetComponentsInChildren<MeshFilter>();
            var mesh = source[0].sharedMesh;
            var material = source[0].GetComponent<MeshRenderer>().sharedMaterial;
            // Match the overlapping centre spacing of the first two authored FBX links.
            float modelPitch = (source[0].transform.localPosition - source[1].transform.localPosition).magnitude;
            float scale = pitch / modelPitch;
            meshOffset = mesh.bounds.center * scale;
            for (int i = 0; i < Links; i++)
            {
                var link = new GameObject("Chain link " + (i + 1), typeof(MeshFilter), typeof(MeshRenderer));
                links[i] = link.transform;
                links[i].SetParent(root.transform, false);
                links[i].localScale = Vector3.one * scale;
                link.GetComponent<MeshFilter>().sharedMesh = mesh;
                link.GetComponent<MeshRenderer>().sharedMaterial = material;
            }
            Hide();
        }

        public void Hide() { root.SetActive(false); accumulator = 0; }

        public void Step(Vector3 a, Vector3 b, float elapsed)
        {
            // A remote hand can briefly arrive ahead of its owner's constrained network pose.
            b = a + Vector3.ClampMagnitude(b - a, length);
            if (!Visible || Vector3.Distance(a, points[0]) > length * .5f ||
                Vector3.Distance(b, points[Links]) > length * .5f)
            {
                var side = Vector3.Cross(b - a, Vector3.up).normalized;
                if (side == Vector3.zero) side = Vector3.forward;
                float bow = Mathf.Sqrt(Mathf.Max(0, length * length - (b - a).sqrMagnitude)) * .5f;
                for (int i = 0; i <= Links; i++)
                {
                    float t = i / (float)Links;
                    // Lay spare chain to one side on spawn, then let gravity settle it.
                    previous[i] = points[i] = Vector3.Lerp(a, b, t) + side * (bow * Mathf.Sin(Mathf.PI * t));
                }
                root.SetActive(true);
            }
            accumulator = Mathf.Min(accumulator + elapsed, 4 * Dt);
            while (accumulator >= Dt)
            {
                Simulate(a, b);
                accumulator -= Dt;
            }
            points[0] = a;
            points[Links] = b;
            PinLengths(b);
            Draw();
        }

        void Simulate(Vector3 a, Vector3 b)
        {
            points[0] = a;
            points[Links] = b;
            for (int i = 1; i < Links; i++)
            {
                var p = points[i];
                var delta = (p - previous[i]) * .988f + Physics.gravity * (Dt * Dt);
                previous[i] = p;
                points[i] += delta;
                normal[i] = Vector3.zero;
                // Sweep the motion, then find supporting terrain even when a neighbouring link drags this one sideways.
                if (Physics.SphereCast(p, Radius, delta.normalized, out var hit, delta.magnitude,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider is not CharacterController)
                {
                    normal[i] = hit.normal;
                    surface[i] = hit.point + hit.normal * Radius;
                }
                else if (Physics.Raycast(points[i] + Vector3.up * .25f, Vector3.down, out hit, .25f + Radius,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider is not CharacterController)
                {
                    normal[i] = hit.normal;
                    surface[i] = hit.point + hit.normal * Radius;
                }
            }
            for (int pass = 0; pass < 12; pass++)
            {
                for (int k = 0; k < Links; k++)
                {
                    int i = (pass & 1) == 0 ? k : Links - 1 - k;
                    var span = points[i + 1] - points[i];
                    float d = span.magnitude;
                    if (d < .00001f) continue;
                    var correction = span * ((d - pitch) / d);
                    if (i == 0) points[1] -= correction;
                    else if (i == Links - 1) points[i] += correction;
                    else { points[i] += correction * .5f; points[i + 1] -= correction * .5f; }
                }
                for (int i = 1; i < Links; i++)
                {
                    float penetration = Vector3.Dot(surface[i] - points[i], normal[i]);
                    if (penetration > 0) points[i] += normal[i] * penetration;
                }
            }
            PinLengths(b);
        }

        // Reconstruct equal-length spans while keeping the remaining endpoint reachable.
        // On the penultimate joint the two adjacent spheres must intersect exactly.
        void PinLengths(Vector3 end)
        {
            for (int i = 1; i < Links; i++)
            {
                var origin = points[i - 1];
                var direction = (points[i] - origin).normalized;
                if (direction == Vector3.zero) direction = Vector3.down;
                var target = origin + direction * pitch;
                float remaining = (Links - i) * pitch;
                if (i == Links - 1 || (end - target).sqrMagnitude > remaining * remaining)
                {
                    var axis = end - origin;
                    float d = axis.magnitude;
                    if (d > .00001f)
                    {
                        axis /= d;
                        float along = Mathf.Clamp((d * d + pitch * pitch - remaining * remaining) / (2 * d), -pitch, pitch);
                        var side = Vector3.ProjectOnPlane(direction, axis);
                        if (side.sqrMagnitude < .000001f)
                            side = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right);
                        target = origin + axis * along + side.normalized * Mathf.Sqrt(Mathf.Max(0, pitch * pitch - along * along));
                    }
                }
                points[i] = target;
            }
        }

        void Draw()
        {
            var forward = Vector3.forward;
            for (int i = 0; i < Links; i++)
            {
                var up = (points[i + 1] - points[i]).normalized;
                forward = Vector3.ProjectOnPlane(forward, up);
                if (forward.sqrMagnitude < .0001f)
                    forward = Vector3.Cross(up, Mathf.Abs(up.y) < .9f ? Vector3.up : Vector3.right);
                forward.Normalize();
                var pose = Quaternion.LookRotation(forward, up);
                if ((i & 1) != 0) pose *= Quaternion.AngleAxis(90, Vector3.up);
                links[i].SetPositionAndRotation((points[i] + points[i + 1]) * .5f - pose * meshOffset, pose);
            }
        }
    }
}
