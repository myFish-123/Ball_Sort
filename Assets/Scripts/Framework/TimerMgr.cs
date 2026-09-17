using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TimerMgr : Singleton<TimerMgr>
{
    public delegate void CompleteEvent();
    class TimerData
    {
        public int id;
        public CompleteEvent onCompleted; //完成回调事件
        public float time;   // 所需时间或帧数
        public float targetTime;   // 目标时间（如果是帧数则无效）
        public bool isIgnoreTimeScale;  // 是否忽略时间速率
        public bool isLoop;     //是否重复
        public int executeCount;   //循环次数
        public bool isSecond;   //是否以秒为单位 否则为帧数
        public Coroutine coroutine; //标记协程
    }

    int timerId = 0;
    Dictionary<int, TimerData> timerDict = new Dictionary<int, TimerData>();
    List<int> tempRemoveTimer = new List<int>();

    public TimerMgr()
    {
        //绑定Update
        MonoMgr.Instance.AddUpdateListener(UpdateTime);
    }

    void UpdateTime()
    {
        for(int i = timerDict.Count - 1; i >= 0 ; i--)
        {
            TimerData timeData = timerDict.ElementAt(i).Value;
            if (!timeData.isSecond)
            {
                continue;
            }

            float nowTime = TimeNow(timeData.isIgnoreTimeScale);
            if (nowTime >= timeData.targetTime)
            {
                timeData.onCompleted?.Invoke();
                timeData.executeCount -= 1;
                if (timeData.isLoop)
                {
                    timeData.targetTime = nowTime + timeData.time;
                }
                else
                {
                    if(timeData.executeCount <= 0)
                    {
                        tempRemoveTimer.Add(timeData.id);
                    }
                    else
                    {
                        timeData.targetTime = nowTime + timeData.time;
                    }
                }
            }
        }
        for(int i = 0; i < tempRemoveTimer.Count; i++)
        {
            RemoveTimer(tempRemoveTimer[i]);
        }
        tempRemoveTimer.Clear();
    }

    // 获取当前时间
    float TimeNow(bool isIgnoreTimeScale)
    {
        return isIgnoreTimeScale ? Time.realtimeSinceStartup : Time.time;
    }

    /// <summary>
    /// 创建一个新的定时器（支持循环）
    /// </summary>
    /// <param name="time">延迟时间（秒）</param>
    /// <param name="onCompleted">结束回调</param>
    /// <param name="isLoop">是否循环,false则只执行一次</param>
    /// <param name="isSecond">秒/帧数</param>
    /// <param name="isIgnoreTimeScale">是否受TimeScale影响</param>
    /// <returns></returns>
    public int CreateNewTimer(float time, CompleteEvent onCompleted, bool isLoop = false, bool isSecond = true,bool isIgnoreTimeScale = false)
    {
        timerId += 1;
        timerDict.Add(timerId, new TimerData()
        {
            id = timerId,
            onCompleted = onCompleted,
            time = time,
            targetTime = time + TimeNow(isIgnoreTimeScale),
            isIgnoreTimeScale = isIgnoreTimeScale,
            isLoop = isLoop,
            executeCount = 1,
            isSecond = isSecond
        });

        // 如果不是以秒为单位，则执行延迟帧数
        if (!isSecond)
        {
            timerDict[timerId].coroutine = MonoMgr.Instance.StartCoroutine(DelayedExecution(timerDict[timerId]));
        }
        return timerId;
    }

    /// <summary>
    /// 创建一个新的定时器（支持执行次数）
    /// </summary>
    /// <param name="time">延迟时间（秒）</param>
    /// <param name="onCompleted">结束回调</param>
    /// <param name="count">执行次数</param>
    /// <param name="isSecond">秒/帧数</param>
    /// <param name="isIgnoreTimeScale">是否受TimeScale影响</param>
    /// <returns></returns>
    public int CreateNewCountTimer(float time, CompleteEvent onCompleted, int count, bool isSecond = true, bool isIgnoreTimeScale = false)
    {
        timerId += 1;
        timerDict.Add(timerId, new TimerData()
        {
            id = timerId,
            onCompleted = onCompleted,
            time = time,
            targetTime = time + TimeNow(isIgnoreTimeScale),
            isIgnoreTimeScale = isIgnoreTimeScale,
            isLoop = false,
            executeCount = count,
            isSecond = isSecond
        });

        // 如果不是以秒为单位，则执行延迟帧数
        if (!isSecond)
        {
            timerDict[timerId].coroutine = MonoMgr.Instance.StartCoroutine(DelayedExecution(timerDict[timerId]));
        }
        return timerId;
    }

    /// <summary>
    /// 移除指定定时器
    /// </summary>
    public void RemoveTimer(int id)
    {
        if (timerDict.ContainsKey(id))
        {
            TimerData data = timerDict[id];
            if (!data.isSecond)
            {
                MonoMgr.Instance.StopCoroutine(data.coroutine);
            }
            data = null;
            timerDict.Remove(id);
        }
    }

    /// <summary>
    /// 清除所有定时器
    /// </summary>
    public void RemoveAllTimer()
    {
        for (int i = timerDict.Count - 1; i >= 0; i--)
        {
            int id = timerDict.ElementAt(i).Key;
            RemoveTimer(id);
        }
    }

    IEnumerator DelayedExecution(TimerData data)
    {
        // 等待指定的帧数
        for (int i = 0; i < data.time; i++)
        {
            yield return null; // 等待下一帧
        }
        // 执行动作
        data.onCompleted?.Invoke();
        data.executeCount -= 1;
        if (data.isLoop)
        {
            // 防止完成事件中移除了定时器，不判断会导致依然执行协程
            if(data != null && timerDict.ContainsKey(data.id))
            {
                data.coroutine = MonoMgr.Instance.StartCoroutine(DelayedExecution(data));
            }
        }
        else
        {
            if(data.executeCount <= 0)
            {
                RemoveTimer(data.id);
            }
            else
            {
                if (data != null && timerDict.ContainsKey(data.id))
                {
                    data.coroutine = MonoMgr.Instance.StartCoroutine(DelayedExecution(data));
                }
            }
        }
    }
}