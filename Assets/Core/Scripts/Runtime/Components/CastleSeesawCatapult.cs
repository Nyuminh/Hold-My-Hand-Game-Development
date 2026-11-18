using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>A one-shot, server-authoritative seesaw. Players stand at the authored points;
    /// Dad must jump and land at the slam point while Child is at the seat.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class CastleSeesawCatapult : NetworkBehaviour
    {
        [Header("Catapult")]
        [SerializeField] private Transform childSeatPoint;
        [SerializeField] private Transform dadSlamPoint;
        [SerializeField] private Transform seesawBeam;
        [SerializeField] private Transform launchTargetLanding;
        [SerializeField, Min(0.1f)] private float childSeatRadius = 1.8f;
        [SerializeField, Min(0.1f)] private float dadSlamRadius = 2.5f;
        [SerializeField, Min(0.1f)] private float flightDuration = 1.35f;
        [SerializeField] private float initialTilt = -15f;
        [SerializeField] private float slammedTilt = 22f;
        [SerializeField, Min(0.01f)] private float beamSlamDuration = 0.12f;

        [Header("Feedback")]
        [SerializeField] private AudioSource slamAudio;
        [SerializeField] private ParticleSystem slamParticles;
        [SerializeField] private string dialogueSpeaker = "Ba";
        [SerializeField, TextArea] private string dialogueLine = "Bám chắc vào nha con yêu!";
        [SerializeField, Min(0f)] private float dialogueDuration = 3.5f;

        private readonly NetworkVariable<bool> m_Launched = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private bool m_OfflineLaunched;
        private bool m_WasAirborne;
        private bool m_Descending;
        private Quaternion m_BeamBaseRotation;
        private float m_BeamProgress;

        private bool NetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public bool IsLaunched => IsSpawned ? m_Launched.Value : m_OfflineLaunched;

        private void Awake()
        {
            if (seesawBeam != null) m_BeamBaseRotation = seesawBeam.localRotation;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            m_Launched.OnValueChanged += OnLaunchChanged;
            // Late joiners see the final pose, not a replay of the sound or dialogue.
            m_BeamProgress = m_Launched.Value ? 1f : 0f;
            ApplyBeam();
        }

        public override void OnNetworkDespawn()
        {
            m_Launched.OnValueChanged -= OnLaunchChanged;
            m_WasAirborne = false;
            m_Descending = false;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsLaunched)
            {
                m_BeamProgress = Mathf.MoveTowards(m_BeamProgress, 1f, Time.deltaTime / Mathf.Max(0.01f, beamSlamDuration));
                ApplyBeam();
                return;
            }
            ApplyBeam();
            // Never treat an unspawned scene object as offline in a live session.
            if (NetworkActive && !IsSpawned) return;

            var dad = FindLocalDad();
            var motor = dad != null ? dad.GetComponent<CoreMovement>() : null;
            if (motor == null || dadSlamPoint == null)
            {
                m_WasAirborne = false;
                m_Descending = false;
                return;
            }

            if (!motor.IsGrounded)
            {
                m_WasAirborne = true;
                if (motor.VerticalVelocity < -0.1f) m_Descending = true;
                return;
            }

            bool landed = m_WasAirborne && m_Descending;
            m_WasAirborne = false;
            m_Descending = false;
            if (!landed || !Near(dad.transform, dadSlamPoint, dadSlamRadius)) return;
            if (IsSpawned) RequestSlamRpc();
            else TryOfflineLaunch(dad);
        }

        private CoopBoostThrowAbility FindLocalDad()
        {
            if (NetworkActive)
            {
                var manager = NetworkManager.Singleton;
                var player = manager.LocalClient != null ? manager.LocalClient.PlayerObject : null;
                if (player == null || !player.IsOwner) return null;
                var role = player.GetComponent<PlayerRoleVisuals>();
                return role != null && role.IsDad ? player.GetComponent<CoopBoostThrowAbility>() : null;
            }

            foreach (var ability in FindObjectsByType<CoopBoostThrowAbility>(FindObjectsSortMode.None))
                if (ability != null && ability.GetComponent<SoloCompanionDummy>() == null && ability.IsDad) return ability;
            return null;
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestSlamRpc(RpcParams rpc = default)
        {
            if (!IsServer || m_Launched.Value || !HasReferences()) return;
            if (!NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var sender)) return;
            var dad = sender.PlayerObject;
            if (dad == null || !dad.IsSpawned || dad.OwnerClientId != rpc.Receive.SenderClientId) return;
            var dadRole = dad.GetComponent<PlayerRoleVisuals>();
            if (dadRole == null || !dadRole.IsDad || !Near(dad.transform, dadSlamPoint, dadSlamRadius)) return;

            foreach (var client in NetworkManager.ConnectedClientsList)
            {
                var child = client.PlayerObject;
                if (child == null || child == dad || !child.IsSpawned || child.OwnerClientId != client.ClientId) continue;
                var role = child.GetComponent<PlayerRoleVisuals>();
                var ability = child.GetComponent<CoopBoostThrowAbility>();
                if (role == null || role.IsDad || ability == null || !ability.IsSpawned || !Near(child.transform, childSeatPoint, childSeatRadius)) continue;
                CalculateLaunch(child.transform.position, child.GetComponent<CoreMovement>(), out var direction, out var vertical, out var horizontal);
                m_Launched.Value = true;
                ability.ReceiveBoostLaunchRpc(direction, vertical, horizontal);
                return;
            }
        }

        private void TryOfflineLaunch(CoopBoostThrowAbility dad)
        {
            if (m_OfflineLaunched || !HasReferences()) return;
            foreach (var child in FindObjectsByType<CoopBoostThrowAbility>(FindObjectsSortMode.None))
            {
                if (child == null || child == dad || child.IsDad || !Near(child.transform, childSeatPoint, childSeatRadius)) continue;
                CalculateLaunch(child.transform.position, child.GetComponent<CoreMovement>(), out var direction, out var vertical, out var horizontal);
                m_OfflineLaunched = true;
                PlayFeedback();
                child.ApplyLaunchLocally(direction, vertical, horizontal);
                return;
            }
        }

        private bool HasReferences() => childSeatPoint != null && dadSlamPoint != null && launchTargetLanding != null;

        private static bool Near(Transform player, Transform point, float radius)
        {
            return player != null && point != null && (player.position - point.position).sqrMagnitude <= radius * radius;
        }

        private void CalculateLaunch(Vector3 start, CoreMovement motor, out Vector3 direction, out float vertical, out float horizontal)
        {
            Vector3 delta = launchTargetLanding.position - start;
            Vector3 flat = Vector3.ProjectOnPlane(delta, Vector3.up);
            float duration = Mathf.Clamp(flightDuration, 0.1f, 3.4f);
            float gravity = motor != null ? Mathf.Max(1f, Mathf.Abs(motor.gravity)) : 15f;
            direction = flat.sqrMagnitude > 0.0001f ? flat.normalized : Vector3.zero;
            vertical = (delta.y + 0.5f * gravity * duration * duration) / duration;
            // CoopBoostThrowAbility reduces horizontal velocity by 4% per second.
            horizontal = flat.magnitude / (duration - 0.02f * duration * duration);
        }

        private void OnLaunchChanged(bool previous, bool current)
        {
            if (!previous && current) PlayFeedback();
        }

        private void PlayFeedback()
        {
            if (slamAudio != null) slamAudio.Play();
            if (slamParticles != null) slamParticles.Play();
            if (!string.IsNullOrEmpty(dialogueLine))
                PlayScene3ObjectiveDirector.Instance?.ShowDialog(dialogueSpeaker, dialogueLine, dialogueDuration);
        }

        private void ApplyBeam()
        {
            if (seesawBeam != null)
                seesawBeam.localRotation = m_BamRotation();
        }

        private Quaternion m_BamRotation()
        {
            return m_BeamBaseRotation * Quaternion.Euler(Mathf.Lerp(initialTilt, slammedTilt, m_BeamProgress), 0f, 0f);
        }
    }
}
