using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// UI界面生命周期策略
    /// </summary>
    public enum UIFormLifecycle
    {      
        /// <summary>自动回收，关闭后存入对象池复用</summary>
        AutoRecycle,
        /// <summary>自动销毁，关闭后直接从内存中移除</summary>
        AutoDestroy,
        /// <summary>常驻界面，关闭后不回收、不销毁</summary>
        IsPermanent,
    }
    /// <summary>
    /// 单个界面的配置信息（序列化到UIConfig中）
    /// </summary>
    [System.Serializable]
    public class UIFormConfig
    {
        public string FormName;          // 界面名称（必须与脚本名一致）
        public UILayer Layer;            // 所属层级
        public bool IsAddToStack;        // 是否加入层级栈 
        public UIMaskType MaskType = UIMaskType.None;   // 遮罩类型
        public bool maskClickClose;        // 点击遮罩是否关闭
        //public Color MaskColor = new Color(0,0,0,0.5f); // 遮罩颜色
        /// <summary>
        /// 是否永久存在（不回收至对象池）/自动回收/自动销毁
        /// </summary>
        public UIFormLifecycle Lifecycle;
        public string PrefabPath;        //预制体路径（Resources/Addressables Key）
        public UIResourceLoadType LoadType; // 资源加载类型
        public string ABPackageName;        //YooAsset中指定PackageName(资源包名称)
        public string AssetBundleName;        // AssetBundle包名（仅AssetBundle方式有效）
        public GameObject RefPrefab;//关联资源对象,如果当前对象不为NULL则优先使用，因为已经存在实例了
        public bool RefPrefabsFromScene;//关联的资源对象来自当前场景，随场景销毁而销毁，如果已经加入到缓存池中后则不会随关联场景销毁
        public bool ShowLoading = false;  // 异步加载时是否显示等待界面
    }

    /// <summary>
    /// UI全局配置（ScriptableObject）
    /// </summary>
    [UnityEngine.CreateAssetMenu(fileName = "UIConfig",menuName = "UISystem/UIConfig",order = 1)]
    public class UIConfig : UnityEngine.ScriptableObject
    {
        [Header("内置界面配置列表")]
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

