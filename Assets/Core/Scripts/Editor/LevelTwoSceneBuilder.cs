using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;

namespace Blocks.Gameplay.Core.LevelTwoAuthoring
{
    /// <summary>
    /// Repeatable authoring of Level 2 (PlayScene2.unity) objects.
    /// Preserves existing environment assets intact while creating the complete
    /// 5-stage river expedition layout, docks, obstacles, and checkpoints.
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
            // Continuous visible water covers gaps between the original stage planes.
            if (Root.transform.Find("Water_RiverContinuous") == null)
            {
                var water = GameObject.CreatePrimitive(PrimitiveType.Plane); water.name = "Water_RiverContinuous";
                water.transform.SetParent(Root.transform, false); water.transform.position = new Vector3(100, 0.5f, 143);
                water.transform.localScale = new Vector3(2.6f, 1, 24);
                water.GetComponent<Renderer>().sharedMaterial = Find("Water_Rapids_1").GetComponent<Renderer>().sharedMaterial;
                Object.DestroyImmediate(water.GetComponent<Collider>());
            }
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
            // Handled dynamically in-memory at runtime by LevelTwoRiverCarver
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
            DressRapidsRocks(Find("04_RapidsWhirlpools_Stage3"));
            Dock("Start_DockLanding", new Vector3(96, 0.5f, 31), new Vector3(94, 1.15f, 30), new Vector3(94, 1.15f, 32));
            Dock("Sluice_DockLanding", new Vector3(106, 0.5f, 70), new Vector3(108, 1.15f, 70), new Vector3(109.5f, 1.15f, 71));
            Save();
        }

        [MenuItem("Hold My Hand/Level 2/Wire Beacons Watermill and Victory")]
        public static void WireStageThree()
        {
            FindMap();
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Wood.mat");
            var gold = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Gold.mat");
            var stage4 = Find("05_MistyMarsh_Stage4");
            // Remove every remnant of the superseded lily-pad/throw puzzle. This is
            // repeatable, so rewiring an older scene cannot leave duplicate props.
            var legacyNames = new HashSet<string>
            {
                "GiantLilyPad_1", "GiantLilyPad_2", "LuminaCrystal_Gem", "ThrowPad", "ThrowPlatform"
            };
            foreach (var legacy in Root.GetComponentsInChildren<Transform>(true)
                         .Where(t => legacyNames.Contains(t.name)).ToArray())
                Object.DestroyImmediate(legacy.gameObject);
            var beacons = new MarshBeaconTrigger[2];
            var beams = new LighthouseLightBeam[2];
            var items = new CoopCarryableItem[2];
            var sockets = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                string side = i == 0 ? "West" : "East";
                float x = i == 0 ? 90 : 110, z = i == 0 ? 175 : 190;
                string rootName = side + "_BeaconGameplay";
                var root = stage4.Find(rootName) ?? Marker(rootName, stage4, Vector3.zero);
                var beacon = Ensure<MarshBeaconTrigger>(root.gameObject); beacons[i] = beacon;
                Ensure<NetworkObject>(root.gameObject);

                Vector3 socketPosition = new Vector3(x, 3.2f, z);
                var socketObject = root.Find(side + "_ItemSocket") != null
                    ? root.Find(side + "_ItemSocket").gameObject
                    : Cylinder(side + "_ItemSocket", root, socketPosition, new Vector3(0.65f, 0.16f, 0.65f), gold);
                socketObject.transform.position = socketPosition;
                beacon.itemSocket = socketObject.transform; sockets[i] = socketObject.transform;

                var wheelObject = root.Find(side + "_TurnWheel") != null
                    ? root.Find(side + "_TurnWheel").gameObject
                    : Cylinder(side + "_TurnWheel", root, new Vector3(x + (i == 0 ? 1.4f : -1.4f), 2.9f, z), new Vector3(0.75f, 0.12f, 0.75f), wood);
                wheelObject.transform.rotation = Quaternion.Euler(90, 0, 0);
                var pivot = root.Find(side + "_BeamEmitter") ?? Marker(side + "_BeamEmitter", root, socketPosition + Vector3.up * 0.35f);
                var wheel = Ensure<LighthouseTurnWheel>(root.gameObject);
                wheel.interactionPoint = wheelObject.transform; wheel.lanternHead = pivot;
                wheel.minimumAngle = i == 0 ? -30 : -45; wheel.maximumAngle = i == 0 ? 30 : 45;
                beacon.turnWheel = wheel;

                var line = Ensure<LineRenderer>(root.gameObject);
                line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Line.mat");
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var beam = Ensure<LighthouseLightBeam>(root.gameObject); beams[i] = beam;
                beam.beamKind = i == 0 ? LighthouseLightBeam.BeamKind.Primary : LighthouseLightBeam.BeamKind.Refracted;
                beam.emitter = pivot; beam.sourceBeacon = beacon; beacon.beam = beam;

                beacon.flame = Find(side + "Beacon_Tower").GetComponentInChildren<Light>(true).gameObject;
                beacon.beaconLight = Ensure<Light>(beacon.flame); beacon.beaconLight.type = LightType.Point;
                beacon.beaconLight.color = new Color(1, 0.85f, 0.4f); beacon.beaconLight.range = 35; beacon.beaconLight.intensity = 6;
                beacon.flame.SetActive(false);
                beacon.crystal = null;
                Feedback(root.gameObject, "Assets/Core/Audio/LVL2_Chime.wav", Color.yellow, socketPosition);
            }
            beacons[0].pairedBeacon = beacons[1];
            beacons[1].pairedBeacon = beacons[0];

            var crankObject = stage4.Find("IgnitionCrank") != null ? stage4.Find("IgnitionCrank").gameObject :
                Cylinder("IgnitionCrank", stage4, new Vector3(95, 2.5f, 170), new Vector3(0.22f, 0.65f, 0.22f), gold);
            Ensure<NetworkObject>(crankObject); Ensure<NetworkTransform>(crankObject);
            items[0] = Ensure<CoopCarryableItem>(crankObject); items[0].itemType = CoopCarryableItem.ItemType.IgnitionCrank;
            items[0].socketOwner = beacons[0]; beacons[0].requiredItem = items[0];

            var logRoot = stage4.Find("East_FallenLogPuzzle") ?? Marker("East_FallenLogPuzzle", stage4, Vector3.zero);
            Ensure<NetworkObject>(logRoot.gameObject);
            var trunk = logRoot.Find("FallenLog") != null ? logRoot.Find("FallenLog").gameObject :
                Cylinder("FallenLog", logRoot, new Vector3(115, 2.2f, 192), new Vector3(0.65f, 3.6f, 0.65f), wood);
            trunk.transform.rotation = Quaternion.Euler(90, 0, 0);
            var grip = logRoot.Find("DadGrip") ?? Marker("DadGrip", logRoot, new Vector3(115, 1.2f, 189));
            var crawlObject = Trigger("East_LogCrawlVolume", logRoot, new Vector3(115, 1.15f, 192), new Vector3(1.2f, 0.75f, 4.8f));
            var crawl = Ensure<CoopCrawlVolume>(crawlObject); crawlObject.SetActive(false);
            var log = Ensure<LighthouseFallenLog>(logRoot.gameObject); log.trunk = trunk.transform; log.dadGrip = grip; log.crawl = crawl;
            if (stage4.Find("Pfb_AncientChest") == null)
                Box("Pfb_AncientChest", stage4, new Vector3(115, 1.25f, 194.5f), new Vector3(1.8f, 0.8f, 1.2f), wood);
            var prismObject = stage4.Find("AncientPrism") != null ? stage4.Find("AncientPrism").gameObject :
                Cylinder("AncientPrism", stage4, new Vector3(115, 1.9f, 194.5f), new Vector3(0.3f, 0.55f, 0.3f), gold);
            Ensure<NetworkObject>(prismObject); Ensure<NetworkTransform>(prismObject);
            items[1] = Ensure<CoopCarryableItem>(prismObject); items[1].itemType = CoopCarryableItem.ItemType.RefractionPrism;
            items[1].socketOwner = beacons[1]; beacons[1].requiredItem = items[1];

            var prismSocket = Ensure<LighthousePrismSocket>(sockets[1].gameObject); prismSocket.beacon = beacons[1];
            beams[0].prismSocket = prismSocket; beams[1].prismSocket = prismSocket;
            beams[0].emitter.LookAt(sockets[1].position);
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
            var sensorCollider = Ensure<BoxCollider>(controller.gameObject);
            sensorCollider.center = new Vector3(0, 4.5f, -5f); sensorCollider.size = new Vector3(2.2f, 2.2f, 0.7f); sensorCollider.isTrigger = true;
            var lens = controller.Find("LuminaMillSensor_Lens") != null ? controller.Find("LuminaMillSensor_Lens").gameObject :
                Cylinder("LuminaMillSensor_Lens", controller, new Vector3(0, 4.5f, -5f), new Vector3(0.8f, 0.2f, 0.8f), gold);
            lens.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var sensor = Ensure<LuminaMillSensor>(controller.gameObject);
            sensor.lensRenderer = lens.GetComponent<Renderer>(); sensor.west = beacons[0]; sensor.east = beacons[1];
            sensor.chime = Ensure<AudioSource>(lens); sensor.chime.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Core/Audio/LVL2_Chime.wav");
            sensor.chime.playOnAwake = false; sensor.chime.spatialBlend = 1;
            beams[1].millSensor = sensor; beams[1].emitter.LookAt(lens.transform.position);
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
            Save();
        }

        // Batch-mode entry point used by CI and task validation to guarantee the
        // authored lighthouse objects are serialized into PlayScene2.unity.
        public static void BakeStageThree()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            WireStageThree();
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

        public static void DressRapidsRocks(Transform stage)
        {
            for (int i = 1; i <= 2; i++)
            {
                var prefix = "Whirlpool_" + i + "_";
                Dress(stage.Find(prefix + "Rock1"), i == 1 ? 10 : 9, i * 90);
                Dress(stage.Find(prefix + "Rock2"), i == 1 ? 9 : 10, i * 180);
                Dress(stage.Find(prefix + "CoreRock"), 2, i * 180);
            }
            Dress(stage.Find("Canyon_Cliff_Left"), 11, 90, 9);
            Dress(stage.Find("Canyon_Cliff_Right"), 11, 270, 9);
        }

        static void Dress(Transform obstacle, int rock, int yaw, int count = 1)
        {
            // Core rocks are created when the gameplay pass wires the whirlpools.
            if (!obstacle || obstacle.Find("Rock Visuals")) return;
            var cover = new GameObject("Rock Visuals").transform;
            cover.SetParent(obstacle, false);
            var target = obstacle.GetComponent<BoxCollider>().bounds;
            float pitch = target.size.z / count;
            for (int i = 0; i < count; i++)
            {
                int model = count > 1 && i % 3 == 1 ? 4 : rock;
                var path = "Assets/Map Material/FlipGameDev/Terrain&GrassPack/Prefabs/Rocks/Rock_" + model + ".prefab";
                var fit = new GameObject("Rock " + (i + 1)).transform;
                fit.SetParent(cover, false);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), fit);
                instance.transform.localRotation = Quaternion.Euler(0, yaw + 180 * (i % 2), 0) * instance.transform.localRotation;
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
                var renderers = instance.GetComponentsInChildren<MeshRenderer>();
                Bounds BoundsOfRock()
                {
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    return bounds;
                }
                var size = BoundsOfRock().size;
                // Interlock neighbouring cliff rocks while keeping the two ends on the original bank.
                float overlap = count == 1 ? 0 : pitch * .16f;
                float start = target.min.z + pitch * i - (i > 0 ? overlap : 0);
                float end = target.min.z + pitch * (i + 1) + (i + 1 < count ? overlap : 0);
                float height = target.size.y * (count > 1 && i % 3 == 1 ? .88f : 1f);
                fit.localScale = new Vector3(target.size.x / size.x, height / size.y, (end - start) / size.z);
                fit.position += new Vector3(target.center.x, target.min.y + height * .5f, (start + end) * .5f) - BoundsOfRock().center;
            }
            Object.DestroyImmediate(obstacle.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(obstacle.GetComponent<MeshFilter>());
        }

        public static GameObject PlaceLighthouse(Transform stage, string side, Renderer island)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Core/Prefabs/Environment/Lighthouse.prefab");
            var tower = (GameObject)PrefabUtility.InstantiatePrefab(prefab, stage);
            tower.name = side + "Beacon_Tower";
            tower.transform.localRotation = Quaternion.Euler(0, side == "West" ? 0 : 180, 0);
            var meshes = tower.GetComponentsInChildren<MeshRenderer>(true);
            var body = meshes.Single(r => r.name == "Boole_1");
            float bottom = meshes.Min(r => r.bounds.min.y);
            float height = meshes.Max(r => r.bounds.max.y) - bottom;
            tower.transform.localScale *= 6.8f / height;
            var centre = body.bounds.center;
            var bank = island.bounds;
            tower.transform.position += new Vector3(bank.center.x - centre.x,
                bank.max.y - meshes.Min(r => r.bounds.min.y), bank.center.z - centre.z);
            var lamp = tower.GetComponentInChildren<Light>(true).gameObject;
            lamp.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lamp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tower.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tower);
            return tower;
        }

        public static void DressSluicePassage(SluiceGateObstacle sluice)
        {
            Dress(sluice.crawlDoor, 2, 40);
            sluice.winch.gameObject.SetActive(false);
            var stone = GateMasonry();
            var stage = sluice.transform;
            var facade = stage.Find("Castle Gatehouse");
            if (facade)
                foreach (var r in facade.GetComponentsInChildren<MeshRenderer>())
                    if (r.name == "Bank Masonry" || r.transform.parent.name == "Front_Gatehouse_Stone")
                        r.sharedMaterial = stone;
            var grid = sluice.gate.Find("Front_Harrow_Portcullis");
            if (grid) grid.GetComponentInChildren<MeshRenderer>().sharedMaterial =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Iron.mat");
        }

        static Material GateMasonry()
        {
            var stone = Material("GateMasonry", Color.white, 0, .18f);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/ForestMap/Textures/cobblestone/cobblestone_diff.jpg");
            stone.SetTexture("_BaseMap", texture);
            stone.SetTextureScale("_BaseMap", new Vector2(8, 4));
            // Keep the shaded river-facing wall legible even before the scene lighting starts.
            stone.EnableKeyword("_EMISSION");
            stone.SetTexture("_EmissionMap", texture);
            stone.SetColor("_EmissionColor", new Color(.17f, .17f, .17f));
            EditorUtility.SetDirty(stone);
            return stone;
        }

        public static void DressSluiceGate(Transform stage)
        {
            if (stage.Find("Castle Gatehouse")) return;
            const string path = "Assets/Castle_Fortress_v2.fbx";
            // Prefer the material overrides on the castle in this map.
            var castle = stage.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "Castle_Fortress_v2");
            if (!castle) castle = AssetDatabase.LoadAssetAtPath<GameObject>(path).transform;
            var gate = stage.Find("SluiceGate_IronPortcullis");
            var barrier = gate.GetComponent<BoxCollider>().bounds;
            var centre = barrier.center;
            var facade = Marker("Castle Gatehouse", stage, centre);
            var stone = GateMasonry();

            // The fortress opening is about one third of the full facade. Raise its
            // sill above the crawl roof and carry the flanks down to the river banks.
            float baseY = stage.Find("Crawl_Roof") is Transform roof
                ? roof.GetComponent<BoxCollider>().bounds.max.y : 2.73f;
            float width = barrier.size.x * 3.45f;
            Fit("Front_Gatehouse_Stone", facade, new Vector3(centre.x, baseY + 5.5f, centre.z), new Vector3(width, 11, 4));
            Fit("Front_Harrow_Portcullis", gate, centre, barrier.size);
            float crawlX = stage.Find("Sluice_RightTower").position.x;
            Foundation(centre.x - width / 2, barrier.min.x);
            Foundation(barrier.max.x, crawlX - .95f);
            Foundation(crawlX + .95f, centre.x + width / 2);
            foreach (var side in new[] { "Left", "Right" })
            {
                string torch = "Torch_Gate_Front_" + side;
                var pos = new Vector3(centre.x + (side == "Left" ? -1 : 1) * (barrier.extents.x + 1.4f), baseY + 4, centre.z - 2.15f);
                Fit(torch, facade, pos, new Vector3(.48f, 1.3f, .6f));
                Fit(torch + "_Flame", facade, pos + new Vector3(0, .12f, -.15f), new Vector3(.4f, .7f, .4f));
            }
            foreach (var name in new[] { "Sluice_LeftTower", "Sluice_RightTower", "Sluice_ArchBeam", "SluiceGate_IronPortcullis" })
                stage.Find(name).GetComponent<MeshRenderer>().enabled = false;

            void Foundation(float left, float right)
            {
                var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obj.name = "Bank Masonry";
                obj.transform.SetParent(facade, false);
                obj.transform.position = new Vector3((left + right) / 2, baseY / 2, centre.z);
                obj.transform.localScale = new Vector3(right - left, baseY, 4);
                obj.GetComponent<MeshRenderer>().sharedMaterial = stone;
                Object.DestroyImmediate(obj.GetComponent<BoxCollider>());
            }
            void Fit(string part, Transform parent, Vector3 position, Vector3 size)
            {
                var source = castle.Find(part);
                var fit = new GameObject(part).transform;
                fit.SetParent(stage, false);
                var model = new GameObject(source.GetComponent<MeshFilter>().sharedMesh.name);
                model.transform.SetParent(fit, false);
                model.transform.localRotation = Quaternion.Euler(0, 180, 0) * Quaternion.Inverse(castle.rotation) * source.rotation;
                model.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<MeshFilter>().sharedMesh;
                var renderer = model.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = source.GetComponent<MeshRenderer>().sharedMaterials;
                if (part == "Front_Gatehouse_Stone") renderer.sharedMaterial = stone;
                var measured = renderer.bounds.size;
                fit.localScale = new Vector3(size.x / measured.x, size.y / measured.y, size.z / measured.z);
                fit.position += position - renderer.bounds.center;
                fit.SetParent(parent, true);
            }
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
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Map Material/FlipGameDev/Terrain&GrassPack/Art/Materials/Water/WaterBasic.mat");
            if (waterMat == null) waterMat = Material("RiverWater", new Color(0.18f, 0.45f, 0.62f, 0.85f), 0.1f, 0.9f);

            // ==========================================
            // 1. START DOCK & SPAWN AREA (Z: 24 - 36)
            // ==========================================
            var startArea = Marker("01_StartDock_Area", Root.transform, Vector3.zero);
            // Wooden Pier
            var dockDeck = Box("StartDock_Pier", startArea, new Vector3(93.5f, 0.95f, 30.0f), new Vector3(4.5f, 0.25f, 9.0f), woodMat);
            // Support pilings
            Cylinder("Piling_1", startArea, new Vector3(91.5f, 0.3f, 26.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);
            Cylinder("Piling_2", startArea, new Vector3(95.5f, 0.3f, 26.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);
            Cylinder("Piling_3", startArea, new Vector3(91.5f, 0.3f, 34.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);
            Cylinder("Piling_4", startArea, new Vector3(95.5f, 0.3f, 34.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);
            // Mooring Bollards
            Cylinder("Bollard_1", startArea, new Vector3(95.5f, 1.25f, 32.0f), new Vector3(0.25f, 0.4f, 0.25f), ironMat);
            Cylinder("Bollard_2", startArea, new Vector3(95.5f, 1.25f, 34.0f), new Vector3(0.25f, 0.4f, 0.25f), ironMat);

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

            // Ensure Boat is placed by dock
            var boat = GameObject.Find("Coop_Wooden_Boat");
            if (boat != null)
            {
                boat.transform.position = new Vector3(97.2f, 0.53f, 34.2f);
                boat.transform.rotation = Quaternion.Euler(0, 5f, 0);
            }

            // ==========================================
            // 2. STAGE 1: SLALOM BUOYS & HAZARD ROCKS (Z: 40 - 68)
            // ==========================================
            var stage1 = Marker("02_SlalomCourse_Stage1", Root.transform, Vector3.zero);
            // Gate 1 (Z: 44)
            Cylinder("Buoy_1_Red", stage1, new Vector3(93.0f, 0.65f, 44.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyRedMat);
            Cylinder("Buoy_1_Green", stage1, new Vector3(103.0f, 0.65f, 44.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyGreenMat);
            var g1Trigger = Box("SlalomGate_1_Trigger", stage1, new Vector3(98.0f, 1.0f, 44.0f), new Vector3(9.0f, 2.5f, 1.5f));
            g1Trigger.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(g1Trigger.GetComponent<MeshRenderer>());

            // Gate 2 (Z: 56)
            Cylinder("Buoy_2_Red", stage1, new Vector3(92.5f, 0.65f, 56.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyRedMat);
            Cylinder("Buoy_2_Green", stage1, new Vector3(104.0f, 0.65f, 56.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyGreenMat);
            var g2Trigger = Box("SlalomGate_2_Trigger", stage1, new Vector3(98.2f, 1.0f, 56.0f), new Vector3(10.5f, 2.5f, 1.5f));
            g2Trigger.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(g2Trigger.GetComponent<MeshRenderer>());

            // Gate 3 (Z: 66)
            Cylinder("Buoy_3_Red", stage1, new Vector3(95.0f, 0.65f, 66.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyRedMat);
            Cylinder("Buoy_3_Green", stage1, new Vector3(106.0f, 0.65f, 66.0f), new Vector3(0.7f, 1.2f, 0.7f), buoyGreenMat);
            var g3Trigger = Box("SlalomGate_3_Trigger", stage1, new Vector3(100.5f, 1.0f, 66.0f), new Vector3(10.0f, 2.5f, 1.5f));
            g3Trigger.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(g3Trigger.GetComponent<MeshRenderer>());

            // Channel Hazard Rocks
            Box("HazardRock_Left_1", stage1, new Vector3(88.0f, 1.2f, 48.0f), new Vector3(4.0f, 2.2f, 6.0f), stoneMat);
            Box("HazardRock_Right_1", stage1, new Vector3(109.0f, 1.2f, 52.0f), new Vector3(4.5f, 2.4f, 7.0f), stoneMat);
            Box("HazardRock_Left_2", stage1, new Vector3(89.5f, 1.2f, 62.0f), new Vector3(3.5f, 2.0f, 5.0f), stoneMat);

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
            DressSluiceGate(stage2);

            // Side Dock for puzzle landing (Right bank)
            Box("Sluice_SideDock", stage2, new Vector3(107.5f, 0.95f, 70.0f), new Vector3(4.0f, 0.25f, 5.0f), woodMat);
            // Dad Winch Station
            var dadWinch = Box("Dad_Winch_Station", stage2, new Vector3(110.5f, 1.8f, 71.5f), new Vector3(1.2f, 1.6f, 1.2f), ironMat);
            Cylinder("Dad_Winch_Wheel", dadWinch.transform, new Vector3(0, 0.6f, 0.4f), new Vector3(1.2f, 0.15f, 1.2f), ironMat);

            // Child Crawl Conduit
            var crawlway = Box("Child_Crawlway_Conduit", stage2, new Vector3(109.5f, 0.9f, 74.0f), new Vector3(1.1f, 1.0f, 4.2f), stoneMat);
            var crawlVol = crawlway.AddComponent<CoopCrawlVolume>();
            crawlway.GetComponent<BoxCollider>().isTrigger = true;

            // Upper Control Balcony & Valve Lever
            Box("Upper_Balcony", stage2, new Vector3(109.5f, 4.2f, 75.5f), new Vector3(3.5f, 0.3f, 3.5f), woodMat);
            var lever = Cylinder("WaterValve_Lever", stage2, new Vector3(109.5f, 5.0f, 75.5f), new Vector3(0.2f, 1.2f, 0.2f), goldMat);

            // ==========================================
            // 4. STAGE 3: RAPIDS RUN & TWIN WHIRLPOOLS (Z: 86 - 150)
            // ==========================================
            var stage3 = Marker("04_RapidsWhirlpools_Stage3", Root.transform, Vector3.zero);
            // Water planes extension for rapids channel
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
            Box("Whirlpool_1_Rock1", stage3, new Vector3(91.0f, 1.5f, 102.0f), new Vector3(2.5f, 3.0f, 3.0f), stoneMat);
            Box("Whirlpool_1_Rock2", stage3, new Vector3(103.0f, 1.5f, 102.0f), new Vector3(2.5f, 3.0f, 3.0f), stoneMat);

            // Whirlpool 2 (Z: 128)
            var wp2 = Cylinder("Whirlpool_2_Vortex", stage3, new Vector3(104.0f, 0.45f, 128.0f), new Vector3(9.5f, 0.1f, 9.5f), ironMat);
            wp2.GetComponent<Collider>().isTrigger = true;
            Cylinder("Whirlpool_2_Ring", stage3, new Vector3(104.0f, 0.48f, 128.0f), new Vector3(13.5f, 0.05f, 13.5f), buoyRedMat);
            Box("Whirlpool_2_Rock1", stage3, new Vector3(98.0f, 1.5f, 128.0f), new Vector3(2.5f, 3.0f, 3.0f), stoneMat);
            Box("Whirlpool_2_Rock2", stage3, new Vector3(110.0f, 1.5f, 128.0f), new Vector3(2.5f, 3.0f, 3.0f), stoneMat);

            // Canyon Gorge Clifflines
            Box("Canyon_Cliff_Left", stage3, new Vector3(85.0f, 3.0f, 115.0f), new Vector3(4.0f, 6.0f, 65.0f), stoneMat);
            Box("Canyon_Cliff_Right", stage3, new Vector3(116.0f, 3.0f, 115.0f), new Vector3(4.0f, 6.0f, 65.0f), stoneMat);
            DressRapidsRocks(stage3);

            // ==========================================
            // 5. STAGE 4: MISTY MARSH & TWIN BEACONS (Z: 155 - 215)
            // ==========================================
            var stage4 = Marker("05_MistyMarsh_Stage4", Root.transform, Vector3.zero);
            // Wide Basin Water Plane
            var marshWater = GameObject.CreatePrimitive(PrimitiveType.Plane);
            marshWater.name = "Water_Marsh_Basin";
            marshWater.transform.SetParent(stage4, false);
            marshWater.transform.position = new Vector3(100.0f, 0.5f, 185.0f);
            marshWater.transform.localScale = new Vector3(5.0f, 1.0f, 5.0f);
            marshWater.GetComponent<Renderer>().sharedMaterial = waterMat;

            // West Beacon Tower & Island (Left)
            var westIsland = Cylinder("WestBeacon_Island", stage4, new Vector3(83.0f, 0.9f, 175.0f), new Vector3(9.0f, 0.8f, 9.0f), stoneMat);
            PlaceLighthouse(stage4, "West", westIsland.GetComponent<Renderer>());

            // East Beacon Tower & Island (Right)
            var eastIsland = Cylinder("EastBeacon_Island", stage4, new Vector3(117.0f, 0.9f, 190.0f), new Vector3(9.0f, 0.8f, 9.0f), stoneMat);
            PlaceLighthouse(stage4, "East", eastIsland.GetComponent<Renderer>());

            // Giant Lily Stepping Stones (BoostThrow landing area for Child)
            Cylinder("GiantLilyPad_1", stage4, new Vector3(90.0f, 0.55f, 175.0f), new Vector3(3.2f, 0.1f, 3.2f), buoyGreenMat);
            Cylinder("GiantLilyPad_2", stage4, new Vector3(110.0f, 0.55f, 190.0f), new Vector3(3.2f, 0.1f, 3.2f), buoyGreenMat);
            // Lumina Crystal Pedestal on West Island
            Box("LuminaCrystal_Pedestal", stage4, new Vector3(83.0f, 1.6f, 172.5f), new Vector3(0.8f, 1.2f, 0.8f), stoneMat);
            Box("LuminaCrystal_Gem", stage4, new Vector3(83.0f, 2.4f, 172.5f), new Vector3(0.5f, 0.6f, 0.5f), goldMat);

            // ==========================================
            // 6. STAGE 5: GIANT WATERMILL CLIMAX & HAVEN (Z: 216 - 265)
            // ==========================================
            var stage5 = Marker("06_WatermillHaven_Stage5", Root.transform, Vector3.zero);
            // Gorge Archway before Watermill
            Box("Watermill_LeftGorge", stage5, new Vector3(89.0f, 5.0f, 225.0f), new Vector3(5.0f, 10.0f, 4.0f), stoneMat);
            Box("Watermill_RightGorge", stage5, new Vector3(111.0f, 5.0f, 225.0f), new Vector3(5.0f, 10.0f, 4.0f), stoneMat);

            // Giant Watermill Axis & 4 Paddles
            var millRoot = Marker("GiantWatermill_Assembly", stage5, new Vector3(100.0f, 4.5f, 225.0f));
            Cylinder("Watermill_Hub", millRoot, Vector3.zero, new Vector3(1.8f, 16.0f, 1.8f), ironMat);
            millRoot.transform.Rotate(0, 0, 90f); // Horizontal axle across river

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
            Cylinder("HavenHarbor_Post_L", stage5, new Vector3(96.5f, 1.3f, 246.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);
            Cylinder("HavenHarbor_Post_R", stage5, new Vector3(103.5f, 1.3f, 246.0f), new Vector3(0.35f, 1.2f, 0.35f), woodMat);

            // Altar of Dawn & Victory Trigger
            var altar = Box("AltarOfDawn_Podium", stage5, new Vector3(100.0f, 1.4f, 256.0f), new Vector3(3.5f, 0.8f, 3.5f), stoneMat);
            Cylinder("Altar_MemoryCrystal", altar.transform, new Vector3(0, 0.8f, 0), new Vector3(0.8f, 1.2f, 0.8f), goldMat);
            var victoryTrig = Box("LevelTwo_VictoryTrigger", stage5, new Vector3(100.0f, 1.8f, 256.0f), new Vector3(5.0f, 3.0f, 5.0f));
            victoryTrig.GetComponent<BoxCollider>().isTrigger = true;
            Object.DestroyImmediate(victoryTrig.GetComponent<MeshRenderer>());

            // ==========================================
            // 7. CHECKPOINTS
            // ==========================================
            var cpRoot = Marker("Checkpoints", Root.transform, Vector3.zero);
            Marker("CP0_StartDock", cpRoot, new Vector3(93.5f, 1.2f, 28.5f));
            Marker("CP1_PostSluice", cpRoot, new Vector3(100.5f, 1.0f, 80.0f));
            Marker("CP2_PostRapids", cpRoot, new Vector3(100.0f, 1.0f, 150.0f));
            Marker("CP3_HavenHarbor", cpRoot, new Vector3(100.0f, 1.2f, 250.0f));

            // Mark dirty and save
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("[LVL2] Complete River Map built successfully in " + ScenePath + "!");
        }
    }
}
