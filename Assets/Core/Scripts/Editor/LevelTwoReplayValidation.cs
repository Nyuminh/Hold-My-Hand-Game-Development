using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Object = UnityEngine.Object;

namespace Blocks.Gameplay.Core.Validation
{
    [InitializeOnLoad]
    public static class LevelTwoReplayValidation
    {
        private const string Active = "HMH.ReplayQA.Active";
        private const string Report = "AI_Reports/tests/LVL2/replay-regression.txt";
        private static readonly string[] Keys = { CampaignProgress.LastLevelKey,
            CampaignProgress.CheckpointKey(1), CampaignProgress.CheckpointKey(2), CampaignProgress.CheckpointKey(3),
            "HMH.Level1.Completed", "HMH.Level2.Completed", "HMH.Level2.Checkpoint" };
        private static int Step { get => SessionState.GetInt(Active + ".Step", 0); set => SessionState.SetInt(Active + ".Step", value); }
        private static double since;
        static LevelTwoReplayValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Active, false))
                { File.AppendAllText(Report, "CANCELLED: stopped by user\n"); RestorePreferences(); }
            };
        }
        [MenuItem("Hold My Hand/QA/Level 2 Fresh Entry and Replay")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save scene and exit Play Mode first.");
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Fresh entry with saved completion -> altar -> Escape restart -> altar again -> PlayScene3\n");
            SessionState.SetBool(Active + ".Role", Lobby3DManager.AmIDad);
            foreach (string key in Keys)
            {
                SessionState.SetBool(Active + key + ".Exists", PlayerPrefs.HasKey(key));
                SessionState.SetInt(Active + key, PlayerPrefs.GetInt(key));
            }
            // Seed the exact reported stale state. Do not clear it to make entry pass.
            CampaignProgress.Save(2, 6);
            CampaignProgress.Select(3, 0);
            Check(CampaignProgress.ConsumeStart(2) == 0, "ordinary entry ignores saved terminal checkpoint");
            CampaignProgress.Select(2, 4);
            Check(CampaignProgress.ConsumeStart(2) == 4, "explicit Continue preserves requested checkpoint");
            Check(CampaignProgress.ConsumeStart(2) == 0, "checkpoint request consumed only once");
            EditorSceneManager.OpenScene("Assets/Core/Scenes/PlayScene2.unity");
            Step = 0; since = 0;
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }
        private static void Check(bool ok, string label)
        { if (!ok) throw new Exception(label); File.AppendAllText(Report, "PASS " + label + "\n"); }
        private static void Next() { Step++; since = EditorApplication.timeSinceStartup; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (since == 0) since = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - since;
            try
            {
                string scene = SceneManager.GetActiveScene().name;
                if ((Step < 7 && scene != "PlayScene2") || (Step >= 7 && scene != "PlayScene2" && scene != "PlayScene3"))
                { File.AppendAllText(Report, "CANCELLED: unrelated scene\n"); RestorePreferences(); return; }
                if (elapsed > 75) throw new TimeoutException("Step " + Step);
                var net = NetworkManager.Singleton;
                if (Step == 0)
                {
                    if (net == null) return;
                    Lobby3DManager.AmIDad = true;
                    var transport = net.GetComponent<UnityTransport>();
                    transport.ConnectionData.Address = "127.0.0.1"; transport.ConnectionData.Port = 7796;
                    Check(net.StartHost(), "host starts with saved checkpoint 6"); Next(); return;
                }
                if (Step == 1 && elapsed > 1)
                {
                    if (net?.LocalClient?.PlayerObject == null) return;
                    var child = Object.Instantiate(net.GetComponent<RoleBasedSpawner>().ResolvePrefab(false));
                    child.name = "ReplayFixtureChild";
                    child.GetComponent<NetworkObject>().Spawn(true);
                    child.GetComponent<CoopBoostThrowAbility>().SetRole(false);
                    var visuals = child.GetComponent<PlayerRoleVisuals>();
                    if (visuals != null)
                        ((NetworkVariable<bool>)typeof(PlayerRoleVisuals).GetField("m_IsDad", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(visuals)).Value = false;
                    Next(); return;
                }
                var cp = Object.FindAnyObjectByType<CampaignCheckpointController>();
                var climax = Object.FindAnyObjectByType<LevelTwoClimaxManager>();
                if (Step == 2 && elapsed > 2)
                {
                    if (cp == null || !cp.Ready) return;
                    Check(cp.Checkpoint == 0 && !climax.Completed && !climax.Docked, "fresh entry never restores old victory");
                    Check(LevelTwoActors.All.All(a => a.transform.position.z < 40), "both roles begin at starting dock");
                    Check(!climax.TryComplete(), "cannot win before playing");
                    ReachAltar(climax);
                    Check(climax.TryComplete(), "altar succeeds after disembarking outside arrival box");
                    Next(); return;
                }
                if (Step == 3 && elapsed > .3)
                {
                    Check(climax.Completed && CampaignProgress.SavedCheckpoint(2) == 6, "first success and completion checkpoint recorded");
                    // Invoke the same handler bound to the actual Escape menu button.
                    typeof(InGameEscapeMenuManager).GetMethod("RestartCampaign", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(InGameEscapeMenuManager.EnsureInstanceInScene(), new object[] { true });
                    Next(); return;
                }
                if (Step == 4 && elapsed > .5)
                {
                    Check(cp.Ready && cp.Checkpoint == 0 && !climax.Completed && !climax.Docked, "Escape restart clears live victory and arrival");
                    Check(!climax.watermill.Passed && !climax.west.Lit && !climax.east.Lit, "restart clears final puzzle flags");
                    Check(LevelTwoActors.All.All(a => a.transform.position.z < 40), "restart returns both roles to beginning");
                    Check(CampaignProgress.SavedCheckpoint(2) == 0 && !climax.TryComplete(), "restart saves beginning and rejects premature victory");
                    ReachAltar(climax);
                    Check(climax.TryComplete(), "success triggers again after replay");
                    Next(); return;
                }
                if (Step == 5 && elapsed > .3)
                {
                    Check(climax.Completed && CampaignProgress.SavedCheckpoint(2) == 6, "replayed success is presented and persisted again");
                    // Terminal checkpoint replay must re-arm success instead of restoring it.
                    cp.RequestRestart(false);
                    Next(); return;
                }
                if (Step == 6 && elapsed > 3.1)
                {
                    // Respect the restart button's server rate limit before replaying the finale.
                    cp.RequestRestart(false);
                    Next(); return;
                }
                if (Step == 7 && elapsed > .5)
                {
                    Check(!climax.Completed, "replaying final checkpoint does not grant automatic success");
                    ReachAltar(climax);
                    Check(climax.TryComplete(), "final checkpoint can be completed again");
                    Check(climax.nextLevelScene == "PlayScene3", "Continue targets level 3");
                    climax.ContinueToNextLevel(); Next(); return;
                }
                if (Step == 8 && scene == "PlayScene3" && elapsed > 3)
                {
                    Check(net != null && net.IsHost && net.LocalClient.PlayerObject != null && net.LocalClient.PlayerObject.IsSpawned,
                        "replayed success Continue preserves host and spawns level 3 player");
                    File.AppendAllText(Report, "RESULT PASS. Prior-puzzle states were fixtures; altar/restart/continue used real handlers. Independent clients untested.\n");
                    Finish();
                }
            }
            catch (Exception ex) { File.AppendAllText(Report, "RESULT FAIL " + ex + "\n"); Debug.LogException(ex); Finish(); }
        }
        private static void ReachAltar(LevelTwoClimaxManager climax)
        {
            bool wasDocked = climax.Docked;
            // Preceding puzzle fixtures only. Never force Completed, Docked, or TryComplete's result.
            foreach (var gate in Object.FindObjectsByType<RiverSlalomGate>()) gate.RestoreCheckpoint(true);
            Object.FindAnyObjectByType<SluiceGateObstacle>().RestoreCheckpoint(true);
            climax.west.RestoreCheckpoint(true); climax.east.RestoreCheckpoint(true);
            climax.watermill.RestoreCheckpoint(true);
            climax.boat.ResetForCheckpoint(new Vector3(100, .53f, 238));
            climax.boat.MountCrewAtCheckpoint();
            climax.boat.DismountPlayer(true); climax.boat.DismountPlayer(false);
            Check(climax.Docked == wasDocked, "fixture did not pre-set harbor arrival");
            foreach (var actor in LevelTwoActors.All)
            {
                actor.CoreMovement.SetPosition(climax.altar.position + Vector3.right * (LevelOnePlayer.IsDad(actor) ? -.8f : .8f));
                var hand = actor.GetComponent<CoopHoldHandAbility>();
                hand.enabled = false; hand.SetHoldingHandsDirect(true);
            }
        }
        private static void Finish() { RestorePreferences(); EditorApplication.isPlaying = false; }
        private static void RestorePreferences()
        {
            SessionState.SetBool(Active, false);
            foreach (string key in Keys)
                if (SessionState.GetBool(Active + key + ".Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(Active + key, 0));
                else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            CampaignProgress.Select(1, 0);
            Lobby3DManager.AmIDad = SessionState.GetBool(Active + ".Role", true);
            File.AppendAllText(Report, "RESTORED pre-test campaign preferences and role.\n");
        }
    }
}
