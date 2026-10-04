using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SealGugu.Editor
{
    public static class ProjectBuild
    {
        public const string Version = "15.2.0";
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [Serializable]
        private sealed class ParityReport
        {
            public string version = "V15.2.0", unityVersion, sourceCommit, verifiedUtc, summary;
            public bool success;
            public int scenarios, checkpoints, assertions;
        }

        [MenuItem("Tools/海豹呼呼/驗證 HTML V13 邏輯一致性")]
        public static void RunParity()
        {
            var chartAsset = Resources.Load<TextAsset>("Data/chart");
            if (!chartAsset) throw new FileNotFoundException("Missing Resources/Data/chart.json");
            string goldenPath = Path.GetFullPath("Tools/core-golden.json");
            if (!File.Exists(goldenPath)) throw new FileNotFoundException("Generate the independent JavaScript golden traces before building.", goldenPath);
            var charts = JsonUtility.FromJson<ChartDocument>(chartAsset.text);
            var golden = JsonUtility.FromJson<GoldenSuite>(File.ReadAllText(goldenPath));
            var report = new ParityReport {
                unityVersion = Application.unityVersion,
                sourceCommit = golden.sourceCommit,
                verifiedUtc = DateTime.UtcNow.ToString("O"),
                scenarios = golden.scenarios.Length
            };
            foreach (var scenario in golden.scenarios) report.checkpoints += scenario.steps.Length;
            Directory.CreateDirectory("Validation");
            try
            {
                report.summary = CoreParity.Run(charts, golden);
                Match assertionCount = Regex.Match(report.summary, @"(\d+) assertions");
                if (assertionCount.Success) report.assertions = int.Parse(assertionCount.Groups[1].Value);
                report.success = true;
            }
            catch (Exception e)
            {
                report.summary = e.ToString();
                File.WriteAllText("Validation/core-parity.json", JsonUtility.ToJson(report, true));
                throw;
            }
            File.WriteAllText("Validation/core-parity.json", JsonUtility.ToJson(report, true));
            Debug.Log("GUGU_PARITY_SUCCESS " + report.summary);
        }

        [MenuItem("Tools/海豹呼呼/準備專案與主場景")]
        public static void Initialize()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Resources");
            PlayerSettings.companyName = "第二組";
            PlayerSettings.productName = "海豹呼呼";
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.rintrint.sealgugu");
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.allowFullscreenSwitch = false; // GuguDesktop owns Alt+Enter; avoid a second native toggle.
            PlayerSettings.runInBackground = false;
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var inputHandler = settings.FindProperty("activeInputHandler");
            if (inputHandler != null)
            {
                inputHandler.intValue = 2;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
            QualitySettings.vSyncCount = 1;
            QualitySettings.antiAliasing = 0;
            AudioConfiguration audio = AudioSettings.GetConfiguration();
            audio.dspBufferSize = 256;
            AudioSettings.Reset(audio);

            if (AssetDatabase.LoadAssetAtPath<GameCredits>("Assets/Resources/GameCredits.asset") == null)
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<GameCredits>(), "Assets/Resources/GameCredits.asset");

            // Keep later hand edits to the scene and Inspector credits when rebuilding.
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.714f, .898f, .98f);
                camera.orthographic = true;
                camera.transform.position = new Vector3(0, 0, -10);
                camera.gameObject.AddComponent<AudioListener>();
                new GameObject("海豹呼呼 · V15.2.0").AddComponent<GuguGame>();
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            else EditorSceneManager.OpenScene(ScenePath);
            var game = UnityEngine.Object.FindAnyObjectByType<GuguGame>();
            if(game&&game.gameObject.name!="海豹呼呼 · "+GuguGame.Version){
                game.gameObject.name="海豹呼呼 · "+GuguGame.Version;
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
                EditorSceneManager.SaveScene(game.gameObject.scene);
            }
            if (game && !game.credits)
            {
                game.credits = AssetDatabase.LoadAssetAtPath<GameCredits>("Assets/Resources/GameCredits.asset");
                EditorUtility.SetDirty(game);
                EditorSceneManager.MarkSceneDirty(game.gameObject.scene);
                EditorSceneManager.SaveScene(game.gameObject.scene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("GUGU_PROJECT_READY V15.2.0; editable credits: Assets/Resources/GameCredits.asset");
        }

        [MenuItem("Tools/海豹呼呼/打包 Windows V15.2.0")]
        public static void BuildWindows() { Build(null,false); }

        [MenuItem("Tools/海豹呼呼/建置 URP 本機測試包（不發布）")]
        public static void BuildUrpPreview() { HuhuMigration.Upgrade(); HuhuMigration.Verify(); Build("Builds/URP-Preview",true); }

        [MenuItem("Tools/海豹呼呼/驗證並建置上岸結局測試版 %#F8")]
        public static void BuildShorePreview() {
            HuhuMigration.Upgrade();
            var chart=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text);
            string result=ShoreSequenceChecks.Run(chart);
            File.WriteAllText("Validation/shore-sequence.txt",result);
            Debug.Log(result);
            Build("Builds/Shore-Preview",true);
        }
        [MenuItem("Tools/海豹呼呼/驗證並打包 Windows V15.2.0")]
        public static void BuildReleaseCandidate() {
            HuhuMigration.Upgrade();
            var chart=JsonUtility.FromJson<ChartDocument>(Resources.Load<TextAsset>("Data/chart").text);
            File.WriteAllText("Validation/shore-sequence.txt",ShoreSequenceChecks.Run(chart));
            Build("Builds/QA-V"+Version,true);
            Build("Builds/V"+Version,false);
        }
        static void Build(string outputOverride,bool qa)
        {
            Initialize();
            HuhuMigration.Verify();
            RunParity();
            OxygenBalanceParity.Verify();
            DifficultyBalanceParity.Verify();
            string[] args=Environment.GetCommandLineArgs();int outputFlag=Array.IndexOf(args,"-guguOutput");
            var directory = Path.GetFullPath(outputOverride ?? (outputFlag>=0&&outputFlag+1<args.Length?args[outputFlag+1]:"Builds/V"+Version));
            Directory.CreateDirectory(directory);
            var buildOptions = BuildOptions.CompressWithLz4HC;
            if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-guguDevelopment"))
                buildOptions |= BuildOptions.Development;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = Path.Combine(directory, "SealBreath.exe"),
                target = BuildTarget.StandaloneWindows64,
                extraScriptingDefines = qa || (buildOptions & BuildOptions.Development) != 0 || Array.IndexOf(args,"-guguQA")>=0 ? new[] { "GUGU_QA" } : Array.Empty<string>(),
                options = buildOptions
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Windows build failed: " + report.summary.result + "; errors=" + report.summary.totalErrors);
            File.WriteAllText(Path.Combine(directory, "README.txt"),
                "海豹呼呼 V15.2.0\r\n\r\n開啟 SealBreath.exe。請保留同資料夾的 SealBreath_Data、UnityPlayer.dll 與其他檔案。\r\n" +
                "上排 D / F / ↑；下排 J / K / ↓；開場吸氣 Space；途中岸上換氣連打 Space 或上下排按鍵；Esc 暫停；Alt + Enter 切換視窗／全螢幕；F1 顯示／隱藏 FPS；F2 場景與結局測試選單。\r\n" +
                "預設判定 ±150ms，可在選曲及暫停設定調整魚速、延遲與判定。\r\n");
            var notices = Path.Combine(directory, "ThirdPartyNotices");
            Directory.CreateDirectory(notices);
            foreach (var license in Directory.GetFiles("Assets/Resources/Fonts", "*.txt"))
                File.Copy(license, Path.Combine(notices, Path.GetFileName(license)), true);
            Debug.Log("GUGU_BUILD_SUCCESS " + report.summary.totalSize + " bytes; " + directory);
        }
    }
}
