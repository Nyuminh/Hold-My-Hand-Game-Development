using System.Collections;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace Blocks.Gameplay.Core
{
    public enum GateClimaxPhase { Drain, Opening, ChainBreak, Holding, ChildSliding, DadRetreat, Slamming, BossImpact, Victory }

    public sealed class CastleGateClimaxManager : LevelOneObstacle
    {
        public MonoBehaviour gateDriver;
        public Transform childLever, dadGrip, slideEntry, childSafe, dadSafe;
        public CoopCrawlVolume drain;
        public Transform blockers;
        public AudioSource impactAudio;
        public AudioClip chainBreakClip, gateSlamClip, bossImpactClip;
        public string nextLevelScene = "PlayScene2";
        private readonly NetworkVariable<float> lift = new NetworkVariable<float>();
        private readonly NetworkVariable<bool> enteredDrain = new NetworkVariable<bool>();
        private float phaseTimer;
        private int shownPhase = -1;
        private LevelOneBracePose brace;
        private Coroutine cinematic;
        private bool transitioning;
        private string transitionError;
        private bool showVictoryMenu;
        public GateClimaxPhase ClimaxPhase => (GateClimaxPhase)Phase;
        public override bool Solved => ClimaxPhase == GateClimaxPhase.Victory;
        protected override int SolvedPhase => (int)GateClimaxPhase.Victory;
        public float GateLift => lift.Value;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            (gateDriver as ILevelOneGateDriver)?.BeginClimaxControl();
        }

        private void SetPhase(GateClimaxPhase value) { phase.Value = (int)value; phaseTimer = 0; }

        public override bool TryInteract(CorePlayerManager player)
        {
            if (!CanInteract(player)) return false;
            bool dad = LevelOnePlayer.IsDad(player);
            if (!dad && ClimaxPhase == GateClimaxPhase.Drain && enteredDrain.Value && Near(player,childLever,2f))
            { SetPhase(GateClimaxPhase.Opening); return true; }
            if (dad && ClimaxPhase == GateClimaxPhase.ChainBreak && Near(player,dadGrip,2.5f))
            { SetPhase(GateClimaxPhase.Holding); lift.Value = 1.15f; return true; }
            if (!dad && ClimaxPhase == GateClimaxPhase.Holding && Near(player,slideEntry,2f))
            {
                SetPhase(GateClimaxPhase.ChildSliding);
                MoveActorRpc(player.NetworkObjectId,childSafe.position,1.3f,true);
                return true;
            }
            return false;
        }

        protected override void ServerTick()
        {
            if (boss.State != ChaseState.Chasing || Solved) return;
            phaseTimer += Time.deltaTime;
            var dad = FindRole(true); var child = FindRole(false);
            switch (ClimaxPhase)
            {
                case GateClimaxPhase.Drain:
                    if (child != null && drain != null)
                    {
                        var pos = drain.transform.InverseTransformPoint(child.transform.position);
                        if (Mathf.Abs(pos.x) < 1.35f && pos.z < -1.5f && pos.z > -8.5f) enteredDrain.Value = true;
                    }
                    break;
                case GateClimaxPhase.Opening:
                    lift.Value = Mathf.MoveTowards(lift.Value,5.5f,Time.deltaTime*4);
                    if (lift.Value >= 5.5f) SetPhase(GateClimaxPhase.ChainBreak);
                    break;
                case GateClimaxPhase.ChainBreak:
                    lift.Value = Mathf.MoveTowards(lift.Value,0f,Time.deltaTime*1.6f);
                    if (lift.Value <= 0) boss.CatchTeam();
                    break;
                case GateClimaxPhase.Holding:
                    if (!Near(dad,dadGrip,3.5f)) boss.CatchTeam();
                    break;
                case GateClimaxPhase.ChildSliding:
                    if (phaseTimer > 1.5f && Near(child,childSafe,2.5f) && dad != null)
                    {
                        SetPhase(GateClimaxPhase.DadRetreat);
                        MoveActorRpc(dad.NetworkObjectId,dadSafe.position,1f,false);
                    }
                    else if (phaseTimer > 5f) boss.CatchTeam();
                    break;
                case GateClimaxPhase.DadRetreat:
                    lift.Value = 3f;
                    if (phaseTimer > 1.2f && Near(dad,dadSafe,2.5f)) SetPhase(GateClimaxPhase.Slamming);
                    else if (phaseTimer > 5f) boss.CatchTeam();
                    break;
                case GateClimaxPhase.Slamming:
                    lift.Value = Mathf.MoveTowards(lift.Value,0f,Time.deltaTime*12);
                    if (lift.Value <= 0) { SetPhase(GateClimaxPhase.BossImpact); boss.ChargeGate(); }
                    break;
                case GateClimaxPhase.BossImpact:
                    if (boss.TravelDistance >= boss.route.Length - 0.1f)
                    {
                        boss.SetCheckpoint(checkpointAfter);
                        boss.StopAtGate();
                        SetPhase(GateClimaxPhase.Victory);
                    }
                    break;
            }
        }

        protected override void Present()
        {
            (gateDriver as ILevelOneGateDriver)?.SetClimaxLift(lift.Value);
            if (blockers != null) blockers.localPosition = Vector3.up * lift.Value;
            bool holding = ClimaxPhase == GateClimaxPhase.Holding || ClimaxPhase == GateClimaxPhase.ChildSliding;
            if (holding && brace == null)
            {
                var dad = FindRole(true);
                if (dad != null) { brace = dad.gameObject.AddComponent<LevelOneBracePose>(); brace.Begin(dad); }
            }
            if (!holding && brace != null) { Destroy(brace); brace = null; }
            if (shownPhase == Phase) return;
            shownPhase = Phase;
            if (ClimaxPhase == GateClimaxPhase.ChainBreak) Impact(chainBreakClip,0.35f);
            if (ClimaxPhase == GateClimaxPhase.BossImpact) Impact(gateSlamClip,0.85f);
            if (Solved)
            {
                Impact(bossImpactClip,1.1f);
                if (CastleCourtyardQuestManager.Instance != null)
                {
                    CastleCourtyardQuestManager.Instance.StartQuest();
                }
                else
                {
                    OnCourtyardQuestVictory();
                }
            }
        }

        public void OnCourtyardQuestVictory()
        {
            showVictoryMenu = true;
            PlayerPrefs.SetInt("HMH.Level1.Completed",1);
            PlayerPrefs.SetInt("HMH.Level1.Checkpoint",checkpointAfter);
            PlayerPrefs.Save();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (boss != null && boss.Players != null)
            {
                foreach(var player in boss.Players)
                    if(player != null && player.IsOwner) { player.CoreInput.enabled=false; player.SetMovementInputEnabled(false); }
            }
        }

        private void Impact(AudioClip clip,float strength)
        {
            if (impactAudio != null && clip != null) impactAudio.PlayOneShot(clip);
            if (CoreDirector.Instance != null) CoreDirector.RequestCameraShake().WithDuration(0.45f).WithVelocity(strength).Execute();
        }

        [Rpc(SendTo.Everyone)]
        private void MoveActorRpc(ulong id,Vector3 destination,float duration,bool slide)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id,out var obj)) return;
            var player=obj.GetComponent<CorePlayerManager>();
            if(player == null || !player.IsOwner) return;
            if(cinematic != null) StopCoroutine(cinematic);
            cinematic=StartCoroutine(MoveActor(player,destination,duration,slide));
        }

        private IEnumerator MoveActor(CorePlayerManager player,Vector3 destination,float duration,bool slide)
        {
            // Let a released brace restore its inputs before this cinematic takes control.
            yield return null;
            // SetPosition is owner-authoritative and replicated by the existing CoreMovement NetworkTransform.
            var motor=player.CoreMovement;
            bool inputWasEnabled=player.CoreInput.enabled;
            player.CoreInput.enabled=false; player.SetMovementInputEnabled(false);
            LevelOnePlayer.ReleaseCoop(player);
            var controller=player.GetComponent<CharacterController>();
            float height=controller.height; Vector3 center=controller.center;
            var start=player.transform.position;
            if(slide) { controller.height=0.9f; controller.center=Vector3.up*0.45f; motor.SetCrouchState(true); }
            else player.transform.forward=-transform.forward;
            try
            {
                float time=0;
                while(time<duration && player.PlayerState.IsActive)
                {
                    time+=Time.deltaTime; float t=Mathf.Clamp01(time/duration);
                    var position=Vector3.Lerp(start,destination,Mathf.SmoothStep(0,1,t));
                    if(!slide)position.y+=Mathf.Sin(t*Mathf.PI)*0.8f;
                    motor.SetPosition(position,false);
                    yield return null;
                }
            }
            finally
            {
                if(player != null)
                {
                    controller.height=height; controller.center=center;motor.SetCrouchState(false);motor.ResetMovementForces();
                    player.CoreInput.enabled=inputWasEnabled&&player.PlayerState.IsActive;
                    player.SetMovementInputEnabled(player.PlayerState.IsActive);
                    if(player.TryGetComponent<CoopBoostThrowAbility>(out var boost))boost.enabled=true;
                }
            }
            cinematic=null;
        }

        protected override void ResetForCheckpoint(int index)
        {
            base.ResetForCheckpoint(index);
            transitioning = false; transitionError = null;
            if(IsServer) { lift.Value=0;enteredDrain.Value=false;phaseTimer=0; }
        }

        protected override void Cleanup()
        {
            if(brace != null)Destroy(brace);brace=null;
            if(cinematic != null)StopCoroutine(cinematic);cinematic=null;
        }

        public async void ContinueToNextLevel()
        {
            if(!showVictoryMenu || transitioning || !IsServer) return;
            transitioning = true;
            try { await CampaignProgress.RefreshCatalog(); }
            catch (System.Exception ex) { transitioning = false; transitionError = ex.Message; return; }
            if (!this || !IsSpawned || !IsServer) return;
            int next = CampaignProgress.NextAvailable(1);
            transitioning = false;
            if (next == 0) { transitionError = "Đã hoàn thành các màn đang mở."; return; }
            nextLevelScene = CampaignProgress.Scene(next);
            if(!Application.CanStreamedLevelBeLoaded(nextLevelScene)) { transitionError="Không tìm thấy màn tiếp theo trong Build Settings.";return; }
            transitioning=true;
            PrepareNextLevelRpc(nextLevelScene);
            var status=NetworkManager.SceneManager.LoadScene(nextLevelScene,LoadSceneMode.Single);
            if(status!=SceneEventProgressStatus.Started){transitioning=false;transitionError="Chưa thể chuyển màn: "+status;}
        }

        [Rpc(SendTo.Everyone)]
        private void PrepareNextLevelRpc(string destination)
        {
            CampaignProgress.Select(CampaignProgress.Level(destination), 0);
            var transition=NetworkManager.gameObject.AddComponent<LevelOneSceneTransition>();
            transition.Begin(NetworkManager,destination);
        }

        protected override string Hint(CorePlayerManager player)
        {
            switch(ClimaxPhase)
            {
                case GateClimaxPhase.Drain:return LevelOnePlayer.IsDad(player)?"Ba: chờ ở điểm giữ cổng. Con chui cống bên phải để kéo cần.":"Con: chui cống bên phải, tới cần bên trong và bấm [E].";
                case GateClimaxPhase.Opening:return "Cổng đang mở! Ba tới điểm giữ cổng; Con quay lại cạnh Ba.";
                case GateClimaxPhase.ChainBreak:return "XÍCH ĐỨT! Ba bấm [E] ngay tại chân cổng để đỡ cổng!";
                case GateClimaxPhase.Holding:return "Ba giữ cổng! Con tới dấu trượt phía ngoài, bấm [E] trượt vào sân trong.";
                default:return "Nắm chặt tay — vào trong lâu đài!";
            }
        }

        protected override void OnGUI()
        {
            if(!showVictoryMenu){base.OnGUI();return;}
            if(!IsSpawned)return;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            var rect=new Rect(Screen.width/2f-310,Screen.height/2f-120,620,240);
            var style=new GUIStyle(GUI.skin.box){fontSize=22,alignment=TextAnchor.UpperCenter,wordWrap=true};
            GUI.Box(rect,"MÀN 1 HOÀN THÀNH\nNẮM TAY BƯỚC RA KHỎI LÂU ĐÀI",style);
            if(GUI.Button(new Rect(rect.x+40,rect.y+120,250,45),"Quay về Lobby"))
                InGameEscapeMenuManager.EnsureInstanceInScene().ReturnToLobby();
            GUI.enabled=IsServer&&!transitioning;
            if(GUI.Button(new Rect(rect.x+330,rect.y+120,250,45),"Tiếp tục chiến dịch"))ContinueToNextLevel();
            GUI.enabled=true;
            GUI.Label(new Rect(rect.x+40,rect.y+185,540,40),transitionError??(IsServer?"Checkpoint đã lưu.":"Chờ chủ phòng chọn tiếp tục Màn 2."));
        }
    }
}
