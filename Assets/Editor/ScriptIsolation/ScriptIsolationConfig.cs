// File: ScriptIsolationConfig.cs
// 脚本隔离配置数据类
using System;
using System.Collections.Generic;
using UnityEditor;

namespace ScriptIsolation
{
    public class ScriptIsolationConfig
    {
        public List<MonoScript> SourceScripts { get; set; } = new List<MonoScript>();
        public string Suffix { get; set; } = "_B";
        public string OutputDirectory { get; set; } = "Assets/BranchB/Scripts";
        
        public bool ProcessScenes { get; set; } = true;
        public bool ProcessPrefabs { get; set; } = true;
        public bool IncludePrefabVariants { get; set; } = true;
        public bool RecursiveScan { get; set; } = true;
        public bool DryRun { get; set; } = true;
        
        public string SceneFolder { get; set; }
        public string PrefabFolder { get; set; }
        public List<string> ManualScenes { get; set; } = new List<string>();
        public List<string> ManualPrefabs { get; set; } = new List<string>();

        /// <summary>
        /// 是否自动扫描并修改引用了目标脚本的外部脚本（直接修改原文件中的引用）
        /// </summary>
        public bool UpdateReferencingScripts { get; set; } = true;

        /// <summary>
        /// 外部引用脚本的扫描目录（为空则扫描整个 Assets）
        /// </summary>
        public string ReferencingScanFolder { get; set; }

        /// <summary>
        /// 排除的扫描目录（如 Editor、Framework 等不需要隔离的目录）
        /// </summary>
        public List<string> ReferencingExcludeFolders { get; set; } = new List<string>();
    }

    public class ScriptMappingInfo
    {
        public MonoScript OriginalScript { get; set; }
        public string OriginalClassName { get; set; }
        public string NewClassName { get; set; }
        public string OriginalPath { get; set; }
        public string NewPath { get; set; }
        public System.Type OriginalType { get; set; }
        public System.Type NewType { get; set; }
    }

    public class MigrationResult
    {
        public string AssetPath { get; set; }
        public string ObjectPath { get; set; }
        public string OriginalType { get; set; }
        public string NewType { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> FieldWarnings { get; set; } = new List<string>();
    }

    public class ScriptAnalysisResult
    {
        public bool IsValid { get; set; } = true;
        public string ScriptPath { get; set; }
        public string ClassName { get; set; }
        public List<string> Issues { get; set; } = new List<string>();
        public bool HasMultipleClasses { get; set; }
        public bool IsPartialClass { get; set; }
        public bool ClassNameMismatch { get; set; }
    }

    public class ReferenceWarning
    {
        public string ScriptPath { get; set; }
        public string FieldName { get; set; }
        public string ReferencedType { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// 缓存 Prefab 实例上旧组件的序列化数据（含 property override 值），
    /// 用于 Prefab 资产处理后将 override 值回写到新组件
    /// </summary>
    public class InstanceOverrideCache
    {
        /// <summary>
        /// key = "scenePath|gameObjectPath|originalTypeName"
        /// </summary>
        public string Key { get; set; }
        public string ScenePath { get; set; }
        public string GameObjectPath { get; set; }
        public Type OriginalType { get; set; }
        
        /// <summary>
        /// 序列化属性快照：propertyPath → SerializedPropertyValue
        /// 只记录有 override 的属性
        /// </summary>
        public Dictionary<string, SerializedPropertyValue> OverrideValues { get; set; } 
            = new Dictionary<string, SerializedPropertyValue>();
    }

    /// <summary>
    /// 存储单个序列化属性的值（支持常见类型）
    /// </summary>
    public class SerializedPropertyValue
    {
        public SerializedPropertyType PropertyType { get; set; }
        public long IntValue { get; set; }
        public bool BoolValue { get; set; }
        public float FloatValue { get; set; }
        public double DoubleValue { get; set; }
        public string StringValue { get; set; }
        public UnityEngine.Color ColorValue { get; set; }
        public UnityEngine.Object ObjectReferenceValue { get; set; }
        public int ObjectReferenceInstanceIDValue { get; set; }
        public UnityEngine.Vector2 Vector2Value { get; set; }
        public UnityEngine.Vector3 Vector3Value { get; set; }
        public UnityEngine.Vector4 Vector4Value { get; set; }
        public UnityEngine.Rect RectValue { get; set; }
        public UnityEngine.AnimationCurve AnimationCurveValue { get; set; }
        public UnityEngine.Bounds BoundsValue { get; set; }
        public UnityEngine.Quaternion QuaternionValue { get; set; }
        public int EnumValueIndex { get; set; }
        public UnityEngine.Vector2Int Vector2IntValue { get; set; }
        public UnityEngine.Vector3Int Vector3IntValue { get; set; }
        public UnityEngine.RectInt RectIntValue { get; set; }
        public UnityEngine.BoundsInt BoundsIntValue { get; set; }
    }
}
