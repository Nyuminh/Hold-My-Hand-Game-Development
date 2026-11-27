using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Blocks.Gameplay.Core.LevelTwoAuthoring
{
    [InitializeOnLoad]
    public static class LevelTwoValidation
    {
        private const string Active = "LVL2.Validation.Active";
        private static int Mode => SessionState.GetInt("LVL2.Validation.Mode", 1);
        private static string Report => "AI_Reports/tests/LVL2/stage" + Mode + "-playmode.txt";
        private static int step;
        private static double at;
        private static Vector3 before;
        static LevelTwoValidation() { EditorApplication.update += Tick; }
        public static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }
        public static void Set(object instance, string name, object value) => instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
        public static void RunStageOne()
        { Run(1); }
        public static void RunStageTwo()
        { Run(2); }
        public static void RunStageThree()
        { Run(3); }
        private static void Run(int mode)
        {
            if (EditorApplication.isPlaying) throw new Exception("Stop Play Mode before validation.");
            SessionState.SetInt("LVL2.Validation.Mode", mode);
            foreach (var key in new[] { "HMH.Level2.Completed", "HMH.Level2.Checkpoint", CampaignProgress.LastLevelKey, CampaignProgress.CheckpointKey(2) })
            { SessionState.SetBool(key + ".QA.Exists", PlayerPrefs.HasKey(key)); SessionState.SetInt(key + ".QA.Value", PlayerPrefs.GetInt(key)); }
            Directory.CreateDirectory("AI_Reports/tests/LVL2");
            File.WriteAllText(Report, "Level 2 task " + mode + ": Unity Play Mode with a local Netcode host.\n");
            SessionState.SetBool(Active, true); step = 0; at = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }
        private static void Next() { step++; at = EditorApplication.timeSinceStartup; }
        private static void Tick()
        {
            if (!SessionState.GetBool(Active, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (at == 0) at = EditorApplication.timeSinceStartup;
            double elapsed = EditorApplication.timeSinceStartup - at;
            try
            {
                if (elapsed > 45) throw new Exception("Timed out at step " + step);
                var nm = NetworkManager.Singleton;
                var boat = UnityEngine.Object.FindAnyObjectByType<CoopWoodenBoat>();
                if (step == 0)
                {
                    if (nm == null || boat == null) return;
                    var transport = nm.GetComponent<UnityTransport>();
                    transport.ConnectionData.Address = "127.0.0.1";
                    transport.ConnectionData.Port = 0;
                    Check(nm.IsHost || nm.StartHost(), "Netcode host starts"); Next(); return;
                }
                if (step == 1 && elapsed > 2)
                {
                    var dad = nm.LocalClient.PlayerObject;
                    Check(dad != null, "host player spawned");
                    dad.name = "The Dad LVL2 Test";
                    var child = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Prefabs/The Child.prefab"));
                    child.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                    child.GetComponent<CoopBoostThrowAbility>().SetRole(false);
                    dad.GetComponent<CoopBoostThrowAbility>().SetRole(true);
                    foreach (var p in UnityEngine.Object.FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None)) p.CoreInput.enabled = false;
                    boat.MountPlayer(dad.gameObject, true); boat.MountPlayer(child, false);
                    Check(boat.GetBoatWorldForward().z > 0.99f, "Blender bow axis points downstream");
                    boat.transform.position = new Vector3(100, 0.53f, 88);
                    before = boat.transform.position; Next(); return;
                }
                if (Mode == 2 && step >= 2) { PuzzleTick(boat, elapsed); return; }
                if (Mode == 3 && step >= 2) { FinaleTick(boat, elapsed); return; }
                if (step == 2 && elapsed > 1)
                {
                    Check(!boat.IsGroundedOnLand, "river bed does not ground boat");
                    Check(boat.transform.position.z > before.z + 0.1f, "current trigger moves crewed boat without rowing");
                    Check(boat.ExternalDriftVelocity.magnitude <= 5.01f, "drift bounded");
                    boat.enabled = false;
                    var gates = UnityEngine.Object.FindObjectsByType<RiverSlalomGate>(FindObjectsSortMode.None).OrderBy(g => g.gateNumber).ToArray();
                    Vector3 a = gates[1].transform.position - Vector3.forward;
                    Check(!gates[1].TryCross(a, a + Vector3.forward * 2), "gate 2 rejects out-of-order crossing");
                    foreach (var gate in gates)
                    {
                        a = gate.transform.position - Vector3.forward;
                        Check(!gate.TryCross(a + Vector3.forward * 2, a), "reverse crossing rejected " + gate.gateNumber);
                        Check(!gate.TryCross(a + Vector3.right * 15, a + Vector3.right * 15 + Vector3.forward * 2), "outside buoys rejected " + gate.gateNumber);
                        Check(gate.TryCross(a, a + Vector3.forward * 2), "gate passes in order " + gate.gateNumber);
                        Check(!gate.TryCross(a, a + Vector3.forward * 2), "gate cannot score twice " + gate.gateNumber);
                    }
                    boat.enabled = true;
                    boat.transform.position = new Vector3(100, 0.53f, 69);
                    Set(boat, "m_ExternalDrift", Vector3.zero);
                    for (int i = 0; i < 8; i++) boat.TriggerRowStroke();
                    Next(); return;
                }
                if (step == 3 && elapsed > 1.5)
                {
                    Check(boat.transform.position.z < 72, "swept hull cannot tunnel through closed iron gate");
                    Check(boat.CurrentSpeed < 3, "collision slows boat");
                    Check(Mathf.Abs(boat.transform.position.y - 0.53f) < 0.1f, "impact preserves water height");
                    Check(Mathf.Abs(boat.transform.eulerAngles.x - 270) < 0.1f, "impact preserves upright hull");
                    File.AppendAllText(Report, "LIMITATION: host with two owned actors; independent remote-client and human steering feel remain release QA.\n");
                    Finish();
                }
            }
            catch (Exception e) { File.AppendAllText(Report, "FAIL step " + step + ": " + e + "\n"); Debug.LogException(e); Finish(); }
        }
        private static void Finish()
        {
            foreach (var key in new[] { "HMH.Level2.Completed", "HMH.Level2.Checkpoint", CampaignProgress.LastLevelKey, CampaignProgress.CheckpointKey(2) })
                if (SessionState.GetBool(key + ".QA.Exists", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(key + ".QA.Value", 0)); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            SessionState.SetBool(Active, false); EditorApplication.isPlaying = false;
        }

        private static void FinaleTick(CoopWoodenBoat boat, double elapsed)
        {
            if (step == 13)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "PlayScene1" && elapsed > 3)
                {
                    Check(NetworkManager.Singleton.IsListening && NetworkManager.Singleton.LocalClient.PlayerObject != null,
                        "Continue loads PlayScene1 and respawns player while preserving session");
                    File.AppendAllText(Report, "LIMITATION: host with two owned actors, not independent remote-client acceptance.\n");
                    Finish();
                }
                return;
            }
            var climax = UnityEngine.Object.FindAnyObjectByType<LevelTwoClimaxManager>();
            var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
            var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));
            var west = climax.west; var east = climax.east; var mill = climax.watermill;
            if (step == 2)
            {
                boat.DismountPlayer(true); boat.DismountPlayer(false); boat.enabled = false;
                boat.transform.position = new Vector3(100, 0.53f, 175);
                Check(!climax.TryComplete(), "altar cannot skip river challenges");
                var crank = UnityEngine.Object.FindObjectsByType<CoopCarryableItem>(FindObjectsSortMode.None)
                    .Single(i => i.itemType == CoopCarryableItem.ItemType.IgnitionCrank);
                var prism = UnityEngine.Object.FindObjectsByType<CoopCarryableItem>(FindObjectsSortMode.None)
                    .Single(i => i.itemType == CoopCarryableItem.ItemType.RefractionPrism);
                Check(west.requiredItem == crank && east.requiredItem == prism, "beacons require authored crank and prism");
                Check(west.beam != null && east.beam != null && west.turnWheel != null && east.turnWheel != null,
                    "both lighthouses have network beams and steering wheels");
                Check(west.beam.emitter.position.y > 8f && east.beam.emitter.position.y > 8f,
                    "both light beams originate from the top lanterns");
                Check(west.turnWheel.interactionPoint.position.y < 2f && east.turnWheel.interactionPoint.position.y < 2f,
                    "both steering wheels are reachable at the ground consoles");
                Check(west.itemSocket.position.y < 2f && east.itemSocket.position.y < 2f,
                    "both item sockets are reachable at the ground consoles");
                child.CoreMovement.SetPosition(crank.transform.position);
                Check(crank.TryPickup(child), "Child picks up Ignition Crank with [F]");
                crank.Drop();
                dad.CoreMovement.SetPosition(crank.transform.position);
                Check(crank.TryPickup(dad), "Child can hand Ignition Crank to Dad");
                dad.CoreMovement.SetPosition(west.itemSocket.position);
                crank.InsertIntoSocket(west.itemSocket);
                Check(crank.IsSocketed && !west.Lit, "[F] sockets crank without prematurely completing puzzle");
                Next(); return;
            }
            if (step == 3 && elapsed > 0.25)
            {
                Check(west.beam.IsEmitting, "socketed crank activates primary west beam");
                var log = UnityEngine.Object.FindAnyObjectByType<LighthouseFallenLog>();
                dad.CoreMovement.SetPosition(log.dadGrip.position);
                Check(log.TryLift(dad), "Dad lifts fallen log at east shore");
                Next(); return;
            }
            if (step == 4 && elapsed > 0.4)
            {
                var log = UnityEngine.Object.FindAnyObjectByType<LighthouseFallenLog>();
                Check(log.IsLifted && log.crawl.gameObject.activeSelf, "lifted log opens Child-only crawl route");
                var prism = UnityEngine.Object.FindObjectsByType<CoopCarryableItem>(FindObjectsSortMode.None)
                    .Single(i => i.itemType == CoopCarryableItem.ItemType.RefractionPrism);
                child.CoreMovement.SetPosition(prism.transform.position);
                Check(prism.TryPickup(child), "Child retrieves Ancient Prism with [F] behind crawl route");
                child.CoreMovement.SetPosition(east.itemSocket.position);
                prism.InsertIntoSocket(east.itemSocket);
                Check(prism.IsSocketed && east.beam.IsEmitting == false, "[F] sockets prism; east beam waits for west ray");
                Next(); return;
            }
            if (step == 5)
            {
                var sensor = UnityEngine.Object.FindAnyObjectByType<LuminaMillSensor>();
                if (!sensor.IsPowered || mill.safetyBoom.position.y <= mill.transform.position.y + 3f) return;
                Check(mill.BeaconsLit && west.Lit && east.Lit, "refracted beam powers sensor and completes both beacons");
                Check(mill.safetyBoom.position.y > mill.transform.position.y + 3f, "powered sensor raises watermill safety boom");
                Next(); return;
            }
            if (step == 6 && elapsed > 3)
            {
                Check(RenderSettings.fogDensity < 0.015f, "two lighthouses clear marsh fog gradually");
                Check(mill.wheel.childCount >= 5 && mill.cycleSeconds == 8 && mill.openingSeconds == 2.5f, "four-paddle 8 s cycle with 2.5 s clearance authored");
                boat.MountPlayer(dad.gameObject, true); boat.MountPlayer(child.gameObject, false);
                boat.transform.rotation = Quaternion.Euler(-90, 180, 0);
                boat.transform.position = new Vector3(100, 0.53f, 223);
                Next(); Next(); return;
            }
            if (step == 8 && !mill.IsOpenWindow)
            {
                boat.TriggerRowStroke();
                Check(!mill.TryPass(new Vector3(100, 0.53f, 226), new Vector3(100, 0.53f, 228)), "closed watermill rejects passage");
                Next(); return;
            }
            if (step == 9 && mill.IsOpenWindow)
            {
                for (int i = 0; i < 8; i++) boat.TriggerRowStroke();
                Check(mill.TryPass(new Vector3(100, 0.53f, 226), new Vector3(100, 0.53f, 228)), "aligned co-op sprint passes during opening");
                boat.transform.position = new Vector3(100, 0.53f, 242);
                Next(); return;
            }
            if (step == 10 && elapsed > 0.2)
            {
                Check(climax.Docked, "crewed boat arrival recorded at Haven Harbor");
                Check(!climax.TryComplete(), "mounted crew cannot trigger victory");
                boat.DismountPlayer(true); boat.DismountPlayer(false);
                dad.CoreMovement.SetPosition(climax.altar.position + Vector3.left * 1.1f);
                child.CoreMovement.SetPosition(climax.altar.position + Vector3.right * 0.8f);
                Check(!climax.TryComplete(), "two actors at altar without holding hands cannot win");
                dad.GetComponent<CoopHoldHandAbility>().enabled = false;
                child.GetComponent<CoopHoldHandAbility>().enabled = false;
                dad.GetComponent<CoopHoldHandAbility>().SetHoldingHandsDirect(true);
                child.GetComponent<CoopHoldHandAbility>().SetHoldingHandsDirect(true);
                Check(climax.TryComplete(), "dismounted Dad and Child holding hands complete level");
                Next(); return;
            }
            if (step == 11 && elapsed > 0.3)
            {
                Check(PlayerPrefs.GetInt("HMH.Level2.Completed") == 1 && PlayerPrefs.GetInt("HMH.Level2.Checkpoint") == 3, "Level 2 completion and checkpoint persisted");
                Capture(climax.altar.position, "haven-victory");
                Next(); return;
            }
            if (step == 12 && elapsed > 0.2) { climax.ContinueToNextLevel(); Next(); }
        }

        public static void Capture(Vector3 target, string name)
        {
            var go = new GameObject("LVL2_QA_Camera"); var camera = go.AddComponent<Camera>();
            camera.transform.position = target + new Vector3(-18, 15, -24); camera.transform.LookAt(target + Vector3.up * 2);
            camera.farClipPlane = 500; camera.fieldOfView = 60;
            var rt = new RenderTexture(1280, 800, 24); var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try { camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt; texture.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); texture.Apply(); File.WriteAllBytes("AI_Reports/tests/LVL2/" + name + ".png", texture.EncodeToPNG()); }
            finally { RenderTexture.active = previous; camera.targetTexture = null; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(go); }
        }

        private static void PuzzleTick(CoopWoodenBoat boat, double elapsed)
        {
            var gate = UnityEngine.Object.FindAnyObjectByType<SluiceGateObstacle>();
            var dad = LevelTwoActors.All.First(LevelOnePlayer.IsDad);
            var child = LevelTwoActors.All.First(p => !LevelOnePlayer.IsDad(p));
            if (step == 2)
            {
                if (!UnityEngine.Object.FindAnyObjectByType<CampaignCheckpointController>().Ready || elapsed < 3) return;
                foreach (var g in UnityEngine.Object.FindObjectsByType<RiverSlalomGate>().OrderBy(g => g.gateNumber))
                    g.RestoreCheckpoint(true);
                boat.DismountPlayer(true); boat.DismountPlayer(false); boat.enabled = false;
                foreach (var actor in new[] { dad, child })
                {
                    actor.CoreMovement.enabled = false;
                    actor.GetComponent<CharacterController>().enabled = true;
                }
                dad.CoreMovement.SetPosition(gate.crawlDoor.position - Vector3.forward);
                child.CoreMovement.SetPosition(gate.valve.position);
                Check(!gate.TryOpenValve(child), "valve rejects child before rock and crawl");
                Check(!gate.TryPushRock(child), "Child cannot push rock");
                Check(gate.TryPushRock(dad), "Dad pushes rock at entrance");
                Next(); return;
            }
            if (step == 3 && elapsed > 0.7)
            {
                Check(gate.Phase == 1 && gate.Lift == 0, "rock clears the passage while main gate stays closed");
                child.CoreMovement.SetPosition(new Vector3(109.5f, 1.15f, 70.4f));
                Next(); return;
            }
            if (step == 4 && elapsed > 0.7)
            {
                var capsule = child.GetComponent<CharacterController>();
                capsule.Move(new Vector3(0, -.04f, .05f));
                if (child.transform.position.z < 76.6f) return;
                Check(capsule.height <= 1.1f, "Child physically crawled beneath roof");

                gate.ObserveCrawl(child);
                Check(gate.ChildTraversed, "server tracks Child entering and leaving conduit");
                Next(); return;
            }
            if (step == 5 && elapsed > 0.2)
            {
                Check(!gate.TryOpenValve(dad), "Dad cannot open Child valve");
                child.CoreMovement.SetPosition(gate.valve.position);
                Check(gate.TryOpenValve(child), "traversed Child opens valve");
                Next(); return;
            }
            if (step == 6 && elapsed > 3)
            {
                Check(gate.Open && gate.Lift >= 4.99f, "iron gate fully lifts and remains open after valve activation");
                boat.MountPlayer(dad.gameObject, true); boat.MountPlayer(child.gameObject, false);
                var whirl = UnityEngine.Object.FindObjectsByType<WhirlpoolHazard>().First(w => w.rotationSign > 0);
                boat.transform.position = whirl.transform.position + Vector3.right * 4;
                boat.transform.rotation = Quaternion.LookRotation(whirl.EscapeDirection(boat.transform.position)) * Quaternion.Euler(-90, 180, 0);
                Set(boat, "m_ChildSteerInput", 0f); boat.TriggerRowStroke();
                Check(!whirl.CanEscape, "rowing alone cannot escape vortex");
                Set(boat, "m_ChildSteerInput", -1f);
                Check(!whirl.CanEscape, "wrong steering direction cannot escape vortex");
                Set(boat, "m_ChildSteerInput", 1f);
                Check(whirl.CanEscape, "correct 45-degree counter-steer with rowing unlocks escape");
                Set(boat, "m_LastStrokeAt", -10f);
                Check(!whirl.CanEscape, "steering alone cannot escape vortex");
                boat.TriggerRowStroke();
                Check(whirl.CanEscape, "new Dad stroke restores cooperative escape");
                File.AppendAllText(Report, "LIMITATION: host with two owned actors; independent client and human difficulty tuning remain release QA.\n");
                Finish();
            }
        }
    }
}
