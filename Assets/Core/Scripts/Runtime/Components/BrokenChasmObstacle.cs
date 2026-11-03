using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class BrokenChasmObstacle : LevelOneObstacle
    {
        public Transform throwPad;
        public Transform landing;
        public Transform lever;
        public Transform bridge;
        public BoxCollider fallZone;
        public float lowerDuration = 1.2f;
        private float lowered;
        public override bool Solved => Phase == 2;

        public override bool TryInteract(CorePlayerManager player)
        {
            if (!CanInteract(player) || LevelOnePlayer.IsDad(player) || Phase != 0 || !Near(player, lever, 2f)) return false;
            var motor = player.CoreMovement;
            if (motor == null || !motor.IsGrounded) return false;
            phase.Value = 1;
            lowered = 0;
            return true;
        }

        protected override void ServerTick()
        {
            if (Phase == 1)
            {
                lowered += Time.deltaTime;
                if (lowered >= lowerDuration) phase.Value = 2;
            }
            if (boss.State != ChaseState.Chasing || fallZone == null) return;
            foreach (var player in boss.Players)
                if (LevelOnePlayer.IsAlive(player) && fallZone.bounds.Contains(player.transform.position)) { boss.CatchTeam(); break; }
        }

        protected override void Present()
        {
            var dad = FindRole(true);
            if (dad != null && dad.IsOwner && dad.TryGetComponent<CoopBoostThrowAbility>(out var boost))
                boost.AssistedLandingTarget = !Solved && Near(dad, throwPad, 2f) ? landing : null;
            if (bridge == null) return;
            bridge.localRotation = Quaternion.RotateTowards(bridge.localRotation, Quaternion.Euler(Phase == 0 ? -82f : 0f, 0f, 0f), Time.deltaTime * (82f / lowerDuration));
        }

        protected override void Cleanup()
        {
            var dad = boss != null ? FindRole(true) : null;
            if (dad != null && dad.TryGetComponent<CoopBoostThrowAbility>(out var boost) && boost.AssistedLandingTarget == landing) boost.AssistedLandingTarget = null;
        }

        protected override string Hint(CorePlayerManager player) => Solved ? "Cầu đã hạ. Ba và Con cùng qua bờ bên kia." :
            LevelOnePlayer.IsDad(player) ? "Ba: đứng trên bệ, Con đứng cạnh. Giữ [F], ngắm bờ đối diện rồi thả để ném Con qua vực 6 m." :
            "Con: để Ba ném qua vực. Tiếp đất ở bờ bên kia, tới cần tời và bấm [E] hạ cầu.";
    }
}
