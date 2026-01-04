using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;
namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// UI对象池（复用界面实例，减少GC）
    /// </summary>
    public class UIPool
    {
        private readonly Transform _poolRoot; // 对象池根节点
        private readonly Dictionary<string,Transform> _formPools = new(); // 每个界面的缓存池
        private readonly Dictionary<string, int> _formPoolCapacities = new(); // 每个界面的池容量配置
        private  int _defaultPoolCapacity = 5; // 默认池容量

        /// <summary>
        /// 初始化对象池
        /// </summary>
        public UIPool(Transform poolRoot) {
            _poolRoot = poolRoot;
            _poolRoot.name = "UIPool";
            _poolRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 设置默认池容量
        /// </summary>
        /// <param name="capacity">默认容量值</param>
        public void SetDefaultPoolCapacity(int capacity)
        {
            if (capacity > 0)
            {
                _defaultPoolCapacity = capacity;
            }
        }

        /// <summary>
        /// 设置特定界面的池容量
        /// </summary>
        /// <param name="formName">界面名称</param>
        /// <param name="capacity">容量值</param>
        public void SetFormPoolCapacity(string formName, int capacity)
        {
            if (capacity > 0)
            {
                _formPoolCapacities[formName] = capacity;
            }
        }

        /// <summary>
        /// 从对象池获取界面实例
        /// </summary>
        public GameObject GetForm(string formName) {
            if (!_formPools.ContainsKey(formName)) {
                _formPools.Add(formName, CreatePoolGroup(formName));
            }

            Transform poolGroup = _formPools[formName];
            if (poolGroup.childCount > 0) {
                // 获取第一个子对象（LRU策略：最先进入的最先被复用）
                GameObject formObj = poolGroup.GetChild(0).gameObject;
                formObj.transform.SetParent(null);
                formObj.SetActive(false);
                return formObj;
            }

            return null; // 无缓存实例，返回null（需重新加载）
        }

        /// <summary>
        /// 回收界面实例到对象池
        /// </summary>
        public void RecycleForm(GameObject formObj, string formName) {
            if (formObj == null) return;

            if (!_formPools.ContainsKey(formName)) {
                _formPools.Add(formName, CreatePoolGroup(formName));
            }
        
            Transform poolGroup = _formPools[formName];
            
            // 检查容量限制
            int maxCapacity = GetPoolCapacity(formName);
            if (poolGroup.childCount >= maxCapacity)
            {
                // 超过容量，销毁多余的对象（销毁最旧的，即第一个）
                Transform oldestObj = poolGroup.GetChild(0);
                GameObject.Destroy(oldestObj.gameObject);
            }
            
            // 回收对象
            formObj.transform.SetParent(poolGroup);
            formObj.transform.localPosition = Vector3.zero;
            formObj.transform.localRotation = Quaternion.identity;
            formObj.transform.localScale = Vector3.one;
            formObj.SetActive(false);
        }

        /// <summary>
        /// 获取指定界面的池容量
        /// </summary>
        private int GetPoolCapacity(string formName)
        {
            if (_formPoolCapacities.TryGetValue(formName, out int capacity))
            {
                return capacity;
            }
            return _defaultPoolCapacity;
        }

        /// <summary>
        /// 清理指定界面的对象池
        /// </summary>
        /// <param name="formName">界面名称</param>
        public void ClearFormPool(string formName)
        {
            if (_formPools.TryGetValue(formName, out Transform poolGroup))
            {
                foreach (Transform child in poolGroup)
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 创建单个界面的缓存组
        /// </summary>
        private Transform CreatePoolGroup(string formName) {
            GameObject groupObj = new GameObject(formName + "Pool");
            groupObj.transform.SetParent(_poolRoot);
            groupObj.transform.localPosition = Vector3.zero;
            groupObj.transform.localScale = Vector3.one;
            return groupObj.transform;
        }

        /// <summary>
        /// 清空对象池（仅在场景切换/游戏退出时使用）
        /// </summary>
        public void Clear() {
            foreach (var poolGroup in _formPools.Values) {
                foreach (Transform child in poolGroup) {
                    GameObject.Destroy(child.gameObject);
                }
            }
            _formPools.Clear();
            _formPoolCapacities.Clear();
        }
        
        /// <summary>
        /// 根据内存压力进行清理，移除一定比例的缓存对象
        /// </summary>
        /// <param name="clearPercentage">清理比例（0-1之间）</param>
        public void ClearByMemoryPressure(float clearPercentage = 0.5f)
        {
            if (clearPercentage <= 0 || clearPercentage > 1)
                clearPercentage = 0.5f;
                
            foreach (var poolGroup in _formPools.Values)
            {
                int childCount = poolGroup.childCount;
                int clearCount = Mathf.FloorToInt(childCount * clearPercentage);
                
                for (int i = 0; i < clearCount && poolGroup.childCount > 0; i++)
                {
                    // 移除最旧的对象（索引0）
                    GameObject.Destroy(poolGroup.GetChild(0).gameObject);
                }
            }
        }
    }
}
