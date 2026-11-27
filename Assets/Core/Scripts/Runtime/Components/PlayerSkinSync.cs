using System;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    [DisallowMultipleComponent]
    public sealed class PlayerSkinSync : NetworkBehaviour
    {
        [SerializeField] private bool isDad;
        [SerializeField] private Animator gameplayAnimator;
        [SerializeField] private SkinnedMeshRenderer originalRenderer;

        private readonly NetworkVariable<FixedString64Bytes> m_EquippedSkinId = new(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private GameObject skin;
        private SkinnedMeshRenderer skinRenderer;
        private HumanPoseHandler sourcePose;
        private HumanPoseHandler skinPose;
        private HumanPose pose;
        private Transform sourceHips;
        private Transform skinHips;
        private AnimatorCullingMode originalCulling;
        private bool originalForceOff;
        private int spawnVersion;

        [Serializable]
        private sealed class EquippedSkins
        {
            public string equippedDadSkinId;
            public string equippedChildSkinId;
        }

        public override void OnNetworkSpawn()
        {
            m_EquippedSkinId.OnValueChanged += OnSkinChanged;
            Apply(m_EquippedSkinId.Value.ToString());
            if (IsOwner && ApiClient.IsLoggedIn)
                _ = LoadEquippedAsync(++spawnVersion, ApiClient.UserId);
        }

        public override void OnNetworkDespawn()
        {
            spawnVersion++;
            m_EquippedSkinId.OnValueChanged -= OnSkinChanged;
            Restore();
        }

        private async Task LoadEquippedAsync(int version, string userId)
        {
            string key = $"HMH.Store.{userId}.{(isDad ? "Dad" : "Child")}";
            string id;
            try
            {
                var json = await ApiClient.GetAsync("/store/inventory");
                var inventory = JsonUtility.FromJson<EquippedSkins>(json);
                id = isDad ? inventory.equippedDadSkinId : inventory.equippedChildSkinId;
                PlayerPrefs.SetString(key, id ?? string.Empty);
            }
            catch (Exception ex)
            {
                if (this != null && version == spawnVersion)
                    Debug.LogWarning($"[PlayerSkinSync] Could not load equipped skin: {ex.Message}");
                id = PlayerPrefs.GetString(key);
            }

            if (this != null && version == spawnVersion && IsSpawned && IsOwner && ApiClient.UserId == userId)
                m_EquippedSkinId.Value = Resolve(id) != null ? new FixedString64Bytes(id) : default;
        }

        private SkinDatabase.Entry Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            var db = SkinDatabase.Load();
            return db != null && db.TryGet(id, out var entry) && entry.Role == (isDad ? "Dad" : "Child")
                ? entry : null;
        }

        private void OnSkinChanged(FixedString64Bytes previous, FixedString64Bytes current) => Apply(current.ToString());

        private void Apply(string id)
        {
            Restore();
            var entry = Resolve(id);
            if (entry == null) return;

            skin = Instantiate(entry.Model, gameplayAnimator.transform, false);
            skin.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = skin.GetComponent<Animator>();
            skin.transform.localScale = Vector3.one * (gameplayAnimator.humanScale / animator.humanScale);
            animator.fireEvents = false;
            animator.enabled = false;
            sourcePose = new HumanPoseHandler(gameplayAnimator.avatar, gameplayAnimator.transform);
            skinPose = new HumanPoseHandler(animator.avatar, animator.transform);
            sourceHips = gameplayAnimator.GetBoneTransform(HumanBodyBones.Hips);
            skinHips = animator.GetBoneTransform(HumanBodyBones.Hips);
            skinRenderer = skin.GetComponentInChildren<SkinnedMeshRenderer>();
            originalForceOff = originalRenderer.forceRenderingOff;
            originalCulling = gameplayAnimator.cullingMode;
            originalRenderer.forceRenderingOff = true;
            gameplayAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            LateUpdate();
        }

        private void LateUpdate()
        {
            if (skinPose == null) return;
            // The gameplay rig keeps driving collisions, IK, and animation events.
            sourcePose.GetHumanPose(ref pose);
            skinPose.SetHumanPose(ref pose);
            skinHips.position = sourceHips.position;
            skinRenderer.enabled = originalRenderer.enabled && !originalForceOff;
        }

        private void Restore()
        {
            sourcePose?.Dispose();
            skinPose?.Dispose();
            sourcePose = null;
            skinPose = null;
            if (skin == null) return;
            originalRenderer.forceRenderingOff = originalForceOff;
            gameplayAnimator.cullingMode = originalCulling;
            skin.SetActive(false);
            Destroy(skin);
            skin = null;
        }
    }
}
