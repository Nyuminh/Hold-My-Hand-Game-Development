using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(Collider))]
    public sealed class LuminaMillSensor : NetworkBehaviour
    {
        public Renderer lensRenderer;
        public MarshBeaconTrigger west;
        public MarshBeaconTrigger east;
        public AudioSource chime;
        public float requiredHitSeconds = 0.5f;
        public Color unpoweredColor = new(1f, 0.08f, 0.03f);
        public Color poweredColor = new(0.15f, 1f, 0.9f);

        private readonly NetworkVariable<bool> isPowered = new();
        private float hitDuration;
        private MaterialPropertyBlock properties;
        public bool IsPowered => isPowered.Value;

        public override void OnNetworkSpawn()
        {
            isPowered.OnValueChanged += PoweredChanged;
            ApplyVisual(IsPowered);
        }

        public override void OnNetworkDespawn() => isPowered.OnValueChanged -= PoweredChanged;

        public void NotifySensorActivated(bool active)
        {
            if (!IsServer || IsPowered) return;
            hitDuration = active ? hitDuration + Time.deltaTime : 0f;
            if (hitDuration < requiredHitSeconds) return;
            isPowered.Value = true;
            west?.SetLit(true);
            east?.SetLit(true);
            PoweredClientRpc();
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
        private void PoweredClientRpc()
        {
            ApplyVisual(true);
            if (chime != null && !chime.isPlaying) chime.Play();
        }
    }
}
