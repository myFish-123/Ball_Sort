using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 特效播放管理器，基于对象池管理粒子特效和帧动画特效的播放和回收
/// </summary>
public class EffectPlayManager : Singleton<EffectPlayManager>
{
    // 存储每个特效对象的定时器ID，用于取消延时回收
    private Dictionary<GameObject, int> m_effectTimers = new Dictionary<GameObject, int>();

    /// <summary>
    /// 播放特效（支持粒子系列和Animator帧动画）
    /// </summary>
    /// <param name="prefab">特效预制体</param>
    /// <param name="position">播放位置</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="parent">父物体（可选）</param>
    /// <returns>特效对象</returns>
    public GameObject PlayEffect(GameObject prefab, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogError("EffectPlayManager.PlayEffect: 特效预制体为空！");
            return null;
        }

        // 使用预制体名称作为对象池的名字
        string poolName = prefab.name;

        // 从对象池获取特效对象
        GameObject effectObj = ObjectPoolManager.Instance.Get(poolName, prefab, parent);
        if (effectObj == null)
        {
            Debug.LogError($"EffectPlayManager.PlayEffect: 无法从对象池获取特效对象 {poolName}！");
            return null;
        }

        // 重置位置和旋转
        effectObj.transform.localPosition = Vector3.zero;
        effectObj.transform.localRotation = Quaternion.identity;

        // 计算最大生命周期
        float maxDuration = 0f;
        bool isLooping = false;

        // 1. 处理粒子系统
        float particleDuration = HandleParticleSystems(effectObj, out bool particleLoop);
        if (particleLoop) isLooping = true;
        if (particleDuration > maxDuration) maxDuration = particleDuration;

        // 2. 处理 Animator 动画
        float animatorDuration = HandleAnimators(effectObj, out bool animatorLoop);
        if (animatorLoop) isLooping = true;
        if (animatorDuration > maxDuration) maxDuration = animatorDuration;

        // 如果之前的定时器存在，移除它
        RemoveTimer(effectObj);

        // 如果是循环特效，不应当自动回收
        if (isLooping)
        {
            Debug.LogWarning($"EffectPlayManager: 特效 {effectObj.name} 包含循环播放组件，不应使用自动回收！");
            // 这里依然返回对象，由外部控制回收
            return effectObj;
        }

        // 设置延时回收
        if (maxDuration > 0)
        {
            int timerId = TimerMgr.Instance.CreateNewTimer(maxDuration, () =>
            {
                RecycleEffect(poolName, effectObj);
            }, false, true, false);

            m_effectTimers[effectObj] = timerId;
        }
        else
        {
            // 如果没有找到有效的时长信息（且非循环），立即回收
            Debug.LogWarning($"EffectPlayManager: 特效 {effectObj.name} 没有有效的时长信息，将立即回收！");
            RecycleEffect(poolName, effectObj);
            return null;
        }

        return effectObj;
    }

    /// <summary>
    /// 处理粒子系统逻辑并返回时长
    /// </summary>
    private float HandleParticleSystems(GameObject effectObj, out bool isLooping)
    {
        float maxDuration = 0f;
        isLooping = false;

        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Play();

            // 检查是否循环
            if (ps.main.loop)
            {
                isLooping = true;
            }

            // 计算时长：Duration + StartLifetime + StartDelay
            float duration = ps.main.duration + ps.main.startLifetime.constantMax + ps.main.startDelay.constantMax;
            if (duration > maxDuration)
            {
                maxDuration = duration;
            }
        }
        return maxDuration;
    }

    /// <summary>
    /// 处理 Animator 逻辑并返回时长
    /// </summary>
    private float HandleAnimators(GameObject effectObj, out bool isLooping)
    {
        float maxDuration = 0f;
        isLooping = false;

        Animator[] animators = effectObj.GetComponentsInChildren<Animator>();
        foreach (Animator anim in animators)
        {
            anim.enabled = true;
            anim.Rebind(); // 重置 Animator 状态
            // anim.Update(0f); // 可选：手动刷新第一帧

            if (anim.runtimeAnimatorController != null)
            {
                // 遍历所有 Clip 查找最长时长
                foreach (AnimationClip clip in anim.runtimeAnimatorController.animationClips)
                {
                    if (clip.isLooping)
                    {
                        isLooping = true;
                    }

                    // 考虑 Animator 的播放速度
                    float speed = Mathf.Abs(anim.speed);
                    if (speed < 0.01f) speed = 1f;

                    float duration = clip.length / speed;
                    if (duration > maxDuration)
                    {
                        maxDuration = duration;
                    }
                }
            }
        }
        return maxDuration;
    }

    /// <summary>
    /// 立即停止并回收特效
    /// </summary>
    /// <param name="poolName">对象池名称</param>
    /// <param name="effectObj">特效对象</param>
    public void StopAndRecycleEffect(string poolName, GameObject effectObj)
    {
        if (effectObj == null) return;

        // 停止粒子
        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 移除定时器
        RemoveTimer(effectObj);

        // 回收到对象池
        RecycleEffect(poolName, effectObj);
    }

    /// <summary>
    /// 内部回收逻辑
    /// </summary>
    private void RecycleEffect(string poolName, GameObject effectObj)
    {
        if (effectObj == null) return;

        // 再次确保移除定时器（防御性编程）
        RemoveTimer(effectObj);

        // 再次确保粒子停止（防止自动回收时还在播放）
        ParticleSystem[] particleSystems = effectObj.GetComponentsInChildren<ParticleSystem>();
        foreach (ParticleSystem ps in particleSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 放回对象池
        ObjectPoolManager.Instance.Release(poolName, effectObj);
    }

    /// <summary>
    /// 移除指定对象的定时器
    /// </summary>
    private void RemoveTimer(GameObject effectObj)
    {
        if (m_effectTimers.ContainsKey(effectObj))
        {
            TimerMgr.Instance.RemoveTimer(m_effectTimers[effectObj]);
            m_effectTimers.Remove(effectObj);
        }
    }

    /// <summary>
    /// 清理所有定时器
    /// </summary>
    private void ClearAllTimers()
    {
        foreach (var timerId in m_effectTimers.Values)
        {
            TimerMgr.Instance.RemoveTimer(timerId);
        }
        m_effectTimers.Clear();
    }

    private void OnDestroy()
    {
        ClearAllTimers();
    }
}
