#if HMH_STORE_QA
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blocks.Gameplay.Core
{
    internal class Store005QaProbe : MonoBehaviour
    {
        [Serializable] class Auth { public string accessToken; public string userId; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Begin() => new GameObject("Store QA").AddComponent<Store005QaProbe>();
        IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            Application.targetFrameRate = 30;
            var args = Environment.GetCommandLineArgs();
            var folder = args[Array.IndexOf(args,"-skinTestFolder")+1];
            var scene = args[Array.IndexOf(args,"-skinScene")+1];
            bool host = args.Contains("-skinHost");
            string peer = host ? "host" : "client";
            string marker = Path.Combine(folder,scene);
            var auth=JsonUtility.FromJson<Auth>(File.ReadAllText(Path.Combine(folder,"store005-"+peer+".json")));
            typeof(ApiClient).GetField("_accessToken",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,auth.accessToken);
            typeof(ApiClient).GetProperty("UserId").SetValue(null,auth.userId);
            ApiClient.SetBaseUrl("http://127.0.0.1:27184/api");
            // Presence marks a Lobby multiplayer launch for the Tutorial's solo fallback.
            gameObject.AddComponent<AutoSessionStarter>().enabled=false;
            Lobby3DManager.AmIDad=host;
            yield return SceneManager.LoadSceneAsync(scene);
            var net=NetworkManager.Singleton;
            var transport=net.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1",17798,"127.0.0.1");
            if (!(host ? net.StartHost() : net.StartClient())) { Debug.LogError("SKIN005 FAIL startup"); Application.Quit(2); yield break; }
            Debug.Log("SKIN005 START "+peer+" "+scene);
            var deadline=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<deadline)
            {
                var actors=FindObjectsByType<PlayerSkinSync>(FindObjectsInactive.Exclude);
                if(actors.Length==2 && actors.Count(a=>a.IsOwner)==1 && actors.All(Check))
                {
                    Debug.Log("SKIN005 PASS "+peer+" "+scene+" local and remote renderer, avatar, head, hips, colliders");
                    File.WriteAllText(marker+"-"+peer+".pass","PASS");
                    while(!File.Exists(marker+"-"+(host?"client":"host")+".pass")&&Time.realtimeSinceStartup<deadline)yield return null;
                    yield return new WaitForSecondsRealtime(host?2:1);
                    Application.Quit(0); yield break;
                }
                yield return new WaitForSecondsRealtime(.25f);
            }
            Debug.LogError("SKIN005 FAIL "+peer+" "+scene+" actor count "+FindObjectsByType<PlayerSkinSync>().Length);
            Application.Quit(3);
        }
        static bool Check(PlayerSkinSync actor)
        {
            var meshes=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            if(meshes.Length!=2 || !actor.GetComponentsInChildren<Collider>().Any(c=>c.enabled))return false;
            var original=meshes.FirstOrDefault(m=>m.forceRenderingOff);
            var visual=meshes.FirstOrDefault(m=>!m.forceRenderingOff&&m.enabled);
            if(!original || !visual)return false;
            var originalRig=original.GetComponentInParent<Animator>();
            var visualRig=visual.GetComponentInParent<Animator>();
            bool dad=actor.OwnerClientId==NetworkManager.ServerClientId;
            var correctMat=dad?"M_Dad_Skin1":"child1";
            return visual.sharedMaterial.name==correctMat && visual.sharedMaterial.GetTexture("_BaseMap") &&
                visualRig.avatar.isValid && visualRig.avatar.isHuman &&
                Vector3.Distance(originalRig.GetBoneTransform(HumanBodyBones.Hips).position,visualRig.GetBoneTransform(HumanBodyBones.Hips).position)<.06f &&
                Vector3.Distance(originalRig.GetBoneTransform(HumanBodyBones.Head).position,visualRig.GetBoneTransform(HumanBodyBones.Head).position)<.35f;
        }
    }
}
#endif
