using System;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Cho phép gắn và hoán đổi visual skin trên nhân vật xem trước (player1Preview / player2Preview)
    /// ngay trong scene Lobby 3D theo thời gian thực mà không làm gián đoạn Animation idle.
    /// </summary>
    public class LobbyPreviewSkin : MonoBehaviour
    {
        private bool m_IsDad;
        private Animator m_GameplayAnimator;
        private SkinnedMeshRenderer m_OriginalRenderer;
        private GameObject m_SpawnedSkin;
        private SkinnedMeshRenderer m_SkinRenderer;
        private HumanPoseHandler m_SourcePose;
        private HumanPoseHandler m_SkinPose;
        private HumanPose m_Pose;
        private Transform m_SourceHips;
        private Transform m_SkinHips;
        private string m_CurrentSkinId;

        public void Initialize(bool isDad)
        {
            m_IsDad = isDad;
            m_GameplayAnimator = GetComponentInChildren<Animator>(true);
            m_OriginalRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
        }

        public void ApplySkin(string skinId)
        {
            if (m_CurrentSkinId == skinId) return;
            m_CurrentSkinId = skinId;

            Restore();

            // Nếu là trang phục gốc/mặc định thì giữ nguyên renderer ban đầu
            if (string.IsNullOrEmpty(skinId) || skinId == "default" || skinId == "default_dad" || skinId == "default_child")
            {
                return;
            }

            var db = SkinDatabase.Load();
            if (db == null || !db.TryGet(skinId, out var entry) || entry.Model == null)
            {
                return;
            }

            if (m_GameplayAnimator == null)
            {
                m_GameplayAnimator = GetComponentInChildren<Animator>(true);
            }
            if (m_OriginalRenderer == null)
            {
                m_OriginalRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);
            }
            if (m_GameplayAnimator == null || m_OriginalRenderer == null) return;

            m_SpawnedSkin = Instantiate(entry.Model, m_GameplayAnimator.transform, false);
            m_SpawnedSkin.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

            var skinAnimator = m_SpawnedSkin.GetComponent<Animator>();
            if (skinAnimator != null)
            {
                m_SpawnedSkin.transform.localScale = Vector3.one * (m_GameplayAnimator.humanScale / skinAnimator.humanScale);
                skinAnimator.fireEvents = false;
                skinAnimator.enabled = false;
                m_SourcePose = new HumanPoseHandler(m_GameplayAnimator.avatar, m_GameplayAnimator.transform);
                m_SkinPose = new HumanPoseHandler(skinAnimator.avatar, skinAnimator.transform);
                m_SourceHips = m_GameplayAnimator.GetBoneTransform(HumanBodyBones.Hips);
                m_SkinHips = skinAnimator.GetBoneTransform(HumanBodyBones.Hips);
            }

            m_SkinRenderer = m_SpawnedSkin.GetComponentInChildren<SkinnedMeshRenderer>();
            m_OriginalRenderer.forceRenderingOff = true;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (m_SkinPose == null || m_GameplayAnimator == null) return;

            m_SourcePose.GetHumanPose(ref m_Pose);
            m_SkinPose.SetHumanPose(ref m_Pose);
            if (m_SkinHips != null && m_SourceHips != null)
            {
                m_SkinHips.position = m_SourceHips.position;
            }
            if (m_SkinRenderer != null && m_OriginalRenderer != null)
            {
                m_SkinRenderer.enabled = m_OriginalRenderer.enabled;
            }
        }

        public void Restore()
        {
            m_SourcePose?.Dispose();
            m_SkinPose?.Dispose();
            m_SourcePose = null;
            m_SkinPose = null;

            if (m_OriginalRenderer != null)
            {
                m_OriginalRenderer.forceRenderingOff = false;
            }

            if (m_SpawnedSkin != null)
            {
                Destroy(m_SpawnedSkin);
                m_SpawnedSkin = null;
            }
        }

        private void OnDestroy()
        {
            Restore();
        }
    }
}
