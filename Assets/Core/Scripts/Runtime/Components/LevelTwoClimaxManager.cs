using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blocks.Gameplay.Core
{
    [RequireComponent(typeof(NetworkObject), typeof(LevelTwoFeedback))]
    public sealed class LevelTwoClimaxManager : NetworkBehaviour
    {
        public CoopWoodenBoat boat;
        public GiantWatermillObstacle watermill;
        public MarshBeaconTrigger west, east;
        public Transform altar;
        public BoxCollider harbor;
        public string nextLevelScene = "PlayScene4";
        private readonly NetworkVariable<bool> docked = new NetworkVariable<bool>();
        private readonly NetworkVariable<bool> completed = new NetworkVariable<bool>();
        private bool presented, transitioning;
        private string transitionError;
        private bool originalFog;
        private float originalDensity;
        private FogMode originalMode;
        private Color originalColor;
        public bool Completed => completed.Value;
        public bool Docked => docked.Value;
        public void RestoreCheckpoint(bool arrived, bool finished)
        {
            if (!IsServer) return;
            docked.Value = arrived;
            completed.Value = finished;
            if (!finished) { presented = transitioning = false; transitionError = null; }
        }
        public override void OnNetworkSpawn()
        { completed.OnValueChanged += CompletionChanged; }
        public override void OnNetworkDespawn()
        { completed.OnValueChanged -= CompletionChanged; }
        private void CompletionChanged(bool before, bool after)
        { if (!after) { presented = transitioning = false; transitionError = null; } }
        private void Awake()
        {
            originalFog = RenderSettings.fog; originalDensity = RenderSettings.fogDensity;
            originalMode = RenderSettings.fogMode; originalColor = RenderSettings.fogColor;
        }
        private void Update()
        {
            if (!IsSpawned) return;
            bool inMarsh = boat != null && boat.transform.position.z > 150 && boat.transform.position.z < 216;
            RenderSettings.fog = inMarsh || originalFog;
            RenderSettings.fogMode = inMarsh ? FogMode.ExponentialSquared : originalMode;
            RenderSettings.fogColor = inMarsh ? new Color(0.65f, 0.7f, 0.73f) : originalColor;
            float density = inMarsh ? Mathf.Lerp(0.065f, 0.005f, ((west.Lit ? 1 : 0) + (east.Lit ? 1 : 0)) / 2f) : originalDensity;
            RenderSettings.fogDensity = Mathf.MoveTowards(RenderSettings.fogDensity, density, Time.deltaTime * 0.018f);
            if (IsServer)
            {
                if (watermill.Passed && boat.HasCoopCrew && harbor.bounds.Contains(boat.transform.position)) docked.Value = true;
                TryComplete();
            }
            if (Completed && !presented)
            {
                presented = true;
                PlayerPrefs.SetInt("HMH.Level2.Completed", 1);
                CampaignProgress.Save(2, 6);
                GetComponent<LevelTwoFeedback>().Play();
            }
        }
        public bool TryComplete()
        {
            if (!IsServer || Completed || !watermill.Passed || !west.Lit || !east.Lit || boat.IsDadMounted || boat.IsChildMounted) return false;
            CorePlayerManager dad = null, child = null;
            foreach (var actor in LevelTwoActors.All)
                if (LevelTwoActors.Near(actor, altar, 3f)) { if (LevelOnePlayer.IsDad(actor)) dad = actor; else child = actor; }
            if (dad == null || child == null || Vector3.Distance(dad.transform.position, child.transform.position) > 2.2f) return false;
            // The dock allows disembarking before the hull enters the arrival box.
            // Both living roles reaching the altar also proves they arrived at Haven.
            docked.Value = true;
            if (!dad.TryGetComponent<CoopHoldHandAbility>(out var dadHand) || !child.TryGetComponent<CoopHoldHandAbility>(out var childHand) ||
                !dadHand.IsHoldingHands || !childHand.IsHoldingHands) return false;
            completed.Value = true; return true;
        }
        public async void ContinueToNextLevel()
        {
            if (!IsServer || !Completed || transitioning) return;
            transitioning = true;
            try { await CampaignProgress.RefreshCatalog(); }
            catch (System.Exception ex) { transitioning = false; transitionError = ex.Message; return; }
            if (!this || !IsSpawned || !IsServer) return;
            int next = CampaignProgress.NextAvailable(2);
            transitioning = false;
            if (next == 0) { transitionError = "Đã hoàn thành các màn đang mở."; return; }
            nextLevelScene = CampaignProgress.Scene(next);
            if (!Application.CanStreamedLevelBeLoaded(nextLevelScene)) { transitionError = "Chưa có màn tiếp theo trong Build Settings."; return; }
            transitioning = true; PrepareNextLevelRpc(nextLevelScene);
            var status = NetworkManager.SceneManager.LoadScene(nextLevelScene, LoadSceneMode.Single);
            if (status != SceneEventProgressStatus.Started) { transitioning = false; transitionError = "Chưa chuyển được màn: " + status; }
        }
        [Rpc(SendTo.Everyone)]
        private void PrepareNextLevelRpc(string destination)
        {
            CampaignProgress.Select(CampaignProgress.Level(destination), 0);
            NetworkManager.gameObject.AddComponent<LevelOneSceneTransition>().Begin(NetworkManager, destination);
        }
        public override void OnDestroy()
        {
            RenderSettings.fog = originalFog; RenderSettings.fogDensity = originalDensity;
            RenderSettings.fogMode = originalMode; RenderSettings.fogColor = originalColor;
            base.OnDestroy();
        }
        private void OnGUI()
        {
            if (!IsSpawned) return;
            if (!Completed)
            {
                var local = LevelTwoActors.Local;
                if (Docked || (local != null && Vector3.Distance(local.transform.position, altar.position) < 14f))
                    GUI.Box(new Rect(20, 85, 650, 60), !west.Lit || !east.Lit ? "Hãy thắp sáng đủ hai hải đăng trước khi hoàn thành màn." :
                        !watermill.Passed ? "Chưa ghi nhận vượt cối xay. Cả hai cùng chèo thuyền qua lúc cửa mở." :
                        "Haven Harbor: cả hai bấm [E] lên bờ.\nCùng tới Bàn Thờ Ký Ức, đứng sát và giữ [H] nắm tay để hoàn thành.");
                return;
            }
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var r = new Rect(Screen.width / 2f - 310, Screen.height / 2f - 120, 620, 240);
            var style = new GUIStyle(GUI.skin.box) { fontSize = 22, alignment = TextAnchor.UpperCenter, wordWrap = true };
            GUI.Box(r, "MÀN 2 HOÀN THÀNH\nNẮM TAY NHAU — BẾN BỜ BÌNH MINH", style);
            if (GUI.Button(new Rect(r.x + 35, r.y + 120, 250, 45), "Quay về Lobby")) InGameEscapeMenuManager.EnsureInstanceInScene().ReturnToLobby();
            GUI.enabled = IsServer && !transitioning;
            if (GUI.Button(new Rect(r.x + 335, r.y + 120, 250, 45), "Tiếp tục chiến dịch")) ContinueToNextLevel();
            GUI.enabled = true;
            GUI.Label(new Rect(r.x + 35, r.y + 190, 550, 35), transitionError ?? (IsServer ? "Tiến độ đã lưu." : "Chờ chủ phòng chuyển màn."));
        }
    }
}
