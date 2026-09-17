using UnityEngine;
using UnityEditor;
using System.IO;

// 翻译配置的ScriptableObject
[CreateAssetMenu(fileName = "MLT_TranslationConfig", menuName = "Tools/MLT Translation/Translation Config")]
public class MLT_TranslationConfig : ScriptableObject
{
    [Header("API Configuration")]
    [Tooltip("Your OpenRouter API Key")]
    public string openRouterApiKey = "";

    // 支持的 AI 模型列表
    // OpenRouter 模型格式说明：
    // 格式为 "provider/model-name"
    // provider: 模型提供商的标识（如 anthropic, openai, google 等）
    // model-name: 具体的模型名称和版本
    // 完整的模型列表可以在 https://openrouter.ai/models 查看
    [Tooltip("Preferred AI Model for translation")]
    public string preferredModel = "openai/o3";

    [Header("Translation Settings")]
    [Tooltip("Game context to provide to AI for better translations")]
    [TextArea(3, 5)]
    public string gameContext = "This is UI text for a mobile casual puzzle game. Keep translations concise, friendly, and suitable for all ages.";

    [Tooltip("Languages to translate to")]
    public SystemLanguage[] targetLanguages = new SystemLanguage[]
    {
        SystemLanguage.Afrikaans,
        SystemLanguage.Arabic,
        SystemLanguage.Basque,
        SystemLanguage.Belarusian,
        SystemLanguage.Bulgarian,
        SystemLanguage.Catalan,
        SystemLanguage.Chinese,
        SystemLanguage.ChineseSimplified,
        SystemLanguage.ChineseTraditional,
        SystemLanguage.Czech,
        SystemLanguage.Danish,
        SystemLanguage.Dutch,
        SystemLanguage.English,
        SystemLanguage.Estonian,
        SystemLanguage.Faroese,
        SystemLanguage.Finnish,
        SystemLanguage.French,
        SystemLanguage.German,
        SystemLanguage.Greek,
        SystemLanguage.Hebrew,
        SystemLanguage.Hindi,
        SystemLanguage.Hungarian,
        SystemLanguage.Icelandic,
        SystemLanguage.Indonesian,
        SystemLanguage.Italian,
        SystemLanguage.Japanese,
        SystemLanguage.Korean,
        SystemLanguage.Latvian,
        SystemLanguage.Lithuanian,
        SystemLanguage.Norwegian,
        SystemLanguage.Polish,
        SystemLanguage.Portuguese,
        SystemLanguage.Romanian,
        SystemLanguage.Russian,
        SystemLanguage.SerboCroatian,
        SystemLanguage.Slovak,
        SystemLanguage.Slovenian,
        SystemLanguage.Spanish,
        SystemLanguage.Swedish,
        SystemLanguage.Thai,
        SystemLanguage.Turkish,
        SystemLanguage.Ukrainian,
        SystemLanguage.Vietnamese
    };

    [Header("Advanced Settings")]
    [Tooltip("Delay between API calls (milliseconds)")]
    public int apiCallDelay = 1000;

    [Tooltip("Maximum retries for failed translations")]
    public int maxRetries = 3;

    // 单例模式
    private static MLT_TranslationConfig _instance;
    public static MLT_TranslationConfig Instance
    {
        get
        {
            if (_instance == null)
            {
                // 尝试从资源中加载
                _instance = Resources.Load<MLT_TranslationConfig>("MLT_TranslationConfig");

                // 如果没有找到，尝试从Assets中查找
                if (_instance == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:MLT_TranslationConfig");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = AssetDatabase.LoadAssetAtPath<MLT_TranslationConfig>(path);
                    }
                }

                // 如果还是没有，创建一个新的
                if (_instance == null)
                {
                    _instance = CreateInstance<MLT_TranslationConfig>();

                    // 确保目录存在
                    string resourcesPath = "Assets/Resources";
                    if (!Directory.Exists(resourcesPath))
                    {
                        Directory.CreateDirectory(resourcesPath);
                    }

                    // 保存到Resources文件夹
                    string assetPath = Path.Combine(resourcesPath, "MLT_TranslationConfig.asset");
                    AssetDatabase.CreateAsset(_instance, assetPath);
                    AssetDatabase.SaveAssets();

                    Debug.Log($"Created MLT_TranslationConfig at {assetPath}");
                }
            }
            return _instance;
        }
    }

    // 验证API密钥是否有效
    public bool IsApiKeyValid()
    {
        return !string.IsNullOrEmpty(openRouterApiKey);
    }

    // 获取语言显示名称
    public static string GetLanguageDisplayName(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.Afrikaans: return "Afrikaans";
            case SystemLanguage.Arabic: return "العربية";
            case SystemLanguage.Basque: return "Euskera";
            case SystemLanguage.Belarusian: return "Беларуская";
            case SystemLanguage.Bulgarian: return "Български";
            case SystemLanguage.Catalan: return "Català";
            case SystemLanguage.Chinese: return "中文";
            case SystemLanguage.Czech: return "Čeština";
            case SystemLanguage.Danish: return "Dansk";
            case SystemLanguage.Dutch: return "Nederlands";
            case SystemLanguage.English: return "English";
            case SystemLanguage.Estonian: return "Eesti";
            case SystemLanguage.Faroese: return "Føroyskt";
            case SystemLanguage.Finnish: return "Suomi";
            case SystemLanguage.French: return "Français";
            case SystemLanguage.German: return "Deutsch";
            case SystemLanguage.Greek: return "Ελληνικά";
            case SystemLanguage.Hebrew: return "עברית";
            case SystemLanguage.Hungarian: return "Magyar";
            case SystemLanguage.Icelandic: return "Íslenska";
            case SystemLanguage.Indonesian: return "Bahasa Indonesia";
            case SystemLanguage.Italian: return "Italiano";
            case SystemLanguage.Japanese: return "日本語";
            case SystemLanguage.Korean: return "한국어";
            case SystemLanguage.Latvian: return "Latviešu";
            case SystemLanguage.Lithuanian: return "Lietuvių";
            case SystemLanguage.Norwegian: return "Norsk";
            case SystemLanguage.Polish: return "Polski";
            case SystemLanguage.Portuguese: return "Português";
            case SystemLanguage.Romanian: return "Română";
            case SystemLanguage.Russian: return "Русский";
            case SystemLanguage.SerboCroatian: return "Srpsko-hrvatski";
            case SystemLanguage.Slovak: return "Slovenčina";
            case SystemLanguage.Slovenian: return "Slovenščina";
            case SystemLanguage.Spanish: return "Español";
            case SystemLanguage.Swedish: return "Svenska";
            case SystemLanguage.Thai: return "ไทย";
            case SystemLanguage.Turkish: return "Türkçe";
            case SystemLanguage.Ukrainian: return "Українська";
            case SystemLanguage.Vietnamese: return "Tiếng Việt";
            case SystemLanguage.ChineseSimplified: return "简体中文";
            case SystemLanguage.ChineseTraditional: return "繁體中文";
            case SystemLanguage.Hindi: return "हिन्दी";
            default: return language.ToString();
        }
    }
}

// 配置编辑器窗口
public class MLT_TranslationConfigWindow : EditorWindow
{
    private MLT_TranslationConfig config;
    private Vector2 scrollPos;

    [MenuItem("Tools/MLT Translation/MLT Translation Settings")]
    public static void ShowWindow()
    {
        var window = GetWindow<MLT_TranslationConfigWindow>("MLT Translation Settings");
        window.minSize = new Vector2(400, 300);
    }

    private void OnEnable()
    {
        config = MLT_TranslationConfig.Instance;
    }

    private void OnGUI()
    {
        if (config == null)
        {
            EditorGUILayout.HelpBox("Configuration not found!", MessageType.Error);
            return;
        }

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // 标题
        GUILayout.Label("MLT Translation Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // API配置
        EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
        config.openRouterApiKey = EditorGUILayout.PasswordField("OpenRouter API Key:", config.openRouterApiKey);
        config.preferredModel = EditorGUILayout.TextField("Preferred Model:", config.preferredModel);

        if (!config.IsApiKeyValid())
        {
            EditorGUILayout.HelpBox("Please enter your OpenRouter API Key to use AI translation.", MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox("API Key is configured.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // 游戏上下文
        EditorGUILayout.LabelField("Game Context", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("This helps AI understand your game better:");
        config.gameContext = EditorGUILayout.TextArea(config.gameContext, GUILayout.Height(60));

        EditorGUILayout.Space();

        // 目标语言
        EditorGUILayout.LabelField("Target Languages", EditorStyles.boldLabel);

        // 快速选择按钮
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All"))
        {
            config.targetLanguages = new SystemLanguage[]
            {
                SystemLanguage.Afrikaans,
                SystemLanguage.Arabic,
                SystemLanguage.Basque,
                SystemLanguage.Belarusian,
                SystemLanguage.Bulgarian,
                SystemLanguage.Catalan,
                SystemLanguage.Chinese,
                SystemLanguage.Czech,
                SystemLanguage.Danish,
                SystemLanguage.Dutch,
                SystemLanguage.English,
                SystemLanguage.Estonian,
                SystemLanguage.Faroese,
                SystemLanguage.Finnish,
                SystemLanguage.French,
                SystemLanguage.German,
                SystemLanguage.Greek,
                SystemLanguage.Hebrew,
                SystemLanguage.Hungarian,
                SystemLanguage.Icelandic,
                SystemLanguage.Indonesian,
                SystemLanguage.Italian,
                SystemLanguage.Japanese,
                SystemLanguage.Korean,
                SystemLanguage.Latvian,
                SystemLanguage.Lithuanian,
                SystemLanguage.Norwegian,
                SystemLanguage.Polish,
                SystemLanguage.Portuguese,
                SystemLanguage.Romanian,
                SystemLanguage.Russian,
                SystemLanguage.SerboCroatian,
                SystemLanguage.Slovak,
                SystemLanguage.Slovenian,
                SystemLanguage.Spanish,
                SystemLanguage.Swedish,
                SystemLanguage.Thai,
                SystemLanguage.Turkish,
                SystemLanguage.Ukrainian,
                SystemLanguage.Vietnamese,
                SystemLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional,
                SystemLanguage.Hindi
            };
        }
        if (GUILayout.Button("Asian Languages"))
        {
            config.targetLanguages = new SystemLanguage[]
            {
                SystemLanguage.Chinese,
                SystemLanguage.ChineseSimplified,
                SystemLanguage.ChineseTraditional,
                SystemLanguage.Japanese,
                SystemLanguage.Korean,
                SystemLanguage.Thai,
                SystemLanguage.Vietnamese,
                SystemLanguage.Indonesian,
                SystemLanguage.Hindi
            };
        }
        if (GUILayout.Button("European Languages"))
        {
            config.targetLanguages = new SystemLanguage[]
            {
                SystemLanguage.English,
                SystemLanguage.French,
                SystemLanguage.German,
                SystemLanguage.Spanish,
                SystemLanguage.Italian,
                SystemLanguage.Portuguese,
                SystemLanguage.Dutch,
                SystemLanguage.Swedish,
                SystemLanguage.Norwegian,
                SystemLanguage.Danish,
                SystemLanguage.Finnish,
                SystemLanguage.Polish,
                SystemLanguage.Czech,
                SystemLanguage.Hungarian,
                SystemLanguage.Romanian,
                SystemLanguage.Greek,
                SystemLanguage.Bulgarian,
                SystemLanguage.Ukrainian,
                SystemLanguage.Russian
            };
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Common Languages"))
        {
            config.targetLanguages = new SystemLanguage[]
            {
                SystemLanguage.English,
                SystemLanguage.ChineseSimplified,
                SystemLanguage.Spanish,
                SystemLanguage.French,
                SystemLanguage.Arabic,
                SystemLanguage.Portuguese,
                SystemLanguage.Russian,
                SystemLanguage.Japanese,
                SystemLanguage.German,
                SystemLanguage.Korean
            };
        }
        if (GUILayout.Button("Clear All"))
        {
            config.targetLanguages = new SystemLanguage[0];
        }
        EditorGUILayout.EndHorizontal();

        // 显示当前选择的语言
        string selectedLangs = "Selected: ";
        foreach (var lang in config.targetLanguages)
        {
            selectedLangs += MLT_TranslationConfig.GetLanguageDisplayName(lang) + ", ";
        }
        EditorGUILayout.LabelField(selectedLangs.TrimEnd(',', ' '));

        EditorGUILayout.Space();

        // 高级设置
        EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel);
        config.apiCallDelay = EditorGUILayout.IntSlider("API Call Delay (ms):", config.apiCallDelay, 100, 5000);
        config.maxRetries = EditorGUILayout.IntSlider("Max Retries:", config.maxRetries, 1, 5);

        EditorGUILayout.Space();

        // 保存按钮
        if (GUILayout.Button("Save Configuration", GUILayout.Height(30)))
        {
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Success", "Configuration saved!", "OK");
        }

        EditorGUILayout.EndScrollView();
    }
}