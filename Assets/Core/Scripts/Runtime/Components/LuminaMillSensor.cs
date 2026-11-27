using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(SphereCollider))]
    public sealed class LuminaMillSensor : NetworkBehaviour
    {
        public const float SensorRadius = 0.55f;
        public Renderer lensRenderer;
        public MarshBeaconTrigger west;
        public MarshBeaconTrigger east;
        public AudioSource chime;
        public float requiredHitSeconds = 0.3f;
        public Color unpoweredColor = new(1f, 0.08f, 0.03f);
        public Color poweredColor = new(0.15f, 1f, 0.9f);

        private readonly NetworkVariable<bool> isPowered = new();
        private float hitDuration;
        private GiantWatermillObstacle watermill;
        private MaterialPropertyBlock properties;
        public bool IsPowered => isPowered.Value;

        private void Reset() => ConfigureCollider();

        private void OnValidate() => ConfigureCollider();

        private void ConfigureCollider()
        {
            var sensorCollider = GetComponent<SphereCollider>();
            if (sensorCollider == null) return;
            sensorCollider.radius = SensorRadius;
            sensorCollider.isTrigger = true;
        }

        public override void OnNetworkSpawn()
        {
            isPowered.OnValueChanged += PoweredChanged;
            ApplyVisual(IsPowered);
        }

        public override void OnNetworkDespawn() => isPowered.OnValueChanged -= PoweredChanged;

        public void NotifySensorActivated(bool active)
        {
            if (!IsServer) return;
            if (active)
            {
                hitDuration += Time.deltaTime;
                if (hitDuration < requiredHitSeconds || isPowered.Value) return;
                isPowered.Value = true;
                west?.SetLit(true);
                east?.SetLit(true);
                PoweredClientRpc(true);
                return;
            }

            hitDuration = 0f;
            if (!isPowered.Value) return;
            watermill ??= Object.FindAnyObjectByType<GiantWatermillObstacle>();
            if (watermill != null && watermill.Passed) return;
            isPowered.Value = false;
            west?.SetLit(false);
            east?.SetLit(false);
            PoweredClientRpc(false);
        }

        public void RestoreCheckpoint(bool powered)
        {
            if (!IsServer) return;
            isPowered.Value = powered;
            hitDuration = powered ? requiredHitSeconds : 0;
            west?.SetLit(powered);
            east?.SetLit(powered);
        }

        private void PoweredChanged(bool before, bool after) => ApplyVisual(after);

        private void ApplyVisual(bool powered)
        {
            if (lensRenderer == null) return;
            properties ??= new MaterialPropertyBlock();
            lensRenderer.GetPropertyBlock(properties);
            Color color = powered ? poweredColor : unpoweredColor;
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_EmissionColor", color * (powered ? 4f : 1.5f));
            lensRenderer.SetPropertyBlock(properties);
        }

        [Rpc(SendTo.Everyone)]
        private void PoweredClientRpc(bool powered)
        {
            ApplyVisual(powered);
            if (powered && chime != null && !chime.isPlaying) chime.Play();
            if (!powered)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = 0.065f;
            }
        }
    }
}
