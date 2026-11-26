using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Blocks.Gameplay.Core
{
    // Local durable progress. The room host selects the start; Netcode replicates the result.
    public static class CampaignProgress
    {
        [Serializable] private class Chapter { public string scene; public string[] quests; }
        [Serializable] private class Catalog { public Chapter[] chapters; }
        private static Catalog catalog;
        private static Task inFlight;
        public static Task RefreshCatalog()
        {
            if (inFlight == null || inFlight.IsCompleted) inFlight = LoadCatalog();
            return inFlight;
        }

        private static async Task LoadCatalog()
        {
            var json = await ApiClient.GetAsync("/game/campaign", false);
            var result = JsonUtility.FromJson<Catalog>(json);
            if (result?.chapters == null) throw new InvalidOperationException("Danh sách màn chơi không hợp lệ.");
            catalog = result;
        }

        public static bool Available(int level) => level >= 1 && level <= 3 && catalog != null &&
            Array.Exists(catalog.chapters, chapter => chapter.scene == Scene(level));

        public static int NextAvailable(int current)
        {
            for (int i = current + 1; i <= 3; i++) if (Available(i)) return i;
            return 0;
        }

        public static bool SkipQuest(int level, string key)
        {
            var chapter = catalog == null ? null : Array.Find(catalog.chapters, item => item.scene == Scene(level));
            return chapter?.quests != null && Array.IndexOf(chapter.quests, key) < 0;
        }

        public const string LastLevelKey = "HMH.Campaign.LastLevel";
        private static int requestedLevel, requestedCheckpoint;
        public static string CheckpointKey(int level) => "HMH.Campaign.Level" + level + ".Checkpoint";
        public static int LastLevel => Mathf.Clamp(PlayerPrefs.GetInt(LastLevelKey,
            PlayerPrefs.GetInt("HMH.Level1.Completed", 0) == 1 ? 2 : 1), 1, 3);
        public static string Scene(int level) => level == 3 ? "PlayScene4" : level == 2 ? "PlayScene2" : "PlayScene1";
        // Older rooms may still advertise PlayScene3. Resolve their level number using the current scene mapping.
        public static int Level(string scene) => scene == "PlayScene4" || scene == "PlayScene3" ? 3 : scene == "PlayScene2" ? 2 : scene == "PlayScene1" ? 1 : 0;
        public static int Clamp(int level, int checkpoint) => Mathf.Clamp(checkpoint, 0, level == 2 ? 6 : 4);
        public static int SavedCheckpoint(int level) => Clamp(level, PlayerPrefs.GetInt(CheckpointKey(level), 0));
        public static void Save(int level, int checkpoint)
        {
            if (level < 1 || level > 3) return;
            PlayerPrefs.SetInt(LastLevelKey, level);
            PlayerPrefs.SetInt(CheckpointKey(level), Clamp(level, checkpoint));
            PlayerPrefs.Save();
        }
        public static void Select(int level, int checkpoint)
        { requestedLevel = Mathf.Clamp(level, 1, 3); requestedCheckpoint = Clamp(requestedLevel, checkpoint); }
        public static int ConsumeStart(int level)
        {
            // Only Lobby Continue supplies a saved checkpoint. Direct scene entry and
            // progression from the previous level start a fresh attempt.
            int checkpoint = requestedLevel == level ? requestedCheckpoint : 0;
            requestedLevel = 0;
            return checkpoint;
        }
        public static string Description(int level, int checkpoint) => "Màn " + level + " · " +
            (checkpoint >= (level == 2 ? 6 : 4) ? "Đã hoàn thành" : "Chặng " + (checkpoint + 1));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { requestedLevel = 0; catalog = null; inFlight = null; }
    }
}
