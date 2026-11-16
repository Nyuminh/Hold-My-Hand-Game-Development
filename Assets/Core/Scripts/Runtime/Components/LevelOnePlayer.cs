using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public static class LevelOnePlayer
    {
        public static bool IsDad(CorePlayerManager player)
        {
            if (player.TryGetComponent<CoopBoostThrowAbility>(out var boost)) return boost.IsDad;
            if (player.TryGetComponent<CoopHoldHandAbility>(out var hand)) return hand.IsDad;
            return player.name.Contains("Dad");
        }

        public static bool IsAlive(CorePlayerManager player) => player != null && player.IsSpawned &&
            player.PlayerState != null && player.PlayerState.IsActive;

        public static void ReleaseCoop(CorePlayerManager player)
        {
            if (!player.IsOwner) return;
            if (player.TryGetComponent<CoopHoldHandAbility>(out var hand)) hand.SetHoldingHandsDirect(false);
            if (player.TryGetComponent<CoopBoostThrowAbility>(out var boost))
            {
                boost.CancelForRespawn();
                boost.enabled = false;
            }
        }

        public static void Respawn(CorePlayerManager player, Transform checkpoint)
        {
            if (!player.IsOwner || checkpoint == null) return;
            player.CoreMovement.ResetMovementForces();
            player.CoreMovement.transform.rotation = checkpoint.rotation;
            player.CoreMovement.SetPosition(checkpoint.position);
            if (player.CoreStats != null)
                player.CoreStats.ModifyStat(StatKeys.Health, player.CoreStats.GetMaxValue(StatKeys.Health));
            player.PlayerState.SetLifeState(PlayerLifeState.Respawned);
            if (player.TryGetComponent<CoopBoostThrowAbility>(out var boost)) boost.enabled = true;
        }
    }
}
