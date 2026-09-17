// File: ScriptAnalyzer.cs
// 脚本分析器 - 检测脚本结构问题
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace ScriptIsolation
{
    public class ScriptAnalyzer
    {
        private static readonly Regex ClassRegex = new Regex(
            @"(?:public|internal|private|protected)?\s*(?:partial\s+)?class\s+(\w+)\s*(?::\s*[\w\s,<>]+)?(?:\s*where\s+\w+\s*:\s*[\w\s,<>]+)*\s*\{",
            RegexOptions.Compiled);
        
        private static readonly Regex PartialRegex = new Regex(
            @"\bpartial\s+class\s+(\w+)",
            RegexOptions.Compiled);
        
        private static readonly Regex MonoBehaviourRegex = new Regex(
            @"class\s+\w+\s*:\s*(?:[\w\s,<>]*\b)?(MonoBehaviour|ScriptableObject)\b",
            RegexOptions.Compiled);

        /// <summary>
        /// 匹配同文件内定义的 struct / enum / interface / 非主类 class
        /// </summary>
        private static readonly Regex AuxTypeRegex = new Regex(
            @"(?:public|internal|private|protected)?\s*(?:partial\s+)?(?:struct|enum|interface|class)\s+(\w+)",
            RegexOptions.Compiled);

        public ScriptAnalysisResult Analyze(MonoScript script)
        {
            var result = new ScriptAnalysisResult
            {
                ScriptPath = AssetDatabase.GetAssetPath(script)
            };

            if (script == null)
            {
                result.IsValid = false;
                result.Issues.Add("脚本为空");
                return result;
            }

            string content = script.text;
            string fileName = Path.GetFileNameWithoutExtension(result.ScriptPath);
            
            var classMatches = ClassRegex.Matches(content);
            var classNames = new List<string>();
            
            foreach (Match match in classMatches)
            {
                classNames.Add(match.Groups[1].Value);
            }

            if (classNames.Count == 0)
            {
                result.IsValid = false;
                result.Issues.Add("未找到类定义");
                return result;
            }

            if (classNames.Count > 1)
            {
                result.HasMultipleClasses = true;
                result.Issues.Add($"文件包含多个类定义: {string.Join(", ", classNames)}");
            }

            var mainClass = script.GetClass();
            if (mainClass != null)
            {
                result.ClassName = mainClass.Name;
                
                if (mainClass.Name != fileName)
                {
                    result.ClassNameMismatch = true;
                    result.Issues.Add($"类名 '{mainClass.Name}' 与文件名 '{fileName}' 不一致");
                }
            }
            else
            {
                result.ClassName = classNames.Count > 0 ? classNames[0] : fileName;
                result.Issues.Add("无法获取脚本类型（可能存在编译错误）");
                result.IsValid = false;
            }

            var partialMatches = PartialRegex.Matches(content);
            if (partialMatches.Count > 0)
            {
                result.IsPartialClass = true;
                result.Issues.Add("脚本使用了 partial class，可能需要手动处理其他部分");
            }

            if (!MonoBehaviourRegex.IsMatch(content))
            {
                result.Issues.Add("脚本可能不是 MonoBehaviour 或 ScriptableObject 的子类");
            }

            if (result.Issues.Count > 0 && result.IsValid)
            {
                result.IsValid = !result.HasMultipleClasses;
            }

            return result;
        }

        public string RenameClassInContent(string content, string oldClassName, string newClassName)
        {
            string pattern = $@"\bclass\s+{Regex.Escape(oldClassName)}\b";
            content = Regex.Replace(content, pattern, $"class {newClassName}");
            
            string constructorPattern = $@"(\bpublic|private|protected|internal)?\s*{Regex.Escape(oldClassName)}\s*\(";
            content = Regex.Replace(content, constructorPattern, match =>
            {
                string modifier = match.Groups[1].Success ? match.Groups[1].Value + " " : "";
                return $"{modifier}{newClassName}(";
            });

            return content;
        }

        /// <summary>
        /// 扫描文件内容，提取除主类以外的所有 struct/enum/interface/class 类型名。
        /// mainClassName: 主类名（将被排除）
        /// </summary>
        public List<string> FindAuxiliaryTypeNames(string content, string mainClassName)
        {
            var result = new List<string>();
            var matches = AuxTypeRegex.Matches(content);
            foreach (Match match in matches)
            {
                string typeName = match.Groups[1].Value;
                if (typeName != mainClassName)
                {
                    result.Add(typeName);
                }
            }
            return result;
        }

        /// <summary>
        /// 在隔离脚本内容中替换所有同批隔离脚本的类型引用，实现无缝衔接。
        /// 同时自动检测并重命名同文件内定义的附属类型（struct/enum/interface 等）。
        /// allMappings: key=原始类名, value=新类名（包含自身）
        /// suffix: 隔离后缀（如 "_A"），用于附属类型的重命名
        /// </summary>
        public string RenameAllReferencesInContent(string content, string selfOldName, 
            Dictionary<string, string> allMappings, string suffix = null)
        {
            // 自动推断后缀
            if (string.IsNullOrEmpty(suffix) && allMappings.ContainsKey(selfOldName))
            {
                string newName = allMappings[selfOldName];
                if (newName.Length > selfOldName.Length)
                {
                    suffix = newName.Substring(selfOldName.Length);
                }
            }

            // 收集同文件内的附属类型（struct/enum/interface/非主类 class）
            var auxTypeNames = FindAuxiliaryTypeNames(content, selfOldName);

            // 构建完整映射表（包含附属类型）
            var fullMappings = new Dictionary<string, string>(allMappings);
            if (!string.IsNullOrEmpty(suffix))
            {
                foreach (var auxName in auxTypeNames)
                {
                    if (!fullMappings.ContainsKey(auxName))
                    {
                        fullMappings[auxName] = auxName + suffix;
                    }
                }
            }

            // 先处理自身主类
            if (fullMappings.ContainsKey(selfOldName))
            {
                string newName = fullMappings[selfOldName];
                content = RenameClassInContent(content, selfOldName, newName);
                content = ReplaceTypeReference(content, selfOldName, newName);
            }

            // 处理附属类型的声明重命名（struct/enum/interface 声明）
            foreach (var auxName in auxTypeNames)
            {
                if (fullMappings.ContainsKey(auxName))
                {
                    string newAuxName = fullMappings[auxName];
                    content = RenameAuxTypeDeclaration(content, auxName, newAuxName);
                    content = ReplaceTypeReference(content, auxName, newAuxName);
                }
            }

            // 处理其他同批脚本的类型引用
            foreach (var kvp in allMappings)
            {
                if (kvp.Key == selfOldName) continue;
                content = ReplaceTypeReference(content, kvp.Key, kvp.Value);
            }

            return content;
        }

        /// <summary>
        /// 获取隔离脚本中附属类型的映射表（原始名 → 新名），供外部引用替换使用。
        /// </summary>
        public Dictionary<string, string> GetAuxTypeMappings(string content, string mainClassName, string suffix)
        {
            var result = new Dictionary<string, string>();
            var auxNames = FindAuxiliaryTypeNames(content, mainClassName);
            foreach (var name in auxNames)
            {
                result[name] = name + suffix;
            }
            return result;
        }

        /// <summary>
        /// 重命名 struct/enum/interface 声明（不含 class，class 由 RenameClassInContent 处理）
        /// </summary>
        private string RenameAuxTypeDeclaration(string content, string oldName, string newName)
        {
            string pattern = $@"\b(struct|enum|interface)\s+{Regex.Escape(oldName)}\b";
            content = Regex.Replace(content, pattern, $"$1 {newName}");
            return content;
        }

        /// <summary>
        /// 替换代码中对指定类型的所有引用（不包括类/struct/enum/interface 声明和方法名）。
        /// 使用负向前瞻排除紧跟 '(' 的方法名位置，避免误替换同名方法。
        /// </summary>
        private string ReplaceTypeReference(string content, string oldTypeName, string newTypeName)
        {
            string escaped = Regex.Escape(oldTypeName);
            // 1. 替换 new TypeName( 构造调用（前面有 new 关键字，后面跟 '(' 或 '{'）
            string ctorPattern = $@"(?<=\bnew\s+)\b{escaped}\b(?=\s*[\({{])";
            content = Regex.Replace(content, ctorPattern, newTypeName);
            // 2. 替换类型引用，排除：声明关键字后（由专用方法处理）、方法名（紧跟 '('）
            string typePattern = $@"(?<!(?:class|struct|enum|interface)\s+)\b{escaped}\b(?!\s*\()";
            content = Regex.Replace(content, typePattern, newTypeName);
            return content;
        }
    }
}
