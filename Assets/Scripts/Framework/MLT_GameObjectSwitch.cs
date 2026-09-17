using System;
using System.Collections.Generic;
using UnityEngine;

public class MLT_GameObjectSwitch : MonoBehaviour
{
    [Serializable]
    public class LanguageGameObject
    {
        public SystemLanguage language;
        public GameObject gameObject;
    }
    
    [Header("语言对象列表")]
    public List<LanguageGameObject> languageGameObjects = new List<LanguageGameObject>();
    
    private void Awake()
    {
        Switch();
        EventCenter.Instance.AddEventListener("语言切换", Switch);
    }

    void Switch()
    {
        // 先关闭所有对象
        foreach (var item in languageGameObjects)
        {
            if (item.gameObject != null)
            {
                item.gameObject.SetActive(false);
            }
        }
        
        // 获取当前语言
        SystemLanguage currentLanguage = GameLanguageManager.Instance.CurrentLanguage;
        
        // 查找匹配的语言对象
        foreach (var item in languageGameObjects)
        {
            if (item.language == currentLanguage && item.gameObject != null)
            {
                item.gameObject.SetActive(true);
                return;
            }
        }
        
        // 如果没有找到匹配的，尝试英语作为默认
        foreach (var item in languageGameObjects)
        {
            if (item.language == SystemLanguage.English && item.gameObject != null)
            {
                item.gameObject.SetActive(true);
                return;
            }
        }
        
        // 如果还是没有找到，激活第一个可用的对象
        if (languageGameObjects.Count > 0 && languageGameObjects[0].gameObject != null)
        {
            languageGameObjects[0].gameObject.SetActive(true);
        }
    }

    private void OnDestroy()
    {
        EventCenter.Instance.RemoveEventListener("语言切换", Switch);
    }
}
