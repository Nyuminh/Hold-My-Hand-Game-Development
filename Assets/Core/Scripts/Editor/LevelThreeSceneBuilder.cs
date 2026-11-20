using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blocks.Gameplay.Core
{
    public static class LevelThreeSceneBuilder
    {
        const string ScenePath = "Assets/Core/Scenes/PlayScene4.unity";
        const string RootName = "LVL3_Castle";

        [MenuItem("Hold My Hand/Level 3/Build Parkour Map")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
            var previous = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            if (scene.isDirty) throw new InvalidOperationException("Save pending PlayScene4 edits first.");
            SceneManager.SetActiveScene(scene);
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var go in roots.Where(o => o.name == RootName || o.name == "LVL1_Encounter" ||
                    o.name == "Environment" || o.name.StartsWith("Pfb_SpawnPad", StringComparison.Ordinal) ||
                    o.name == "Day-Night-Manager" || o.name == "DamageArea"))
                    Object.DestroyImmediate(go);

                var root = new GameObject(RootName).transform;
                var gorge = Group("Drawbridge Ravine", root);
                var crypt = Group("Castle Crypt", root);
                var pillars = Group("Fractured Pillars", root);
                var tower = Group("Clocktower Summit", root);
                var net = Group("Coop Tether", root).gameObject;
                var tether = net.AddComponent<CoopTetherChain>();
                tether.abyss = Group("Abyss", root).gameObject.AddComponent<AbyssFallTrigger>();
                tether.abyss.transform.position = Vector3.down * 2.5f;
                tether.clink = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Platformer/Audio/Clips/RotatingObstacleImpact.wav");
                var line = net.GetComponent<LineRenderer>();
                line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
                line.enabled = false;
                net.GetComponent<AudioSource>().playOnAwake = false;
                // Coordinates describe the top centre and outside dimensions of each piece.
                Piece(gorge, "Platform", "Departure", 0, 0, 3, 9, 3, 14);
                Piece(gorge, "StoneBridgeFlat", "Bridge Landing", 0, 0, 20, 8, 2, 10);
                Piece(crypt, "Platform", "Crypt Floor", 0, 0, 34, 8, 3, 18);
                Piece(crypt, "Walls/Walllvl1Merged", "West Rampart", -4.4f, 5, 34, 1, 8, 18);
                Piece(crypt, "Walls/Walllvl1Merged", "East Rampart", 4.4f, 5, 34, 1, 8, 18);
                Piece(crypt, "Gates/WallEntrance", "North Arch", 8, 6, 43, 6, 9, 3);
                for (int i = 0; i < 5; i++)
                    Piece(pillars, "Platform", "Pillar " + (i + 1), (i % 2 == 0 ? -1 : 1), i * .22f,
                        45.5f + i * 4, 3, 5 + i * .22f, 3);
                Piece(tower, "Platform", "Clockwork Walk", 0, 1, 71, 7, 5, 16);
                Piece(tower, "Platform", "Altar Terrace", 0, 1, 85, 10, 6, 12);
                Piece(tower, "HightTower", "Belfry", -10, 19, 83, 8, 25, 8);
                Piece(tower, "Ramp", "Belfry Buttress", -8, 1, 75, 5, 6, 8);
                foreach (int z in new[] { 4, 22, 30, 40, 70, 86 })
                    Piece(root, "FloorTorch", "Torch " + z, 3, z >= 70 ? 3 : 2, z, .6f, 2, .6f);

                BuildPuzzles(gorge, crypt, pillars, tower);
                BuildCampaign(root, tether);
                var gm = scene.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<GameManager>(true)).Single();
                var settings = new SerializedObject(gm);
                var spawns = settings.FindProperty("spawnPoints");
                spawns.arraySize = 2;
                for (int i = 0; i < 2; i++)
                {
                    var spawn = Group(i == 0 ? "Dad Spawn" : "Child Spawn", gorge);
                    spawn.position = new Vector3(i == 0 ? -1 : 1, .2f, 0);
                    spawns.GetArrayElementAtIndex(i).objectReferenceValue = spawn;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                var sun = roots.Where(o => o && o.name == "Directional Light").Select(o => o.GetComponent<Light>()).Single();
                sun.transform.rotation = Quaternion.Euler(24, -38, 0);
                sun.color = new Color(1f, .76f, .52f);
                RenderSettings.sun = sun;
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(.65f, .53f, .55f);
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogDensity = .008f;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save PlayScene4.");
            }
            finally { if (previous.isLoaded) SceneManager.SetActiveScene(previous); }
        }

        static void BuildCampaign(Transform root, CoopTetherChain tether)
        {
            var campaign = Mechanism<LevelThreeManager>("Level 3 Campaign", root);
            campaign.tether = tether;
            campaign.bridge = root.GetComponentInChildren<LevelThreeDrawbridgePulley>();
            campaign.gate = root.GetComponentInChildren<LevelThreeCryptCrawlGate>();
            campaign.altar = root.GetComponentInChildren<DualAltarPortal>();
            campaign.pillars = root.GetComponentsInChildren<CollapsingPlatform>();
            campaign.checkpoints = new Transform[10];
            float[] stations = { 0, 22, 40, 65, 85 };
            for (int i = 0; i < stations.Length; i++)
            {
                float y = i >= 3 ? 1.35f : .3f;
                campaign.checkpoints[i * 2] = Mark("CP" + i + " Dad", campaign.transform, -1, y, stations[i]);
                campaign.checkpoints[i * 2 + 1] = Mark("CP" + i + " Child", campaign.transform, 1, y, stations[i]);
            }
            var hud = new GameObject("Objective HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            hud.transform.SetParent(campaign.transform, false);
            var canvas = hud.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 5;
            var scaler = hud.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            var panel = new GameObject("Objective Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            panel.transform.SetParent(hud.transform, false);
            panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.045f, .055f, .075f, .86f);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -12); rect.sizeDelta = new Vector2(640, 58);
            var label = new GameObject("Objective", typeof(RectTransform), typeof(UnityEngine.UI.Text));
            label.transform.SetParent(panel.transform, false);
            campaign.objective = label.GetComponent<UnityEngine.UI.Text>();
            var textRect = campaign.objective.rectTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 4); textRect.offsetMax = new Vector2(-12, -4);
            campaign.objective.fontSize = 22;
            campaign.objective.alignment = TextAnchor.MiddleCenter;
            campaign.objective.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            campaign.objective.raycastTarget = false;
            campaign.objective.text = "Đang chờ đủ Ba và Con…";
        }
        static void BuildPuzzles(Transform gorge, Transform crypt, Transform pillars, Transform tower)
        {
            var pulley = Mechanism<LevelThreeDrawbridgePulley>("Drawbridge Pulley", gorge);
            pulley.hinge = Mark("Hinge", pulley.transform, 0, 0, 15);
            var bridge = Block("Drawbridge", pulley.hinge, new Vector3(0, -.12f, 12.5f), new Vector3(3.2f, .24f, 5));
            SolidMechanism(bridge);
            // Keep the flight path beside the raised bridge leaf.
            pulley.launch = Cylinder("Throw Pad", pulley.transform, new Vector3(2.7f, .04f, 8.7f), new Vector3(2, .04f, 2));
            pulley.landing = Cylinder("Landing", pulley.transform, new Vector3(2.8f, .04f, 16.8f), new Vector3(1.6f, .04f, 1.6f));
            pulley.pulley = Block("Winch", pulley.transform, new Vector3(1.8f, .65f, 17), new Vector3(.6f, 1.3f, .6f)).transform;
            var drum = Cylinder("Winch Drum", pulley.transform, new Vector3(1.8f, 1.2f, 17), new Vector3(.85f, .32f, .85f));
            drum.localRotation = Quaternion.Euler(0, 0, 90);
            Block("Crank", pulley.transform, new Vector3(2.18f, 1.42f, 17), new Vector3(.13f, .58f, .14f));
            pulley.hinge.localRotation = Quaternion.Euler(85, 0, 0);

            var cryptGate = Mechanism<LevelThreeCryptCrawlGate>("Crypt Gate", crypt);
            cryptGate.gate = Block("Portcullis", cryptGate.transform, new Vector3(-1.2f, 2, 34), new Vector3(5.6f, 4, .65f)).transform;
            SolidMechanism(cryptGate.gate.gameObject);
            Block("Crawl Roof", cryptGate.transform, new Vector3(2.8f, 2.65f, 34), new Vector3(2.4f, 2.95f, 4));
            var volume = Mark("Crawl Passage", cryptGate.transform, 2.8f, .8f, 33.8f);
            cryptGate.crawl = volume.gameObject.AddComponent<BoxCollider>();
            cryptGate.crawl.size = new Vector3(2.4f, 1.6f, 6.5f);
            cryptGate.crawl.isTrigger = true;
            volume.gameObject.AddComponent<CoopCrawlVolume>();
            cryptGate.lever = Block("Inner Lever", cryptGate.transform, new Vector3(2, .55f, 37.4f), new Vector3(.35f, 1.1f, .35f)).transform;

            foreach (Transform stone in pillars.Cast<Transform>().ToArray())
            {
                var platform = Mechanism<CollapsingPlatform>(stone.name + " Support", pillars);
                stone.SetParent(platform.transform, true);
                platform.stone = stone;
                platform.tread = stone.Find("Stone Tread").GetComponent<BoxCollider>();
            }

            var trap = Mechanism<ClocktowerGearHazard>("Clockwork", tower);
            trap.gear = Mark("Gear Axle", trap.transform, 0, 1.5f, 69);
            var cross = Block("Gear Teeth", trap.gear, trap.gear.position, new Vector3(5.5f, .55f, .8f));
            trap.blade = cross.GetComponent<BoxCollider>();
            trap.blade.isTrigger = true;
            Cylinder("Gear Wheel", trap.gear, trap.gear.position, new Vector3(2.7f, .22f, 2.7f));
            for (int i = 0; i < 10; i++)
            {
                var rotation = Quaternion.Euler(0, 36 * i, 0);
                var tooth = Block("Gear Tooth", trap.gear, trap.gear.position + rotation * Vector3.forward * 1.4f,
                    new Vector3(.36f, .46f, .4f));
                tooth.transform.rotation = rotation;
                tooth.GetComponent<BoxCollider>().isTrigger = true;
            }
            trap.pendulum = Mark("Pendulum Pivot", trap.transform, 0, 5.8f, 75);
            var rod = Block("Pendulum Rod", trap.pendulum, new Vector3(0, 4, 75), new Vector3(.1f, 3.6f, .1f));
            rod.GetComponent<BoxCollider>().enabled = false;
            trap.bob = Block("Pendulum Weight", trap.pendulum, new Vector3(0, 2, 75), new Vector3(1.4f, 1.4f, 1.4f)).GetComponent<BoxCollider>();
            trap.bob.isTrigger = true;

            var altar = Mechanism<DualAltarPortal>("Twin Altars", tower);
            altar.dadPad = Block("Dad Altar", altar.transform, new Vector3(-2, 1.1f, 85), new Vector3(2, .2f, 2)).transform;
            altar.childPad = Block("Child Altar", altar.transform, new Vector3(2, 1.1f, 85), new Vector3(2, .2f, 2)).transform;
            altar.portal = Mark("Portal of Light", altar.transform, 0, 2.5f, 89).gameObject;
            var light = altar.portal.AddComponent<Light>(); light.color = new Color(1, .83f, .45f); light.range = 12; light.intensity = 8;
            Block("Lintel", altar.portal.transform, new Vector3(0, 4.6f, 89), new Vector3(4, .5f, 1));
            foreach (int side in new[] {-1, 1}) Block("Pillar", altar.portal.transform, new Vector3(side * 1.8f, 2.7f, 89), new Vector3(.4f, 3.4f, 1));
            altar.portal.SetActive(false);
        }

        static T Mechanism<T>(string name, Transform parent) where T : Unity.Netcode.NetworkBehaviour
        {
            var obj = Group(name, parent).gameObject;
            obj.AddComponent<Unity.Netcode.NetworkObject>();
            return obj.AddComponent<T>();
        }

        static Transform Mark(string name, Transform parent, float x, float y, float z)
        {
            var t = Group(name, parent); t.position = new Vector3(x, y, z); return t;
        }

        static GameObject Block(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position; obj.transform.localScale = size;
            string material = name == "Drawbridge" || name == "Inner Lever" ? "LVL2_Wood" :
                name.Contains("Altar") || name == "Lintel" || name == "Pillar" ? "LVL2_Gold" : "LVL2_Iron";
            obj.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/" + material + ".mat");
            return obj;
        }

        static void SolidMechanism(GameObject obj)
        {
            var rb = obj.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        }

        static Transform Cylinder(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(obj.GetComponent<CapsuleCollider>());
            obj.AddComponent<MeshCollider>().sharedMesh = obj.GetComponent<MeshFilter>().sharedMesh;
            obj.name = name;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.transform.localScale = size;
            obj.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Core/Materials/LVL2_Iron.mat");
            return obj.transform;
        }
        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        static void Piece(Transform parent, string asset, string name, float x, float top, float z,
            float width, float height, float depth)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CastlePack/Prefabs/" + asset + ".prefab");
            if (!prefab) throw new InvalidOperationException("Missing castle prefab " + asset);
            var group = Group(name, parent);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
            var renderers = visual.GetComponentsInChildren<MeshRenderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            visual.transform.position -= new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            group.localScale = new Vector3(width / bounds.size.x, height / bounds.size.y, depth / bounds.size.z);
            group.position = new Vector3(x, top, z);
            if (asset == "Platform" || asset == "StoneBridgeFlat")
            {
                // These meshes have recessed centres. Cap the masonry with a visible, level tread.
                var tread = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tread.name = "Stone Tread";
                tread.transform.position = group.position + Vector3.down * .09f;
                tread.transform.localScale = new Vector3(width, .18f, depth);
                tread.transform.SetParent(group, true);
                tread.GetComponent<MeshRenderer>().sharedMaterial = renderers[0].sharedMaterial;
            }
        }
    }
}
