using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Quản lý Điểm Kiểm Tra (Checkpoints), Khu Vực Rơi Chết (Damage / Kill Zone)
    /// và Cơ Chế Hồi Sinh (Respawn) chuẩn cho PlayScene3.
    /// Hoạt động 100% trơn tru cả khi Test 1 mình (Solo Offline) lẫn Chơi mạng 2 người (Netcode Co-op).
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class PlayScene3RespawnManager : NetworkBehaviour
    {
        public static PlayScene3RespawnManager Instance { get; private set; }

        [Header("Fall & Death Settings")]
        [Tooltip("Cao độ sàn rơi xuống vực kích hoạt chết và hồi sinh toàn cục (mét)")]
        [SerializeField] private float killPlaneY = -3.5f;

        [Tooltip("Thời gian chờ hồi sinh giữa các lần (giây) để tránh trigger liên tục")]
        [SerializeField] private float respawnCooldown = 0.5f;

        [Header("Checkpoints Configuration")]
        [Tooltip("Danh sách các điểm kiểm tra cố định trong PlayScene3")]
        [SerializeField] private List<CheckpointData> checkpoints = new List<CheckpointData>();

        [System.Serializable]
        public struct CheckpointData
        {
            public string name;
            public Vector3 position;
            public float triggerRadius;
            public bool isReached;

            public CheckpointData(string n, Vector3 pos, float r = 5.0f)
            {
                name = n;
                position = pos;
                triggerRadius = r;
                isReached = false;
            }
        }

        // NetworkVariable đồng bộ checkpoint index cao nhất đạt được giữa Host và Client
        private readonly NetworkVariable<int> currentCheckpointIndex = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private int localSoloCheckpoint = 0;
        private readonly Dictionary<Transform, float> lastRespawnTime = new Dictionary<Transform, float>();
        private string activeDeathPrompt = "";
        private float promptDismissTime = 0f;

        public int CurrentCheckpoint => IsNetworkActive ? currentCheckpointIndex.Value : localSoloCheckpoint;
        public bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeDefaultCheckpoints();
        }

        private void InitializeDefaultCheckpoints()
        {
            if (checkpoints.Count > 0) return;

            // 0: Khởi đầu Zone 1 (Lều trại lối vào)
            checkpoints.Add(new CheckpointData("0. Bãi Khởi Đầu (Zone 1 Entrance)", new Vector3(-1.0f, 1.25f, 5.0f), 5.5f));

            // 1: Sau khi qua cầu rút Zone 1 (Trụ đá Islet)
            checkpoints.Add(new CheckpointData("1. Trụ Đá Sau Cầu Rút (Zone 1 Islet)", new Vector3(-1.0f, 1.25f, -15.0f), 5.5f));

            // 2: Bến Cáp Treo Islet Zone 2
            checkpoints.Add(new CheckpointData("2. Bến Cáp Treo Islet (Zone 2)", new Vector3(-1.0f, 10.85f, -23.0f), 5.5f));

            // 3: Bờ Cao Nguyên Zone 3 (Bàn Xoay Bánh Răng)
            checkpoints.Add(new CheckpointData("3. Cao Nguyên Bàn Xoay (Zone 3)", new Vector3(-1.0f, 14.50f, -48.0f), 6.5f));

            // 4: Bến Cầu Vịnh Hẹp Cove Zone 4
            checkpoints.Add(new CheckpointData("4. Bến Cầu Vịnh Hẹp (Zone 4 Cove)", new Vector3(26.0f, 3.80f, -75.0f), 6.5f));

            // 5: Sàn Tàu Đắm Lấy Chìa Khóa Zone 4
            checkpoints.Add(new CheckpointData("5. Sàn Tàu Đắm Lấy Chìa Khóa (Zone 4 Boat)", new Vector3(44.5f, 4.50f, -54.0f), 5.5f));

            // 6: Chân Cầu Thang Vách Đá Zone 5
            checkpoints.Add(new CheckpointData("6. Chân Cầu Thang Vách Đá (Zone 5 Cliff)", new Vector3(-11.5f, 14.0f, -73.5f), 6.0f));

            // 7: Đỉnh Tháp Canh Skybridge Zone 6
            checkpoints.Add(new CheckpointData("7. Đỉnh Tháp Canh Skybridge (Zone 6)", new Vector3(-10.5f, 31.8f, -87.0f), 6.0f));

            // 8: Sân Trước Cổng Lâu Đài Zone 7 (Sau khi đu dây)
            checkpoints.Add(new CheckpointData("8. Sân Trước Cổng Lâu Đài (Zone 7)", new Vector3(6.0f, 24.5f, -101.0f), 6.5f));

            // 9: Sân Trong Lâu Đài Hoàng Gia Zone 7
            checkpoints.Add(new CheckpointData("9. Sân Lâu Đài Hoàng Gia (Zone 7 Courtyard)", new Vector3(0.0f, 24.5f, -118.5f), 7.0f));

            // 10: Bệ Bay Khinh Khí Cầu Zephyr Zone 8
            checkpoints.Add(new CheckpointData("10. Bệ Bay Khinh Khí Cầu Zephyr (Zone 8)", new Vector3(0.0f, 25.0f, -135.0f), 8.0f));
        }

        private void Update()
        {
            TrackPlayerProgress();
            CheckFallHazardZones();
        }

        /// <summary>
        /// Thu thập toàn bộ transform nhân vật người chơi đang hoạt động (cả Ba lẫn Con).
        /// </summary>
        public List<Transform> GetAllPlayerTransforms()
        {
            var list = new List<Transform>();

            // 1. Dùng PlayerRoleVisuals nếu có
            if (PlayerRoleVisuals.AllPlayers != null && PlayerRoleVisuals.AllPlayers.Count > 0)
            {
                foreach (var p in PlayerRoleVisuals.AllPlayers)
                {
                    if (p != null && !list.Contains(p.transform)) list.Add(p.transform);
                }
            }

            // 2. Tìm tất cả CharacterController trong màn
            var ccs = FindObjectsOfType<CharacterController>();
            foreach (var cc in ccs)
            {
                if (cc != null && !list.Contains(cc.transform)) list.Add(cc.transform);
            }

            // 3. Tìm theo Tag Player
            var tagged = GameObject.FindGameObjectsWithTag("Player");
            foreach (var t in tagged)
            {
                if (t != null && !list.Contains(t.transform)) list.Add(t.transform);
            }

            return list;
        }

        /// <summary>
        /// Theo dõi vị trí người chơi để tự động kích hoạt Checkpoint mới nhất khi bước vào phạm vi.
        /// </summary>
        private void TrackPlayerProgress()
        {
            var players = GetAllPlayerTransforms();
            if (players == null || players.Count == 0) return;

            for (int i = 0; i < checkpoints.Count; i++)
            {
                var cp = checkpoints[i];
                foreach (var p in players)
                {
                    if (p == null) continue;
                    float dist = Vector3.Distance(p.position, cp.position);
                    if (dist <= cp.triggerRadius)
                    {
                        if (i > CurrentCheckpoint)
                        {
                            SetCheckpoint(i);
                        }
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Quét kiểm tra nhân vật có bị rơi xuống vực/biển ở từng khu vực địa hình không.
        /// </summary>
        private void CheckFallHazardZones()
        {
            var players = GetAllPlayerTransforms();
            if (players == null || players.Count == 0) return;

            foreach (var p in players)
            {
                if (p == null) continue;
                Vector3 pos = p.position;

                // 1. Rơi xuống dưới sàn tử thần toàn cục (Y < killPlaneY)
                if (pos.y < killPlaneY)
                {
                    HandlePlayerFall(p);
                    continue;
                }

                // 2. Khu Vực Zone 2 & 3: Sàn hố khai thác đá (Quarry Abyss Floor) nằm ở Y ≈ 7.96m.
                // Nếu người chơi ngã khỏi cầu bập bênh, cáp treo hoặc cao nguyên (Y = 10m - 14m)
                // và rớt xuống Y < 9.5m trong khu vực Z từ -18 đến -65 -> Xem như rơi vực!
                if (pos.z <= -18f && pos.z >= -65f && pos.x >= -35f && pos.x <= 25f)
                {
                    if (pos.y < 9.5f)
                    {
                        HandlePlayerFall(p);
                        continue;
                    }
                }

                // 3. Khu Vực Zone 5, 6 & Vách Đá Lâu Đài:
                // Nếu rớt khỏi cầu thang vách đá hoặc cầu treo Skybridge xuống dưới Y < 12.0m
                if (pos.z < -65f && pos.z >= -105f && pos.x >= -35f && pos.x <= 20f)
                {
                    // Ngoại trừ khu vực bãi biển Cove thấp (X > 20, Y ~ 3.5m)
                    if (pos.x < 18f && pos.y < 12.0f)
                    {
                        HandlePlayerFall(p);
                        continue;
                    }
                }

                // 4. Khu Vực Biển Vịnh Cove (Zone 4): nếu rớt xuống biển sâu Y < 1.0m
                if (pos.x >= 15f && pos.z <= -45f && pos.z >= -85f)
                {
                    if (pos.y < 1.5f)
                    {
                        HandlePlayerFall(p);
                        continue;
                    }
                }
            }
        }

        public void SetCheckpoint(int index)
        {
            index = Mathf.Clamp(index, 0, checkpoints.Count - 1);
            if (index <= CurrentCheckpoint) return;

            if (IsNetworkActive && IsServer)
            {
                currentCheckpointIndex.Value = index;
            }
            else if (!IsNetworkActive)
            {
                localSoloCheckpoint = index;
            }

            Debug.Log($"<color=green>[Checkpoint] 🚩 ĐÃ KÍCH HOẠT CHECKPOINT MỚI: {checkpoints[index].name}!</color>");
            ShowPrompt($"🚩 Đã lưu Điểm Hồi Sinh: <color=#FFEE55>{checkpoints[index].name}</color>", 3.0f);
        }

        public Vector3 GetCheckpointPosition(int index)
        {
            if (checkpoints.Count == 0) return new Vector3(-1.0f, 1.25f, 5.0f);
            index = Mathf.Clamp(index, 0, checkpoints.Count - 1);
            return checkpoints[index].position;
        }

        /// <summary>
        /// Xử lý nhân vật rơi xuống vực: kích hoạt hồi sinh tại Checkpoint gần nhất.
        /// </summary>
        public void HandlePlayerFall(Transform playerTransform)
        {
            if (playerTransform == null) return;

            if (lastRespawnTime.TryGetValue(playerTransform, out float lastTime))
            {
                if (Time.time - lastTime < respawnCooldown) return;
            }
            lastRespawnTime[playerTransform] = Time.time;

            Vector3 spawnPoint = GetCheckpointPosition(CurrentCheckpoint);
            string pName = playerTransform.gameObject.name;

            Debug.Log($"<color=orange>[RespawnManager] ⚠️ '{pName}' rơi xuống vực (Y: {playerTransform.position.y:F1})! Đang hồi sinh tại Checkpoint {CurrentCheckpoint} ({spawnPoint})...</color>");
            ShowPrompt($"⚠️ <color=#FF6666>Rơi xuống vực!</color> Đang hồi sinh tại: <color=#FFEE55>{checkpoints[CurrentCheckpoint].name}</color>", 2.5f);

            // 1. Tắt tạm thời CharacterController để định vị lại tọa độ mượt mà
            if (playerTransform.TryGetComponent<CharacterController>(out var cc))
            {
                cc.enabled = false;
                playerTransform.position = spawnPoint;
                cc.enabled = true;
            }
            else
            {
                playerTransform.position = spawnPoint;
            }

            // 2. Reset quán tính di chuyển nếu có CoreMovement
            if (playerTransform.TryGetComponent<CoreMovement>(out var movement))
            {
                movement.SetPosition(spawnPoint);
                movement.ResetMovementForces();
            }

            // 3. Phục hồi 100% máu
            if (playerTransform.TryGetComponent<CoreStatsHandler>(out var stats))
            {
                stats.ModifyStat(StatKeys.Health, 100, 0, ModificationSource.Healing);
            }

            // 4. Cập nhật LifeState sang Respawned
            if (playerTransform.TryGetComponent<CorePlayerState>(out var pState))
            {
                pState.SetLifeState(PlayerLifeState.Respawned);
            }
        }

        private void ShowPrompt(string message, float duration)
        {
            activeDeathPrompt = message;
            promptDismissTime = Time.time + duration;
        }

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(activeDeathPrompt) && Time.time < promptDismissTime)
            {
                GUIStyle style = new GUIStyle(GUI.skin.box);
                style.fontSize = 16;
                style.fontStyle = FontStyle.Bold;
                style.alignment = TextAnchor.MiddleCenter;
                style.normal.textColor = Color.white;

                float width = 560f;
                float height = 52f;
                float x = (Screen.width - width) * 0.5f;
                float y = 90f;

                GUI.backgroundColor = new Color(0.1f, 0.1f, 0.15f, 0.92f);
                GUI.Box(new Rect(x, y, width, height), activeDeathPrompt, style);
            }
        }
    }

    /// <summary>
    /// Trigger Zone gây chết khi rơi xuống vực / biển / vực sâu (Fall Damage / Kill Zone) cho PlayScene3.
    /// Bất kỳ nhân vật (Ba hoặc Con) chạm vào trigger này sẽ lập tức được hồi sinh tại Checkpoint gần nhất.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayScene3FallDamageZone : MonoBehaviour
    {
        [Tooltip("Tên khu vực rơi vực hiển thị trên thông báo")]
        [SerializeField] private string hazardZoneName = "Vực Thẳm";

        private Collider triggerCollider;

        private void Awake()
        {
            triggerCollider = GetComponent<Collider>();
            if (triggerCollider != null)
            {
                triggerCollider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            CheckAndTriggerRespawn(other);
        }

        private void OnTriggerStay(Collider other)
        {
            CheckAndTriggerRespawn(other);
        }

        private void CheckAndTriggerRespawn(Collider other)
        {
            if (other == null) return;
            Transform playerTransform = GetPlayerRootTransform(other);
            if (playerTransform != null && PlayScene3RespawnManager.Instance != null)
            {
                PlayScene3RespawnManager.Instance.HandlePlayerFall(playerTransform);
            }
        }

        public static Transform GetPlayerRootTransform(Collider col)
        {
            if (col == null) return null;
            var cc = col.GetComponent<CharacterController>();
            if (cc != null) return cc.transform;
            var visuals = col.GetComponentInParent<PlayerRoleVisuals>();
            if (visuals != null) return visuals.transform;
            var movement = col.GetComponentInParent<CoreMovement>();
            if (movement != null) return movement.transform;
            if (col.CompareTag("Player")) return col.transform;
            Transform curr = col.transform;
            while (curr != null)
            {
                string n = curr.name.ToLower();
                if (n.Contains("dad") || n.Contains("son") || n.Contains("child") || n.Contains("player"))
                {
                    return curr;
                }
                curr = curr.parent;
            }
            return null;
        }
    }

}