using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Blocks.Gameplay.Core.LevelTwoAuthoring
{
    [InitializeOnLoad]
    public static class LevelTwoTransitionValidation
    {
        private const string Active = "LVL2.TransitionQA.Active";
        private const string Report = "AI_Reports/tests/LVL2/transition-playmode.txt";
        private static int step;
        private static double since, loadStarted;
        private static Vector3 before;
        private static ulong oldPlayer;
        static LevelTwoTransitionValidation() { EditorApplication.update += Tick; }
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Run with a saved scene outside Play Mode.");
            Directory.CreateDirectory("AI_Reports/tests/LVL2");
            File.WriteAllText(Report, "Castle Continue -> PlayScene2, local host regression.\n");
            foreach (var key in new[] { "HMH.Level1.Completed", "HMH.Level1.Checkpoint" })
            { SessionState.SetBool(key + ".TransitionQA.Exists", PlayerPrefs.HasKey(key)); SessionState.SetInt(key + ".TransitionQA.Value", PlayerPrefs.GetInt(key)); }
            EditorSceneManager.OpenScene("Assets/Core/Scenes/PlayScene3.unity", OpenSceneMode.Single);
            SessionState.SetBool(Active, true); step = 0; since = 0;
            EditorApplication.isPlaying = true;
        }
        private static void Check(bool value, string label)
        { if (!value) throw new Exception(label); File.AppendAllText(Report, "PASS " + label + "\n"); }
        private static void Next() { step++; since = EditorApplication.timeSinceStartup; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (since == 0) since = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - since;
            try
            {
                if (elapsed > 90) throw new Exception("Transition timeout at step " + step + " scene=" + SceneManager.GetActiveScene().name);
                var network = NetworkManager.Singleton;
                if (step == 0)
                {
                    if (network == null) return;
                    var boss = UnityEngine.Object.FindAnyObjectByType<ChaseBossController>();
                    if (boss != null) boss.startGrace = 10000;
                    var transport = network.GetComponent<UnityTransport>();
                    transport.ConnectionData.Address = "127.0.0.1"; transport.ConnectionData.Port = 7793;
                    Check(network.StartHost(), "host starts in PlayScene3"); Next(); return;
                }
                if (step == 1 && elapsed > 2)
                {
                    var climax = UnityEngine.Object.FindAnyObjectByType<CastleGateClimaxManager>();
                    Check(climax.nextLevelScene == "PlayScene2", "serialized Continue destination is PlayScene2");
                    Check(network.LocalClient.PlayerObject != null, "source player exists");
                    oldPlayer = network.LocalClient.PlayerObject.NetworkObjectId;
                    // Only prepare the victory state; exercise the real Continue button handler.
                    var phase = (NetworkVariable<int>)typeof(LevelOneObstacle).GetField("phase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(climax);
                    phase.Value = (int)GateClimaxPhase.Victory;
                    loadStarted = EditorApplication.timeSinceStartup;
                    climax.ContinueToNextLevel(); Next(); return;
                }
                if (step == 2 && SceneManager.GetActiveScene().name == "PlayScene2" && elapsed > 5)
                {
                    Check(network != null && network.IsListening && network.IsHost, "host session survives Continue");
                    var player = network.LocalClient.PlayerObject;
                    Check(player != null && player.IsSpawned && player.NetworkObjectId != oldPlayer, "fresh player spawned in destination");
                    File.AppendAllText(Report, "Arrival after " + (EditorApplication.timeSinceStartup - loadStarted).ToString("F2") + " s; player=" + player.transform.position + "\n");
                    Check(GameManager.Instance != null, "destination GameManager exists");
                    var expected = GameManager.Instance.GetSpawnPosition(network.LocalClientId);
                    Check(Vector3.Distance(player.transform.position, expected) < 4 && expected.z > 20 && expected.z < 40, "player is at river start dock");
                    var actor = player.GetComponent<CorePlayerManager>();
                    foreach (var ui in UnityEngine.Object.FindObjectsByType<UIDocument>())
                    {
                        File.AppendAllText(Report, "UI " + ui.name + " display=" + ui.rootVisualElement.resolvedStyle.display + "\n");
                        if (ui.name == "LoadingScreenUI" || ui.name == "WaitRoomUI")
                            Check(ui.rootVisualElement.resolvedStyle.display == DisplayStyle.None, ui.name + " no longer blocks gameplay");
                    }
                    Check(UnityEngine.Object.FindObjectsByType<NetworkManager>().Length == 1, "only the live NetworkManager remains");
                    Check(actor.CoreMovement.enabled && actor.CoreMovement.IsMovementEnabled, "movement unlocked after continuation");
                    Check(actor.CoreInput.enabled, "gameplay input enabled after continuation");
                    var camera = player.GetComponent<CoreCameraController>();
                    Check(camera != null && camera.enabled && camera.ActiveCameraMode != null, "local gameplay camera active");
                    before = player.transform.position;
                    actor.CoreMovement.SetMoveInput(Vector2.up);
                    Next(); return;
                }
                if (step == 3 && elapsed > 0.3)
                {
                    var actor = network.LocalClient.PlayerObject.GetComponent<CorePlayerManager>();
                    // CoreInput normally overwrites synthetic input; sample enabled state and camera
                    // separately from a physical controller displacement.
                    var cc = actor.GetComponent<CharacterController>();
                    var start = actor.transform.position;
                    cc.Move(Vector3.forward * 0.3f);
                    Check((actor.transform.position - start).sqrMagnitude > 0.01f, "spawn controller can move on dock");
                    actor.CoreMovement.SetMoveInput(Vector2.zero);
                    File.AppendAllText(Report, "LIMITATION: local host; independent remote-client loading not exercised.\n");
                    Finish();
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL " + e + "\n"); Debug.LogException(e); Finish(); }
        }
        private static void Finish()
        {
            foreach (var key in new[] { "HMH.Level1.Completed", "HMH.Level1.Checkpoint" })
                if (SessionState.GetBool(key + ".TransitionQA.Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(key + ".TransitionQA.Value", 0)); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save(); SessionState.SetBool(Active, false); EditorApplication.isPlaying = false;
        }
    }
}
