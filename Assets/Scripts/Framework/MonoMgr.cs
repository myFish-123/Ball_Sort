using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Internal;
using System.Linq;
using UnityEditor;

/// <summary>
/// 声明生命周期函数
/// 事件
/// 协程
/// </summary>
public class MonoMgr : DontDestroyMonoSingleton<MonoMgr>
{
    Action updateAction;

    private void Update()
    {
        updateAction?.Invoke();
    }

    /// <summary>
    /// 给外部提供的 添加帧更新事件的函数
    /// </summary>
    /// <param name="fun"></param>
    public void AddUpdateListener(Action fun)
    {
        updateAction += fun;
    }
    /// <summary>
    /// 提供给外部 用于移除帧更新事件函数
    /// </summary>
    /// <param name="fun"></param>
    public void RemoveUpdateListener(Action fun)
    {
        updateAction -= fun;
    }

    public new Coroutine StartCoroutine(IEnumerator routine)
    {
        return base.StartCoroutine(routine);
    }

    public new void StopCoroutine(IEnumerator routine)
    {
        base.StopCoroutine(routine);
    }

    public new void StopCoroutine(Coroutine routine)
    {
        base.StopCoroutine(routine);
    }
}
