// File: MigrationReport.cs
// 迁移报告生成器 - 仅通过 Console Log 输出
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ScriptIsolation
{
    public class MigrationReport
    {
        private List<ScriptAnalysisResult> analysisResults = new List<ScriptAnalysisResult>();
        private List<ScriptMappingInfo> mappings = new List<ScriptMappingInfo>();
        private List<MigrationResult> migrationResults = new List<MigrationResult>();
        private List<ReferenceWarning> referenceWarnings = new List<ReferenceWarning>();
        private List<string> createdScripts = new List<string>();
        private List<string> updatedReferencingScripts = new List<string>();
        private List<string> errors = new List<string>();
        
        private int sceneCount;
        private int prefabCount;

        public void AddAnalysisResult(ScriptAnalysisResult result)
        {
            analysisResults.Add(result);
        }

        public void AddMapping(ScriptMappingInfo mapping)
        {
            mappings.Add(mapping);
        }

        public void AddMigrationResult(MigrationResult result)
        {
            migrationResults.Add(result);
        }

        public void AddReferenceWarning(ReferenceWarning warning)
        {
            referenceWarnings.Add(warning);
        }

        public void AddCreatedScript(string path)
        {
            createdScripts.Add(path);
        }

        public void SetUpdatedReferencingScripts(List<string> scripts)
        {
            updatedReferencingScripts = scripts;
        }

        public void AddError(string error)
        {
            errors.Add(error);
        }

        public void SetSceneCount(int count)
        {
            sceneCount = count;
        }

        public void SetPrefabCount(int count)
        {
            prefabCount = count;
        }

        public void Generate(bool isDryRun)
        {
            var sb = new StringBuilder();
            string mode = isDryRun ? "[Dry Run 模式]" : "[执行模式]";
            
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine($"              脚本隔离与迁移报告 {mode}");
            sb.AppendLine($"              生成时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            GenerateSummary(sb);
            GenerateScriptAnalysis(sb);
            GenerateMappings(sb);
            GenerateUpdatedReferencingScripts(sb);
            GenerateMigrationDetails(sb);
            GenerateReferenceWarnings(sb);
            GenerateErrors(sb);
            GenerateLimitations(sb);

            Debug.Log(sb.ToString());
        }

        private void GenerateSummary(StringBuilder sb)
        {
            int successCount = 0;
            int failCount = 0;
            foreach (var r in migrationResults)
            {
                if (r.Success) successCount++;
                else failCount++;
            }

            sb.AppendLine("【摘要】");
            sb.AppendLine($"  • 分析脚本数: {analysisResults.Count}");
            sb.AppendLine($"  • 隔离脚本映射数: {mappings.Count}");
            sb.AppendLine($"  • 创建隔离脚本数: {createdScripts.Count}");
            sb.AppendLine($"  • 更新外部引用脚本数: {updatedReferencingScripts.Count}");
            sb.AppendLine($"  • 扫描场景数: {sceneCount}");
            sb.AppendLine($"  • 扫描 Prefab 数: {prefabCount}");
            sb.AppendLine($"  • 组件迁移成功: {successCount}");
            sb.AppendLine($"  • 组件迁移失败: {failCount}");
            sb.AppendLine($"  • 引用警告数: {referenceWarnings.Count}");
            sb.AppendLine($"  • 错误数: {errors.Count}");
            sb.AppendLine();
        }

        private void GenerateScriptAnalysis(StringBuilder sb)
        {
            sb.AppendLine("【脚本分析结果】");
            foreach (var result in analysisResults)
            {
                string status = result.IsValid ? "✓" : "✗";
                sb.AppendLine($"  {status} {result.ScriptPath}");
                sb.AppendLine($"      类名: {result.ClassName}");
                
                if (result.Issues.Count > 0)
                {
                    sb.AppendLine("      问题:");
                    foreach (var issue in result.Issues)
                    {
                        sb.AppendLine($"        - {issue}");
                    }
                }
            }
            sb.AppendLine();
        }

        private void GenerateMappings(StringBuilder sb)
        {
            sb.AppendLine("【类型映射】");
            foreach (var mapping in mappings)
            {
                sb.AppendLine($"  {mapping.OriginalClassName} → {mapping.NewClassName}");
                sb.AppendLine($"      原路径: {mapping.OriginalPath}");
                sb.AppendLine($"      新路径: {mapping.NewPath}");
            }
            sb.AppendLine();
        }

        private void GenerateUpdatedReferencingScripts(StringBuilder sb)
        {
            if (updatedReferencingScripts.Count == 0) return;

            sb.AppendLine("【已更新引用的外部脚本（直接修改原文件）】");
            foreach (var path in updatedReferencingScripts)
            {
                sb.AppendLine($"  ✓ {path}");
            }
            sb.AppendLine();
        }

        private void GenerateMigrationDetails(StringBuilder sb)
        {
            if (migrationResults.Count == 0)
            {
                sb.AppendLine("【迁移详情】");
                sb.AppendLine("  无组件需要迁移");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("【迁移详情】");
            
            string currentAsset = "";
            foreach (var result in migrationResults)
            {
                if (result.AssetPath != currentAsset)
                {
                    currentAsset = result.AssetPath;
                    sb.AppendLine($"\n  资源: {currentAsset}");
                }

                string status = result.Success ? "✓" : "✗";
                sb.AppendLine($"    {status} {result.ObjectPath}");
                sb.AppendLine($"        {result.OriginalType} → {result.NewType}");
                sb.AppendLine($"        {result.Message}");

                if (result.FieldWarnings.Count > 0)
                {
                    sb.AppendLine("        字段警告:");
                    foreach (var warning in result.FieldWarnings)
                    {
                        sb.AppendLine($"          - {warning}");
                    }
                }
            }
            sb.AppendLine();
        }

        private void GenerateReferenceWarnings(StringBuilder sb)
        {
            if (referenceWarnings.Count == 0)
            {
                sb.AppendLine("【引用警告】");
                sb.AppendLine("  未检测到未处理的强类型引用问题");
                sb.AppendLine();
                return;
            }

            sb.AppendLine("【引用警告】");
            sb.AppendLine("  以下脚本存在对旧类型的强类型引用，但未被自动更新：");
            sb.AppendLine("  （可能位于排除目录中，或未开启「更新外部引用脚本」选项）");
            sb.AppendLine();
            
            foreach (var warning in referenceWarnings)
            {
                sb.AppendLine($"  ⚠ {warning.ScriptPath}");
                sb.AppendLine($"      引用类型: {warning.ReferencedType}");
                sb.AppendLine($"      {warning.Message}");
            }
            
            sb.AppendLine();
            sb.AppendLine("  建议处理方式：");
            sb.AppendLine("    1. 检查是否需要将该脚本纳入更新范围（调整排除目录设置）");
            sb.AppendLine("    2. 将引用字段类型改为接口或基类");
            sb.AppendLine("    3. 使用 GetComponent<T>() 动态获取");
            sb.AppendLine("    4. 手动更新引用字段类型");
            sb.AppendLine();
        }

        private void GenerateErrors(StringBuilder sb)
        {
            if (errors.Count == 0) return;

            sb.AppendLine("【错误列表】");
            foreach (var error in errors)
            {
                sb.AppendLine($"  ✗ {error}");
            }
            sb.AppendLine();
        }

        private void GenerateLimitations(StringBuilder sb)
        {
            sb.AppendLine("【已知限制】");
            sb.AppendLine("  1. 排除目录中的脚本不会被自动更新引用，可能存在引用断开的情况。");
            sb.AppendLine("  2. 字段名/类型变更可能导致数据丢失，建议使用 [FormerlySerializedAs] 保持兼容。");
            sb.AppendLine("  3. Partial 类需要手动处理其他部分文件。");
            sb.AppendLine("  4. 嵌套类/多类文件可能无法正确处理。");
            sb.AppendLine("  5. 动态引用（字符串/反射）无法被检测。");
            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine("                          报告结束");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
        }
    }
}
