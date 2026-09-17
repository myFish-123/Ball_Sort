using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class AssetBundleManager : EditorWindow
{
    private Vector2 scrollPosition;
    private List<AssetBundleInfo> assetBundleInfos = new List<AssetBundleInfo>();
    private string searchFilter = "";
    private bool showOnlyWithBundle = true;
    private string newBundleName = "";
    private int totalAssetsCount = 0;
    private int assetsWithBundleCount = 0;

    [System.Serializable]
    private class AssetBundleInfo
    {
        public string assetPath;
        public string bundleName;
        public string variantName;
        public bool selected;
        public Object asset;

        public AssetBundleInfo(string path)
        {
            assetPath = path;
            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                bundleName = importer.assetBundleName;
                variantName = importer.assetBundleVariant;
            }
            asset = AssetDatabase.LoadAssetAtPath<Object>(path);
        }
    }

    [MenuItem("Tools/Custom Tools/Asset Bundle Manager")]
    public static void ShowWindow()
    {
        AssetBundleManager window = GetWindow<AssetBundleManager>("Asset Bundle Manager");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshAssetList();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);

        // 标题
        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel);
        titleStyle.fontSize = 16;
        titleStyle.alignment = TextAnchor.MiddleCenter;
        EditorGUILayout.LabelField("Asset Bundle 管理工具", titleStyle);

        EditorGUILayout.Space(10);

        // 统计信息
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField($"总资源数: {totalAssetsCount}", GUILayout.Width(150));
        EditorGUILayout.LabelField($"已设置Bundle的资源: {assetsWithBundleCount}", GUILayout.Width(200));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 工具栏
        EditorGUILayout.BeginHorizontal();

        // 搜索框
        EditorGUILayout.LabelField("搜索:", GUILayout.Width(40));
        searchFilter = EditorGUILayout.TextField(searchFilter, GUILayout.Width(200));

        // 过滤选项
        showOnlyWithBundle = EditorGUILayout.Toggle("仅显示有Bundle的资源", showOnlyWithBundle);

        // 刷新按钮
        if (GUILayout.Button("刷新", GUILayout.Width(60)))
        {
            RefreshAssetList();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        // 批量操作区域
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("批量操作", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("新Bundle名称:", GUILayout.Width(100));
        newBundleName = EditorGUILayout.TextField(newBundleName, GUILayout.Width(200));

        if (GUILayout.Button("设置选中资源的Bundle", GUILayout.Width(150)))
        {
            SetSelectedAssetsBundle(newBundleName);
        }

        if (GUILayout.Button("清空选中资源的Bundle", GUILayout.Width(150)))
        {
            SetSelectedAssetsBundle("");
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全选", GUILayout.Width(60)))
        {
            SelectAll(true);
        }
        if (GUILayout.Button("全不选", GUILayout.Width(60)))
        {
            SelectAll(false);
        }
        if (GUILayout.Button("选择有Bundle的资源", GUILayout.Width(130)))
        {
            SelectAssetsWithBundle();
        }
        if (GUILayout.Button("清空所有Bundle设置", GUILayout.Width(130)))
        {
            if (EditorUtility.DisplayDialog("确认", "确定要清空所有资源的Asset Bundle设置吗？", "确定", "取消"))
            {
                ClearAllBundles();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        EditorGUILayout.Space(5);

        // 资源列表标题
        EditorGUILayout.BeginHorizontal("box");
        EditorGUILayout.LabelField("选择", GUILayout.Width(40));
        EditorGUILayout.LabelField("资源路径", GUILayout.Width(300));
        EditorGUILayout.LabelField("Bundle名称", GUILayout.Width(150));
        EditorGUILayout.LabelField("类型", GUILayout.Width(100));
        EditorGUILayout.EndHorizontal();

        // 资源列表
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        foreach (var info in GetFilteredAssets())
        {
            EditorGUILayout.BeginHorizontal();

            // 选择框
            info.selected = EditorGUILayout.Toggle(info.selected, GUILayout.Width(40));

            // 资源路径（可点击）
            if (GUILayout.Button(info.assetPath, EditorStyles.label, GUILayout.Width(300)))
            {
                Selection.activeObject = info.asset;
                EditorGUIUtility.PingObject(info.asset);
            }

            // Bundle名称
            string bundleDisplay = string.IsNullOrEmpty(info.bundleName) ? "<无>" : info.bundleName;
            if (!string.IsNullOrEmpty(info.variantName))
            {
                bundleDisplay += $".{info.variantName}";
            }
            EditorGUILayout.LabelField(bundleDisplay, GUILayout.Width(150));

            // 资源类型
            string assetType = info.asset != null ? info.asset.GetType().Name : "Unknown";
            EditorGUILayout.LabelField(assetType, GUILayout.Width(100));

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndScrollView();
    }

    private void RefreshAssetList()
    {
        assetBundleInfos.Clear();

        // 获取所有资源
        string[] allAssets = AssetDatabase.GetAllAssetPaths();
        totalAssetsCount = 0;
        assetsWithBundleCount = 0;

        foreach (string assetPath in allAssets)
        {
            // 跳过目录和特殊文件
            if (AssetDatabase.IsValidFolder(assetPath) ||
                assetPath.StartsWith("Packages/") ||
                assetPath.EndsWith(".cs") ||
                assetPath.EndsWith(".dll") ||
                assetPath.Contains("/Editor/"))
            {
                continue;
            }

            // 只处理Assets目录下的资源
            if (!assetPath.StartsWith("Assets/"))
            {
                continue;
            }

            totalAssetsCount++;
            AssetBundleInfo info = new AssetBundleInfo(assetPath);

            if (!string.IsNullOrEmpty(info.bundleName))
            {
                assetsWithBundleCount++;
            }

            assetBundleInfos.Add(info);
        }
    }

    private List<AssetBundleInfo> GetFilteredAssets()
    {
        return assetBundleInfos.Where(info =>
        {
            // 根据Bundle过滤
            if (showOnlyWithBundle && string.IsNullOrEmpty(info.bundleName))
            {
                return false;
            }

            // 根据搜索词过滤
            if (!string.IsNullOrEmpty(searchFilter))
            {
                string lowerSearch = searchFilter.ToLower();
                return info.assetPath.ToLower().Contains(lowerSearch) ||
                       (info.bundleName != null && info.bundleName.ToLower().Contains(lowerSearch));
            }

            return true;
        }).ToList();
    }

    private void SelectAll(bool selected)
    {
        foreach (var info in GetFilteredAssets())
        {
            info.selected = selected;
        }
    }

    private void SelectAssetsWithBundle()
    {
        foreach (var info in assetBundleInfos)
        {
            info.selected = !string.IsNullOrEmpty(info.bundleName);
        }
    }

    private void SetSelectedAssetsBundle(string bundleName)
    {
        int modifiedCount = 0;

        foreach (var info in assetBundleInfos.Where(i => i.selected))
        {
            AssetImporter importer = AssetImporter.GetAtPath(info.assetPath);
            if (importer != null)
            {
                importer.assetBundleName = bundleName.ToLower();
                info.bundleName = importer.assetBundleName;
                modifiedCount++;
            }
        }

        if (modifiedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssetList();

            string message = string.IsNullOrEmpty(bundleName) ?
                $"已清空 {modifiedCount} 个资源的Bundle设置" :
                $"已将 {modifiedCount} 个资源设置为Bundle: {bundleName}";

            EditorUtility.DisplayDialog("操作完成", message, "确定");
        }
    }

    private void ClearAllBundles()
    {
        int clearedCount = 0;

        foreach (var info in assetBundleInfos.Where(i => !string.IsNullOrEmpty(i.bundleName)))
        {
            AssetImporter importer = AssetImporter.GetAtPath(info.assetPath);
            if (importer != null)
            {
                importer.assetBundleName = "";
                clearedCount++;
            }
        }

        if (clearedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshAssetList();

            EditorUtility.DisplayDialog("操作完成", $"已清空 {clearedCount} 个资源的Bundle设置", "确定");
        }
    }
}