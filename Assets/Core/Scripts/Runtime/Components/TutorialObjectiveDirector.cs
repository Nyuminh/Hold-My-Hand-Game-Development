using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blocks.Gameplay.Core
{
    // ─────────────────────────────────────────────────────────────────────────
    // Enum & Data classes
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loại điều kiện hoàn thành của một chương.
    /// Director đọc trạng thái thật từ các controller có sẵn trong scene
    /// (CraneElevatorController, GiantGearTurntableController, CoveDrawbridgeCoopController,
    ///  Stage3ZiplineCoopController, ShipwreckCoopController, CoopDrawbridgeController)
    /// để biết người chơi đã xong mục tiêu hay chưa — KHÔNG cần UI bấm "Next".
    /// </summary>
    public enum ChapterProbe
    {
        /// <summary>Chương tự hoàn thành khi có người chơi bước vào vùng mục tiêu.</summary>
        RegionEntered = 0,

        /// <summary>Chương hoàn thành khi TẤT CẢ người chơi cùng ở trong vùng mục tiêu.</summary>
        AllPlayersInRegion = 1,

        /// <summary>Thang dây đã được Con thả xuống bờ tường (Màn 2: Đôi Cánh Của Cha).</summary>
        LadderDropped = 2,

        /// <summary>Cầu rút Zone1 đã hạ phẳng (cả 2 cần gạt).</summary>
        DrawbridgeZone1Lowered = 3,

        /// <summary>Thang cẩu Zone3 đã đưa được cả 2 người sang bên kia.</summary>
        CraneElevatorCompleted = 4,

        /// <summary>Bàn xoay bánh răng đá đã xoay xong.</summary>
        GearTurntableCompleted = 5,

        /// <summary>Cầu rút khu Vịnh đã hạ phẳng (nhổ chốt + quay tời).</summary>
        CoveDrawbridgeOpened = 6,

        /// <summary>Đã đi Zipline từ Mỏ sang Tàu Đắm (hoặc ngược lại).</summary>
        ZiplineRidden = 7,

        /// <summary>Rương thuyền trưởng đã mở (đã lấy được chìa khóa).</summary>
        ShipwreckChestOpened = 8,

        /// <summary>Cầu tàu (gangplank) đã hạ xuống bến.</summary>
        ShipwreckGangplankLowered = 9,

        /// <summary>Hai cha con cùng đứng lên 2 bệ đá và nắm tay nhau kích hoạt Cổng sang Màn tiếp theo.</summary>
        NextLevelPortalActivated = 13,

        PlatesPowered = 14,

        /// <summary>Chương được đánh dấu hoàn thành từ bên ngoài qua Director API.</summary>
        Manual = 15
    }

    /// <summary>
    /// Một chương trong luồng chính của Tutorial.
    ///
    /// Mỗi chương có:
    ///   - Vị trí mục tiêu trong world (triggerCenter): Beacon sẽ đặt tại đây,
    ///     mũi tên la bàn trên HUD sẽ chỉ về đây.
    ///   - Điều kiện hoàn thành (probe) đọc từ trạng thái thật của các controller.
    ///   - Danh sách phím cần dùng — chỉ hiện đúng phím của chương đang mở
    ///     (thay vì bảng phím cố định như hiện tại).
    /// </summary>
    [Serializable]
    public class TutorialChapter
    {
        [Header("Identity")]
        [Tooltip("Mã chương, dùng để debug/log")]
        public string id = "chapter";

        [Tooltip("Tiêu đề hiển thị trên thẻ mục tiêu")]
        public string title = "";

        [Tooltip("Gợi ý ngắn 1 dòng. Để trống nếu không cần.")]
        public string hint = "";

        [Header("Activation")]
        [Tooltip("Vị trí mục tiêu trong world space. Beacon + mũi tên la bàn dùng điểm này.")]
        public Vector3 triggerCenter = Vector3.zero;

        [Tooltip("Bán kính vùng mục tiêu (mét). Người chơi vào trong vùng thì coi như đã tới nơi.")]
        public float triggerRadius = 10f;

        [Tooltip("Thứ tự chương. Chương chỉ mở khi chương trước đã hoàn thành.")]
        public int order = 0;

        [Header("Completion")]
        [Tooltip("Điều kiện hoàn thành chương.")]
        public ChapterProbe probe = ChapterProbe.RegionEntered;

        [Tooltip("Danh sách tên object bàn đạp (dùng cho probe = PlatesPowered).")]
        public string[] plateObjectNames = Array.Empty<string>();

        [Header("UI")]
        [Tooltip("Các phím cần dùng trong chương này (ví dụ: H, F, E, R). Rỗng = không hiện phím.")]
        public string[] keys = Array.Empty<string>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Director MonoBehaviour
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Đạo diễn luồng Tutorial.
    ///
    /// Mỗi interval (probeHz lần/giây) kiểm tra <see cref="ChapterProbe"/> của chương hiện tại
    /// bằng cách đọc property boolean từ các controller thật trong scene.
    /// Khi probe thỏa mãn → tự động AdvanceChapter() sau completionDelay giây:
    ///   - Dịch chuyển Beacon 3D đến triggerCenter của chương tiếp theo.
    ///   - Cập nhật thẻ tiêu đề + gợi ý + danh sách phím trên TutorialHUD.
    ///   - Xoay mũi tên compass UI về hướng mục tiêu mới (mỗi frame).
    ///
    /// Cách dùng trong Unity Editor:
    ///   1. Gắn script này vào bất kỳ GameObject nào trong Tutorial.unity.
    ///   2. Điền chapters theo đúng thứ tự (order = 0, 1, 2, …).
    ///   3. Kéo các controller reference vào slot tương ứng.
    ///   4. (Tuỳ chọn) Kéo UIDocument TutorialHUD và beaconObject.
    /// </summary>
    public class TutorialObjectiveDirector : MonoBehaviour
    {
        // ── Inspector ──────────────────────────────────────────────────────────

        [Header("Chapters (kéo thả, sắp xếp theo order)")]
        [SerializeField] private List<TutorialChapter> chapters = new();

        [Header("Controllers – kéo GameObject tương ứng vào đây")]
        [SerializeField] private CoopRopeLadderDrop            ropeLadderCtrl;
        [SerializeField] private CoopDrawbridgeController      zone1DrawbridgeCtrl;
        [SerializeField] private CraneElevatorController       craneElevatorCtrl;
        [SerializeField] private GiantGearTurntableController  gearTurntableCtrl;
        [SerializeField] private CoveDrawbridgeCoopController  coveDrawbridgeCtrl;
        [SerializeField] private Stage3ZiplineCoopController   ziplineCtrl;
        [SerializeField] private ShipwreckCoopController       shipwreckCtrl;
        [SerializeField] private CoopLevelExitPortal           nextLevelPortalCtrl;

        [Header("Beacon 3D (tùy chọn)")]
        [Tooltip("GameObject làm ngọn hải đăng chỉ mục tiêu. Sẽ được dịch chuyển đến triggerCenter của chương hiện tại.")]
        [SerializeField] private GameObject beaconObject;

        [Header("HUD (tùy chọn)")]
        [Tooltip("UIDocument chứa TutorialHUD.uxml. Nếu để trống, Director vẫn hoạt động nhưng không cập nhật UI.")]
        [SerializeField] private UIDocument tutorialHUD;

        [Tooltip("Tên element UXML hiển thị tiêu đề mục tiêu hiện tại.")]
        [SerializeField] private string objectiveTitleElementName = "objective-title-label";

        [Tooltip("Tên element UXML hiển thị gợi ý ngắn.")]
        [SerializeField] private string objectiveHintElementName  = "objective-hint-label";

        [Tooltip("Tên container UXML chứa các ô phím. Director rebuild nội dung container này.")]
        [SerializeField] private string keysContainerElementName  = "objective-keys-container";

        [Tooltip("Tên element UXML làm mũi tên / icon compass chỉ hướng mục tiêu.")]
        [SerializeField] private string compassArrowElementName   = "compass-arrow";

        [Header("Probe polling")]
        [Tooltip("Số lần kiểm tra probe mỗi giây (10 là đủ, không cần 60).")]
        [SerializeField] private float probeHz = 10f;

        [Header("Chapter Complete FX")]
        [Tooltip("Delay (giây) sau khi probe thỏa mãn trước khi chuyển chương — để animation/sound kịp chạy.")]
        [SerializeField] private float completionDelay = 1.2f;

        // ── Runtime state ──────────────────────────────────────────────────────

        private int  m_CurrentIndex = 0;
        private bool m_IsAdvancing  = false;
        private bool m_AllDone      = false;

        private List<TutorialChapter> m_Sorted;
        private List<Transform>       m_Players;

        // HUD element references (bound in Start)
        private Label         m_TitleLabel;
        private Label         m_HintLabel;
        private VisualElement m_KeysContainer;
        private VisualElement m_CompassArrow;

        // ── Unity lifecycle ────────────────────────────────────────────────────

        private void Awake()
        {
            m_Sorted = new List<TutorialChapter>(chapters);
            m_Sorted.Sort((a, b) => a.order.CompareTo(b.order));
        }

        private void Start()
        {
            BindHUDElements();
            CachePlayerTransforms();

            if (m_Sorted.Count == 0)
            {
                Debug.LogWarning("[TutorialObjectiveDirector] No chapters defined — Director idle.");
                return;
            }

            ApplyChapter(m_CurrentIndex);
            StartCoroutine(PollProbeRoutine());
        }

        private void Update()
        {
            UpdateCompassArrow();
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        // ── HUD binding ────────────────────────────────────────────────────────

        private void BindHUDElements()
        {
            if (tutorialHUD == null) return;
            var root = tutorialHUD.rootVisualElement;
            m_TitleLabel    = root.Q<Label>(objectiveTitleElementName);
            m_HintLabel     = root.Q<Label>(objectiveHintElementName);
            m_KeysContainer = root.Q<VisualElement>(keysContainerElementName);
            m_CompassArrow  = root.Q<VisualElement>(compassArrowElementName);
        }

        // ── Player discovery ───────────────────────────────────────────────────

        private void CachePlayerTransforms()
        {
            m_Players = new List<Transform>();

            // Ưu tiên tag "Player"
            foreach (var go in GameObject.FindGameObjectsWithTag("Player"))
                m_Players.Add(go.transform);

            // Fallback: CoreStatsHandler được gắn vào nhân vật người chơi
            if (m_Players.Count == 0)
            {
                foreach (var stats in FindObjectsByType<CoreStatsHandler>(FindObjectsSortMode.None))
                    m_Players.Add(stats.transform);
            }
        }

        // ── Probe poll coroutine ───────────────────────────────────────────────

        private IEnumerator PollProbeRoutine()
        {
            var wait = new WaitForSeconds(1f / Mathf.Max(1f, probeHz));
            while (!m_AllDone)
            {
                yield return wait;

                if (m_IsAdvancing) continue;
                if (m_CurrentIndex >= m_Sorted.Count) { m_AllDone = true; break; }

                if (EvaluateProbe(m_Sorted[m_CurrentIndex]))
                    StartCoroutine(AdvanceAfterDelay());
            }
        }

        // ── Probe evaluation ───────────────────────────────────────────────────

        private bool EvaluateProbe(TutorialChapter chapter)
        {
            switch (chapter.probe)
            {
                // ── Region probes ──────────────────────────────────────────────
                case ChapterProbe.RegionEntered:
                    return AnyPlayerInRadius(chapter.triggerCenter, chapter.triggerRadius);

                case ChapterProbe.AllPlayersInRegion:
                    return AllPlayersInRadius(chapter.triggerCenter, chapter.triggerRadius);

                // ── Màn 2: Con thả thang dây trên đỉnh bờ tường ───────────────
                case ChapterProbe.LadderDropped:
                    if (ropeLadderCtrl == null) { LogMissing("ropeLadderCtrl", chapter.id); return false; }
                    return ropeLadderCtrl.IsLadderDropped;

                // ── Cầu rút Zone1: Cha gạt cần trái + Con gạt cần phải ─────────
                case ChapterProbe.DrawbridgeZone1Lowered:
                    if (zone1DrawbridgeCtrl == null) { LogMissing("zone1DrawbridgeCtrl", chapter.id); return false; }
                    return zone1DrawbridgeCtrl.IsLeftPulled && zone1DrawbridgeCtrl.IsRightPulled;

                // ── Thang cẩu Zone3: stage đạt BothCompleted ──────────────────
                case ChapterProbe.CraneElevatorCompleted:
                    if (craneElevatorCtrl == null) { LogMissing("craneElevatorCtrl", chapter.id); return false; }
                    return craneElevatorCtrl.CurrentStage == CraneElevatorController.ElevatorStage.BothCompleted;

                // ── Bánh răng Zone3: IsCompleted ───────────────────────────────
                case ChapterProbe.GearTurntableCompleted:
                    if (gearTurntableCtrl == null) { LogMissing("gearTurntableCtrl", chapter.id); return false; }
                    return gearTurntableCtrl.IsCompleted;

                // ── Cầu rút Vịnh: Con nhổ chốt + Cha quay tời (hạ phẳng) ──────
                case ChapterProbe.CoveDrawbridgeOpened:
                    if (coveDrawbridgeCtrl == null) { LogMissing("coveDrawbridgeCtrl", chapter.id); return false; }
                    return coveDrawbridgeCtrl.IsPinReleased && coveDrawbridgeCtrl.IsBridgeOpened;

                // ── Zipline: đã hoàn thành 1 lần đi dây ──────────────────────
                case ChapterProbe.ZiplineRidden:
                    if (ziplineCtrl == null) { LogMissing("ziplineCtrl", chapter.id); return false; }
                    return ziplineCtrl.HasCompletedRide;

                // ── Tàu đắm: rương thuyền trưởng đã mở ───────────────────────
                case ChapterProbe.ShipwreckChestOpened:
                    if (shipwreckCtrl == null) { LogMissing("shipwreckCtrl", chapter.id); return false; }
                    return shipwreckCtrl.IsChestOpened;

                // ── Tàu đắm: cầu tàu đã hạ xuống bến ────────────────────────
                case ChapterProbe.ShipwreckGangplankLowered:
                    if (shipwreckCtrl == null) { LogMissing("shipwreckCtrl", chapter.id); return false; }
                    return shipwreckCtrl.IsGangplankLowered;

                // ── Cơ cấu qua màn tiếp theo: Cổng Cổ Đại ──────────────────
                case ChapterProbe.NextLevelPortalActivated:
                    if (nextLevelPortalCtrl == null) nextLevelPortalCtrl = CoopLevelExitPortal.Instance ?? FindAnyObjectByType<CoopLevelExitPortal>();
                    return nextLevelPortalCtrl != null && nextLevelPortalCtrl.IsPortalActivated;

                case ChapterProbe.PlatesPowered:
                    return ArePlatesPowered(chapter.plateObjectNames);

                // ── Manual: không tự hoàn thành, cần gọi CompleteCurrentChapter() ──
                case ChapterProbe.Manual:
                    return false;

                default:
                    return false;
            }
        }

        // ── Advance logic ──────────────────────────────────────────────────────

        private IEnumerator AdvanceAfterDelay()
        {
            m_IsAdvancing = true;
            Debug.Log($"[TutorialObjectiveDirector] ✅ Chapter '{m_Sorted[m_CurrentIndex].id}' complete. " +
                      $"Advancing in {completionDelay}s…");

            yield return new WaitForSeconds(completionDelay);

            m_CurrentIndex++;
            m_IsAdvancing = false;

            if (m_CurrentIndex >= m_Sorted.Count)
            {
                m_AllDone = true;
                OnAllChaptersComplete();
                yield break;
            }

            ApplyChapter(m_CurrentIndex);
        }

        // ── Apply chapter to world & HUD ───────────────────────────────────────

        private void ApplyChapter(int index)
        {
            if (index < 0 || index >= m_Sorted.Count) return;
            var ch = m_Sorted[index];

            Debug.Log($"[TutorialObjectiveDirector] ▶ Chapter [{ch.order}] {ch.id}: \"{ch.title}\"");

            // 1. Dịch chuyển Beacon 3D
            if (beaconObject != null)
            {
                beaconObject.SetActive(true);
                beaconObject.transform.position = ch.triggerCenter;
            }

            // 2. Cập nhật label tiêu đề & gợi ý
            if (m_TitleLabel != null)
                m_TitleLabel.text = ch.title;

            if (m_HintLabel != null)
            {
                m_HintLabel.text = ch.hint;
                m_HintLabel.style.display = string.IsNullOrEmpty(ch.hint)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }

            // 3. Rebuild container phím
            RebuildKeysUI(ch.keys);

            // 4. Hiện compass arrow
            if (m_CompassArrow != null)
                m_CompassArrow.style.display = DisplayStyle.Flex;
        }

        // ── Keys UI rebuild ────────────────────────────────────────────────────

        private void RebuildKeysUI(string[] keys)
        {
            if (m_KeysContainer == null) return;

            m_KeysContainer.Clear();

            if (keys == null || keys.Length == 0)
            {
                m_KeysContainer.style.display = DisplayStyle.None;
                return;
            }

            m_KeysContainer.style.display = DisplayStyle.Flex;
            foreach (var key in keys)
            {
                var badge = new Label(key);
                badge.AddToClassList("key-badge");
                m_KeysContainer.Add(badge);
            }
        }

        // ── Compass update (every frame) ───────────────────────────────────────

        private void UpdateCompassArrow()
        {
            if (m_CompassArrow == null || m_AllDone || m_Sorted == null) return;
            if (m_CurrentIndex >= m_Sorted.Count) return;

            Transform player = GetLocalPlayer();
            if (player == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 toTarget = m_Sorted[m_CurrentIndex].triggerCenter - player.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.01f) return;

            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();

            float angle = Vector3.SignedAngle(camForward, toTarget.normalized, Vector3.up);
            m_CompassArrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
        }

        // ── Region helpers ─────────────────────────────────────────────────────

        private bool AnyPlayerInRadius(Vector3 center, float radius)
        {
            float r2 = radius * radius;
            foreach (var t in m_Players)
            {
                if (t != null && (t.position - center).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        private bool AllPlayersInRadius(Vector3 center, float radius)
        {
            if (m_Players.Count == 0) return false;
            float r2 = radius * radius;
            foreach (var t in m_Players)
            {
                if (t == null) continue;
                if ((t.position - center).sqrMagnitude > r2) return false;
            }
            return true;
        }

        // ── Pressure plates helper ─────────────────────────────────────────────

        /// <summary>
        /// Kiểm tra xem tất cả bàn đạp có tên trong <paramref name="plateNames"/>
        /// đang bị giẫm cùng lúc không.
        /// Component trên mỗi bàn đạp cần có field/property bool IsPressed hoặc bool IsPowered.
        /// Director tìm component bằng reflection để không phụ thuộc vào interface cứng.
        /// </summary>
        private static bool ArePlatesPowered(string[] plateNames)
        {
            if (plateNames == null || plateNames.Length == 0) return false;

            int powered = 0;
            foreach (var name in plateNames)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;

                // Thử IsPressed rồi IsPowered qua reflection (tránh cần interface cứng)
                bool pressed = false;
                foreach (var comp in go.GetComponents<MonoBehaviour>())
                {
                    var type = comp.GetType();
                    var propPressed = type.GetProperty("IsPressed");
                    var propPowered = type.GetProperty("IsPowered");

                    if (propPressed != null && propPressed.GetValue(comp) is bool bp && bp) { pressed = true; break; }
                    if (propPowered != null && propPowered.GetValue(comp) is bool bw && bw) { pressed = true; break; }
                }
                if (pressed) powered++;
            }
            return powered >= plateNames.Length;
        }

        // ── Misc helpers ───────────────────────────────────────────────────────

        private Transform GetLocalPlayer()
        {
            if (m_Players != null && m_Players.Count > 0 && m_Players[0] != null)
                return m_Players[0];
            return Camera.main != null ? Camera.main.transform : null;
        }

        private static void LogMissing(string fieldName, string chapterId)
        {
            Debug.LogWarning($"[TutorialObjectiveDirector] Chapter '{chapterId}': " +
                             $"controller '{fieldName}' is not assigned. Probe will never fire.");
        }

        private void OnAllChaptersComplete()
        {
            Debug.Log("[TutorialObjectiveDirector] 🎉 All chapters complete!");

            if (beaconObject != null)
                beaconObject.SetActive(false);

            if (m_CompassArrow != null)
                m_CompassArrow.style.display = DisplayStyle.None;

            if (m_TitleLabel != null)
                m_TitleLabel.text = "✅ Hoàn thành Tutorial!";

            if (m_HintLabel != null)
            {
                m_HintLabel.text = "Bạn đã nắm vững các cơ chế. Hãy vào Game thật!";
                m_HintLabel.style.display = DisplayStyle.Flex;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>Chương đang hoạt động (null nếu xong hết).</summary>
        public TutorialChapter CurrentChapter =>
            (m_Sorted != null && m_CurrentIndex < m_Sorted.Count)
                ? m_Sorted[m_CurrentIndex]
                : null;

        /// <summary>True khi tất cả chương đã hoàn thành.</summary>
        public bool IsAllDone => m_AllDone;

        /// <summary>
        /// Hoàn thành chương hiện tại theo cách thủ công.
        /// Dùng cho probe = Manual hoặc khi cần skip từ editor/debug.
        /// </summary>
        public void CompleteCurrentChapter()
        {
            if (m_IsAdvancing || m_AllDone) return;
            StartCoroutine(AdvanceAfterDelay());
        }

        /// <summary>Làm mới cache danh sách player (gọi khi spawn/despawn nhân vật).</summary>
        public void RefreshPlayerCache() => CachePlayerTransforms();

        // ── Gizmos ─────────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (chapters == null) return;
            foreach (var ch in chapters)
            {
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.3f);
                Gizmos.DrawSphere(ch.triggerCenter, ch.triggerRadius);
                Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.9f);
                Gizmos.DrawWireSphere(ch.triggerCenter, ch.triggerRadius);
                UnityEditor.Handles.Label(
                    ch.triggerCenter + Vector3.up * (ch.triggerRadius + 0.5f),
                    $"[{ch.order}] {ch.id}");
            }
        }
#endif
    }
}
