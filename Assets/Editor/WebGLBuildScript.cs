using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MunCraft.EditorTools
{
    /// <summary>
    /// One-click WebGL build with sensible defaults.
    /// Menu: MunCraft → Build WebGL (and ...Build for GitHub Pages).
    ///
    /// Workflow:
    ///   1. MunCraft → Configure WebGL Settings  (one-time; switches the
    ///      active platform to WebGL — can take a minute the first time
    ///      while Unity reimports assets for the new platform)
    ///   2. MunCraft → Build WebGL                (or Build for GitHub Pages)
    /// </summary>
    public static class WebGLBuildScript
    {
        const string DefaultOutput = "Build/WebGL";
        const string PagesOutput = "docs";
        const string MainScene = "Assets/Scenes/SampleScene.unity";

        [MenuItem("MunCraft/Configure WebGL Settings")]
        public static void ConfigureWebGL()
        {
            // Compress with Gzip — best universal browser support.
            // Decompression fallback so the build also works without server-side
            // Content-Encoding headers (e.g. file://, simple static hosts).
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;

            // Cheaper exception handling — only the ones we explicitly throw.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

            // Don't pause when the tab loses focus.
            PlayerSettings.runInBackground = true;

            // Make sure shaders we look up at runtime via Shader.Find() are
            // actually included in the build. Without this they get stripped
            // and Shader.Find returns null in the player.
            EnsureShaderAlwaysIncluded("MunCraft/FlatBlock");
            EnsureShaderAlwaysIncluded("MunCraft/Sky");

            UnityEngine.Debug.Log("[WebGLBuildScript] Player settings configured.");

            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                UnityEngine.Debug.Log("[WebGLBuildScript] Switching active build target to WebGL — " +
                    "this can take a minute the first time. Watch for the spinner in the bottom-right.");
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            }
            else
            {
                UnityEngine.Debug.Log("[WebGLBuildScript] Already on WebGL.");
            }
        }

        /// <summary>
        /// Adds a shader to ProjectSettings → Graphics → Always Included Shaders
        /// so it survives the build-time stripping pass.
        /// </summary>
        static void EnsureShaderAlwaysIncluded(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                UnityEngine.Debug.LogWarning(
                    $"[WebGLBuildScript] Shader '{shaderName}' not found in editor; " +
                    "skipping Always Included setup. Did the shader file move?");
                return;
            }

            var graphicsSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                "ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(graphicsSettings);
            var arr = so.FindProperty("m_AlwaysIncludedShaders");

            for (int i = 0; i < arr.arraySize; i++)
            {
                var element = arr.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == shader)
                {
                    UnityEngine.Debug.Log($"[WebGLBuildScript] '{shaderName}' already in Always Included Shaders.");
                    return;
                }
            }

            arr.arraySize++;
            arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = shader;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            UnityEngine.Debug.Log($"[WebGLBuildScript] Added '{shaderName}' to Always Included Shaders.");
        }

        [MenuItem("MunCraft/Build WebGL")]
        public static void BuildWebGL() => DoBuild(DefaultOutput, isPages: false);

        [MenuItem("MunCraft/Build for GitHub Pages")]
        public static void BuildForPages() => DoBuild(PagesOutput, isPages: true);

        static void DoBuild(string outputPath, bool isPages)
        {
            // Hard guard: if the active target isn't WebGL, refuse to build.
            // SwitchActiveBuildTarget is "synchronous" but the platform-switch
            // asset reimport can leave the editor in an inconsistent state.
            // Building anyway produces a player for the wrong target (e.g. a
            // Windows .exe sitting where a WebGL build should be).
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            {
                UnityEngine.Debug.LogError(
                    "[WebGLBuildScript] Active build target is " +
                    EditorUserBuildSettings.activeBuildTarget +
                    ", not WebGL. Run MunCraft → Configure WebGL Settings first, " +
                    "wait for the platform switch to finish (watch the spinner in the " +
                    "bottom-right of the editor), then try the build again.");
                return;
            }

            if (Directory.Exists(outputPath))
                Directory.Delete(outputPath, recursive: true);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = outputPath,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            UnityEngine.Debug.Log($"[WebGLBuildScript] Building WebGL → {outputPath}");
            BuildReport report = BuildPipeline.BuildPlayer(options);

            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
            {
                UnityEngine.Debug.Log($"[WebGLBuildScript] Build succeeded — {s.totalSize / 1024 / 1024} MB " +
                          $"in {s.totalTime.TotalSeconds:F1}s. Output: {outputPath}");

                if (isPages)
                {
                    File.WriteAllText(Path.Combine(outputPath, ".nojekyll"), "");
                    UnityEngine.Debug.Log($"[WebGLBuildScript] Wrote .nojekyll into {outputPath}/");
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"[WebGLBuildScript] Build {s.result}: {s.totalErrors} errors");
            }
        }
    }
}
