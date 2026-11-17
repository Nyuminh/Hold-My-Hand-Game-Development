using System.Linq;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Dynamically carves the river bed and clears waterway foliage in memory when Level 2 loads.
    /// This avoids committing a 125MB duplicated TerrainData binary asset to Git,
    /// ensuring zero-cost git push and total isolation from other levels.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class LevelTwoRiverCarver : MonoBehaviour
    {
        private void Awake()
        {
            var ground = Terrain.activeTerrain;
            if (ground == null) ground = FindFirstObjectByType<Terrain>();
            if (ground == null || ground.terrainData == null) return;

            // Clone TerrainData in memory so the shared on-disk Demo.asset is never modified.
            var bed = Instantiate(ground.terrainData);
            bed.name = "LevelTwoRiverBed_RuntimeInstance";
            ground.terrainData = bed;

            if (ground.TryGetComponent<TerrainCollider>(out var col))
            {
                col.terrainData = bed;
            }

            // Carve riverbed trench in the bounding box: wx in [68, 132], wz in [23, 435]
            int resolution = bed.heightmapResolution;
            float xMinNorm = (68f - ground.transform.position.x) / bed.size.x;
            float xMaxNorm = (132f - ground.transform.position.x) / bed.size.x;
            float zMinNorm = (23f - ground.transform.position.z) / bed.size.z;
            float zMaxNorm = (435f - ground.transform.position.z) / bed.size.z;

            int xBase = Mathf.Clamp(Mathf.FloorToInt(xMinNorm * (resolution - 1)), 0, resolution - 1);
            int zBase = Mathf.Clamp(Mathf.FloorToInt(zMinNorm * (resolution - 1)), 0, resolution - 1);
            int width = Mathf.Clamp(Mathf.CeilToInt((xMaxNorm - xMinNorm) * (resolution - 1)), 1, resolution - xBase);
            int height = Mathf.Clamp(Mathf.CeilToInt((zMaxNorm - zMinNorm) * (resolution - 1)), 1, resolution - zBase);

            var subHeights = bed.GetHeights(xBase, zBase, width, height);
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float wx = ground.transform.position.x + (xBase + x) * bed.size.x / (resolution - 1);
                    float wz = ground.transform.position.z + (zBase + z) * bed.size.z / (resolution - 1);
                    
                    float halfWidth = 14f;
                    if (wz >= 150f && wz <= 225f) halfWidth = 22f;       // Hang động nhũ đá dạ quang
                    else if (wz > 225f && wz < 315f) halfWidth = 26f;    // Đầm lầy Misty Marsh
                    else if (wz >= 315f && wz <= 365f) halfWidth = 16f;  // Khúc cua chữ S
                    else if (wz > 365f) halfWidth = 22f;                 // Vịnh Cảng Haven
                    
                    if (wz >= 23f && wz <= 435f && Mathf.Abs(wx - 100f) <= halfWidth)
                    {
                        subHeights[z, x] = 0f;
                    }
                }
            }
            bed.SetHeights(xBase, zBase, subHeights);

            // Filter out trees inside the river channel
            bool InRiver(float x, float z)
            {
                if (z < 23f || z > 435f) return false;
                float halfWidth = 14f;
                if (z >= 150f && z <= 225f) halfWidth = 22f;
                else if (z > 225f && z < 315f) halfWidth = 26f;
                else if (z >= 315f && z <= 365f) halfWidth = 16f;
                else if (z > 365f) halfWidth = 22f;
                return Mathf.Abs(x - 100f) <= halfWidth;
            }
            bed.treeInstances = bed.treeInstances.Where(tree =>
            {
                var p = ground.transform.position + Vector3.Scale(tree.position, bed.size);
                return !InRiver(p.x, p.z);
            }).ToArray();

            // Disable collision on pre-existing environment rocks in the river navigation channel
            var colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var c in colliders)
            {
                if (c is TerrainCollider) continue;
                if (c.transform.IsChildOf(transform)) continue; // Keep intentional LVL2 obstacles
                var p = c.transform.position;
                if (p.z >= 24f && p.z <= 265f && Mathf.Abs(p.x - 100f) <= 7.0f && p.y <= 1.8f)
                {
                    c.enabled = false;
                }
            }
        }
    }
}
