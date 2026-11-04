using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace Blocks.Gameplay.Core.LevelOneAuthoring
{
    [InitializeOnLoad]
    public static class LevelOneBatchValidation
    {
        private const string ActiveKey="LVL1.Batch.Active";
        private static int stage;
        private static double stageAt;
        private static double started;
        private static string Report=>"AI_Reports/tests/LVL1/climax-playmode.txt";
        static LevelOneBatchValidation(){EditorApplication.update+=Tick;}

        public static void Run()
        {
            if(Environment.GetCommandLineArgs().Contains("-lvl1ResetTestProgress"))
            {
                PlayerPrefs.DeleteKey("HMH.Level1.Completed");PlayerPrefs.DeleteKey("HMH.Level1.Checkpoint");PlayerPrefs.Save();
            }
            foreach(var key in new[]{"HMH.Level1.Completed","HMH.Level1.Checkpoint"})
            {SessionState.SetBool(key+".Exists",PlayerPrefs.HasKey(key));SessionState.SetInt(key+".BeforeQA",PlayerPrefs.GetInt(key));}
            EditorSceneManager.OpenScene(LevelOneSceneBuilder.ScenePath,OpenSceneMode.Single);
            LevelOneSceneBuilder.FindEncounter();
            if(LevelOneSceneBuilder.Root.GetComponentInChildren<CastleGateClimaxManager>()==null)LevelOneSceneBuilder.BuildClimax();
            LevelOneValidation.ValidateBoss();
            var c=LevelOneSceneBuilder.Root.GetComponentInChildren<CastleGateClimaxManager>();
            LevelOneValidation.Check(c.gateDriver is ILevelOneGateDriver,"CastleGateController implements driver");
            LevelOneValidation.Check(c.chainBreakClip&&c.gateSlamClip&&c.bossImpactClip,"all three audio cues assigned");
            LevelOneValidation.Check(c.nextLevelScene=="PlayScene1","user-selected continuation");
            CaptureCastle(c);
            File.WriteAllText(Report,"PASS compile and scene: climax driver, audio, blockers, next scene PlayScene1.\n");
            SessionState.SetBool(ActiveKey,true);stage=0;started=EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying=true;
        }

        private static void Advance(){stage++;stageAt=EditorApplication.timeSinceStartup;}
        private static void Pass(string message){File.AppendAllText(Report,"PASS "+message+"\n");Debug.Log("[LVL1 QA] "+message);}

        private static void Tick()
        {
            if(!SessionState.GetBool(ActiveKey,false)||!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
            if(started==0)started=EditorApplication.timeSinceStartup;
            if(EditorApplication.timeSinceStartup-started>180){Fail(new Exception("Timed out at stage "+stage));return;}
            try
            {
                var boss=UnityEngine.Object.FindAnyObjectByType<ChaseBossController>();
                var climax=UnityEngine.Object.FindAnyObjectByType<CastleGateClimaxManager>();
                var nm=NetworkManager.Singleton;
                double elapsed=EditorApplication.timeSinceStartup-stageAt;
                if(stage==0)
                {
                    if(nm==null||boss==null)return;
                    boss.startGrace=10000;
                    var transport=nm.GetComponent<UnityTransport>();
                    transport.ConnectionData.Address="127.0.0.1";
                    transport.ConnectionData.Port=7781;
                    LevelOneValidation.Check(nm.StartHost(),"test host starts");Advance();return;
                }
                if(stage==1 && elapsed>1)
                {
                    var child=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Prefabs/The Child.prefab"),boss.checkpoints[0].child.position,Quaternion.identity);
                    child.name="The Child LVL1 Test";child.GetComponent<NetworkObject>().SpawnWithOwnership(0);
                    Advance();return;
                }
                if(stage>=2 && stage<=6)
                {
                    if(boss==null||boss.Players.Count<2)return;
                    var dad=boss.Players.First(LevelOnePlayer.IsDad);var child=boss.Players.First(p=>!LevelOnePlayer.IsDad(p));
                    if(stage==2 && elapsed>1)
                    {
                        boss.SetCheckpoint(3);
                        foreach(var p in boss.Players){p.CoreInput.enabled=false;p.CoreMovement.SetMoveInput(Vector2.zero);}
                        dad.CoreMovement.SetPosition(climax.dadGrip.position);
                        child.CoreMovement.SetPosition(climax.drain.transform.TransformPoint(new Vector3(0,0,-5)));
                        LevelOneValidation.Check(!climax.TryInteract(dad),"Dad cannot operate child lever");
                        Advance();return;
                    }
                    if(stage==3 && elapsed>0.5)
                    {
                        LevelOneValidation.Check(child.GetComponent<CharacterController>().height<1,"drain applies crawl height");
                        child.CoreMovement.SetPosition(climax.childLever.position);
                        LevelOneValidation.Check(climax.TryInteract(child),"child lever after drain");
                        Pass("child drain and role-guarded lever open gate");Advance();return;
                    }
                    if(stage==4 && climax.ClimaxPhase==GateClimaxPhase.ChainBreak)
                    {
                        LevelOneValidation.Check(!climax.TryInteract(child),"child cannot hold falling gate");
                        LevelOneValidation.Check(climax.TryInteract(dad),"Dad catches falling gate");
                        Pass("chain break transitions to Dad holding; Child cannot hold gate");Advance();return;
                    }
                    if(stage==5 && elapsed>0.3)
                    {
                        LevelOneValidation.Check(dad.GetComponent<LevelOneBracePose>()!=null,"Dad brace shown");
                        child.CoreMovement.SetPosition(climax.slideEntry.position);
                        LevelOneValidation.Check(climax.TryInteract(child),"child slide starts");
                        Pass("held gate brace and Child slide interaction");Advance();return;
                    }
                    if(stage==6 && climax.Solved)
                    {
                        LevelOneValidation.Check(boss.State==ChaseState.Stopped,"boss stopped");
                        LevelOneValidation.Check(climax.GateLift==0,"gate slammed shut");
                        LevelOneValidation.Check(Vector3.Distance(child.transform.position,climax.childSafe.position)<2,"child safe");
                        LevelOneValidation.Check(Vector3.Distance(dad.transform.position,climax.dadSafe.position)<2,"Dad safe");
                        // Presentation and PlayerPrefs are updated after this server frame.
                        Pass("Child slide, Dad retreat, gate slam and boss impact complete in order");Advance();return;
                    }
                }
                if(stage==7 && elapsed>0.5)
                {
                    LevelOneValidation.Check(PlayerPrefs.GetInt("HMH.Level1.Completed")==1,"victory persisted");
                    LevelOneValidation.Check(boss.CheckpointIndex==4,"final checkpoint saved");
                    Pass("victory and checkpoint persisted");
                    climax.ContinueToNextLevel();Advance();return;
                }
                if(stage==8 && SceneManager.GetActiveScene().name=="PlayScene1" && elapsed>3)
                {
                    LevelOneValidation.Check(nm!=null&&nm.IsListening,"network session survives next scene");
                    LevelOneValidation.Check(nm.LocalClient.PlayerObject!=null,"player respawned in next scene");
                    LevelOneValidation.Check(GameManager.Instance!=null,"next-level GameManager exists");
                    Pass("Continue loads PlayScene1, keeps host session and respawns player");
                    File.AppendAllText(Report,"LIMITATION: one host with two owned actors, not two independent network clients.\n");
                    RestoreProgress();SessionState.SetBool(ActiveKey,false);EditorApplication.Exit(0);
                }
            }
            catch(Exception exception){Fail(exception);}
        }

        private static void Fail(Exception exception)
        {
            File.AppendAllText(Report,"FAIL stage "+stage+": "+exception+"\n");
            SessionState.SetBool(ActiveKey,false);Debug.LogException(exception);EditorApplication.Exit(1);
        }

        private static void RestoreProgress()
        {
            foreach(var key in new[]{"HMH.Level1.Completed","HMH.Level1.Checkpoint"})
                if(SessionState.GetBool(key+".Exists",false))PlayerPrefs.SetInt(key,SessionState.GetInt(key+".BeforeQA",0));else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }

        private static void CaptureCastle(CastleGateClimaxManager climax)
        {
            var go=new GameObject("LVL1_QA_Camera");var camera=go.AddComponent<Camera>();
            camera.transform.position=climax.transform.position-climax.transform.forward*25+Vector3.up*13;
            camera.transform.LookAt(climax.transform.position+Vector3.up*3);
            camera.farClipPlane=1000;camera.fieldOfView=65;
            var rt=new RenderTexture(1024,768,24);var texture=new Texture2D(1024,768,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1024,768),0,0);texture.Apply();File.WriteAllBytes("AI_Reports/tests/LVL1/castle-climax.png",texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
