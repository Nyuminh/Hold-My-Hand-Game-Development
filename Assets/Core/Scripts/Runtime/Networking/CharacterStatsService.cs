using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// Dịch vụ quản lý và đồng bộ chỉ số nhân vật (Stats & Movement) từ Backend.
    /// Tự động nạp trước khi vào Scene và tự động áp dụng vào nhân vật khi Spawn/Awake.
    /// </summary>
    public static class CharacterStatsService
    {
        public const string DAD_ID = "6ad72f610aca21a5c69e49c3";
        public const string CHILD_ID = "6ac5ba545d35b6c25c59ec4e";

        public static ModApiClient.CharacterDto DadCharacter { get; private set; }
        public static ModApiClient.CharacterDto ChildCharacter { get; private set; }

        public static bool IsLoaded { get; private set; }
        public static bool IsBackendLoaded { get; private set; }
        private static Task<bool> s_CurrentFetchTask;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            InitFallbackDefaults();
            _ = FetchCharacterStatsAsync();
        }

        public static void InitFallbackDefaults()
        {
            if (DadCharacter == null) DadCharacter = CreateDefaultDad();
            if (ChildCharacter == null) ChildCharacter = CreateDefaultChild();
            IsLoaded = true;
        }

        public static async Task<bool> EnsureLoadedAsync()
        {
            if (s_CurrentFetchTask != null && !s_CurrentFetchTask.IsCompleted)
            {
                return await s_CurrentFetchTask;
            }

            if (!IsBackendLoaded)
            {
                return await FetchCharacterStatsAsync();
            }

            return true;
        }

        public static Task<bool> FetchCharacterStatsAsync(bool forceReload = false)
        {
            if (!forceReload && s_CurrentFetchTask != null && !s_CurrentFetchTask.IsCompleted)
            {
                return s_CurrentFetchTask;
            }

            s_CurrentFetchTask = FetchInternalAsync();
            return s_CurrentFetchTask;
        }

        private static async Task<bool> FetchInternalAsync()
        {
            InitFallbackDefaults();

            try
            {
                if (!ApiClient.IsLoggedIn)
                {
                    ApiClient.LoadSession();
                }

                Debug.Log("[CharacterStatsService] Đang tải chỉ số nhân vật từ Backend...");
                string json = null;

                // 1. Thử gọi /game/characters
                try
                {
                    json = await ApiClient.GetAsync("/game/characters", authenticated: ApiClient.IsLoggedIn);
                }
                catch (ApiException apiEx) when (apiEx.StatusCode == 401)
                {
                    Debug.LogWarning("[CharacterStatsService] 401 với access token hiện tại. Đang thử tải công khai không qua auth...");
                    try
                    {
                        json = await ApiClient.GetAsync("/game/characters", authenticated: false);
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CharacterStatsService] Lỗi khi gọi /game/characters ({ex.Message}). Thử endpoint dự phòng...");
                }

                // 2. Fallback thử endpoint /mod/characters nếu có quyền Admin/Mod
                if (string.IsNullOrEmpty(json) && ApiClient.IsLoggedIn)
                {
                    try
                    {
                        json = await ApiClient.GetAsync("/mod/characters", authenticated: true);
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(json))
                {
                    Debug.LogWarning("[CharacterStatsService] Không nhận được phản hồi từ backend. Giữ nguyên chỉ số hiện tại.");
                    return false;
                }

                var characters = JsonHelper.FromJsonArray<ModApiClient.CharacterDto>(json);
                if (characters == null || characters.Length == 0)
                {
                    Debug.LogWarning("[CharacterStatsService] Danh sách character rỗng từ backend.");
                    return false;
                }

                UpdateLocalCache(characters);
                IsBackendLoaded = true;
                IsLoaded = true;

                Debug.Log($"[CharacterStatsService] ✅ Đã tải và đồng bộ chỉ số thành công từ Backend! Dad moveSpeed={DadCharacter.movement?.moveSpeed}, jump={DadCharacter.movement?.jumpHeight}; Child moveSpeed={ChildCharacter.movement?.moveSpeed}, jump={ChildCharacter.movement?.jumpHeight}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterStatsService] Lỗi khi tải stats từ backend ({ex.Message}). Sử dụng dữ liệu offline fallback.");
                return false;
            }
        }

        /// <summary>
        /// Cập nhật cache cục bộ ngay lập tức (dùng khi Admin/Mod chỉnh sửa hoặc tải về).
        /// </summary>
        public static void UpdateLocalCache(ModApiClient.CharacterDto[] characters)
        {
            if (characters == null) return;
            foreach (var c in characters)
            {
                UpdateLocalCache(c);
            }
        }

        /// <summary>
        /// Cập nhật cache cục bộ cho 1 nhân vật ngay lập tức.
        /// </summary>
        public static void UpdateLocalCache(ModApiClient.CharacterDto c)
        {
            if (c == null) return;
            if (c.id == DAD_ID || (!string.IsNullOrEmpty(c.name) && c.name.ToLower().Contains("dad")))
            {
                DadCharacter = c;
            }
            else if (c.id == CHILD_ID || (!string.IsNullOrEmpty(c.name) && (c.name.ToLower().Contains("child") || c.name.ToLower().Contains("con"))))
            {
                ChildCharacter = c;
            }
            IsLoaded = true;
        }

        public static ModApiClient.CharacterDto GetCharacterDto(bool isDad)
        {
            InitFallbackDefaults();
            return isDad ? DadCharacter : ChildCharacter;
        }

        public static void ApplyStatsToCharacter(GameObject characterGo, bool isDad)
        {
            if (characterGo == null) return;
            InitFallbackDefaults();

            var dto = GetCharacterDto(isDad);
            if (dto == null) return;

            // 1. Áp dụng thông số di chuyển (CoreMovement)
            if (characterGo.TryGetComponent<CoreMovement>(out var movement) && dto.movement != null)
            {
                movement.ApplyMovementSettings(dto.movement);
            }

            // 2. Áp dụng thông số sinh tồn (CoreStatsHandler: Health, Stamina)
            if (characterGo.TryGetComponent<CoreStatsHandler>(out var stats) && dto.attributes != null)
            {
                stats.ApplyStatAttributes(dto.attributes, dto.primaryStatName);
            }

            Debug.Log($"[CharacterStatsService] ⚡ Đã tự động gán Stats cho '{characterGo.name}' (Vai: {(isDad ? "Dad" : "Child")}) -> MoveSpeed: {dto.movement?.moveSpeed}, SprintSpeed: {dto.movement?.sprintSpeed}, JumpHeight: {dto.movement?.jumpHeight}, Gravity: {dto.movement?.gravity}");
        }

        public static void ApplyStatsToCharacter(GameObject characterGo)
        {
            if (characterGo == null) return;
            bool isDad = characterGo.name.ToLower().Contains("dad");
            ApplyStatsToCharacter(characterGo, isDad);
        }

        private static ModApiClient.CharacterDto CreateDefaultChild()
        {
            return new ModApiClient.CharacterDto
            {
                id = CHILD_ID,
                name = "Child Character",
                physicsType = "Platformer",
                primaryStatName = "Health",
                isActive = true,
                attributes = new[]
                {
                    new ModApiClient.StatAttributeDto
                    {
                        statName = "Health",
                        minValue = 0f,
                        maxValue = 100f,
                        startingValue = 100f,
                        regenRate = 1f,
                        regenDelay = 4f,
                        eventFlags = 3
                    },
                    new ModApiClient.StatAttributeDto
                    {
                        statName = "Stamina",
                        minValue = 0f,
                        maxValue = 120f,
                        startingValue = 120f,
                        regenRate = 15f,
                        regenDelay = 0.5f,
                        eventFlags = 3
                    }
                },
                movement = new ModApiClient.MovementSettingsDto
                {
                    moveSpeed = 3f,
                    sprintSpeed = 15f,
                    rotationSmoothTime = 0.1f,
                    speedChangeRate = 15f,
                    forceDecayRate = 5f,
                    gravity = -15f,
                    jumpHeight = 3f,
                    groundedOffset = -0.14f,
                    groundedRadius = 0.28f,
                    slopeForce = 5f,
                    slopeForceRayLength = 1.5f
                }
            };
        }

        private static ModApiClient.CharacterDto CreateDefaultDad()
        {
            return new ModApiClient.CharacterDto
            {
                id = DAD_ID,
                name = "Dad Character",
                physicsType = "Platformer",
                primaryStatName = "Health",
                isActive = true,
                attributes = new[]
                {
                    new ModApiClient.StatAttributeDto
                    {
                        statName = "Health",
                        minValue = 0f,
                        maxValue = 100f,
                        startingValue = 100f,
                        regenRate = 1f,
                        regenDelay = 4f,
                        eventFlags = 3
                    },
                    new ModApiClient.StatAttributeDto
                    {
                        statName = "Stamina",
                        minValue = 0f,
                        maxValue = 120f,
                        startingValue = 120f,
                        regenRate = 15f,
                        regenDelay = 0.5f,
                        eventFlags = 3
                    }
                },
                movement = new ModApiClient.MovementSettingsDto
                {
                    moveSpeed = 3f,
                    sprintSpeed = 15f,
                    rotationSmoothTime = 0.1f,
                    speedChangeRate = 15f,
                    forceDecayRate = 5f,
                    gravity = -15f,
                    jumpHeight = 3f,
                    groundedOffset = -0.14f,
                    groundedRadius = 0.28f,
                    slopeForce = 5f,
                    slopeForceRayLength = 1.5f
                }
            };
        }
    }
}
