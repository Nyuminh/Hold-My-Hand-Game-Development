using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Object = UnityEngine.Object;

namespace Blocks.Gameplay.Core.Validation
{
    [InitializeOnLoad]
    public static class LevelThreeTransitionValidation
    {
        private const string Active = "HMH.Level3QA.Active";
        private const string Report = "AI_Reports/tests/LVL3/transition-playmode.txt";
        private static readonly string[] Keys = { CampaignProgress.LastLevelKey, "HMH.Level2.Completed", "HMH.Level2.Checkpoint", "HMH.Campaign.Level2.Checkpoint", "HMH.Campaign.Level3.Checkpoint" };
        private static int step { get => SessionState.GetInt(Active + ".Step", 0); set => SessionState.SetInt(Active + ".Step", value); }
        private static double since;
        private static ulong oldPlayer
        {
            get => ulong.TryParse(SessionState.GetString(Active + ".Player", "0"), out var id) ? id : 0;
            set => SessionState.SetString(Active + ".Player", value.ToString());
        }
        static LevelThreeTransitionValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Active, false))
                {
                    File.AppendAllText(Report, "CANCELLED: Play Mode stopped before test completion.\n");
                    RestoreSession();
                }
            };
        }

        [MenuItem("Hold My Hand/QA/Level 2 to Level 3 (Host Dad)")]
        private static void RunDad() => Run(true);
        [MenuItem("Hold My Hand/QA/Level 2 to Level 3 (Host Child)")]
        private static void RunChild() => Run(false);

        public static void Run(bool hostIsDad = true)
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save scene and exit Play Mode first.");
            Directory.CreateDirectory("AI_Reports/tests/LVL3");
            File.AppendAllText(Report, "\nLevel 2 actual altar success -> Level 3; host role=" + (hostIsDad ? "Dad" : "Child") + "\n");
            SessionState.SetBool("HMH.Level3QA.Role", Lobby3DManager.AmIDad);
            SessionState.SetBool("HMH.Level3QA.TestRole", hostIsDad);
            SessionState.SetString("HMH.Level3QA.Scene", SceneManager.GetActiveScene().path);
            foreach (var key in Keys)
            {
                SessionState.SetBool(key + Active, PlayerPrefs.HasKey(key));
                SessionState.SetInt(key + Active, PlayerPrefs.GetInt(key));
            }
            CampaignProgress.Select(2, 0);
            EditorSceneManager.OpenScene("Assets/Core/Scenes/PlayScene2.unity");
            step = 0; since = 0;
            SessionState.SetBool(Active, true);
            EditorApplication.isPlaying = true;
        }
        private static void Check(bool value, string label)
        {
            if (!value) throw new Exception(label);
            File.AppendAllText(Report, "PASS " + label + "\n");
        }
        private static void Next() { step++; since = EditorApplication.timeSinceStartup; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (since == 0) since = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - since;
            try
            {
                string scene = SceneManager.GetActiveScene().name;
                if ((step < 4 && scene != "PlayScene2") || (step == 4 && scene != "PlayScene2" && scene != "PlayScene3"))
                {
                    File.AppendAllText(Report, "CANCELLED: unexpected scene " + scene + "; test will not alter this session.\n");
                    RestoreSession();
                    return;
                }
                if (elapsed > 90) throw new TimeoutException("Step " + step + " in " + SceneManager.GetActiveScene().name);
                var net = NetworkManager.Singleton;
                if (step == 0)
                {
                    if (net == null) return;
                    Lobby3DManager.AmIDad = SessionState.GetBool("HMH.Level3QA.TestRole", true);
                    CampaignProgress.Select(2, 0);
                    var transport = net.GetComponent<UnityTransport>();
                    transport.ConnectionData.Address = "127.0.0.1";
                    transport.ConnectionData.Port = 7795;
                    Check(!net.IsListening && net.StartHost(), "source host starts"); Next(); return;
                }
                if (step == 1 && elapsed > 2)
                {
                    if (net == null || !net.IsListening || net.LocalClient?.PlayerObject == null) return;
                    var spawner = net.GetComponent<RoleBasedSpawner>();
                    var companion = Object.Instantiate(spawner.ResolvePrefab(!Lobby3DManager.AmIDad));
                    companion.GetComponent<CoopBoostThrowAbility>().SetRole(!Lobby3DManager.AmIDad);
                    companion.GetComponent<NetworkObject>().Spawn(true);
                    companion.GetComponent<CoopBoostThrowAbility>().SetRole(!Lobby3DManager.AmIDad);
                    Next(); return;
                }
                if (step == 2 && elapsed > 2)
                {
                    var climax = Object.FindAnyObjectByType<LevelTwoClimaxManager>();
                    Check(climax.nextLevelScene == "PlayScene3", "serialized Continue destination is PlayScene3");
                    Check(!climax.TryComplete(), "success rejected before prerequisites");
                    // Fixture: preceding puzzles completed. Exercise the real final altar condition.
                    climax.watermill.RestoreCheckpoint(true);
                    climax.west.RestoreCheckpoint(true); climax.east.RestoreCheckpoint(true);
                    climax.RestoreCheckpoint(true, false);
                    climax.boat.DismountPlayer(true); climax.boat.DismountPlayer(false);
                    var actors = LevelTwoActors.All.ToArray();
                    Check(actors.Any(LevelOnePlayer.IsDad) && actors.Any(a => !LevelOnePlayer.IsDad(a)), "source has both roles");
                    foreach (var a in actors)
                    {
                        a.GetComponent<CharacterController>().enabled = true;
                        a.CoreMovement.SetPosition(climax.altar.position + Vector3.right * (LevelOnePlayer.IsDad(a) ? -.8f : .8f));
                        var hand = a.GetComponent<CoopHoldHandAbility>();
                        hand.enabled = false; hand.SetHoldingHandsDirect(true);
                    }
                    Check(climax.TryComplete() && climax.Completed, "actual hand-holding altar condition triggers success");
                    Next(); return;
                }
                if (step == 3 && elapsed > .5)
                {
                    Check(PlayerPrefs.GetInt("HMH.Level2.Completed") == 1, "success persisted");
                    Check(CampaignProgress.SavedCheckpoint(2) == 6, "completed checkpoint is 6");
                    oldPlayer = net.LocalClient.PlayerObject.NetworkObjectId;
                    Object.FindAnyObjectByType<LevelTwoClimaxManager>().ContinueToNextLevel();
                    Next(); return;
                }
                if (step == 4 && SceneManager.GetActiveScene().name == "PlayScene3" && elapsed > 4)
                {
                    Check(net.IsListening && net.IsHost, "host session survives continuation");
                    var player = net.LocalClient.PlayerObject;
                    Check(player != null && player.IsSpawned && player.NetworkObjectId != oldPlayer, "fresh destination player spawned");
                    var actor = player.GetComponent<CorePlayerManager>();
                    Check(LevelOnePlayer.IsDad(actor) == Lobby3DManager.AmIDad, "selected role preserved");
                    var gm = GameManager.Instance;
                    Check(gm != null && Vector3.Distance(player.transform.position, gm.GetSpawnPosition(net.LocalClientId)) < 3, "player arrives at level 3 start");
                    Check(Vector3.Distance(gm.GetSpawnPosition(0), gm.GetSpawnPosition(1)) > 1.2f, "two separate start spawn positions");
                    Check(Object.FindAnyObjectByType<TutorialManager>() == null && Object.FindAnyObjectByType<TutorialObjectiveDirector>() == null, "no tutorial session or objectives");
                    Check(Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).All(d => d.visualTreeAsset == null || d.visualTreeAsset.name != "TutorialHUD"), "no tutorial HUD");
                    Check(Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 1, "only live NetworkManager remains");
                    Check(actor.CoreMovement.enabled && actor.CoreMovement.IsMovementEnabled && actor.CoreInput.enabled, "movement and input unlocked");
                    var camera = actor.GetComponent<CoreCameraController>();
                    Check(camera.enabled && camera.ActiveCameraMode != null, "gameplay camera active");
                    Check(CampaignProgress.Level("PlayScene3") == 3 && CampaignProgress.LastLevel == 3, "campaign correctly records level 3");
                    foreach (var d in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                        if (d.name == "LoadingScreenUI")
                        {
                            var root = d.rootVisualElement;
                            File.AppendAllText(Report, "UI inline=" + root.style.display + " resolved=" + root.resolvedStyle.display + " panel=" + (root.panel != null) + "\n");
                            // An unattached editor panel does not resolve its styles. Inline
                            // display:none is still the authoritative hidden state in that case.
                            Check(root.style.display.value == DisplayStyle.None || root.resolvedStyle.display == DisplayStyle.None, "loading overlay hidden");
                        }
                    Check(Object.FindAnyObjectByType<CraneElevatorController>() != null && Object.FindAnyObjectByType<GiantGearTurntableController>() != null && Object.FindAnyObjectByType<CoveDrawbridgeCoopController>() != null && Object.FindAnyObjectByType<Stage3ZiplineCoopController>() != null && Object.FindAnyObjectByType<CoopLevelExitPortal>() != null, "authored level 3 puzzles remain");
                    File.AppendAllText(Report, "RESULT PASS. LIMITATION: host plus owned companion fixture; independent two-client transition not exercised.\n");
                    Finish();
                }
            }
            catch (Exception ex) { File.AppendAllText(Report, "RESULT FAIL " + ex + "\n"); Debug.LogException(ex); Finish(); }
        }
        private static void Finish()
        {
            RestoreSession();
            EditorApplication.isPlaying = false;
        }
        private static void RestoreSession()
        {
            SessionState.SetBool(Active, false);
            foreach (var key in Keys)
                if (SessionState.GetBool(key + Active, false)) PlayerPrefs.SetInt(key, SessionState.GetInt(key + Active, 0)); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); CampaignProgress.Select(1, 0);
            Lobby3DManager.AmIDad = SessionState.GetBool("HMH.Level3QA.Role", true);
        }
    }
}
