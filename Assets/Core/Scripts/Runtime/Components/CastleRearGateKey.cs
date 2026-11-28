using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Controls the collectible rear gate key placed on the castle wall.
    /// Handles idle floating/rotation, glow aura, [E] interaction, and network replication.
    /// </summary>
    public class CastleRearGateKey : NetworkBehaviour
    {
        [Header("Animation Settings")]
        [SerializeField] private float rotationSpeed = 60f;
        [SerializeField] private float bobHeight = 0.2f;
        [SerializeField] private float bobFrequency = 2f;
        [SerializeField] private float interactionDistance = 2.5f;

        [Header("Visual Effects")]
        [SerializeField] private Light auraLight;

        private Vector3 startPosition;
        private Transform localPlayerTransform;
        private bool isPlayerNearby;
        private bool isCollected;

        private void Start()
        {
            startPosition = transform.position;

            // Ensure a warm golden point light exists for visibility on the battlement
            if (auraLight == null)
            {
                auraLight = GetComponentInChildren<Light>();
                if (auraLight == null)
                {
                    var lightObj = new GameObject("Key_AuraLight");
                    lightObj.transform.SetParent(transform, false);
                    lightObj.transform.localPosition = Vector3.up * 0.3f;
                    auraLight = lightObj.AddComponent<Light>();
                    auraLight.type = LightType.Point;
                    auraLight.color = new Color(1f, 0.85f, 0.3f);
                    auraLight.range = 6f;
                    auraLight.intensity = 2.5f;
                }
            }
        }

        private void Update()
        {
            if (isCollected) return;

            // 1. Idle animation (spin and bob)
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
            float newY = startPosition.y + Mathf.Sin(Time.time * bobFrequency) * bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);

            // 2. Interaction detection
            FindLocalPlayer();
            if (localPlayerTransform == null)
            {
                isPlayerNearby = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, localPlayerTransform.position);
            isPlayerNearby = dist <= interactionDistance;

            bool interactPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame));

            if (isPlayerNearby && interactPressed)
            {
                var quest = CastleCourtyardQuestManager.Instance;
                // Only allow pickup if quest has reached FindKey stage or earlier
                if (quest != null && quest.CurrentStep >= CourtyardQuestStep.FindKey)
                {
                    CollectKeyServerRpc();
                }
            }
        }

        private void FindLocalPlayer()
        {
            if (localPlayerTransform != null) return;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
            {
                localPlayerTransform = NetworkManager.Singleton.LocalClient.PlayerObject.transform;
                return;
            }

            var players = Object.FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                if (p != null && p.IsOwner)
                {
                    localPlayerTransform = p.transform;
                    break;
                }
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void CollectKeyServerRpc()
        {
            if (isCollected) return;
            isCollected = true;

            var quest = CastleCourtyardQuestManager.Instance;
            if (quest != null)
            {
                quest.KeyCollected();
            }

            HideKeyRpc();
        }

        [Rpc(SendTo.Everyone)]
        private void HideKeyRpc()
        {
            isCollected = true;
            isPlayerNearby = false;

            // Disable renderers and colliders
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                r.enabled = false;
            }
            foreach (var c in GetComponentsInChildren<Collider>())
            {
                c.enabled = false;
            }
            if (auraLight != null)
            {
                auraLight.enabled = false;
            }
        }

        private void OnGUI()
        {
            if (isCollected || !isPlayerNearby) return;

            var quest = CastleCourtyardQuestManager.Instance;
            if (quest != null && quest.CurrentStep < CourtyardQuestStep.FindKey) return;

            float promptWidth = 320f;
            float promptHeight = 35f;
            Rect rect = new Rect((Screen.width - promptWidth) / 2f, Screen.height - 165f, promptWidth, promptHeight);

            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) }
            };
            GUI.Label(rect, "[E] Nhặt Chìa Khóa Cổng Sau", style);
        }
    }
}
