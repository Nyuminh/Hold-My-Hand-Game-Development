using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Mô phỏng vật lý chuỗi xích sắt treo (Verlet chain) với độ giật mượt mà,
    /// chống xuyên tường/xuyên sàn tại mép vực và tỉ lệ mắt xích thu nhỏ 1/2.
    /// </summary>
    internal sealed class HangingChain
    {
        const int Links = 56;
        const float Radius = 0.032f; // Thu nhỏ bán kính va chạm cho mắt xích 1/2
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

            // Tính tỉ lệ mắt xích và thu nhỏ 1/2 (0.5f) theo yêu cầu người dùng
            float modelPitch = (source[0].transform.localPosition - source[1].transform.localPosition).magnitude;
            float scale = (pitch / modelPitch) * 0.5f; // Thu nhỏ 1/2
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
            // Kiểm tra vật cản (mép vực / sàn) giữa 2 điểm đầu mút
            Vector3 clampedB = a + Vector3.ClampMagnitude(b - a, length);

            if (!Visible || Vector3.Distance(a, points[0]) > length * 0.5f ||
                Vector3.Distance(clampedB, points[Links]) > length * 0.5f)
            {
                var side = Vector3.Cross(clampedB - a, Vector3.up).normalized;
                if (side == Vector3.zero) side = Vector3.forward;
                float bow = Mathf.Sqrt(Mathf.Max(0, length * length - (clampedB - a).sqrMagnitude)) * 0.5f;
                for (int i = 0; i <= Links; i++)
                {
                    float t = i / (float)Links;
                    previous[i] = points[i] = Vector3.Lerp(a, clampedB, t) + side * (bow * Mathf.Sin(Mathf.PI * t));
                }
                root.SetActive(true);
            }

            accumulator = Mathf.Min(accumulator + elapsed, 4 * Dt);
            while (accumulator >= Dt)
            {
                Simulate(a, clampedB);
                accumulator -= Dt;
            }

            points[0] = a;
            points[Links] = clampedB;
            PinLengths(clampedB);
            ResolveWallAndFloorCollisions();
            Draw();
        }

        void Simulate(Vector3 a, Vector3 b)
        {
            points[0] = a;
            points[Links] = b;

            for (int i = 1; i < Links; i++)
            {
                var p = points[i];
                var delta = (p - previous[i]) * 0.985f + Physics.gravity * (Dt * Dt);
                previous[i] = p;
                points[i] += delta;
                normal[i] = Vector3.zero;

                // Quét va chạm địa hình / sàn / tường
                if (Physics.SphereCast(p, Radius, delta.normalized, out var hit, delta.magnitude + 0.02f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && hit.collider is not CharacterController)
                {
                    normal[i] = hit.normal;
                    surface[i] = hit.point + hit.normal * Radius;
                }
                else if (Physics.Raycast(points[i] + Vector3.up * 0.35f, Vector3.down, out hit, 0.35f + Radius,
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
                    if (d < 0.00001f) continue;
                    var correction = span * ((d - pitch) / d);
                    if (i == 0) points[1] -= correction;
                    else if (i == Links - 1) points[i] += correction;
                    else { points[i] += correction * 0.5f; points[i + 1] -= correction * 0.5f; }
                }

                for (int i = 1; i < Links; i++)
                {
                    if (normal[i] != Vector3.zero)
                    {
                        float penetration = Vector3.Dot(surface[i] - points[i], normal[i]);
                        if (penetration > 0) points[i] += normal[i] * penetration;
                    }
                }
            }

            PinLengths(b);
        }

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
                    if (d > 0.00001f)
                    {
                        axis /= d;
                        float along = Mathf.Clamp((d * d + pitch * pitch - remaining * remaining) / (2 * d), -pitch, pitch);
                        var side = Vector3.ProjectOnPlane(direction, axis);
                        if (side.sqrMagnitude < 0.000001f)
                            side = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
                        target = origin + axis * along + side.normalized * Mathf.Sqrt(Mathf.Max(0, pitch * pitch - along * along));
                    }
                }
                points[i] = target;
            }
        }

        /// <summary>
        /// Khắc phục hiện tượng xích xuyên sàn/tường:
        /// Sau khi PinLengths ghim khoảng cách, đẩy dứt điểm các mắt xích nổi lên trên mặt sàn hoặc ra ngoài mép đá.
        /// </summary>
        void ResolveWallAndFloorCollisions()
        {
            for (int i = 1; i < Links; i++)
            {
                // 1. Kiểm tra tia dọc chống xích chìm dưới mặt sàn
                if (Physics.Raycast(points[i] + Vector3.up * 0.4f, Vector3.down, out var floorHit, 0.4f + Radius + 0.05f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && floorHit.collider is not CharacterController)
                {
                    float minHeight = floorHit.point.y + Radius;
                    if (points[i].y < minHeight)
                    {
                        points[i].y = minHeight;
                    }
                }

                // 2. Kiểm tra xuyên qua mép tường/gờ đá giữa 2 mắt xích liền kề
                Vector3 seg = points[i] - points[i - 1];
                float segLen = seg.magnitude;
                if (segLen > 0.001f)
                {
                    if (Physics.Raycast(points[i - 1], seg.normalized, out var wallHit, segLen,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) && wallHit.collider is not CharacterController)
                    {
                        points[i] = wallHit.point + wallHit.normal * Radius;
                    }
                }
            }
        }

        void Draw()
        {
            var forward = Vector3.forward;
            for (int i = 0; i < Links; i++)
            {
                var up = (points[i + 1] - points[i]).normalized;
                forward = Vector3.ProjectOnPlane(forward, up);
                if (forward.sqrMagnitude < 0.0001f)
                    forward = Vector3.Cross(up, Mathf.Abs(up.y) < 0.9f ? Vector3.up : Vector3.right);
                forward.Normalize();
                var pose = Quaternion.LookRotation(forward, up);
                if ((i & 1) != 0) pose *= Quaternion.AngleAxis(90, Vector3.up);
                links[i].SetPositionAndRotation((points[i] + points[i + 1]) * 0.5f - pose * meshOffset, pose);
            }
        }
    }
}
