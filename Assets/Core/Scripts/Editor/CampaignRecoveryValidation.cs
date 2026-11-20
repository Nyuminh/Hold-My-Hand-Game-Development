using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Object = UnityEngine.Object;

namespace Blocks.Gameplay.Core
{
    [InitializeOnLoad]
    public static class CampaignRecoveryValidation
    {
        const string Active = "HMH.RecoveryQA.Active", Phase = "HMH.RecoveryQA.Phase";
        const string Report = "AI_Reports/tests/LVL2/recovery-playmode.txt";
        static readonly string[] Keys = { CampaignProgress.LastLevelKey, CampaignProgress.CheckpointKey(1), CampaignProgress.CheckpointKey(2),
            "HMH.Level1.Completed", "HMH.Level1.Checkpoint", "HMH.Level2.Completed", "HMH.Level2.Checkpoint" };
        static int step;
        static double at;
        static bool sawWreck;
        static GameObject post;
        static CampaignRecoveryValidation()
        { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += StateChanged; }
        [MenuItem("Hold My Hand/Campaign/Run Recovery QA")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty) throw new Exception("Save scenes and stop Play Mode first.");
            foreach (var key in Keys)
            { SessionState.SetBool(key + ".RecoveryQA.Exists", PlayerPrefs.HasKey(key)); SessionState.SetInt(key + ".RecoveryQA.Value", PlayerPrefs.GetInt(key)); }
            SessionState.SetString("HMH.RecoveryQA.Scene", UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            SessionState.SetBool("HMH.RecoveryQA.Role", Lobby3DManager.AmIDad);
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, "Campaign recovery regression: Unity Play Mode, local host with two owned actors.\n");
            SessionState.SetBool(Active, true); SessionState.SetInt(Phase, 1);
            CampaignProgress.Save(2, 0);
            StartPhase();
        }
        static void StartPhase()
        {
            int phase = SessionState.GetInt(Phase, 1);
            EditorSceneManager.OpenScene("Assets/Core/Scenes/" + (phase == 3 ? "PlayScene3" : "PlayScene2") + ".unity");
            step = 0; at = 0; sawWreck = false;
            EditorApplication.isPlaying = true;
        }
        static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            if (SessionState.GetBool(Active, false)) EditorApplication.delayCall += StartPhase;
            else if (SessionState.GetBool("HMH.RecoveryQA.RestoreScene", false))
            {
                SessionState.SetBool("HMH.RecoveryQA.RestoreScene", false);
                EditorApplication.delayCall += () => EditorSceneManager.OpenScene(SessionState.GetString("HMH.RecoveryQA.Scene", "Assets/Core/Scenes/LoginScene.unity"));
            }
        }
        static void Check(bool ok, string message)
        { if (!ok) throw new Exception(message); File.AppendAllText(Report, "PASS " + message + "\n"); }
        static void Next() { step++; at = EditorApplication.timeSinceStartup; }
        static void Set(object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);
        static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (at == 0) at = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - at;
            int phase = SessionState.GetInt(Phase, 1);
            try
            {
                if (elapsed > 55) throw new Exception("Timed out phase " + phase + " step " + step);
                var nm = NetworkManager.Singleton;
                var cp = Object.FindAnyObjectByType<CampaignCheckpointController>();
                if (cp == null || nm == null) return;
                var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                var boss = Object.FindAnyObjectByType<ChaseBossController>();
                if (step == 0)
                {
                    if (nm == null || cp == null) return;
                    Lobby3DManager.AmIDad = true;
                    if (boss != null) boss.startGrace = 10000;
                    var transport = nm.GetComponent<UnityTransport>(); transport.ConnectionData.Address = "127.0.0.1"; transport.ConnectionData.Port = 7794;
                    Check(nm.StartHost(), "phase " + phase + " host starts"); Next(); return;
                }
                if (step == 1 && elapsed > 2)
                {
                    var dadObject = nm.LocalClient.PlayerObject;
                    Check(dadObject != null, "host actor spawned"); dadObject.name = "The Dad Recovery QA";
                    var childObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Prefabs/The Child.prefab"));
                    childObject.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                    childObject.GetComponent<CoopBoostThrowAbility>().SetRole(false); dadObject.GetComponent<CoopBoostThrowAbility>().SetRole(true);
                    GameManager.Instance.ResumeAfterLevelTransition();
                    dadObject.name = "The Dad Recovery QA";
                    childObject.name = "The Child Recovery QA";
                    var visuals = childObject.GetComponent<PlayerRoleVisuals>();
                    if (visuals != null) ((NetworkVariable<bool>)typeof(PlayerRoleVisuals).GetField("m_IsDad", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visuals)).Value = false;
                    childObject.GetComponent<CoreMovement>().SetPosition(dadObject.transform.position + Vector3.right * 1.5f);
                    foreach (var actor in Object.FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None))
                    { actor.CoreInput.enabled = false; actor.CoreMovement.enabled = false; }
                    Next(); return;
                }
                if (phase == 1 && step == 6 && cp.BoatHealth == 0) sawWreck = true;
                if (step < 2 || !cp.Ready || !LevelTwoActors.All.Any(LevelOnePlayer.IsDad) || !LevelTwoActors.All.Any(p => !LevelOnePlayer.IsDad(p))) return;
                var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));
                if (phase == 2 && step == 2 && elapsed > 3)
                {
                    Check(cp.Checkpoint == 2 && CampaignProgress.LastLevel == 2, "fresh session resumes saved Level 2 checkpoint, not Level 1");
                    Check(boat.HasCoopCrew && Vector3.Distance(boat.transform.position, CampaignCheckpointController.BoatPosition(2)) < 8, "both actors remounted beyond the sluice on resume");
                    Check(Object.FindAnyObjectByType<SluiceGateObstacle>().Open && Object.FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None).All(g => g.Passed), "resume restores solved sluice and all slalom gates");
                    CampaignProgress.Save(1, 2); NextPhase(3); return;
                }
                if (phase == 3 && step == 2 && elapsed > 7)
                {
                    Check(boss.CheckpointIndex == 2 && boss.State == ChaseState.Chasing, "Level 1 resumes saved checkpoint and chase");
                    Check(Vector3.Distance(dad.transform.position, boss.checkpoints[2].dad.position) < 2, "Level 1 Dad restored to checkpoint");
                    Check(Vector3.Distance(child.transform.position, boss.checkpoints[2].child.position) < 2, "Level 1 Child restored to checkpoint");
                    Check(Object.FindObjectsByType<LevelOneObstacle>(FindObjectsSortMode.None).Where(o => o.checkpointAfter <= 2).All(o => o.Solved), "Level 1 prior puzzles restored");
                    cp.RequestRestart(true); Next(); return;
                }
                if (phase == 3 && step == 3 && elapsed > 5)
                {
                    Check(cp.Checkpoint == 0 && boss.CheckpointIndex == 0 && CampaignProgress.SavedCheckpoint(1) == 0, "Level 1 restart clears active checkpoint");
                    Check(Object.FindObjectsByType<LevelOneObstacle>(FindObjectsSortMode.None).All(o => !o.Solved), "Level 1 restart resets every puzzle");
                    Finish(); return;
                }
                if (phase != 1) return;
                var sluice = Object.FindAnyObjectByType<SluiceGateObstacle>();
                if (step == 2 && elapsed > 3)
                {
                    foreach (var actor in new[] { dad, child }) { actor.CoreInput.enabled = false; actor.CoreMovement.enabled = false; }
                    foreach (var gate in Object.FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None)) gate.RestoreCheckpoint(true);
                    dad.CoreMovement.SetPosition(new Vector3(110.5f, 1.15f, 70));
                    child.CoreMovement.SetPosition(new Vector3(109.5f, 1.15f, 70.5f));
                    Check(sluice.TryPushRock(dad), "Dad pushes rock after slalom");
                    Next(); return;
                }
                if (step == 3)
                {
                    if (elapsed < 1) return;
                    var cc = child.GetComponent<CharacterController>();
                    cc.Move(new Vector3(0, -.04f, .045f));
                    if (child.transform.position.z < 77.5f) return;
                    Check(sluice.ChildTraversed, "Child physically walks through full crawl passage using CharacterController.Move");
                    Check(sluice.TryOpenValve(child), "valve opens only after complete physical traversal");
                    File.AppendAllText(Report, "Child exit " + child.transform.position + ", capsule height " + cc.height + "\n");
                    Next(); return;
                }
                if (step == 4 && elapsed > 1)
                {
                    Check(cp.Checkpoint == 2 && CampaignProgress.SavedCheckpoint(2) == 2, "sluice completion saves checkpoint 2 durably");
                    boat.ResetForCheckpoint(CampaignCheckpointController.BoatPosition(2)); boat.MountCrewAtCheckpoint();
                    post = GameObject.CreatePrimitive(PrimitiveType.Cube); post.name = "QA collision post";
                    post.transform.position = new Vector3(100, 1, 90); post.transform.localScale = new Vector3(4, 3, .5f);
                    Physics.SyncTransforms(); Next(); return;
                }
                if (step == 5)
                {
                    if (cp.BoatHealth > 0) for (int i = 0; i < 4; i++) boat.TriggerRowStroke();
                    if (cp.BoatHealth == 100) return;
                    Check(cp.BoatHealth < 100 && boat.transform.position.z < 90, "actual swept-hull post collision blocks motion and loses boat health");
                    Next(); return;
                }
                if (step == 6)
                {
                    if (cp.BoatHealth == 0) sawWreck = true;
                    if (!sawWreck) for (int i = 0; i < 4; i++) boat.TriggerRowStroke();
                    if (!sawWreck || cp.BoatHealth != 100) return;
                    Check(boat.HasCoopCrew && Vector3.Distance(boat.transform.position, CampaignCheckpointController.BoatPosition(2)) < 3, "zero health automatically recovers and remounts both actors at checkpoint");
                    Object.Destroy(post); boat.transform.position = new Vector3(100, .53f, 130);
                    cp.RequestRestart(false); Next(); return;
                }
                if (step == 7 && elapsed > 3.2)
                {
                    Check(cp.Checkpoint == 2 && boat.HasCoopCrew && boat.transform.position.z < 96, "manual stage retry restores saved checkpoint and crew");
                    cp.RequestRestart(true); Next(); return;
                }
                if (step == 8 && elapsed > 1)
                {
                    Check(cp.Checkpoint == 0 && CampaignProgress.SavedCheckpoint(2) == 0, "replay from beginning resets persistent active checkpoint");
                    Check(!sluice.Open && Object.FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None).All(g => !g.Passed), "replay closes sluice and clears gates");
                    Check(!boat.HasCoopCrew && dad.GetComponent<CharacterController>().enabled && child.GetComponent<CharacterController>().enabled, "replay dismounts and restores both character controllers");
                    foreach (var gate in Object.FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None)) gate.RestoreCheckpoint(true);
                    sluice.RestoreCheckpoint(true); Next(); return;
                }
                if (step == 9 && elapsed > 1)
                {
                    Check(CampaignProgress.SavedCheckpoint(2) == 2, "resume fixture saved before ending session");
                    NextPhase(2);
                }
            }
            catch (Exception ex) { File.AppendAllText(Report, "FAIL phase " + phase + " step " + step + ": " + ex + "\n"); Debug.LogException(ex); Finish(); }
        }
        static void NextPhase(int phase) { step = 0; SessionState.SetInt(Phase, phase); EditorApplication.isPlaying = false; }
        static void Finish()
        {
            foreach (var key in Keys)
                if (SessionState.GetBool(key + ".RecoveryQA.Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(key + ".RecoveryQA.Value", 0)); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Lobby3DManager.AmIDad = SessionState.GetBool("HMH.RecoveryQA.Role", true);
            SessionState.SetBool(Active, false); SessionState.SetBool("HMH.RecoveryQA.RestoreScene", true);
            EditorApplication.isPlaying = false;
        }
    }
}
