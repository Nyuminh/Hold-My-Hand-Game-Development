using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public class TorchFlickerLight : MonoBehaviour
    {
        [SerializeField] private float baseIntensity = 1.8f;
        [SerializeField] private float amplitude = 0.7f;
        [SerializeField] private float speed = 2.4f;
        private Light target;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            seed = Random.Range(0f, 1000f);
            if (target != null) baseIntensity = Mathf.Max(baseIntensity, target.intensity);
        }

        private void Update()
        {
            if (target == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            target.intensity = baseIntensity * (0.75f + n * amplitude);
        }
    }
}
