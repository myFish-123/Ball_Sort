using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对象池管理器，提供对象的存储、获取和清理功能
/// </summary>
public class ObjectPoolManager : DontDestroyMonoSingleton<ObjectPoolManager>
{
    /// <summary>
    /// 自定义对象池实现
    /// </summary>
    private class CustomObjectPool
    {
        private Queue<GameObject> m_pool;
        private int m_maxSize;
        private int m_defaultCapacity;
        private System.Func<GameObject> m_createFunc;
        private System.Action<GameObject> m_actionOnGet;
        private System.Action<GameObject> m_actionOnRelease;
        private System.Action<GameObject> m_actionOnDestroy;
        
        public CustomObjectPool(
            System.Func<GameObject> createFunc,
            System.Action<GameObject> actionOnGet,
            System.Action<GameObject> actionOnRelease,
            System.Action<GameObject> actionOnDestroy,
            int defaultCapacity,
            int maxSize)
        {
            m_createFunc = createFunc;
            m_actionOnGet = actionOnGet;
            m_actionOnRelease = actionOnRelease;
            m_actionOnDestroy = actionOnDestroy;
            m_defaultCapacity = defaultCapacity;
            m_maxSize = maxSize;
            m_pool = new Queue<GameObject>(defaultCapacity);
        }
        
        public GameObject Get()
        {
            GameObject obj = null;
            
            if (m_pool.Count > 0)
            {
                obj = m_pool.Dequeue();
            }
            else
            {
                obj = m_createFunc();
            }
            
            if (m_actionOnGet != null)
            {
                m_actionOnGet(obj);
            }
            
            return obj;
        }
        
        public void Release(GameObject obj)
        {
            if (obj == null)
                return;
                
            if (m_actionOnRelease != null)
            {
                m_actionOnRelease(obj);
            }
            
            if (m_pool.Count < m_maxSize)
            {
                m_pool.Enqueue(obj);
            }
            else
            {
                if (m_actionOnDestroy != null)
                {
                    m_actionOnDestroy(obj);
                }
            }
        }
        
        public void Clear()
        {
            while (m_pool.Count > 0)
            {
                GameObject obj = m_pool.Dequeue();
                if (m_actionOnDestroy != null)
                {
                    m_actionOnDestroy(obj);
                }
            }
        }
        
        public void Dispose()
        {
            Clear();
        }
    }
    
    /// <summary>
    /// 对象池信息
    /// </summary>
    private class PoolInfo
    {
        public GameObject prefab;
        public Transform poolRoot;
        public CustomObjectPool pool;
    }
    
    // 所有对象池的字典，key为池的名字
    private Dictionary<string, PoolInfo> m_pools = new Dictionary<string, PoolInfo>();
    
    // 对象池的根节点
    private Transform m_poolRoot;
    
    protected void Awake()
    {
        InitPoolRoot();
    }
    
    /// <summary>
    /// 初始化对象池根节点
    /// </summary>
    private void InitPoolRoot()
    {
        GameObject poolRootObj = new GameObject("[ObjectPoolRoot]");
        poolRootObj.transform.SetParent(transform);
        m_poolRoot = poolRootObj.transform;
    }
    
    /// <summary>
    /// 从对象池获取对象
    /// </summary>
    /// <param name="poolName">池的名字</param>
    /// <param name="prefab">预制体</param>
    /// <param name="parent">父物体</param>
    /// <returns>获取到的对象</returns>
    public GameObject Get(string poolName, GameObject prefab, Transform parent = null)
    {
        if (string.IsNullOrEmpty(poolName) || prefab == null)
        {
            Debug.LogError("ObjectPoolManager.Get: 参数无效！");
            return null;
        }
        
        // 确保对象池存在
        EnsurePoolExists(poolName, prefab);
        
        // 从对象池获取对象
        PoolInfo poolInfo = m_pools[poolName];
        GameObject obj = poolInfo.pool.Get();
        
        // 设置父物体
        if (parent != null)
        {
            obj.transform.SetParent(parent);
        }
        
        // 重置位置和旋转
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        obj.transform.localScale = Vector3.one;
        
        return obj;
    }
    
    /// <summary>
    /// 将对象放回对象池
    /// </summary>
    /// <param name="poolName">池的名字</param>
    /// <param name="obj">要放回的对象</param>
    public void Release(string poolName, GameObject obj)
    {
        if (string.IsNullOrEmpty(poolName) || obj == null)
        {
            Debug.LogError("ObjectPoolManager.Release: 参数无效！");
            return;
        }
        
        if (!m_pools.ContainsKey(poolName))
        {
            Debug.LogError($"ObjectPoolManager.Release: 对象池 {poolName} 不存在！");
            Destroy(obj);
            return;
        }
        
        PoolInfo poolInfo = m_pools[poolName];
        poolInfo.pool.Release(obj);
    }

    public void Prewarm(string poolName, GameObject prefab, int count)
    {
        if (string.IsNullOrEmpty(poolName) || prefab == null || count <= 0)
        {
            return;
        }

        EnsurePoolExists(poolName, prefab);

        PoolInfo poolInfo = m_pools[poolName];
        List<GameObject> tempObjects = new List<GameObject>(count);
        for (int i = 0; i < count; i++)
        {
            tempObjects.Add(poolInfo.pool.Get());
        }

        for (int i = 0; i < tempObjects.Count; i++)
        {
            poolInfo.pool.Release(tempObjects[i]);
        }
    }
    
    /// <summary>
    /// 清理指定的对象池
    /// </summary>
    /// <param name="poolName">池的名字</param>
    public void ClearPool(string poolName)
    {
        if (string.IsNullOrEmpty(poolName))
        {
            Debug.LogError("ObjectPoolManager.ClearPool: 池名称无效！");
            return;
        }
        
        if (!m_pools.ContainsKey(poolName))
        {
            Debug.LogWarning($"ObjectPoolManager.ClearPool: 对象池 {poolName} 不存在！");
            return;
        }
        
        PoolInfo poolInfo = m_pools[poolName];
        poolInfo.pool.Clear();
    }
    
    /// <summary>
    /// 清理所有对象池
    /// </summary>
    public void ClearAllPools()
    {
        foreach (var kvp in m_pools)
        {
            kvp.Value.pool.Clear();
        }
    }
    
    /// <summary>
    /// 销毁指定的对象池
    /// </summary>
    /// <param name="poolName">池的名字</param>
    public void DestroyPool(string poolName)
    {
        if (string.IsNullOrEmpty(poolName))
        {
            Debug.LogError("ObjectPoolManager.DestroyPool: 池名称无效！");
            return;
        }
        
        if (!m_pools.ContainsKey(poolName))
        {
            Debug.LogWarning($"ObjectPoolManager.DestroyPool: 对象池 {poolName} 不存在！");
            return;
        }
        
        PoolInfo poolInfo = m_pools[poolName];
        poolInfo.pool.Dispose();
        
        // 销毁池的根节点
        if (poolInfo.poolRoot != null)
        {
            Destroy(poolInfo.poolRoot.gameObject);
        }
        
        m_pools.Remove(poolName);
    }
    
    /// <summary>
    /// 销毁所有对象池
    /// </summary>
    public void DestroyAllPools()
    {
        List<string> poolNames = new List<string>(m_pools.Keys);
        foreach (string poolName in poolNames)
        {
            DestroyPool(poolName);
        }
        m_pools.Clear();
    }
    
    /// <summary>
    /// 确保对象池存在，如果不存在则创建
    /// </summary>
    /// <param name="poolName">池的名字</param>
    /// <param name="prefab">预制体</param>
    private void EnsurePoolExists(string poolName, GameObject prefab)
    {
        if (m_pools.ContainsKey(poolName))
        {
            return;
        }
        
        // 创建该池的根节点
        GameObject poolRoot = new GameObject($"Pool_{poolName}");
        poolRoot.transform.SetParent(m_poolRoot);
        
        PoolInfo poolInfo = new PoolInfo();
        poolInfo.prefab = prefab;
        poolInfo.poolRoot = poolRoot.transform;
        
        // 创建自定义对象池
        poolInfo.pool = new CustomObjectPool(
            createFunc: () => Instantiate(prefab),
            actionOnGet: (obj) => obj.SetActive(true),
            actionOnRelease: (obj) => 
            {
                obj.SetActive(false);
                obj.transform.SetParent(poolInfo.poolRoot);
            },
            actionOnDestroy: (obj) => Destroy(obj),
            defaultCapacity: 10,
            maxSize: 100
        );
        
        m_pools.Add(poolName, poolInfo);
    }
    

    

    
    private void OnDestroy()
    {
        DestroyAllPools();
    }
}
