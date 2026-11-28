using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.InputSystem;

namespace Blocks.Gameplay.Core
{
    public enum CourtyardQuestStep
    {
        NotStarted = 0,
        TalkToFrontGuard = 1,      // Nói chuyện với warrior_2
        TalkToRearGuard = 2,       // Đi tới cổng sau gặp warrior_1
        FindKey = 3,               // Tìm chìa khóa trên tường thành
        ReturnKeyToRearGuard = 4,  // Mang chìa khóa về cho warrior_1
        RearGateUnlocked = 5,      // Cổng sau đã mở, bước ra cầu đá
        Completed = 6              // Hoàn thành màn 1
    }

    /// <summary>
    /// Manages the post-climax courtyard questline in PlayScene1.
    /// Synchronizes quest steps, dialogue subtitles, HUD tracker, and level transition across Netcode.
    /// </summary>
    public class CastleCourtyardQuestManager : NetworkBehaviour
    {
        public static CastleCourtyardQuestManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private MonoBehaviour gateDriver;
        [SerializeField] private CastleGateClimaxManager climaxManager;

        private readonly NetworkVariable<int> currentStep = new NetworkVariable<int>((int)CourtyardQuestStep.NotStarted);
        private readonly NetworkVariable<bool> hasKey = new NetworkVariable<bool>(false);

        public CourtyardQuestStep CurrentStep => (CourtyardQuestStep)currentStep.Value;
        public bool HasKey => hasKey.Value;

        // Subtitle UI State
        private string activeSpeaker = "";
        private string activeSubtitle = "";
        private int activePage = 1;
        private int activeTotalPages = 1;
        private float subtitleTimer = 0f;
        private Coroutine subtitleCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureGateDriverRef();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            EnsureGateDriverRef();
            if (climaxManager == null)
            {
                climaxManager = Object.FindFirstObjectByType<CastleGateClimaxManager>();
            }

            currentStep.OnValueChanged += OnStepChanged;

            // If client spawns into a session where gate was already unlocked, open immediately
            if (CurrentStep >= CourtyardQuestStep.RearGateUnlocked)
            {
                ApplyRearGateState(true, immediate: true);
            }
        }

        public override void OnNetworkDespawn()
        {
            currentStep.OnValueChanged -= OnStepChanged;
            base.OnNetworkDespawn();
        }

        private void OnStepChanged(int oldStep, int newStep)
        {
            if (newStep >= (int)CourtyardQuestStep.RearGateUnlocked)
            {
                // Ensure gate is open if RPC was missed or late
                ApplyRearGateState(true, immediate: false);
            }
        }

        private void EnsureGateDriverRef()
        {
            if (gateDriver == null)
            {
                foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                {
                    if (mb is ILevelOneGateDriver)
                    {
                        gateDriver = mb;
                        break;
                    }
                }
            }
        }

        private void Update()
        {
            if (subtitleTimer > 0f)
            {
                subtitleTimer -= Time.deltaTime;
                if (subtitleTimer <= 0f)
                {
                    activeSubtitle = "";
                    activeSpeaker = "";
                }
            }
        }

        /// <summary>
        /// Initiates the courtyard questline once the front portcullis is secured.
        /// </summary>
        public void StartQuest()
        {
            if (!IsServer) return;
            if (currentStep.Value == (int)CourtyardQuestStep.NotStarted)
            {
                currentStep.Value = (int)CourtyardQuestStep.TalkToFrontGuard;
                ShowDialogueRpc("Nhiệm vụ mới", "Cổng trước đã bị chặn! Hãy tìm người trong sân lâu đài để tìm cách thoát.", 5f);
            }
        }

        public void AdvanceStep(CourtyardQuestStep nextStep)
        {
            if (!IsServer) return;
            if ((int)nextStep > currentStep.Value)
            {
                currentStep.Value = (int)nextStep;
            }
        }

        public void KeyCollected()
        {
            if (!IsServer) return;
            hasKey.Value = true;
            AdvanceStep(CourtyardQuestStep.ReturnKeyToRearGuard);
            ShowDialogueRpc("Nhặt vật phẩm", "Đã tìm thấy Chìa Khóa Cổng Sau! Mau quay lại gặp Người Gác Cổng Sau.", 5f);
        }

        public void UnlockRearGate()
        {
            if (!IsServer) return;
            AdvanceStep(CourtyardQuestStep.RearGateUnlocked);
            OpenRearGateWithAnimationRpc();
            ShowDialogueRpc("Đội Trưởng Cổng Sau", "Cổng sau đã mở! Mau bước qua cầu đá phía sau để thoát khỏi lâu đài!", 6f);
        }

        [Rpc(SendTo.Everyone)]
        public void OpenRearGateWithAnimationRpc()
        {
            ApplyRearGateState(true, immediate: false);
        }

        private void ApplyRearGateState(bool open, bool immediate)
        {
            EnsureGateDriverRef();
            if (gateDriver is ILevelOneGateDriver driver)
            {
                if (immediate)
                    driver.SetRearGateImmediate(open);
                else
                    driver.SetRearGate(open);
            }
        }

        public void ReachExit()
        {
            if (IsServer)
            {
                ProcessReachExit();
            }
            else
            {
                ReachExitServerRpc();
            }
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void ReachExitServerRpc()
        {
            ProcessReachExit();
        }

        private void ProcessReachExit()
        {
            if (CurrentStep == CourtyardQuestStep.RearGateUnlocked)
            {
                currentStep.Value = (int)CourtyardQuestStep.Completed;
                TriggerVictoryRpc();
            }
        }

        [Rpc(SendTo.Everyone)]
        public void ShowDialogueRpc(string speaker, string message, float duration)
        {
            ShowDialoguePagedRpc(speaker, message, 1, 1, duration);
        }

        [Rpc(SendTo.Everyone)]
        public void ShowDialoguePagedRpc(string speaker, string message, int page, int totalPages, float duration)
        {
            activeSpeaker = speaker;
            activeSubtitle = message;
            activePage = page;
            activeTotalPages = totalPages;
            subtitleTimer = duration;
        }

        [Rpc(SendTo.Everyone)]
        public void CloseDialogueRpc()
        {
            activeSpeaker = "";
            activeSubtitle = "";
            subtitleTimer = 0f;
        }

        [Rpc(SendTo.Everyone)]
        private void TriggerVictoryRpc()
        {
            if (climaxManager != null)
            {
                climaxManager.OnCourtyardQuestVictory();
            }
        }

        public string GetCurrentObjectiveHint()
        {
            switch (CurrentStep)
            {
                case CourtyardQuestStep.TalkToFrontGuard:
                    return "Mục tiêu: Nói chuyện với Người Gác Cổng Trước ([E])";
                case CourtyardQuestStep.TalkToRearGuard:
                    return "Mục tiêu: Đi tới Cổng Sau gặp Đội Trưởng Cổng Sau ([E])";
                case CourtyardQuestStep.FindKey:
                    return "Mục tiêu: Tìm Chìa Khóa Cổng Sau (chú ý khu vực trên Tường Thành)";
                case CourtyardQuestStep.ReturnKeyToRearGuard:
                    return "Mục tiêu: Đem Chìa Khóa giao cho Đội Trưởng Cổng Sau ([E])";
                case CourtyardQuestStep.RearGateUnlocked:
                    return "Mục tiêu: Bước qua Cổng Sau và cầu đá để rời khỏi Lâu Đài!";
                case CourtyardQuestStep.Completed:
                    return "ĐÃ HOÀN THÀNH MÀN 1!";
                default:
                    return "";
            }
        }

        private void OnGUI()
        {
            if (CurrentStep == CourtyardQuestStep.NotStarted || CurrentStep == CourtyardQuestStep.Completed)
                return;

            // 1. Objective HUD Tracker (Top-Center)
            string objective = GetCurrentObjectiveHint();
            if (!string.IsNullOrEmpty(objective))
            {
                float hudWidth = 580f;
                float hudHeight = 36f;
                Rect hudRect = new Rect((Screen.width - hudWidth) / 2f, 20f, hudWidth, hudHeight);

                GUI.color = new Color(0f, 0f, 0f, 0.75f);
                GUI.DrawTexture(hudRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var hudStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1f, 0.9f, 0.4f) }
                };
                GUI.Label(hudRect, objective, hudStyle);
            }

            // 2. Subtitle Dialogue Box (Bottom-Center)
            if (!string.IsNullOrEmpty(activeSubtitle))
            {
                float subWidth = 850f;
                float subHeight = 110f;
                Rect subRect = new Rect((Screen.width - subWidth) / 2f, Screen.height - subHeight - 45f, subWidth, subHeight);

                GUI.color = new Color(0.05f, 0.08f, 0.12f, 0.92f);
                GUI.DrawTexture(subRect, Texture2D.whiteTexture);
                GUI.color = Color.white;

                var speakerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 17,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.95f, 0.75f, 0.25f) }
                };
                GUI.Label(new Rect(subRect.x + 20f, subRect.y + 10f, subWidth - 40f, 24f), activeSpeaker, speakerStyle);

                var textStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    wordWrap = true,
                    normal = { textColor = Color.white }
                };
                GUI.Label(new Rect(subRect.x + 20f, subRect.y + 36f, subWidth - 40f, 65f), activeSubtitle, textStyle);

                if (activeTotalPages > 1)
                {
                    string pageHint = activePage < activeTotalPages
                        ? $"[E] Tiếp tục ▶ ({activePage}/{activeTotalPages})"
                        : $"[E] Đóng ✓ ({activePage}/{activeTotalPages})";

                    var pageStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleRight,
                        fontSize = 13,
                        fontStyle = FontStyle.Bold,
                        normal = { textColor = new Color(0.3f, 0.85f, 1f) }
                    };
                    GUI.Label(new Rect(subRect.x + subWidth - 240f, subRect.y + subHeight - 26f, 220f, 20f), pageHint, pageStyle);
                }
            }
        }
    }
}
