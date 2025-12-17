using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// 单个界面的配置信息（序列化到UIConfig中）
    /// </summary>
    [System.Serializable]
    public class UIFormConfig
    {
        public string FormName;          // 界面名称（必须与脚本名一致）
        public UILayer Layer;            // 所属层级
        public bool IsAddToStack;        // 是否加入层级栈
        public bool IsPermanent;         // 是否永久存在（不回收至对象池）
        public string PrefabPath;        // 预制体路径（根据加载类型填写）
        public UIResourceLoadType LoadType; // 资源加载类型
        public string AssetBundleName;        // AssetBundle包名（仅AssetBundle方式有效）
        public GameObject RefPrefab;//关联资源对象,如果当前对象不为NULL则优先使用，因为已经存在实例了
        public bool RefPrefabsFromScene;//关联的资源对象来自当前场景，随场景销毁而销毁，如果已经加入到缓存池中后则不会随关联场景销毁
    }

    /// <summary>
    /// UI全局配置（ScriptableObject）
    /// </summary>
    [UnityEngine.CreateAssetMenu(fileName = "UIConfig",menuName = "UGUI/UIConfig",order = 1)]
    public class UIConfig : UnityEngine.ScriptableObject
    {
        public UIFormConfig[] FormConfigs; // 所有界面的配置信息

        /// <summary>
        /// 根据界面名称获取配置
        /// </summary>
        public UIFormConfig GetFormConfig(string formName) {
            foreach (var config in FormConfigs) {
                if (config.FormName == formName) {
                    return config;
                }
            }
            UnityEngine.Debug.LogError($"UIConfig: 未找到界面配置 {formName}");
            return null;
        }
    }
}

