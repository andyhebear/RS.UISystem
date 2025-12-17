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

        /// <summary>
        /// 初始化对象池
        /// </summary>
        public UIPool(Transform poolRoot) {
            _poolRoot = poolRoot;
            _poolRoot.name = "UIPool";
            _poolRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// 从对象池获取界面实例
        /// </summary>
        public GameObject GetForm(string formName) {
            if (!_formPools.ContainsKey(formName)) {
                _formPools.Add(formName,CreatePoolGroup(formName));
            }

            Transform poolGroup = _formPools[formName];
            if (poolGroup.childCount > 0) {
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
        public void RecycleForm(GameObject formObj,string formName) {
            if (formObj == null) return;

            if (!_formPools.ContainsKey(formName)) {
                _formPools.Add(formName,CreatePoolGroup(formName));
            }

            Transform poolGroup = _formPools[formName];
            formObj.transform.SetParent(poolGroup);
            formObj.transform.localPosition = Vector3.zero;
            formObj.transform.localRotation = Quaternion.identity;
            formObj.transform.localScale = Vector3.one;
            formObj.SetActive(false);
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
        }
    }
}
