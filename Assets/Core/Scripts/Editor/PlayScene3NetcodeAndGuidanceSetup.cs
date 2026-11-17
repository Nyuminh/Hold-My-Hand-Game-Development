using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Blocks.Gameplay.Core;

namespace Blocks.Gameplay.Core
{
    public static class PlayScene3NetcodeAndGuidanceSetup
    {
        [MenuItem("Tools/Coop/Setup PlayScene3 Netcode and Guidance")]
        public static void ExecuteSetup()
        {
            string scenePath = "Assets/Core/Scenes/PlayScene3.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"Could not open scene {scenePath}");
                return;
            }

            Debug.Log("[Setup] Setting up PlayScene3 Netcode & Objective Director...");

            // 1. Move Zone3_Electrical_Box onto deck to clear the rock obstruction
            var electricBox = GameObject.Find("Zone3_Electrical_Box");
            if (electricBox != null)
            {
                electricBox.transform.position = new Vector3(0.15f, 14.35f, -45.50f);
                Debug.Log($"[Setup] Moved Zone3_Electrical_Box to {electricBox.transform.position} (clear of all rocks)");
            }

            // 2. Adjust Pushable_Boulder position and ensure it doesn't block the walkway
            var boulder = GameObject.Find("Pushable_Boulder");
            if (boulder != null)
            {
                boulder.transform.position = new Vector3(-4.20f, 15.40f, -54.00f);
                var sphereCol = boulder.GetComponent<SphereCollider>();
                if (sphereCol != null)
                {
                    sphereCol.radius = 0.5f;
                }
                Debug.Log($"[Setup] Adjusted Pushable_Boulder at {boulder.transform.position}");
            }

            // 3. Add NetworkObject to all interactive GameObjects that require Netcode
            EnsureNetworkObject("Zone1_Lever_PivotBox_Left");
            EnsureNetworkObject("Crane_Elevator_System");
            EnsureNetworkObject("Zone3_Gear_Turntable_Pivot");
            EnsureNetworkObject("Giant_Stone_Gear_Body");
            EnsureNetworkObject("Stage3_Winch_Assembly");
            EnsureNetworkObject("Cove_Winch_Assembly");
            EnsureNetworkObject("Pushable_Boulder");

            // Also check for any GameObject with NetworkBehaviour that doesn't have NetworkObject
            var allNetworkBehaviours = Object.FindObjectsByType<NetworkBehaviour>(FindObjectsSortMode.None);
            foreach (var nb in allNetworkBehaviours)
            {
                if (nb.GetComponent<NetworkObject>() == null)
                {
                    var no = nb.gameObject.AddComponent<NetworkObject>();
                    Debug.Log($"[Setup] Added NetworkObject to {nb.gameObject.name} for {nb.GetType().Name}");
                }
            }

            // 4. Add Directional Guidance (TutorialObjectiveDirector + UIDocument)
            var existingDirector = Object.FindObjectsByType<TutorialObjectiveDirector>(FindObjectsSortMode.None);
            GameObject directorGo;
            TutorialObjectiveDirector directorComp;
            if (existingDirector.Length > 0)
            {
                directorComp = existingDirector[0];
                directorGo = directorComp.gameObject;
            }
            else
            {
                directorGo = new GameObject("PlayScene3_ObjectiveDirector");
                directorComp = directorGo.AddComponent<TutorialObjectiveDirector>();
            }

            // Ensure UIDocument with TutorialHUD.uxml
            var uiDoc = directorGo.GetComponent<UIDocument>();
            if (uiDoc == null) uiDoc = directorGo.AddComponent<UIDocument>();
            var hudAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Core/UI/TutorialHUD.uxml");
            if (hudAsset != null)
            {
                uiDoc.visualTreeAsset = hudAsset;
                Debug.Log("[Setup] Assigned TutorialHUD.uxml to UIDocument");
            }
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Core/UI/UI Toolkit Panel Settings.asset");
            if (panelSettings != null)
            {
                uiDoc.panelSettings = panelSettings;
            }

            // Save Scene
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Setup] PlayScene3 saved: {saved}!");
        }

        private static void EnsureNetworkObject(string goName)
        {
            var go = GameObject.Find(goName);
            if (go != null)
            {
                if (go.GetComponent<NetworkObject>() == null)
                {
                    go.AddComponent<NetworkObject>();
                    Debug.Log($"[Setup] Added NetworkObject to '{goName}'");
                }
            }
            else
            {
                Debug.LogWarning($"[Setup] GameObject '{goName}' not found.");
            }
        }
    }
}
