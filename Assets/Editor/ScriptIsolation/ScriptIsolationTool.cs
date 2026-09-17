// File: ScriptIsolationTool.cs
// 脚本隔离与迁移工具 - 主 EditorWindow（含 EditorPrefs 缓存）
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ScriptIsolation
{
    public class ScriptIsolationTool : EditorWindow
    {
        private const string PREFS_PREFIX = "ScriptIsolationTool_";

        [MenuItem("Tools/Custom Tools/Script Isolation Tool")]
        public static void ShowWindow()
        {
            var window = GetWindow<ScriptIsolationTool>("脚本隔离工具");
            window.minSize = new Vector2(500, 600);
        }

        private Vector2 scrollPos;

        [SerializeField] private List<MonoScript> sourceScripts = new List<MonoScript>();
        [SerializeField] private string suffix = "_B";
        [SerializeField] private string outputDirectory = "Assets/BranchB/Scripts";

        [SerializeField] private bool processScenes = true;
        [SerializeField] private bool processPrefabs = true;
        [SerializeField] private bool includePrefabVariants = true;
        [SerializeField] private bool recursiveScan = true;
        [SerializeField] private bool dryRun = true;

        [SerializeField] private DefaultAsset sceneFolder;
        [SerializeField] private DefaultAsset prefabFolder;
        [SerializeField] private List<SceneAsset> manualScenes = new List<SceneAsset>();
        [SerializeField] private List<GameObject> manualPrefabs = new List<GameObject>();

        [SerializeField] private bool updateReferencingScripts = true;
        [SerializeField] private DefaultAsset referencingScanFolder;
        [SerializeField] private List<DefaultAsset> referencingExcludeFolders = new List<DefaultAsset>();

        private SerializedObject serializedObject;
        private SerializedProperty sourceScriptsProp;
        private SerializedProperty manualScenesProp;
        private SerializedProperty manualPrefabsProp;
        private SerializedProperty referencingExcludeFoldersProp;

        private bool showAdvanced;
        private bool isProcessing;
        private ScriptIsolationProcessor processor;

        private void OnEnable()
        {
            serializedObject = new SerializedObject(this);
            sourceScriptsProp = serializedObject.FindProperty("sourceScripts");
            manualScenesProp = serializedObject.FindProperty("manualScenes");
            manualPrefabsProp = serializedObject.FindProperty("manualPrefabs");
            referencingExcludeFoldersProp = serializedObject.FindProperty("referencingExcludeFolders");
            processor = new ScriptIsolationProcessor();

            LoadCache();
        }

        private void OnDisable()
        {
            SaveCache();
        }

        private void OnGUI()
        {
            serializedObject.Update();
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            DrawHeader();
            DrawSourceScriptsSection();
            DrawIsolationSettings();
            DrawResourceScopeSection();
            DrawAdvancedOptions();
            DrawActionButtons();

            EditorGUILayout.EndScrollView();
            serializedObject.ApplyModifiedProperties();
        }

        private void DrawHeader()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("脚本隔离与迁移工具", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "此工具用于将差异脚本按隔离规则处理，并将场景/Prefab上挂载脚本的序列化内容迁移到改名后的脚本。\n" +
                "⚠️ 执行前请确保已使用版本控制或备份项目！",
                MessageType.Info);
            EditorGUILayout.Space(5);
        }

        private void DrawSourceScriptsSection()
        {
            EditorGUILayout.LabelField("源脚本列表", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(sourceScriptsProp, new GUIContent("需要隔离的脚本"), true);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("从选中添加", GUILayout.Width(100)))
            {
                AddSelectedScripts();
            }
            if (GUILayout.Button("清空列表", GUILayout.Width(100)))
            {
                sourceScripts.Clear();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }

        private void DrawIsolationSettings()
        {
            EditorGUILayout.LabelField("隔离设置", EditorStyles.boldLabel);

            suffix = EditorGUILayout.TextField("类名后缀", suffix);

            EditorGUILayout.BeginHorizontal();
            outputDirectory = EditorGUILayout.TextField("输出目录", outputDirectory);
            if (GUILayout.Button("选择", GUILayout.Width(50)))
            {
                string path = EditorUtility.OpenFolderPanel("选择输出目录", "Assets", "");
                if (!string.IsNullOrEmpty(path))
                {
                    if (path.StartsWith(Application.dataPath))
                    {
                        outputDirectory = "Assets" + path.Substring(Application.dataPath.Length);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("错误", "请选择 Assets 目录下的文件夹", "确定");
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(10);
        }

        private void DrawResourceScopeSection()
        {
            EditorGUILayout.LabelField("资源范围", EditorStyles.boldLabel);

            processScenes = EditorGUILayout.Toggle("处理场景", processScenes);
            if (processScenes)
            {
                EditorGUI.indentLevel++;
                sceneFolder = (DefaultAsset)EditorGUILayout.ObjectField("场景文件夹", sceneFolder, typeof(DefaultAsset), false);
                EditorGUILayout.PropertyField(manualScenesProp, new GUIContent("手动选择场景"), true);
                EditorGUI.indentLevel--;
            }

            processPrefabs = EditorGUILayout.Toggle("处理 Prefab", processPrefabs);
            if (processPrefabs)
            {
                EditorGUI.indentLevel++;
                prefabFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefab 文件夹", prefabFolder, typeof(DefaultAsset), false);
                EditorGUILayout.PropertyField(manualPrefabsProp, new GUIContent("手动选择 Prefab"), true);
                includePrefabVariants = EditorGUILayout.Toggle("包含 Prefab Variant", includePrefabVariants);
                EditorGUI.indentLevel--;
            }

            recursiveScan = EditorGUILayout.Toggle("递归扫描依赖", recursiveScan);
            EditorGUILayout.Space(10);
        }

        private void DrawAdvancedOptions()
        {
            showAdvanced = EditorGUILayout.Foldout(showAdvanced, "高级选项", true);
            if (showAdvanced)
            {
                EditorGUI.indentLevel++;
                updateReferencingScripts = EditorGUILayout.Toggle(
                    new GUIContent("更新外部引用脚本",
                        "自动扫描引用了目标脚本的外部脚本，直接修改其中的类型引用"),
                    updateReferencingScripts);

                if (updateReferencingScripts)
                {
                    EditorGUI.indentLevel++;
                    referencingScanFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                        new GUIContent("扫描目录", "为空则扫描整个 Assets"),
                        referencingScanFolder, typeof(DefaultAsset), false);
                    EditorGUILayout.PropertyField(referencingExcludeFoldersProp,
                        new GUIContent("排除目录", "不需要更新的目录（如 Editor、Framework）"), true);
                    EditorGUI.indentLevel--;
                }

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space(10);
        }

        private void DrawActionButtons()
        {
            EditorGUI.BeginDisabledGroup(isProcessing);

            EditorGUILayout.BeginHorizontal();

            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("预检查 (Dry Run)", GUILayout.Height(30)))
            {
                dryRun = true;
                StartProcess();
            }

            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("执行隔离与迁移", GUILayout.Height(30)))
            {
                if (EditorUtility.DisplayDialog("确认执行",
                    "此操作将：\n" +
                    "1. 复制并重命名脚本\n" +
                    "2. 替换场景/Prefab中的组件\n" +
                    "3. 迁移序列化数据\n" +
                    "4. 更新外部脚本中的引用\n\n" +
                    "请确保已备份项目！是否继续？",
                    "执行", "取消"))
                {
                    dryRun = false;
                    StartProcess();
                }
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUI.EndDisabledGroup();

            if (isProcessing)
            {
                EditorGUILayout.HelpBox("正在处理中...", MessageType.Info);
            }
        }

        private void AddSelectedScripts()
        {
            foreach (var obj in Selection.objects)
            {
                if (obj is MonoScript script && !sourceScripts.Contains(script))
                {
                    sourceScripts.Add(script);
                }
            }
        }

        private void StartProcess()
        {
            if (sourceScripts.Count == 0 || sourceScripts.All(s => s == null))
            {
                EditorUtility.DisplayDialog("错误", "请至少添加一个源脚本", "确定");
                return;
            }

            if (string.IsNullOrEmpty(suffix))
            {
                EditorUtility.DisplayDialog("错误", "请设置类名后缀", "确定");
                return;
            }

            var excludeFolders = new List<string>();
            foreach (var folder in referencingExcludeFolders)
            {
                if (folder != null)
                {
                    excludeFolders.Add(AssetDatabase.GetAssetPath(folder));
                }
            }

            var config = new ScriptIsolationConfig
            {
                SourceScripts = sourceScripts.Where(s => s != null).ToList(),
                Suffix = suffix,
                OutputDirectory = outputDirectory,
                ProcessScenes = processScenes,
                ProcessPrefabs = processPrefabs,
                IncludePrefabVariants = includePrefabVariants,
                RecursiveScan = recursiveScan,
                DryRun = dryRun,
                SceneFolder = sceneFolder != null ? AssetDatabase.GetAssetPath(sceneFolder) : null,
                PrefabFolder = prefabFolder != null ? AssetDatabase.GetAssetPath(prefabFolder) : null,
                ManualScenes = manualScenes.Where(s => s != null).Select(s => AssetDatabase.GetAssetPath(s)).ToList(),
                ManualPrefabs = manualPrefabs.Where(p => p != null).Select(p => AssetDatabase.GetAssetPath(p)).ToList(),
                UpdateReferencingScripts = updateReferencingScripts,
                ReferencingScanFolder = referencingScanFolder != null ? AssetDatabase.GetAssetPath(referencingScanFolder) : null,
                ReferencingExcludeFolders = excludeFolders
            };

            isProcessing = true;
            try
            {
                processor.Process(config);
            }
            finally
            {
                isProcessing = false;
                SaveCache();
            }
        }

        #region EditorPrefs 缓存

        private void SaveCache()
        {
            EditorPrefs.SetString(PREFS_PREFIX + "suffix", suffix);
            EditorPrefs.SetString(PREFS_PREFIX + "outputDirectory", outputDirectory);
            EditorPrefs.SetBool(PREFS_PREFIX + "processScenes", processScenes);
            EditorPrefs.SetBool(PREFS_PREFIX + "processPrefabs", processPrefabs);
            EditorPrefs.SetBool(PREFS_PREFIX + "includePrefabVariants", includePrefabVariants);
            EditorPrefs.SetBool(PREFS_PREFIX + "recursiveScan", recursiveScan);
            EditorPrefs.SetBool(PREFS_PREFIX + "updateReferencingScripts", updateReferencingScripts);
            EditorPrefs.SetBool(PREFS_PREFIX + "showAdvanced", showAdvanced);

            SaveAssetGuid(PREFS_PREFIX + "sceneFolder", sceneFolder);
            SaveAssetGuid(PREFS_PREFIX + "prefabFolder", prefabFolder);
            SaveAssetGuid(PREFS_PREFIX + "referencingScanFolder", referencingScanFolder);

            SaveAssetGuidList(PREFS_PREFIX + "sourceScripts", sourceScripts);
            SaveAssetGuidList(PREFS_PREFIX + "manualScenes", manualScenes);
            SaveAssetGuidList(PREFS_PREFIX + "manualPrefabs", manualPrefabs);
            SaveAssetGuidList(PREFS_PREFIX + "referencingExcludeFolders", referencingExcludeFolders);
        }

        private void LoadCache()
        {
            if (!EditorPrefs.HasKey(PREFS_PREFIX + "suffix")) return;

            suffix = EditorPrefs.GetString(PREFS_PREFIX + "suffix", "_B");
            outputDirectory = EditorPrefs.GetString(PREFS_PREFIX + "outputDirectory", "Assets/BranchB/Scripts");
            processScenes = EditorPrefs.GetBool(PREFS_PREFIX + "processScenes", true);
            processPrefabs = EditorPrefs.GetBool(PREFS_PREFIX + "processPrefabs", true);
            includePrefabVariants = EditorPrefs.GetBool(PREFS_PREFIX + "includePrefabVariants", true);
            recursiveScan = EditorPrefs.GetBool(PREFS_PREFIX + "recursiveScan", true);
            updateReferencingScripts = EditorPrefs.GetBool(PREFS_PREFIX + "updateReferencingScripts", true);
            showAdvanced = EditorPrefs.GetBool(PREFS_PREFIX + "showAdvanced", false);

            sceneFolder = LoadAssetFromGuid<DefaultAsset>(PREFS_PREFIX + "sceneFolder");
            prefabFolder = LoadAssetFromGuid<DefaultAsset>(PREFS_PREFIX + "prefabFolder");
            referencingScanFolder = LoadAssetFromGuid<DefaultAsset>(PREFS_PREFIX + "referencingScanFolder");

            sourceScripts = LoadAssetGuidList<MonoScript>(PREFS_PREFIX + "sourceScripts");
            manualScenes = LoadAssetGuidList<SceneAsset>(PREFS_PREFIX + "manualScenes");
            manualPrefabs = LoadAssetGuidList<GameObject>(PREFS_PREFIX + "manualPrefabs");
            referencingExcludeFolders = LoadAssetGuidList<DefaultAsset>(PREFS_PREFIX + "referencingExcludeFolders");

            serializedObject = new SerializedObject(this);
            sourceScriptsProp = serializedObject.FindProperty("sourceScripts");
            manualScenesProp = serializedObject.FindProperty("manualScenes");
            manualPrefabsProp = serializedObject.FindProperty("manualPrefabs");
            referencingExcludeFoldersProp = serializedObject.FindProperty("referencingExcludeFolders");
        }

        private void SaveAssetGuid(string key, UnityEngine.Object asset)
        {
            if (asset != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                EditorPrefs.SetString(key, guid);
            }
            else
            {
                EditorPrefs.SetString(key, "");
            }
        }

        private T LoadAssetFromGuid<T>(string key) where T : UnityEngine.Object
        {
            string guid = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;

            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private void SaveAssetGuidList<T>(string key, List<T> assets) where T : UnityEngine.Object
        {
            var guids = new List<string>();
            for (int i = 0; i < assets.Count; i++)
            {
                if (assets[i] != null)
                {
                    string assetPath = AssetDatabase.GetAssetPath(assets[i]);
                    guids.Add(AssetDatabase.AssetPathToGUID(assetPath));
                }
                else
                {
                    guids.Add("");
                }
            }
            EditorPrefs.SetString(key, string.Join("|", guids));
        }

        private List<T> LoadAssetGuidList<T>(string key) where T : UnityEngine.Object
        {
            var result = new List<T>();
            string data = EditorPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(data)) return result;

            string[] guids = data.Split('|');
            for (int i = 0; i < guids.Length; i++)
            {
                if (string.IsNullOrEmpty(guids[i]))
                {
                    result.Add(null);
                    continue;
                }

                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                {
                    result.Add(null);
                    continue;
                }

                result.Add(AssetDatabase.LoadAssetAtPath<T>(path));
            }

            return result;
        }

        #endregion
    }
}
