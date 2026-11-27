using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Component hỗ trợ bắt sự kiện kéo chuột xoay 360 độ trên khung Preview.
    /// Tương thích hoàn toàn với New Input System thông qua EventSystem UI handlers.
    /// </summary>
    public class StorePreviewDragRotator : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public StorePreviewStage stage;
        public float sensitivity = 1.2f;

        public void OnPointerDown(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            if (stage != null)
            {
                stage.RotateModel(eventData.delta.x * sensitivity);
            }
        }
    }

    /// <summary>
    /// Điều khiển toàn bộ Giao diện Cửa hàng (Store), Bộ Sưu Tập (Collection) và Nạp Coin trên Lobby Canvas.
    /// - Thiết kế chuẩn Academic Monochrome: Nền xám, chữ trắng, khung viền trắng sắc cạnh 90°.
    /// - Kích thước rộng rãi, thoáng đãng, chống chen chúc chữ.
    /// - Xem trước 3D xoay 360 độ qua RenderTexture.
    /// - Hỗ trợ Cửa Hàng (mua skin) và Bộ Sưu Tập (trang bị skin đã có hoặc về mặc định).
    /// - Đồng bộ trực tiếp model nhân vật trong Lobby 3D thời gian thực.
    /// - Cơ chế Fallback Offline tự động đảm bảo catalog luôn sẵn sàng trong Play Mode.
    /// </summary>
    public class StoreUIController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Canvas lobbyCanvas;
        [SerializeField] private StorePreviewStage previewStage;

        // ─── Data DTOs ──────────────────────────────────────────────
        [Serializable]
        public class SkinDto
        {
            public string skinId;
            public string name;
            public string role;
            public int price;
            public bool isActive;
        }

        [Serializable]
        private class CatalogResponse
        {
            public SkinDto[] skins;
        }

        [Serializable]
        private class InventoryResponse
        {
            public SkinDto[] skins;
            public string equippedDadSkinId;
            public string equippedChildSkinId;
        }

        [Serializable]
        private class BalanceResponse
        {
            public int coins;
            public bool sandboxEnabled;
        }

        [Serializable]
        private class TopupRequest
        {
            public string packageId;
            public string requestId;
        }

        [Serializable]
        private class BuyRequest
        {
            public string skinId;
        }

        [Serializable]
        private class EquipRequest
        {
            public string skinId;
            public string role;
        }

        [Serializable]
        private class ActionResult
        {
            public int coins;
            public string transactionId;
            public string equippedDadSkinId;
            public string equippedChildSkinId;
        }

        // ─── State ──────────────────────────────────────────────────
        public enum ViewMode { Store, Collection }
        private ViewMode m_CurrentMode = ViewMode.Store;

        private int m_CurrentCoins = 500;
        private readonly List<SkinDto> m_Catalog = new List<SkinDto>();
        private readonly HashSet<string> m_OwnedSkinIds = new HashSet<string>();
        private string m_EquippedDadSkinId;
        private string m_EquippedChildSkinId;
        private string m_SelectedSkinId;
        private string m_RoleFilter = "ALL"; // ALL, Dad, Child
        private bool m_IsBusy;

        private readonly List<GameObject> m_HiddenPanelsDuringStore = new List<GameObject>();

        // ─── UI References ──────────────────────────────────────────
        private Button m_TopStoreButton;
        private Button m_CoinBadgeButton;
        private TextMeshProUGUI m_CoinBadgeText;

        private GameObject m_StoreModal;
        private GameObject m_TopupModal;
        private Transform m_SkinCardsContainer;
        private RawImage m_PreviewRawImage;
        private TextMeshProUGUI m_StoreCoinText;
        private TextMeshProUGUI m_PreviewSkinNameText;
        private TextMeshProUGUI m_PreviewSkinPriceText;
        private Button m_PreviewActionButton;
        private TextMeshProUGUI m_PreviewActionText;
        private TextMeshProUGUI m_StatusToastText;

        private Button m_ModeStoreBtn;
        private Button m_ModeCollectionBtn;
        private TextMeshProUGUI m_ModeStoreText;
        private TextMeshProUGUI m_ModeCollectionText;

        private Button m_FilterAllBtn;
        private Button m_FilterDadBtn;
        private Button m_FilterChildBtn;
        private TextMeshProUGUI m_FilterAllText;
        private TextMeshProUGUI m_FilterDadText;
        private TextMeshProUGUI m_FilterChildText;

        private TMP_FontAsset m_FontAsset;

        // ─── Palette Constants: Monochrome (Xám - Trắng - Khung Trắng) ────
        private static readonly Color ModalBgColor = new Color(0.08f, 0.09f, 0.11f, 0.98f);     // Nền xám đậm #14171C
        private static readonly Color HeaderBgColor = new Color(0.06f, 0.07f, 0.08f, 1f);       // Nền xám header #0F1214
        private static readonly Color CardBgColor = new Color(0.12f, 0.13f, 0.16f, 1f);         // Nền xám card #1F2129
        private static readonly Color CardBgSelected = new Color(0.18f, 0.20f, 0.24f, 1f);      // Nền xám card được chọn
        private static readonly Color PanelInnerBg = new Color(0.09f, 0.10f, 0.12f, 1f);        // Nền khung chứa #171A1F
        private static readonly Color BorderWhite = Color.white;                                // Viền trắng thuần
        private static readonly Color BorderSubtle = new Color(0.45f, 0.48f, 0.54f, 1f);        // Viền phụ xám sáng
        private static readonly Color ButtonDarkBg = new Color(0.14f, 0.15f, 0.18f, 1f);        // Nền nút xám đậm
        private static readonly Color TextWhite = Color.white;                                  // Chữ trắng
        private static readonly Color TextMuted = new Color(0.78f, 0.81f, 0.86f, 1f);           // Chữ xám trắng sáng

        private void Awake()
        {
            if (lobbyCanvas == null)
            {
                lobbyCanvas = GetComponentInParent<Canvas>();
                if (lobbyCanvas == null)
                {
                    lobbyCanvas = FindAnyObjectByType<Canvas>();
                }
            }

            var tmpSample = lobbyCanvas != null ? lobbyCanvas.GetComponentInChildren<TextMeshProUGUI>(true) : null;
            if (tmpSample != null)
            {
                m_FontAsset = tmpSample.font;
            }

            EnsurePreviewStage();
            BuildOrBindUI();
        }

        private void Start()
        {
            LoadLocalCacheOrDefaults();
            if (ApiClient.IsLoggedIn)
            {
                _ = RefreshWalletAndInventoryAsync();
            }
        }

        private void OnEnable()
        {
            if (ApiClient.IsLoggedIn)
            {
                _ = RefreshWalletAndInventoryAsync();
            }
        }

        private void EnsurePreviewStage()
        {
            if (previewStage == null)
            {
                var stageGo = new GameObject("[Store_Preview_Stage_Manager]");
                stageGo.transform.SetParent(transform, false);
                previewStage = stageGo.AddComponent<StorePreviewStage>();
            }
            previewStage.Initialize();
        }

        // ─── UI Building / Binding ──────────────────────────────────

        private void BuildOrBindUI()
        {
            if (lobbyCanvas == null) return;

            CleanOldDuplicateButtons();

            // 1. Nút Cửa Hàng và Huy hiệu Coin duy nhất ở góc trên Canvas
            BindOrBuildLobbyTriggers();

            // 2. Cửa sổ Store Modal (Kích thước rộng rãi 1140x700, viền trắng sắc nét)
            BuildStoreModal();

            // 3. Cửa sổ Topup Modal (Kích thước rộng rãi 720x480, viền trắng sắc nét)
            BuildTopupModal();
        }

        private void CleanOldDuplicateButtons()
        {
            string[] oldButtonNames = { "store-button-main", "store-button-lobby", "store-button" };
            foreach (var bName in oldButtonNames)
            {
                foreach (var btn in lobbyCanvas.GetComponentsInChildren<Button>(true))
                {
                    if (btn.gameObject.name == bName)
                    {
                        Destroy(btn.gameObject);
                    }
                }
            }
        }

        private void BindOrBuildLobbyTriggers()
        {
            // 1. Nút CỬA HÀNG (Khung trắng, nền xám, chữ trắng)
            var existingTopBtn = lobbyCanvas.transform.Find("store-button-top");
            if (existingTopBtn == null)
            {
                var btnGo = CreateBorderedButton(lobbyCanvas.transform, "store-button-top", "🛒 CỬA HÀNG", new Vector2(165, 42), ButtonDarkBg, BorderWhite, TextWhite, 1.5f);
                var rt = btnGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-235f, -20f);
                m_TopStoreButton = btnGo.GetComponent<Button>();
            }
            else
            {
                m_TopStoreButton = existingTopBtn.GetComponent<Button>();
            }
            m_TopStoreButton.onClick.RemoveAllListeners();
            m_TopStoreButton.onClick.AddListener(OpenStore);

            // 2. Huy hiệu Coins (Khung trắng, nền xám, chữ trắng)
            var existingCoinBadge = lobbyCanvas.transform.Find("coin-badge-button");
            if (existingCoinBadge == null)
            {
                var badgeGo = CreateBorderedButton(lobbyCanvas.transform, "coin-badge-button", $"Coins: {m_CurrentCoins:N0}", new Vector2(160, 42), ButtonDarkBg, BorderWhite, TextWhite, 1.5f);
                var rt = badgeGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-410f, -20f);
                m_CoinBadgeButton = badgeGo.GetComponent<Button>();
                m_CoinBadgeText = badgeGo.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                m_CoinBadgeButton = existingCoinBadge.GetComponent<Button>();
                m_CoinBadgeText = existingCoinBadge.GetComponentInChildren<TextMeshProUGUI>();
            }
            m_CoinBadgeButton.onClick.RemoveAllListeners();
            m_CoinBadgeButton.onClick.AddListener(OpenTopup);
        }

        private void BuildStoreModal()
        {
            if (m_StoreModal != null) return;

            // Root Backdrop
            m_StoreModal = CreatePanel(lobbyCanvas.transform, "store-modal-panel", new Color(0f, 0f, 0f, 0.88f));
            var rootRt = m_StoreModal.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            m_StoreModal.SetActive(false);

            // Dialog Window (Rộng rãi 1140x700, Khung viền trắng 2px, Nền xám)
            var dialog = CreateBorderedPanel(m_StoreModal.transform, "store-dialog", ModalBgColor, BorderWhite, 2f);
            var dialogRt = dialog.GetComponent<RectTransform>();
            dialogRt.anchorMin = dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(1140, 700);

            var dialogBody = dialog.transform.Find("InnerBg");

            // Header Bar
            var header = CreatePanel(dialogBody, "header-bar", HeaderBgColor);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 62);

            // Đường kẻ phân cách Header
            var headerSep = CreatePanel(header.transform, "sep", BorderSubtle);
            var sepRt = headerSep.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0f, 0f);
            sepRt.anchorMax = new Vector2(1f, 0f);
            sepRt.sizeDelta = new Vector2(0, 1.5f);

            var titleText = CreateText(header.transform, "title", "TRANG PHỤC NHÂN VẬT", 22, FontStyles.Bold, TextWhite);
            var titleRt = titleText.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(28, 0);

            m_StoreCoinText = CreateText(header.transform, "coin-display", $"Coins: {m_CurrentCoins:N0}", 17, FontStyles.Bold, TextWhite);
            var coinRt = m_StoreCoinText.GetComponent<RectTransform>();
            coinRt.anchorMin = coinRt.anchorMax = new Vector2(1f, 0.5f);
            coinRt.pivot = new Vector2(1f, 0.5f);
            coinRt.anchoredPosition = new Vector2(-220, 0);

            var quickTopupBtn = CreateBorderedButton(header.transform, "quick-topup", "+ NẠP COIN", new Vector2(130, 38), ButtonDarkBg, BorderWhite, TextWhite, 1.5f);
            var quickRt = quickTopupBtn.GetComponent<RectTransform>();
            quickRt.anchorMin = quickRt.anchorMax = new Vector2(1f, 0.5f);
            quickRt.pivot = new Vector2(1f, 0.5f);
            quickRt.anchoredPosition = new Vector2(-75, 0);
            quickTopupBtn.GetComponent<Button>().onClick.AddListener(OpenTopup);

            var closeBtn = CreateBorderedButton(header.transform, "close-btn", "✕", new Vector2(38, 38), ButtonDarkBg, BorderWhite, TextWhite, 1.5f);
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-16, 0);
            closeBtn.GetComponent<Button>().onClick.AddListener(CloseStore);

            // Mode Selection Bar: [CỬA HÀNG (MUA)] và [BỘ SƯU TẬP (ĐÃ CÓ)]
            var modeBar = CreatePanel(dialogBody, "mode-bar", HeaderBgColor);
            var modeRt = modeBar.GetComponent<RectTransform>();
            modeRt.anchorMin = new Vector2(0f, 1f);
            modeRt.anchorMax = new Vector2(1f, 1f);
            modeRt.pivot = new Vector2(0.5f, 1f);
            modeRt.anchoredPosition = new Vector2(0, -62);
            modeRt.sizeDelta = new Vector2(0, 50);

            // Đường kẻ dưới Mode Bar
            var modeSep = CreatePanel(modeBar.transform, "sep", BorderSubtle);
            var modeSepRt = modeSep.GetComponent<RectTransform>();
            modeSepRt.anchorMin = new Vector2(0f, 0f);
            modeSepRt.anchorMax = new Vector2(1f, 0f);
            modeSepRt.sizeDelta = new Vector2(0, 1.5f);

            // Mode Buttons
            var modeStoreGo = CreateBorderedButton(modeBar.transform, "mode-store", "🛍️ CỬA HÀNG (MUA)", new Vector2(210, 38), Color.white, BorderWhite, Color.black, 1.5f);
            var msRt = modeStoreGo.GetComponent<RectTransform>();
            msRt.anchorMin = msRt.anchorMax = new Vector2(0f, 0.5f);
            msRt.pivot = new Vector2(0f, 0.5f);
            msRt.anchoredPosition = new Vector2(24, 0);
            m_ModeStoreBtn = modeStoreGo.GetComponent<Button>();
            m_ModeStoreText = modeStoreGo.GetComponentInChildren<TextMeshProUGUI>();
            m_ModeStoreBtn.onClick.AddListener(() => SwitchMode(ViewMode.Store));

            var modeColGo = CreateBorderedButton(modeBar.transform, "mode-col", "👔 BỘ SƯU TẬP (ĐÃ CÓ)", new Vector2(230, 38), ButtonDarkBg, BorderSubtle, TextWhite, 1.5f);
            var mcRt = modeColGo.GetComponent<RectTransform>();
            mcRt.anchorMin = mcRt.anchorMax = new Vector2(0f, 0.5f);
            mcRt.pivot = new Vector2(0f, 0.5f);
            mcRt.anchoredPosition = new Vector2(246, 0);
            m_ModeCollectionBtn = modeColGo.GetComponent<Button>();
            m_ModeCollectionText = modeColGo.GetComponentInChildren<TextMeshProUGUI>();
            m_ModeCollectionBtn.onClick.AddListener(() => SwitchMode(ViewMode.Collection));

            // Role Filter Sub-Tabs: [Tất Cả], [Cha], [Con]
            var filterAllGo = CreateBorderedButton(modeBar.transform, "tab-all", "Tất Cả", new Vector2(110, 34), Color.white, BorderWhite, Color.black, 1.5f);
            var faRt = filterAllGo.GetComponent<RectTransform>();
            faRt.anchorMin = faRt.anchorMax = new Vector2(1f, 0.5f);
            faRt.pivot = new Vector2(1f, 0.5f);
            faRt.anchoredPosition = new Vector2(-260, 0);
            m_FilterAllBtn = filterAllGo.GetComponent<Button>();
            m_FilterAllText = filterAllGo.GetComponentInChildren<TextMeshProUGUI>();
            m_FilterAllBtn.onClick.AddListener(() => SetFilter("ALL"));

            var filterDadGo = CreateBorderedButton(modeBar.transform, "tab-dad", "Cha (Dad)", new Vector2(115, 34), ButtonDarkBg, BorderSubtle, TextWhite, 1.5f);
            var fdRt = filterDadGo.GetComponent<RectTransform>();
            fdRt.anchorMin = fdRt.anchorMax = new Vector2(1f, 0.5f);
            fdRt.pivot = new Vector2(1f, 0.5f);
            fdRt.anchoredPosition = new Vector2(-135, 0);
            m_FilterDadBtn = filterDadGo.GetComponent<Button>();
            m_FilterDadText = filterDadGo.GetComponentInChildren<TextMeshProUGUI>();
            m_FilterDadBtn.onClick.AddListener(() => SetFilter("Dad"));

            var filterChildGo = CreateBorderedButton(modeBar.transform, "tab-child", "Con (Child)", new Vector2(115, 34), ButtonDarkBg, BorderSubtle, TextWhite, 1.5f);
            var fcRt = filterChildGo.GetComponent<RectTransform>();
            fcRt.anchorMin = fcRt.anchorMax = new Vector2(1f, 0.5f);
            fcRt.pivot = new Vector2(1f, 0.5f);
            fcRt.anchoredPosition = new Vector2(-10, 0);
            m_FilterChildBtn = filterChildGo.GetComponent<Button>();
            m_FilterChildText = filterChildGo.GetComponentInChildren<TextMeshProUGUI>();
            m_FilterChildBtn.onClick.AddListener(() => SetFilter("Child"));

            // Body Area (Split Left: List ~58%, Right: 3D Preview ~42%)
            var body = CreatePanel(dialogBody, "body-area", Color.clear);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(24, 44);
            bodyRt.offsetMax = new Vector2(-24, -120);

            // Left: Scroll List (Khung viền trắng 1.5px, Nền xám đậm)
            var leftPanel = CreateBorderedPanel(body.transform, "catalog-scroll-panel", PanelInnerBg, BorderSubtle, 1.5f);
            var leftRt = leftPanel.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0.56f, 1f);
            leftRt.offsetMin = leftRt.offsetMax = Vector2.zero;

            var leftInner = leftPanel.transform.Find("InnerBg");

            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGo.transform.SetParent(leftInner, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(8, 8);
            scrollRt.offsetMax = new Vector2(-8, -8);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.05f);
            scrollGo.GetComponent<Mask>().showMaskGraphic = false;

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.sizeDelta = new Vector2(0, 0);

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10;
            vlg.padding = new RectOffset(6, 6, 6, 6);
            vlg.childForceExpandHeight = false;
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = scrollGo.GetComponent<ScrollRect>();
            sr.content = contentRt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;

            m_SkinCardsContainer = contentGo.transform;

            // Right: 3D Preview Stage (Khung viền trắng 1.5px, Nền xám đậm)
            var rightPanel = CreateBorderedPanel(body.transform, "preview-panel", PanelInnerBg, BorderSubtle, 1.5f);
            var rightRt = rightPanel.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(0.58f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = rightRt.offsetMax = Vector2.zero;

            var rightInner = rightPanel.transform.Find("InnerBg");

            // Preview RawImage (Hiển thị model 3D)
            var rawImageGo = new GameObject("PreviewRawImage", typeof(RectTransform), typeof(RawImage));
            rawImageGo.transform.SetParent(rightInner, false);
            var rawRt = rawImageGo.GetComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.04f, 0.28f);
            rawRt.anchorMax = new Vector2(0.96f, 0.96f);
            rawRt.offsetMin = rawRt.offsetMax = Vector2.zero;

            m_PreviewRawImage = rawImageGo.GetComponent<RawImage>();
            m_PreviewRawImage.raycastTarget = true;
            if (previewStage != null && previewStage.TargetTexture != null)
            {
                m_PreviewRawImage.texture = previewStage.TargetTexture;
            }

            // Gắn component bắt sự kiện kéo chuột xoay 360 độ
            var dragRotator = rawImageGo.AddComponent<StorePreviewDragRotator>();
            dragRotator.stage = previewStage;
            dragRotator.sensitivity = 1.2f;

            var hintText = CreateText(rightInner, "hint", "Kéo chuột trái để xoay 360°", 13, FontStyles.Italic, TextMuted);
            var hintRt = hintText.GetComponent<RectTransform>();
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.25f);
            hintRt.anchoredPosition = Vector2.zero;

            m_PreviewSkinNameText = CreateText(rightInner, "skin-name", "Chọn trang phục", 18, FontStyles.Bold, TextWhite);
            var nameRt = m_PreviewSkinNameText.GetComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 0.18f);
            nameRt.anchoredPosition = Vector2.zero;

            m_PreviewSkinPriceText = CreateText(rightInner, "skin-price", "", 15, FontStyles.Normal, TextMuted);
            var priceRt = m_PreviewSkinPriceText.GetComponent<RectTransform>();
            priceRt.anchorMin = priceRt.anchorMax = new Vector2(0.5f, 0.12f);
            priceRt.anchoredPosition = Vector2.zero;

            var actionBtnGo = CreateBorderedButton(rightInner, "action-btn", "MUA", new Vector2(230, 42), Color.white, BorderWhite, Color.black, 1.5f);
            var actionRt = actionBtnGo.GetComponent<RectTransform>();
            actionRt.anchorMin = actionRt.anchorMax = new Vector2(0.5f, 0.05f);
            actionRt.anchoredPosition = Vector2.zero;
            m_PreviewActionButton = actionBtnGo.GetComponent<Button>();
            m_PreviewActionText = actionBtnGo.GetComponentInChildren<TextMeshProUGUI>();
            m_PreviewActionButton.onClick.AddListener(OnPreviewActionClicked);

            // Bottom Status / Toast
            m_StatusToastText = CreateText(dialogBody, "toast", "", 14, FontStyles.Normal, TextWhite);
            var toastRt = m_StatusToastText.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0f, 0f);
            toastRt.anchorMax = new Vector2(1f, 0f);
            toastRt.pivot = new Vector2(0.5f, 0f);
            toastRt.anchoredPosition = new Vector2(0, 12);
            toastRt.sizeDelta = new Vector2(0, 26);
        }

        private void BuildTopupModal()
        {
            if (m_TopupModal != null) return;

            m_TopupModal = CreatePanel(lobbyCanvas.transform, "topup-modal-panel", new Color(0f, 0f, 0f, 0.88f));
            var rootRt = m_TopupModal.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            m_TopupModal.SetActive(false);

            // Dialog Window (Rộng rãi 720x480, Viền trắng 2px, Nền xám)
            var dialog = CreateBorderedPanel(m_TopupModal.transform, "topup-dialog", ModalBgColor, BorderWhite, 2f);
            var dialogRt = dialog.GetComponent<RectTransform>();
            dialogRt.anchorMin = dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(720, 480);

            var dialogBody = dialog.transform.Find("InnerBg");

            // Header
            var header = CreatePanel(dialogBody, "header", HeaderBgColor);
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 56);

            var headerSep = CreatePanel(header.transform, "sep", BorderSubtle);
            var sepRt = headerSep.GetComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(0f, 0f);
            sepRt.anchorMax = new Vector2(1f, 0f);
            sepRt.sizeDelta = new Vector2(0, 1.5f);

            var title = CreateText(header.transform, "title", "CỔNG NẠP COIN (SANDBOX)", 20, FontStyles.Bold, TextWhite);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(24, 0);

            var closeBtn = CreateBorderedButton(header.transform, "close-btn", "✕", new Vector2(36, 36), ButtonDarkBg, BorderWhite, TextWhite, 1.5f);
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-14, 0);
            closeBtn.GetComponent<Button>().onClick.AddListener(CloseTopup);

            // Package Cards Area
            var body = CreatePanel(dialogBody, "body", Color.clear);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(28, 48);
            bodyRt.offsetMax = new Vector2(-28, -68);

            CreatePackageCard(body.transform, "vnd_10000", "GÓI CƠ BẢN", "10.000 VNĐ", "100 COINS", new Vector2(0, 60));
            CreatePackageCard(body.transform, "vnd_50000", "GÓI PHỔ BIẾN (+10% BONUS)", "50.000 VNĐ", "550 COINS", new Vector2(0, -60));

            var infoText = CreateText(dialogBody, "info", "Môi trường Sandbox thử nghiệm — Nạp coin không phát sinh chi phí thật.", 13, FontStyles.Italic, TextMuted);
            var infoRt = infoText.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0.5f, 0f);
            infoRt.anchorMax = new Vector2(0.5f, 0f);
            infoRt.anchoredPosition = new Vector2(0, 20);
        }

        private void CreatePackageCard(Transform parent, string packageId, string title, string price, string coins, Vector2 pos)
        {
            var card = CreateBorderedPanel(parent, "pkg-" + packageId, CardBgColor, BorderSubtle, 1.5f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 0.5f);
            cardRt.anchorMax = new Vector2(1f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = pos;
            cardRt.sizeDelta = new Vector2(0, 96);

            var inner = card.transform.Find("InnerBg");

            var t1 = CreateText(inner, "t1", title, 15, FontStyles.Bold, TextWhite);
            t1.alignment = TextAlignmentOptions.Left;
            var t1Rt = t1.GetComponent<RectTransform>();
            t1Rt.anchorMin = t1Rt.anchorMax = new Vector2(0f, 0.5f);
            t1Rt.pivot = new Vector2(0f, 0.5f);
            t1Rt.anchoredPosition = new Vector2(24, 18);
            t1Rt.sizeDelta = new Vector2(350, 30);

            var t2 = CreateText(inner, "t2", coins, 18, FontStyles.Bold, TextWhite);
            t2.alignment = TextAlignmentOptions.Left;
            var t2Rt = t2.GetComponent<RectTransform>();
            t2Rt.anchorMin = t2Rt.anchorMax = new Vector2(0f, 0.5f);
            t2Rt.pivot = new Vector2(0f, 0.5f);
            t2Rt.anchoredPosition = new Vector2(24, -18);
            t2Rt.sizeDelta = new Vector2(350, 30);

            var btn = CreateBorderedButton(inner, "buy-btn", price + " (Test Pay)", new Vector2(200, 44), Color.white, BorderWhite, Color.black, 1.5f);
            var btnRt = btn.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.anchoredPosition = new Vector2(-20, 0);

            btn.GetComponent<Button>().onClick.AddListener(() => _ = ExecuteTopupAsync(packageId));
        }

        // ─── Actions & Logic ────────────────────────────────────────

        public void SwitchMode(ViewMode mode)
        {
            m_CurrentMode = mode;
            UpdateModeTabsVisual();
            RebuildSkinCards();
        }

        private void UpdateModeTabsVisual()
        {
            if (m_ModeStoreBtn != null && m_ModeStoreText != null)
            {
                bool isStore = (m_CurrentMode == ViewMode.Store);
                SetButtonMonochromeStyle(m_ModeStoreBtn, m_ModeStoreText, isStore);
            }

            if (m_ModeCollectionBtn != null && m_ModeCollectionText != null)
            {
                bool isCol = (m_CurrentMode == ViewMode.Collection);
                SetButtonMonochromeStyle(m_ModeCollectionBtn, m_ModeCollectionText, isCol);
            }
        }

        public void SetFilter(string filter)
        {
            m_RoleFilter = filter;
            UpdateFilterTabsVisual();
            RebuildSkinCards();
        }

        private void UpdateFilterTabsVisual()
        {
            if (m_FilterAllBtn != null && m_FilterAllText != null)
                SetButtonMonochromeStyle(m_FilterAllBtn, m_FilterAllText, m_RoleFilter == "ALL");
            if (m_FilterDadBtn != null && m_FilterDadText != null)
                SetButtonMonochromeStyle(m_FilterDadBtn, m_FilterDadText, m_RoleFilter == "Dad");
            if (m_FilterChildBtn != null && m_FilterChildText != null)
                SetButtonMonochromeStyle(m_FilterChildBtn, m_FilterChildText, m_RoleFilter == "Child");
        }

        private void SetButtonMonochromeStyle(Button btn, TextMeshProUGUI label, bool isActive)
        {
            var inner = btn.transform.Find("InnerBg");
            var innerImg = inner != null ? inner.GetComponent<Image>() : btn.GetComponent<Image>();
            var outerImg = btn.GetComponent<Image>();

            if (isActive)
            {
                if (innerImg != null) innerImg.color = Color.white;
                if (outerImg != null) outerImg.color = Color.white;
                if (label != null) label.color = Color.black;
            }
            else
            {
                if (innerImg != null) innerImg.color = ButtonDarkBg;
                if (outerImg != null) outerImg.color = BorderSubtle;
                if (label != null) label.color = TextWhite;
            }
        }

        public void OpenStore()
        {
            UISoundManager.PlayClick();
            HideBackgroundPanels();

            if (m_StoreModal != null)
            {
                m_StoreModal.SetActive(true);
            }

            EnsureCatalogPopulated();
            _ = RefreshWalletAndInventoryAsync();
        }

        public void CloseStore()
        {
            UISoundManager.PlayCancel();

            if (m_StoreModal != null)
            {
                m_StoreModal.SetActive(false);
            }

            RestoreBackgroundPanels();
            Lobby3DManager.Instance?.RefreshEquippedSkinsInLobby();
        }

        public void OpenTopup()
        {
            UISoundManager.PlayClick();

            if (m_StoreModal == null || !m_StoreModal.activeSelf)
            {
                HideBackgroundPanels();
            }

            if (m_TopupModal != null)
            {
                m_TopupModal.SetActive(true);
            }
        }

        public void CloseTopup()
        {
            UISoundManager.PlayCancel();

            if (m_TopupModal != null)
            {
                m_TopupModal.SetActive(false);
            }

            if (m_StoreModal == null || !m_StoreModal.activeSelf)
            {
                RestoreBackgroundPanels();
            }
        }

        private void HideBackgroundPanels()
        {
            m_HiddenPanelsDuringStore.Clear();
            string[] panelsToHide = {
                "main-buttons-panel",
                "lobby-buttons-panel",
                "room-info-panel",
                "session-list-panel",
                "chat-panel",
                "tutorial-dialog-panel",
                "join-code-panel",
                "voice-settings-panel"
            };

            foreach (var panelName in panelsToHide)
            {
                var panel = FindInCanvas<Transform>(panelName);
                if (panel != null && panel.gameObject.activeSelf)
                {
                    panel.gameObject.SetActive(false);
                    m_HiddenPanelsDuringStore.Add(panel.gameObject);
                }
            }
        }

        private void RestoreBackgroundPanels()
        {
            foreach (var panel in m_HiddenPanelsDuringStore)
            {
                if (panel != null)
                {
                    panel.SetActive(true);
                }
            }
            m_HiddenPanelsDuringStore.Clear();
        }

        private void LoadLocalCacheOrDefaults()
        {
            // Tải số coin offline mock để test Play Mode không bị chặn
            if (!ApiClient.IsLoggedIn)
            {
                m_CurrentCoins = PlayerPrefs.GetInt("HMH.Store.LocalCoins", 500);
                string savedOwned = PlayerPrefs.GetString("HMH.Store.LocalOwned", "");
                if (!string.IsNullOrEmpty(savedOwned))
                {
                    foreach (var id in savedOwned.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        m_OwnedSkinIds.Add(id);
                    }
                }
                m_EquippedDadSkinId = PlayerPrefs.GetString("HMH.Store.LocalDad", "");
                m_EquippedChildSkinId = PlayerPrefs.GetString("HMH.Store.LocalChild", "");
                UpdateCoinsDisplay();
            }

            EnsureCatalogPopulated();
            RebuildSkinCards();
            SelectSkin(string.IsNullOrEmpty(m_SelectedSkinId) ? "default_dad" : m_SelectedSkinId);
        }

        private void EnsureCatalogPopulated()
        {
            if (m_Catalog.Count > 0) return;

            // Nạp dữ liệu fallback chuẩn từ SkinDatabase
            m_Catalog.Add(new SkinDto
            {
                skinId = "dad_skin_flannel_01",
                name = "Áo Sơ Mi Ca Rô",
                role = "Dad",
                price = 150,
                isActive = true
            });

            m_Catalog.Add(new SkinDto
            {
                skinId = "child_skin_01",
                name = "Đồ Thám Hiểm",
                role = "Child",
                price = 200,
                isActive = true
            });
        }

        private async Task RefreshWalletAndInventoryAsync()
        {
            if (m_IsBusy) return;
            m_IsBusy = true;

            try
            {
                if (ApiClient.IsLoggedIn)
                {
                    // 1. Tải Balance
                    var balanceJson = await ApiClient.GetAsync("/wallet/balance");
                    var balance = JsonUtility.FromJson<BalanceResponse>(balanceJson);
                    m_CurrentCoins = balance.coins;
                    UpdateCoinsDisplay();

                    // 2. Tải Catalog
                    var catalogJson = await ApiClient.GetAsync("/store/catalog");
                    var catalogRes = JsonUtility.FromJson<CatalogResponse>(catalogJson);
                    if (catalogRes?.skins != null && catalogRes.skins.Length > 0)
                    {
                        m_Catalog.Clear();
                        m_Catalog.AddRange(catalogRes.skins);
                    }

                    // 3. Tải Inventory & Equipped
                    var inventoryJson = await ApiClient.GetAsync("/store/inventory");
                    var inventoryRes = JsonUtility.FromJson<InventoryResponse>(inventoryJson);
                    m_OwnedSkinIds.Clear();
                    if (inventoryRes?.skins != null)
                    {
                        foreach (var s in inventoryRes.skins)
                        {
                            m_OwnedSkinIds.Add(s.skinId);
                        }
                    }
                    m_EquippedDadSkinId = inventoryRes?.equippedDadSkinId;
                    m_EquippedChildSkinId = inventoryRes?.equippedChildSkinId;

                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Dad", m_EquippedDadSkinId ?? string.Empty);
                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Child", m_EquippedChildSkinId ?? string.Empty);
                }
                else
                {
                    LoadLocalCacheOrDefaults();
                }

                EnsureCatalogPopulated();
                Lobby3DManager.Instance?.RefreshEquippedSkinsInLobby();
                RebuildSkinCards();

                if (string.IsNullOrEmpty(m_SelectedSkinId))
                {
                    SelectSkin(m_CurrentMode == ViewMode.Collection ? "default_dad" : (m_Catalog.Count > 0 ? m_Catalog[0].skinId : "default_dad"));
                }
                else
                {
                    SelectSkin(m_SelectedSkinId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StoreUIController] Đang chạy chế độ Offline Fallback ({ex.Message})");
                EnsureCatalogPopulated();
                RebuildSkinCards();
            }
            finally
            {
                m_IsBusy = false;
            }
        }

        private void RebuildSkinCards()
        {
            if (m_SkinCardsContainer == null) return;

            for (int i = m_SkinCardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(m_SkinCardsContainer.GetChild(i).gameObject);
            }

            // Nếu đang ở tab BỘ SƯU TẬP: luôn đưa trang phục Mặc Định lên đầu tiên
            if (m_CurrentMode == ViewMode.Collection)
            {
                if (m_RoleFilter == "ALL" || m_RoleFilter == "Dad")
                {
                    CreateSkinCard(new SkinDto { skinId = "default_dad", name = "Trang phục Mặc Định", role = "Dad", price = 0, isActive = true }, isDefault: true);
                }
                if (m_RoleFilter == "ALL" || m_RoleFilter == "Child")
                {
                    CreateSkinCard(new SkinDto { skinId = "default_child", name = "Trang phục Mặc Định", role = "Child", price = 0, isActive = true }, isDefault: true);
                }
            }

            foreach (var skin in m_Catalog)
            {
                if (m_RoleFilter != "ALL" && !string.Equals(skin.role, m_RoleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool isOwned = m_OwnedSkinIds.Contains(skin.skinId);

                // Trong tab Bộ Sưu Tập, chỉ hiển thị skin đã mua
                if (m_CurrentMode == ViewMode.Collection && !isOwned)
                {
                    continue;
                }

                CreateSkinCard(skin, isDefault: false);
            }
        }

        private void CreateSkinCard(SkinDto skin, bool isDefault)
        {
            bool isDad = string.Equals(skin.role, "Dad", StringComparison.OrdinalIgnoreCase);
            bool isOwned = isDefault || m_OwnedSkinIds.Contains(skin.skinId);
            bool isEquipped = false;

            if (isDefault)
            {
                isEquipped = isDad ? string.IsNullOrEmpty(m_EquippedDadSkinId) : string.IsNullOrEmpty(m_EquippedChildSkinId);
            }
            else
            {
                isEquipped = isDad ? (m_EquippedDadSkinId == skin.skinId) : (m_EquippedChildSkinId == skin.skinId);
            }

            bool isSelected = (m_SelectedSkinId == skin.skinId);
            Color cardBg = isSelected ? CardBgSelected : CardBgColor;
            Color cardBorder = isSelected ? BorderWhite : BorderSubtle;

            var card = CreateBorderedPanel(m_SkinCardsContainer, "card-" + skin.skinId, cardBg, cardBorder, 1.5f);
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(0, 88);

            var btnTrigger = card.AddComponent<Button>();
            btnTrigger.transition = Selectable.Transition.ColorTint;
            btnTrigger.onClick.AddListener(() => SelectSkin(skin.skinId));

            var inner = card.transform.Find("InnerBg");

            // Role Tag
            string roleLabel = isDad ? "[CHA]" : "[CON]";
            var tagText = CreateText(inner, "tag", roleLabel, 13, FontStyles.Bold, TextWhite);
            tagText.alignment = TextAlignmentOptions.Left;
            var tagRt = tagText.GetComponent<RectTransform>();
            tagRt.anchorMin = tagRt.anchorMax = new Vector2(0f, 0.5f);
            tagRt.pivot = new Vector2(0f, 0.5f);
            tagRt.anchoredPosition = new Vector2(18, 18);
            tagRt.sizeDelta = new Vector2(70, 24);

            // Skin Name
            var nameText = CreateText(inner, "name", skin.name, 17, FontStyles.Bold, TextWhite);
            nameText.alignment = TextAlignmentOptions.Left;
            var nameRt = nameText.GetComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0f, 0.5f);
            nameRt.pivot = new Vector2(0f, 0.5f);
            nameRt.anchoredPosition = new Vector2(90, 18);
            nameRt.sizeDelta = new Vector2(280, 26);

            // Price / Status Label
            string priceStr;
            if (isDefault)
                priceStr = "Trang phục gốc";
            else if (isOwned)
                priceStr = "Đã sở hữu";
            else
                priceStr = $"{skin.price:N0} Coins";

            var priceText = CreateText(inner, "price", priceStr, 14, FontStyles.Normal, TextMuted);
            priceText.alignment = TextAlignmentOptions.Left;
            var priceRt = priceText.GetComponent<RectTransform>();
            priceRt.anchorMin = priceRt.anchorMax = new Vector2(0f, 0.5f);
            priceRt.pivot = new Vector2(0f, 0.5f);
            priceRt.anchoredPosition = new Vector2(18, -18);
            priceRt.sizeDelta = new Vector2(300, 24);

            // Action Button
            string actionLabel = isEquipped ? "ĐANG DÙNG" : (isOwned ? "TRANG BỊ" : "MUA");
            Color btnBg = isEquipped ? ButtonDarkBg : (isOwned ? Color.white : Color.white);
            Color btnBorder = isEquipped ? BorderSubtle : BorderWhite;
            Color btnText = isEquipped ? TextMuted : Color.black;

            var actionBtnGo = CreateBorderedButton(inner, "btn", actionLabel, new Vector2(130, 42), btnBg, btnBorder, btnText, 1.5f);
            var actionRt = actionBtnGo.GetComponent<RectTransform>();
            actionRt.anchorMin = actionRt.anchorMax = new Vector2(1f, 0.5f);
            actionRt.pivot = new Vector2(1f, 0.5f);
            actionRt.anchoredPosition = new Vector2(-16, 0);

            var btn = actionBtnGo.GetComponent<Button>();
            if (isEquipped)
            {
                btn.interactable = false;
            }
            else if (isOwned)
            {
                btn.onClick.AddListener(() => _ = ExecuteEquipAsync(skin.skinId, skin.role));
            }
            else
            {
                btn.onClick.AddListener(() => _ = ExecuteBuyAsync(skin));
            }
        }

        public void SelectSkin(string skinId)
        {
            m_SelectedSkinId = skinId;
            bool isDefault = (skinId == "default_dad" || skinId == "default_child");

            SkinDto skin = null;
            if (isDefault)
            {
                string r = skinId == "default_dad" ? "Dad" : "Child";
                skin = new SkinDto { skinId = skinId, name = "Trang phục Mặc Định", role = r, price = 0 };
            }
            else
            {
                skin = m_Catalog.Find(s => s.skinId == skinId);
            }

            if (skin == null) return;

            // 1. Cập nhật Preview Stage
            if (previewStage != null)
            {
                previewStage.ShowSkin(skinId);
            }

            // 2. CẬP NHẬT TRỰC TIẾP MODEL NHÂN VẬT TRONG LOBBY 3D
            if (Lobby3DManager.Instance != null)
            {
                Lobby3DManager.Instance.UpdatePreviewSkin(skin.role, isDefault ? string.Empty : skin.skinId);
            }

            if (m_PreviewSkinNameText != null)
            {
                string roleTag = string.Equals(skin.role, "Dad", StringComparison.OrdinalIgnoreCase) ? "[Cha]" : "[Con]";
                m_PreviewSkinNameText.text = $"{skin.name} {roleTag}";
            }

            bool isDad = string.Equals(skin.role, "Dad", StringComparison.OrdinalIgnoreCase);
            bool isOwned = isDefault || m_OwnedSkinIds.Contains(skin.skinId);
            bool isEquipped = false;

            if (isDefault)
            {
                isEquipped = isDad ? string.IsNullOrEmpty(m_EquippedDadSkinId) : string.IsNullOrEmpty(m_EquippedChildSkinId);
            }
            else
            {
                isEquipped = isDad ? (m_EquippedDadSkinId == skin.skinId) : (m_EquippedChildSkinId == skin.skinId);
            }

            if (m_PreviewSkinPriceText != null)
            {
                if (isDefault)
                    m_PreviewSkinPriceText.text = "Trang phục nguyên bản của nhân vật";
                else if (isOwned)
                    m_PreviewSkinPriceText.text = "✓ Đã trong bộ sưu tập";
                else
                    m_PreviewSkinPriceText.text = $"Giá bán: {skin.price:N0} Coins";
            }

            if (m_PreviewActionButton != null && m_PreviewActionText != null)
            {
                if (isEquipped)
                {
                    m_PreviewActionText.text = "ĐANG DÙNG";
                    m_PreviewActionButton.interactable = false;
                    SetButtonMonochromeStyle(m_PreviewActionButton, m_PreviewActionText, false);
                }
                else if (isOwned)
                {
                    m_PreviewActionText.text = "TRANG BỊ";
                    m_PreviewActionButton.interactable = true;
                    SetButtonMonochromeStyle(m_PreviewActionButton, m_PreviewActionText, true);
                }
                else
                {
                    m_PreviewActionText.text = $"MUA ({skin.price:N0} Coins)";
                    m_PreviewActionButton.interactable = true;
                    SetButtonMonochromeStyle(m_PreviewActionButton, m_PreviewActionText, true);
                }
            }
        }

        private void OnPreviewActionClicked()
        {
            if (string.IsNullOrEmpty(m_SelectedSkinId)) return;

            bool isDefault = (m_SelectedSkinId == "default_dad" || m_SelectedSkinId == "default_child");
            if (isDefault)
            {
                string r = m_SelectedSkinId == "default_dad" ? "Dad" : "Child";
                _ = ExecuteEquipAsync(m_SelectedSkinId, r);
                return;
            }

            var skin = m_Catalog.Find(s => s.skinId == m_SelectedSkinId);
            if (skin == null) return;

            bool isOwned = m_OwnedSkinIds.Contains(skin.skinId);
            if (isOwned)
            {
                _ = ExecuteEquipAsync(skin.skinId, skin.role);
            }
            else
            {
                _ = ExecuteBuyAsync(skin);
            }
        }

        private async Task ExecuteBuyAsync(SkinDto skin)
        {
            if (m_IsBusy) return;

            if (m_CurrentCoins < skin.price)
            {
                UISoundManager.PlayError();
                ShowToast("Không đủ Coin! Vui lòng nạp thêm Coin để mua trang phục.");
                OpenTopup();
                return;
            }

            m_IsBusy = true;
            try
            {
                if (ApiClient.IsLoggedIn)
                {
                    var body = JsonUtility.ToJson(new BuyRequest { skinId = skin.skinId });
                    var resJson = await ApiClient.PostAsync("/store/buy", body);
                    var res = JsonUtility.FromJson<ActionResult>(resJson);

                    m_CurrentCoins = res.coins;
                    m_OwnedSkinIds.Add(skin.skinId);
                }
                else
                {
                    // Chế độ Offline Sandbox Play Mode
                    m_CurrentCoins -= skin.price;
                    m_OwnedSkinIds.Add(skin.skinId);
                    PlayerPrefs.SetInt("HMH.Store.LocalCoins", m_CurrentCoins);
                    PlayerPrefs.SetString("HMH.Store.LocalOwned", string.Join(",", m_OwnedSkinIds));
                }

                UpdateCoinsDisplay();
                UISoundManager.PlaySuccess();
                ShowToast($"Mua thành công '{skin.name}'!");

                await ExecuteEquipAsync(skin.skinId, skin.role);
            }
            catch (Exception ex)
            {
                UISoundManager.PlayError();
                ShowToast($"Mua thất bại: {ex.Message}");
            }
            finally
            {
                m_IsBusy = false;
                RebuildSkinCards();
                SelectSkin(skin.skinId);
            }
        }

        private async Task ExecuteEquipAsync(string skinId, string role)
        {
            m_IsBusy = true;
            try
            {
                bool isDefault = (skinId == "default_dad" || skinId == "default_child");
                string apiSkinId = isDefault ? string.Empty : skinId;

                if (ApiClient.IsLoggedIn)
                {
                    var body = JsonUtility.ToJson(new EquipRequest { skinId = apiSkinId, role = role });
                    var resJson = await ApiClient.PostAsync("/store/equip", body);
                    var res = JsonUtility.FromJson<ActionResult>(resJson);

                    m_EquippedDadSkinId = res.equippedDadSkinId;
                    m_EquippedChildSkinId = res.equippedChildSkinId;

                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Dad", m_EquippedDadSkinId ?? string.Empty);
                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Child", m_EquippedChildSkinId ?? string.Empty);
                }
                else
                {
                    // Chế độ Offline Sandbox Play Mode
                    if (string.Equals(role, "Dad", StringComparison.OrdinalIgnoreCase))
                        m_EquippedDadSkinId = apiSkinId;
                    else
                        m_EquippedChildSkinId = apiSkinId;

                    PlayerPrefs.SetString("HMH.Store.LocalDad", m_EquippedDadSkinId ?? string.Empty);
                    PlayerPrefs.SetString("HMH.Store.LocalChild", m_EquippedChildSkinId ?? string.Empty);
                }

                Lobby3DManager.Instance?.RefreshEquippedSkinsInLobby();

                UISoundManager.PlayConfirm();
                ShowToast(isDefault ? "Đã chuyển về trang phục mặc định!" : "Đã trang bị skin thành công!");
            }
            catch (Exception ex)
            {
                UISoundManager.PlayError();
                ShowToast($"Trang bị thất bại: {ex.Message}");
            }
            finally
            {
                m_IsBusy = false;
                RebuildSkinCards();
                SelectSkin(skinId);
            }
        }

        private async Task ExecuteTopupAsync(string packageId)
        {
            if (m_IsBusy) return;
            m_IsBusy = true;

            try
            {
                int addedCoins = (packageId == "vnd_50000") ? 550 : 100;

                if (ApiClient.IsLoggedIn)
                {
                    var req = new TopupRequest
                    {
                        packageId = packageId,
                        requestId = Guid.NewGuid().ToString("N")
                    };

                    var resJson = await ApiClient.PostAsync("/wallet/topup", JsonUtility.ToJson(req));
                    var res = JsonUtility.FromJson<ActionResult>(resJson);
                    m_CurrentCoins = res.coins;
                }
                else
                {
                    // Chế độ Offline Sandbox Play Mode
                    m_CurrentCoins += addedCoins;
                    PlayerPrefs.SetInt("HMH.Store.LocalCoins", m_CurrentCoins);
                }

                UpdateCoinsDisplay();
                UISoundManager.PlaySuccess();
                ShowToast($"Nạp thành công {addedCoins} Coins!");
                CloseTopup();
            }
            catch (Exception ex)
            {
                UISoundManager.PlayError();
                ShowToast($"Nạp Coin thất bại: {ex.Message}");
            }
            finally
            {
                m_IsBusy = false;
            }
        }

        private void UpdateCoinsDisplay()
        {
            string coinStr = $"Coins: {m_CurrentCoins:N0}";
            if (m_CoinBadgeText != null) m_CoinBadgeText.text = coinStr;
            if (m_StoreCoinText != null) m_StoreCoinText.text = coinStr;
        }

        private void ShowToast(string message)
        {
            if (m_StatusToastText != null)
            {
                m_StatusToastText.text = message;
                CancelInvoke(nameof(ClearToast));
                Invoke(nameof(ClearToast), 4.5f);
            }
        }

        private void ClearToast()
        {
            if (m_StatusToastText != null) m_StatusToastText.text = string.Empty;
        }

        // ─── Ready Guard ───────────────────────────────────────────
        public void SetStoreInteractable(bool interactable)
        {
            if (m_TopStoreButton != null) m_TopStoreButton.interactable = interactable;
            if (m_CoinBadgeButton != null) m_CoinBadgeButton.interactable = interactable;
            if (!interactable && m_StoreModal != null && m_StoreModal.activeSelf)
            {
                CloseStore();
            }
        }

        // ─── Helpers: Academic Monochrome UI Construction ────────────

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private GameObject CreateBorderedPanel(Transform parent, string name, Color bgColor, Color borderColor, float borderWidth = 1.5f)
        {
            // Outer Box (Border)
            var borderGo = new GameObject(name, typeof(RectTransform), typeof(Image));
            borderGo.transform.SetParent(parent, false);
            borderGo.GetComponent<Image>().color = borderColor;

            // Inner Box (Background)
            var innerGo = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
            innerGo.transform.SetParent(borderGo.transform, false);
            var innerRt = innerGo.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(borderWidth, borderWidth);
            innerRt.offsetMax = new Vector2(-borderWidth, -borderWidth);
            innerGo.GetComponent<Image>().color = bgColor;

            return borderGo;
        }

        private GameObject CreateBorderedButton(Transform parent, string name, string label, Vector2 size, Color bgColor, Color borderColor, Color textColor, float borderWidth = 1.5f)
        {
            var borderGo = CreateBorderedPanel(parent, name, bgColor, borderColor, borderWidth);
            var rt = borderGo.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var btn = borderGo.AddComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;

            var innerBg = borderGo.transform.Find("InnerBg");
            var targetParent = (innerBg != null) ? innerBg : borderGo.transform;

            var text = CreateText(targetParent, "label", label, 14, FontStyles.Bold, textColor);
            var textRt = text.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;

            return borderGo;
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (m_FontAsset != null) tmp.font = m_FontAsset;
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return tmp;
        }

        private T FindInCanvas<T>(string name) where T : Component
        {
            if (lobbyCanvas == null) return null;
            foreach (var item in lobbyCanvas.GetComponentsInChildren<T>(true))
            {
                if (item.gameObject.name == name) return item;
            }
            return null;
        }
    }
}
