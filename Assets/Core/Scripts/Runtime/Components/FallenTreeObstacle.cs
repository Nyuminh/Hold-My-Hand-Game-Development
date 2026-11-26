using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class FallenTreeObstacle : LevelOneObstacle
    {
        public Transform trunk;
        public Transform dadGrip;
        public Transform childWedge;
        public CoopCrawlVolume crawl;
        public GameObject wedge;
        private Vector3 closedPosition;
        private LevelOneBracePose brace;
        protected override string QuestKey => "tree";
        public override bool Solved => Phase == 2;

        private void Awake() { if (trunk != null) closedPosition = trunk.localPosition; }

        public override bool TryInteract(CorePlayerManager player)
        {
            if (!CanInteract(player)) return false;
            if (LevelOnePlayer.IsDad(player) && Near(player, dadGrip) && Phase < 2)
            { phase.Value = Phase == 0 ? 1 : 0; return true; }
            if (!LevelOnePlayer.IsDad(player) && Phase == 1 && Near(player, childWedge, 1.8f))
            { phase.Value = 2; return true; }
            return false;
        }

        protected override void ServerTick()
        {
            if (Phase == 1 && !Near(FindRole(true), dadGrip, 3f)) phase.Value = 0;
        }

        protected override void Present()
        {
            if (trunk != null)
                trunk.localPosition = Vector3.MoveTowards(trunk.localPosition, closedPosition + Vector3.up * (Phase == 1 ? 1.05f : Phase == 2 ? 3f : 0f), Time.deltaTime * 3f);
            if (crawl != null) crawl.gameObject.SetActive(Phase == 1);
            if (wedge != null) wedge.SetActive(Solved);
            if (Phase == 1 && brace == null)
            {
                var dad = FindRole(true);
                if (dad != null) { brace = dad.gameObject.AddComponent<LevelOneBracePose>(); brace.Begin(dad); }
            }
            if (Phase != 1 && brace != null) { Destroy(brace); brace = null; }
        }

        protected override void Cleanup() { if (brace != null) Destroy(brace); brace = null; }
        protected override string Hint(CorePlayerManager player) => Solved ? "Đá đã chèn chắc! Cùng chạy tiếp." :
            LevelOnePlayer.IsDad(player) ? (Phase == 1 ? "Ba đang gồng giữ cây. Chờ Con chèn đá; [E] để buông." : "Ba: đến chỗ tay nắm, bấm [E] nâng cây.") :
            Phase == 1 ? "Con: chui dưới thân cây, tới đá phía bên kia và bấm [E] chèn chốt." : "Con: chờ Ba nâng cây rồi chui qua.";
    }
}
