using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Điều khiển toàn bộ Giao diện Cửa hàng (Store) và Nạp Coin trên Lobby Canvas.
    /// Cho phép xem trước skin 3D 360 độ, mua bằng Coin, trang bị skin cho Ba/Con trước khi Ready,
    /// và nạp Coin tức thì qua Sandbox Mock Payment.
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
        private int m_CurrentCoins;
        private List<SkinDto> m_Catalog = new List<SkinDto>();
        private HashSet<string> m_OwnedSkinIds = new HashSet<string>();
        private string m_EquippedDadSkinId;
        private string m_EquippedChildSkinId;
        private string m_SelectedSkinId;
        private string m_RoleFilter = "ALL"; // ALL, Dad, Child
        private bool m_IsBusy;

        // ─── UI References ──────────────────────────────────────────
        private Button m_LobbyStoreButton;
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

        private TMP_FontAsset m_FontAsset;

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

            // 1. Nút Cửa Hàng và Huy hiệu Coin trên Canvas
            BindOrBuildLobbyTriggers();

            // 2. Cửa sổ Store Modal
            BuildStoreModal();

            // 3. Cửa sổ Topup Modal
            BuildTopupModal();
        }

        private void BindOrBuildLobbyTriggers()
        {
            // Tìm hoặc tạo nút [🛒 Cửa Hàng]
            var existingStoreBtn = FindInCanvas<Button>("store-button");
            if (existingStoreBtn != null)
            {
                m_LobbyStoreButton = existingStoreBtn;
                m_LobbyStoreButton.onClick.RemoveAllListeners();
                m_LobbyStoreButton.onClick.AddListener(OpenStore);
            }
            else
            {
                // Thêm vào main-buttons-panel và lobby-buttons-panel nếu có
                var mainPanel = FindInCanvas<Transform>("main-buttons-panel");
                var lobbyPanel = FindInCanvas<Transform>("lobby-buttons-panel");

                var parent = lobbyPanel != null ? lobbyPanel : (mainPanel != null ? mainPanel : lobbyCanvas.transform);
                var btnGo = CreateButton(parent, "store-button", "🛒 CỬA HÀNG", new Vector2(160, 42), new Color(0.12f, 0.44f, 0.88f));
                var rt = btnGo.GetComponent<RectTransform>();
                if (parent == lobbyCanvas.transform)
                {
                    rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(1f, 1f);
                    rt.anchoredPosition = new Vector2(-220f, -20f);
                }
                m_LobbyStoreButton = btnGo.GetComponent<Button>();
                m_LobbyStoreButton.onClick.AddListener(OpenStore);
            }

            // Tìm hoặc tạo huy hiệu [🪙 0 Coins]
            var existingCoinBadge = FindInCanvas<Button>("coin-badge-button");
            if (existingCoinBadge != null)
            {
                m_CoinBadgeButton = existingCoinBadge;
                m_CoinBadgeText = existingCoinBadge.GetComponentInChildren<TextMeshProUGUI>();
            }
            else
            {
                var badgeGo = CreateButton(lobbyCanvas.transform, "coin-badge-button", "🪙 0 Coins", new Vector2(140, 36), new Color(0.18f, 0.22f, 0.28f));
                var rt = badgeGo.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-400f, -22f);
                m_CoinBadgeButton = badgeGo.GetComponent<Button>();
                m_CoinBadgeText = badgeGo.GetComponentInChildren<TextMeshProUGUI>();
                m_CoinBadgeText.color = new Color(0.98f, 0.75f, 0.18f); // Gold
            }

            m_CoinBadgeButton.onClick.RemoveAllListeners();
            m_CoinBadgeButton.onClick.AddListener(OpenTopup);
        }

        private void BuildStoreModal()
        {
            if (m_StoreModal != null) return;

            // Root Backdrop
            m_StoreModal = CreatePanel(lobbyCanvas.transform, "store-modal-panel", new Color(0f, 0f, 0f, 0.82f));
            var rootRt = m_StoreModal.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            m_StoreModal.SetActive(false);

            // Dialog Window
            var dialog = CreatePanel(m_StoreModal.transform, "store-dialog", new Color(0.09f, 0.12f, 0.18f, 0.98f));
            var dialogRt = dialog.GetComponent<RectTransform>();
            dialogRt.anchorMin = dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(880, 560);

            // Header Bar
            var header = CreatePanel(dialog.transform, "header-bar", new Color(0.06f, 0.08f, 0.13f, 1f));
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 56);

            var titleText = CreateText(header.transform, "title", "CỬA HÀNG TRANG PHỤC", 20, FontStyles.Bold, Color.white);
            var titleRt = titleText.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(24, 0);

            m_StoreCoinText = CreateText(header.transform, "coin-display", "🪙 0 Coins", 16, FontStyles.Bold, new Color(0.98f, 0.75f, 0.18f));
            var coinRt = m_StoreCoinText.GetComponent<RectTransform>();
            coinRt.anchorMin = coinRt.anchorMax = new Vector2(1f, 0.5f);
            coinRt.pivot = new Vector2(1f, 0.5f);
            coinRt.anchoredPosition = new Vector2(-160, 0);

            var quickTopupBtn = CreateButton(header.transform, "quick-topup", "+ NẠP COIN", new Vector2(100, 32), new Color(0.13f, 0.65f, 0.35f));
            var quickRt = quickTopupBtn.GetComponent<RectTransform>();
            quickRt.anchorMin = quickRt.anchorMax = new Vector2(1f, 0.5f);
            quickRt.pivot = new Vector2(1f, 0.5f);
            quickRt.anchoredPosition = new Vector2(-54, 0);
            quickTopupBtn.GetComponent<Button>().onClick.AddListener(OpenTopup);

            var closeBtn = CreateButton(header.transform, "close-btn", "✕", new Vector2(34, 32), new Color(0.6f, 0.15f, 0.15f));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-12, 0);
            closeBtn.GetComponent<Button>().onClick.AddListener(CloseStore);

            // Filter Tabs Bar
            var tabsBar = CreatePanel(dialog.transform, "tabs-bar", new Color(0.08f, 0.10f, 0.15f, 1f));
            var tabsRt = tabsBar.GetComponent<RectTransform>();
            tabsRt.anchorMin = new Vector2(0f, 1f);
            tabsRt.anchorMax = new Vector2(1f, 1f);
            tabsRt.pivot = new Vector2(0.5f, 1f);
            tabsRt.anchoredPosition = new Vector2(0, -58);
            tabsRt.sizeDelta = new Vector2(0, 42);

            CreateTabButton(tabsBar.transform, "tab-all", "TẤT CẢ", new Vector2(30, 0), () => SetFilter("ALL"));
            CreateTabButton(tabsBar.transform, "tab-dad", "CHA (DAD)", new Vector2(140, 0), () => SetFilter("Dad"));
            CreateTabButton(tabsBar.transform, "tab-child", "CON (CHILD)", new Vector2(250, 0), () => SetFilter("Child"));

            // Body Area (Split Left: List, Right: 3D Preview)
            var body = CreatePanel(dialog.transform, "body-area", Color.clear);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(16, 40);
            bodyRt.offsetMax = new Vector2(-16, -106);

            // Left: Scroll List
            var leftPanel = CreatePanel(body.transform, "catalog-scroll-panel", new Color(0.07f, 0.09f, 0.14f, 0.8f));
            var leftRt = leftPanel.GetComponent<RectTransform>();
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0.54f, 1f);
            leftRt.offsetMin = leftRt.offsetMax = Vector2.zero;

            var scrollGo = new GameObject("ScrollRect", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
            scrollGo.transform.SetParent(leftPanel.transform, false);
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
            vlg.spacing = 8;
            vlg.padding = new RectOffset(4, 4, 4, 4);
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

            // Right: 3D Preview Stage
            var rightPanel = CreatePanel(body.transform, "preview-panel", new Color(0.06f, 0.08f, 0.12f, 1f));
            var rightRt = rightPanel.GetComponent<RectTransform>();
            rightRt.anchorMin = new Vector2(0.56f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.offsetMin = rightRt.offsetMax = Vector2.zero;

            // Preview RawImage
            var rawImageGo = new GameObject("PreviewRawImage", typeof(RectTransform), typeof(RawImage));
            rawImageGo.transform.SetParent(rightPanel.transform, false);
            var rawRt = rawImageGo.GetComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.05f, 0.28f);
            rawRt.anchorMax = new Vector2(0.95f, 0.95f);
            rawRt.offsetMin = rawRt.offsetMax = Vector2.zero;

            m_PreviewRawImage = rawImageGo.GetComponent<RawImage>();
            if (previewStage != null && previewStage.TargetTexture != null)
            {
                m_PreviewRawImage.texture = previewStage.TargetTexture;
            }

            // Drag handler on the preview stage
            var dragTrigger = rawImageGo.AddComponent<Button>();
            dragTrigger.transition = Selectable.Transition.None;

            var hintText = CreateText(rightPanel.transform, "hint", "Kéo chuột để xoay 360°", 12, FontStyles.Italic, new Color(0.7f, 0.7f, 0.7f));
            var hintRt = hintText.GetComponent<RectTransform>();
            hintRt.anchorMin = hintRt.anchorMax = new Vector2(0.5f, 0.25f);
            hintRt.anchoredPosition = Vector2.zero;

            m_PreviewSkinNameText = CreateText(rightPanel.transform, "skin-name", "Chọn trang phục", 16, FontStyles.Bold, Color.white);
            var nameRt = m_PreviewSkinNameText.GetComponent<RectTransform>();
            nameRt.anchorMin = nameRt.anchorMax = new Vector2(0.5f, 0.18f);
            nameRt.anchoredPosition = Vector2.zero;

            m_PreviewSkinPriceText = CreateText(rightPanel.transform, "skin-price", "", 14, FontStyles.Normal, new Color(0.98f, 0.75f, 0.18f));
            var priceRt = m_PreviewSkinPriceText.GetComponent<RectTransform>();
            priceRt.anchorMin = priceRt.anchorMax = new Vector2(0.5f, 0.12f);
            priceRt.anchoredPosition = Vector2.zero;

            var actionBtnGo = CreateButton(rightPanel.transform, "action-btn", "MUA", new Vector2(180, 36), new Color(0.12f, 0.44f, 0.88f));
            var actionRt = actionBtnGo.GetComponent<RectTransform>();
            actionRt.anchorMin = actionRt.anchorMax = new Vector2(0.5f, 0.05f);
            actionRt.anchoredPosition = Vector2.zero;
            m_PreviewActionButton = actionBtnGo.GetComponent<Button>();
            m_PreviewActionText = actionBtnGo.GetComponentInChildren<TextMeshProUGUI>();
            m_PreviewActionButton.onClick.AddListener(OnPreviewActionClicked);

            // Bottom Status / Toast
            m_StatusToastText = CreateText(dialog.transform, "toast", "", 13, FontStyles.Normal, new Color(0.3f, 0.85f, 0.5f));
            var toastRt = m_StatusToastText.GetComponent<RectTransform>();
            toastRt.anchorMin = new Vector2(0f, 0f);
            toastRt.anchorMax = new Vector2(1f, 0f);
            toastRt.pivot = new Vector2(0.5f, 0f);
            toastRt.anchoredPosition = new Vector2(0, 10);
            toastRt.sizeDelta = new Vector2(0, 24);
        }

        private void BuildTopupModal()
        {
            if (m_TopupModal != null) return;

            m_TopupModal = CreatePanel(lobbyCanvas.transform, "topup-modal-panel", new Color(0f, 0f, 0f, 0.85f));
            var rootRt = m_TopupModal.GetComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = rootRt.offsetMax = Vector2.zero;
            m_TopupModal.SetActive(false);

            var dialog = CreatePanel(m_TopupModal.transform, "topup-dialog", new Color(0.09f, 0.12f, 0.18f, 0.98f));
            var dialogRt = dialog.GetComponent<RectTransform>();
            dialogRt.anchorMin = dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
            dialogRt.pivot = new Vector2(0.5f, 0.5f);
            dialogRt.sizeDelta = new Vector2(520, 380);

            // Header
            var header = CreatePanel(dialog.transform, "header", new Color(0.06f, 0.08f, 0.13f, 1f));
            var headerRt = header.GetComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 1f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.pivot = new Vector2(0.5f, 1f);
            headerRt.sizeDelta = new Vector2(0, 48);

            var title = CreateText(header.transform, "title", "CỔNG NẠP COIN (SANDBOX)", 18, FontStyles.Bold, Color.white);
            var titleRt = title.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.5f);
            titleRt.anchorMax = new Vector2(0f, 0.5f);
            titleRt.pivot = new Vector2(0f, 0.5f);
            titleRt.anchoredPosition = new Vector2(20, 0);

            var closeBtn = CreateButton(header.transform, "close-btn", "✕", new Vector2(32, 30), new Color(0.6f, 0.15f, 0.15f));
            var closeRt = closeBtn.GetComponent<RectTransform>();
            closeRt.anchorMin = closeRt.anchorMax = new Vector2(1f, 0.5f);
            closeRt.pivot = new Vector2(1f, 0.5f);
            closeRt.anchoredPosition = new Vector2(-10, 0);
            closeBtn.GetComponent<Button>().onClick.AddListener(CloseTopup);

            // Package Cards
            var body = CreatePanel(dialog.transform, "body", Color.clear);
            var bodyRt = body.GetComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero;
            bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = new Vector2(24, 20);
            bodyRt.offsetMax = new Vector2(-24, -60);

            CreatePackageCard(body.transform, "vnd_10000", "GÓI CƠ BẢN", "10.000 VNĐ", "100 COINS", new Vector2(0, 110));
            CreatePackageCard(body.transform, "vnd_50000", "GÓI PHỔ BIẾN (+10% BONUS)", "50.000 VNĐ", "550 COINS", new Vector2(0, 10));

            var infoText = CreateText(dialog.transform, "info", "Môi trường Sandbox thử nghiệm — Nạp coin không phát sinh chi phí thật.", 11, FontStyles.Italic, new Color(0.6f, 0.7f, 0.8f));
            var infoRt = infoText.GetComponent<RectTransform>();
            infoRt.anchorMin = new Vector2(0.5f, 0f);
            infoRt.anchorMax = new Vector2(0.5f, 0f);
            infoRt.anchoredPosition = new Vector2(0, 20);
        }

        private void CreatePackageCard(Transform parent, string packageId, string title, string price, string coins, Vector2 pos)
        {
            var card = CreatePanel(parent, "pkg-" + packageId, new Color(0.12f, 0.16f, 0.24f, 1f));
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0f, 0.5f);
            cardRt.anchorMax = new Vector2(1f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = pos;
            cardRt.sizeDelta = new Vector2(0, 80);

            var t1 = CreateText(card.transform, "t1", title, 13, FontStyles.Bold, Color.white);
            t1.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, 20);

            var t2 = CreateText(card.transform, "t2", coins, 16, FontStyles.Bold, new Color(0.98f, 0.75f, 0.18f));
            t2.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, -10);

            var btn = CreateButton(card.transform, "buy-btn", price + " (Test Pay)", new Vector2(160, 36), new Color(0.13f, 0.65f, 0.35f));
            var btnRt = btn.GetComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(1f, 0.5f);
            btnRt.pivot = new Vector2(1f, 0.5f);
            btnRt.anchoredPosition = new Vector2(-16, 0);

            btn.GetComponent<Button>().onClick.AddListener(() => _ = ExecuteTopupAsync(packageId));
        }

        private void CreateTabButton(Transform parent, string name, string label, Vector2 pos, Action onClick)
        {
            var btn = CreateButton(parent, name, label, new Vector2(100, 30), new Color(0.15f, 0.20f, 0.30f));
            var rt = btn.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = pos;
            btn.GetComponent<Button>().onClick.AddListener(() => onClick());
        }

        // ─── Actions & Logic ────────────────────────────────────────

        public void OpenStore()
        {
            UISoundManager.PlayClick();

            if (m_StoreModal != null)
            {
                m_StoreModal.SetActive(true);
            }

            _ = RefreshWalletAndInventoryAsync();
        }

        public void CloseStore()
        {
            UISoundManager.PlayCancel();

            if (m_StoreModal != null)
            {
                m_StoreModal.SetActive(false);
            }
        }

        public void OpenTopup()
        {
            UISoundManager.PlayClick();

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
        }

        public void SetFilter(string filter)
        {
            m_RoleFilter = filter;
            RebuildSkinCards();
        }

        private async Task RefreshWalletAndInventoryAsync()
        {
            if (m_IsBusy) return;
            m_IsBusy = true;

            try
            {
                // 1. Tải Balance
                var balanceJson = await ApiClient.GetAsync("/wallet/balance");
                var balance = JsonUtility.FromJson<BalanceResponse>(balanceJson);
                m_CurrentCoins = balance.coins;
                UpdateCoinsDisplay();

                // 2. Tải Catalog
                var catalogJson = await ApiClient.GetAsync("/store/catalog");
                var catalogRes = JsonUtility.FromJson<CatalogResponse>(catalogJson);
                m_Catalog.Clear();
                if (catalogRes?.skins != null)
                {
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

                // Sync PlayerPrefs cho PlayerSkinSync
                if (ApiClient.IsLoggedIn)
                {
                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Dad", m_EquippedDadSkinId ?? string.Empty);
                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Child", m_EquippedChildSkinId ?? string.Empty);
                }

                RebuildSkinCards();

                // Mặc định chọn skin đầu tiên nếu chưa chọn
                if (string.IsNullOrEmpty(m_SelectedSkinId) && m_Catalog.Count > 0)
                {
                    SelectSkin(m_Catalog[0].skinId);
                }
                else if (!string.IsNullOrEmpty(m_SelectedSkinId))
                {
                    SelectSkin(m_SelectedSkinId);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StoreUIController] Lỗi đồng bộ store: {ex.Message}");
                ShowToast("Chưa thể cập nhật dữ liệu cửa hàng.");
            }
            finally
            {
                m_IsBusy = false;
            }
        }

        private void RebuildSkinCards()
        {
            if (m_SkinCardsContainer == null) return;

            // Xóa cards cũ
            for (int i = m_SkinCardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(m_SkinCardsContainer.GetChild(i).gameObject);
            }

            foreach (var skin in m_Catalog)
            {
                if (m_RoleFilter != "ALL" && !string.Equals(skin.role, m_RoleFilter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                CreateSkinCard(skin);
            }
        }

        private void CreateSkinCard(SkinDto skin)
        {
            bool isOwned = m_OwnedSkinIds.Contains(skin.skinId);
            bool isDad = string.Equals(skin.role, "Dad", StringComparison.OrdinalIgnoreCase);
            bool isEquipped = isDad ? (m_EquippedDadSkinId == skin.skinId) : (m_EquippedChildSkinId == skin.skinId);

            var card = CreatePanel(m_SkinCardsContainer, "card-" + skin.skinId, new Color(0.12f, 0.15f, 0.22f, 1f));
            var cardRt = card.GetComponent<RectTransform>();
            cardRt.sizeDelta = new Vector2(0, 72);

            var btnTrigger = card.AddComponent<Button>();
            btnTrigger.transition = Selectable.Transition.ColorTint;
            btnTrigger.onClick.AddListener(() => SelectSkin(skin.skinId));

            // Role Tag
            string roleLabel = isDad ? "[CHA]" : "[CON]";
            Color roleCol = isDad ? new Color(0.3f, 0.6f, 0.95f) : new Color(0.95f, 0.5f, 0.3f);
            var tagText = CreateText(card.transform, "tag", roleLabel, 12, FontStyles.Bold, roleCol);
            tagText.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, 16);

            // Skin Name
            var nameText = CreateText(card.transform, "name", skin.name, 15, FontStyles.Bold, Color.white);
            nameText.GetComponent<RectTransform>().anchoredPosition = new Vector2(65, 16);

            // Price / Owned Status
            string priceStr = isOwned ? "Đã sở hữu" : $"🪙 {skin.price} Coins";
            Color priceCol = isOwned ? new Color(0.5f, 0.8f, 0.5f) : new Color(0.98f, 0.75f, 0.18f);
            var priceText = CreateText(card.transform, "price", priceStr, 13, FontStyles.Normal, priceCol);
            priceText.GetComponent<RectTransform>().anchoredPosition = new Vector2(16, -14);

            // Action Button
            string actionLabel = isEquipped ? "ĐANG DÙNG" : (isOwned ? "TRANG BỊ" : "MUA");
            Color actionCol = isEquipped ? new Color(0.15f, 0.5f, 0.25f) : (isOwned ? new Color(0.2f, 0.45f, 0.75f) : new Color(0.85f, 0.5f, 0.1f));

            var actionBtnGo = CreateButton(card.transform, "btn", actionLabel, new Vector2(100, 32), actionCol);
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
                btn.onClick.AddListener(() => _ = ExecuteEquipAsync(skin.skinId));
            }
            else
            {
                btn.onClick.AddListener(() => _ = ExecuteBuyAsync(skin));
            }
        }

        public void SelectSkin(string skinId)
        {
            m_SelectedSkinId = skinId;
            var skin = m_Catalog.Find(s => s.skinId == skinId);
            if (skin == null) return;

            if (previewStage != null)
            {
                previewStage.ShowSkin(skinId);
            }

            if (m_PreviewSkinNameText != null)
            {
                string roleTag = string.Equals(skin.role, "Dad", StringComparison.OrdinalIgnoreCase) ? "[Cha]" : "[Con]";
                m_PreviewSkinNameText.text = $"{skin.name} {roleTag}";
            }

            bool isOwned = m_OwnedSkinIds.Contains(skin.skinId);
            bool isDad = string.Equals(skin.role, "Dad", StringComparison.OrdinalIgnoreCase);
            bool isEquipped = isDad ? (m_EquippedDadSkinId == skin.skinId) : (m_EquippedChildSkinId == skin.skinId);

            if (m_PreviewSkinPriceText != null)
            {
                m_PreviewSkinPriceText.text = isOwned ? "✓ Đã trong bộ sưu tập" : $"Giá bán: 🪙 {skin.price} Coins";
            }

            if (m_PreviewActionButton != null && m_PreviewActionText != null)
            {
                if (isEquipped)
                {
                    m_PreviewActionText.text = "ĐANG DÙNG";
                    m_PreviewActionButton.interactable = false;
                    m_PreviewActionButton.GetComponent<Image>().color = new Color(0.15f, 0.5f, 0.25f);
                }
                else if (isOwned)
                {
                    m_PreviewActionText.text = "TRANG BỊ";
                    m_PreviewActionButton.interactable = true;
                    m_PreviewActionButton.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.75f);
                }
                else
                {
                    m_PreviewActionText.text = $"MUA ({skin.price} 🪙)";
                    m_PreviewActionButton.interactable = true;
                    m_PreviewActionButton.GetComponent<Image>().color = new Color(0.85f, 0.5f, 0.1f);
                }
            }
        }

        private void OnPreviewActionClicked()
        {
            if (string.IsNullOrEmpty(m_SelectedSkinId)) return;
            var skin = m_Catalog.Find(s => s.skinId == m_SelectedSkinId);
            if (skin == null) return;

            bool isOwned = m_OwnedSkinIds.Contains(skin.skinId);
            if (isOwned)
            {
                _ = ExecuteEquipAsync(skin.skinId);
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
                var body = JsonUtility.ToJson(new BuyRequest { skinId = skin.skinId });
                var resJson = await ApiClient.PostAsync("/store/buy", body);
                var res = JsonUtility.FromJson<ActionResult>(resJson);

                m_CurrentCoins = res.coins;
                m_OwnedSkinIds.Add(skin.skinId);
                UpdateCoinsDisplay();

                UISoundManager.PlaySuccess();
                ShowToast($"Mua thành công '{skin.name}'!");

                // Tự động trang bị sau khi mua
                await ExecuteEquipAsync(skin.skinId);
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

        private async Task ExecuteEquipAsync(string skinId)
        {
            m_IsBusy = true;
            try
            {
                var body = JsonUtility.ToJson(new EquipRequest { skinId = skinId });
                var resJson = await ApiClient.PostAsync("/store/equip", body);
                var res = JsonUtility.FromJson<ActionResult>(resJson);

                m_EquippedDadSkinId = res.equippedDadSkinId;
                m_EquippedChildSkinId = res.equippedChildSkinId;

                if (ApiClient.IsLoggedIn)
                {
                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Dad", m_EquippedDadSkinId ?? string.Empty);
                    PlayerPrefs.SetString($"HMH.Store.{ApiClient.UserId}.Child", m_EquippedChildSkinId ?? string.Empty);
                }

                UISoundManager.PlayConfirm();
                ShowToast("Đã trang bị skin thành công!");
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
                var req = new TopupRequest
                {
                    packageId = packageId,
                    requestId = Guid.NewGuid().ToString("N")
                };

                var resJson = await ApiClient.PostAsync("/wallet/topup", JsonUtility.ToJson(req));
                var res = JsonUtility.FromJson<ActionResult>(resJson);

                m_CurrentCoins = res.coins;
                UpdateCoinsDisplay();

                UISoundManager.PlaySuccess();
                ShowToast("Nạp Coin thành công!");
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
            string coinStr = $"🪙 {m_CurrentCoins:N0} Coins";
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
            if (m_LobbyStoreButton != null) m_LobbyStoreButton.interactable = interactable;
            if (m_CoinBadgeButton != null) m_CoinBadgeButton.interactable = interactable;
            if (!interactable && m_StoreModal != null && m_StoreModal.activeSelf)
            {
                CloseStore();
            }
        }

        // ─── Helpers: UI Construction ──────────────────────────────

        private GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private GameObject CreateButton(Transform parent, string name, string label, Vector2 size, Color bgColor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = bgColor;
            img.type = Image.Type.Sliced;

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = bgColor * 1.15f;
            colors.pressedColor = bgColor * 0.85f;
            colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            btn.colors = colors;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;

            var text = CreateText(go.transform, "label", label, 14, FontStyles.Bold, Color.white);
            var textRt = text.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;

            return go;
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
