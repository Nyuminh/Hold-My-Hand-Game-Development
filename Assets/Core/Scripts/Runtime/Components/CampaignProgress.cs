using UnityEngine;

namespace Blocks.Gameplay.Core
{
    // Local durable progress. The room host selects the start; Netcode replicates the result.
    public static class CampaignProgress
    {
        public const string LastLevelKey = "HMH.Campaign.LastLevel";
        private static int requestedLevel, requestedCheckpoint;
        public static string CheckpointKey(int level) => "HMH.Campaign.Level" + level + ".Checkpoint";
        public static int LastLevel => Mathf.Clamp(PlayerPrefs.GetInt(LastLevelKey,
            PlayerPrefs.GetInt("HMH.Level1.Completed", 0) == 1 ? 2 : 1), 1, 2);
        public static string Scene(int level) => level == 2 ? "PlayScene2" : "PlayScene3";
        public static int Level(string scene) => scene == "PlayScene2" ? 2 : scene == "PlayScene3" ? 1 : 0;
        public static int Clamp(int level, int checkpoint) => Mathf.Clamp(checkpoint, 0, level == 2 ? 6 : 4);
        public static int SavedCheckpoint(int level) => Clamp(level, PlayerPrefs.GetInt(CheckpointKey(level), 0));
        public static void Save(int level, int checkpoint)
        {
            if (level < 1 || level > 2) return;
            PlayerPrefs.SetInt(LastLevelKey, level);
            PlayerPrefs.SetInt(CheckpointKey(level), Clamp(level, checkpoint));
            PlayerPrefs.Save();
        }
        public static void Select(int level, int checkpoint)
        { requestedLevel = Mathf.Clamp(level, 1, 2); requestedCheckpoint = Clamp(requestedLevel, checkpoint); }
        public static int ConsumeStart(int level)
        {
            int checkpoint = requestedLevel == level ? requestedCheckpoint : SavedCheckpoint(level);
            requestedLevel = 0;
            return checkpoint;
        }
        public static string Description(int level, int checkpoint) => "Màn " + level + " · " +
            (checkpoint >= (level == 2 ? 6 : 4) ? "Đã hoàn thành" : "Chặng " + (checkpoint + 1));
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { requestedLevel = 0; }
    }
}
