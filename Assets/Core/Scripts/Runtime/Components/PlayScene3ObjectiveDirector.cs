using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Bộ Chỉ Hướng Mục Tiêu & La Bàn Dẫn Đường cho PlayScene3 (Màn 3).
    /// Hiển thị:
    /// 1. Cột hải đăng ánh sáng 3D (Objective Beacon Pillar) chiếu thẳng lên trời tại vị trí mục tiêu hiện tại.
    /// 2. Mũi tên la bàn (Compass Needle) tự động xoay theo góc nhìn của người chơi cục bộ về phía mục tiêu.
    /// 3. Thẻ mục tiêu (Objective Card) hiển thị gợi ý phối hợp cho Ba & Con và khoảng cách (mét).
    /// </summary>
    [SelectionBase]
    public class PlayScene3ObjectiveDirector : MonoBehaviour
    {
        private static PlayScene3ObjectiveDirector instance;
        public static PlayScene3ObjectiveDirector Instance => instance;

        [Serializable]
        public class LevelStageObjective
        {
            public string id;
            public string title;
            public string hint;
            public Vector3 targetPosition;
            public float completeRadius;
        }

        [Header("Stage Objectives")]
        [SerializeField] private List<LevelStageObjective> objectives = new List<LevelStageObjective>();
        [SerializeField] private int currentStageIndex = 0;

        [Header("3D Beacon")]
        [SerializeField] private GameObject beaconRoot;
        [SerializeField] private Color beaconColor = new Color(1f, 0.82f, 0.20f, 0.85f);

        [Header("UI Document (Optional)")]
        [SerializeField] private UIDocument uiDocument;

        private VisualElement compassArrowElement;
        private Label titleLabelElement;
        private Label hintLabelElement;

        private float pollTimer = 0f;
        private GUIStyle cardBoxStyle;
        private GUIStyle cardTitleStyle;
        private GUIStyle cardHintStyle;
        private GUIStyle compassIconStyle;

        private void Awake()
        {
            if (instance == null) instance = this;
            InitializeObjectives();
        }

        private void Start()
        {
            EnsureBeaconCreated();
            BindUIDocument();
            UpdateBeaconPosition();
        }

        private void InitializeObjectives()
        {
            if (objectives.Count == 0)
            {
                objectives.Add(new LevelStageObjective
                {
                    id = "zone1_bridge",
                    title = "🎯 Mục Tiêu 1: Hạ Cầu Rút Vực Đá (Zone 1)",
                    hint = "👨 Ba gạt cần trái, 👦 Con gạt cần phải cùng lúc để hạ cầu qua vực!",
                    targetPosition = new Vector3(-14.05f, 7.00f, -0.50f),
                    completeRadius = 8.0f
                });

                objectives.Add(new LevelStageObjective
                {
                    id = "zone2_crane",
                    title = "🎯 Mục Tiêu 2: Thang Cẩu & Đối Trọng Đá (Zone 2)",
                    hint = "👨 Ba xoay tời hạ đối trọng đưa Con qua. 👦 Con sửa hộp điện/đẩy đá đón Ba!",
                    targetPosition = new Vector3(-10.76f, 8.68f, -13.62f),
                    completeRadius = 8.0f
                });

                objectives.Add(new LevelStageObjective
                {
                    id = "zone3_turntable",
                    title = "🎯 Mục Tiêu 3: Bàn Xoay Bánh Răng Đá (Zone 3)",
                    hint = "👦 Con gỡ chốt kẹt bánh răng đá, 👨 Ba quay cần tời mở lối sang Zone 4!",
                    targetPosition = new Vector3(0.13f, 14.65f, -60.00f),
                    completeRadius = 8.0f
                });

                objectives.Add(new LevelStageObjective
                {
                    id = "zone3_zipline",
                    title = "🎯 Mục Tiêu 4: Đường Trượt Zipline Sang Vịnh Thuyền Đắm",
                    hint = "Cả hai cùng tiến đến bục cáp Zipline, nhấn [E] bám thanh trượt bay sang Tàu Đắm!",
                    targetPosition = new Vector3(-20.53f, 13.67f, -69.51f),
                    completeRadius = 8.0f
                });

                objectives.Add(new LevelStageObjective
                {
                    id = "zone5_portal",
                    title = "🎯 Mục Tiêu 5: Cổng Cổ Đại Vịnh Thuyền Đắm",
                    hint = "Hai Cha Con cùng bước lên bệ đá kích hoạt cổng hoàn thành ải!",
                    targetPosition = new Vector3(39.50f, 5.80f, -53.50f),
                    completeRadius = 6.0f
                });
            }
        }

        private void EnsureBeaconCreated()
        {
            // Bỏ hoàn toàn cột trụ 3D vật thể giữa cầu theo yêu cầu của người chơi
            var existing = GameObject.Find("PlayScene3_Beacon_Pillar");
            if (existing != null)
            {
                Destroy(existing);
            }
            beaconRoot = null;
        }

        private void BindUIDocument()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? FindAnyObjectByType<UIDocument>();
            }

            if (uiDocument != null && uiDocument.rootVisualElement != null)
            {
                var root = uiDocument.rootVisualElement;
                compassArrowElement = root.Q<VisualElement>("compass-arrow");
                titleLabelElement = root.Q<Label>("objective-title-label");
                hintLabelElement = root.Q<Label>("objective-hint-label");
            }
        }

        private void Update()
        {
            pollTimer += Time.deltaTime;
            if (pollTimer >= 0.2f)
            {
                pollTimer = 0f;
                EvaluateProgression();
            }

            UpdateCompass();
        }

        private void EvaluateProgression()
        {
            if (currentStageIndex == 0)
            {
                if (CoopDrawbridgeController.Instance != null &&
                    CoopDrawbridgeController.Instance.IsLeftPulled &&
                    CoopDrawbridgeController.Instance.IsRightPulled)
                {
                    AdvanceStage();
                }
            }
            else if (currentStageIndex == 1)
            {
                if (CraneElevatorController.Instance != null &&
                    CraneElevatorController.Instance.CurrentStage == CraneElevatorController.ElevatorStage.BothCompleted)
                {
                    AdvanceStage();
                }
            }
            else if (currentStageIndex == 2)
            {
                if (GiantGearTurntableController.Instance != null &&
                    GiantGearTurntableController.Instance.IsCompleted)
                {
                    AdvanceStage();
                }
            }
            else if (currentStageIndex == 3)
            {
                if (Stage3ZiplineCoopController.Instance != null &&
                    Stage3ZiplineCoopController.Instance.HasCompletedRide)
                {
                    AdvanceStage();
                }
            }
        }

        private void AdvanceStage()
        {
            if (currentStageIndex < objectives.Count - 1)
            {
                currentStageIndex++;
                UpdateBeaconPosition();
                Debug.Log($"<color=green>[ObjectiveDirector] ✅ Đã hoàn thành giai đoạn trước! Chuyển sang: {objectives[currentStageIndex].title}</color>");
            }
        }

        private void UpdateBeaconPosition()
        {
            if (currentStageIndex >= 0 && currentStageIndex < objectives.Count)
            {
                if (beaconRoot != null)
                {
                    Destroy(beaconRoot);
                    beaconRoot = null;
                }

                if (titleLabelElement != null)
                {
                    titleLabelElement.text = objectives[currentStageIndex].title;
                }
                if (hintLabelElement != null)
                {
                    hintLabelElement.text = objectives[currentStageIndex].hint;
                }
            }
        }

        private Transform GetLocalPlayerTransform()
        {
            var pTags = GameObject.FindGameObjectsWithTag("Player");
            if (pTags != null)
            {
                foreach (var p in pTags)
                {
                    if (p == null) continue;
                    var no = p.GetComponentInParent<NetworkObject>();
                    if (no != null && no.IsOwner) return p.transform;
                }
                if (pTags.Length > 0 && pTags[0] != null) return pTags[0].transform;
            }

            var dad = GameObject.Find("Dad");
            if (dad != null) return dad.transform;
            var son = GameObject.Find("Son");
            if (son != null) return son.transform;

            return Camera.main != null ? Camera.main.transform : null;
        }

        private float currentCompassAngle = 0f;
        private float distanceToTarget = 0f;

        private void UpdateCompass()
        {
            if (currentStageIndex < 0 || currentStageIndex >= objectives.Count) return;

            Transform player = GetLocalPlayerTransform();
            if (player == null) return;

            Vector3 targetPos = objectives[currentStageIndex].targetPosition;
            Vector3 toTarget = targetPos - player.position;
            distanceToTarget = toTarget.magnitude;

            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;

            Camera cam = Camera.main;
            Vector3 forward = cam != null ? cam.transform.forward : player.forward;
            forward.y = 0f;
            forward.Normalize();

            currentCompassAngle = Vector3.SignedAngle(forward, toTarget.normalized, Vector3.up);

            if (compassArrowElement != null)
            {
                compassArrowElement.style.rotate = new Rotate(new Angle(currentCompassAngle, AngleUnit.Degree));
            }
        }

        private void OnGUI()
        {
            if (currentStageIndex < 0 || currentStageIndex >= objectives.Count) return;

            var obj = objectives[currentStageIndex];

            if (cardBoxStyle == null)
            {
                cardBoxStyle = new GUIStyle(GUI.skin.box);
                cardBoxStyle.normal.background = Texture2D.whiteTexture;

                cardTitleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1f, 0.90f, 0.25f) }
                };

                cardHintStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                compassIconStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.cyan }
                };
            }

            // Thanh HUD Mục Tiêu & La Bàn ở mép trên màn hình
            float w = 560f;
            float h = 68f;
            float x = (Screen.width - w) * 0.5f;
            float y = 14f;

            Color oldCol = GUI.color;

            // Khung nền màu tối trong suốt thanh lịch
            GUI.color = new Color(0.08f, 0.10f, 0.15f, 0.88f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            // Viền vàng trên đỉnh
            GUI.color = new Color(1f, 0.82f, 0.20f, 0.90f);
            GUI.DrawTexture(new Rect(x, y, w, 2.5f), Texture2D.whiteTexture);

            // Mũi tên la bàn xoay theo góc
            int distMeters = Mathf.RoundToInt(distanceToTarget);
            string arrowSymbol = GetArrowFromAngle(currentCompassAngle);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 10f, y + 8f, 50f, 50f), arrowSymbol, compassIconStyle);

            // Tiêu đề & Gợi ý
            GUI.Label(new Rect(x + 60f, y + 8f, w - 75f, 24f), $"{obj.title} ({distMeters}m)", cardTitleStyle);
            GUI.Label(new Rect(x + 60f, y + 34f, w - 75f, 24f), obj.hint, cardHintStyle);

            GUI.color = oldCol;
        }

        private string GetArrowFromAngle(float angle)
        {
            if (angle >= -22.5f && angle < 22.5f) return "⬆️";
            if (angle >= 22.5f && angle < 67.5f) return "↗️";
            if (angle >= 67.5f && angle < 112.5f) return "➡️";
            if (angle >= 112.5f && angle < 157.5f) return "↘️";
            if (angle >= -67.5f && angle < -22.5f) return "↖️";
            if (angle >= -112.5f && angle < -67.5f) return "⬅️";
            if (angle >= -157.5f && angle < -112.5f) return "↙️";
            return "⬇️";
        }
    }
}
