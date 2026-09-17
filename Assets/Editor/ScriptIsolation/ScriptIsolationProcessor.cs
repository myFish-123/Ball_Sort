// File: ScriptIsolationProcessor.cs
// 脚本隔离处理器 - 核心处理逻辑
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScriptIsolation
{
    public class ScriptIsolationProcessor
    {
        private ScriptAnalyzer analyzer = new ScriptAnalyzer();
        private MigrationReport report = new MigrationReport();
        private Dictionary<Type, ScriptMappingInfo> typeMappings = new Dictionary<Type, ScriptMappingInfo>();
        private HashSet<string> processedPrefabPaths = new HashSet<string>();

        /// <summary>
        /// 被直接修改引用的外部脚本路径列表（用于报告）
        /// </summary>
        private List<string> updatedReferencingScripts = new List<string>();

        /// <summary>
        /// 完整类名映射表（含主类 + 附属类型 struct/enum/interface），供外部引用替换使用
        /// </summary>
        private Dictionary<string, string> fullClassMappings = new Dictionary<string, string>();

        /// <summary>
        /// 缓存 Scene 中 Prefab 实例上旧组件的完整序列化数据（含 override 值）
        /// key = "scenePath|gameObjectPath|originalTypeName"
        /// </summary>
        private Dictionary<string, InstanceOverrideCache> overrideCaches
            = new Dictionary<string, InstanceOverrideCache>();

        public void Process(ScriptIsolationConfig config)
        {
            report = new MigrationReport();
            typeMappings.Clear();
            processedPrefabPaths.Clear();
            overrideCaches.Clear();
            updatedReferencingScripts.Clear();
            fullClassMappings.Clear();

            try
            {
                EditorUtility.DisplayProgressBar("脚本隔离工具", "分析脚本...", 0.05f);
                if (!AnalyzeScripts(config))
                {
                    EditorUtility.ClearProgressBar();
                    return;
                }

                if (!config.DryRun)
                {
                    EditorUtility.DisplayProgressBar("脚本隔离工具", "创建隔离脚本...", 0.1f);
                    if (!CreateIsolatedScripts(config))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    // ★ 直接修改外部引用脚本中的类型引用（不生成副本）
                    if (config.UpdateReferencingScripts)
                    {
                        EditorUtility.DisplayProgressBar("脚本隔离工具", "更新外部引用脚本...", 0.12f);
                        UpdateReferencingScripts(config);
                    }

                    EditorUtility.DisplayProgressBar("脚本隔离工具", "等待编译完成...", 0.15f);
                    AssetDatabase.Refresh();

                    if (EditorApplication.isCompiling)
                    {
                        EditorUtility.DisplayDialog("提示",
                            "脚本已创建，Unity 正在编译。\n编译完成后请再次运行工具完成迁移。",
                            "确定");
                        EditorUtility.ClearProgressBar();
                        return;
                    }

                    if (!ResolveNewTypes(config))
                    {
                        EditorUtility.ClearProgressBar();
                        return;
                    }
                }

                EditorUtility.DisplayProgressBar("脚本隔离工具", "扫描资源...", 0.2f);
                var scenes = CollectScenes(config);
                var prefabs = CollectPrefabs(config);

                EditorUtility.DisplayProgressBar("脚本隔离工具", "检测引用...", 0.25f);
                DetectReferences(config);

                // ★ 核心流程（三阶段）：
                // 阶段1: 预收集 - 打开每个 Scene，收集 Prefab 实例上旧组件的完整序列化数据
                if (config.ProcessScenes && config.ProcessPrefabs && scenes.Count > 0 && prefabs.Count > 0)
                {
                    EditorUtility.DisplayProgressBar("脚本隔离工具", "收集 Prefab 实例 override 数据...", 0.3f);
                    PreCollectInstanceOverrides(config, scenes);
                }

                // 阶段2: 处理 Prefab 资产
                if (config.ProcessPrefabs && prefabs.Count > 0)
                {
                    ProcessPrefabs(config, prefabs);
                }

                // 阶段3: 处理 Scene（对 Prefab 实例回写 override 数据）
                if (config.ProcessScenes && scenes.Count > 0)
                {
                    ProcessScenes(config, scenes);
                }

                EditorUtility.DisplayProgressBar("脚本隔离工具", "生成报告...", 0.95f);
                report.SetUpdatedReferencingScripts(updatedReferencingScripts);
                report.Generate(config.DryRun);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ScriptIsolation] 处理过程中发生错误: {e.Message}\n{e.StackTrace}");
                report.AddError($"处理过程中发生错误: {e.Message}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        #region 脚本分析与创建

        private bool AnalyzeScripts(ScriptIsolationConfig config)
        {
            bool hasBlockingIssues = false;

            foreach (var script in config.SourceScripts)
            {
                var result = analyzer.Analyze(script);
                report.AddAnalysisResult(result);

                if (!result.IsValid)
                {
                    hasBlockingIssues = true;
                    Debug.LogError($"[ScriptIsolation] 脚本分析失败: {result.ScriptPath}\n" +
                                   string.Join("\n", result.Issues));
                }
                else if (result.Issues.Count > 0)
                {
                    Debug.LogWarning($"[ScriptIsolation] 脚本存在潜在问题: {result.ScriptPath}\n" +
                                     string.Join("\n", result.Issues));
                }

                if (result.IsValid)
                {
                    var mapping = new ScriptMappingInfo
                    {
                        OriginalScript = script,
                        OriginalClassName = result.ClassName,
                        NewClassName = result.ClassName + config.Suffix,
                        OriginalPath = result.ScriptPath,
                        NewPath = Path.Combine(config.OutputDirectory,
                            Path.GetFileNameWithoutExtension(result.ScriptPath) + config.Suffix + ".cs"),
                        OriginalType = script.GetClass()
                    };

                    if (mapping.OriginalType != null)
                    {
                        typeMappings[mapping.OriginalType] = mapping;
                    }

                    report.AddMapping(mapping);
                }
            }

            if (hasBlockingIssues)
            {
                EditorUtility.DisplayDialog("分析失败",
                    "部分脚本存在阻塞性问题，请查看 Console 日志。", "确定");
                return false;
            }

            return true;
        }

        private bool CreateIsolatedScripts(ScriptIsolationConfig config)
        {
            if (!Directory.Exists(config.OutputDirectory))
            {
                Directory.CreateDirectory(config.OutputDirectory);
            }

            // 构建目标脚本的类名映射表（原始类名 → 新类名）
            var allClassMappings = new Dictionary<string, string>();
            foreach (var mapping in typeMappings.Values)
            {
                allClassMappings[mapping.OriginalClassName] = mapping.NewClassName;
            }

            // 收集所有目标脚本中的附属类型（struct/enum/interface），加入映射表
            foreach (var mapping in typeMappings.Values)
            {
                string content = File.ReadAllText(mapping.OriginalPath);
                var auxMappings = analyzer.GetAuxTypeMappings(content, mapping.OriginalClassName, config.Suffix);
                foreach (var kvp in auxMappings)
                {
                    if (!allClassMappings.ContainsKey(kvp.Key))
                    {
                        allClassMappings[kvp.Key] = kvp.Value;
                    }
                }
            }

            // 保存完整映射表供 UpdateReferencingScripts 使用
            fullClassMappings = allClassMappings;

            // 创建目标脚本的隔离版本
            foreach (var mapping in typeMappings.Values)
            {
                try
                {
                    string content = File.ReadAllText(mapping.OriginalPath);
                    string newContent = analyzer.RenameAllReferencesInContent(
                        content, mapping.OriginalClassName, allClassMappings, config.Suffix);

                    string newPath = mapping.NewPath.Replace("\\", "/");
                    File.WriteAllText(newPath, newContent);

                    Debug.Log($"[ScriptIsolation] 创建隔离脚本: {newPath}");
                    report.AddCreatedScript(newPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ScriptIsolation] 创建脚本失败: {mapping.NewPath}\n{e.Message}");
                    report.AddError($"创建脚本失败: {mapping.NewPath} - {e.Message}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 直接修改外部引用脚本的源文件，将其中对目标脚本的类型引用替换为隔离后的新类名。
        /// 不生成副本，C 还是 C，只是 C 中的 A 引用变成 A_B。
        /// </summary>
        private void UpdateReferencingScripts(ScriptIsolationConfig config)
        {
            var targetPaths = new HashSet<string>();
            foreach (var mapping in typeMappings.Values)
            {
                targetPaths.Add(mapping.OriginalPath);
                targetPaths.Add(mapping.NewPath);
            }

            // 使用完整映射表（含主类 + 附属类型 struct/enum/interface）
            var classMappings = fullClassMappings;

            string[] searchFolders;
            if (!string.IsNullOrEmpty(config.ReferencingScanFolder))
            {
                searchFolders = new[] { config.ReferencingScanFolder };
            }
            else
            {
                searchFolders = new[] { "Assets" };
            }

            var excludeFolders = new HashSet<string>();
            if (config.ReferencingExcludeFolders != null)
            {
                foreach (var folder in config.ReferencingExcludeFolders)
                {
                    if (!string.IsNullOrEmpty(folder))
                    {
                        excludeFolders.Add(folder.Replace("\\", "/"));
                    }
                }
            }
            excludeFolders.Add("Assets/Editor");
            excludeFolders.Add(config.OutputDirectory.Replace("\\", "/"));

            var allScriptGuids = AssetDatabase.FindAssets("t:MonoScript", searchFolders);
            int updatedCount = 0;

            foreach (var guid in allScriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (targetPaths.Contains(path))
                    continue;

                string normalizedPath = path.Replace("\\", "/");
                bool excluded = false;
                foreach (var excludeFolder in excludeFolders)
                {
                    if (normalizedPath.StartsWith(excludeFolder + "/") || normalizedPath == excludeFolder)
                    {
                        excluded = true;
                        break;
                    }
                }
                if (excluded) continue;

                string content = File.ReadAllText(path);
                string originalContent = content;

                foreach (var kvp in classMappings)
                {
                    string pattern = $@"\b{Regex.Escape(kvp.Key)}\b";
                    content = Regex.Replace(content, pattern, kvp.Value);
                }

                if (content != originalContent)
                {
                    File.WriteAllText(path, content);
                    updatedReferencingScripts.Add(path);
                    updatedCount++;
                    Debug.Log($"[ScriptIsolation] 已更新外部脚本引用: {path}");
                }
            }

            Debug.Log($"[ScriptIsolation] 共更新 {updatedCount} 个外部脚本的引用");
        }

        private bool ResolveNewTypes(ScriptIsolationConfig config)
        {
            foreach (var mapping in typeMappings.Values)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                Type newType = null;

                foreach (var assembly in assemblies)
                {
                    newType = assembly.GetType(mapping.NewClassName);
                    if (newType != null) break;

                    if (mapping.OriginalType != null && !string.IsNullOrEmpty(mapping.OriginalType.Namespace))
                    {
                        newType = assembly.GetType($"{mapping.OriginalType.Namespace}.{mapping.NewClassName}");
                        if (newType != null) break;
                    }
                }

                if (newType == null)
                {
                    Debug.LogError($"[ScriptIsolation] 无法找到新类型: {mapping.NewClassName}");
                    report.AddError($"无法找到新类型: {mapping.NewClassName}，请确保编译已完成");
                    return false;
                }

                mapping.NewType = newType;
            }

            return true;
        }

        #endregion

        #region 资源收集

        private List<string> CollectScenes(ScriptIsolationConfig config)
        {
            var scenes = new List<string>();

            if (!string.IsNullOrEmpty(config.SceneFolder))
            {
                var guids = AssetDatabase.FindAssets("t:Scene", new[] { config.SceneFolder });
                foreach (var guid in guids)
                {
                    scenes.Add(AssetDatabase.GUIDToAssetPath(guid));
                }
            }

            foreach (var scenePath in config.ManualScenes)
            {
                if (!scenes.Contains(scenePath))
                {
                    scenes.Add(scenePath);
                }
            }

            report.SetSceneCount(scenes.Count);
            return scenes;
        }

        private List<string> CollectPrefabs(ScriptIsolationConfig config)
        {
            var prefabs = new List<string>();

            if (!string.IsNullOrEmpty(config.PrefabFolder))
            {
                var guids = AssetDatabase.FindAssets("t:Prefab", new[] { config.PrefabFolder });
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    if (!config.IncludePrefabVariants)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                        if (PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.Variant)
                        {
                            continue;
                        }
                    }

                    prefabs.Add(path);
                }
            }

            foreach (var prefabPath in config.ManualPrefabs)
            {
                if (!prefabs.Contains(prefabPath))
                {
                    prefabs.Add(prefabPath);
                }
            }

            report.SetPrefabCount(prefabs.Count);
            return prefabs;
        }

        private void DetectReferences(ScriptIsolationConfig config)
        {
            // 已更新引用的外部脚本不再发出警告
            var handledPaths = new HashSet<string>();
            foreach (var mapping in typeMappings.Values)
            {
                handledPaths.Add(mapping.OriginalPath);
                handledPaths.Add(mapping.NewPath);
            }
            foreach (var path in updatedReferencingScripts)
            {
                handledPaths.Add(path);
            }

            var allScripts = AssetDatabase.FindAssets("t:MonoScript");

            foreach (var guid in allScripts)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (handledPaths.Contains(path))
                    continue;

                string content = File.ReadAllText(path);

                foreach (var kvp in fullClassMappings)
                {
                    string originalName = kvp.Key;
                    if (content.Contains($"public {originalName} ") ||
                        content.Contains($"private {originalName} ") ||
                        content.Contains($"protected {originalName} ") ||
                        content.Contains($"[SerializeField] {originalName} ") ||
                        content.Contains($"<{originalName}>"))
                    {
                        var warning = new ReferenceWarning
                        {
                            ScriptPath = path,
                            ReferencedType = originalName,
                            Message = $"脚本 {path} 中存在对 {originalName} 的强类型引用，" +
                                      "未被自动更新（可能在排除目录中）"
                        };
                        report.AddReferenceWarning(warning);
                    }
                }
            }
        }

        #endregion

        #region 阶段1: 预收集 Prefab 实例 override 数据

        /// <summary>
        /// 在 Prefab 资产处理之前，打开每个 Scene 收集 Prefab 实例上旧组件的完整序列化数据。
        /// 这些数据包含了实例上的 property override 值。
        /// </summary>
        private void PreCollectInstanceOverrides(ScriptIsolationConfig config, List<string> scenes)
        {
            foreach (var scenePath in scenes)
            {
                try
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var rootObjects = scene.GetRootGameObjects();

                    foreach (var root in rootObjects)
                    {
                        CollectOverridesRecursive(root, scenePath);
                    }

                    Debug.Log($"[ScriptIsolation] 已收集场景 override 数据: {scenePath}, " +
                              $"缓存条目数: {overrideCaches.Count}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ScriptIsolation] 收集 override 数据失败: {scenePath}\n{e.Message}");
                    report.AddError($"收集 override 数据失败: {scenePath} - {e.Message}");
                }
            }
        }

        private void CollectOverridesRecursive(GameObject go, string scenePath)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                foreach (Transform child in go.transform)
                {
                    CollectOverridesRecursive(child.gameObject, scenePath);
                }
                return;
            }

            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var componentType = component.GetType();
                if (!typeMappings.ContainsKey(componentType)) continue;

                string objectPath = GetGameObjectPath(go);
                string cacheKey = $"{scenePath}|{objectPath}|{componentType.Name}";

                var cache = new InstanceOverrideCache
                {
                    Key = cacheKey,
                    ScenePath = scenePath,
                    GameObjectPath = objectPath,
                    OriginalType = componentType
                };

                var so = new SerializedObject(component);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.name == "m_Script") continue;
                    if (!prop.hasChildren || prop.propertyType == SerializedPropertyType.String
                                          || prop.propertyType == SerializedPropertyType.ObjectReference)
                    {
                        var value = CapturePropertyValue(prop);
                        if (value != null)
                        {
                            cache.OverrideValues[prop.propertyPath] = value;
                        }
                    }
                }

                overrideCaches[cacheKey] = cache;
            }

            foreach (Transform child in go.transform)
            {
                CollectOverridesRecursive(child.gameObject, scenePath);
            }
        }

        /// <summary>
        /// 将 SerializedProperty 的当前值快照到 SerializedPropertyValue
        /// </summary>
        private SerializedPropertyValue CapturePropertyValue(SerializedProperty prop)
        {
            var value = new SerializedPropertyValue { PropertyType = prop.propertyType };

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    value.IntValue = prop.longValue;
                    break;
                case SerializedPropertyType.Boolean:
                    value.BoolValue = prop.boolValue;
                    break;
                case SerializedPropertyType.Float:
                    value.FloatValue = prop.floatValue;
                    break;
                case SerializedPropertyType.String:
                    value.StringValue = prop.stringValue;
                    break;
                case SerializedPropertyType.Color:
                    value.ColorValue = prop.colorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    value.ObjectReferenceValue = prop.objectReferenceValue;
                    value.ObjectReferenceInstanceIDValue = prop.objectReferenceInstanceIDValue;
                    break;
                case SerializedPropertyType.Enum:
                    value.EnumValueIndex = prop.enumValueIndex;
                    break;
                case SerializedPropertyType.Vector2:
                    value.Vector2Value = prop.vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    value.Vector3Value = prop.vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    value.Vector4Value = prop.vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    value.RectValue = prop.rectValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    value.AnimationCurveValue = prop.animationCurveValue;
                    break;
                case SerializedPropertyType.Bounds:
                    value.BoundsValue = prop.boundsValue;
                    break;
                case SerializedPropertyType.Quaternion:
                    value.QuaternionValue = prop.quaternionValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    value.Vector2IntValue = prop.vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    value.Vector3IntValue = prop.vector3IntValue;
                    break;
                case SerializedPropertyType.RectInt:
                    value.RectIntValue = prop.rectIntValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    value.BoundsIntValue = prop.boundsIntValue;
                    break;
                case SerializedPropertyType.ArraySize:
                    value.IntValue = prop.intValue;
                    break;
                default:
                    return null;
            }

            return value;
        }

        #endregion

        #region 阶段2: Prefab 资产处理

        private void ProcessPrefabs(ScriptIsolationConfig config, List<string> prefabs)
        {
            float progress = 0.4f;
            float step = 0.2f / Mathf.Max(prefabs.Count, 1);

            foreach (var prefabPath in prefabs)
            {
                EditorUtility.DisplayProgressBar("脚本隔离工具", $"处理 Prefab: {prefabPath}", progress);
                progress += step;

                try
                {
                    ProcessPrefab(config, prefabPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ScriptIsolation] 处理 Prefab 失败: {prefabPath}\n{e.Message}");
                    report.AddError($"处理 Prefab 失败: {prefabPath} - {e.Message}");
                }
            }
        }

        private void ProcessPrefab(ScriptIsolationConfig config, string prefabPath)
        {
            var prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            bool modified = false;

            try
            {
                modified = ProcessGameObjectForPrefabAsset(config, prefabContents, prefabPath);

                if (modified && !config.DryRun)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
                    processedPrefabPaths.Add(prefabPath);
                    Debug.Log($"[ScriptIsolation] Prefab 已保存: {prefabPath}");
                }
                else if (modified && config.DryRun)
                {
                    processedPrefabPaths.Add(prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabContents);
            }
        }

        private bool ProcessGameObjectForPrefabAsset(ScriptIsolationConfig config, GameObject root, string assetPath)
        {
            var replacementMap = new Dictionary<Component, Component>();
            bool modified = CollectMigrationsRecursive(config, root, assetPath, replacementMap);

            if (replacementMap.Count > 0)
            {
                FixObjectReferences(root, replacementMap);
                DestroyOldComponents(replacementMap);
            }

            return modified;
        }

        /// <summary>
        /// 递归遍历 GameObject 树，执行组件迁移并收集 old→new 映射（不删除旧组件）
        /// </summary>
        private bool CollectMigrationsRecursive(ScriptIsolationConfig config, GameObject go,
            string assetPath, Dictionary<Component, Component> replacementMap)
        {
            bool modified = false;
            string objectPath = GetGameObjectPath(go);

            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var componentType = component.GetType();
                if (typeMappings.TryGetValue(componentType, out var mapping))
                {
                    var result = MigrateComponent(config, go, component, mapping,
                        assetPath, objectPath, replacementMap);
                    report.AddMigrationResult(result);
                    modified |= result.Success;
                }
            }

            foreach (Transform child in go.transform)
            {
                modified |= CollectMigrationsRecursive(config, child.gameObject, assetPath, replacementMap);
            }

            return modified;
        }

        #endregion

        #region 阶段3: Scene 处理（含 override 回写）

        private void ProcessScenes(ScriptIsolationConfig config, List<string> scenes)
        {
            float progress = 0.65f;
            float step = 0.25f / Mathf.Max(scenes.Count, 1);

            foreach (var scenePath in scenes)
            {
                EditorUtility.DisplayProgressBar("脚本隔离工具", $"处理场景: {scenePath}", progress);
                progress += step;

                try
                {
                    ProcessScene(config, scenePath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ScriptIsolation] 处理场景失败: {scenePath}\n{e.Message}");
                    report.AddError($"处理场景失败: {scenePath} - {e.Message}");
                }
            }
        }

        private void ProcessScene(ScriptIsolationConfig config, string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool modified = false;

            var sceneReplacementMap = new Dictionary<Component, Component>();
            var rootObjects = scene.GetRootGameObjects();
            foreach (var root in rootObjects)
            {
                modified |= ProcessGameObjectForScene(config, root, scenePath, sceneReplacementMap);
            }

            // 场景级别统一修复引用：遍历所有根节点
            if (sceneReplacementMap.Count > 0)
            {
                foreach (var root in rootObjects)
                {
                    FixObjectReferences(root, sceneReplacementMap);
                }
                DestroyOldComponents(sceneReplacementMap);
            }

            if (modified && !config.DryRun)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[ScriptIsolation] 场景已保存: {scenePath}");
            }
        }

        private bool ProcessGameObjectForScene(ScriptIsolationConfig config, GameObject go,
            string scenePath, Dictionary<Component, Component> sceneReplacementMap)
        {
            bool modified = false;
            string objectPath = GetGameObjectPath(go);

            bool isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(go);
            string sourcePrefabPath = null;

            if (isPrefabInstance)
            {
                var prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
                if (prefabAsset != null)
                {
                    sourcePrefabPath = AssetDatabase.GetAssetPath(prefabAsset);
                }
            }

            bool prefabAlreadyProcessed = sourcePrefabPath != null
                                          && processedPrefabPaths.Contains(sourcePrefabPath);

            var components = go.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null) continue;

                var componentType = component.GetType();

                // 情况A: Prefab 实例 + Prefab 已处理 → 需要在新组件上回写 override 数据
                if (isPrefabInstance && prefabAlreadyProcessed)
                {
                    foreach (var mapping in typeMappings.Values)
                    {
                        if (mapping.NewType != null && componentType == mapping.NewType)
                        {
                            string cacheKey = $"{scenePath}|{objectPath}|{mapping.OriginalClassName}";
                            if (overrideCaches.TryGetValue(cacheKey, out var cache))
                            {
                                var result = ApplyOverrideToNewComponent(
                                    config, go, component, mapping, cache, scenePath, objectPath);
                                report.AddMigrationResult(result);
                                modified |= result.Success;
                            }
                            break;
                        }
                    }

                    if (typeMappings.TryGetValue(componentType, out var addedMapping))
                    {
                        bool isAddedComponent = PrefabUtility.IsAddedComponentOverride(component);
                        if (isAddedComponent)
                        {
                            var result = MigrateComponent(config, go, component, addedMapping,
                                scenePath, objectPath, sceneReplacementMap);
                            result.Message = "Prefab 实例 Added Component: " + result.Message;
                            report.AddMigrationResult(result);
                            modified |= result.Success;
                        }
                        else
                        {
                            report.AddMigrationResult(new MigrationResult
                            {
                                AssetPath = scenePath,
                                ObjectPath = objectPath,
                                OriginalType = addedMapping.OriginalClassName,
                                NewType = addedMapping.NewClassName,
                                Success = false,
                                Message = "Prefab 实例上仍存在旧类型组件，Prefab 资产处理可能未成功"
                            });
                        }
                    }
                }
                // 情况B: 非 Prefab 实例，或 Prefab 未被处理过 → 正常迁移
                else if (typeMappings.TryGetValue(componentType, out var mapping))
                {
                    var result = MigrateComponent(config, go, component, mapping,
                        scenePath, objectPath, sceneReplacementMap);
                    report.AddMigrationResult(result);
                    modified |= result.Success;
                }
            }

            foreach (Transform child in go.transform)
            {
                modified |= ProcessGameObjectForScene(config, child.gameObject, scenePath, sceneReplacementMap);
            }

            return modified;
        }

        /// <summary>
        /// 将预收集的 override 数据回写到 Prefab 实例上的新组件
        /// </summary>
        private MigrationResult ApplyOverrideToNewComponent(ScriptIsolationConfig config,
            GameObject go, Component newComponent, ScriptMappingInfo mapping,
            InstanceOverrideCache cache, string scenePath, string objectPath)
        {
            var result = new MigrationResult
            {
                AssetPath = scenePath,
                ObjectPath = objectPath,
                OriginalType = mapping.OriginalClassName,
                NewType = mapping.NewClassName
            };

            if (cache.OverrideValues.Count == 0)
            {
                result.Success = true;
                result.Message = config.DryRun
                    ? "[Dry Run] Prefab 实例无 override 数据需要回写"
                    : "Prefab 实例无 override 数据需要回写";
                return result;
            }

            if (config.DryRun)
            {
                result.Success = true;
                result.Message = $"[Dry Run] 将回写 {cache.OverrideValues.Count} 个属性的 override 数据";
                return result;
            }

            try
            {
                Undo.RegisterCompleteObjectUndo(go, "Script Migration - Override Writeback");

                var so = new SerializedObject(newComponent);
                int appliedCount = 0;

                // 第一轮：优先写入 ArraySize，立即 Apply 使数组扩容，后续元素才能找到位置
                bool hasArraySize = false;
                foreach (var kvp in cache.OverrideValues)
                {
                    if (kvp.Value.PropertyType != SerializedPropertyType.ArraySize) continue;

                    var prop = so.FindProperty(kvp.Key);
                    if (prop == null) continue;

                    ApplyPropertyValue(prop, kvp.Value);
                    appliedCount++;
                    hasArraySize = true;
                }
                if (hasArraySize)
                {
                    so.ApplyModifiedProperties();
                    so.Update();
                }

                // 第二轮：写入其余属性（数组元素、普通字段等）
                foreach (var kvp in cache.OverrideValues)
                {
                    if (kvp.Value.PropertyType == SerializedPropertyType.ArraySize) continue;

                    string propertyPath = kvp.Key;
                    var cachedValue = kvp.Value;

                    var prop = so.FindProperty(propertyPath);
                    if (prop == null)
                    {
                        result.FieldWarnings.Add($"属性 '{propertyPath}' 在新组件中不存在，跳过");
                        continue;
                    }

                    if (prop.propertyType != cachedValue.PropertyType)
                    {
                        result.FieldWarnings.Add(
                            $"属性 '{propertyPath}' 类型不匹配: " +
                            $"缓存={cachedValue.PropertyType}, 新组件={prop.propertyType}，跳过");
                        continue;
                    }

                    ApplyPropertyValue(prop, cachedValue);
                    appliedCount++;
                }

                so.ApplyModifiedProperties();

                result.Success = true;
                result.Message = $"Override 回写完成: {appliedCount}/{cache.OverrideValues.Count} 个属性" +
                    (result.FieldWarnings.Count > 0 ? $"，{result.FieldWarnings.Count} 个警告" : "");
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Message = $"Override 回写失败: {e.Message}";
            }

            return result;
        }

        /// <summary>
        /// 将缓存的值写入 SerializedProperty
        /// </summary>
        private void ApplyPropertyValue(SerializedProperty prop, SerializedPropertyValue value)
        {
            switch (value.PropertyType)
            {
                case SerializedPropertyType.Integer:
                    prop.longValue = value.IntValue;
                    break;
                case SerializedPropertyType.Boolean:
                    prop.boolValue = value.BoolValue;
                    break;
                case SerializedPropertyType.Float:
                    prop.floatValue = value.FloatValue;
                    break;
                case SerializedPropertyType.String:
                    prop.stringValue = value.StringValue;
                    break;
                case SerializedPropertyType.Color:
                    prop.colorValue = value.ColorValue;
                    break;
                case SerializedPropertyType.ObjectReference:
                    prop.objectReferenceValue = value.ObjectReferenceValue;
                    break;
                case SerializedPropertyType.Enum:
                    prop.enumValueIndex = value.EnumValueIndex;
                    break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = value.Vector2Value;
                    break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = value.Vector3Value;
                    break;
                case SerializedPropertyType.Vector4:
                    prop.vector4Value = value.Vector4Value;
                    break;
                case SerializedPropertyType.Rect:
                    prop.rectValue = value.RectValue;
                    break;
                case SerializedPropertyType.AnimationCurve:
                    prop.animationCurveValue = value.AnimationCurveValue;
                    break;
                case SerializedPropertyType.Bounds:
                    prop.boundsValue = value.BoundsValue;
                    break;
                case SerializedPropertyType.Quaternion:
                    prop.quaternionValue = value.QuaternionValue;
                    break;
                case SerializedPropertyType.Vector2Int:
                    prop.vector2IntValue = value.Vector2IntValue;
                    break;
                case SerializedPropertyType.Vector3Int:
                    prop.vector3IntValue = value.Vector3IntValue;
                    break;
                case SerializedPropertyType.RectInt:
                    prop.rectIntValue = value.RectIntValue;
                    break;
                case SerializedPropertyType.BoundsInt:
                    prop.boundsIntValue = value.BoundsIntValue;
                    break;
                case SerializedPropertyType.ArraySize:
                    prop.intValue = (int)value.IntValue;
                    break;
            }
        }

        #endregion

        #region 通用迁移

        /// <summary>
        /// 迁移组件：添加新组件并复制数据，但不删除旧组件。
        /// 旧→新映射通过 componentReplacementMap 输出，供后续统一修复引用和删除旧组件。
        /// </summary>
        private MigrationResult MigrateComponent(ScriptIsolationConfig config, GameObject go,
            Component oldComponent, ScriptMappingInfo mapping, string assetPath, string objectPath,
            Dictionary<Component, Component> componentReplacementMap)
        {
            var result = new MigrationResult
            {
                AssetPath = assetPath,
                ObjectPath = objectPath,
                OriginalType = mapping.OriginalClassName,
                NewType = mapping.NewClassName
            };

            if (config.DryRun)
            {
                result.Success = true;
                result.Message = "[Dry Run] 将替换组件";
                return result;
            }

            if (mapping.NewType == null)
            {
                result.Success = false;
                result.Message = $"新类型 {mapping.NewClassName} 未找到";
                return result;
            }

            try
            {
                Undo.RegisterCompleteObjectUndo(go, "Script Migration");

                var newComponent = go.AddComponent(mapping.NewType);
                if (newComponent == null)
                {
                    result.Success = false;
                    result.Message = $"无法添加组件 {mapping.NewClassName}";
                    return result;
                }

                EditorUtility.CopySerialized(oldComponent, newComponent);

                var fieldWarnings = ValidateFieldMigration(oldComponent, newComponent);
                result.FieldWarnings = fieldWarnings;

                componentReplacementMap[oldComponent] = newComponent;

                result.Success = true;
                result.Message = fieldWarnings.Count > 0
                    ? $"迁移完成，但有 {fieldWarnings.Count} 个字段警告"
                    : "迁移成功";
            }
            catch (Exception e)
            {
                result.Success = false;
                result.Message = $"迁移失败: {e.Message}";
            }

            return result;
        }

        /// <summary>
        /// 遍历根节点下所有 GameObject 的所有组件，将 ObjectReference 中指向旧组件的引用替换为新组件。
        /// 覆盖范围：普通字段引用、UnityEvent（Button.onClick 等）的 m_Target、数组/列表中的引用等。
        /// </summary>
        private void FixObjectReferences(GameObject root, Dictionary<Component, Component> replacementMap)
        {
            if (replacementMap.Count == 0) return;

            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allTransforms)
            {
                var components = t.gameObject.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    if (replacementMap.ContainsKey(comp)) continue;

                    var so = new SerializedObject(comp);
                    bool changed = false;

                    var prop = so.GetIterator();
                    while (prop.Next(true))
                    {
                        if (prop.propertyType == SerializedPropertyType.ObjectReference)
                        {
                            var refObj = prop.objectReferenceValue;
                            if (refObj != null && refObj is Component refComp
                                && replacementMap.TryGetValue(refComp, out var newComp))
                            {
                                prop.objectReferenceValue = newComp;
                                changed = true;
                            }
                        }
                    }

                    if (changed)
                    {
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
        }

        /// <summary>
        /// 统一删除所有已迁移的旧组件
        /// </summary>
        private void DestroyOldComponents(Dictionary<Component, Component> replacementMap)
        {
            foreach (var kvp in replacementMap)
            {
                if (kvp.Key != null)
                {
                    Undo.DestroyObjectImmediate(kvp.Key);
                }
            }
        }

        private List<string> ValidateFieldMigration(Component oldComponent, Component newComponent)
        {
            var warnings = new List<string>();

            var oldSo = new SerializedObject(oldComponent);
            var newSo = new SerializedObject(newComponent);

            var oldProp = oldSo.GetIterator();
            while (oldProp.NextVisible(true))
            {
                if (oldProp.name == "m_Script") continue;

                var newProp = newSo.FindProperty(oldProp.propertyPath);
                if (newProp == null)
                {
                    warnings.Add($"字段 '{oldProp.propertyPath}' 在新组件中不存在");
                }
                else if (newProp.propertyType != oldProp.propertyType)
                {
                    warnings.Add($"字段 '{oldProp.propertyPath}' 类型不匹配: " +
                                 $"{oldProp.propertyType} -> {newProp.propertyType}");
                }
            }

            return warnings;
        }

        #endregion

        private string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }
}
