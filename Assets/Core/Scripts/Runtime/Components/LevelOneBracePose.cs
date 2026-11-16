using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>Reuses BoostThrow's squash/tremble pose without starting a throw or mounting the child.</summary>
    public sealed class LevelOneBracePose : MonoBehaviour
    {
        private CorePlayerManager player;
        private Transform model;
        private Vector3 originalScale, originalPosition;
        private bool boostEnabled, handEnabled, inputEnabled;

        public void Begin(CorePlayerManager actor)
        {
            player = actor;
            var animator = actor.GetComponentInChildren<Animator>();
            if (animator != null) model = animator.transform;
            if (model != null) { originalScale = model.localScale; originalPosition = model.localPosition; }
            if (!actor.IsOwner) return;
            inputEnabled = actor.CoreInput.enabled;
            actor.CoreInput.enabled = false;
            actor.SetMovementInputEnabled(false);
            actor.CoreMovement.ResetMovementForces();
            if (actor.TryGetComponent<CoopBoostThrowAbility>(out var boost)) { boostEnabled = boost.enabled; boost.CancelForRespawn(); boost.enabled = false; }
            if (actor.TryGetComponent<CoopHoldHandAbility>(out var hand)) { handEnabled = hand.enabled; hand.SetHoldingHandsDirect(false); hand.enabled = false; }
        }

        private void LateUpdate()
        {
            if (player == null || !LevelOnePlayer.IsAlive(player)) { Destroy(this); return; }
            if (model == null) return;
            model.localScale = Vector3.Scale(originalScale, new Vector3(1.1f, 0.82f, 1.1f));
            model.localPosition = originalPosition + Vector3.right * (Mathf.Sin(Time.time * 28f) * 0.012f);
        }

        private void OnDestroy()
        {
            if (model != null) { model.localScale = originalScale; model.localPosition = originalPosition; }
            if (player == null || !player.IsOwner) return;
            player.CoreInput.enabled = inputEnabled && player.PlayerState.IsActive;
            player.SetMovementInputEnabled(player.PlayerState.IsActive);
            if (player.TryGetComponent<CoopBoostThrowAbility>(out var boost)) boost.enabled = boostEnabled;
            if (player.TryGetComponent<CoopHoldHandAbility>(out var hand)) hand.enabled = handEnabled;
        }
    }
}
