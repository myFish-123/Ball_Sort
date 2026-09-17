using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameLanguageManager : Singleton<GameLanguageManager>
{
    // 当前语言，保证只计算一次
    private SystemLanguage? currentLanguage;

    public SystemLanguage CurrentLanguage
    {
        get
        {
            if (currentLanguage == null)
            {
                // 直接使用系统语言
                currentLanguage = Application.systemLanguage;
            }
            return currentLanguage.Value;
        }
        set
        {
            currentLanguage = value;
            // 触发语言切换事件
            EventCenter.Instance.EventTrigger("语言切换");
        }
    }
}