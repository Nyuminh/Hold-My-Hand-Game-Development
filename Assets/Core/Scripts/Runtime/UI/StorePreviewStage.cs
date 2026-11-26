using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Sân khấu 3D độc lập (Preview Stage) hiển thị model trang phục xoay 360 độ qua RenderTexture.
    /// Hoạt động độc lập ở tọa độ cách ly để không ảnh hưởng đến scene Lobby chính.
    /// </summary>
    public class StorePreviewStage : MonoBehaviour, IDragHandler
    {
        [Header("Render Texture")]
        [SerializeField] private int textureWidth = 512;
        [SerializeField] private int textureHeight = 512;
        [SerializeField] private float rotationSpeed = 1.2f;

        private static readonly Vector3 StageOrigin = new Vector3(800f, 800f, 800f);

        private RenderTexture m_RenderTexture;
        private Camera m_PreviewCamera;
        private Light m_StageLight;
        private Transform m_ModelRotator;
        private GameObject m_CurrentModel;
        private string m_CurrentSkinId;

        public RenderTexture TargetTexture => m_RenderTexture;

        public void Initialize()
        {
            if (m_RenderTexture != null) return;

            // 1. Tạo RenderTexture
            m_RenderTexture = new RenderTexture(textureWidth, textureHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = "Store_Preview_RT",
                antiAliasing = 2,
                filterMode = FilterMode.Bilinear
            };
            m_RenderTexture.Create();

            // 2. Tạo Root GameObject cho Stage ở tọa độ cách ly
            var stageRoot = new GameObject("[Store_Preview_Stage]");
            stageRoot.transform.position = StageOrigin;
            stageRoot.transform.rotation = Quaternion.identity;
            DontDestroyOnLoad(stageRoot);

            // 3. Tạo Camera riêng biệt
            var camGo = new GameObject("Preview_Camera");
            camGo.transform.SetParent(stageRoot.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.0f, -2.5f);
            camGo.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

            m_PreviewCamera = camGo.AddComponent<Camera>();
            m_PreviewCamera.clearFlags = CameraClearFlags.SolidColor;
            m_PreviewCamera.backgroundColor = new Color(0.08f, 0.11f, 0.17f, 1f); // Dark slate (#141C2B)
            m_PreviewCamera.fieldOfView = 36f;
            m_PreviewCamera.nearClipPlane = 0.1f;
            m_PreviewCamera.farClipPlane = 20f;
            m_PreviewCamera.targetTexture = m_RenderTexture;
            m_PreviewCamera.enabled = true;

            // 4. Tạo Ánh sáng 3 điểm (Key Light + Fill Light)
            var lightGo = new GameObject("Preview_KeyLight");
            lightGo.transform.SetParent(stageRoot.transform, false);
            lightGo.transform.localPosition = new Vector3(1.5f, 2.5f, -1.5f);
            lightGo.transform.localRotation = Quaternion.Euler(45f, -30f, 0f);
            m_StageLight = lightGo.AddComponent<Light>();
            m_StageLight.type = LightType.Directional;
            m_StageLight.color = new Color(1f, 0.98f, 0.94f);
            m_StageLight.intensity = 1.3f;

            var fillLightGo = new GameObject("Preview_FillLight");
            fillLightGo.transform.SetParent(stageRoot.transform, false);
            fillLightGo.transform.localPosition = new Vector3(-1.5f, 1.5f, -1f);
            fillLightGo.transform.localRotation = Quaternion.Euler(30f, 60f, 0f);
            var fillLight = fillLightGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.color = new Color(0.7f, 0.8f, 0.95f);
            fillLight.intensity = 0.7f;

            // 5. Tạo Transform xoay model
            var rotatorGo = new GameObject("Model_Rotator");
            rotatorGo.transform.SetParent(stageRoot.transform, false);
            rotatorGo.transform.localPosition = new Vector3(0f, 0f, 0f);
            m_ModelRotator = rotatorGo.transform;
        }

        public void ShowSkin(string skinId)
        {
            if (m_ModelRotator == null) Initialize();
            if (m_CurrentSkinId == skinId && m_CurrentModel != null) return;

            m_CurrentSkinId = skinId;

            // Xóa model cũ nếu có
            if (m_CurrentModel != null)
            {
                Destroy(m_CurrentModel);
                m_CurrentModel = null;
            }

            GameObject prefabToSpawn = null;
            bool isChild = false;

            if (skinId == "default_dad")
            {
                if (Lobby3DManager.Instance != null && Lobby3DManager.Instance.Player1Preview != null)
                {
                    prefabToSpawn = Lobby3DManager.Instance.Player1Preview;
                }
                isChild = false;
            }
            else if (skinId == "default_child")
            {
                if (Lobby3DManager.Instance != null && Lobby3DManager.Instance.Player2Preview != null)
                {
                    prefabToSpawn = Lobby3DManager.Instance.Player2Preview;
                }
                isChild = true;
            }
            else
            {
                var db = SkinDatabase.Load();
                if (db != null && db.TryGet(skinId, out var entry) && entry.Model != null)
                {
                    prefabToSpawn = entry.Model;
                    isChild = string.Equals(entry.Role, "Child", StringComparison.OrdinalIgnoreCase);
                }
            }

            if (prefabToSpawn == null)
            {
                Debug.LogWarning($"[StorePreviewStage] Không tìm thấy model cho skin: {skinId}");
                return;
            }

            m_CurrentModel = Instantiate(prefabToSpawn, m_ModelRotator);
            m_CurrentModel.transform.localPosition = Vector3.zero;
            m_CurrentModel.transform.localRotation = Quaternion.identity;

            // Dọn dẹp các component gameplay/network nếu clone từ scene
            foreach (var comp in m_CurrentModel.GetComponentsInChildren<MonoBehaviour>())
            {
                if (!(comp is Animator)) comp.enabled = false;
            }
            foreach (var col in m_CurrentModel.GetComponentsInChildren<Collider>())
            {
                col.enabled = false;
            }

            // Tự động căn giữa và tỉ lệ phù hợp cho khung hình
            if (isChild)
            {
                m_CurrentModel.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                m_CurrentModel.transform.localScale = Vector3.one * 1.35f;
            }
            else
            {
                m_CurrentModel.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                m_CurrentModel.transform.localScale = Vector3.one;
            }

            // Tắt animator không cần thiết trong preview để tránh xung đột
            var anim = m_CurrentModel.GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
            }
        }

        public void RotateModel(float deltaX)
        {
            if (m_ModelRotator != null)
            {
                m_ModelRotator.Rotate(0f, -deltaX * rotationSpeed, 0f, Space.World);
            }
        }

        public void ResetRotation()
        {
            if (m_ModelRotator != null)
            {
                m_ModelRotator.localRotation = Quaternion.identity;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            RotateModel(eventData.delta.x);
        }

        private void OnDestroy()
        {
            if (m_CurrentModel != null) Destroy(m_CurrentModel);
            if (m_RenderTexture != null)
            {
                m_RenderTexture.Release();
                Destroy(m_RenderTexture);
                m_RenderTexture = null;
            }
            if (m_PreviewCamera != null && m_PreviewCamera.transform.parent != null)
            {
                Destroy(m_PreviewCamera.transform.parent.gameObject);
            }
        }
    }
}
