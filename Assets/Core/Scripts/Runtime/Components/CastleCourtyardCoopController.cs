using System;
using System.Collections;
using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cơ chế Câu Đố Phối Hợp Sân Lâu Đài Hoàng Gia (Grand Castle Courtyard Coop Puzzle):
    /// 1. Giai đoạn 1 (Bập bênh phóng Con lên ban công cao):
    ///    - Ba dậm bập bênh CastleSeesawCatapult để phóng Con bay lên ban công tháp đông (y = 29.2).
    /// 2. Giai đoạn 2 (Con gạt cần mở chốt xích hoàng gia & thả thang dây):
    ///    - Con chạy trên ban công gạt cần Castle_Balcony_Lever để giải phóng cùm xích trên cao.
    ///    - Thang dây rủ xuống sân để Ba leo lên hoặc Con chạy xuống. Năng lượng truyền xuống 2 bệ đá.
    /// 3. Giai đoạn 3 (Kích hoạt 2 Bệ Cổ Ngữ Thái Dương & Nguyệt Quang):
    ///    - Ba đứng lên Bệ Thái Dương (Vàng), Con đứng lên Bệ Nguyệt Quang (Lam).
    ///    - Khi cả 2 bệ được kích hoạt sau khi đã mở xích ban công: Cổng Sập Hoàng Gia mở toang dẫn ra Vực Mây Zephyr!
    /// </summary>
    public class CastleCourtyardCoopController : NetworkBehaviour
    {
        private static CastleCourtyardCoopController instance;
        public static CastleCourtyardCoopController Instance => instance;

        [Header("Balcony Mechanism (High-Low Co-op)")]
        [Tooltip("Cần gạt mở khóa cùm xích trên ban công")]
        [SerializeField] private Transform balconyLever;
        [SerializeField] private Transform balconyLeverHandle;
        [SerializeField] private Transform balconyChain;
        [SerializeField] private CoopRopeLadderDrop balconyRopeLadder;
        [SerializeField] private Light balconyRuneLight;
        [SerializeField] private Vector3 balconyLeverPos = new Vector3(8.50f, 29.20f, -119.50f);

        [Header("Rune Pressure Plates")]
        [SerializeField] private Transform fatherPlate;
        [SerializeField] private Transform childPlate;
        [SerializeField] private Transform centerDais;
        [SerializeField] private Light fatherLight;
        [SerializeField] private Light childLight;
        [SerializeField] private Light daisAuraLight;

        [Header("Portcullis Gate")]
        [SerializeField] private Transform portcullisGate;
        [SerializeField] private float openHeight = 5.2f;
        [SerializeField] private float openSpeed = 2.0f;

        [Header("Puzzle State")]
        [SerializeField] private bool isBalconyUnlocked = false;
        [SerializeField] private bool isFatherOnPlate = false;
        [SerializeField] private bool isChildOnPlate = false;
        [SerializeField] private bool isGateUnlocked = false;

        private Vector3 gateInitialPos;
        private Vector3 gateTargetPos;
        private Quaternion leverHandleInitialRot;
        private bool isLeverAnimating = false;

        // UI Prompt State
        private string currentPrompt = "";
        private Color promptColor = Color.yellow;
        private float promptTimer = 0f;

        public bool IsBalconyUnlocked => isBalconyUnlocked;
        public bool IsGateUnlocked => isGateUnlocked;

        private void Awake()
        {
            if (instance == null) instance = this;
        }

        private void Start()
        {
            FindReferencesIfNull();
            BuildProceduralBalconyProps();

            if (portcullisGate != null)
            {
                gateInitialPos = portcullisGate.position;
                gateTargetPos = gateInitialPos + Vector3.up * openHeight;
            }

            if (fatherLight != null) fatherLight.intensity = 0f;
            if (childLight != null) childLight.intensity = 0f;
            if (daisAuraLight != null) daisAuraLight.intensity = 0f;
            if (balconyRuneLight != null) balconyRuneLight.intensity = isBalconyUnlocked ? 3.0f : 1.2f;

            if (balconyLeverHandle != null)
            {
                leverHandleInitialRot = balconyLeverHandle.localRotation;
                if (isBalconyUnlocked)
                {
                    balconyLeverHandle.localRotation = leverHandleInitialRot * Quaternion.Euler(55f, 0f, 0f);
                }
            }
        }

        private void FindReferencesIfNull()
        {
            if (fatherPlate == null)
            {
                var go = GameObject.Find("Castle_Coop_Plate_Father");
                if (go != null) fatherPlate = go.transform;
            }

            if (childPlate == null)
            {
                var go = GameObject.Find("Castle_Coop_Plate_Child");
                if (go != null) childPlate = go.transform;
            }

            if (centerDais == null)
            {
                var go = GameObject.Find("Castle_Coop_CenterDais");
                if (go != null) centerDais = go.transform;
            }

            if (portcullisGate == null)
            {
                var go = GameObject.Find("Castle_RearGate_Portcullis");
                if (go != null) portcullisGate = go.transform;
            }

            if (fatherLight == null && fatherPlate != null)
            {
                fatherLight = fatherPlate.GetComponentInChildren<Light>();
            }

            if (childLight == null && childPlate != null)
            {
                childLight = childPlate.GetComponentInChildren<Light>();
            }

            if (daisAuraLight == null && centerDais != null)
            {
                daisAuraLight = centerDais.GetComponentInChildren<Light>();
            }
        }

        private void BuildProceduralBalconyProps()
        {
            // 1. Balcony Lever
            if (balconyLever == null)
            {
                var existing = GameObject.Find("Castle_Balcony_Lever");
                if (existing != null)
                {
                    balconyLever = existing.transform;
                }
                else
                {
                    var leverRoot = new GameObject("Castle_Balcony_Lever");
                    leverRoot.transform.position = balconyLeverPos;

                    var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    baseGo.name = "Lever_Base";
                    baseGo.transform.SetParent(leverRoot.transform, false);
                    baseGo.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
                    var rend = baseGo.GetComponent<Renderer>();
                    if (rend != null) rend.material.color = new Color(0.25f, 0.22f, 0.20f);

                    var handlePivot = new GameObject("Lever_Handle_Pivot");
                    handlePivot.transform.SetParent(leverRoot.transform, false);
                    handlePivot.transform.localPosition = new Vector3(0f, 0.15f, 0f);

                    var handleGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    handleGo.name = "Lever_Handle";
                    handleGo.transform.SetParent(handlePivot.transform, false);
                    handleGo.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                    handleGo.transform.localScale = new Vector3(0.08f, 0.35f, 0.08f);
                    var hRend = handleGo.GetComponent<Renderer>();
                    if (hRend != null) hRend.material.color = new Color(0.85f, 0.65f, 0.25f);

                    var knobGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    knobGo.name = "Lever_Knob";
                    knobGo.transform.SetParent(handlePivot.transform, false);
                    knobGo.transform.localPosition = new Vector3(0f, 0.72f, 0f);
                    knobGo.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    var kRend = knobGo.GetComponent<Renderer>();
                    if (kRend != null) kRend.material.color = new Color(1f, 0.85f, 0.2f);

                    balconyLever = leverRoot.transform;
                    balconyLeverHandle = handlePivot.transform;

                    var lightGo = new GameObject("Balcony_Rune_Light");
                    lightGo.transform.SetParent(leverRoot.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                    var l = lightGo.AddComponent<Light>();
                    l.type = LightType.Point;
                    l.range = 6.0f;
                    l.intensity = 2.0f;
                    l.color = new Color(1f, 0.82f, 0.3f);
                    balconyRuneLight = l;
                }
            }

            // 2. Balcony Chain visual
            if (balconyChain == null)
            {
                var existingChain = GameObject.Find("Castle_Balcony_Chain_Lock");
                if (existingChain != null)
                {
                    balconyChain = existingChain.transform;
                }
                else
                {
                    var chainGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    chainGo.name = "Castle_Balcony_Chain_Lock";
                    chainGo.transform.position = new Vector3(4.25f, 27.5f, -126.0f);
                    chainGo.transform.localScale = new Vector3(0.12f, 7.5f, 0.12f);
                    chainGo.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
                    var r = chainGo.GetComponent<Renderer>();
                    if (r != null) r.material.color = new Color(0.3f, 0.3f, 0.35f);
                    var col = chainGo.GetComponent<Collider>();
                    if (col != null) col.enabled = false;
                    balconyChain = chainGo.transform;
                }
            }
        }

        private void Update()
        {
            if (promptTimer > 0f)
            {
                promptTimer -= Time.deltaTime;
                if (promptTimer <= 0f) currentPrompt = "";
            }

            if (!isGateUnlocked)
            {
                UpdateBalconyLeverInteraction();
                CheckPlateTriggers();

                if (isBalconyUnlocked && isFatherOnPlate && isChildOnPlate)
                {
                    UnlockGate();
                }
            }
            else
            {
                // Smoothly raise the portcullis gate
                if (portcullisGate != null && Vector3.Distance(portcullisGate.position, gateTargetPos) > 0.02f)
                {
                    portcullisGate.position = Vector3.MoveTowards(portcullisGate.position, gateTargetPos, openSpeed * Time.deltaTime);
                }
            }
        }

        private void UpdateBalconyLeverInteraction()
        {
            if (isBalconyUnlocked || isLeverAnimating) return;

            Vector3 checkPos = balconyLever != null ? balconyLever.position : balconyLeverPos;
            var cols = Physics.OverlapSphere(checkPos, 2.5f);
            bool playerNear = false;

            foreach (var col in cols)
            {
                if (col.isTrigger) continue;
                Transform p = col.transform;
                while (p != null)
                {
                    string n = p.name.ToLower();
                    if (n.Contains("player") || n.Contains("child") || n.Contains("son") || n.Contains("dad"))
                    {
                        playerNear = true;
                        break;
                    }
                    p = p.parent;
                }
                if (playerNear) break;
            }

            if (playerNear)
            {
                ShowPrompt("[E] Gạt Cần Khóa Xích Hoàng Gia", new Color(1f, 0.88f, 0.2f), 0.25f);

                if (IsInteractPressed())
                {
                    StartCoroutine(PullBalconyLeverRoutine());
                }
            }
        }

        private IEnumerator PullBalconyLeverRoutine()
        {
            isLeverAnimating = true;
            UISoundManager.PlayConfirm();

            if (balconyLeverHandle != null)
            {
                Quaternion start = balconyLeverHandle.localRotation;
                Quaternion target = start * Quaternion.Euler(55f, 0f, 0f);
                float dur = 0.6f;
                float elapsed = 0f;
                while (elapsed < dur)
                {
                    elapsed += Time.deltaTime;
                    balconyLeverHandle.localRotation = Quaternion.Slerp(start, target, elapsed / dur);
                    yield return null;
                }
            }

            isBalconyUnlocked = true;
            isLeverAnimating = false;

            // Retract / hide chain
            if (balconyChain != null)
            {
                balconyChain.gameObject.SetActive(false);
            }

            if (balconyRuneLight != null)
            {
                balconyRuneLight.color = new Color(0.3f, 1f, 0.5f);
                balconyRuneLight.intensity = 4.0f;
            }

            // Drop rope ladder if exists
            var rope = FindAnyObjectByType<CoopRopeLadderDrop>();
            if (rope != null && !rope.IsLadderDropped)
            {
                rope.DropLadder();
            }

            UISoundManager.PlaySuccess();
            ShowPrompt("✨ XÍCH HOÀNG GIA ĐÃ MỞ! 2 BỆ CỔ NGỮ ĐÃ ĐƯỢC KÍCH HOẠT!", new Color(0.4f, 1f, 0.5f), 4.5f);
            Debug.Log("<color=green>[CastleCourtyard] Cần gạt ban công đã mở xích! Năng lượng kích hoạt 2 bệ đá!</color>");
        }

        private void CheckPlateTriggers()
        {
            float checkRadius = 1.3f;

            if (fatherPlate != null)
            {
                isFatherOnPlate = CheckPlayerNear(fatherPlate.position, checkRadius, true);
                if (fatherLight != null)
                {
                    float targetInt = isFatherOnPlate ? (isBalconyUnlocked ? 2.5f : 0.8f) : 0f;
                    fatherLight.intensity = Mathf.MoveTowards(fatherLight.intensity, targetInt, Time.deltaTime * 3f);
                    fatherLight.color = isBalconyUnlocked ? new Color(1f, 0.85f, 0.2f) : new Color(0.8f, 0.4f, 0.4f);
                }
            }

            if (childPlate != null)
            {
                isChildOnPlate = CheckPlayerNear(childPlate.position, checkRadius, false);
                if (childLight != null)
                {
                    float targetInt = isChildOnPlate ? (isBalconyUnlocked ? 2.5f : 0.8f) : 0f;
                    childLight.intensity = Mathf.MoveTowards(childLight.intensity, targetInt, Time.deltaTime * 3f);
                    childLight.color = isBalconyUnlocked ? new Color(0.3f, 0.85f, 1f) : new Color(0.8f, 0.4f, 0.4f);
                }
            }

            // Nếu người chơi bước lên đĩa mà chưa mở xích ban công -> hiện prompt cảnh báo mách nước
            if (!isBalconyUnlocked && (isFatherOnPlate || isChildOnPlate))
            {
                ShowPrompt("⚠️ Cổng sau đang bị cùm xích trên ban công khóa chặt!\nCần Ba phóng Con lên ban công gạt cần mở xích!", new Color(1f, 0.4f, 0.4f), 0.25f);
            }
            else if (isBalconyUnlocked && (isFatherOnPlate ^ isChildOnPlate))
            {
                string waitRole = isFatherOnPlate ? "Con (Bệ Nguyệt Quang - Lam)" : "Ba (Bệ Thái Dương - Vàng)";
                ShowPrompt($"Đang chờ {waitRole} bước lên đĩa cùng lúc...", new Color(1f, 0.88f, 0.35f), 0.25f);
            }
        }

        private bool CheckPlayerNear(Vector3 center, float radius, bool isFatherCheck)
        {
            var colliders = Physics.OverlapSphere(center, radius);
            foreach (var col in colliders)
            {
                if (col.isTrigger) continue;
                string colName = col.gameObject.name.ToLower();
                Transform p = col.transform;
                while (p != null)
                {
                    string pName = p.name.ToLower();
                    if (isFatherCheck)
                    {
                        if (pName.Contains("dad") || pName.Contains("father") || pName.Contains("player_1") || pName.Contains("character_dad"))
                            return true;
                    }
                    else
                    {
                        if (pName.Contains("son") || pName.Contains("child") || pName.Contains("player_2") || pName.Contains("the child"))
                            return true;
                    }
                    p = p.parent;
                }

                // If general player tag and offline/single test
                if (col.CompareTag("Player") || colName.Contains("player") || colName.Contains("character"))
                {
                    return true;
                }
            }
            return false;
        }

        private void UnlockGate()
        {
            isGateUnlocked = true;
            Debug.Log("<color=cyan>[CastleCourtyard] ✨ CẢ HAI CHA CON ĐÃ KÍCH HOẠT ĐỒNG THỜI 2 BIA ĐÁ HOÀNG GIA! CỔNG SẬP ĐANG MỞ!</color>");

            UISoundManager.PlaySuccess();
            ShowPrompt("🎉 CỔNG HOÀNG GIA ĐÃ MỞ! TIẾN LÊN KHINH KHÍ CẦU ZEPHYR!", new Color(0.4f, 1f, 0.6f), 5.0f);

            if (daisAuraLight != null)
            {
                daisAuraLight.intensity = 5.0f;
                daisAuraLight.color = new Color(0.4f, 1f, 0.6f);
            }

            if (PlayScene3ObjectiveDirector.Instance != null)
            {
                PlayScene3ObjectiveDirector.Instance.SetCastleCourtyardCompleted();
            }
        }

        private bool IsInteractPressed()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) return true;
            if (Gamepad.current != null && (Gamepad.current.buttonSouth.wasPressedThisFrame || Gamepad.current.buttonWest.wasPressedThisFrame)) return true;
            return false;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }

        private void ShowPrompt(string text, Color col, float duration)
        {
            currentPrompt = text;
            promptColor = col;
            promptTimer = duration;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(currentPrompt) || promptTimer <= 0f) return;

            GUI.depth = -100;
            var boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.alignment = TextAnchor.MiddleCenter;
            boxStyle.fontSize = 18;
            boxStyle.fontStyle = FontStyle.Bold;
            boxStyle.normal.textColor = promptColor;

            float width = 820f;
            float height = 58f;
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height - 110f;

            GUI.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0.92f);
            GUI.Box(new Rect(x, y, width, height), currentPrompt, boxStyle);
        }
    }
}
