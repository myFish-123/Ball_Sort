using UnityEngine;
using UnityEditor;

/// <summary>
/// 2D平面排列工具
/// 用法：选择需要排列的物体的父物体（没有可临时添加），设置X/Y间距，点击按钮，脚本将以第一个子物体为起点进行单方向排列。
/// </summary>
public class ArrangeChildrenEditor : EditorWindow
{
    // 间距设置
    private float xSpacing = 1f;
    private float ySpacing = 0f;
    
    // 可选：是否使用第一个子物体的位置作为起点
    private bool useFirstChildAsOrigin = true;
    
    // 可选：是否保持原有Z轴位置
    private bool keepZPosition = true;
    
    // 添加菜单项
    [MenuItem("Tools/wwTs/2D平面排列")]
    public static void ShowWindow()
    {
        GetWindow<ArrangeChildrenEditor>("排列子物体");
    }
    
    private void OnGUI()
    {
        EditorGUILayout.LabelField("间距设置", EditorStyles.boldLabel);
        
        // 间距输入
        xSpacing = EditorGUILayout.FloatField("X轴间距", xSpacing);
        ySpacing = EditorGUILayout.FloatField("Y轴间距", ySpacing);
        
        EditorGUILayout.Space(10);
        
        // 可选设置
        EditorGUILayout.LabelField("选项", EditorStyles.boldLabel);
        useFirstChildAsOrigin = EditorGUILayout.Toggle("以第一个子物体为起点", useFirstChildAsOrigin);
        keepZPosition = EditorGUILayout.Toggle("保持Z轴位置", keepZPosition);
        
        EditorGUILayout.Space(20);
        
        // 执行按钮
        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("排列选中物体的子物体", GUILayout.Height(40)))
        {
            ArrangeSelected();
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(10);
        
        // 提示信息
        EditorGUILayout.HelpBox(
            "使用方法：\n" +
            "1. 在Hierarchy中选中父物体\n" +
            "2. 设置X/Y间距\n" +
            "3. 点击按钮执行排列", 
            MessageType.Info);
    }
    
    private void ArrangeSelected()
    {
        // 获取选中的物体
        GameObject selected = Selection.activeGameObject;
        
        if (selected == null)
        {
            EditorUtility.DisplayDialog("错误", "请先选中一个父物体！", "确定");
            return;
        }
        
        Transform parent = selected.transform;
        
        // 只获取一级子物体
        int childCount = parent.childCount;
        
        if (childCount == 0)
        {
            EditorUtility.DisplayDialog("错误", "选中的物体没有子物体！", "确定");
            return;
        }
        
        // 记录操作以便撤销
        Undo.RecordObjects(GetAllChildren(parent), "排列子物体");
        
        // 获取起点位置
        Vector3 startPos;
        if (useFirstChildAsOrigin && childCount > 0)
        {
            startPos = parent.GetChild(0).position;
        }
        else
        {
            startPos = parent.position;
        }
        
        // 排列子物体
        for (int i = 0; i < childCount; i++)
        {
            Transform child = parent.GetChild(i);
            
            // 计算新位置
            float newX = startPos.x + (xSpacing * i);
            float newY = startPos.y + (ySpacing * i);
            float newZ = keepZPosition ? child.position.z : startPos.z;
            
            child.position = new Vector3(newX, newY, newZ);
        }
        
        Debug.Log($"已排列 {childCount} 个子物体，X间距: {xSpacing}, Y间距: {ySpacing}");
    }
    
    // 获取所有子物体的Transform数组（用于Undo）
    private Transform[] GetAllChildren(Transform parent)
    {
        Transform[] children = new Transform[parent.childCount];
        for (int i = 0; i < parent.childCount; i++)
        {
            children[i] = parent.GetChild(i);
        }
        return children;
    }
}