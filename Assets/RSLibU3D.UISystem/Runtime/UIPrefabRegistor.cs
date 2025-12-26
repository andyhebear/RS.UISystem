using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// 用于场景绑定UIForm界面注册
    /// </summary>
    public class UIFormPrefabRegistor : MonoBehaviour
    {
        [SerializeField] private List<UIFormPrefabInfo> _uiPrefabs = new List<UIFormPrefabInfo>();
        private Dictionary<string,GameObject> _prefabDictionary = new Dictionary<string,GameObject>();
        private static UIFormPrefabRegistor _instance;
        /// <summary>
        /// 不会动态创建，只会在场景自己绑定才有，在场景重新加载后重新加载
        /// </summary>
        public static UIFormPrefabRegistor Instance => _instance;
        private void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            // 初始化预设字典
            InitializePrefabs();
        }
        private void OnDestroy() {
            if (_instance == this) {
                _instance = null;
            }
        }
        private void InitializePrefabs() {
            _prefabDictionary.Clear();
            foreach (var prefabInfo in _uiPrefabs) {
                if (prefabInfo.UIFormPrefab != null && !string.IsNullOrEmpty(prefabInfo.UIFormName)) {
                    _prefabDictionary[prefabInfo.UIFormName] = prefabInfo.UIFormPrefab;
                    UIFormConfig info = new UIFormConfig() {
                        FormName = prefabInfo.UIFormName,
                        Lifecycle = UIFormLifecycle.AutoDestroy,
                        LoadType = UIResourceLoadType.PrefabRef,
                        IsAddToStack = prefabInfo.IsAddToStack,
                        RefPrefab = prefabInfo.UIFormPrefab,
                        RefPrefabsFromScene = prefabInfo.PrefabIsFromScene,
                        Layer = prefabInfo.Layer,
                        MaskType = prefabInfo.MaskType,
                        //MaskColor = prefabInfo.MaskColor,
                    };
                    UIManager.Instance.RegisterUIFormConfig(info);
                }
            }
        }
        /// <summary>
        /// 注册UI预设，如果已经存在则更新
        /// </summary>
        public void RegisterPrefab(string name,GameObject prefab,UILayer layer,bool prefabIsFromScene, bool isAddToStack   ,UIMaskType masktype) {
            if (!string.IsNullOrEmpty(name) && prefab != null) {
                _prefabDictionary[name] = prefab;

                // 更新列表
                bool found = false;
                foreach (var prefabInfo in _uiPrefabs) {
                    if (prefabInfo.UIFormName == name) {
                        found = true;
                        //
                        prefabInfo.UIFormPrefab = prefab;
                        prefabInfo.Layer = layer;
                        prefabInfo.PrefabIsFromScene = prefabIsFromScene;
                        //
                        UIFormConfig info = new UIFormConfig() {
                            FormName = name,
                            Lifecycle = UIFormLifecycle.AutoDestroy,
                            LoadType = UIResourceLoadType.PrefabRef,
                             IsAddToStack=isAddToStack,
                            RefPrefab = prefab,
                            RefPrefabsFromScene = prefabIsFromScene,
                            Layer = layer,
                            MaskType = prefabInfo.MaskType,
                            //MaskColor = prefabInfo.MaskColor,
                        };
                        UIManager.Instance.RegisterUIFormConfig(info);
                        break;
                    }
                }

                if (!found) {
                    _uiPrefabs.Add(new UIFormPrefabInfo {
                        UIFormName = name,
                        UIFormPrefab = prefab,
                        Layer = layer,
                        PrefabIsFromScene = true
                    });
                    UIFormConfig info = new UIFormConfig() {
                        FormName = name,
                        Lifecycle = UIFormLifecycle.AutoDestroy,
                        LoadType = UIResourceLoadType.PrefabRef,
                        IsAddToStack=isAddToStack,
                        RefPrefab = prefab,
                        RefPrefabsFromScene = prefabIsFromScene,
                        Layer = layer,
                        MaskType = masktype,
                        //MaskColor = maskColor,
                    };
                    UIManager.Instance.RegisterUIFormConfig(info);
                }
            }
        }

        /// <summary>
        /// 获取动态注册的UI预设
        /// </summary>
        public GameObject GetPrefab(string name) {
            if (_prefabDictionary.TryGetValue(name,out GameObject prefab)) {
                return prefab;
            }
            return null;
        }

        /// <summary>
        /// 检查动态注册的预设是否存在
        /// </summary>
        public bool HasPrefab(string name) {
            return _prefabDictionary.ContainsKey(name);
        }

        /// <summary>
        /// 获取所有动态注册的预设名称
        /// </summary>
        public List<string> GetAllPrefabNames() {
            return new List<string>(_prefabDictionary.Keys);
        }
    }
    /// <summary>
    /// UI预设信息，来自于场景中预设资源引用
    /// </summary>
    [System.Serializable]
    public class UIFormPrefabInfo
    {
        public string UIFormName;
        public GameObject UIFormPrefab;
        public UILayer Layer;
        public bool IsAddToStack;
        public bool PrefabIsFromScene;
        public UIMaskType MaskType = UIMaskType.None;   // 遮罩类型
        //public Color MaskColor = new Color(0,0,0,0.5f); // 遮罩颜色
    }
}
