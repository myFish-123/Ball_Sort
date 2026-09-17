using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class MLT_SpriteSwitch : MonoBehaviour
{
    [Serializable]
    public class LanguageSprite
    {
        public SystemLanguage language;
        public Sprite sprite;
    }
    
    [Header("语言精灵图配置")]
    public List<LanguageSprite> languageSprites = new List<LanguageSprite>();
    
    private Image image;
    
    private void Awake()
    {
        image = GetComponent<Image>();
        Switch();
        EventCenter.Instance.AddEventListener("语言切换", Switch);
    }
    
    void Switch()
    {
        // 获取当前系统语言
        SystemLanguage currentLang = GameLanguageManager.Instance.CurrentLanguage;
        
        // 查找对应语言的精灵图
        foreach (var langSprite in languageSprites)
        {
            if (langSprite.language == currentLang && langSprite.sprite != null)
            {
                image.sprite = langSprite.sprite;
                return;
            }
        }
        
        // 如果没有找到对应语言，尝试使用英语
        foreach (var langSprite in languageSprites)
        {
            if (langSprite.language == SystemLanguage.English && langSprite.sprite != null)
            {
                image.sprite = langSprite.sprite;
                return;
            }
        }
        
        // 如果连英语都没有，使用第一个可用的精灵图
        if (languageSprites.Count > 0 && languageSprites[0].sprite != null)
        {
            image.sprite = languageSprites[0].sprite;
        }
    }
    
    private void OnDestroy()
    {
        EventCenter.Instance.RemoveEventListener("语言切换", Switch);
    }
}