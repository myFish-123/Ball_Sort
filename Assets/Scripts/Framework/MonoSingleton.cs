using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class MonoSingleton<T> : MonoBehaviour
    where T : MonoBehaviour
{
    static T m_instance;

    public static T Instance
    {
        get
        {
            return m_instance;
        }
    }

    protected virtual void Awake()
    {
        m_instance = this as T;
    }

    protected virtual void OnDestroy()
    {
        if (m_instance == this)
        {
            m_instance = null;
        }
    }
}