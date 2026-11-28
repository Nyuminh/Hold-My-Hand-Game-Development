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
    /// Handles proximity detection, multi-line sequence dialogues with [E] continuation,
    /// reminder dialogues, and quest progression.
    /// </summary>
    public class CastleCourtyardNPC : NetworkBehaviour
    {
        [Header("NPC Role & Identity")]
        [SerializeField] private CourtyardNPCRole role;
        [SerializeField] private string npcTitle = "Vệ Binh";
        [SerializeField] private float interactionDistance = 3.5f;

        private Transform localPlayerTransform;
        private bool isPlayerNearby;
        private float nextInputTime;

        // Dialogue sequence state
        private bool isConversing;
        private string[] activeLines;
        private int lineIndex;

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
                if (isConversing) CancelConversation();
                return;
            }

            float dist = Vector3.Distance(transform.position, localPlayerTransform.position);
            isPlayerNearby = dist <= interactionDistance;

            if (isConversing && dist > interactionDistance + 2f)
            {
                CancelConversation();
            }

            bool interactPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                || (Gamepad.current != null && (Gamepad.current.buttonWest.wasPressedThisFrame || Gamepad.current.buttonSouth.wasPressedThisFrame));

            if (isPlayerNearby && interactPressed)
            {
                if (Time.time >= nextInputTime)
                {
                    nextInputTime = Time.time + 0.25f;
                    InteractServerRpc();
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

        private void CancelConversation()
        {
            isConversing = false;
            activeLines = null;
            lineIndex = 0;
            var quest = CastleCourtyardQuestManager.Instance;
            if (quest != null)
            {
                quest.CloseDialogueRpc();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void InteractServerRpc()
        {
            var quest = CastleCourtyardQuestManager.Instance;
            if (quest == null) return;

            if (!isConversing)
            {
                // Begin new dialogue sequence based on current role and quest step
                activeLines = GetDialogueLinesForCurrentState(quest);
                if (activeLines == null || activeLines.Length == 0) return;

                isConversing = true;
                lineIndex = 0;
                quest.ShowDialoguePagedRpc(npcTitle, activeLines[lineIndex], lineIndex + 1, activeLines.Length, 25f);
            }
            else
            {
                // Advance to next line or finish
                lineIndex++;
                if (activeLines != null && lineIndex < activeLines.Length)
                {
                    quest.ShowDialoguePagedRpc(npcTitle, activeLines[lineIndex], lineIndex + 1, activeLines.Length, 25f);
                }
                else
                {
                    // Completed dialogue sequence
                    FinishConversation(quest);
                }
            }
        }

        private string[] GetDialogueLinesForCurrentState(CastleCourtyardQuestManager quest)
        {
            var step = quest.CurrentStep;

            if (role == CourtyardNPCRole.FrontGateGuard)
            {
                if (step <= CourtyardQuestStep.TalkToFrontGuard)
                {
                    return new string[]
                    {
                        "Ơn trời, các bạn đã vào được bên trong an toàn!",
                        "Nhưng quái vật ngoài kia đã đâm sập chốt cổng trước rồi, chúng ta không thể quay lại bằng đường đó đâu! Mau chạy về phía Cổng Sau gặp Người Gác Cổng để tìm đường rời khỏi đây!"
                    };
                }
                else
                {
                    return new string[]
                    {
                        "Hãy mau đến Cổng Sau ở phía cuối sân thành! Người gác cổng ở đó sẽ chỉ cách cho các bạn."
                    };
                }
            }
            else if (role == CourtyardNPCRole.RearGateGuard)
            {
                if (step <= CourtyardQuestStep.TalkToRearGuard)
                {
                    // Initial conversation: Hint about key on castle walls + safety warning
                    return new string[]
                    {
                        "Cổng sau đã bị khóa chặt để ngăn lũ quái vật bên ngoài tràn vào lâu đài!",
                        "Lúc nãy tôi có trèo lên tường thành quan sát quái vật... hình như đã sơ ý đánh rơi Chùm Chìa Khóa ở trên đó!",
                        "Hai người hãy trèo lên các bậc thang dẫn lên tường thành tìm giúp tôi! Nhưng nhớ tuyệt đối đừng để rơi ra ngoài thành, quái vật đang lảng vảng ngay dưới chân tường đấy!",
                        "Tìm được chìa khóa đem lại đây, tôi sẽ lập tức mở cổng sau cho hai người ra ngoài an toàn!"
                    };
                }
                else if (step == CourtyardQuestStep.FindKey)
                {
                    // Reminder conversation: Player accepted quest but hasn't found key yet
                    return new string[]
                    {
                        "Hai người vẫn chưa tìm thấy sao? Hãy men theo cầu thang trèo lên tường thành tìm kỹ quanh các góc tường nhé!",
                        "Có chìa khóa tôi mới mở cổng cho hai người thoát ra ngoài được. Nhớ chú ý an toàn, đừng nhảy ra ngoài thành!"
                    };
                }
                else if (step == CourtyardQuestStep.ReturnKeyToRearGuard)
                {
                    // Turn in key conversation
                    return new string[]
                    {
                        "Các bạn tìm thấy rồi sao? Đúng là chìa khóa cổng sau rồi, cảm ơn hai người rất nhiều!",
                        "Mau lùi lại một chút, tôi mở cổng sau ngay đây! Chúc hai cha con lên đường bình an!"
                    };
                }
                else
                {
                    // Gate already unlocked
                    return new string[]
                    {
                        "Cổng đã mở rồi! Mau bước qua cầu đá phía sau để rời khỏi lâu đài!"
                    };
                }
            }

            return null;
        }

        private void FinishConversation(CastleCourtyardQuestManager quest)
        {
            var step = quest.CurrentStep;
            isConversing = false;
            activeLines = null;
            lineIndex = 0;
            quest.CloseDialogueRpc();

            if (role == CourtyardNPCRole.FrontGateGuard)
            {
                if (step <= CourtyardQuestStep.TalkToFrontGuard)
                {
                    quest.AdvanceStep(CourtyardQuestStep.TalkToRearGuard);
                }
            }
            else if (role == CourtyardNPCRole.RearGateGuard)
            {
                if (step <= CourtyardQuestStep.TalkToRearGuard)
                {
                    quest.AdvanceStep(CourtyardQuestStep.FindKey);
                }
                else if (step == CourtyardQuestStep.ReturnKeyToRearGuard)
                {
                    quest.UnlockRearGate();
                }
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

            string promptText = isConversing ? "[E] Tiếp tục hội thoại" : $"[E] Nói chuyện với {npcTitle}";
            GUI.Label(rect, promptText, style);
        }
    }
}
