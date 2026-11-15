using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class LevelTwoDock : MonoBehaviour
    {
        public Transform dadLanding;
        public Transform childLanding;
        public float boardingRadius = 6f;
        public static bool TryLanding(Vector3 boatPosition, bool dad, out Vector3 position)
        {
            position = default;
            float closest = float.PositiveInfinity;
            foreach (var dock in FindObjectsByType<LevelTwoDock>())
            {
                float distance = Vector3.Distance(boatPosition, dock.transform.position);
                var target = dad ? dock.dadLanding : dock.childLanding;
                if (target != null && distance <= dock.boardingRadius && distance < closest)
                { closest = distance; position = target.position; }
            }
            return closest < float.PositiveInfinity;
        }
    }
}
