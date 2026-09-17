using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class MLT_ImageSwitch : MonoBehaviour
{
    [Serializable]
    public class LanguageImageSettings
    {
        [Tooltip("这条图片配置对应的系统语言。")]
        public SystemLanguage language;
        [Header("图像设置")]
        [Tooltip("该语言下显示的 Sprite。")]
        public Sprite sprite;
        [Tooltip("是否保持图片原始宽高比。")]
        public bool preserveAspect = false;
        [Tooltip("Image 组件的显示类型，例如 Simple、Sliced、Filled。")]
        public Image.Type imageType = Image.Type.Simple;
        
        [Header("RectTransform设置（可选）")]
        [Tooltip("是否在切换到该语言时覆盖 RectTransform 的尺寸。")]
        public bool overrideSize = false;
        [Tooltip("启用覆盖尺寸时使用的 sizeDelta。")]
        public Vector2 sizeDelta;
        
        [Header("材质设置（可选）")]
        [Tooltip("该语言图片需要使用的材质；为空时保留当前材质。")]
        public Material material;
    }
    
    [Header("语言图像配置")]
    [Tooltip("按语言配置的图片列表；找不到系统语言时优先回退到英语，再回退到第一条。")]
    public List<LanguageImageSettings> languageImages = new List<LanguageImageSettings>();
    
    private Image image;
    private RectTransform rectTransform;
    
    private void Awake()
    {
        image = GetComponent<Image>();
        rectTransform = GetComponent<RectTransform>();
        Switch();
        EventCenter.Instance.AddEventListener("语言切换", Switch);
    }
    
    void Switch()
    {
        // 获取当前系统语言
        SystemLanguage currentLang = GameLanguageManager.Instance.CurrentLanguage;
        
        // 查找对应语言的图像设置
        foreach (var langImage in languageImages)
        {
            if (langImage.language == currentLang)
            {
                ApplyImageSettings(langImage);
                return;
            }
        }
        
        // 如果没有找到对应语言，尝试使用英语
        foreach (var langImage in languageImages)
        {
            if (langImage.language == SystemLanguage.English)
            {
                ApplyImageSettings(langImage);
                return;
            }
        }
        
        // 如果连英语都没有，使用第一个可用的设置
        if (languageImages.Count > 0)
        {
            ApplyImageSettings(languageImages[0]);
        }
    }
    
    private void ApplyImageSettings(LanguageImageSettings settings)
    {
        if (settings == null) return;
        
        // 应用精灵图
        if (settings.sprite != null)
        {
            image.sprite = settings.sprite;
        }
        
        // 应用图像类型
        image.type = settings.imageType;
        
        // 应用保持宽高比
        image.preserveAspect = settings.preserveAspect;
        
        // 应用材质
        if (settings.material != null)
        {
            image.material = settings.material;
        }
        
        // 应用尺寸
        if (settings.overrideSize && rectTransform != null)
        {
            rectTransform.sizeDelta = settings.sizeDelta;
        }
    }
    
    private void OnDestroy()
    {
        EventCenter.Instance.RemoveEventListener("语言切换", Switch);
    }
}
