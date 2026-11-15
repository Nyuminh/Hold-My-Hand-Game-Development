using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(LevelTwoFeedback))]
    public sealed class WhirlpoolHazard : MonoBehaviour
    {
        public CoopWoodenBoat boat;
        public float radius = 7f;
        public float rotationSign = 1f;
        private float nextWarning;
        public bool CanEscape => boat != null && boat.HasCoopCrew && boat.HasRecentStroke &&
            boat.IsCounterSteering(rotationSign) && Vector3.Dot(boat.GetBoatWorldForward(), EscapeDirection(boat.transform.position)) > Mathf.Cos(25 * Mathf.Deg2Rad);
        public Vector3 EscapeDirection(Vector3 position)
        {
            var outward = Vector3.ProjectOnPlane(position - transform.position, Vector3.up).normalized;
            return (outward - Vector3.Cross(Vector3.up, outward) * rotationSign).normalized;
        }
        private void FixedUpdate()
        {
            if (boat == null || !boat.HasSimulationAuthority || !boat.HasCoopCrew) return;
            var delta = Vector3.ProjectOnPlane(boat.transform.position - transform.position, Vector3.up);
            if (delta.magnitude >= radius || delta.sqrMagnitude < 0.01f) return;
            float strength = Mathf.Lerp(8f, 4f, delta.magnitude / radius);
            var radial = delta.normalized;
            var tangent = Vector3.Cross(Vector3.up, radial) * rotationSign;
            bool escape = CanEscape;
            boat.LimitWhirlpoolPropulsion(escape);
            boat.AddExternalDriftForce(escape ? EscapeDirection(boat.transform.position) * 5f : -radial * strength + tangent * 3f, Time.fixedDeltaTime);
        }
        private void Update()
        {
            if (boat != null && boat.HasCoopCrew && Vector3.Distance(boat.transform.position, transform.position) < radius * 0.65f && Time.time > nextWarning)
            {
                nextWarning = Time.time + 1.5f;
                GetComponent<LevelTwoFeedback>().Play(0.15f);
            }
        }
        private void OnGUI()
        {
            if (boat == null || Vector3.Distance(boat.transform.position, transform.position) > radius + 2) return;
            GUI.Box(new Rect(20, 155, 650, 60), "XOÁY NƯỚC! Ba: nhấn Space liên tục.\nCon: bẻ lái ngược chiều xoáy, hướng mũi thuyền chéo 45° ra ngoài.");
        }
    }
}
