using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    public sealed class CollapsingPlatform : NetworkBehaviour
    {
        public Transform stone;
        public BoxCollider tread;
        public float delay = 1.5f;
        readonly NetworkVariable<double> started = new(-1);
        Vector3 origin;
        void Awake() => origin = stone.localPosition;
        public void Restore() { if (IsServer) started.Value = -1; }
        void Update()
        {
            if (!IsSpawned) return;
            if (IsServer && started.Value < 0)
            {
                var area = tread.bounds;
                foreach (var actor in LevelTwoActors.All)
                {
                    var foot = actor.transform.position;
                    if (Mathf.Abs(foot.y - area.max.y) < .3f && foot.x >= area.min.x && foot.x <= area.max.x && foot.z >= area.min.z && foot.z <= area.max.z)
                    { started.Value = NetworkManager.ServerTime.Time; break; }
                }
            }
            float age = started.Value < 0 ? 0 : Mathf.Max(0, (float)(NetworkManager.ServerTime.Time - started.Value));
            float fall = Mathf.Max(0, age - delay);
            stone.localPosition = origin + Vector3.right * (fall == 0 ? Mathf.Sin(age * 42) * .045f : 0) + Vector3.down * Mathf.Min(30, 6 * fall * fall);
            stone.gameObject.SetActive(fall < 2.5f);
        }
    }
}
