using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;

namespace Blocks.Gameplay.Core.LevelTwoAuthoring
{
    /// <summary>
    /// Repeatable authoring of Level 2 (PlayScene2.unity) objects.
    /// Preserves existing environment assets intact while creating the complete
    /// 5-stage river expedition layout, docks, obstacles, and checkpoints,
    /// with professional architectural blockout, aesthetic details, signposts,
    /// and a fully integrated guiding and waypoint navigation arrow system.
    /// </summary>
    public static class LevelTwoSceneBuilder
    {
        public const string ScenePath = "Assets/Core/Scenes/PlayScene2.unity";
        public static GameObject Root;
        public static Terrain Ground;

        public static T Ensure<T>(GameObject obj) where T : Component
        {
            var component = obj.GetComponent<T>();
            return component != null ? component : obj.AddComponent<T>();
        }
        public static Transform Find(string name) => Root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        public static void FindMap()
        {
            if (SceneManager.GetActiveScene().path != ScenePath) throw new System.InvalidOperationException("Open PlayScene2 first.");
            Root = GameObject.Find("LVL2_RiverExpedition");
            Ground = Object.FindAnyObjectByType<Terrain>();
            if (Root == null) throw new System.InvalidOperationException("Build the river map first.");
        }

        public static void Feedback(GameObject obj, string clipPath, Color color, Vector3? position = null)
        {
            var fx = Ensure<LevelTwoFeedback>(obj);
            fx.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            var emitter = obj.transform.Find("FeedbackAudio");
            if (emitter == null) emitter = Marker("FeedbackAudio", obj.transform, position ?? obj.transform.position);
            emitter.position = position ?? obj.transform.position;
            var audio = Ensure<AudioSource>(emitter.gameObject); fx.emitter = audio;
            audio.playOnAwake = false; audio.spatialBlend = 1; audio.minDistance = 4; audio.maxDistance = 40;
            var child = obj.transform.Find("FeedbackParticles");
            if (child == null) child = Marker("FeedbackParticles", obj.transform, obj.transform.position);
            child.position = position ?? obj.transform.position;
            var particles = Ensure<ParticleSystem>(child.gameObject);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.playOnAwake = false; main.loop = false; main.duration = 0.5f;
            main.startLifetime = 0.6f; main.startSpeed = 2; main.startSize = 0.12f;
            main.startColor = color; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = particles.emission; emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 24) });
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            fx.particles = particles;
        }

        public static GameObject Trigger(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var existing = parent.Find(name);
            var obj = existing != null ? existing.gameObject : Marker(name, parent, position).gameObject;
            obj.transform.position = position; obj.transform.localScale = Vector3.one;
            var box = Ensure<BoxCollider>(obj); box.size = size; box.isTrigger = true;
            return obj;
        }

        [MenuItem("Hold My Hand/Level 2/Wire Stage 1")]
        public static void WireStageOne()
        {
            FindMap();
            var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
            boat.transform.rotation = Quaternion.Euler(-90, 180, 0);
            var settings = new SerializedObject(boat);
            settings.FindProperty("riverPhysicsEnabled").boolValue = true;
            settings.FindProperty("soloTestMode").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo();
            var body = Ensure<Rigidbody>(boat.gameObject);
            body.isKinematic = true; body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Ensure<NetworkObject>(boat.gameObject);
            Feedback(boat.gameObject, "Assets/Platformer/Audio/Clips/RotatingObstacleImpact.wav", Color.white);
            RiverSlalomGate previous = null;
            for (int i = 1; i <= 3; i++)
            {
                var gate = Ensure<RiverSlalomGate>(Find("SlalomGate_" + i + "_Trigger").gameObject);
                gate.boat = boat; gate.previous = previous; gate.gateNumber = i;
                Feedback(gate.gameObject, "Assets/Core/Audio/LVL2_Chime.wav", Color.yellow);
                previous = gate;
            }
            foreach (var t in Root.GetComponentsInChildren<Transform>())
            {
                if (t.name.Contains("HazardRock") || t.name.Contains("Cliff_")) Ensure<RiverRockHazard>(t.gameObject);
                // Decorative water surfaces must not ground the boat.
                if (t.name.StartsWith("Water_") && t.TryGetComponent<Collider>(out var water)) water.enabled = false;
            }
            var current = Ensure<RiverCurrentZone>(Find("Rapids_CurrentZone").gameObject);
            current.acceleration = new Vector3(0, 0, 2.0f);
            Ensure<RiverCurrentZone>(Trigger("Slalom_Current", Find("02_SlalomCourse_Stage1"), new Vector3(100, 0.8f, 52), new Vector3(18, 3, 30))).acceleration = new Vector3(0, 0, 0.8f);

            // Attach runtime river carver so terrain is carved in-memory without a 125MB binary asset in Git
            Ensure<LevelTwoRiverCarver>(Root);
            ClearRiverVegetation();
            // Continuous visible water covers gaps between the original stage planes.
            if (Root.transform.Find("Water_RiverContinuous") == null)
            {
                var water = GameObject.CreatePrimitive(PrimitiveType.Plane); water.name = "Water_RiverContinuous";
                water.transform.SetParent(Root.transform, false); water.transform.position = new Vector3(100, 0.5f, 143);
                water.transform.localScale = new Vector3(2.6f, 1, 24);
                water.GetComponent<Renderer>().sharedMaterial = Find("Water_Rapids_1").GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(water.GetComponent<Collider>());
            }

            // Wire Guiding Director
            WireGuidingDirector();

            Save();
        }

        public static void Save()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        public static void ClearRiverVegetation()
        {
            var colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            foreach (var c in colliders)
            {
                if (c is TerrainCollider) continue;
                if (Root != null && c.transform.IsChildOf(Root.transform)) continue;
                var p = c.transform.position;
                if (p.z >= 24f && p.z <= 265f && Mathf.Abs(p.x - 100f) <= 7.0f && p.y <= 1.8f)
                {
                    c.enabled = false;
                }
            }
        }

        public static void Dock(string name, Vector3 position, Vector3 dad, Vector3 child)
        {
            var obj = Root.transform.Find(name);
            if (obj == null) obj = Marker(name, Root.transform, position);
            var dock = Ensure<LevelTwoDock>(obj.gameObject);
            dock.dadLanding = obj.Find("Dad") ?? Marker("Dad", obj, dad);
            dock.childLanding = obj.Find("Child") ?? Marker("Child", obj, child);
        }

        [MenuItem("Hold My Hand/Level 2/Wire Stages 2 and 3")]
        public static void WireStageTwo()
        {
            FindMap();
            var stage = Find("03_SluiceGate_Stage2");
            var sluice = Ensure<SluiceGateObstacle>(stage.gameObject);
            sluice.gate = Find("SluiceGate_IronPortcullis"); sluice.winch = Find("Dad_Winch_Station"); sluice.valve = Find("WaterValve_Lever");
            sluice.lastSlalom = Find("SlalomGate_3_Trigger").GetComponent<RiverSlalomGate>();
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Wood.mat");
            var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Stone.mat");
            var iron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Iron.mat");
            // The original solid tower intersected the entire crawlway. Keep its upper visual,
            // and author a real 0.5 m passage with floor, roof and a lifting door underneath.
            var tower = Find("Sluice_RightTower"); tower.position = new Vector3(109.5f, 5f, 74); tower.localScale = new Vector3(4, 6, 4.5f);
            var conduit = Find("Child_Crawlway_Conduit");
            conduit.position = new Vector3(109.5f, 1.35f, 74); conduit.localScale = Vector3.one;
            conduit.GetComponent<Renderer>().enabled = false;
            sluice.crawl = conduit.GetComponent<BoxCollider>(); sluice.crawl.size = new Vector3(1.2f, 0.6f, 4.5f);
            var crawl = new SerializedObject(conduit.GetComponent<CoopCrawlVolume>());
            crawl.FindProperty("crawlHeight").floatValue = 0.45f; crawl.ApplyModifiedPropertiesWithoutUndo();
            if (stage.Find("Crawl_Floor") == null)
            {
                Box("Crawl_Floor", stage, new Vector3(109.5f, 0.95f, 74), new Vector3(1.5f, 0.25f, 6), stone);
                Box("Crawl_Roof", stage, new Vector3(109.5f, 1.85f, 74), new Vector3(1.5f, 0.55f, 4.5f), stone);
                Box("Crawl_LeftWall", stage, new Vector3(108.65f, 1.5f, 74), new Vector3(0.5f, 1.5f, 4.5f), stone);
                Box("Crawl_RightWall", stage, new Vector3(110.35f, 1.5f, 74), new Vector3(0.5f, 1.5f, 4.5f), stone);
                Box("Crawl_LiftingDoor", stage, new Vector3(109.5f, 1.3f, 73.8f), new Vector3(1.2f, 0.5f, 0.15f), iron);
                Box("Valve_ChamberFloor", stage, new Vector3(109.5f, 0.95f, 78), new Vector3(5, 0.25f, 4), wood);
            }
            sluice.crawlDoor = Find("Crawl_LiftingDoor");
            Find("Sluice_SideDock").localScale = new Vector3(7, 0.25f, 5);
            sluice.valve.position = new Vector3(109.5f, 1.7f, 78);
            CampaignRecoveryAuthoring.FixPassage(sluice);
            Feedback(stage.gameObject, "Assets/Core/Audio/LVL2_Water.wav", Color.white, sluice.gate.position);
            var boat = Object.FindAnyObjectByType<CoopWoodenBoat>();
            for (int i = 1; i <= 2; i++)
            {
                var vortex = Find("Whirlpool_" + i + "_Vortex");
                var whirl = Ensure<WhirlpoolHazard>(vortex.gameObject); whirl.boat = boat; whirl.rotationSign = i == 1 ? 1 : -1;
                vortex.GetComponent<Collider>().enabled = false;
                Find("Whirlpool_" + i + "_Ring").GetComponent<Collider>().enabled = false;
                Feedback(vortex.gameObject, "Assets/Core/Audio/LVL2_Water.wav", Color.white);
                string coreName = "Whirlpool_" + i + "_CoreRock";
                if (Find("04_RapidsWhirlpools_Stage3").Find(coreName) == null)
                    Box(coreName, Find("04_RapidsWhirlpools_Stage3"), new Vector3(vortex.position.x, 0.9f, vortex.position.z), new Vector3(1.2f, 1.8f, 1.2f), stone).AddComponent<RiverRockHazard>();
            }
            Dock("Start_DockLanding", new Vector3(96, 0.5f, 31), new Vector3(94, 1.15f, 30), new Vector3(94, 1.15f, 32));
            Dock("Sluice_DockLanding", new Vector3(106, 0.5f, 70), new Vector3(108, 1.15f, 70), new Vector3(109.5f, 1.15f, 71));

            WireGuidingDirector();

            Save();
        }

        [MenuItem("Hold My Hand/Level 2/Wire Beacons Watermill and Victory")]
        public static void WireStageThree()
        {
            FindMap();
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Wood.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Gold.mat");
            var stage4 = Find("05_MistyMarsh_Stage4");
            var beacons = new MarshBeaconTrigger[2];
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "West" : "East";
                float x = i == 0 ? 90 : 110, z = i == 0 ? 175 : 190;
                string rootName = side + "_BeaconGameplay";
                var root = stage4.Find(rootName) ?? Marker(rootName, stage4, Vector3.zero);
                var beacon = Ensure<MarshBeaconTrigger>(root.gameObject); beacons[i] = beacon;
                // Launch from an adjacent dock onto a raised lily; the crystal can only be
                // collected after the real BoostThrow launch and a grounded landing.
                Vector3 pad = new Vector3(i == 0 ? 97 : 103, 1.15f, z);
                if (root.Find("ThrowPlatform") == null) Box("ThrowPlatform", root, pad - Vector3.up * 0.2f, new Vector3(3.5f, 0.3f, 4), wood);
                beacon.throwPad = root.Find("ThrowPad") ?? Marker("ThrowPad", root, pad);
                var lily = Find("GiantLilyPad_" + (i + 1)); lily.position = new Vector3(x, 2.4f, z);
                lily.localScale = new Vector3(4, 0.2f, 4);
                // Unity's primitive Cylinder uses a CapsuleCollider: flattening its Y scale
                // leaves a large rounded collision dome. Static pads need their actual mesh.
                foreach (var surface in new[] { lily, Find(side + "Beacon_Island") })
                {
                    if (surface.TryGetComponent<CapsuleCollider>(out var capsule)) Object.DestroyImmediate(capsule);
                    Ensure<MeshCollider>(surface.gameObject).sharedMesh = surface.GetComponent<MeshFilter>().sharedMesh;
                }
                beacon.landing = root.Find("Landing") ?? Marker("Landing", root, new Vector3(x, 2.65f, z));
                GameObject crystal;
                if (i == 0) crystal = Find("LuminaCrystal_Gem").gameObject;
                else crystal = root.Find("LuminaCrystal_East") != null ? root.Find("LuminaCrystal_East").gameObject : Box("LuminaCrystal_East", root, new Vector3(x, 3.4f, z), new Vector3(0.5f, 0.6f, 0.5f), gold);
                crystal.transform.position = new Vector3(x, 3.4f, z); crystal.GetComponent<Collider>().enabled = false; beacon.crystal = crystal;
                beacon.flame = Find(side + "Beacon_Flame").gameObject; beacon.flame.GetComponent<Collider>().enabled = false;
                beacon.beaconLight = Ensure<Light>(beacon.flame); beacon.beaconLight.type = LightType.Point;
                beacon.beaconLight.color = new Color(1, 0.85f, 0.4f); beacon.beaconLight.range = 35; beacon.beaconLight.intensity = 6;
                beacon.flame.SetActive(false);
                Feedback(root.gameObject, "Assets/Core/Audio/LVL2_Chime.wav", Color.yellow, beacon.landing.position);
                Dock(side + "_MarshDock", pad - Vector3.up * 0.6f, pad + Vector3.back * 0.7f, pad + Vector3.forward * 0.7f);
                // Return steps prevent the Child being stranded after collecting a crystal.
                for (int j = 1; j <= 3; j++)
                {
                    string name = side + "_ReturnStep_" + j;
                    if (root.Find(name) == null)
                    {
                        var point = Vector3.Lerp(new Vector3(x, 2.4f, z + 3), new Vector3(pad.x, 0.95f, z + 3), j / 3f);
                        Box(name, root, point, new Vector3(2.4f, 0.25f, 2), wood);
                    }
                }
            }
            var stage5 = Find("06_WatermillHaven_Stage5");
            var controller = stage5.Find("WatermillController") ?? Marker("WatermillController", stage5, new Vector3(100, 0.5f, 225));
            var mill = Ensure<GiantWatermillObstacle>(controller.gameObject);
            mill.boat = Object.FindAnyObjectByType<CoopWoodenBoat>(); mill.west = beacons[0]; mill.east = beacons[1];
            mill.wheel = Find("GiantWatermill_Assembly"); mill.wheel.position = new Vector3(100, 5.8f, 225); mill.wheel.rotation = Quaternion.identity;
            var hub = Find("Watermill_Hub"); hub.localRotation = Quaternion.Euler(0, 0, 90); hub.localScale = new Vector3(1.2f, 8, 1.2f);
            var blades = mill.wheel.Cast<Transform>().Where(t => t.name.StartsWith("Paddle_Blade")).ToArray();
            for (int i = 0; i < blades.Length; i++)
            {
                float angle = i * 90;
                blades[i].localPosition = new Vector3(0, Mathf.Cos(angle * Mathf.Deg2Rad) * 4.3f, Mathf.Sin(angle * Mathf.Deg2Rad) * 4.3f);
                blades[i].localRotation = Quaternion.Euler(angle, 0, 0); blades[i].localScale = new Vector3(15, 1.8f, 0.45f);
                Ensure<RiverRockHazard>(blades[i].gameObject);
            }
            var rb = Ensure<Rigidbody>(mill.wheel.gameObject); rb.isKinematic = true; rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (controller.Find("Watermill_SafetyBoom") == null)
            {
                var boom = Box("Watermill_SafetyBoom", controller, new Vector3(0, 0.5f, -4), new Vector3(17, 0.6f, 0.5f), wood);
                Ensure<RiverRockHazard>(boom);
            }
            mill.safetyBoom = controller.Find("Watermill_SafetyBoom");
            var climax = Ensure<LevelTwoClimaxManager>(Find("LevelTwo_VictoryTrigger").gameObject);
            Ensure<CampaignCheckpointController>(mill.boat.gameObject).level = 2;
            climax.boat = mill.boat; climax.watermill = mill; climax.west = beacons[0]; climax.east = beacons[1];
            climax.altar = Find("AltarOfDawn_Podium");
            climax.harbor = Trigger("Haven_Arrival", stage5, new Vector3(100, 0.7f, 242), new Vector3(14, 3, 7)).GetComponent<BoxCollider>();
            Feedback(climax.gameObject, "Assets/Core/Audio/LVL2_Chime.wav", Color.yellow);
            if (stage5.Find("Haven_AltarWalkway") == null)
                Box("Haven_AltarWalkway", stage5, new Vector3(100, 0.95f, 253), new Vector3(8, 0.3f, 10), wood);
            Dock("Haven_DockLanding", new Vector3(100, 0.5f, 244), new Vector3(99, 1.25f, 248), new Vector3(101, 1.25f, 248));
            // The new scene needs a build entry for Netcode scene loading and packaged builds.
            var scenes = EditorBuildSettings.scenes.ToList();
            if (!scenes.Any(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            else scenes.First(s => s.path == ScenePath).enabled = true;
            EditorBuildSettings.scenes = scenes.ToArray();
            // Unity 6 can override the global scene list in an active Build Profile.
            // Its in-memory scene list is not persisted unless the profile asset is dirty.
            foreach (var guid in AssetDatabase.FindAssets("t:BuildProfile"))
            {
                var profile = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null) EditorUtility.SetDirty(profile);
            }

            WireGuidingDirector();

            Save();
        }

        public static void WireGuidingDirector()
        {
            var directorObj = GameObject.Find("LVL2_GuidingDirector");
            if (directorObj == null)
            {
                directorObj = new GameObject("LVL2_GuidingDirector");
                directorObj.transform.position = Vector3.zero;
            }
            // Dọn sạch vòng tròn dưới đất cũ nếu có
            var oldRing = GameObject.Find("LVL2_WaypointPulseRing");
            if (oldRing != null) Object.DestroyImmediate(oldRing);

            var director = Ensure<LevelTwoGuidingDirector>(directorObj);
            director.AutoFindReferences();
            director.EnsureWaypointVisuals();
            director.UpdateWaypointTarget(true);
        }

        public static Vector3 Surface(float x, float z)
        {
            if (Ground == null) return new Vector3(x, 1f, z);
            return new Vector3(x, Ground.SampleHeight(new Vector3(x, 0, z)) + Ground.transform.position.y + 0.05f, z);
        }

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

        public static GameObject Cylinder(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material = null)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = size;
            if (material != null) obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        /// <summary>
        /// Tạo Tảng đá Tự Nhiên từ 3D Asset Prefab có sẵn trong project thay cho hình hộp vuông
        /// </summary>
        public static GameObject SpawnRock(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotation, string rockPrefab = "Rock_1")
        {
            GameObject prefab = null;
            if (rockPrefab.StartsWith("Rocks_Pile") || rockPrefab.StartsWith("Road_Rocks"))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Prefabs/Rocks_Variation/" + rockPrefab + ".prefab");
            }
            else if (rockPrefab.StartsWith("Rock_Small"))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Prefabs/Rocks_Small/" + rockPrefab + ".prefab");
            }
            else
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Prefabs/Rocks/" + rockPrefab + ".prefab");
            }

            GameObject obj = null;
            if (prefab != null)
            {
                obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                obj.name = name;
                obj.transform.localPosition = localPosition;
                obj.transform.localScale = localScale;
                obj.transform.localRotation = Quaternion.Euler(rotation);
            }
            else
            {
                obj = Box(name, parent, localPosition, localScale, Material("Stone", new Color(0.48f, 0.50f, 0.52f)));
                obj.transform.localRotation = Quaternion.Euler(rotation);
            }

            // Đảm bảo có Collider chuẩn
            var cols = obj.GetComponentsInChildren<Collider>();
            if (cols.Length == 0)
            {
                var mf = obj.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var mc = obj.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
                else
                {
                    obj.AddComponent<BoxCollider>();
                }
            }
            Ensure<RiverRockHazard>(obj);
            return obj;
        }

        /// <summary>
        /// Tạo Cụm Hoa Sen & Lá Sen Tự Nhiên trên mặt nước thay cho hình trụ tròn
        /// </summary>
        public static GameObject SpawnWaterLily(string name, Transform parent, Vector3 localPosition, Vector3 localScale, bool withFlower = true)
        {
            var root = Marker(name, parent, localPosition).gameObject;
            var liefPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Prefabs/WaterLily/WaterLily_Lief.prefab");
            if (liefPrefab != null)
            {
                var lief = (GameObject)PrefabUtility.InstantiatePrefab(liefPrefab, root.transform);
                lief.name = "LilyLeaf";
                lief.transform.localPosition = Vector3.zero;
                lief.transform.localScale = localScale;
                
                var mf = lief.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var mc = lief.GetComponentInChildren<MeshCollider>();
                    if (mc == null) mc = lief.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                else
                {
                    var col = lief.AddComponent<BoxCollider>();
                    col.size = new Vector3(3f, 0.1f, 3f);
                }
            }
            else
            {
                Cylinder("LilyPad_Fallback", root.transform, Vector3.zero, localScale, Material("BuoyGreen", new Color(0.12f, 0.78f, 0.28f)));
            }

            if (withFlower)
            {
                var flowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Prefabs/WaterLily/WaterLily_Flower_Opened.prefab");
                if (flowerPrefab != null)
                {
                    var flower = (GameObject)PrefabUtility.InstantiatePrefab(flowerPrefab, root.transform);
                    flower.name = "LilyFlower";
                    flower.transform.localPosition = new Vector3(0.5f * localScale.x, 0.08f, -0.4f * localScale.z);
                    flower.transform.localScale = localScale * 0.65f;
                }
            }
            return root;
        }

        /// <summary>
        /// Tạo Cầu gỗ cổ điển / bến đỗ gỗ với lan can
        /// </summary>
        public static GameObject SpawnWoodenBridge(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Vector3 rotation)
        {
            var bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CastlePack/Prefabs/WoodenBridgeHanddrails.prefab");
            if (bridgePrefab == null) bridgePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CastlePack/Prefabs/WoodenBridge.prefab");
            if (bridgePrefab != null)
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(bridgePrefab, parent);
                obj.name = name;
                obj.transform.localPosition = localPosition;
                obj.transform.localScale = localScale;
                obj.transform.localRotation = Quaternion.Euler(rotation);
                return obj;
            }
            return null;
        }

        /// <summary>
        /// Tạo Cột đèn lồng ấm áp cổ điển có Point Light chiếu rọi
        /// </summary>
        public static GameObject LanternPost(string name, Transform parent, Vector3 position, Material woodMat, Material goldMat)
        {
            var root = Marker(name, parent, position).gameObject;
            // Cột gỗ đứng
            Cylinder("Post", root.transform, new Vector3(0, 1.2f, 0), new Vector3(0.25f, 2.4f, 0.25f), woodMat);
            // Tay đòn ngang vươn ra
            Box("Arm", root.transform, new Vector3(0.4f, 2.2f, 0), new Vector3(0.9f, 0.15f, 0.15f), woodMat);
            // Lồng đèn vàng
            var lantern = Box("Lantern", root.transform, new Vector3(0.75f, 1.9f, 0), new Vector3(0.35f, 0.45f, 0.35f), goldMat);
            var light = lantern.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.85f, 0.45f);
            light.range = 10f;
            light.intensity = 2.5f;

            foreach (var c in root.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            return root;
        }

        /// <summary>
        /// Tạo Biển báo gỗ chỉ đường có mũi tên chạm khắc chỉ hướng
        /// </summary>
        public static GameObject Signpost(string name, Transform parent, Vector3 position, Vector3 rotation, Material woodMat, Material goldMat, bool arrowPointsForward = true)
        {
            var root = Marker(name, parent, position).gameObject;
            root.transform.localRotation = Quaternion.Euler(rotation);
            // Cột biển báo
            Cylinder("Post", root.transform, new Vector3(0, 0.9f, 0), new Vector3(0.2f, 1.8f, 0.2f), woodMat);
            // Bảng gỗ
            Box("Board", root.transform, new Vector3(0, 1.5f, 0), new Vector3(1.2f, 0.55f, 0.12f), woodMat);
            // Biểu tượng mũi tên trên biển báo
            var arrowShaft = Box("ArrowShaft", root.transform, new Vector3(0, 1.5f, 0.08f), new Vector3(0.5f, 0.12f, 0.06f), goldMat);
            var arrowHead = Box("ArrowHead", root.transform, new Vector3(arrowPointsForward ? 0.28f : -0.28f, 1.5f, 0.08f), new Vector3(0.2f, 0.25f, 0.06f), goldMat);
            arrowHead.transform.localRotation = Quaternion.Euler(0, 0, arrowPointsForward ? -45 : 45);

            foreach (var c in root.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            return root;
        }

        /// <summary>
        /// Tạo Phao nổi chỉ luồng sông hình mũi tên (Floating River Arrow Buoy)
        /// </summary>
        public static GameObject FloatingRiverArrow(string name, Transform parent, Vector3 position, float yRotation, Material woodMat, Material goldMat)
        {
            var root = Marker(name, parent, position).gameObject;
            root.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
            // Thân phao chữ V
            var shaft = Box("ArrowShaft", root.transform, new Vector3(0, 0.05f, -0.4f), new Vector3(0.5f, 0.15f, 1.4f), woodMat);
            var headL = Box("ArrowHead_L", root.transform, new Vector3(-0.45f, 0.06f, 0.4f), new Vector3(0.35f, 0.16f, 0.9f), goldMat);
            headL.transform.localRotation = Quaternion.Euler(0, -35, 0);
            var headR = Box("ArrowHead_R", root.transform, new Vector3(0.45f, 0.06f, 0.4f), new Vector3(0.35f, 0.16f, 0.9f), goldMat);
            headR.transform.localRotation = Quaternion.Euler(0, 35, 0);

            foreach (var c in root.GetComponentsInChildren<Collider>()) c.isTrigger = true;
            return root;
        }

        public static Material Material(string name, Color color, float metallic = 0f, float smoothness = 0.4f)
        {
            const string folder = "Assets/Core/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/Core", "Materials");
            var path = folder + "/LVL2_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = color;
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        [MenuItem("Hold My Hand/Level 2/Build Complete River Map")]
        public static void BuildMap()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Build only in edit mode.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            // Clean up existing LVL2_RiverExpedition if rebuilding
            var existing = scene.GetRootGameObjects().FirstOrDefault(go => go.name == "LVL2_RiverExpedition");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            Ground = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<Terrain>()).FirstOrDefault();
            Root = new GameObject("LVL2_RiverExpedition");

            // Load or create materials
            var woodMat = Material("Wood", new Color(0.42f, 0.28f, 0.16f), 0.05f, 0.2f);
            var stoneMat = Material("Stone", new Color(0.48f, 0.50f, 0.52f), 0.1f, 0.15f);
            var ironMat = Material("Iron", new Color(0.20f, 0.22f, 0.25f), 0.85f, 0.65f);
            var buoyRedMat = Material("BuoyRed", new Color(0.88f, 0.18f, 0.14f), 0.2f, 0.7f);
            var buoyGreenMat = Material("BuoyGreen", new Color(0.12f, 0.78f, 0.28f), 0.2f, 0.7f);
            var goldMat = Material("Gold", new Color(0.95f, 0.76f, 0.15f), 0.7f, 0.8f);
            var glowstoneMat = Material("Glowstone", new Color(0.2f, 0.85f, 0.95f), 0.2f, 0.8f);
            if (glowstoneMat.HasProperty("_EmissionColor"))
            {
                glowstoneMat.EnableKeyword("_EMISSION");
                glowstoneMat.SetColor("_EmissionColor", new Color(0.2f, 0.85f, 0.95f) * 2.2f);
            }
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Art/Materials/Water/WaterBasic.mat");
            if (waterMat == null) waterMat = Material("RiverWater", new Color(0.18f, 0.45f, 0.62f, 0.85f), 0.1f, 0.9f);

            // ==========================================
            // 1. START DOCK & SPAWN AREA (Z: 24 - 36)
            // ==========================================
            var startArea = Marker("01_StartDock_Area", Root.transform, Vector3.zero);
            // Wooden Pier
            var dockDeck = Box("StartDock_Pier", startArea, new Vector3(93.5f, 0.95f, 30.0f), new Vector3(4.5f, 0.25f, 9.0f), woodMat);
            // Support pilings (set isTrigger = true to prevent blocking boat BoxCast)
            Cylinder("Piling_1", startArea, new Vector3(91.5f, 0.3f, 26.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat).GetComponent<Collider>().isTrigger = true;
            Cylinder("Piling_2", startArea, new Vector3(95.5f, 0.3f, 26.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat).GetComponent<Collider>().isTrigger = true;
            Cylinder("Piling_3", startArea, new Vector3(91.5f, 0.3f, 34.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat).GetComponent<Collider>().isTrigger = true;
            Cylinder("Piling_4", startArea, new Vector3(95.5f, 0.3f, 34.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat).GetComponent<Collider>().isTrigger = true;
            // Mooring Bollards
            Cylinder("Bollard_1", startArea, new Vector3(95.5f, 1.25f, 32.0f), new Vector3(0.25f, 0.4f, 0.25f), ironMat).GetComponent<Collider>().isTrigger = true;
            Cylinder("Bollard_2", startArea, new Vector3(95.5f, 1.25f, 34.0f), new Vector3(0.25f, 0.4f, 0.25f), ironMat).GetComponent<Collider>().isTrigger = true;

            // NÂNG CẤP KIẾN TRÚC START DOCK:
            // Cầu dốc thoai thoải nối bờ cát với sàn cầu cảng (Ramp)
            var ramp = Box("StartDock_Ramp", startArea, new Vector3(93.5f, 0.85f, 24.5f), new Vector3(4.0f, 0.2f, 3.2f), woodMat);
            ramp.transform.localRotation = Quaternion.Euler(6f, 0, 0);

            // Lan can bảo vệ hai bên mép dock (Railings)
            Box("StartDock_Railing_Left", startArea, new Vector3(91.2f, 1.45f, 30.0f), new Vector3(0.18f, 0.85f, 8.8f), woodMat);
            Box("StartDock_Railing_Right_Back", startArea, new Vector3(95.8f, 1.45f, 27.5f), new Vector3(0.18f, 0.85f, 3.8f), woodMat);
            // Cầu gỗ cổ điển nối dài bến tàu
            SpawnWoodenBridge("StartDock_WoodenPier", startArea, new Vector3(93.5f, 0.95f, 30.0f), new Vector3(1.1f, 1.0f, 1.2f), new Vector3(0, 90f, 0));
            // Cầu ván nhảy áp sát mạn thuyền (Gangway leading to boat - trigger để không kẹt thuyền)
            var gangway = Box("StartDock_Gangway", startArea, new Vector3(96.0f, 0.85f, 33.5f), new Vector3(1.2f, 0.15f, 2.0f), woodMat);
            gangway.transform.localRotation = Quaternion.Euler(0, 5f, -6f);
            gangway.GetComponent<Collider>().isTrigger = true;

            // 2 Cột đèn lồng ấm áp thắp sáng bến khởi hành
            LanternPost("StartDock_Lantern_1", startArea, new Vector3(91.5f, 0.95f, 25.5f), woodMat, goldMat);
            LanternPost("StartDock_Lantern_2", startArea, new Vector3(95.6f, 0.95f, 35.0f), woodMat, goldMat);

            // Biển gỗ chỉ dẫn "XUỐNG THUYỀN [E]"
            Signpost("StartDock_Signpost", startArea, new Vector3(94.8f, 0.95f, 26.5f), new Vector3(0, -30f, 0), woodMat, goldMat, true);

            // Spawn points on dock
            var spawnDad = Marker("SpawnPoint_Dad", startArea, new Vector3(93.0f, 1.25f, 28.5f));
            spawnDad.forward = Vector3.forward;
            var spawnChild = Marker("SpawnPoint_Child", startArea, new Vector3(94.2f, 1.25f, 28.5f));
            spawnChild.forward = Vector3.forward;

            // Update GameManager spawns & Pfb_SpawnPads
            var gm = scene.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<GameManager>()).FirstOrDefault();
            if (gm != null)
            {
                var so = new SerializedObject(gm);
                var spawns = so.FindProperty("spawnPoints");
                spawns.arraySize = 2;
                spawns.GetArrayElementAtIndex(0).objectReferenceValue = spawnDad;
                spawns.GetArrayElementAtIndex(1).objectReferenceValue = spawnChild;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            var pad0 = GameObject.Find("Pfb_SpawnPad");
            if (pad0 != null) pad0.transform.position = spawnDad.position;
            var pad1 = GameObject.Find("Pfb_SpawnPad (1)");
            if (pad1 != null) pad1.transform.position = spawnChild.position;

            // Ensure Boat is placed in clear water by dock
            var boat = GameObject.Find("Coop_Wooden_Boat");
            if (boat != null)
            {
                boat.transform.position = new Vector3(97.8f, 0.53f, 34.5f);
                boat.transform.rotation = Quaternion.Euler(0, 0f, 0);
            }

            // ==========================================
            // 2. STAGE 1: SLALOM BUOYS & HAZARD ROCKS (Z: 40 - 68)
            // ==========================================
            var stage1 = Marker("02_SlalomCourse_Stage1", Root.transform, Vector3.zero);

            // Gate 1 (Z: 44)
            Cylinder("Buoy_1_Red", stage1, new Vector3(93.0f, 0.65f, 44.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyRedMat);
            Cylinder("Buoy_1_Green", stage1, new Vector3(103.0f, 0.65f, 44.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyGreenMat);
            // Thêm vành nổi & chóp đèn cho Phao Gate 1
            Cylinder("Buoy_1_Red_Base", stage1, new Vector3(93.0f, 0.48f, 44.0f), new Vector3(1.3f, 0.15f, 1.3f), ironMat);
            Cylinder("Buoy_1_Green_Base", stage1, new Vector3(103.0f, 0.48f, 44.0f), new Vector3(1.3f, 0.15f, 1.3f), ironMat);
            Box("Buoy_1_Red_Flag", stage1, new Vector3(93.0f, 1.45f, 44.0f), new Vector3(0.08f, 0.4f, 0.4f), buoyRedMat);
            Box("Buoy_1_Green_Flag", stage1, new Vector3(103.0f, 1.45f, 44.0f), new Vector3(0.08f, 0.4f, 0.4f), buoyGreenMat);

            var g1Trigger = Box("SlalomGate_1_Trigger", stage1, new Vector3(98.0f, 1.0f, 44.0f), new Vector3(9.0f, 2.5f, 1.5f));
            g1Trigger.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(g1Trigger.GetComponent<MeshRenderer>());

            // Gate 2 (Z: 56)
            Cylinder("Buoy_2_Red", stage1, new Vector3(92.5f, 0.65f, 56.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyRedMat);
            Cylinder("Buoy_2_Green", stage1, new Vector3(104.0f, 0.65f, 56.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyGreenMat);
            Cylinder("Buoy_2_Red_Base", stage1, new Vector3(92.5f, 0.48f, 56.0f), new Vector3(1.3f, 0.15f, 1.3f), ironMat);
            Cylinder("Buoy_2_Green_Base", stage1, new Vector3(104.0f, 0.48f, 56.0f), new Vector3(1.3f, 0.15f, 1.3f), ironMat);
            Box("Buoy_2_Red_Flag", stage1, new Vector3(92.5f, 1.45f, 56.0f), new Vector3(0.08f, 0.4f, 0.4f), buoyRedMat);
            Box("Buoy_2_Green_Flag", stage1, new Vector3(104.0f, 1.45f, 56.0f), new Vector3(0.08f, 0.4f, 0.4f), buoyGreenMat);

            var g2Trigger = Box("SlalomGate_2_Trigger", stage1, new Vector3(98.2f, 1.0f, 56.0f), new Vector3(10.5f, 2.5f, 1.5f));
            g2Trigger.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(g2Trigger.GetComponent<MeshRenderer>());

            // Gate 3 (Z: 66)
            Cylinder("Buoy_3_Red", stage1, new Vector3(95.0f, 0.65f, 66.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyRedMat);
            Cylinder("Buoy_3_Green", stage1, new Vector3(106.0f, 0.65f, 66.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyGreenMat);
            Cylinder("Buoy_3_Red_Base", stage1, new Vector3(95.0f, 0.48f, 66.0f), new Vector3(1.3f, 0.15f, 1.3f), ironMat);
            Cylinder("Buoy_3_Green_Base", stage1, new Vector3(106.0f, 0.48f, 66.0f), new Vector3(1.3f, 0.15f, 1.3f), ironMat);
            Box("Buoy_3_Red_Flag", stage1, new Vector3(95.0f, 1.45f, 66.0f), new Vector3(0.08f, 0.4f, 0.4f), buoyRedMat);
            Box("Buoy_3_Green_Flag", stage1, new Vector3(106.0f, 1.45f, 66.0f), new Vector3(0.08f, 0.4f, 0.4f), buoyGreenMat);

            var g3Trigger = Box("SlalomGate_3_Trigger", stage1, new Vector3(100.5f, 1.0f, 66.0f), new Vector3(10.0f, 2.5f, 1.5f));
            g3Trigger.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(g3Trigger.GetComponent<MeshRenderer>());

            // Channel Hazard Rocks - Tảng đá tự nhiên 3D chân thực thay thế hình hộp vuông
            SpawnRock("HazardRock_Left_1", stage1, new Vector3(88.0f, 0.4f, 48.0f), new Vector3(3.8f, 3.2f, 3.8f), new Vector3(0, 40, 10), "Rock_1");
            SpawnRock("HazardRock_Left_1_Sub", stage1, new Vector3(89.5f, 0.25f, 49.5f), new Vector3(2.2f, 1.8f, 2.2f), new Vector3(15, -30, 0), "Rock_Small_1");

            SpawnRock("HazardRock_Right_1", stage1, new Vector3(109.0f, 0.4f, 52.0f), new Vector3(4.2f, 3.5f, 4.2f), new Vector3(-10, 85, 0), "Rock_3");
            SpawnRock("HazardRock_Right_1_Sub", stage1, new Vector3(107.5f, 0.25f, 53.5f), new Vector3(2.2f, 1.6f, 2.2f), new Vector3(0, 120, 0), "Rock_Small_2");

            SpawnRock("HazardRock_Left_2", stage1, new Vector3(89.2f, 0.4f, 62.0f), new Vector3(3.6f, 3.0f, 3.6f), new Vector3(20, 50, 0), "Rock_7");
            SpawnRock("HazardRock_Mid_Shallow", stage1, new Vector3(98.5f, 0.15f, 50.0f), new Vector3(1.8f, 1.2f, 1.8f), new Vector3(0, 35, 0), "Rock_Small_3");

            // Phao Mũi tên dẫn luồng nổi trên sông (Floating Waypoint Arrows) chỉ hướng lượn qua 3 cổng
            FloatingRiverArrow("Slalom_FlowArrow_1", stage1, new Vector3(97.8f, 0.48f, 40.0f), 5f, woodMat, goldMat);
            FloatingRiverArrow("Slalom_FlowArrow_2", stage1, new Vector3(98.5f, 0.48f, 50.0f), -8f, woodMat, goldMat);
            FloatingRiverArrow("Slalom_FlowArrow_3", stage1, new Vector3(99.8f, 0.48f, 61.0f), 12f, woodMat, goldMat);

            // ==========================================
            // 3. STAGE 2: SLUICE WATERGATE & ISLAND PUZZLE (Z: 70 - 85)
            // ==========================================
            var stage2 = Marker("03_SluiceGate_Stage2", Root.transform, Vector3.zero);
            // Sluice Gate Architecture (Z: 74)
            Box("Sluice_LeftTower", stage2, new Vector3(91.5f, 4.0f, 74.0f), new Vector3(3.5f, 8.0f, 4.0f), stoneMat);
            Box("Sluice_RightTower", stage2, new Vector3(109.5f, 4.0f, 74.0f), new Vector3(4.0f, 8.0f, 4.5f), stoneMat);
            Box("Sluice_ArchBeam", stage2, new Vector3(100.5f, 7.6f, 74.0f), new Vector3(17.5f, 1.2f, 3.0f), stoneMat);
            // Movable Iron Portcullis Gate
            var gateBarrier = Box("SluiceGate_IronPortcullis", stage2, new Vector3(100.5f, 2.2f, 74.0f), new Vector3(14.5f, 4.2f, 0.6f), ironMat);

            // Chi tiết vòm đá đỉnh tháp (Battlements / Cornices)
            Box("Sluice_LeftTower_Cap", stage2, new Vector3(91.5f, 8.3f, 74.0f), new Vector3(4.2f, 0.6f, 4.8f), stoneMat);
            Box("Sluice_RightTower_Cap", stage2, new Vector3(109.5f, 8.3f, 74.0f), new Vector3(4.6f, 0.6f, 5.2f), stoneMat);

            // Side Dock for puzzle landing (Right bank)
            Box("Sluice_SideDock", stage2, new Vector3(107.5f, 0.95f, 70.0f), new Vector3(5.0f, 0.25f, 6.0f), woodMat);
            // Bậc thang gỗ lên bờ bến Sluice
            Box("Sluice_Dock_Stairs", stage2, new Vector3(109.8f, 1.05f, 70.0f), new Vector3(1.6f, 0.35f, 4.0f), woodMat);
            // Cọc neo thuyền và đèn lồng bến
            Cylinder("Sluice_Dock_Bollard", stage2, new Vector3(105.5f, 1.2f, 68.0f), new Vector3(0.25f, 0.5f, 0.25f), ironMat);
            LanternPost("Sluice_Dock_Lantern", stage2, new Vector3(109.5f, 0.95f, 67.5f), woodMat, goldMat);

            // Biển hướng dẫn phân công Co-op tại bến Cửa Cống
            Signpost("Sluice_Guidance_Sign", stage2, new Vector3(109.5f, 1.15f, 68.8f), new Vector3(0, 180f, 0), woodMat, goldMat, true);

            // Dad Winch Station - Bệ đá nâng cao trang trọng
            Box("Dad_Winch_BasePlinth", stage2, new Vector3(110.5f, 1.15f, 71.5f), new Vector3(2.4f, 0.4f, 2.4f), stoneMat);
            var dadWinch = Box("Dad_Winch_Station", stage2, new Vector3(110.5f, 1.8f, 71.5f), new Vector3(1.2f, 1.6f, 1.2f), ironMat);
            Cylinder("Dad_Winch_Wheel", dadWinch.transform, new Vector3(0, 0.6f, 0.4f), new Vector3(1.2f, 0.15f, 1.2f), ironMat);
            // Tay quay tời (Winch Handles)
            Box("Winch_Handle_1", dadWinch.transform, new Vector3(0, 1.1f, 0.4f), new Vector3(0.12f, 0.45f, 0.12f), goldMat);
            Box("Winch_Handle_2", dadWinch.transform, new Vector3(0, 0.1f, 0.4f), new Vector3(0.12f, 0.45f, 0.12f), goldMat);
            // Biển báo "BA: GIỮ TỜI [E]"
            Signpost("Dad_Winch_Sign", stage2, new Vector3(111.8f, 1.15f, 71.5f), new Vector3(0, -90f, 0), woodMat, goldMat, false);

            // Child Crawl Conduit
            var crawlway = Box("Child_Crawlway_Conduit", stage2, new Vector3(109.5f, 0.9f, 74.0f), new Vector3(1.1f, 1.0f, 4.2f), stoneMat);
            var crawlVol = crawlway.AddComponent<CoopCrawlVolume>();
            crawlway.GetComponent<BoxCollider>().isTrigger = true;

            // Vòm đá lối vào hầm cho Con (Arched Entry)
            Box("Crawl_Arch_Header", stage2, new Vector3(109.5f, 2.0f, 71.6f), new Vector3(1.8f, 0.4f, 0.4f), stoneMat);
            Box("Crawl_Arch_Post_L", stage2, new Vector3(108.6f, 1.4f, 71.6f), new Vector3(0.4f, 1.2f, 0.4f), stoneMat);
            Box("Crawl_Arch_Post_R", stage2, new Vector3(110.4f, 1.4f, 71.6f), new Vector3(0.4f, 1.2f, 0.4f), stoneMat);
            // Biển chỉ dẫn "CON: CHUI HẦM [CRAWL]"
            Signpost("Child_Crawl_Sign", stage2, new Vector3(108.4f, 1.15f, 72.2f), new Vector3(0, 45f, 0), woodMat, goldMat, true);

            // Upper Control Balcony & Valve Lever
            Box("Upper_Balcony", stage2, new Vector3(109.5f, 4.2f, 75.5f), new Vector3(3.5f, 0.3f, 3.5f), woodMat);
            var lever = Cylinder("WaterValve_Lever", stage2, new Vector3(109.5f, 5.0f, 75.5f), new Vector3(0.2f, 1.2f, 0.2f), goldMat);
            // Bệ đỡ van xả hoàng kim
            Cylinder("Valve_Pedestal", stage2, new Vector3(109.5f, 4.5f, 75.5f), new Vector3(0.6f, 0.5f, 0.6f), stoneMat);
            Cylinder("Valve_Wheel", stage2, new Vector3(109.5f, 5.3f, 75.5f), new Vector3(0.85f, 0.12f, 0.85f), goldMat);

            // ==========================================
            // 4. STAGE 3: RAPIDS RUN & TWIN WHIRLPOOLS (Z: 86 - 150)
            // ==========================================
            var stage3 = Marker("04_RapidsWhirlpools_Stage3", Root.transform, Vector3.zero);
            var rapidsWater1 = GameObject.CreatePrimitive(PrimitiveType.Plane);
            rapidsWater1.name = "Water_Rapids_1";
            rapidsWater1.transform.SetParent(stage3, false);
            rapidsWater1.transform.position = new Vector3(100.0f, 0.5f, 105.0f);
            rapidsWater1.transform.localScale = new Vector3(3.5f, 1.0f, 3.5f);
            rapidsWater1.GetComponent<Renderer>().sharedMaterial = waterMat;

            var rapidsWater2 = GameObject.CreatePrimitive(PrimitiveType.Plane);
            rapidsWater2.name = "Water_Rapids_2";
            rapidsWater2.transform.SetParent(stage3, false);
            rapidsWater2.transform.position = new Vector3(100.0f, 0.5f, 140.0f);
            rapidsWater2.transform.localScale = new Vector3(3.5f, 1.0f, 3.5f);
            rapidsWater2.GetComponent<Renderer>().sharedMaterial = waterMat;

            // Rapids River Current Trigger
            var currentZone = Box("Rapids_CurrentZone", stage3, new Vector3(100.0f, 0.8f, 115.0f), new Vector3(20.0f, 1.8f, 60.0f));
            currentZone.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(currentZone.GetComponent<MeshRenderer>());

            // Whirlpool 1 (Z: 102)
            var wp1 = Cylinder("Whirlpool_1_Vortex", stage3, new Vector3(97.0f, 0.45f, 102.0f), new Vector3(9.0f, 0.1f, 9.0f), ironMat);
            wp1.GetComponent<Collider>().isTrigger = true;
            Cylinder("Whirlpool_1_Ring", stage3, new Vector3(97.0f, 0.48f, 102.0f), new Vector3(13.0f, 0.05f, 13.0f), buoyRedMat);
            SpawnRock("Whirlpool_1_Rock1", stage3, new Vector3(91.0f, 0.5f, 102.0f), new Vector3(3.2f, 3.6f, 3.2f), new Vector3(10, 20, 5), "Rock_8");
            SpawnRock("Whirlpool_1_Rock2", stage3, new Vector3(103.0f, 0.5f, 102.0f), new Vector3(3.0f, 3.4f, 3.0f), new Vector3(-15, 60, 0), "Rock_9");

            // Whirlpool 2 (Z: 128)
            var wp2 = Cylinder("Whirlpool_2_Vortex", stage3, new Vector3(104.0f, 0.45f, 128.0f), new Vector3(9.5f, 0.1f, 9.5f), ironMat);
            wp2.GetComponent<Collider>().isTrigger = true;
            Cylinder("Whirlpool_2_Ring", stage3, new Vector3(104.0f, 0.48f, 128.0f), new Vector3(13.5f, 0.05f, 13.5f), buoyRedMat);
            SpawnRock("Whirlpool_2_Rock1", stage3, new Vector3(98.0f, 0.5f, 128.0f), new Vector3(3.0f, 3.5f, 3.0f), new Vector3(5, -45, 10), "Rock_11");
            SpawnRock("Whirlpool_2_Rock2", stage3, new Vector3(110.0f, 0.5f, 128.0f), new Vector3(3.2f, 3.6f, 3.2f), new Vector3(0, 110, -5), "Rock_12");

            // Canyon Gorge Clifflines & Rặng đá thiên nhiên xếp lớp hùng vĩ
            SpawnRock("Cliff_Rock_L1", stage3, new Vector3(84.0f, 0.5f, 95.0f), new Vector3(5.0f, 5.5f, 5.0f), new Vector3(0, 15, 0), "Rocks_Pile");
            SpawnRock("Cliff_Rock_L2", stage3, new Vector3(83.5f, 0.5f, 115.0f), new Vector3(5.5f, 6.0f, 5.5f), new Vector3(0, 75, 0), "Rocks_Pile_2");
            SpawnRock("Cliff_Rock_L3", stage3, new Vector3(84.5f, 0.5f, 135.0f), new Vector3(5.0f, 5.5f, 5.0f), new Vector3(0, -30, 0), "Rocks_Pile");

            SpawnRock("Cliff_Rock_R1", stage3, new Vector3(116.0f, 0.5f, 95.0f), new Vector3(5.0f, 5.5f, 5.0f), new Vector3(0, 110, 0), "Rocks_Pile");
            SpawnRock("Cliff_Rock_R2", stage3, new Vector3(116.5f, 0.5f, 115.0f), new Vector3(5.5f, 6.0f, 5.5f), new Vector3(0, -60, 0), "Rocks_Pile_2");
            SpawnRock("Cliff_Rock_R3", stage3, new Vector3(115.5f, 0.5f, 135.0f), new Vector3(5.0f, 5.5f, 5.0f), new Vector3(0, 140, 0), "Rocks_Pile");

            var cliffL = Box("Canyon_Cliff_Left", stage3, new Vector3(82.0f, 3.0f, 115.0f), new Vector3(2.0f, 6.0f, 65.0f), stoneMat);
            var cliffR = Box("Canyon_Cliff_Right", stage3, new Vector3(118.0f, 3.0f, 115.0f), new Vector3(2.0f, 6.0f, 65.0f), stoneMat);
            Object.DestroyImmediate(cliffL.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(cliffR.GetComponent<MeshRenderer>());
            Box("Canyon_Ledge_1", stage3, new Vector3(87.5f, 2.5f, 102.0f), new Vector3(3.0f, 4.0f, 12.0f), stoneMat);
            Box("Canyon_Ledge_2", stage3, new Vector3(113.5f, 2.5f, 128.0f), new Vector3(3.0f, 4.0f, 12.0f), stoneMat);

            // Phao Mũi tên dẫn hướng né xoáy nước (Navigate Path Arrows)
            FloatingRiverArrow("Rapids_Arrow_1", stage3, new Vector3(103.5f, 0.48f, 96.0f), 15f, woodMat, goldMat); // Né xoáy 1 qua phải
            FloatingRiverArrow("Rapids_Arrow_2", stage3, new Vector3(98.5f, 0.48f, 116.0f), -15f, woodMat, goldMat); // Lách qua trái né xoáy 2
            FloatingRiverArrow("Rapids_Arrow_3", stage3, new Vector3(100.0f, 0.48f, 142.0f), 0f, woodMat, goldMat); // Ra khỏi hẻm ghềnh thác

            // ==========================================
            // 4B. STAGE 3B: GLOWSTONE CAVERN (Z: 145 - 172)
            // Hang Động Nhũ Đá Huyền Bí phát sáng dạ quang
            // ==========================================
            var stage3B = Marker("04B_GlowstoneCavern_Stage3B", Root.transform, Vector3.zero);

            // Cổng vòm đá vào hang động (Entrance Portal - Z: 146)
            Box("Cavern_Portal_L", stage3B, new Vector3(88.0f, 4.5f, 146.0f), new Vector3(4.0f, 9.0f, 4.0f), stoneMat);
            Box("Cavern_Portal_R", stage3B, new Vector3(112.0f, 4.5f, 146.0f), new Vector3(4.0f, 9.0f, 4.0f), stoneMat);
            var entranceArch = Box("Cavern_Portal_Arch", stage3B, new Vector3(100.0f, 8.8f, 146.0f), new Vector3(22.0f, 1.8f, 3.5f), stoneMat);
            if (entranceArch.TryGetComponent<Collider>(out var arcCol)) arcCol.isTrigger = true;

            // Vòm mái hang động (Vaulted Cavern Roof - Z: 148 - 170)
            var roof1 = Box("Cavern_Roof_1", stage3B, new Vector3(100.0f, 9.2f, 154.0f), new Vector3(25.0f, 1.5f, 14.0f), stoneMat);
            if (roof1.TryGetComponent<Collider>(out var r1Col)) r1Col.isTrigger = true;
            var roof2 = Box("Cavern_Roof_2", stage3B, new Vector3(100.0f, 9.2f, 166.0f), new Vector3(25.0f, 1.5f, 14.0f), stoneMat);
            if (roof2.TryGetComponent<Collider>(out var r2Col)) r2Col.isTrigger = true;

            // Vách đá hai bên hang ngầm (Cavern Flank Cliffs)
            Box("Cavern_Cliff_L1", stage3B, new Vector3(87.5f, 4.5f, 154.0f), new Vector3(4.5f, 8.0f, 15.0f), stoneMat);
            Box("Cavern_Cliff_R1", stage3B, new Vector3(112.5f, 4.5f, 154.0f), new Vector3(4.5f, 8.0f, 15.0f), stoneMat);
            Box("Cavern_Cliff_L2", stage3B, new Vector3(87.5f, 4.5f, 166.0f), new Vector3(4.5f, 8.0f, 15.0f), stoneMat);
            Box("Cavern_Cliff_R2", stage3B, new Vector3(112.5f, 4.5f, 166.0f), new Vector3(4.5f, 8.0f, 15.0f), stoneMat);

            // Cổng vòm đá ra khỏi hang mở sang Đầm lầy (Exit Portal - Z: 172)
            Box("Cavern_Exit_L", stage3B, new Vector3(88.0f, 4.5f, 172.0f), new Vector3(4.0f, 9.0f, 3.5f), stoneMat);
            Box("Cavern_Exit_R", stage3B, new Vector3(112.0f, 4.5f, 172.0f), new Vector3(4.0f, 9.0f, 3.5f), stoneMat);
            var exitArch = Box("Cavern_Exit_Arch", stage3B, new Vector3(100.0f, 8.8f, 172.0f), new Vector3(22.0f, 1.8f, 3.5f), stoneMat);
            if (exitArch.TryGetComponent<Collider>(out var exCol)) exCol.isTrigger = true;

            // Thác nước ngầm đổ từ vách hang (Subterranean Waterfalls)
            var wfL = Box("Cavern_Waterfall_L", stage3B, new Vector3(89.2f, 4.5f, 158.0f), new Vector3(0.4f, 7.5f, 5.0f), waterMat);
            if (wfL.TryGetComponent<Collider>(out var wfLCol)) wfLCol.isTrigger = true;
            var wfR = Box("Cavern_Waterfall_R", stage3B, new Vector3(110.8f, 4.5f, 162.0f), new Vector3(0.4f, 7.5f, 5.0f), waterMat);
            if (wfR.TryGetComponent<Collider>(out var wfRCol)) wfRCol.isTrigger = true;

            // Hàm tạo nhũ đá phát quang (Bioluminescent Stalactite with Cyan Glow)
            System.Action<string, Vector3, Vector3> createStalactite = (stName, pos, size) =>
            {
                var s = Cylinder(stName, stage3B, pos, size, glowstoneMat);
                if (s.TryGetComponent<Collider>(out var c)) c.isTrigger = true;
                var crystal = Box(stName + "_Tip", stage3B, pos - Vector3.up * (size.y * 0.45f), new Vector3(size.x * 0.5f, 0.7f, size.z * 0.5f), glowstoneMat);
                if (crystal.TryGetComponent<Collider>(out var cc)) cc.isTrigger = true;
                var light = crystal.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(0.2f, 0.85f, 0.95f);
                light.range = 14f;
                light.intensity = 2.2f;
            };

            createStalactite("Stalactite_L1", new Vector3(93.5f, 6.2f, 151.0f), new Vector3(1.3f, 3.8f, 1.3f));
            createStalactite("Stalactite_R1", new Vector3(106.5f, 6.2f, 155.0f), new Vector3(1.4f, 4.0f, 1.4f));
            createStalactite("Stalactite_L2", new Vector3(94.0f, 6.4f, 163.0f), new Vector3(1.5f, 3.6f, 1.5f));
            createStalactite("Stalactite_R2", new Vector3(106.0f, 6.4f, 167.0f), new Vector3(1.4f, 3.8f, 1.4f));

            // Măng đá dạ quang mọc hai bên bờ gờ hang (Stalagmites)
            System.Action<string, Vector3, Vector3> createStalagmite = (smName, pos, size) =>
            {
                var sm = Cylinder(smName, stage3B, pos, size, glowstoneMat);
                if (sm.TryGetComponent<Collider>(out var sc)) sc.isTrigger = true;
            };

            createStalagmite("Stalagmite_L1", new Vector3(89.8f, 1.6f, 150.0f), new Vector3(1.6f, 2.4f, 1.6f));
            createStalagmite("Stalagmite_R1", new Vector3(110.2f, 1.6f, 153.0f), new Vector3(1.8f, 2.6f, 1.8f));
            createStalagmite("Stalagmite_L2", new Vector3(89.8f, 1.8f, 161.0f), new Vector3(1.8f, 2.8f, 1.8f));
            createStalagmite("Stalagmite_R2", new Vector3(110.2f, 1.8f, 165.0f), new Vector3(1.6f, 2.6f, 1.6f));

            // Phao Mũi tên dạ quang chỉ luồng hang động ngầm (Cyan Flow Arrows)
            FloatingRiverArrow("Cavern_FlowArrow_1", stage3B, new Vector3(100.0f, 0.48f, 151.0f), 0f, woodMat, glowstoneMat);
            FloatingRiverArrow("Cavern_FlowArrow_2", stage3B, new Vector3(100.0f, 0.48f, 163.0f), 0f, woodMat, glowstoneMat);

            // ==========================================
            // 5. STAGE 4: MISTY MARSH & TWIN BEACONS (Z: 155 - 215)
            // ==========================================
            var stage4 = Marker("05_MistyMarsh_Stage4", Root.transform, Vector3.zero);
            var marshWater = GameObject.CreatePrimitive(PrimitiveType.Plane);
            marshWater.name = "Water_Marsh_Basin";
            marshWater.transform.SetParent(stage4, false);
            marshWater.transform.position = new Vector3(100.0f, 0.5f, 185.0f);
            marshWater.transform.localScale = new Vector3(5.0f, 1.0f, 5.0f);
            marshWater.GetComponent<Renderer>().sharedMaterial = waterMat;

            // West Beacon Tower & Island (Left) với đế đá tự nhiên
            SpawnRock("WestBeacon_RockBase", stage4, new Vector3(83.0f, 0.5f, 175.0f), new Vector3(4.5f, 2.5f, 4.5f), new Vector3(0, 45, 0), "Rocks_Pile");
            var westIsland = Cylinder("WestBeacon_Island", stage4, new Vector3(83.0f, 0.9f, 175.0f), new Vector3(9.0f, 0.8f, 9.0f), stoneMat);
            Cylinder("WestBeacon_Tower", stage4, new Vector3(83.0f, 3.5f, 175.0f), new Vector3(2.5f, 5.0f, 2.5f), stoneMat);
            Cylinder("WestBeacon_Brazier", stage4, new Vector3(83.0f, 6.2f, 175.0f), new Vector3(1.4f, 0.8f, 1.4f), ironMat);
            var westLight = Box("WestBeacon_Flame", stage4, new Vector3(83.0f, 6.8f, 175.0f), new Vector3(0.8f, 0.8f, 0.8f), goldMat);
            Cylinder("WestBeacon_Base_Step", stage4, new Vector3(83.0f, 1.4f, 175.0f), new Vector3(5.0f, 0.4f, 5.0f), stoneMat);

            // East Beacon Tower & Island (Right) với đế đá tự nhiên
            SpawnRock("EastBeacon_RockBase", stage4, new Vector3(117.0f, 0.5f, 190.0f), new Vector3(4.5f, 2.5f, 4.5f), new Vector3(0, 120, 0), "Rocks_Pile");
            var eastIsland = Cylinder("EastBeacon_Island", stage4, new Vector3(117.0f, 0.9f, 190.0f), new Vector3(9.0f, 0.8f, 9.0f), stoneMat);
            Cylinder("EastBeacon_Tower", stage4, new Vector3(117.0f, 3.5f, 190.0f), new Vector3(2.5f, 5.0f, 2.5f), stoneMat);
            Cylinder("EastBeacon_Brazier", stage4, new Vector3(117.0f, 6.2f, 190.0f), new Vector3(1.4f, 0.8f, 1.4f), ironMat);
            var eastLight = Box("EastBeacon_Flame", stage4, new Vector3(117.0f, 6.8f, 190.0f), new Vector3(0.8f, 0.8f, 0.8f), goldMat);
            Cylinder("EastBeacon_Base_Step", stage4, new Vector3(117.0f, 1.4f, 190.0f), new Vector3(5.0f, 0.4f, 5.0f), stoneMat);

            // Giant Lily Stepping Stones (Lá sen & Hoa sen tự nhiên 3D cho Con đáp xuống)
            var lily1 = SpawnWaterLily("GiantLilyPad_1", stage4, new Vector3(90.0f, 0.52f, 175.0f), new Vector3(3.8f, 1.2f, 3.8f), true);
            var lily2 = SpawnWaterLily("GiantLilyPad_2", stage4, new Vector3(110.0f, 0.52f, 190.0f), new Vector3(3.8f, 1.2f, 3.8f), true);

            // Cụm hoa sen & lá sen trang trí điểm xuyết đầm lầy
            SpawnWaterLily("LilyDeco_1", stage4, new Vector3(86.0f, 0.50f, 182.0f), new Vector3(2.4f, 0.8f, 2.4f), true);
            SpawnWaterLily("LilyDeco_2", stage4, new Vector3(114.0f, 0.50f, 182.0f), new Vector3(2.6f, 0.9f, 2.6f), false);
            SpawnWaterLily("LilyDeco_3", stage4, new Vector3(98.0f, 0.50f, 168.0f), new Vector3(2.0f, 0.8f, 2.0f), true);
            SpawnWaterLily("LilyDeco_4", stage4, new Vector3(104.0f, 0.50f, 198.0f), new Vector3(2.5f, 0.9f, 2.5f), false);

            // Lumina Crystal Pedestal on West Island
            Box("LuminaCrystal_Pedestal", stage4, new Vector3(83.0f, 1.6f, 172.5f), new Vector3(0.8f, 1.2f, 0.8f), stoneMat);
            Box("LuminaCrystal_Gem", stage4, new Vector3(83.0f, 2.4f, 172.5f), new Vector3(0.5f, 0.6f, 0.5f), goldMat);

            // Biển hướng dẫn ném đồng đội tại 2 bến Đầm lầy
            Signpost("West_Throw_Sign", stage4, new Vector3(98.5f, 1.15f, 173.5f), new Vector3(0, -90f, 0), woodMat, goldMat, false);
            Signpost("East_Throw_Sign", stage4, new Vector3(101.5f, 1.15f, 188.5f), new Vector3(0, 90f, 0), woodMat, goldMat, true);

            // ==========================================
            // 6. STAGE 5: GIANT WATERMILL CLIMAX & HAVEN (Z: 216 - 265)
            // ==========================================
            var stage5 = Marker("06_WatermillHaven_Stage5", Root.transform, Vector3.zero);
            // Gorge Archway before Watermill với vách đá tự nhiên
            SpawnRock("Haven_GorgeRock_L", stage5, new Vector3(88.0f, 0.5f, 225.0f), new Vector3(5.0f, 6.5f, 5.0f), new Vector3(0, 30, 0), "Rocks_Pile");
            SpawnRock("Haven_GorgeRock_R", stage5, new Vector3(112.0f, 0.5f, 225.0f), new Vector3(5.0f, 6.5f, 5.0f), new Vector3(0, -45, 0), "Rocks_Pile");
            Box("Watermill_LeftGorge", stage5, new Vector3(89.0f, 5.0f, 225.0f), new Vector3(5.0f, 10.0f, 4.0f), stoneMat);
            Box("Watermill_RightGorge", stage5, new Vector3(111.0f, 5.0f, 225.0f), new Vector3(5.0f, 10.0f, 4.0f), stoneMat);
            // Vòm đá vươn ngang hẻm núi nối hai trụ
            Box("Watermill_Arch_Overhead", stage5, new Vector3(100.0f, 10.5f, 225.0f), new Vector3(25.0f, 1.5f, 3.5f), stoneMat);

            // Giant Watermill Axis & 4 Paddles
            var millRoot = Marker("GiantWatermill_Assembly", stage5, new Vector3(100.0f, 4.5f, 225.0f));
            Cylinder("Watermill_Hub", millRoot, Vector3.zero, new Vector3(1.8f, 16.0f, 1.8f), ironMat);
            millRoot.transform.Rotate(0, 0, 90f);

            // 4 Paddles in cross formation
            Box("Paddle_Blade_Top", millRoot, new Vector3(0, 0, 3.8f), new Vector3(15.0f, 0.3f, 3.2f), woodMat);
            Box("Paddle_Blade_Bottom", millRoot, new Vector3(0, 0, -3.8f), new Vector3(15.0f, 0.3f, 3.2f), woodMat);
            Box("Paddle_Blade_Front", millRoot, new Vector3(3.8f, 0, 0), new Vector3(3.2f, 0.3f, 15.0f), woodMat);
            Box("Paddle_Blade_Back", millRoot, new Vector3(-3.8f, 0, 0), new Vector3(3.2f, 0.3f, 15.0f), woodMat);

            // Haven Harbor Pier
            var havenWater = GameObject.CreatePrimitive(PrimitiveType.Plane);
            havenWater.name = "Water_Haven_Harbor";
            havenWater.transform.SetParent(stage5, false);
            havenWater.transform.position = new Vector3(100.0f, 0.5f, 245.0f);
            havenWater.transform.localScale = new Vector3(3.0f, 1.0f, 3.0f);
            havenWater.GetComponent<Renderer>().sharedMaterial = waterMat;

            // Docking platform
            Box("HavenHarbor_Dock", stage5, new Vector3(100.0f, 1.0f, 248.0f), new Vector3(8.0f, 0.3f, 6.0f), woodMat);
            SpawnWoodenBridge("HavenHarbor_PierBridge", stage5, new Vector3(100.0f, 0.95f, 245.0f), new Vector3(1.2f, 1.0f, 1.2f), Vector3.zero);
            Cylinder("HavenHarbor_Post_L", stage5, new Vector3(96.5f, 1.3f, 246.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);
            Cylinder("HavenHarbor_Post_R", stage5, new Vector3(103.5f, 1.3f, 246.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);

            // Cột đèn lồng chào đón tại Haven Harbor
            LanternPost("Haven_Lantern_Left", stage5, new Vector3(96.2f, 1.0f, 246.5f), woodMat, goldMat);
            LanternPost("Haven_Lantern_Right", stage5, new Vector3(103.8f, 1.0f, 246.5f), woodMat, goldMat);

            // Đại sảnh Bậc Tam Cấp dẫn lên Bàn Thờ (Grand Stairs)
            Box("Haven_GrandStairs_1", stage5, new Vector3(100.0f, 1.1f, 251.5f), new Vector3(6.5f, 0.25f, 1.8f), stoneMat);
            Box("Haven_GrandStairs_2", stage5, new Vector3(100.0f, 1.3f, 253.2f), new Vector3(5.5f, 0.25f, 1.8f), stoneMat);

            // Altar of Dawn & Victory Trigger
            var altar = Box("AltarOfDawn_Podium", stage5, new Vector3(100.0f, 1.4f, 256.0f), new Vector3(3.5f, 0.8f, 3.5f), stoneMat);
            Cylinder("Altar_MemoryCrystal", altar.transform, new Vector3(0, 0.8f, 0), new Vector3(0.8f, 1.2f, 0.8f), goldMat);
            var victoryTrig = Box("LevelTwo_VictoryTrigger", stage5, new Vector3(100.0f, 1.8f, 256.0f), new Vector3(5.0f, 3.0f, 5.0f));
            victoryTrig.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(victoryTrig.GetComponent<MeshRenderer>());

            // Hai Bục đứng thiêng liêng dành cho Cha và Con tại Bàn Thờ Bình Minh
            Cylinder("Altar_Dad_Plinth", stage5, new Vector3(98.8f, 1.5f, 256.0f), new Vector3(1.2f, 0.15f, 1.2f), goldMat);
            Cylinder("Altar_Child_Plinth", stage5, new Vector3(101.2f, 1.5f, 256.0f), new Vector3(1.0f, 0.15f, 1.0f), goldMat);
            // 2 Ngọn đuốc thiêng hai bên Bàn Thờ
            LanternPost("Altar_Torch_L", stage5, new Vector3(97.2f, 1.4f, 257.5f), stoneMat, goldMat);
            LanternPost("Altar_Torch_R", stage5, new Vector3(102.8f, 1.4f, 257.5f), stoneMat, goldMat);

            // Biển chỉ dẫn hoàn thành: "NẮM TAY NHAU [H] ĐỂ CHIẾN THẮNG"
            Signpost("Altar_Victory_Sign", stage5, new Vector3(100.0f, 1.4f, 254.5f), new Vector3(0, 0, 0), woodMat, goldMat, true);

            // ==========================================
            // 7. CHECKPOINTS
            // ==========================================
            var cpRoot = Marker("Checkpoints", Root.transform, Vector3.zero);
            Marker("CP0_StartDock", cpRoot, new Vector3(93.5f, 1.2f, 28.5f));
            Marker("CP1_PostSluice", cpRoot, new Vector3(100.5f, 1.0f, 80.0f));
            Marker("CP2_PostRapids", cpRoot, new Vector3(100.0f, 1.0f, 150.0f));
            Marker("CP3_HavenHarbor", cpRoot, new Vector3(100.0f, 1.2f, 250.0f));

            // Wiring stages
            WireStageOne();
            WireStageTwo();
            WireStageThree();

            // Mark dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[LVL2] Complete River Map with Guiding Arrows & Architectural Blockout built successfully in " + ScenePath + "!");
        }
    }
}
