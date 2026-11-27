using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace Blocks.Gameplay.Core.LevelTwoAuthoring
{
    [InitializeOnLoad]
    internal static class CodexLighthouseTaskRunner
    {
        private const string RequestPath = "Temp/CodexLighthouseTask.request";
        private const string StatusPath = "Temp/CodexLighthouseTask.status";

        static CodexLighthouseTaskRunner()
        {
            EditorApplication.delayCall += Run;
        }

        private static void Run()
        {
            if (!File.Exists(RequestPath)) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // The requested bake/validation owns the next editor run. Leave any
                // previous QA play session cleanly, then resume from delayCall.
                EditorApplication.isPlaying = false;
                EditorApplication.delayCall += Run;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += Run;
                return;
            }

            if (Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(scene => scene.isDirty))
            {
                File.WriteAllText(StatusPath, "BLOCKED: an open scene has unsaved changes");
                return;
            }

            try
            {
                LevelTwoSceneBuilder.BakeStageThree();
                File.Delete(RequestPath);
                File.WriteAllText(StatusPath, "BAKED: starting LevelTwoValidation.RunStageThree");
                LevelTwoValidation.RunStageThree();
            }
            catch (System.Exception exception)
            {
                File.WriteAllText(StatusPath, "FAILED: " + exception);
                throw;
            }
        }
    }
}
