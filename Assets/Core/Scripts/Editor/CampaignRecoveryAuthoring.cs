using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Blocks.Gameplay.Core
{
    public static class CampaignRecoveryAuthoring
    {
        public static void FixPassage(SluiceGateObstacle sluice)
        {
            var root = sluice.transform;
            Transform Part(string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
            // 1.10 m clearance for a 0.75 m capsule, including controller skin width.
            var roof = Part("Crawl_Roof"); roof.position = new Vector3(109.5f, 2.45f, 74); roof.localScale = new Vector3(1.8f, .55f, 4.5f);
            var tower = Part("Sluice_RightTower"); tower.position = new Vector3(109.5f, 5.5f, 74); tower.localScale = new Vector3(4, 6, 4.5f);
            Part("Crawl_LeftWall").position = new Vector3(108.4f, 1.6f, 74);
            Part("Crawl_RightWall").position = new Vector3(110.6f, 1.6f, 74);
            Part("Crawl_Floor").localScale = new Vector3(1.8f, .25f, 7);
            sluice.crawl.transform.position = new Vector3(109.5f, 1.7f, 74);
            sluice.crawl.size = new Vector3(1.7f, 1.3f, 7f);
            var crawl = new SerializedObject(sluice.crawl.GetComponent<CoopCrawlVolume>());
            crawl.FindProperty("crawlHeight").floatValue = .75f; crawl.ApplyModifiedPropertiesWithoutUndo();
            sluice.crawlDoor.position = new Vector3(109.5f, 1.6f, 71.3f);
            sluice.crawlDoor.localScale = new Vector3(1.95f, 1.3f, 1f);
            LevelTwoAuthoring.LevelTwoSceneBuilder.DressSluicePassage(sluice);
        }
        [MenuItem("Hold My Hand/Campaign/Apply recovery and lobby selection")]
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode first.");
            string original = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            for (int level = 1; level <= 2; level++)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Core/Scenes/" + CampaignProgress.Scene(level) + ".unity");
                var host = level == 1 ? Object.FindAnyObjectByType<ChaseBossController>().gameObject : Object.FindAnyObjectByType<CoopWoodenBoat>().gameObject;
                var controller = host.GetComponent<CampaignCheckpointController>();
                if (controller == null) controller = host.AddComponent<CampaignCheckpointController>();
                controller.level = level;
                if (level == 2) FixPassage(Object.FindAnyObjectByType<SluiceGateObstacle>());
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            var lobby = EditorSceneManager.OpenScene("Assets/Core/Scenes/Lobby.unity");
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(c => c.name == "LobbyCanvas");
            var existing = canvas.transform.Find("campaign-selection-panel");
            if (existing == null)
            {
                var panel = new GameObject("campaign-selection-panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Image));
                panel.transform.SetParent(canvas.transform, false);
                var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1, 0);
                rect.sizeDelta = new Vector2(360, 152); rect.anchoredPosition = new Vector2(-32, 338);
                panel.GetComponent<UnityEngine.UI.Image>().color = new Color(.045f, .065f, .09f, .94f);
                var prototype = canvas.GetComponentsInChildren<Button>(true).First(b => b.name == "play-button");
                string[] names = { "campaign-continue-button", "campaign-level1-button", "campaign-level2-button" };
                string[] labels = { "TIẾP TỤC TIẾN ĐỘ ĐÃ LƯU", "MÀN 1 · TỪ ĐẦU", "MÀN 2 · TỪ ĐẦU" };
                for (int i = 0; i < 3; i++)
                {
                    var button = Object.Instantiate(prototype, panel.transform); button.name = names[i];
                    button.onClick = new Button.ButtonClickedEvent(); button.gameObject.SetActive(true);
                    var rt = button.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 0);
                    rt.anchoredPosition = new Vector2(8, i == 0 ? 65 : 20); rt.sizeDelta = new Vector2(i == 0 ? 344 : 168, 38);
                    if (i == 2) rt.anchoredPosition = new Vector2(184, 20);
                    var label = button.GetComponentInChildren<TextMeshProUGUI>(); label.text = labels[i]; label.fontSize = 15;
                }
                var text = new GameObject("campaign-selection-label", typeof(RectTransform), typeof(TextMeshProUGUI));
                text.transform.SetParent(panel.transform, false);
                var tr = text.GetComponent<RectTransform>(); tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0, 0);
                tr.anchoredPosition = new Vector2(8, 108); tr.sizeDelta = new Vector2(344, 36);
                var tmp = text.GetComponent<TextMeshProUGUI>(); tmp.font = prototype.GetComponentInChildren<TextMeshProUGUI>().font;
                tmp.fontSize = 16; tmp.color = Color.white; tmp.alignment = TextAlignmentOptions.Center; tmp.text = "CHỌN MÀN / TIẾP TỤC";
            }
            EnsureThreeLevelButtons(canvas.transform.Find("campaign-selection-panel"));
            EditorSceneManager.MarkSceneDirty(lobby); EditorSceneManager.SaveScene(lobby);
            if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original);
            AssetDatabase.SaveAssets();
        }

        public static void EnsureThreeLevelButtons(Transform panel)
        {
            var prototype = panel.Find("campaign-level1-button").GetComponent<Button>();
            const float width = (344f - 16f) / 3f;
            for (int level = 1; level <= 3; level++)
            {
                string name = "campaign-level" + level + "-button";
                var existing = panel.Find(name);
                var button = existing != null ? existing.GetComponent<Button>() : Object.Instantiate(prototype, panel);
                if (existing == null)
                {
                    button.name = name;
                    button.onClick = new Button.ButtonClickedEvent();
                }
                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                rect.anchoredPosition = new Vector2(8 + (level - 1) * (width + 8), 20);
                rect.sizeDelta = new Vector2(width, 38);
                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                label.text = "MÀN " + level;
                label.fontSize = 15;
                button.gameObject.SetActive(true);
            }
        }
    }
}
