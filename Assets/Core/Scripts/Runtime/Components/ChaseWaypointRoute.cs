using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>Arc-length sampling keeps speed constant, including across short waypoint segments.</summary>
    public sealed class ChaseWaypointRoute : MonoBehaviour
    {
        public Transform[] waypoints = System.Array.Empty<Transform>();
        private float[] distances;
        public float Length { get { EnsureCache(); return distances.Length > 0 ? distances[distances.Length - 1] : 0f; } }

        public void Rebuild()
        {
            distances = new float[waypoints.Length];
            for (int i = 1; i < waypoints.Length; i++)
                distances[i] = distances[i - 1] + Vector3.Distance(waypoints[i - 1].position, waypoints[i].position);
        }

        private void EnsureCache() { if (distances == null || distances.Length != waypoints.Length) Rebuild(); }

        public Vector3 Sample(float distance, out Vector3 forward)
        {
            EnsureCache();
            forward = transform.forward;
            if (waypoints.Length == 0) return transform.position;
            for (int i = 1; i < waypoints.Length; i++)
            {
                float length = distances[i] - distances[i - 1];
                if (length < 0.001f) continue;
                forward = (waypoints[i].position - waypoints[i - 1].position) / length;
                if (distance <= distances[i])
                    return Vector3.Lerp(waypoints[i - 1].position, waypoints[i].position, Mathf.Clamp01((distance - distances[i - 1]) / length));
            }
            return waypoints[waypoints.Length - 1].position;
        }

        public float Project(Vector3 point)
        {
            EnsureCache();
            float nearest = float.PositiveInfinity, result = 0f;
            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector3 a = waypoints[i - 1].position, delta = waypoints[i].position - a;
                float t = delta.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude) : 0f;
                float squared = (point - (a + delta * t)).sqrMagnitude;
                if (squared >= nearest) continue;
                nearest = squared;
                result = Mathf.Lerp(distances[i - 1], distances[i], t);
            }
            return result;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            for (int i = 1; i < waypoints.Length; i++)
                if (waypoints[i - 1] && waypoints[i]) Gizmos.DrawLine(waypoints[i - 1].position, waypoints[i].position);
        }
    }
}
