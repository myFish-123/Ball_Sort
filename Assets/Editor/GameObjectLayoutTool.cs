using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// GameObject布局工具 - 用于在Unity编辑器中自动排列游戏对象
/// 支持横向、纵向和网格布局
/// </summary>
public class GameObjectLayoutTool : EditorWindow
{
    // 布局模式枚举
    public enum LayoutMode
    {
        Horizontal,  // 横向布局
        Vertical,    // 纵向布局
        Grid         // 网格布局
    }

    // 对齐方式枚举
    public enum AlignmentMode
    {
        TopLeft,
        TopCenter,
        TopRight,
        MiddleLeft,
        MiddleCenter,
        MiddleRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    // 工具参数
    private Transform parentTransform;
    private LayoutMode layoutMode = LayoutMode.Horizontal;
    private AlignmentMode alignmentMode = AlignmentMode.MiddleCenter;
    private Vector2 spacing = new Vector2(1f, 1f);
    private Vector2 padding = new Vector2(0f, 0f);
    private int columns = 3;  // 网格布局的列数

    // 选中的GameObject列表
    private List<Transform> selectedObjects = new List<Transform>();
    private Vector2 scrollPosition;

    // 预览相关
    private bool showPreview = false;
    private Dictionary<Transform, Vector3> originalPositions = new Dictionary<Transform, Vector3>();
    private Dictionary<Transform, Vector3> previewPositions = new Dictionary<Transform, Vector3>();

    [MenuItem("Tools/Custom Tools/GameObject Layout Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<GameObjectLayoutTool>("GameObject布局工具");
        window.minSize = new Vector2(400, 600);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader();
        EditorGUILayout.Space(10);

        DrawLayoutSettings();
        EditorGUILayout.Space(10);

        DrawObjectSelection();
        EditorGUILayout.Space(10);

        DrawActionButtons();

        EditorGUILayout.EndScrollView();
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("GameObject布局工具", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("此工具可以帮助您快速排列场景中的游戏对象，支持横向、纵向和网格布局。", MessageType.Info);
    }

    private void DrawLayoutSettings()
    {
        EditorGUILayout.LabelField("布局设置", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            // 布局模式
            layoutMode = (LayoutMode)EditorGUILayout.EnumPopup("布局模式", layoutMode);

            // 对齐方式
            alignmentMode = (AlignmentMode)EditorGUILayout.EnumPopup("对齐方式", alignmentMode);

            // 间距设置
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("间距设置", EditorStyles.miniLabel);

            if (layoutMode == LayoutMode.Horizontal)
            {
                spacing.x = EditorGUILayout.FloatField("水平间距", spacing.x);
            }
            else if (layoutMode == LayoutMode.Vertical)
            {
                spacing.y = EditorGUILayout.FloatField("垂直间距", spacing.y);
            }
            else // Grid
            {
                spacing = EditorGUILayout.Vector2Field("网格间距", spacing);
                columns = EditorGUILayout.IntSlider("列数", columns, 1, 10);
            }

            // 边距设置
            EditorGUILayout.Space(5);
            padding = EditorGUILayout.Vector2Field("边距", padding);

            // 父对象设置（可选）
            EditorGUILayout.Space(5);
            parentTransform = (Transform)EditorGUILayout.ObjectField(
                "父对象（可选）",
                parentTransform,
                typeof(Transform),
                true
            );
        }
    }

    private void DrawObjectSelection()
    {
        EditorGUILayout.LabelField("对象选择", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("从选中对象获取", GUILayout.Height(30)))
            {
                GetObjectsFromSelection();
            }

            if (GUILayout.Button("从父对象获取子对象", GUILayout.Height(30)))
            {
                GetObjectsFromParent();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"已选择 {selectedObjects.Count} 个游戏对象：");

            // 显示选中的对象列表
            if (selectedObjects.Count > 0)
            {
                using (var scrollView = new EditorGUILayout.ScrollViewScope(new Vector2(0, 100), GUILayout.MaxHeight(100)))
                {
                    for (int i = 0; i < selectedObjects.Count; i++)
                    {
                        if (selectedObjects[i] == null)
                        {
                            selectedObjects.RemoveAt(i);
                            i--;
                            continue;
                        }

                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField($"{i + 1}.", GUILayout.Width(30));
                        GUI.enabled = false;
                        EditorGUILayout.ObjectField(selectedObjects[i], typeof(Transform), true);
                        GUI.enabled = true;

                        if (GUILayout.Button("✕", GUILayout.Width(20)))
                        {
                            selectedObjects.RemoveAt(i);
                            i--;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("清空选择"))
            {
                selectedObjects.Clear();
                showPreview = false;
                ResetPreview();
            }
        }
    }

    private void DrawActionButtons()
    {
        EditorGUILayout.LabelField("操作", EditorStyles.boldLabel);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            // 预览按钮
            EditorGUILayout.BeginHorizontal();

            GUI.enabled = selectedObjects.Count > 0;

            if (GUILayout.Button(showPreview ? "隐藏预览" : "显示预览", GUILayout.Height(30)))
            {
                TogglePreview();
            }

            if (GUILayout.Button("应用布局", GUILayout.Height(30)))
            {
                ApplyLayout();
            }

            EditorGUILayout.EndHorizontal();

            GUI.enabled = true;

            if (showPreview)
            {
                EditorGUILayout.HelpBox("预览模式已开启，点击'应用布局'以确认更改。", MessageType.Warning);
            }
        }
    }

    private void GetObjectsFromSelection()
    {
        selectedObjects.Clear();

        GameObject[] selection = Selection.gameObjects;
        foreach (var go in selection)
        {
            selectedObjects.Add(go.transform);
        }

        // 根据层级顺序排序
        selectedObjects = selectedObjects.OrderBy(t => t.GetSiblingIndex()).ToList();

        if (showPreview)
        {
            UpdatePreview();
        }
    }

    private void GetObjectsFromParent()
    {
        if (parentTransform == null)
        {
            EditorUtility.DisplayDialog("提示", "请先设置父对象！", "确定");
            return;
        }

        selectedObjects.Clear();

        // 只获取第一级子对象
        foreach (Transform child in parentTransform)
        {
            selectedObjects.Add(child);
        }

        if (showPreview)
        {
            UpdatePreview();
        }
    }

    private void TogglePreview()
    {
        showPreview = !showPreview;

        if (showPreview)
        {
            SaveOriginalPositions();
            UpdatePreview();
        }
        else
        {
            ResetPreview();
        }

        SceneView.RepaintAll();
    }

    private void SaveOriginalPositions()
    {
        originalPositions.Clear();
        foreach (var obj in selectedObjects)
        {
            if (obj != null)
            {
                originalPositions[obj] = obj.position;
            }
        }
    }

    private void UpdatePreview()
    {
        if (!showPreview || selectedObjects.Count == 0) return;

        previewPositions.Clear();
        Vector3[] positions = CalculatePositions();

        for (int i = 0; i < selectedObjects.Count && i < positions.Length; i++)
        {
            if (selectedObjects[i] != null)
            {
                previewPositions[selectedObjects[i]] = positions[i];

                // 如果有父物体且对象是其子物体，使用localPosition
                if (parentTransform != null && selectedObjects[i].parent == parentTransform)
                {
                    selectedObjects[i].localPosition = parentTransform.InverseTransformPoint(positions[i]);
                }
                else
                {
                    selectedObjects[i].position = positions[i];
                }
            }
        }

        SceneView.RepaintAll();
    }

    private void ResetPreview()
    {
        foreach (var kvp in originalPositions)
        {
            if (kvp.Key != null)
            {
                kvp.Key.position = kvp.Value;
            }
        }

        previewPositions.Clear();
        SceneView.RepaintAll();
    }

    private void ApplyLayout()
    {
        if (selectedObjects.Count == 0)
        {
            EditorUtility.DisplayDialog("提示", "请先选择游戏对象！", "确定");
            return;
        }

        // 记录操作用于撤销
        Undo.RecordObjects(selectedObjects.ToArray(), "Apply GameObject Layout");

        Vector3[] positions = CalculatePositions();

        for (int i = 0; i < selectedObjects.Count && i < positions.Length; i++)
        {
            if (selectedObjects[i] != null)
            {
                // 如果有父物体且对象是其子物体，使用localPosition
                if (parentTransform != null && selectedObjects[i].parent == parentTransform)
                {
                    selectedObjects[i].localPosition = parentTransform.InverseTransformPoint(positions[i]);
                }
                else
                {
                    selectedObjects[i].position = positions[i];
                }
            }
        }

        // 保存新位置
        SaveOriginalPositions();
        showPreview = false;

        EditorUtility.DisplayDialog("成功", "布局已应用！", "确定");
    }

    private Vector3[] CalculatePositions()
    {
        Vector3[] positions = new Vector3[selectedObjects.Count];

        // 确定起始位置，明确这是局部坐标
        Vector3 startPos = GetStartPosition();

        switch (layoutMode)
        {
            case LayoutMode.Horizontal:
                CalculateHorizontalPositions(positions, startPos);
                break;
            case LayoutMode.Vertical:
                CalculateVerticalPositions(positions, startPos);
                break;
            case LayoutMode.Grid:
                CalculateGridPositions(positions, startPos);
                break;
        }

        return positions;
    }

    private Vector3 GetStartPosition()
    {
        // 以(0,0,0)为中心作为布局计算的起始点（局部坐标系）
        Vector3 startPos = Vector3.zero;

        // 添加边距
        startPos.x += padding.x;
        startPos.y -= padding.y;

        return startPos;
    }

    private void CalculateHorizontalPositions(Vector3[] positions, Vector3 startPos)
    {
        float currentX = startPos.x;

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            // 直接使用间距计算位置
            positions[i] = new Vector3(currentX, startPos.y, startPos.z);
            currentX += spacing.x;
        }

        // 根据对齐方式调整
        ApplyAlignment(positions, LayoutMode.Horizontal);
    }

    private void CalculateVerticalPositions(Vector3[] positions, Vector3 startPos)
    {
        float currentY = startPos.y;

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            // 直接使用间距计算位置
            positions[i] = new Vector3(startPos.x, currentY, startPos.z);
            currentY -= spacing.y;
        }

        // 根据对齐方式调整
        ApplyAlignment(positions, LayoutMode.Vertical);
    }

    private void CalculateGridPositions(Vector3[] positions, Vector3 startPos)
    {
        float currentX = startPos.x;
        float currentY = startPos.y;

        for (int i = 0; i < selectedObjects.Count; i++)
        {
            int row = i / columns;
            int col = i % columns;

            // 直接使用间距计算位置
            positions[i] = new Vector3(
                startPos.x + col * spacing.x,
                startPos.y - row * spacing.y,
                startPos.z
            );
        }

        // 根据对齐方式调整
        ApplyAlignment(positions, LayoutMode.Grid);
    }

    private void ApplyAlignment(Vector3[] positions, LayoutMode mode)
    {
        if (positions.Length == 0) return;

        // 计算边界
        Bounds bounds = CalculateBounds(positions);
        Vector3 offset = Vector3.zero;

        // 水平对齐
        switch (alignmentMode)
        {
            case AlignmentMode.TopLeft:
            case AlignmentMode.MiddleLeft:
            case AlignmentMode.BottomLeft:
                // 左对齐，不需要偏移
                break;

            case AlignmentMode.TopCenter:
            case AlignmentMode.MiddleCenter:
            case AlignmentMode.BottomCenter:
                offset.x = -bounds.center.x;
                break;

            case AlignmentMode.TopRight:
            case AlignmentMode.MiddleRight:
            case AlignmentMode.BottomRight:
                offset.x = -bounds.max.x;
                break;
        }

        // 垂直对齐
        switch (alignmentMode)
        {
            case AlignmentMode.TopLeft:
            case AlignmentMode.TopCenter:
            case AlignmentMode.TopRight:
                // 顶部对齐，不需要偏移
                break;

            case AlignmentMode.MiddleLeft:
            case AlignmentMode.MiddleCenter:
            case AlignmentMode.MiddleRight:
                offset.y = -bounds.center.y;
                break;

            case AlignmentMode.BottomLeft:
            case AlignmentMode.BottomCenter:
            case AlignmentMode.BottomRight:
                offset.y = -bounds.min.y;
                break;
        }

        // 应用偏移
        for (int i = 0; i < positions.Length; i++)
        {
            positions[i] += offset;

            // 如果有父物体，转换为世界坐标
            if (parentTransform != null)
            {
                positions[i] = parentTransform.TransformPoint(positions[i]);
            }
        }
    }

    private Bounds CalculateBounds(Vector3[] positions)
    {
        if (positions.Length == 0) return new Bounds();

        Bounds bounds = new Bounds(positions[0], Vector3.zero);

        foreach (var pos in positions)
        {
            bounds.Encapsulate(pos);
        }

        return bounds;
    }

    private void OnDisable()
    {
        if (showPreview)
        {
            ResetPreview();
        }
    }

    private void OnDestroy()
    {
        if (showPreview)
        {
            ResetPreview();
        }
    }
}