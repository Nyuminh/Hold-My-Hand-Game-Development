using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class MudslideRunTrigger : LevelOneObstacle
    {
        [Range(0.05f,1f)] public float childSpeedMultiplier = 0.4f;
        private CoreMovement affected;
        private Func<Vector3,Vector3> previous, mudMove;
        protected override string QuestKey => "mud";
        public override bool Solved => Phase == 2;
        protected override bool UsesInteract => false;
        public override bool TryInteract(CorePlayerManager player) => false;

        protected override void ServerTick() { if (!Solved && BothBeyondExit()) phase.Value = 2; }

        protected override void Present()
        {
            var child = FindRole(false);
            bool inside = !Solved && child != null && child.IsOwner && Contains(child.transform.position) && boss.State == ChaseState.Chasing;
            if (!inside) { Cleanup(); return; }
            if (affected == child.CoreMovement) return;
            Cleanup();
            affected = child.CoreMovement;
            previous = affected.FinalMoveCalculationOverride;
            mudMove = ApplyMud;
            affected.FinalMoveCalculationOverride = mudMove;
        }

        private Vector3 ApplyMud(Vector3 delta)
        {
            if (previous != null) delta = previous(delta);
            if (affected == null) return delta;
            var hands = affected.GetComponent<CoopHoldHandAbility>();
            var dad = FindRole(true);
            if (hands != null && hands.IsHoldingHands && dad != null && dad.TryGetComponent<CoopHoldHandAbility>(out var dadHands) && dadHands.IsHoldingHands)
            {
                var flat = Vector3.ProjectOnPlane(dad.transform.position - affected.transform.position, Vector3.up);
                if (flat.magnitude > 1.2f)
                {
                    Vector3 pull = flat.normalized * Mathf.Min(dad.CoreMovement.sprintSpeed * Time.deltaTime, flat.magnitude - 1.1f);
                    return new Vector3(pull.x, delta.y, pull.z);
                }
                return delta;
            }
            return new Vector3(delta.x * childSpeedMultiplier, delta.y, delta.z * childSpeedMultiplier);
        }

        protected override void Cleanup()
        {
            if (affected != null && affected.FinalMoveCalculationOverride == mudMove) affected.FinalMoveCalculationOverride = previous;
            affected = null; previous = null; mudMove = null;
        }
        private void OnDisable() => Cleanup();
        protected override string Hint(CorePlayerManager player) => "BÙN LÚN — Con bị giảm 60% tốc độ. Cả hai giữ [H] nắm tay; Ba giữ [Shift] kéo Con qua.";
    }
}
