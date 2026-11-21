using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

namespace Blocks.Gameplay.Core
{
    public enum ChaseState : byte { Waiting, Chasing, Caught, Stopped }

    [RequireComponent(typeof(NetworkObject))]
    public sealed class ChaseBossController : NetworkBehaviour
    {
        [Serializable] public struct Checkpoint
        {
            public Transform dad;
            public Transform child;
            public float bossDistance;
        }

        public ChaseWaypointRoute route;
        public Transform bossVisual;
        public Checkpoint[] checkpoints = Array.Empty<Checkpoint>();
        public float baseSpeed = 4.5f;
        public float slowdownSpeed = 1.8f;
        public float dangerRadius = 8f;
        public float catchDistance = 2.2f;
        public float respawnDelay = 3f;
        public float startGrace = 5f;
        public AudioSource footsteps;
        public UnityEvent onPlayerCaught = new UnityEvent();
        public UnityEvent onBossStopped = new UnityEvent();
        public event Action<int> CheckpointReset;

        private readonly NetworkVariable<float> distance = new NetworkVariable<float>();
        private readonly NetworkVariable<int> checkpoint = new NetworkVariable<int>();
        private readonly NetworkVariable<ChaseState> state = new NetworkVariable<ChaseState>(ChaseState.Waiting);
        private readonly HashSet<UnityEngine.Object> obstacles = new HashSet<UnityEngine.Object>();
        private readonly List<CorePlayerManager> players = new List<CorePlayerManager>();
        private float refreshAt, moveAfter, nextFootstep, displayDistance;
        private Coroutine respawnRoutine;
        private bool finalCharge;
        public IReadOnlyList<CorePlayerManager> Players => players;
        public ChaseState State => state.Value;
        public int CheckpointIndex => checkpoint.Value;
        public float TravelDistance => distance.Value;
        public float CurrentSpeed => obstacles.Count > 0 ? slowdownSpeed : baseSpeed;

        public override void OnNetworkSpawn()
        {
            displayDistance = distance.Value;
            state.OnValueChanged += StateChanged;
            RefreshPlayers();
        }

        public override void OnNetworkDespawn()
        {
            state.OnValueChanged -= StateChanged;
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            obstacles.Clear();
            players.Clear();
        }

        private void StateChanged(ChaseState previous, ChaseState current)
        {
            if (current == ChaseState.Caught) onPlayerCaught.Invoke();
            if (current == ChaseState.Stopped) onBossStopped.Invoke();
        }

        private void RefreshPlayers()
        {
            players.Clear();
            foreach (var player in FindObjectsByType<CorePlayerManager>(FindObjectsSortMode.None))
                if (player.IsSpawned && player.GetComponent<SoloCompanionDummy>() == null) players.Add(player);
            refreshAt = Time.unscaledTime + 0.5f;
        }

        private void Update()
        {
            if (!IsSpawned || route == null || bossVisual == null) return;
            if (Time.unscaledTime >= refreshAt) RefreshPlayers();
            if (IsServer)
            {
                obstacles.RemoveWhere(item => item == null);
                if (state.Value == ChaseState.Waiting && HasBothRoles())
                {
                    state.Value = ChaseState.Chasing;
                    moveAfter = Time.time + startGrace;
                }
                if (state.Value == ChaseState.Chasing && HasBothRoles())
                {
                    if (Time.time >= moveAfter || finalCharge) distance.Value = Mathf.Min(route.Length, distance.Value + (finalCharge ? Mathf.Max(12f, (route.Length - distance.Value) / 0.6f) : CurrentSpeed) * Time.deltaTime);
                    Vector3 bossPosition = route.Sample(distance.Value, out _);
                    foreach (var player in players)
                    {
                        if (player == null) continue;
                        if (!finalCharge && Time.time >= moveAfter && (!LevelOnePlayer.IsAlive(player) || Vector3.Distance(player.transform.position, bossPosition) < catchDistance))
                        { CatchTeam(); break; }
                    }
                }
            }
            // Server decides progress; remote views interpolate only the presentation.
            displayDistance = IsServer || Mathf.Abs(displayDistance - distance.Value) > 10f
                ? distance.Value : Mathf.Lerp(displayDistance, distance.Value, 1f - Mathf.Exp(-18f * Time.deltaTime));
            bossVisual.position = route.Sample(displayDistance, out var forward);
            forward.y = 0;
            if (forward.sqrMagnitude > 0.001f) bossVisual.rotation = Quaternion.Slerp(bossVisual.rotation, Quaternion.LookRotation(forward), Time.deltaTime * 8f);
            UpdateThreatFeedback();
        }

        private bool HasBothRoles()
        {
            bool dad = false, child = false;
            foreach (var player in players)
            {
                if (player == null) continue;
                if (LevelOnePlayer.IsDad(player)) dad = true; else child = true;
            }
            return dad && child;
        }

        private void UpdateThreatFeedback()
        {
            if (state.Value != ChaseState.Chasing || Time.time < nextFootstep) return;
            float nearestLocal = float.PositiveInfinity;
            foreach (var player in players)
                if (player != null && player.IsOwner && LevelOnePlayer.IsAlive(player))
                    nearestLocal = Mathf.Min(nearestLocal, Vector3.Distance(player.transform.position, bossVisual.position));
            nextFootstep = Time.time + (obstacles.Count > 0 ? 0.85f : 0.55f);
            if (nearestLocal >= dangerRadius) return;
            float intensity = Mathf.Clamp01(1f - nearestLocal / dangerRadius);
            if (footsteps != null && footsteps.clip != null) footsteps.PlayOneShot(footsteps.clip, Mathf.Lerp(0.25f, 1f, intensity));
            if (CoreDirector.Instance != null)
                CoreDirector.RequestCameraShake().WithDuration(0.2f).WithVelocity(0.08f + intensity * 0.65f).Execute();
        }

        public void SetObstacleActive(UnityEngine.Object source, bool active)
        {
            if (!IsServer || source == null) return;
            if (active) obstacles.Add(source); else obstacles.Remove(source);
        }

        public void RestoreCampaignCheckpoint(int index)
        {
            if (!IsServer || checkpoints.Length == 0) return;
            StopAllCoroutines(); respawnRoutine = null;
            checkpoint.Value = Mathf.Clamp(index, 0, checkpoints.Length - 1);
            finalCharge = false; obstacles.Clear();
            distance.Value = checkpoints[checkpoint.Value].bossDistance;
            moveAfter = Time.time + startGrace;
            state.Value = ChaseState.Chasing;
            RestoreCrewRpc(checkpoint.Value);
        }

        [Rpc(SendTo.Everyone)]
        private void RestoreCrewRpc(int index)
        {
            InGameEscapeMenuManager.Instance?.CloseMenu();
            if (respawnRoutine != null) { StopCoroutine(respawnRoutine); respawnRoutine = null; }
            RefreshPlayers();
            foreach (var player in players)
            {
                if (!player.IsOwner) continue;
                LevelOnePlayer.ReleaseCoop(player);
                LevelOnePlayer.Respawn(player, LevelOnePlayer.IsDad(player) ? checkpoints[index].dad : checkpoints[index].child);
                player.CoreInput.enabled = true;
                player.SetMovementInputEnabled(true);
            }
            CheckpointReset?.Invoke(index);
        }

        public void SetCheckpoint(int index)
        {
            if (!IsServer || index <= checkpoint.Value || index >= checkpoints.Length || state.Value != ChaseState.Chasing) return;
            checkpoint.Value = index;
        }

        public void CatchTeam()
        {
            if (!IsServer || state.Value != ChaseState.Chasing || checkpoints.Length == 0) return;
            state.Value = ChaseState.Caught;
            BeginTeamRespawnRpc(checkpoint.Value);
            StartCoroutine(ResumeChase());
        }

        [Rpc(SendTo.Everyone)]
        private void BeginTeamRespawnRpc(int index)
        {
            InGameEscapeMenuManager.Instance?.CloseMenu();
            if (respawnRoutine != null) StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnOwners(index));
        }

        private IEnumerator RespawnOwners(int index)
        {
            RefreshPlayers();
            foreach (var player in players)
            {
                if (!player.IsOwner) continue;
                LevelOnePlayer.ReleaseCoop(player);
                // Same lifecycle consumed by Platformer VisualsAddon: elimination VFX/SFX and component shutdown.
                player.PlayerState.SetLifeState(PlayerLifeState.Eliminated);
            }
            CheckpointReset?.Invoke(index);
            yield return new WaitForSeconds(respawnDelay);
            RefreshPlayers();
            foreach (var player in players)
            {
                LevelOnePlayer.Respawn(player, LevelOnePlayer.IsDad(player) ? checkpoints[index].dad : checkpoints[index].child);
                if (player.IsOwner && index < checkpoints.Length - 1)
                { player.CoreInput.enabled = true; player.SetMovementInputEnabled(true); }
            }
            respawnRoutine = null;
        }

        private IEnumerator ResumeChase()
        {
            yield return new WaitForSeconds(respawnDelay + 0.5f);
            distance.Value = checkpoints[checkpoint.Value].bossDistance;
            obstacles.Clear();
            moveAfter = Time.time + startGrace;
            state.Value = ChaseState.Chasing;
        }

        public void StopAtGate()
        {
            if (!IsServer) return;
            state.Value = ChaseState.Stopped;
            obstacles.Clear();
        }

        public void ChargeGate()
        {
            if (IsServer && state.Value == ChaseState.Chasing) finalCharge = true;
        }

        private void OnGUI()
        {
            if (!IsSpawned || state.Value != ChaseState.Caught) return;
            GUI.Box(new Rect(Screen.width / 2f - 230, Screen.height * 0.3f, 460, 65),
                "BOSS ĐÃ BẮT ĐƯỢC!\nBa và Con đang trở về checkpoint...");
        }
    }
}
