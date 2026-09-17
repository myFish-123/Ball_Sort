using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace EditorTools
{
    public class ScriptReplacerWindow : EditorWindow
    {
        private MonoScript sourceScript;
        private MonoScript targetScript;
        private GameObject targetGameObject;

        [MenuItem("Tools/Custom Tools/Script Replacer")]
        public static void ShowWindow()
        {
            GetWindow<ScriptReplacerWindow>("Script Replacer");
        }

        private void OnGUI()
        {
            GUILayout.Label("Script Replacer Tool", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            sourceScript = (MonoScript)EditorGUILayout.ObjectField("Source Script (A)", sourceScript, typeof(MonoScript), false);
            targetScript = (MonoScript)EditorGUILayout.ObjectField("Target Script (B)", targetScript, typeof(MonoScript), false);
            targetGameObject = (GameObject)EditorGUILayout.ObjectField("Target GameObject", targetGameObject, typeof(GameObject), true);

            EditorGUILayout.Space();

            if (GUILayout.Button("Replace Scripts"))
            {
                if (sourceScript == null || targetScript == null || targetGameObject == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please fill all fields.", "OK");
                    return;
                }

                if (sourceScript == targetScript)
                {
                    EditorUtility.DisplayDialog("Error", "Source and Target scripts cannot be the same.", "OK");
                    return;
                }

                ReplaceScripts();
            }
        }

        private void ReplaceScripts()
        {
            if (sourceScript == null || targetScript == null)
            {
                Debug.LogError("Invalid scripts.");
                return;
            }

            Type sourceType = sourceScript.GetClass();
            Type targetType = targetScript.GetClass();

            if (sourceType == null || targetType == null)
            {
                Debug.LogError("Invalid script types.");
                return;
            }

            // 获取所有子物体（包括自身）
            Transform[] transforms = targetGameObject.GetComponentsInChildren<Transform>(true);
            int count = 0;

            // 注册撤销组
            Undo.SetCurrentGroupName("Replace Scripts");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (Transform t in transforms)
            {
                Component[] components = t.GetComponents(sourceType);

                // 因为可能挂载了多个相同的脚本，所以遍历处理
                // 使用 List 缓存组件，避免在销毁时影响数组遍历
                List<Component> sourceComponents = new List<Component>(components);

                foreach (Component sourceComp in sourceComponents)
                {
                    if (sourceComp == null) continue;

                    // 1. 读取 Source 数据 (Key: propertyPath, Value: object)
                    // 使用 List 保持顺序，确保先设置 Array.size 再设置元素
                    List<KeyValuePair<string, object>> fieldValues = GetFieldValues(sourceComp);

                    // 2. 销毁 Source 组件 (支持撤销)
                    Undo.DestroyObjectImmediate(sourceComp);

                    // 3. 添加 Target 组件 (支持撤销)
                    Component targetComp = Undo.AddComponent(t.gameObject, targetType);

                    // 4. 写入 Target 数据
                    if (targetComp != null)
                    {
                        SetFieldValues(targetComp, fieldValues);
                        count++;
                    }
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"Replaced {count} scripts from {sourceScript.name} to {targetScript.name}.");
            AssetDatabase.Refresh();
        }

        // 使用 SerializedObject 获取数据
        private List<KeyValuePair<string, object>> GetFieldValues(Component component)
        {
            var values = new List<KeyValuePair<string, object>>();
            SerializedObject so = new SerializedObject(component);
            SerializedProperty prop = so.GetIterator();

            // 使用 NextVisible(true) 遍历所有可见属性，包括子属性
            if (prop.NextVisible(true))
            {
                do
                {
                    // 忽略脚本自身的引用字段 "m_Script"
                    if (prop.name == "m_Script") continue;

                    object value = GetValueFromProperty(prop);
                    if (value != null) // 只有支持的类型才记录
                    {
                        values.Add(new KeyValuePair<string, object>(prop.propertyPath, value));
                    }
                }
                while (prop.NextVisible(true)); // 继续深入遍历子属性
            }

            return values;
        }

        private void SetFieldValues(Component component, List<KeyValuePair<string, object>> values)
        {
            SerializedObject so = new SerializedObject(component);
            so.Update();

            foreach (var kvp in values)
            {
                string path = kvp.Key;
                object value = kvp.Value;

                SerializedProperty prop = so.FindProperty(path);
                if (prop != null)
                {
                    // 类型检查：确保目标属性类型与值类型兼容
                    if (IsTypeCompatible(prop, value))
                    {
                        SetValueToProperty(prop, value);
                    }
                }
            }

            so.ApplyModifiedProperties();
        }

        // 简单的类型兼容性检查
        private bool IsTypeCompatible(SerializedProperty prop, object value)
        {
            if (value == null) return true; // null 可以赋值给对象引用

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return value is int || value is long;
                case SerializedPropertyType.Boolean: return value is bool;
                case SerializedPropertyType.Float: return value is float || value is double;
                case SerializedPropertyType.String: return value is string;
                case SerializedPropertyType.Color: return value is Color;
                case SerializedPropertyType.ObjectReference: return value is UnityEngine.Object;
                case SerializedPropertyType.LayerMask: return value is int;
                case SerializedPropertyType.Enum: return value is int || value is Enum;
                case SerializedPropertyType.Vector2: return value is Vector2;
                case SerializedPropertyType.Vector3: return value is Vector3;
                case SerializedPropertyType.Vector4: return value is Vector4;
                case SerializedPropertyType.Rect: return value is Rect;
                case SerializedPropertyType.ArraySize: return value is int;
                case SerializedPropertyType.Character: return value is char || value is int;
                case SerializedPropertyType.AnimationCurve: return value is AnimationCurve;
                case SerializedPropertyType.Bounds: return value is Bounds;
                case SerializedPropertyType.Quaternion: return value is Quaternion;
                case SerializedPropertyType.Vector2Int: return value is Vector2Int;
                case SerializedPropertyType.Vector3Int: return value is Vector3Int;
                case SerializedPropertyType.RectInt: return value is RectInt;
                case SerializedPropertyType.BoundsInt: return value is BoundsInt;
                default: return false;
            }
        }

        private object GetValueFromProperty(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue;
                case SerializedPropertyType.Boolean: return prop.boolValue;
                case SerializedPropertyType.Float: return prop.floatValue;
                case SerializedPropertyType.String: return prop.stringValue;
                case SerializedPropertyType.Color: return prop.colorValue;
                case SerializedPropertyType.ObjectReference: return prop.objectReferenceValue;
                case SerializedPropertyType.LayerMask: return prop.intValue;
                case SerializedPropertyType.Enum: return prop.intValue;
                case SerializedPropertyType.Vector2: return prop.vector2Value;
                case SerializedPropertyType.Vector3: return prop.vector3Value;
                case SerializedPropertyType.Vector4: return prop.vector4Value;
                case SerializedPropertyType.Rect: return prop.rectValue;
                case SerializedPropertyType.ArraySize: return prop.intValue;
                case SerializedPropertyType.Character: return (char)prop.intValue;
                case SerializedPropertyType.AnimationCurve: return prop.animationCurveValue;
                case SerializedPropertyType.Bounds: return prop.boundsValue;
                case SerializedPropertyType.Quaternion: return prop.quaternionValue;
                case SerializedPropertyType.Vector2Int: return prop.vector2IntValue;
                case SerializedPropertyType.Vector3Int: return prop.vector3IntValue;
                case SerializedPropertyType.RectInt: return prop.rectIntValue;
                case SerializedPropertyType.BoundsInt: return prop.boundsIntValue;
                default: return null;
            }
        }

        private void SetValueToProperty(SerializedProperty prop, object value)
        {
            if (value == null && prop.propertyType == SerializedPropertyType.ObjectReference)
            {
                prop.objectReferenceValue = null;
                return;
            }

            if (value == null) return;

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: prop.intValue = Convert.ToInt32(value); break;
                case SerializedPropertyType.Boolean: prop.boolValue = (bool)value; break;
                case SerializedPropertyType.Float: prop.floatValue = Convert.ToSingle(value); break;
                case SerializedPropertyType.String: prop.stringValue = (string)value; break;
                case SerializedPropertyType.Color: prop.colorValue = (Color)value; break;
                case SerializedPropertyType.ObjectReference: prop.objectReferenceValue = (UnityEngine.Object)value; break;
                case SerializedPropertyType.LayerMask: prop.intValue = (int)value; break;
                case SerializedPropertyType.Enum: prop.intValue = (int)value; break;
                case SerializedPropertyType.Vector2: prop.vector2Value = (Vector2)value; break;
                case SerializedPropertyType.Vector3: prop.vector3Value = (Vector3)value; break;
                case SerializedPropertyType.Vector4: prop.vector4Value = (Vector4)value; break;
                case SerializedPropertyType.Rect: prop.rectValue = (Rect)value; break;
                case SerializedPropertyType.ArraySize: prop.intValue = (int)value; break;
                case SerializedPropertyType.Character: prop.intValue = (char)value; break;
                case SerializedPropertyType.AnimationCurve: prop.animationCurveValue = (AnimationCurve)value; break;
                case SerializedPropertyType.Bounds: prop.boundsValue = (Bounds)value; break;
                case SerializedPropertyType.Quaternion: prop.quaternionValue = (Quaternion)value; break;
                case SerializedPropertyType.Vector2Int: prop.vector2IntValue = (Vector2Int)value; break;
                case SerializedPropertyType.Vector3Int: prop.vector3IntValue = (Vector3Int)value; break;
                case SerializedPropertyType.RectInt: prop.rectIntValue = (RectInt)value; break;
                case SerializedPropertyType.BoundsInt: prop.boundsIntValue = (BoundsInt)value; break;
            }
        }
    }
}
