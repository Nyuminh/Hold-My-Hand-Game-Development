using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>Three sequential sun/moon plate pairs unlock the existing Zephyr bridge.</summary>
    [RequireComponent(typeof(NetworkObject))]
    public class RuneLightBridgeController : NetworkBehaviour
    {
        [System.Serializable]
        public class RunePair
        {
            public Transform sunPlate;
            public Renderer sunRenderer;
            public Light sunLight;
            public Transform moonPlate;
            public Renderer moonRenderer;
            public Light moonLight;
        }

        [Header("Three pairs, in order")]
        [Tooltip("Assign exactly three pairs. The plate transform is the centre of its activation area.")]
        public RunePair[] pairs = new RunePair[3];
        [Min(0.01f)] public float plateRadius = 1.5f;
        [Min(0.01f)] public float plateHeightTolerance = 2f;
        [Tooltip("Offline only: a single player may activate both roles.")]
        public bool allowSoloFallback = true;

        [Header("Existing bridge and optional blocking gate")]
        [Tooltip("Bridge walking colliders are enabled only when the puzzle is complete.")]
        public Collider[] bridgeColliders;
        [Tooltip("Bridge visual renderers are enabled only when the puzzle is complete.")]
        public Renderer[] bridgeRenderers;
        [Tooltip("Optional barrier colliders are disabled when the bridge opens.")]
        public Collider[] gateColliders;
        [Tooltip("Optional barrier renderers are hidden when the bridge opens.")]
        public Renderer[] gateRenderers;

        [Header("Rune appearance")]
        public Color sunColor = new Color(1f, 0.65f, 0.12f);
        public Color moonColor = new Color(0.25f, 0.65f, 1f);
        [Min(0f)] public float emissionIntensity = 2f;

        [Header("Completion dialogue")]
        public string completionSpeaker = "Ba";
        [TextArea] public string completionLine = "Ba con mình phối hợp đỉnh thật đấy!";
        [Min(0f)] public float completionDialogDuration = 4f;

        // A single replicated stage avoids inconsistent intermediate states on late join.
        // 0..5: sun then moon for each pair; 6: bridge open.
        private readonly NetworkVariable<int> netStage = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private int offlineStage;
        private MaterialPropertyBlock propertyBlock;
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public int CompletedPairs => Mathf.Clamp(CurrentStage / 2, 0, 3);
        public bool IsComplete => CurrentStage >= 6;
        private int CurrentStage => IsSpawned ? netStage.Value : offlineStage;
        private bool NetworkListening => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        private void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            ApplyState(0);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            netStage.OnValueChanged += OnStageChanged;
            // Initial replicated state is already available here, including for late joiners.
            ApplyState(netStage.Value);
        }

        public override void OnNetworkDespawn()
        {
            netStage.OnValueChanged -= OnStageChanged;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            // Never run a local simulation while a network session is starting or active.
            if (NetworkListening && (!IsSpawned || !IsServer)) return;
            if (IsSpawned && !IsServer) return;

            int stage = CurrentStage;
            if (stage >= 6 || pairs == null || pairs.Length < 3) return;
            RunePair pair = pairs[stage / 2];
            if (pair == null) return;
            Transform plate = stage % 2 == 0 ? pair.sunPlate : pair.moonPlate;
            if (plate == null || !IsEligiblePlayerOnPlate(plate, stage % 2 == 0)) return;

            if (IsSpawned)
                netStage.Value = stage + 1;
            else
            {
                offlineStage = stage + 1;
                ApplyState(offlineStage);
                if (offlineStage == 6) ShowCompletionDialog();
            }
        }

        private bool IsEligiblePlayerOnPlate(Transform plate, bool requiresDad)
        {
            int activeCount = 0;
            PlayerRoleVisuals onlyPlayer = null;
            foreach (PlayerRoleVisuals player in PlayerRoleVisuals.AllPlayers)
            {
                if (player == null || !player.isActiveAndEnabled || !player.IsSpawned) continue;
                activeCount++;
                onlyPlayer = player;
                if (player.IsDad == requiresDad && IsOnPlate(player.transform, plate)) return true;
            }

            if (NetworkListening || !allowSoloFallback) return false;
            // Offline PlayerRoleVisuals instances are not registered in AllPlayers.
            if (activeCount == 0)
            {
                PlayerRoleVisuals[] players = FindObjectsByType<PlayerRoleVisuals>(FindObjectsSortMode.None);
                foreach (PlayerRoleVisuals player in players)
                {
                    if (player == null || !player.isActiveAndEnabled) continue;
                    activeCount++;
                    onlyPlayer = player;
                    if (player.IsDad == requiresDad && IsOnPlate(player.transform, plate)) return true;
                }
            }
            if (activeCount == 1 && onlyPlayer != null) return IsOnPlate(onlyPlayer.transform, plate);
            if (activeCount != 0) return false;
            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            return taggedPlayers.Length == 1 && IsOnPlate(taggedPlayers[0].transform, plate);
        }

        private bool IsOnPlate(Transform player, Transform plate)
        {
            Vector3 delta = player.position - plate.position;
            return Mathf.Abs(delta.y) <= plateHeightTolerance &&
                   delta.x * delta.x + delta.z * delta.z <= plateRadius * plateRadius;
        }

        private void OnStageChanged(int previous, int current)
        {
            ApplyState(current);
            if (previous < 6 && current >= 6 && IsClient) ShowCompletionDialog();
        }

        private void ApplyState(int stage)
        {
            if (pairs != null)
            {
                for (int i = 0; i < Mathf.Min(3, pairs.Length); i++)
                {
                    RunePair pair = pairs[i];
                    if (pair == null) continue;
                    SetRune(pair.sunRenderer, pair.sunLight, stage > i * 2, sunColor);
                    SetRune(pair.moonRenderer, pair.moonLight, stage > i * 2 + 1, moonColor);
                }
            }
            bool open = stage >= 6;
            SetColliders(bridgeColliders, open);
            SetRenderers(bridgeRenderers, open);
            SetColliders(gateColliders, !open);
            SetRenderers(gateRenderers, !open);
        }

        private void SetRune(Renderer target, Light lightSource, bool lit, Color color)
        {
            if (lightSource != null) lightSource.enabled = lit;
            if (target == null) return;
            target.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColor, lit ? color * emissionIntensity : Color.black);
            target.SetPropertyBlock(propertyBlock);
        }

        private static void SetColliders(Collider[] targets, bool enabled)
        {
            if (targets == null) return;
            foreach (Collider target in targets) if (target != null) target.enabled = enabled;
        }

        private static void SetRenderers(Renderer[] targets, bool enabled)
        {
            if (targets == null) return;
            foreach (Renderer target in targets) if (target != null) target.enabled = enabled;
        }

        private void ShowCompletionDialog()
        {
            if (PlayScene3ObjectiveDirector.Instance != null && !string.IsNullOrEmpty(completionLine))
                PlayScene3ObjectiveDirector.Instance.ShowDialog(completionSpeaker, completionLine, completionDialogDuration);
        }
    }
}
