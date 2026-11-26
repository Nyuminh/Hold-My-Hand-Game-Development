using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [CreateAssetMenu(menuName = "Hold My Hand/Skin Database")]
    public sealed class SkinDatabase : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string skinId;
            [SerializeField] private string role;
            [SerializeField] private GameObject model;

            public string SkinId => skinId;
            public string Role => role;
            public GameObject Model => model;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public static SkinDatabase Load() => Resources.Load<SkinDatabase>("SkinDatabase");

        public bool TryGet(string skinId, out Entry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate != null && candidate.Model != null &&
                    string.Equals(candidate.SkinId, skinId, StringComparison.Ordinal))
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }
    }
}
