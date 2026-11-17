using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Hệ thống Đạo diễn Hướng dẫn và Mũi tên dẫn đường (Level 2 Objective & Guiding System):
    /// - Quản lý tuần tự các chặng chơi của Màn 2: Thám hiểm Dòng sông (River Expedition).
    /// - Hiển thị Mũi tên 3D phát sáng (Golden Waypoint Arrow) lơ lửng, chuyển động mượt mà chỉ thẳng vào mục tiêu.
    /// - Cung cấp giao diện HUD thông báo mục tiêu trực quan kèm phím bấm tương tác.
    /// - Tự động phát hiện tiến trình thực tế từ các controller: thuyền, cổng phao, cửa cống, đài lửa, cối xay và bàn thờ.
    /// </summary>
    public class LevelTwoGuidingDirector : MonoBehaviour
    {
        public static LevelTwoGuidingDirector Instance { get; private set; }

        public enum StageStep
        {
            BoardBoat = 0,         // Bước 1: Lên thuyền gỗ tại Bến khởi hành
            SlalomGate1 = 1,       // Bước 2: Chèo qua Cổng Phao 1
            SlalomGate2 = 2,       // Bước 3: Chèo qua Cổng Phao 2
            SlalomGate3 = 3,       // Bước 4: Chèo qua Cổng Phao 3
            DockAtSluice = 4,      // Bước 5: Ghé bến Cửa Cống Thủy Lợi
            SluicePuzzle = 6,      // Bước 6: Ba giữ Tời [E], Con chui hầm gạt Van xả [E]
            NavigateRapids = 7,    // Bước 7: Chèo qua Cửa Cống & Vượt Xoáy Nước
            WestBeacon = 8,        // Bước 8: Ghé Bến Tây - Ba ném Con [F] thắp Hải Đăng Tây
            EastBeacon = 9,        // Bước 9: Ghé Bến Đông - Ba ném Con [F] thắp Hải Đăng Đông
            PassWatermill = 10,    // Bước 10: Canh nhịp Cối Xay Dừng - Tăng tốc chèo qua
            DockAtHaven = 11,      // Bước 11: Cập bến Haven Harbor
            AltarHandHold = 12,    // Bước 12: Lên Bàn Thờ Ký Ức - Nắm Tay Nhau [H] hoàn thành
            Completed = 13
        }

        [Header("State")]
        [SerializeField] private StageStep currentStep = StageStep.BoardBoat;

        [Header("References")]
        public CoopWoodenBoat boat;
        public RiverSlalomGate gate1;
        public RiverSlalomGate gate2;
        public RiverSlalomGate gate3;
        public SluiceGateObstacle sluiceGate;
        public MarshBeaconTrigger westBeacon;
        public MarshBeaconTrigger eastBeacon;
        public GiantWatermillObstacle watermill;
        public LevelTwoClimaxManager climaxManager;

        [Header("Waypoint Arrow Visual")]
        public Transform arrowTransform;
        public GameObject waypointPulseRing;
        public Color arrowGlowColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);

        // Runtime animation variables
        private Vector3 m_TargetWaypointPos;
        private Vector3 m_CurrentArrowPos;
        private float m_BobTimer;
        private GUIStyle m_HeaderStyle;
        private GUIStyle m_SubStyle;
        private GUIStyle m_KeyStyle;
        private GUIStyle m_BoxStyle;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            AutoFindReferences();
            EnsureWaypointVisuals();
            UpdateWaypointTarget(true);
        }

        public void AutoFindReferences()
        {
            if (boat == null) boat = FindAnyObjectByType<CoopWoodenBoat>();
            var gates = FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None);
            foreach (var g in gates)
            {
                if (g.gateNumber == 1) gate1 = g;
                else if (g.gateNumber == 2) gate2 = g;
                else if (g.gateNumber == 3) gate3 = g;
            }
            if (sluiceGate == null) sluiceGate = FindAnyObjectByType<SluiceGateObstacle>();
            if (watermill == null) watermill = FindAnyObjectByType<GiantWatermillObstacle>();
            if (climaxManager == null) climaxManager = FindAnyObjectByType<LevelTwoClimaxManager>();
            if (westBeacon == null && watermill != null) westBeacon = watermill.west;
            if (eastBeacon == null && watermill != null) eastBeacon = watermill.east;
        }

        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        /// <summary>
        /// Tạo mô hình Mũi tên 3D phát sáng chỉ thẳng xuống mục tiêu (Không có hình tròn ở dưới)
        /// </summary>
        public void EnsureWaypointVisuals()
        {
            // Dọn sạch hoàn toàn hình tròn bên dưới nếu còn tồn tại
            var existingRing = GameObject.Find("LVL2_WaypointPulseRing");
            if (existingRing != null) SafeDestroy(existingRing);
            if (waypointPulseRing != null)
            {
                SafeDestroy(waypointPulseRing);
                waypointPulseRing = null;
            }

            if (arrowTransform == null)
            {
                var existingArrow = GameObject.Find("LVL2_GuidingWaypointArrow");
                if (existingArrow != null)
                {
                    arrowTransform = existingArrow.transform;
                }
            }

            // Làm mới các bộ phận của mũi tên để đảm bảo mũi tên chúc thẳng xuống đất
            if (arrowTransform != null)
            {
                for (int i = arrowTransform.childCount - 1; i >= 0; i--)
                {
                    SafeDestroy(arrowTransform.GetChild(i).gameObject);
                }
            }
            else
            {
                var arrowObj = new GameObject("LVL2_GuidingWaypointArrow");
                arrowTransform = arrowObj.transform;
                arrowTransform.SetParent(transform, true);
            }

            // Tạo Material phát sáng ánh vàng cam
            var glowMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            glowMat.name = "LVL2_ArrowGlow_Runtime";
            glowMat.color = arrowGlowColor;
            if (glowMat.HasProperty("_EmissionColor"))
            {
                glowMat.EnableKeyword("_EMISSION");
                glowMat.SetColor("_EmissionColor", arrowGlowColor * 2.8f);
            }

            // ==========================================
            // DỰNG MŨI TÊN CHỈ THẲNG XUỐNG DƯỚI (DOWN-POINTING ARROW)
            // ==========================================
            // 1. Chóp nhọn cắm thẳng xuống dưới cùng (Vị trí thấp nhất Y = 0.15)
            var tip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tip.name = "ArrowTip_Down";
            tip.transform.SetParent(arrowTransform, false);
            tip.transform.localPosition = new Vector3(0, 0.15f, 0);
            tip.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            tip.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            SafeDestroy(tip.GetComponent<Collider>());
            tip.GetComponent<Renderer>().sharedMaterial = glowMat;

            // 2. Đầu mũi tên (Inverted Head)
            var head = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            head.name = "ArrowHead";
            head.transform.SetParent(arrowTransform, false);
            head.transform.localPosition = new Vector3(0, 0.55f, 0);
            head.transform.localScale = new Vector3(0.95f, 0.4f, 0.95f);
            SafeDestroy(head.GetComponent<Collider>());
            head.GetComponent<Renderer>().sharedMaterial = glowMat;

            // 3. Thân mũi tên (Shaft) vươn lên cao
            var shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaft.name = "ArrowShaft";
            shaft.transform.SetParent(arrowTransform, false);
            shaft.transform.localPosition = new Vector3(0, 1.45f, 0);
            shaft.transform.localScale = new Vector3(0.32f, 0.75f, 0.32f);
            SafeDestroy(shaft.GetComponent<Collider>());
            shaft.GetComponent<Renderer>().sharedMaterial = glowMat;

            // 4. Hai vây cánh chữ V xuôi theo hướng chỉ xuống
            var finL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finL.name = "ArrowFin_L";
            finL.transform.SetParent(arrowTransform, false);
            finL.transform.localPosition = new Vector3(-0.35f, 0.85f, 0);
            finL.transform.localRotation = Quaternion.Euler(0, 0, -35f);
            finL.transform.localScale = new Vector3(0.18f, 0.75f, 0.25f);
            SafeDestroy(finL.GetComponent<Collider>());
            finL.GetComponent<Renderer>().sharedMaterial = glowMat;

            var finR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            finR.name = "ArrowFin_R";
            finR.transform.SetParent(arrowTransform, false);
            finR.transform.localPosition = new Vector3(0.35f, 0.85f, 0);
            finR.transform.localRotation = Quaternion.Euler(0, 0, 35f);
            finR.transform.localScale = new Vector3(0.18f, 0.75f, 0.25f);
            SafeDestroy(finR.GetComponent<Collider>());
            finR.GetComponent<Renderer>().sharedMaterial = glowMat;

            // Thêm đèn Point Light chiếu rọi thẳng xuống tâm điểm mục tiêu
            var light = arrowTransform.GetComponent<Light>();
            if (light == null) light = arrowTransform.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = arrowGlowColor;
            light.range = 8f;
            light.intensity = 3.0f;
        }

        private void Update()
        {
            EvaluateProgress();
            AnimateWaypointArrow();
        }

        /// <summary>
        /// Tự động đánh giá tiến trình thực tế của người chơi
        /// </summary>
        private void EvaluateProgress()
        {
            if (boat == null) AutoFindReferences();

            // Nếu đã xong toàn màn
            if (climaxManager != null && climaxManager.Completed)
            {
                currentStep = StageStep.Completed;
                UpdateWaypointTarget(false);
                return;
            }

            switch (currentStep)
            {
                case StageStep.BoardBoat:
                    // Hoàn thành khi cả hai hoặc người chơi đã lên thuyền
                    if (boat != null && (boat.HasCoopCrew || boat.IsDadMounted || boat.IsChildMounted))
                    {
                        currentStep = StageStep.SlalomGate1;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.SlalomGate1:
                    if (gate1 != null && gate1.Passed)
                    {
                        currentStep = StageStep.SlalomGate2;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.SlalomGate2:
                    if (gate2 != null && gate2.Passed)
                    {
                        currentStep = StageStep.SlalomGate3;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.SlalomGate3:
                    if (gate3 != null && gate3.Passed)
                    {
                        currentStep = StageStep.DockAtSluice;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.DockAtSluice:
                    // Khi thuyền tới bãi cập bến Sluice hoặc có người lên bờ
                    if (boat != null && boat.transform.position.z >= 68f && (!boat.IsDadMounted || !boat.IsChildMounted || sluiceGate != null && sluiceGate.Phase > 0))
                    {
                        currentStep = StageStep.SluicePuzzle;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.SluicePuzzle:
                    if (sluiceGate != null && sluiceGate.Open)
                    {
                        currentStep = StageStep.NavigateRapids;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.NavigateRapids:
                    // Khi thuyền vượt qua hẻm ghềnh thác và 2 xoáy nước
                    if (boat != null && boat.transform.position.z >= 150f)
                    {
                        currentStep = (westBeacon != null && westBeacon.Lit) ? StageStep.EastBeacon : StageStep.WestBeacon;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.WestBeacon:
                    if (westBeacon != null && westBeacon.Lit)
                    {
                        currentStep = (eastBeacon != null && eastBeacon.Lit) ? StageStep.PassWatermill : StageStep.EastBeacon;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.EastBeacon:
                    if (eastBeacon != null && eastBeacon.Lit)
                    {
                        currentStep = (westBeacon != null && westBeacon.Lit) ? StageStep.PassWatermill : StageStep.WestBeacon;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.PassWatermill:
                    if (watermill != null && watermill.Passed)
                    {
                        currentStep = StageStep.DockAtHaven;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.DockAtHaven:
                    // Khi thuyền đã cập Haven hoặc cả 2 đã lên bờ
                    if (climaxManager != null && (climaxManager.Docked || (boat != null && !boat.IsDadMounted && !boat.IsChildMounted && boat.transform.position.z > 240f)))
                    {
                        currentStep = StageStep.AltarHandHold;
                        UpdateWaypointTarget(false);
                    }
                    break;

                case StageStep.AltarHandHold:
                    if (climaxManager != null && climaxManager.Completed)
                    {
                        currentStep = StageStep.Completed;
                        UpdateWaypointTarget(false);
                    }
                    break;
            }
        }

        /// <summary>
        /// Cập nhật vị trí tọa độ mục tiêu cần hướng tới
        /// </summary>
        public void UpdateWaypointTarget(bool instantSnap)
        {
            Vector3 target = Vector3.zero;

            switch (currentStep)
            {
                case StageStep.BoardBoat:
                    target = boat != null ? boat.transform.position + Vector3.up * 1.5f : new Vector3(97.2f, 2.0f, 34.2f);
                    break;

                case StageStep.SlalomGate1:
                    target = gate1 != null ? gate1.transform.position + Vector3.up * 1.8f : new Vector3(98.0f, 2.0f, 44.0f);
                    break;

                case StageStep.SlalomGate2:
                    target = gate2 != null ? gate2.transform.position + Vector3.up * 1.8f : new Vector3(98.2f, 2.0f, 56.0f);
                    break;

                case StageStep.SlalomGate3:
                    target = gate3 != null ? gate3.transform.position + Vector3.up * 1.8f : new Vector3(100.5f, 2.0f, 66.0f);
                    break;

                case StageStep.DockAtSluice:
                    target = new Vector3(107.5f, 2.0f, 70.0f); // Bến cập thuyền Cửa Cống
                    break;

                case StageStep.SluicePuzzle:
                    // Định vị theo vai của người chơi
                    var local = LevelTwoActors.Local;
                    if (local != null && !LevelOnePlayer.IsDad(local))
                    {
                        // Con: Hướng dẫn vào lỗ chui hoặc van xả
                        target = (sluiceGate != null && sluiceGate.ChildTraversed) 
                            ? (sluiceGate.valve != null ? sluiceGate.valve.position + Vector3.up * 1.5f : new Vector3(109.5f, 3.2f, 78.0f))
                            : (sluiceGate != null && sluiceGate.crawl != null ? sluiceGate.crawl.transform.position + Vector3.up * 1.5f : new Vector3(109.5f, 2.2f, 74.0f));
                    }
                    else
                    {
                        // Ba: Hướng dẫn đến trạm tời giữ cần [E]
                        target = (sluiceGate != null && sluiceGate.winch != null) ? sluiceGate.winch.position + Vector3.up * 2.0f : new Vector3(110.5f, 3.0f, 71.5f);
                    }
                    break;

                case StageStep.NavigateRapids:
                    target = new Vector3(100.0f, 2.0f, 135.0f); // Cuối hẻm ghềnh thác
                    break;

                case StageStep.WestBeacon:
                    target = (westBeacon != null && westBeacon.throwPad != null) ? westBeacon.throwPad.position + Vector3.up * 2.2f : new Vector3(97.0f, 2.8f, 175.0f);
                    break;

                case StageStep.EastBeacon:
                    target = (eastBeacon != null && eastBeacon.throwPad != null) ? eastBeacon.throwPad.position + Vector3.up * 2.2f : new Vector3(103.0f, 2.8f, 190.0f);
                    break;

                case StageStep.PassWatermill:
                    target = (watermill != null) ? watermill.transform.position + Vector3.up * 2.5f : new Vector3(100.0f, 2.5f, 225.0f);
                    break;

                case StageStep.DockAtHaven:
                    target = new Vector3(100.0f, 2.2f, 246.0f); // Bến Haven
                    break;

                case StageStep.AltarHandHold:
                    target = (climaxManager != null && climaxManager.altar != null) ? climaxManager.altar.position + Vector3.up * 2.5f : new Vector3(100.0f, 2.8f, 256.0f);
                    break;

                case StageStep.Completed:
                    target = new Vector3(100.0f, 3.0f, 256.0f);
                    break;
            }

            m_TargetWaypointPos = target;
            if (instantSnap)
            {
                m_CurrentArrowPos = target + Vector3.up * 2.5f;
                if (arrowTransform != null) arrowTransform.position = m_CurrentArrowPos;
            }
        }

        /// <summary>
        /// Hiệu ứng xoay tròn và nhấp nhô của mũi tên 3D (Chỉ có mũi tên chỉ xuống, không có hình tròn dưới)
        /// </summary>
        private void AnimateWaypointArrow()
        {
            if (arrowTransform == null) return;

            if (currentStep == StageStep.Completed)
            {
                arrowTransform.gameObject.SetActive(false);
                return;
            }

            arrowTransform.gameObject.SetActive(true);

            m_BobTimer += Time.deltaTime * 3.5f;
            float bobOffset = Mathf.Sin(m_BobTimer) * 0.45f;

            // Di chuyển mũi tên mượt mà tới mục tiêu
            Vector3 desiredPos = m_TargetWaypointPos + Vector3.up * (2.8f + bobOffset);
            m_CurrentArrowPos = Vector3.Lerp(m_CurrentArrowPos, desiredPos, Time.deltaTime * 6f);
            arrowTransform.position = m_CurrentArrowPos;

            // Xoay nhẹ mũi tên quanh trục Y để gây chú ý (mũi nhọn luôn cắm thẳng xuống đất)
            arrowTransform.Rotate(Vector3.up, 65f * Time.deltaTime, Space.World);
        }

        /// <summary>
        /// Lấy thông tin mô tả chi tiết của từng bước
        /// </summary>
        public (string Title, string Description, string KeyHint) GetCurrentStageInfo()
        {
            switch (currentStep)
            {
                case StageStep.BoardBoat:
                    return ("CHẶNG 0: XUẤT PHÁT", "Tiến lại gần thuyền gỗ và lên thuyền để bắt đầu cuộc hành trình", "[E] Lên thuyền | [W/A/S/D] Chèo thuyền");

                case StageStep.SlalomGate1:
                    return ("CHẶNG 1: LUỒNG LÁCH CỔNG PHAO (1/3)", "Lái thuyền uốn lượn vượt qua Cổng Phao số 1 (Giữa phao Đỏ và Xanh)", "[A/D] Bẻ lái | [W] Chèo tiến | [Space] Bứt tốc");

                case StageStep.SlalomGate2:
                    return ("CHẶNG 1: LUỒNG LÁCH CỔNG PHAO (2/3)", "Né tránh tảng đá ngầm bên trái và lượn qua Cổng Phao số 2", "[A/D] Bẻ lái nhịp nhàng theo dòng chảy");

                case StageStep.SlalomGate3:
                    return ("CHẶNG 1: LUỒNG LÁCH CỔNG PHAO (3/3)", "Căn thẳng mũi thuyền và lướt qua Cổng Phao số 3", "[Space] Nhấn nhịp để phóng nhanh qua cổng");

                case StageStep.DockAtSluice:
                    return ("CHẶNG 2: CỬA CỐNG THỦY LỢI", "Cửa cống sắt đang khóa chặn dòng sông! Cả hai ghé thuyền vào bến mạn phải để lên bờ", "[E] Rời thuyền lên bến gỗ");

                case StageStep.SluicePuzzle:
                    var local = LevelTwoActors.Local;
                    bool isDad = local != null && LevelOnePlayer.IsDad(local);
                    if (isDad)
                    {
                        return ("CHẶNG 2: PHỐI HỢP CỬA CỐNG", "Ba: Bước lên Bệ Tời Đá và GIỮ [E] để nâng cổng sắt hầm ngầm cho Con bò qua!", "BA: GIỮ [E] VẬN TỜI CÁP");
                    }
                    else
                    {
                        return ("CHẶNG 2: PHỐI HỢP CỬA CỐNG", "Con: Chờ Ba giữ tời -> Chui qua hầm ngầm [Crawl] -> Lên ban công bấm [E] mở van xả nước!", "CON: CHUI HẦM & BẤM [E] XOAY VAN XẢ");
                    }

                case StageStep.NavigateRapids:
                    return ("CHẶNG 3: HẺM GHỀNH THÁC & XOÁY NƯỚC", "Cửa cống đã mở! Mau lên thuyền, giữ chắc tay chèo né 2 tâm xoáy nước hung dữ", "[A/D] Bẻ lái dứt khoát né tâm xoáy nước!");

                case StageStep.WestBeacon:
                    return ("CHẶNG 4: ĐẦM LẦY SƯƠNG MÙ - HẢI ĐĂNG TÂY", "Ghé thuyền vào Đảo Tây. Ba đứng Bục ném -> Ném Con [F] lên đài sen thắp sáng Hải Đăng!", "BA: [F] NÉM ĐỒNG ĐỘI | CON: THẮP ĐÀI LỬA");

                case StageStep.EastBeacon:
                    return ("CHẶNG 4: ĐẦM LẦY SƯƠNG MÙ - HẢI ĐĂNG ĐÔNG", "Tiếp tục sang Đảo Đông. Ba ném Con [F] lên đài sen để thắp sáng ngọn Hải Đăng còn lại!", "BA: [F] NÉM ĐỒNG ĐỘI LÊN ĐÀI SEN");

                case StageStep.PassWatermill:
                    return ("CHẶNG 5: CỐI XAY NƯỚC KHỔNG LỒ", "Hai ngọn hải đăng đã mở lối! Canh nhịp cối xay dừng ở góc chéo (2.5s) -> Đồng lòng chèo vọt qua!", "CẢ HAI: [W] + [SPACE] BỨT TỐC QUA KHE CỐI XAY");

                case StageStep.DockAtHaven:
                    return ("CHẶNG 5: BẾN BỜ BÌNH MINH (HAVEN HARBOR)", "Đã vượt qua cối xay! Cập thuyền vào cầu cảng Haven Harbor và bước lên bậc tam cấp", "[E] Lên bờ tiến về Bàn Thờ Ký Ức");

                case StageStep.AltarHandHold:
                    return ("CHẶNG CUỐI: BÀN THỜ KÝ ỨC", "Cùng đứng lên Bàn Thờ Ký Ức, đứng sát bên nhau và GIỮ [H] NẮM TAY NHAU!", "GIỮ [H] NẮM TAY NHAU ĐỂ CHIẾN THẮNG!");

                case StageStep.Completed:
                    return ("HOÀN THÀNH MÀN 2!", "Tình phụ tử đã vượt qua ngọn sóng dữ! Chúc mừng hai cha con!", "BẤM NÚT TRÊN MÀN HÌNH ĐỂ TIẾP TỤC");

                default:
                    return ("MÀN 2: THÁM HIỂM DÒNG SÔNG", "Hãy cùng đồng đội chèo thuyền vượt qua các thử thách", "");
            }
        }

        private void OnGUI()
        {
            if (currentStep == StageStep.Completed) return;

            InitStyles();

            var info = GetCurrentStageInfo();

            // Tính khoảng cách tới mục tiêu
            float distance = 0f;
            var local = LevelTwoActors.Local;
            if (local != null)
            {
                distance = Vector3.Distance(local.transform.position, m_TargetWaypointPos);
            }
            else if (boat != null)
            {
                distance = Vector3.Distance(boat.transform.position, m_TargetWaypointPos);
            }

            // Vẽ bảng Banner hướng dẫn ở cạnh trên màn hình (Screen Top Center)
            float boxWidth = Mathf.Min(680f, Screen.width - 40f);
            float boxHeight = 88f;
            float boxX = (Screen.width - boxWidth) * 0.5f;
            float boxY = 16f;

            GUI.Box(new Rect(boxX, boxY, boxWidth, boxHeight), GUIContent.none, m_BoxStyle);

            // Icon Mũi tên và Tiêu đề
            string titleText = $"► {info.Title}  [Mục tiêu: {Mathf.RoundToInt(distance)}m]";
            GUI.Label(new Rect(boxX + 16, boxY + 8, boxWidth - 32, 26), titleText, m_HeaderStyle);

            // Nội dung hướng dẫn chi tiết
            GUI.Label(new Rect(boxX + 16, boxY + 34, boxWidth - 32, 24), info.Description, m_SubStyle);

            // Gợi ý phím thao tác (Highlight màu vàng ấm)
            if (!string.IsNullOrEmpty(info.KeyHint))
            {
                GUI.Label(new Rect(boxX + 16, boxY + 58, boxWidth - 32, 22), $"THAO TÁC: {info.KeyHint}", m_KeyStyle);
            }
        }

        private void InitStyles()
        {
            if (m_BoxStyle == null)
            {
                m_BoxStyle = new GUIStyle(GUI.skin.box);
                // Tạo Texture nền bán trong suốt sang trọng
                var bgTex = new Texture2D(1, 1);
                bgTex.SetPixel(0, 0, new Color(0.08f, 0.12f, 0.16f, 0.88f));
                bgTex.Apply();
                m_BoxStyle.normal.background = bgTex;
            }

            if (m_HeaderStyle == null)
            {
                m_HeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1.0f, 0.86f, 0.3f) }
                };
            }

            if (m_SubStyle == null)
            {
                m_SubStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Normal,
                    normal = { textColor = Color.white }
                };
            }

            if (m_KeyStyle == null)
            {
                m_KeyStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.35f, 0.95f, 0.45f) }
                };
            }
        }
    }
}
