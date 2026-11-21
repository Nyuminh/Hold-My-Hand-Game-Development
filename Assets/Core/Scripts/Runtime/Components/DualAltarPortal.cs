using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class DualAltarPortal : NetworkBehaviour
    {
        public Transform dadPad, childPad;
        public GameObject portal;
        readonly NetworkVariable<bool> lit = new();
        public bool Lit => lit.Value;
        public bool Armed { get; set; } = true;
        public void Restore(bool completed) { if (IsServer) lit.Value = completed; }
        void Update()
        {
            if (!IsSpawned) return;
            portal.SetActive(Lit);
            if (!IsServer || Lit || !Armed) return;
            bool dad = false, child = false;
            foreach (var actor in LevelTwoActors.All)
            {
                if (LevelOnePlayer.IsDad(actor)) dad |= LevelTwoActors.Near(actor, dadPad, 1.35f);
                else child |= LevelTwoActors.Near(actor, childPad, 1.35f);
            }
            if (dad && child) lit.Value = true;
        }
    }
}
