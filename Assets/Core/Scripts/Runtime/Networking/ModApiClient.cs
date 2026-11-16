using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    /// <summary>
    /// API Client dành cho các tính năng kiểm duyệt và nội dung (Moderator & Admin).
    /// Hỗ trợ CRUD Chapters, Quests, Characters (chỉ số stats), Bosses, Tools, v.v.
    /// </summary>
    public static class ModApiClient
    {
        [Serializable]
        public class CloseLobbyRequest
        {
            public string reason;
            public bool notifyPlayers = true;
        }

        [Serializable]
        public class CloseLobbyResponse
        {
            public bool success;
            public string lobbyCode;
            public string previousStatus;
            public string currentStatus;
            public string closedBy;
            public string closedAt;
            public string reason;
            public AffectedPlayer[] affectedPlayers;
        }

        [Serializable]
        public class AffectedPlayer
        {
            public string playerId;
            public string username;
            public string role;
        }

        public static async Task<CloseLobbyResponse> CloseLobbyAsync(string lobbyCode, CloseLobbyRequest req)
        {
            if (string.IsNullOrWhiteSpace(lobbyCode)) throw new ArgumentException("Lobby code is required.", nameof(lobbyCode));
            var json = await ApiClient.PostAsync($"/mod/lobby/{Uri.EscapeDataString(lobbyCode)}/close", JsonUtility.ToJson(req));
            return JsonUtility.FromJson<CloseLobbyResponse>(json);
        }
        // ─── Chapters CRUD ──────────────────────────────────────────

        public static async Task<ChapterDto[]> GetChaptersAsync()
        {
            var json = await ApiClient.GetAsync("/mod/chapters");
            return JsonHelper.FromJsonArray<ChapterDto>(json);
        }

        public static async Task<ChapterDto> GetChapterByIdAsync(string id)
        {
            var json = await ApiClient.GetAsync($"/mod/chapters/{id}");
            return JsonUtility.FromJson<ChapterDto>(json);
        }

        public static async Task<ChapterDto> CreateChapterAsync(CreateChapterRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PostAsync("/mod/chapters", body);
            return JsonUtility.FromJson<ChapterDto>(json);
        }

        public static async Task<ChapterDto> UpdateChapterAsync(string id, UpdateChapterRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PutAsync($"/mod/chapters/{id}", body);
            return JsonUtility.FromJson<ChapterDto>(json);
        }

        public static async Task DeleteChapterAsync(string id)
        {
            await ApiClient.DeleteAsync($"/mod/chapters/{id}");
        }

        // ─── Quests CRUD ────────────────────────────────────────────

        public static async Task<QuestDto[]> GetQuestsAsync(string chapterId = null)
        {
            var endpoint = string.IsNullOrEmpty(chapterId) ? "/mod/quests" : $"/mod/quests?chapterId={chapterId}";
            var json = await ApiClient.GetAsync(endpoint);
            return JsonHelper.FromJsonArray<QuestDto>(json);
        }

        public static async Task<QuestDto> GetQuestByIdAsync(string id)
        {
            var json = await ApiClient.GetAsync($"/mod/quests/{id}");
            return JsonUtility.FromJson<QuestDto>(json);
        }

        public static async Task<QuestDto> CreateQuestAsync(CreateQuestRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PostAsync("/mod/quests", body);
            return JsonUtility.FromJson<QuestDto>(json);
        }

        public static async Task<QuestDto> UpdateQuestAsync(string id, UpdateQuestRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PutAsync($"/mod/quests/{id}", body);
            return JsonUtility.FromJson<QuestDto>(json);
        }

        public static async Task DeleteQuestAsync(string id)
        {
            await ApiClient.DeleteAsync($"/mod/quests/{id}");
        }

        // ─── Characters & Stats CRUD ────────────────────────────────

        public static async Task<CharacterDto[]> GetCharactersAsync()
        {
            var json = await ApiClient.GetAsync("/mod/characters");
            return JsonHelper.FromJsonArray<CharacterDto>(json);
        }

        public static async Task<CharacterDto> GetCharacterByIdAsync(string id)
        {
            var json = await ApiClient.GetAsync($"/mod/characters/{id}");
            return JsonUtility.FromJson<CharacterDto>(json);
        }

        public static async Task<CharacterDto> CreateCharacterAsync(CreateCharacterRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PostAsync("/mod/characters", body);
            return JsonUtility.FromJson<CharacterDto>(json);
        }

        public static async Task<CharacterDto> UpdateCharacterAsync(string id, UpdateCharacterRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PutAsync($"/mod/characters/{id}", body);
            return JsonUtility.FromJson<CharacterDto>(json);
        }

        public static async Task DeleteCharacterAsync(string id)
        {
            await ApiClient.DeleteAsync($"/mod/characters/{id}");
        }

        // ─── Bosses CRUD ────────────────────────────────────────────

        public static async Task<BossDto[]> GetBossesAsync()
        {
            var json = await ApiClient.GetAsync("/mod/bosses");
            return JsonHelper.FromJsonArray<BossDto>(json);
        }

        public static async Task<BossDto> CreateBossAsync(CreateBossRequest req)
        {
            var body = JsonUtility.ToJson(req);
            var json = await ApiClient.PostAsync("/mod/bosses", body);
            return JsonUtility.FromJson<BossDto>(json);
        }

        public static async Task DeleteBossAsync(string id)
        {
            await ApiClient.DeleteAsync($"/mod/bosses/{id}");
        }

        // ─── DTOs & Models ──────────────────────────────────────────

        [Serializable]
        public class ChapterDto
        {
            public string id;
            public string name;
            public string theme;
            public int orderIndex;
            public string description;
            public string backgroundAssetKey;
            public bool isActive;
        }

        [Serializable]
        public class CreateChapterRequest
        {
            public string name;
            public string theme;
            public int orderIndex;
            public string description;
            public bool isActive = true;
        }

        [Serializable]
        public class UpdateChapterRequest
        {
            public string name;
            public string theme;
            public int orderIndex;
            public string description;
            public bool isActive = true;
        }

        [Serializable]
        public class QuestDto
        {
            public string id;
            public string chapterId;
            public string name;
            public string questType;
            public string description;
            public int orderIndex;
            public bool isActive;
        }

        [Serializable]
        public class CreateQuestRequest
        {
            public string chapterId;
            public string name;
            public string questType;
            public string description;
            public int orderIndex;
            public bool isActive = true;
        }

        [Serializable]
        public class UpdateQuestRequest
        {
            public string name;
            public string questType;
            public string description;
            public int orderIndex;
            public bool isActive = true;
        }

        [Serializable]
        public class StatAttributeDto
        {
            public string statName;
            public float minValue;
            public float maxValue;
            public float startingValue;
            public float regenRate;
            public float regenDelay;
            public int eventFlags;
        }

        [Serializable]
        public class MovementSettingsDto
        {
            public float moveSpeed = 3.0f;
            public float sprintSpeed = 15.0f;
            public float rotationSmoothTime = 0.1f;
            public float speedChangeRate = 15.0f;
            public float forceDecayRate = 5.0f;
            public float gravity = -15.0f;
            public float jumpHeight = 3.0f;
            public float groundedOffset = -0.14f;
            public float groundedRadius = 0.28f;
            public float slopeForce = 5.0f;
            public float slopeForceRayLength = 1.5f;
        }

        [Serializable]
        public class CharacterDto
        {
            public string id;
            public string name;
            public string physicsType;
            public string primaryStatName;
            public bool isActive;
            public StatAttributeDto[] attributes;
            public MovementSettingsDto movement;
        }

        [Serializable]
        public class CreateCharacterRequest
        {
            public string name;
            public string physicsType;
            public string primaryStatName;
            public bool isActive = true;
            public StatAttributeDto[] attributes;
            public MovementSettingsDto movement;
        }

        [Serializable]
        public class UpdateCharacterRequest
        {
            public string name;
            public string physicsType;
            public string primaryStatName;
            public bool isActive = true;
            public StatAttributeDto[] attributes;
            public MovementSettingsDto movement;
        }

        [Serializable]
        public class BossDto
        {
            public string id;
            public string chapterId;
            public string name;
            public int phasesCount;
        }

        [Serializable]
        public class CreateBossRequest
        {
            public string chapterId;
            public string name;
            public int phasesCount = 1;
        }
    }
}
