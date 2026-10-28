using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TitanBuild
{
    const string ScenePath = "Assets/Scenes/Main.unity";
    [MenuItem("Titan/Create Starter Scene")]
    public static void CreateScene()
    {
        AngelVisualAssets.Ensure();
        if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory("Assets/Scenes");
        Directory.CreateDirectory("Assets/Resources");
        if (!File.Exists("Assets/Resources/SpriteBase.mat"))
            AssetDatabase.CreateAsset(new Material(Shader.Find("Sprites/Default")), "Assets/Resources/SpriteBase.mat");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Angel Bounce").AddComponent<AngelBounceGame>();
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("Starter scene created. Press Play to test. C# and mobile verification are still required.");
    }
    [MenuItem("Titan/Build Mac")]
    public static void BuildMac()
    {
        CreateScene();
        PlayerSettings.productName = "Angel Bounce";
        PlayerSettings.companyName = "Titan";
        PlayerSettings.runInBackground = true;
        PlayerSettings.defaultScreenWidth = 1280; PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        string day = Directory.GetParent(Application.dataPath).Parent.FullName;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = Path.Combine(day, "Builds/Mac/AngelBounce.app"),
            target = BuildTarget.StandaloneOSX, options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Mac build failed");
        Debug.Log("ANGEL_BUILD_SUCCESS: " + report.summary.totalSize);
    }
    [MenuItem("Titan/Build iOS Xcode")]
    public static void BuildIOS()
    {
        if (EditorApplication.isPlaying) throw new BuildFailedException("Stop Play before building.");
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.iOS, BuildTarget.iOS))
            throw new BuildFailedException("Install iOS Build Support for Unity 6000.3.26f1.");
        CreateScene();
        PlayerSettings.productName = "Angel Bounce";
        PlayerSettings.companyName = "Titan";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.titan.angelbounce.day104");
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        string day = Directory.GetParent(Application.dataPath).Parent.FullName;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = Path.Combine(day, "Builds/iOS"),
            target = BuildTarget.iOS, options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("iOS export failed");
        Debug.Log("ANGEL_IOS_EXPORT_SUCCESS. Open Builds/iOS/Unity-iPhone.xcodeproj and choose your signing Team for a physical device.");
    }
    [MenuItem("Titan/Build Web")]
    public static void BuildWeb()
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Install Web Build Support for this exact Unity version in Unity Hub.");
        if (!File.Exists(ScenePath)) CreateScene();
        if (!File.Exists(ScenePath)) throw new BuildFailedException("Main scene not created.");
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            throw new BuildFailedException("Could not switch to Web target. Select Web in Build Profiles and try again.");
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;
        PlayerSettings.defaultWebScreenWidth = 960; PlayerSettings.defaultWebScreenHeight = 540;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        string day = Directory.GetParent(Application.dataPath).Parent.FullName;
        string output = Path.Combine(day, "Builds", "Web");
        Directory.CreateDirectory(output);
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
            scenes = new[] { ScenePath }, locationPathName = output, target = BuildTarget.WebGL, options = BuildOptions.None
        });
        if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException("Web build failed; inspect Console.");
        Debug.Log("Web build created: " + output + ". Serve via HTTP and test on a phone; not published yet.");
    }
}
