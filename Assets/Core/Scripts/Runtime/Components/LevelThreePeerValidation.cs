#if HMH_PARKOUR_QA
using System;
using System.Collections;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blocks.Gameplay.Core
{
    // Compiled only into the standalone pair used for parkour network acceptance.
    public sealed class LevelThreePeerValidation : MonoBehaviour
    {
        readonly string[] keys = { CampaignProgress.LastLevelKey, "HMH.Campaign.Level3.Checkpoint", "HMH.Level3.Completed",
            "HMH.Campaign.Level2.Checkpoint", "HMH.Level2.Completed", "HMH.Level2.Checkpoint" };
        int?[] saved;
        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            saved = keys.Select(key => PlayerPrefs.HasKey(key) ? (int?)PlayerPrefs.GetInt(key) : null).ToArray();
        }
        void OnApplicationQuit()
        {
            for (int i = 0; i < keys.Length; i++)
                if (saved[i].HasValue) PlayerPrefs.SetInt(keys[i], saved[i].Value);
                else PlayerPrefs.DeleteKey(keys[i]);
            PlayerPrefs.Save();
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin() => new GameObject("Parkour Peer Test").AddComponent<LevelThreePeerValidation>();
        IEnumerator Start()
        {
            bool host = Environment.GetCommandLineArgs().Contains("-parkourHost");
            Lobby3DManager.AmIDad = host;
            Application.runInBackground = true;
            var net = NetworkManager.Singleton;
            var transport = net.GetComponent<UnityTransport>();
            transport.ConnectionData.Address = "127.0.0.1";
            transport.ConnectionData.Port = 17795;
            if (!(host ? net.StartHost() : net.StartClient())) { Debug.LogError("FAIL network startup"); Application.Quit(1); yield break; }
            float deadline = Time.realtimeSinceStartup + 75;
            while (LevelTwoActors.All.Count() < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            if (LevelTwoActors.All.Count() < 2) { Debug.LogError("FAIL pair spawn"); Application.Quit(2); yield break; }
            if (SceneManager.GetActiveScene().name == "PlayScene2")
            {
                yield return Transition(net, host);
                if (SceneManager.GetActiveScene().name != "PlayScene4") yield break;
            }
            var tether = FindAnyObjectByType<CoopTetherChain>();
            deadline = Time.realtimeSinceStartup + 75;
            var actor = net.LocalClient.PlayerObject.GetComponent<CorePlayerManager>();
            var launch = FindAnyObjectByType<LevelThreeDrawbridgePulley>().launch.position;
            yield return new WaitForSeconds(1);
            actor.CoreMovement.SetPosition(launch + new Vector3(host ? -2 : 0, .12f, -.7f));
            yield return new WaitForSeconds(1);
            if (!host) actor.CoreMovement.SetPosition(launch + new Vector3(-2, -3.2f, 3.8f));
            bool caught = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (tether.IsSuspended && !caught)
                {
                    caught = true;
                    Debug.Log("PASS replicated catch on " + (host ? "host" : "client"));
                    if (host) { yield return new WaitForSeconds(1); tether.RequestHaul(); }
                }
                if (caught && !tether.IsSuspended && actor.transform.position.y > launch.y - .5f &&
                    tether.ChainVisible)
                {
                    Debug.Log("PASS replicated rescue on " + (host ? "host" : "client"));
                    yield return Puzzles(actor, host);
                    yield break;
                }
                yield return null;
            }
            Debug.LogError("FAIL rescue on " + (host ? "host" : "client") + " at " + actor.transform.position + " caught=" + caught);
            Application.Quit(3);
        }

        IEnumerator Puzzles(CorePlayerManager actor, bool host)
        {
            var bridge = FindAnyObjectByType<LevelThreeDrawbridgePulley>();
            var gate = FindAnyObjectByType<LevelThreeCryptCrawlGate>();
            var altar = FindAnyObjectByType<DualAltarPortal>();
            var campaign = FindAnyObjectByType<LevelThreeManager>();
            var pillar = FindObjectsByType<CollapsingPlatform>().OrderBy(p => p.stone.position.z).First();
            actor.CoreMovement.SetPosition(bridge.pulley.position + new Vector3(host ? -.2f : -2.8f, -.53f, 0));
            yield return new WaitForSeconds(.6f);
            if (host) bridge.RequestTurn();
            yield return new WaitForSeconds(.5f);
            if (bridge.Open) { Fail("Dad opened child winch"); yield break; }
            if (!host) actor.CoreMovement.SetPosition(bridge.pulley.position + new Vector3(-.4f, -.53f, -.6f));
            yield return new WaitForSeconds(.5f);
            if (!host) bridge.RequestTurn();
            yield return new WaitForSeconds(1.7f);
            if (!bridge.Open || Quaternion.Angle(bridge.hinge.localRotation, Quaternion.identity) > 1)
            { Fail("drawbridge"); yield break; }
            Debug.Log("PASS drawbridge lowered by Child");
            actor.CoreMovement.SetPosition(gate.crawl.transform.position + new Vector3(host ? -3.8f : 0, -.7f, host ? -2.8f : -.8f));
            yield return new WaitForSeconds(1);
            if (!host) actor.CoreMovement.SetPosition(gate.lever.position + new Vector3(.6f, -.45f, -.4f));
            yield return new WaitForSeconds(.7f);
            if (!host) gate.RequestUnlock();
            yield return new WaitForSeconds(1.5f);
            if (!gate.Open) { Fail("crawl lever"); yield break; }
            Debug.Log("PASS crypt opened after crawl");
            var checkpoint = campaign.checkpoints[host ? 4 : 5];
            actor.CoreMovement.SetPosition(checkpoint.position);
            yield return new WaitForSeconds(.7f);
            if (campaign.Checkpoint != 2 || string.IsNullOrEmpty(campaign.objective.text)) { Fail("crypt checkpoint or objective"); yield break; }
            actor.CoreMovement.SetPosition(checkpoint.position + new Vector3(12, -7, 4));
            yield return new WaitForSeconds(2.5f);
            if (Vector3.Distance(actor.transform.position, checkpoint.position) < 1.5f) { Fail("respawn before three seconds"); yield break; }
            yield return new WaitForSeconds(1.3f);
            if (Vector3.Distance(actor.transform.position, checkpoint.position) > 1.5f) { Fail("paired checkpoint recovery"); yield break; }
            Debug.Log("PASS both-fall checkpoint and objective");
            float pillarTop = pillar.stone.position.y;
            actor.CoreMovement.SetPosition(host ? pillar.stone.position + new Vector3(1, .12f, -4) : pillar.stone.position + Vector3.up * .12f);
            yield return new WaitForSeconds(.6f);
            if (pillar.stone.position.y < pillarTop - .2f) { Fail("pillar fell early"); yield break; }
            yield return new WaitForSeconds(1.8f);
            if (pillar.stone.position.y > pillarTop - .25f) { Fail("pillar stayed up"); yield break; }
            Debug.Log("PASS collapsing stone");
            actor.CoreMovement.SetPosition(campaign.checkpoints[host ? 6 : 7].position + Vector3.forward * .5f);
            yield return new WaitForSeconds(1);
            actor.CoreMovement.SetPosition(host ? altar.dadPad.position + Vector3.up * .2f : altar.childPad.position + new Vector3(0, .1f, -4));
            yield return new WaitForSeconds(.8f);
            if (altar.Lit) { Fail("single altar activation"); yield break; }
            if (!host) actor.CoreMovement.SetPosition(altar.childPad.position + Vector3.up * .2f);
            yield return new WaitForSeconds(1);
            if (!altar.Lit || !altar.portal.activeSelf) { Fail("paired altar activation"); yield break; }
            Debug.Log("PASS dual altar and portal");
            if (campaign.Checkpoint != 4 || CampaignProgress.SavedCheckpoint(3) != 4 || PlayerPrefs.GetInt("HMH.Level3.Completed") != 1)
            { Fail("durable completion"); yield break; }
            yield return new WaitForSeconds(1);
            if (!host) campaign.RequestRestart(false);
            float deadline = Time.realtimeSinceStartup + 8;
            while ((campaign.Checkpoint != 3 || altar.Lit) && Time.realtimeSinceStartup < deadline) yield return null;
            if (campaign.Checkpoint != 3 || altar.Lit) { Fail("replay final stage"); yield break; }
            yield return new WaitForSeconds(2);
            if (host) campaign.RequestRestart(true);
            deadline = Time.realtimeSinceStartup + 8;
            while (campaign.Checkpoint != 0 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            if (campaign.Checkpoint != 0 || bridge.Open || gate.Open || !pillar.stone.gameObject.activeSelf) { Fail("fresh replay"); yield break; }
            Debug.Log("PASS durable victory and both restart paths");
            yield return new WaitForSeconds(host ? 2 : 1);
            Application.Quit(0);
        }

        static void Fail(string label) { Debug.LogError("FAIL " + label); Application.Quit(4); }

        IEnumerator Transition(NetworkManager net, bool host)
        {
            var source = net.LocalClient.PlayerObject;
            var oldId = source.NetworkObjectId;
            var owner = net.LocalClientId;
            var climax = FindAnyObjectByType<LevelTwoClimaxManager>();
            yield return new WaitForSeconds(2.5f);
            if (host)
            {
                if (climax.TryComplete()) { Fail("missing prerequisites"); yield break; }
                climax.watermill.RestoreCheckpoint(true);
                climax.west.RestoreCheckpoint(true);
                climax.east.RestoreCheckpoint(true);
                climax.RestoreCheckpoint(true, false);
                climax.boat.DismountPlayer(true);
                climax.boat.DismountPlayer(false);
            }
            yield return new WaitForSeconds(1);
            source.GetComponent<CharacterController>().enabled = true;
            source.GetComponent<CoreMovement>().SetPosition(climax.altar.position + Vector3.right * (host ? -.8f : .8f));
            var hand = source.GetComponent<CoopHoldHandAbility>();
            hand.enabled = false;
            hand.SetHoldingHandsDirect(true);
            yield return new WaitForSeconds(1.2f);
            if (host && !climax.Completed) climax.TryComplete();
            yield return new WaitForSeconds(.5f);
            if (!climax.Completed) { Fail("cooperative river altar"); yield break; }
            Debug.Log("PASS cooperative river altar");
            yield return new WaitForSeconds(.5f);
            if (host) climax.ContinueToNextLevel();
            float deadline = Time.realtimeSinceStartup + 60;
            while (Time.realtimeSinceStartup < deadline && (SceneManager.GetActiveScene().name != "PlayScene4" ||
                !net.LocalClient.PlayerObject || net.LocalClient.PlayerObject.NetworkObjectId == oldId)) yield return null;
            yield return new WaitForSeconds(2.5f);
            var castle = FindAnyObjectByType<LevelThreeManager>();
            var actor = net.LocalClient?.PlayerObject?.GetComponent<CorePlayerManager>();
            if (!castle || !actor || !net.IsListening || net.LocalClientId != owner || actor.NetworkObjectId == oldId ||
                LevelOnePlayer.IsDad(actor) != host || !actor.CoreMovement.IsMovementEnabled || !actor.CoreInput.enabled ||
                actor.GetComponent<CoreCameraController>().ActiveCameraMode == null || !actor.GetComponent<CoreCameraController>().enabled ||
                Vector3.Distance(actor.transform.position, castle.checkpoints[host ? 0 : 1].position) > 2.5f)
            { Fail("peer continuity, role, spawn, controls or camera"); yield break; }
            Debug.Log("PASS independent castle transition on " + (host ? "host" : "client"));
        }
    }
}
#endif
