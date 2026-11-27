using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    public enum CourtyardNPCRole
    {
        FrontGateGuard,
        RearGateGuard
    }

    /// <summary>
    /// Interactive NPC script for warrior_1 (Rear Gate) and warrior_2 (Front Gate).
    /// Handles proximity detection, [E] interaction prompt, and quest-driven dialogue.
    /// </summary>
    public class CastleCourtyardNPC : NetworkBehaviour
    {
        [Header("NPC Role & Identity")]
        [SerializeField] private CourtyardNPCRole role;
        [SerializeField] private string npcTitle = "Vệ Binh";
        [SerializeField] private float interactionDistance = 3.5f;

        private Transform localPlayerTransform;
        private bool isPlayerNearby;

        private void Start()
        {
            if (role == CourtyardNPCRole.FrontGateGuard && string.IsNullOrEmpty(npcTitle))
            {
                npcTitle = "Vệ Binh Sân Trước";
            }
            else if (role == CourtyardNPCRole.RearGateGuard && string.IsNullOrEmpty(npcTitle))
            {
                npcTitle = "Đội Trưởng Cổng Sau";
            }
        }

        private void Update()
        {
            FindLocalPlayer();

            if (localPlayerTransform == null)
            {
                isPlayerNearby = false;
                return;
            }

            float dist = Vector3.Distance(transform.position, localPlayerTransform.position);
            isPlayerNearby = dist <= interactionDistance;

            if (isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                InteractServerRpc();
            }
        }

        private void FindLocalPlayer()
        {
            if (localPlayerTransform != null) return;

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

        [Rpc(SendTo.Server)]
        private void InteractServerRpc()
        {
            var quest = CastleCourtyardQuestManager.Instance;
            if (quest == null) return;

            if (role == CourtyardNPCRole.FrontGateGuard)
            {
                HandleFrontGuardDialogue(quest);
            }
            else if (role == CourtyardNPCRole.RearGateGuard)
            {
                HandleRearGuardDialogue(quest);
            }
        }

        private void HandleFrontGuardDialogue(CastleCourtyardQuestManager quest)
        {
            var step = quest.CurrentStep;

            if (step <= CourtyardQuestStep.TalkToFrontGuard)
            {
                quest.ShowDialogueRpc(
                    npcTitle,
                    "Ơn trời, các bạn đã vào được bên trong an toàn! Nhưng con quái vật đá ngoài kia đã đâm sập và làm hỏng hoàn toàn chốt cổng trước rồi, chúng ta không thể quay lại bằng đường đó đâu! Hãy mau chạy về phía Cổng Sau gặp Người Gác Cổng để tìm đường rời khỏi đây!",
                    10f
                );
                quest.AdvanceStep(CourtyardQuestStep.TalkToRearGuard);
            }
            else
            {
                quest.ShowDialogueRpc(
                    npcTitle,
                    "Hãy mau đến Cổng Sau ở phía cuối sân thành! Người gác cổng ở đó sẽ chỉ cách cho các bạn.",
                    6f
                );
            }
        }

        private void HandleRearGuardDialogue(CastleCourtyardQuestManager quest)
        {
            var step = quest.CurrentStep;

            if (step <= CourtyardQuestStep.TalkToRearGuard)
            {
                quest.ShowDialogueRpc(
                    npcTitle,
                    "Cổng sau đã bị khóa chặt để ngăn quái vật tràn vào! Nhưng chúng tôi đã đánh mất Chìa Khóa Cổng Sau ở khu vực Thành! Hai người làm ơn tìm giúp tôi với, có chìa khóa tôi mới mở cổng cho mọi người thoát được! Nên nhớ là trong lúc tìm hãy chú ý đừng để rơi ra khỏi Thành nếu như có tìm ở khu vực trên tường thành, quái vật có thể đang lảng vảng ở phía ngoài thành đấy! Phía cổng sau này thì sẽ an toàn hơn do không có ai hoạt động phía sau thành nên quái vật thường ở phía trước!",
                    14f
                );
                quest.AdvanceStep(CourtyardQuestStep.FindKey);
            }
            else if (step == CourtyardQuestStep.FindKey)
            {
                quest.ShowDialogueRpc(
                    npcTitle,
                    "Hãy giúp tôi tìm chìa khóa, tôi sẽ mở cổng thành cho 2 người ra ngoài.",
                    5f
                );
            }
            else if (step == CourtyardQuestStep.ReturnKeyToRearGuard)
            {
                quest.ShowDialogueRpc(
                    npcTitle,
                    "Các bạn tìm thấy rồi sao? Tuyệt vời quá! Mau lùi lại một chút, tôi mở cổng sau ngay đây. Chúc hai cha con lên đường bình an!",
                    7f
                );
                quest.UnlockRearGate();
            }
            else
            {
                quest.ShowDialogueRpc(
                    npcTitle,
                    "Cổng đã mở rồi! Mau bước qua cầu đá phía sau để thoát khỏi lâu đài!",
                    5f
                );
            }
        }

        private void OnGUI()
        {
            if (!isPlayerNearby) return;

            var quest = CastleCourtyardQuestManager.Instance;
            if (quest != null && quest.CurrentStep == CourtyardQuestStep.NotStarted) return;

            // Display floating prompt near bottom center
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
            GUI.Label(rect, $"[E] Nói chuyện với {npcTitle}", style);
        }
    }
}
