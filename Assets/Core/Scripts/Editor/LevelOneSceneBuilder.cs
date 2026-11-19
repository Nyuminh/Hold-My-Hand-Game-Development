using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Blocks.Gameplay.Core.LevelOneAuthoring
{
    /// <summary>Repeatable authoring of LVL1 objects only. Existing environment assets remain intact.</summary>
    public static class LevelOneSceneBuilder
    {
        public const string ScenePath = "Assets/Core/Scenes/PlayScene3.unity";
        public static GameObject Root;
        public static ChaseBossController Boss;
        public static Terrain Ground;

        public static Vector3 Surface(float x, float z) => new Vector3(x, Ground.SampleHeight(new Vector3(x, 0, z)) + Ground.transform.position.y + 0.08f, z);

        public static Transform Marker(string name, Transform parent, Vector3 position)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            return obj.transform;
        }

        public static GameObject Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material = null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = size;
            if (material != null) obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        public static Material Material(string name, Color color)
        {
            const string folder = "Assets/Core/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Core", "Materials");
            var path = folder + "/LVL1_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color;
            return mat;
        }

        [MenuItem("Hold My Hand/Level 1/Build Boss And Route")]
        public static void BuildBoss()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Build only in edit mode.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            if (scene.GetRootGameObjects().Any(go => go.name == "LVL1_Encounter"))
                throw new System.InvalidOperationException("LVL1 already exists. Edit existing objects instead of overwriting them.");
            Ground = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Terrain>()).First();
            Root = new GameObject("LVL1_Encounter");
            Root.AddComponent<NetworkObject>();
            var route = Root.AddComponent<ChaseWaypointRoute>();
            var points = new[] {
                new Vector2(98,-80),new Vector2(100,-55),new Vector2(104,-25),new Vector2(113,0),
                new Vector2(125,22),new Vector2(129,48),new Vector2(132,78),new Vector2(130,102),
                new Vector2(122,130),new Vector2(110,151),new Vector2(105,176),new Vector2(98,202),
                new Vector2(94,225),new Vector2(96,250),new Vector2(101,270),new Vector2(111,287),
                new Vector2(130,307),new Vector2(150,325),new Vector2(166,334),new Vector2(199,334),
                new Vector2(228,330),new Vector2(256,326),new Vector2(283,317),new Vector2(296,310),
                new Vector2(306,320),new Vector2(312,326)
            };
            var routeRoot = Marker("WaypointRoute", Root.transform, Vector3.zero);
            route.waypoints = points.Select((p, i) => Marker("Waypoint_" + i.ToString("00"), routeRoot, Surface(p.x, p.y))).ToArray();
            route.Rebuild();
            var proxy = new GameObject("Pfb_ChaseBoss_Proxy");
            var mat = Material("Boss", new Color(0.26f, 0.12f, 0.09f));
            Box("Torso", proxy.transform, new Vector3(0, 3.4f, 0), new Vector3(3.6f, 4f, 2f), mat);
            Box("Head", proxy.transform, new Vector3(0, 6.3f, 0.3f), new Vector3(2.4f, 1.8f, 2.2f), mat);
            Box("LeftArm", proxy.transform, new Vector3(-2.5f, 3f, 0), new Vector3(1.2f, 3.5f, 1.2f), mat);
            Box("RightArm", proxy.transform, new Vector3(2.5f, 3f, 0), new Vector3(1.2f, 3.5f, 1.2f), mat);
            foreach (var col in proxy.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(col);
            var collider = proxy.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 3.5f; collider.height = 7f; collider.radius = 1.4f; collider.isTrigger = true;
            var rb = proxy.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var audio = proxy.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1f;
            audio.minDistance = 2f; audio.maxDistance = 18f; audio.pitch = 0.55f;
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Platformer/Audio/Clips/RotatingObstacleImpact.wav");
            var prefab = PrefabUtility.SaveAsPrefabAsset(proxy, "Assets/Core/Prefabs/Pfb_ChaseBoss_Proxy.prefab");
            Object.DestroyImmediate(proxy);
            proxy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Root.transform);
            Boss = Root.AddComponent<ChaseBossController>();
            Root.AddComponent<CampaignCheckpointController>().level = 1;
            Boss.route = route; Boss.bossVisual = proxy.transform; Boss.footsteps = proxy.GetComponent<AudioSource>();
            proxy.transform.position = route.Sample(0, out var direction); proxy.transform.forward = direction;
            var cpRoot = Marker("Checkpoints", Root.transform, Vector3.zero);
            var cpPoints = new[] { new Vector2(103,-48),new Vector2(131,63),new Vector2(96,236),new Vector2(280,318),new Vector2(327,340) };
            Boss.checkpoints = cpPoints.Select((p, i) => {
                var center = Surface(p.x,p.y); route.Sample(route.Project(center),out var f);f.y=0;
                var dad = Marker("CP"+i+"_Dad",cpRoot,center+Vector3.Cross(Vector3.up,f.normalized)*0.9f);
                var child = Marker("CP"+i+"_Child",cpRoot,center-Vector3.Cross(Vector3.up,f.normalized)*0.9f);
                dad.forward=child.forward=f;
                return new ChaseBossController.Checkpoint {dad=dad,child=child,bossDistance=Mathf.Max(0,route.Project(center)-30f)};
            }).ToArray();
            var gm = scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<GameManager>()).First();
            var so = new SerializedObject(gm);
            so.FindProperty("autoRespawn").boolValue = false; // Team lifecycle belongs to the encounter in this scene.
            var spawns = so.FindProperty("spawnPoints");
            spawns.arraySize=2; spawns.GetArrayElementAtIndex(0).objectReferenceValue=Boss.checkpoints[0].dad;
            spawns.GetArrayElementAtIndex(1).objectReferenceValue=Boss.checkpoints[0].child;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("[LVL1] Boss route built: "+route.waypoints.Length+" points, "+route.Length+" metres.");
        }

        public static void FindEncounter()
        {
            var scene=SceneManager.GetSceneByPath(ScenePath);
            Root=scene.GetRootGameObjects().First(go=>go.name=="LVL1_Encounter");
            Boss=Root.GetComponent<ChaseBossController>();
            Ground=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Terrain>()).First();
        }

        public static T Obstacle<T>(string name, Vector3 position, Vector3 forward, Vector3 zoneSize, int checkpointIndex) where T : LevelOneObstacle
        {
            var root = Marker(name,Root.transform,position).gameObject;
            root.transform.forward = Vector3.ProjectOnPlane(forward,Vector3.up).normalized;
            root.AddComponent<NetworkObject>();
            var obstacle = root.AddComponent<T>(); obstacle.boss = Boss; obstacle.checkpointAfter = checkpointIndex;
            var zone = root.AddComponent<BoxCollider>(); zone.isTrigger = true; zone.size = zoneSize; zone.center = Vector3.up * 2;
            obstacle.paceZone = zone;
            var body = root.AddComponent<Rigidbody>();body.isKinematic = true;body.useGravity = false;
            obstacle.exit=Marker("Exit",root.transform,position+root.transform.forward*(zoneSize.z*0.5f-1));
            obstacle.exit.forward=root.transform.forward;
            return obstacle;
        }

        [MenuItem("Hold My Hand/Level 1/Build Obstacles")]
        public static void BuildObstacles()
        {
            FindEncounter();
            if (Root.GetComponentInChildren<FallenTreeObstacle>() != null) throw new System.InvalidOperationException("Obstacles already authored.");
            var wood=Material("Wood",new Color(0.27f,0.15f,0.07f));
            var stone=Material("Stone",new Color(0.25f,0.28f,0.27f));
            var mud=Material("Mud",new Color(0.16f,0.11f,0.055f));
            var tree=Obstacle<FallenTreeObstacle>("LVL1_FallenTree",Surface(130,48),Vector3.forward,new Vector3(18,8,22),1);
            tree.trunk=Box("FallenTrunk",tree.transform,new Vector3(0,0.8f,0),new Vector3(12,1.6f,1.6f),wood).transform;
            tree.dadGrip=Marker("DadGrip_E",tree.transform,tree.transform.TransformPoint(new Vector3(-2,0,-2.3f)));
            tree.childWedge=Marker("ChildWedge_E",tree.transform,tree.transform.TransformPoint(new Vector3(1.5f,0,2.5f)));
            Box("GripMarker",tree.transform,new Vector3(-2,0.1f,-2.3f),new Vector3(1.2f,0.2f,1.2f),stone);
            tree.wedge=Box("WedgeStone",tree.transform,new Vector3(1.5f,0.55f,1.5f),new Vector3(1.1f,1.1f,1.1f),stone);
            tree.wedge.SetActive(false);
            // Low crawl roof lifts with the trunk; the crawl volume prevents Dad entering while holding.
            var crawl=Marker("ChildCrawl",tree.transform,tree.transform.position).gameObject;
            var crawlCol=crawl.AddComponent<BoxCollider>();crawlCol.isTrigger=true;crawlCol.size=new Vector3(7,1.4f,4);crawlCol.center=Vector3.up*0.7f;
            tree.crawl=crawl.AddComponent<CoopCrawlVolume>();crawl.SetActive(false);
            for(int side=-1;side<=1;side+=2)Box("TreeSideBoulder",tree.transform,new Vector3(side*8,2,0),new Vector3(4,4,6),stone);

            var chasm=Obstacle<BrokenChasmObstacle>("LVL1_BrokenChasm",Surface(95,218),new Vector3(0,0,1),new Vector3(20,12,38),2);
            // Raised cliff proxies create a real six-metre unsupported span without modifying shared terrain data.
            for(int side=-1;side<=1;side+=2)
            {
                Box("CliffBank",chasm.transform,new Vector3(0,1.5f,side*5),new Vector3(10,3,4),stone);
                var ramp=Box("ApproachRamp",chasm.transform,new Vector3(0,1.45f,side*12),new Vector3(8,0.35f,11),wood);
                ramp.transform.localRotation=Quaternion.Euler(side*16.2f,0,0);
                Box("CliffSide",chasm.transform,new Vector3(side*7,2,0),new Vector3(4,4,9),stone);
            }
            chasm.throwPad=Marker("ThrowPad_F",chasm.transform,chasm.transform.TransformPoint(new Vector3(3.6f,3.15f,-4.3f)));
            chasm.landing=Marker("ChildLanding",chasm.transform,chasm.transform.TransformPoint(new Vector3(3.6f,3.15f,4.3f)));
            Box("ThrowPad",chasm.transform,new Vector3(3.6f,3.05f,-4.3f),new Vector3(1.8f,0.1f,2.3f),wood);
            Box("LandingPad",chasm.transform,new Vector3(3.6f,3.05f,4.3f),new Vector3(1.8f,0.1f,2.3f),wood);
            chasm.lever=Marker("Winch_E",chasm.transform,chasm.transform.TransformPoint(new Vector3(2.8f,3.1f,5)));
            Box("WinchPost",chasm.transform,new Vector3(2.8f,3.65f,5),new Vector3(0.4f,1.3f,0.4f),wood);
            Box("WinchHandle",chasm.transform,new Vector3(2.8f,4.2f,5),new Vector3(1.6f,0.2f,0.2f),stone);
            chasm.bridge=Marker("BridgePivot",chasm.transform,chasm.transform.TransformPoint(new Vector3(0,2.85f,-3.1f)));
            Box("BridgePlanks",chasm.bridge,new Vector3(0,0,3.1f),new Vector3(5,0.3f,6.2f),wood);
            chasm.bridge.localRotation=Quaternion.Euler(-82,0,0);
            var fall=Marker("FallRecoveryVolume",chasm.transform,chasm.transform.TransformPoint(new Vector3(0,-0.2f,0))).gameObject;
            chasm.fallZone=fall.AddComponent<BoxCollider>();chasm.fallZone.isTrigger=true;chasm.fallZone.size=new Vector3(10,4,5.8f);
            Box("ChasmFloor",chasm.transform,new Vector3(0,0.02f,0),new Vector3(10,0.03f,6),mud);
            // Route follows the top of the crossing rather than passing underneath the bank geometry.
            var points=Boss.route.waypoints.ToList();
            int insert=points.FindIndex(p=>p.position.z>=202 && p.position.z<225);
            foreach(var local in new[]{new Vector3(0,0,-18),new Vector3(0,3.15f,-6),new Vector3(0,3.15f,6),new Vector3(0,0,18)})
                points.Insert(++insert,Marker("ChasmRoute",Boss.route.waypoints[0].parent,chasm.transform.TransformPoint(local)));
            points.RemoveAll(p=>p.name=="Waypoint_12"); // Replace the point inside the span with crossing samples.
            Boss.route.waypoints=points.ToArray();Boss.route.Rebuild();

            var mudZone=Obstacle<MudslideRunTrigger>("LVL1_Mudslide",Surface(250,327),new Vector3(1,0,-0.18f),new Vector3(12,7,26),3);
            Box("MudSurface",mudZone.transform,new Vector3(0,0.03f,0),new Vector3(10,0.06f,24),mud);
            for(int side=-1;side<=1;side+=2)Box("MudBank",mudZone.transform,new Vector3(side*6,0.6f,0),new Vector3(2,1.2f,25),stone);
            for(int i=0;i<Boss.checkpoints.Length;i++)Boss.checkpoints[i].bossDistance=Mathf.Max(0,Boss.route.Project(Boss.checkpoints[i].dad.position)-30f);
            EditorSceneManager.MarkSceneDirty(Root.scene);EditorSceneManager.SaveScene(Root.scene);AssetDatabase.SaveAssets();
            Debug.Log("[LVL1] Tree, chasm, mud authored and connected to boss pace/checkpoints.");
        }

        [MenuItem("Hold My Hand/Level 1/Build Castle Climax")]
        public static void BuildClimax()
        {
            FindEncounter();
            if(Root.GetComponentInChildren<CastleGateClimaxManager>()!=null)throw new System.InvalidOperationException("Climax already authored.");
            var all=Root.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true));
            var castle=all.First(t=>t.name=="Castle_Fortress_v2");
            var front=castle.GetComponentsInChildren<Transform>().First(t=>t.name=="Front_Harrow_Portcullis");
            var center=front.GetComponent<Renderer>().bounds.center;center.y=2.05f;
            var forward=Vector3.ProjectOnPlane(castle.position-center,Vector3.up).normalized;
            var climax=Obstacle<CastleGateClimaxManager>("LVL1_CastleClimax",center,forward,new Vector3(28,20,44),4);
            climax.gateDriver=castle.GetComponents<MonoBehaviour>().First(c=>c is ILevelOneGateDriver);
            var wood=Material("Wood",new Color(0.27f,0.15f,0.07f));var stone=Material("Stone",new Color(0.25f,0.28f,0.27f));
            climax.blockers=Marker("PortcullisCollision",climax.transform,center);
            for(int i=0;i<2;i++)
            {
                var blocker=Box("SolidGate",climax.blockers,new Vector3(-1.0f,4.5f,i*6.4f),new Vector3(10.8f,9,0.45f),stone);
                blocker.GetComponent<Renderer>().enabled=false;
            }
            var drain=Marker("ChildDrain",climax.transform,climax.transform.TransformPoint(new Vector3(5.35f,0,2))).gameObject;
            drain.transform.rotation=climax.transform.rotation;
            var volume=drain.AddComponent<BoxCollider>();volume.isTrigger=true;volume.size=new Vector3(2.2f,1.4f,20.5f);volume.center=new Vector3(0,0.7f,0);
            climax.drain=drain.AddComponent<CoopCrawlVolume>();
            var crawlSo=new SerializedObject(climax.drain);crawlSo.FindProperty("crawlHeight").floatValue=0.75f;crawlSo.ApplyModifiedPropertiesWithoutUndo();
            Box("DrainFloor",drain.transform,new Vector3(0,-0.07f,0),new Vector3(2.2f,0.14f,16),stone);
            Box("DrainRoof",drain.transform,new Vector3(0,1.30f,0),new Vector3(2.2f,0.25f,16),stone);
            Box("DrainOuterWall",drain.transform,new Vector3(1.15f,0.75f,0),new Vector3(0.3f,1.5f,16),stone);
            var gRight=Box("DrainEntranceGuide_Right",drain.transform,new Vector3(1.35f,0.75f,-8.7f),new Vector3(0.3f,1.5f,1.8f),stone);
            gRight.transform.localRotation=Quaternion.Euler(0,-25f,0);
            var gLeft=Box("DrainEntranceGuide_Left",drain.transform,new Vector3(-1.15f,0.75f,-8.7f),new Vector3(0.3f,1.5f,1.8f),stone);
            gLeft.transform.localRotation=Quaternion.Euler(0,25f,0);
            var ramp=Box("DrainEntranceRamp",drain.transform,new Vector3(0,-0.05f,-8.6f),new Vector3(2.2f,0.10f,1.4f),stone);
            ramp.transform.localRotation=Quaternion.Euler(6f,0,0);
            climax.childLever=Marker("ChildLever_E",climax.transform,climax.transform.TransformPoint(new Vector3(3.6f,0,11)));
            Box("LeverPost",climax.transform,new Vector3(3.6f,0.65f,11),new Vector3(0.35f,1.3f,0.35f),wood);
            Box("LeverHandle",climax.transform,new Vector3(3.6f,1.2f,11),new Vector3(1.2f,0.2f,0.2f),stone);
            climax.dadGrip=Marker("DadHold_E",climax.transform,climax.transform.TransformPoint(new Vector3(0,0,-1.2f)));
            climax.slideEntry=Marker("ChildSlide_E",climax.transform,climax.transform.TransformPoint(new Vector3(1.2f,0,-3)));
            Box("HoldMarker",climax.transform,new Vector3(0,0.03f,-1.2f),new Vector3(2,0.06f,1.5f),wood);
            Box("SlideMarker",climax.transform,new Vector3(1.2f,0.03f,-3),new Vector3(1.2f,0.06f,1.2f),wood);
            climax.childSafe=Marker("ChildSafe",climax.transform,climax.transform.TransformPoint(new Vector3(1.2f,-0.5f,12)));
            climax.dadSafe=Marker("DadSafe",climax.transform,climax.transform.TransformPoint(new Vector3(-1,-0.5f,10)));
            climax.exit.position=climax.dadSafe.position;climax.exit.forward=forward;
            climax.nextLevelScene="PlayScene2";
            climax.impactAudio=climax.gameObject.AddComponent<AudioSource>();climax.impactAudio.playOnAwake=false;climax.impactAudio.spatialBlend=0.5f;climax.impactAudio.pitch=0.7f;
            climax.chainBreakClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Platformer/Audio/Clips/RotatingObstacleImpact.wav");
            climax.gateSlamClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Shooter/Audio/Clips/Weapons/Grenade Launcher/Explode/Grenade Explode-001.wav");
            climax.bossImpactClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Shooter/Audio/Clips/Weapons/Grenade Launcher/Explode/Grenade Explode-003.wav");
            // Boss stops physically outside the closed portcullis.
            Boss.route.waypoints[Boss.route.waypoints.Length-1].position=center-forward*1.6f;
            Boss.route.Rebuild();
            Boss.checkpoints[4].dad.position=climax.dadSafe.position;Boss.checkpoints[4].child.position=climax.childSafe.position;
            EditorSceneManager.MarkSceneDirty(Root.scene);EditorSceneManager.SaveScene(Root.scene);AssetDatabase.SaveAssets();
            Debug.Log("[LVL1] Castle climax built, continuation=PlayScene2.");
        }
    }
}
