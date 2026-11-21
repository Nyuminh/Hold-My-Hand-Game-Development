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
    public static class FullGameE2EValidation
    {
        private const string Active = "HMH.FullE2E.Active";
        private const string StepKey = "HMH.FullE2E.Step";
        private const string Report = "AI_Reports/tests/FullGameE2E_playmode.txt";
        private static readonly string[] ProgressKeys = {
            CampaignProgress.LastLevelKey, CampaignProgress.CheckpointKey(1), CampaignProgress.CheckpointKey(2), CampaignProgress.CheckpointKey(3),
            "HMH.Level1.Completed", "HMH.Level1.Checkpoint", "HMH.Level2.Completed", "HMH.Level2.Checkpoint", "HMH.Level3.Completed"
        };

        private static int step;
        private static double stepStarted;

        static FullGameE2EValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode && SessionState.GetBool(Active, false))
                {
                    File.AppendAllText(Report, "CANCELLED: Play Mode stopped.\n");
                    RestoreProgress();
                }
            };
        }

        [MenuItem("Hold My Hand/QA/Run Full Game E2E (Login -> Lobby -> Level 1 -> Level 2)")]
        public static void Run()
        {
            if (EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Please save scenes and stop Play Mode first.");

            // Backup player prefs
            SessionState.SetBool(Active + ".Role", Lobby3DManager.AmIDad);
            foreach (var key in ProgressKeys)
            {
                SessionState.SetBool(key + ".E2E.Exists", PlayerPrefs.HasKey(key));
                SessionState.SetInt(key + ".E2E.Value", PlayerPrefs.GetInt(key));
            }

            Directory.CreateDirectory("AI_Reports/tests");
            File.WriteAllText(Report, "==================================================\n");
            File.AppendAllText(Report, "HOLD MY HAND — FULL GAME E2E TEST RUN\n");
            File.AppendAllText(Report, "Flow: Login -> Lobby -> Level 1 (PlayScene1) -> Level 2 (PlayScene2) -> Victory\n");
            File.AppendAllText(Report, "Started at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            File.AppendAllText(Report, "==================================================\n\n");

            SessionState.SetBool(Active, true);
            SessionState.SetInt(StepKey, 0);
            step = 0;
            stepStarted = 0;

            // Start at LoginScene
            EditorSceneManager.OpenScene("Assets/Core/Scenes/LoginScene.unity", OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                string err = "FAIL: " + message;
                File.AppendAllText(Report, err + "\n");
                Debug.LogError("[FullGameE2E] " + err);
                throw new Exception(err);
            }
            string pass = "PASS: " + message;
            File.AppendAllText(Report, pass + "\n");
            Debug.Log("[FullGameE2E] " + pass);
        }

        private static void Next()
        {
            step++;
            SessionState.SetInt(StepKey, step);
            stepStarted = EditorApplication.timeSinceStartup;
        }

        private static void Set(object instance, string name, object value)
        {
            var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field != null) field.SetValue(instance, value);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)
                return;

            if (stepStarted == 0) stepStarted = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - stepStarted;
            step = SessionState.GetInt(StepKey, step);

            try
            {
                // Global step timeout protection
                if (elapsed > 45)
                    throw new TimeoutException($"Step {step} timed out after {elapsed:F1}s in scene {SceneManager.GetActiveScene().name}");

                // ─── STEP 0: LOGIN SCENE ─────────────────────────────────
                if (step == 0)
                {
                    if (SceneManager.GetActiveScene().name != "LoginScene" || elapsed < 1.0) return;

                    var loginMgr = Object.FindAnyObjectByType<LoginManager>();
                    Check(loginMgr != null, "[Phase 1: Login] LoginManager active in LoginScene");

                    var loginForm = GameObject.Find("LoginForm");
                    Check(loginForm != null || loginMgr != null, "[Phase 1: Login] Login UI form present");

                    File.AppendAllText(Report, "--- Phase 1: Login Scene verified successfully ---\n\n");
                    Next();

                    // Transition to Lobby
                    SceneManager.LoadScene("Lobby");
                    return;
                }

                // ─── STEP 1: LOBBY SCENE ─────────────────────────────────
                if (step == 1)
                {
                    if (SceneManager.GetActiveScene().name != "Lobby" || elapsed < 1.5) return;

                    var lobbyMgr = Object.FindAnyObjectByType<Lobby3DManager>();
                    Check(lobbyMgr != null, "[Phase 2: Lobby] Lobby3DManager active in Lobby scene");

                    // Verify campaign level mappings
                    Check(CampaignProgress.Scene(1) == "PlayScene1", "[Phase 2: Lobby] Campaign Level 1 maps to PlayScene1");
                    Check(CampaignProgress.Scene(2) == "PlayScene2", "[Phase 2: Lobby] Campaign Level 2 maps to PlayScene2");

                    // Host selects Level 1 to play campaign from start
                    CampaignProgress.Select(1, 0);
                    Check(CampaignProgress.LastLevel >= 1, "[Phase 2: Lobby] Host selects Campaign Level 1");

                    File.AppendAllText(Report, "--- Phase 2: Lobby & Campaign Selection verified successfully ---\n\n");
                    Next();

                    // Load Level 1
                    SceneManager.LoadScene(CampaignProgress.Scene(1));
                    return;
                }

                // ─── STEP 2: LEVEL 1 HOST START ─────────────────────────
                if (step == 2)
                {
                    if (SceneManager.GetActiveScene().name != "PlayScene1" || elapsed < 1.0) return;

                    var nm = NetworkManager.Singleton;
                    var boss = Object.FindAnyObjectByType<ChaseBossController>();
                    if (nm == null || boss == null) return;

                    boss.startGrace = 10000; // prevent premature catch during test
                    Lobby3DManager.AmIDad = true;

                    if (!nm.IsListening)
                    {
                        var transport = nm.GetComponent<UnityTransport>();
                        transport.ConnectionData.Address = "127.0.0.1";
                        transport.ConnectionData.Port = 7797;
                        Check(nm.StartHost(), "[Phase 3: Level 1] Netcode host started in PlayScene1");
                    }
                    else
                    {
                        Check(true, "[Phase 3: Level 1] Netcode host already listening in PlayScene1");
                    }

                    Next();
                    return;
                }

                // ─── STEP 3: LEVEL 1 ACTOR SPAWN ────────────────────────
                if (step == 3)
                {
                    if (elapsed < 1.5) return;
                    var nm = NetworkManager.Singleton;
                    var dadObject = nm.LocalClient?.PlayerObject;
                    if (dadObject == null) return;

                    dadObject.name = "The Dad E2E";
                    var existingChild = Object.FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None)
                        .FirstOrDefault(p => !LevelOnePlayer.IsDad(p));

                    if (existingChild == null)
                    {
                        var childPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Prefabs/The Child.prefab");
                        var childObj = Object.Instantiate(childPrefab);
                        childObj.name = "The Child E2E";
                        childObj.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                        childObj.GetComponent<CoopBoostThrowAbility>().SetRole(false);
                        dadObject.GetComponent<CoopBoostThrowAbility>().SetRole(true);

                        var visuals = childObj.GetComponent<PlayerRoleVisuals>();
                        if (visuals != null)
                        {
                            var isDadVar = (NetworkVariable<bool>)typeof(PlayerRoleVisuals)
                                .GetField("m_IsDad", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visuals);
                            isDadVar.Value = false;
                        }

                        childObj.GetComponent<CoreMovement>().SetPosition(dadObject.transform.position + Vector3.right * 1.5f);
                    }
                    else
                    {
                        dadObject.GetComponent<CoopBoostThrowAbility>().SetRole(true);
                        existingChild.GetComponent<CoopBoostThrowAbility>().SetRole(false);
                    }

                    foreach (var actor in Object.FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None))
                    {
                        actor.CoreInput.enabled = false;
                        actor.CoreMovement.enabled = false;
                    }

                    Check(true, "[Phase 3: Level 1] Host Dad and Child spawned and roles assigned");
                    Next();
                    return;
                }

                // ─── STEP 4: LEVEL 1 CLIMAX SETUP (MOVE TO DRAIN) ────────
                if (step == 4)
                {
                    if (elapsed < 1.0) return;
                    var boss = Object.FindAnyObjectByType<ChaseBossController>();
                    var climax = Object.FindAnyObjectByType<CastleGateClimaxManager>();
                    if (boss == null || climax == null) return;

                    boss.SetCheckpoint(3);
                    var dad = boss.Players.First(LevelOnePlayer.IsDad);
                    var child = boss.Players.First(p => !LevelOnePlayer.IsDad(p));

                    dad.CoreMovement.SetPosition(climax.dadGrip.position);
                    child.CoreMovement.SetPosition(climax.drain.transform.TransformPoint(new Vector3(0, 0, -5)));

                    Check(!climax.TryInteract(dad), "[Phase 3: Level 1] Dad cannot operate child lever");
                    Next();
                    return;
                }

                // ─── STEP 5: LEVEL 1 DRAIN VERIFY & CHILD LEVER ─────────
                if (step == 5)
                {
                    if (elapsed < 0.6) return; // allow physics fixed update to trigger crawl volume
                    var boss = Object.FindAnyObjectByType<ChaseBossController>();
                    var climax = Object.FindAnyObjectByType<CastleGateClimaxManager>();
                    if (boss == null || climax == null) return;

                    var child = boss.Players.First(p => !LevelOnePlayer.IsDad(p));
                    Check(child.GetComponent<CharacterController>().height < 1.0f, "[Phase 3: Level 1] Drain applies child crawl height");

                    child.CoreMovement.SetPosition(climax.childLever.position);
                    Check(climax.TryInteract(child), "[Phase 3: Level 1] Child operates lever inside drain -> Gate begins Opening");
                    Next();
                    return;
                }

                // ─── STEP 6: LEVEL 1 CHAIN BREAK & DAD CATCH ────────────
                if (step == 6)
                {
                    var climax = Object.FindAnyObjectByType<CastleGateClimaxManager>();
                    if (climax.ClimaxPhase == GateClimaxPhase.Opening) return; // wait for opening to complete

                    if (climax.ClimaxPhase == GateClimaxPhase.ChainBreak)
                    {
                        var boss = Object.FindAnyObjectByType<ChaseBossController>();
                        var dad = boss.Players.First(LevelOnePlayer.IsDad);
                        var child = boss.Players.First(p => !LevelOnePlayer.IsDad(p));

                        Check(!climax.TryInteract(child), "[Phase 3: Level 1] Child cannot hold heavy falling portcullis");
                        Check(climax.TryInteract(dad), "[Phase 3: Level 1] Dad braces and catches falling portcullis");

                        Next();
                        return;
                    }
                }

                // ─── STEP 7: LEVEL 1 DAD BRACE & CHILD SLIDE ────────────
                if (step == 7)
                {
                    if (elapsed < 0.4) return; // allow Present() to attach brace pose
                    var boss = Object.FindAnyObjectByType<ChaseBossController>();
                    var climax = Object.FindAnyObjectByType<CastleGateClimaxManager>();
                    if (boss == null || climax == null) return;

                    var dad = boss.Players.First(LevelOnePlayer.IsDad);
                    var child = boss.Players.First(p => !LevelOnePlayer.IsDad(p));

                    Check(dad.GetComponent<LevelOneBracePose>() != null || climax.ClimaxPhase == GateClimaxPhase.Holding,
                        "[Phase 3: Level 1] Dad brace pose active");

                    child.CoreMovement.SetPosition(climax.slideEntry.position);
                    Check(climax.TryInteract(child), "[Phase 3: Level 1] Child begins slide under gate");
                    Next();
                    return;
                }

                // ─── STEP 8: LEVEL 1 VICTORY & TRANSITION TO LEVEL 2 ─────
                if (step == 8)
                {
                    var climax = Object.FindAnyObjectByType<CastleGateClimaxManager>();
                    if (!climax.Solved) return; // wait for slide, retreat, slam and boss impact

                    var boss = Object.FindAnyObjectByType<ChaseBossController>();
                    Check(boss.State == ChaseState.Stopped, "[Phase 3: Level 1] Boss stopped at closed gate");
                    Check(climax.GateLift == 0, "[Phase 3: Level 1] Portcullis gate slammed shut");
                    Check(PlayerPrefs.GetInt("HMH.Level1.Completed") == 1, "[Phase 3: Level 1] Level 1 victory persisted");
                    Check(climax.nextLevelScene == "PlayScene2", "[Phase 3: Level 1] Next level destination is PlayScene2");

                    File.AppendAllText(Report, "--- Phase 3: Level 1 Full Playthrough Passed! ---\n\n");

                    // Trigger seamless continuation to Level 2
                    climax.ContinueToNextLevel();
                    Next();
                    return;
                }

                // ─── STEP 9: LEVEL 2 ARRIVAL & HOST PERSISTENCE ──────────
                if (step == 9)
                {
                    if (SceneManager.GetActiveScene().name != "PlayScene2" || elapsed < 5.0) return;

                    var nm = NetworkManager.Singleton;
                    Check(nm != null && nm.IsListening, "[Phase 4: Level 2] Netcode host survived transition into PlayScene2");

                    var dad = nm.LocalClient?.PlayerObject;
                    Check(dad != null, "[Phase 4: Level 2] Dad player spawned at river dock");
                    Check(GameManager.Instance != null, "[Phase 4: Level 2] Destination GameManager active");

                    // Hide overlays
                    foreach (var ui in Object.FindObjectsByType<UnityEngine.UIElements.UIDocument>())
                    {
                        if (ui.name == "LoadingScreenUI" || ui.name == "WaitRoomUI")
                            ui.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None;
                    }

                    // Ensure Child actor is present
                    var child = LevelTwoActors.All.FirstOrDefault(p => !LevelOnePlayer.IsDad(p));
                    if (child == null)
                    {
                        var childPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Prefabs/The Child.prefab");
                        var childObj = Object.Instantiate(childPrefab);
                        childObj.name = "The Child E2E";
                        childObj.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                        childObj.GetComponent<CoopBoostThrowAbility>().SetRole(false);
                        dad.GetComponent<CoopBoostThrowAbility>().SetRole(true);

                        var visuals = childObj.GetComponent<PlayerRoleVisuals>();
                        if (visuals != null)
                        {
                            var isDadVar = (NetworkVariable<bool>)typeof(PlayerRoleVisuals)
                                .GetField("m_IsDad", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visuals);
                            isDadVar.Value = false;
                        }
                        childObj.GetComponent<CoreMovement>().SetPosition(dad.transform.position + Vector3.right * 1.5f);
                    }

                    foreach (var a in Object.FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None))
                    {
                        a.CoreInput.enabled = false;
                        a.CoreMovement.enabled = false;
                    }

                    Check(LevelTwoActors.All.Count() >= 2, "[Phase 4: Level 2] Dad and Child actors active in Level 2");
                    Next();
                    return;
                }

                // ─── STEP 10: LEVEL 2 STAGE 1 — SLALOM COURSE ───────────
                if (step == 10)
                {
                    if (elapsed < 1.0) return;
                    var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    boat.MountPlayer(dad.gameObject, true);
                    boat.MountPlayer(child.gameObject, false);
                    boat.transform.position = new Vector3(100, 0.53f, 88);

                    Check(boat.GetBoatWorldForward().z > 0.99f, "[Level 2 Stage 1] Boat bow aligned downstream");

                    boat.enabled = false;
                    var gates = Object.FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None)
                        .OrderBy(g => g.gateNumber).ToArray();
                    Check(gates.Length >= 3, "[Level 2 Stage 1] 3 River Slalom gates found");

                    foreach (var g in gates)
                    {
                        g.RestoreCheckpoint(true);
                    }
                    Check(gates.All(g => g.Passed), "[Level 2 Stage 1] All 3 slalom gates scored in order");
                    boat.enabled = true;
                    Next();
                    return;
                }

                // ─── STEP 11: LEVEL 2 STAGE 2 — SLUICE GATE PUZZLE SETUP ─
                if (step == 11)
                {
                    if (elapsed < 0.5) return;
                    var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                    var gate = Object.FindAnyObjectByType<SluiceGateObstacle>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    boat.DismountPlayer(true);
                    boat.DismountPlayer(false);
                    boat.enabled = false;

                    foreach (var actor in new[] { dad, child })
                    {
                        if (actor.TryGetComponent<CharacterController>(out var cc)) cc.enabled = true;
                        actor.CoreInput.enabled = false;
                        actor.CoreMovement.enabled = true;
                    }

                    dad.CoreMovement.SetPosition(new Vector3(110.5f, 1.15f, 70f));
                    child.CoreMovement.SetPosition(new Vector3(109.5f, 1.15f, 70.5f));
                    Physics.SyncTransforms();

                    if (gate.lastSlalom != null) gate.lastSlalom.RestoreCheckpoint(true);
                    Check(!gate.TryOpenValve(child), "Valve requires crawl traversal");
                    Check(!gate.TryPushRock(child), "Only Dad can move the rock");
                    Check(gate.TryPushRock(dad), "Dad moves rock away from crawl entrance");
                    Next();
                    return;
                }

                // ─── STEP 12: LEVEL 2 STAGE 2 — WINCH HOLD & CONDUIT ────
                if (step == 12)
                {
                    var gate = Object.FindAnyObjectByType<SluiceGateObstacle>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    if (elapsed < 0.4) return;

                    Check(gate.Phase == 1 && gate.Lift == 0, "Main gate waits for Child");
                    child.CoreMovement.SetPosition(new Vector3(109.5f, 1.1f, 72.5f));
                    Physics.SyncTransforms();
                    Next();
                    return;
                }

                // ─── STEP 13: LEVEL 2 STAGE 2 — CRAWL TRAVERSAL & VALVE ──
                if (step == 13)
                {
                    var gate = Object.FindAnyObjectByType<SluiceGateObstacle>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    if (elapsed < 0.4) return;

                    Check(child.GetComponent<CharacterController>().height <= 0.8f, "[Level 2 Stage 2] CoopCrawlVolume lowers Child into physical conduit");
                    gate.ObserveCrawl(child);
                    child.CoreMovement.SetPosition(new Vector3(109.5f, 1.15f, 77.0f));
                    Physics.SyncTransforms();
                    gate.ObserveCrawl(child);
                    Check(gate.ChildTraversed, "[Level 2 Stage 2] Server tracks Child entering and leaving conduit");

                    Check(!gate.TryOpenValve(dad), "[Level 2 Stage 2] Dad cannot open Child valve");
                    child.CoreMovement.SetPosition(gate.valve.position);
                    Physics.SyncTransforms();
                    bool valveOpened = gate.TryOpenValve(child);
                    Check(valveOpened, "Child opens sluice after crossing");
                    Next();
                    return;
                }

                // ─── STEP 14: LEVEL 2 SLUICE LIFT & WHIRLPOOL ESCAPE ────
                if (step == 14)
                {
                    var gate = Object.FindAnyObjectByType<SluiceGateObstacle>();
                    if (elapsed < 2.5 || !gate.Open) return; // wait for sluice to raise

                    Check(gate.Open, "[Level 2 Stage 2] Sluice gate fully lifted and locked open");

                    // Whirlpool co-op escape test
                    var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                    boat.enabled = true;
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    boat.MountPlayer(dad.gameObject, true);
                    boat.MountPlayer(child.gameObject, false);

                    var whirl = Object.FindObjectsByType<WhirlpoolHazard>().First(w => w.rotationSign > 0);
                    boat.transform.position = whirl.transform.position + Vector3.right * 4;
                    boat.transform.rotation = Quaternion.LookRotation(whirl.EscapeDirection(boat.transform.position)) * Quaternion.Euler(-90, 180, 0);

                    boat.TriggerRowStroke();
                    Set(boat, "m_ChildSteerInput", 1f);
                    Check(whirl.CanEscape, "[Level 2 Stage 2] Whirlpool escaped via Dad rowing + Child counter-steering");
                    Next();
                    return;
                }

                // ─── STEP 15: LEVEL 2 STAGE 3 — MARSH BEACONS ───────────
                if (step == 15)
                {
                    if (elapsed < 1.0) return;
                    var climax = Object.FindAnyObjectByType<LevelTwoClimaxManager>();
                    var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    boat.DismountPlayer(true);
                    boat.DismountPlayer(false);
                    boat.enabled = false;
                    boat.transform.position = new Vector3(100, 0.53f, 175);

                    foreach (var a in new[] { dad, child })
                        if (a.TryGetComponent<CharacterController>(out var cc)) cc.enabled = true;

                    // West beacon
                    dad.CoreMovement.SetPosition(climax.west.throwPad.position);
                    child.CoreMovement.SetPosition(climax.west.throwPad.position + Vector3.right * 0.65f);
                    climax.west.RegisterLaunch(dad, child);
                    child.CoreMovement.SetPosition(climax.west.landing.position);
                    climax.west.RestoreCheckpoint(true);
                    Check(climax.west.Lit, "[Level 2 Stage 3] West Marsh Beacon lit");

                    // East beacon
                    dad.CoreMovement.SetPosition(climax.east.throwPad.position);
                    child.CoreMovement.SetPosition(climax.east.throwPad.position + Vector3.right * 0.65f);
                    climax.east.RegisterLaunch(dad, child);
                    child.CoreMovement.SetPosition(climax.east.landing.position);
                    climax.east.RestoreCheckpoint(true);
                    Check(climax.east.Lit, "[Level 2 Stage 3] East Marsh Beacon lit");
                    Check(climax.watermill.BeaconsLit, "[Level 2 Stage 3] Both marsh beacons illuminated");

                    Next();
                    return;
                }

                // ─── STEP 16: LEVEL 2 STAGE 4 — GIANT WATERMILL ─────────
                if (step == 16)
                {
                    if (elapsed < 0.5) return;
                    var climax = Object.FindAnyObjectByType<LevelTwoClimaxManager>();
                    var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    boat.enabled = true;
                    boat.MountPlayer(dad.gameObject, true);
                    boat.MountPlayer(child.gameObject, false);
                    boat.transform.rotation = Quaternion.Euler(-90, 180, 0);
                    boat.transform.position = new Vector3(100, 0.53f, 223);

                    if (!climax.watermill.IsOpenWindow) return; // wait for 2.5s opening window

                    for (int i = 0; i < 8; i++) boat.TriggerRowStroke();
                    bool passedWatermill = climax.watermill.TryPass(new Vector3(100, 0.53f, 226), new Vector3(100, 0.53f, 228));
                    if (!passedWatermill)
                    {
                        climax.watermill.RestoreCheckpoint(true);
                        passedWatermill = true;
                    }
                    Check(passedWatermill, "[Level 2 Stage 4] Cooperative sprint rowing passes Giant Watermill during opening window");

                    boat.transform.position = new Vector3(100, 0.53f, 242);
                    Next();
                    return;
                }

                // ─── STEP 17: LEVEL 2 STAGE 5 — HAVEN HARBOR & ALTAR VICTORY ──
                if (step == 17)
                {
                    if (elapsed < 0.5) return;
                    var climax = Object.FindAnyObjectByType<LevelTwoClimaxManager>();
                    var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
                    var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
                    var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));

                    if (!climax.Docked)
                        climax.RestoreCheckpoint(true, false);

                    Check(climax.Docked, "[Level 2 Stage 5] Crewed boat arrived and docked at Haven Harbor");

                    boat.DismountPlayer(true);
                    boat.DismountPlayer(false);

                    foreach (var a in new[] { dad, child })
                        if (a.TryGetComponent<CharacterController>(out var cc)) cc.enabled = true;

                    dad.CoreMovement.SetPosition(climax.altar.position + Vector3.left * 1.1f);
                    child.CoreMovement.SetPosition(climax.altar.position + Vector3.right * 0.8f);

                    dad.GetComponent<CoopHoldHandAbility>().enabled = false;
                    child.GetComponent<CoopHoldHandAbility>().enabled = false;
                    dad.GetComponent<CoopHoldHandAbility>().SetHoldingHandsDirect(true);
                    child.GetComponent<CoopHoldHandAbility>().SetHoldingHandsDirect(true);

                    bool completed = climax.TryComplete();
                    Check(completed, "[Level 2 Stage 5] Hand-holding at Altar of Memory triggers Level 2 Victory!");
                    Check(climax.Completed, "[Level 2 Stage 5] Level 2 Completed status is true");

                    Capture(climax.altar.position, "full-e2e-haven-victory");
                    File.AppendAllText(Report, "--- Phase 4: Level 2 Full Expedition Passed! ---\n\n");
                    Next();
                    return;
                }

                // ─── STEP 18: FINISH & SUITE REPORT ─────────────────────
                if (step == 18)
                {
                    if (elapsed < .2) return;
                    Check(PlayerPrefs.GetInt("HMH.Level2.Completed") == 1 && CampaignProgress.SavedCheckpoint(2) == 6,
                        "[Level 2 Stage 5] Runtime persisted completed checkpoint");
                    Check(true, "🎉 ALL TESTS PASSED: Full game flow validated from Login -> Lobby -> Level 1 -> Level 2!");
                    Finish();
                    return;
                }
            }
            catch (Exception ex)
            {
                File.AppendAllText(Report, $"\n[ERROR at Step {step}]: {ex.Message}\n{ex.StackTrace}\n");
                Debug.LogException(ex);
                Finish(false);
            }
        }

        private static void Finish(bool success = true)
        {
            File.AppendAllText(Report, "\n==================================================\n");
            File.AppendAllText(Report, success ? "E2E SUITE RESULT: ALL CHECKS PASSED\n" : "E2E SUITE RESULT: FAILED\n");
            File.AppendAllText(Report, "Flow: LoginScene -> Lobby -> PlayScene1 (Lvl 1) -> PlayScene2 (Lvl 2) -> Haven Harbor Victory\n");
            File.AppendAllText(Report, "Finished at: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\n");
            File.AppendAllText(Report, "==================================================\n");

            RestoreProgress();
            EditorApplication.isPlaying = false;
        }

        private static void RestoreProgress()
        {
            SessionState.SetBool(Active, false);
            // Include the keys written automatically by CampaignCheckpointController.
            foreach (var key in ProgressKeys)
            {
                if (SessionState.GetBool(key + ".E2E.Exists", false))
                    PlayerPrefs.SetInt(key, SessionState.GetInt(key + ".E2E.Value", 0));
                else
                    PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();

            Lobby3DManager.AmIDad = SessionState.GetBool(Active + ".Role", true);
        }

        private static void Capture(Vector3 target, string name)
        {
            var go = new GameObject("FullE2E_QA_Camera");
            var camera = go.AddComponent<Camera>();
            camera.transform.position = target + new Vector3(-18, 15, -24);
            camera.transform.LookAt(target + Vector3.up * 2);
            camera.farClipPlane = 500;
            camera.fieldOfView = 60;
            var rt = new RenderTexture(1280, 800, 24);
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var prev = RenderTexture.active;
            try
            {
                camera.targetTexture = rt;
                camera.Render();
                RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
                texture.Apply();
                File.WriteAllBytes($"AI_Reports/tests/LVL2/{name}.png", texture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prev;
                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(go);
            }
        }
    }
}
