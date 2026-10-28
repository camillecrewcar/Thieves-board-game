using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the game used by mlagents-learn, also from the command line:
// Unity.exe -batchmode -quit -projectPath . -executeMethod TrainingBuild.Build -logFile build.log
public static class TrainingBuild
{
    const string OutputPath = "Build/Thieves(board Game).exe";

    [MenuItem("Thieves/Build for training")]
    public static void Build()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Training build: " + report.summary.result + " (" + report.summary.totalErrors + " errors)");
        // a failed build must fail the command line run too
        if (Application.isBatchMode && report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}
