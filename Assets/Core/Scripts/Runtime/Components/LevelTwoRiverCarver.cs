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

            // Carve riverbed trench in the bounding box: wx in [75, 125], wz in [23, 265]
            int resolution = bed.heightmapResolution;
            float xMinNorm = (75f - ground.transform.position.x) / bed.size.x;
            float xMaxNorm = (125f - ground.transform.position.x) / bed.size.x;
            float zMinNorm = (23f - ground.transform.position.z) / bed.size.z;
            float zMaxNorm = (265f - ground.transform.position.z) / bed.size.z;

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
                    float halfWidth = (wz > 151f && wz < 215f) ? 25f : 13f;
                    if (wz >= 23f && wz <= 265f && Mathf.Abs(wx - 100f) <= halfWidth)
                    {
                        subHeights[z, x] = 0f;
                    }
                }
            }
            bed.SetHeights(xBase, zBase, subHeights);

            // Filter out trees inside the river channel
            bool InRiver(float x, float z) => z >= 23f && z <= 265f && Mathf.Abs(x - 100f) <= (z > 151f && z < 215f ? 25f : 13f);
            bed.treeInstances = bed.treeInstances.Where(tree =>
            {
                var p = ground.transform.position + Vector3.Scale(tree.position, bed.size);
                return !InRiver(p.x, p.z);
            }).ToArray();
        }
    }
}
