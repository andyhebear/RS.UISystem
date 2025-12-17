using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ADDRESSABLES_SUPPORT
//#define ADDRESSABLES_ENABLED
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif
#if YOOASSET_SUPPORT
using YooAsset;
#endif
namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// UI核心管理器（单例）
    /// 负责界面加载、显示、关闭、层级管理、资源加载等核心逻辑
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }
        public Camera getUICamera() { return _UICamera; }
        public Color getBackSideColor() { return Color.black; }
        public Vector2 getDesignResolution() { return new Vector2(1920,1080); }     
   
        /// <summary>
        /// 动态注册预设资源路径
        /// </summary>
        private Dictionary<string,UIFormConfig> _uiFormConfigs = new Dictionary<string,UIFormConfig>();   
        [Header("基础配置")]
        [SerializeField] private UIConfig _uiConfig;
       
        [SerializeField] private Transform _UILayers; // 绑定UILayers节点
        [SerializeField] private Camera _UICamera; // 绑定UICamera节点
        [SerializeField] private UIResourceLoadType _defaultLoadType = UIResourceLoadType.Resources;

        [Header("适配配置")]
        [SerializeField] private AdaptMode _defaultAdaptMode = AdaptMode.BlackBars;
        [Header("SafeArea配置")]
        [SerializeField] private bool _enableSafeArea = true; // 是否启用SafeArea适配
        // 核心数据结构
        private readonly Dictionary<UILayer,Stack<string>> _layerFormStacks = new Dictionary<UILayer,Stack<string>>(); // 层级独立栈
        private readonly Dictionary<string,UIForm> _loadedForms = new Dictionary<string,UIForm>(); // 已加载界面缓存
        private readonly Dictionary<UILayer,Transform> _layerParents = new Dictionary<UILayer,Transform>(); // 层级父节点缓存
        private readonly Dictionary<UIResourceLoadType,IUIResourceLoader> _resourceLoaders = new Dictionary<UIResourceLoadType,IUIResourceLoader>(); // 资源加载器缓存

        private UIPool _uiPool;
        private UIRootAdapter _uiRootAdapter;
        private IUIResourceLoader _currentResourceLoader;

        private void Awake() {
            // 单例初始化
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitUICamera();
            // 初始化层级父节点
            InitLayerParents();

            // 初始化层级栈
            InitLayerStacks();

            // 初始化对象池
            var uiPoolGO = new GameObject("UIPool");
            uiPoolGO.transform.SetParent(this.transform);
            _uiPool = new UIPool(uiPoolGO.transform);

            // 初始化适配管理器
            InitUIRootAdapter();

            // 初始化资源加载器
            RegisterResourceLoaders();
            _currentResourceLoader = _resourceLoaders[_defaultLoadType];

            // 初始化EventSystem
            InitEventSystem();
        }
        private void Start() {
            ShowForm<UINotifyLayer>();
        }
        private void InitUICamera() {
            if (_UICamera == null) {
                var co = this.transform.Find("UICamera");
                if (co == null) {
                    co = new GameObject("UICamera",typeof(Camera)).transform;
                    co.SetParent(this.transform);
                }
                _UICamera = co.GetComponent<Camera>();
                if (_UICamera == null) {
                    _UICamera = co.gameObject.AddComponent<Camera>();
                }
            }
        }
        /// <summary>
        /// 初始化层级父节点（绑定UILayers下的子节点）
        /// </summary>
        private void InitLayerParents() {
            if (_UILayers == null) {
                _UILayers = this.transform.Find("UILayers");
                if (_UILayers == null) {
                    GameObject ul = new GameObject("UILayers",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                    ul.layer = LayerMask.NameToLayer("UI");
                    ul.transform.SetParent(this.transform);
                    ul.transform.localPosition = Vector3.zero;
                    _UILayers = ul.transform;
                }
            }

            foreach (UILayer layer in Enum.GetValues(typeof(UILayer))) {
                string layerName = layer.ToString();
                Transform child = _UILayers.Find(layerName);
                if (child != null) {
                    _layerParents.Add(layer,child);
                }
                else {
                    //Debug.LogError($"UIManager: UILayers下未找到层级节点 {layerName}");
                    GameObject lay = new GameObject(layerName,typeof(RectTransform));
                    lay.layer = LayerMask.NameToLayer("UI");
                    lay.transform.SetParent(_UILayers);
                    child = lay.transform;
                    child.localPosition = new Vector3(0f,0f,((int)layer + 1f) * 100f);
                    _layerParents.Add(layer,child);
                }
                RectTransform rectTrans = child.GetComponent<RectTransform>();
                if (rectTrans != null) {
                    rectTrans.anchorMin = Vector2.zero;
                    rectTrans.anchorMax = Vector2.one;
                    rectTrans.offsetMin = Vector2.zero;
                    rectTrans.offsetMax = Vector2.zero;
                    rectTrans.localScale = Vector3.one;
                }
                UISafeArea sa = child.GetComponent<UISafeArea>();
                if (_enableSafeArea) {
                    if (sa == null) {
                        sa = child.gameObject.AddComponent<UISafeArea>();
                    }
                    sa.EnableSafeArea = true;
                }
                else {
                    //todo:
                    if (sa != null) {
                        sa.EnableSafeArea = false;
                    }
                }
            }
        }

        /// <summary>
        /// 初始化层级栈
        /// </summary>
        private void InitLayerStacks() {
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer))) {
                if (!_layerFormStacks.ContainsKey(layer)) {
                    _layerFormStacks.Add(layer,new Stack<string>());
                }
            }
        }

        /// <summary>
        /// 初始化适配管理器
        /// </summary>
        private void InitUIRootAdapter() {
            // 配置UICamera
            if (_UICamera != null) {
                _UICamera.clearFlags = CameraClearFlags.Depth;
                _UICamera.backgroundColor = Color.clear;
                _UICamera.cullingMask = 1 << LayerMask.NameToLayer("UI");
                _UICamera.orthographic = true;
                _UICamera.depth = 100;
                //
                _UICamera.nearClipPlane = 0.3f;
                _UICamera.farClipPlane = 1000f;
                _UICamera.transform.localPosition = new Vector3(0,0,-100f);
                //cam.nearClipPlane = -50f;
                //cam.farClipPlane = 50f;
                //camObj.layer = LayerMask.NameToLayer("UI");
                //camObj.transform.parent = go.transform;
                //camObj.transform.localPosition = new Vector3(0, 0, -100f);
                //Camera cam = camObj.AddComponent<Camera>();
                //cam.clearFlags = CameraClearFlags.Depth;
                //cam.orthographic = true;
                //cam.farClipPlane = 200f;
                //can.worldCamera = cam;
                //cam.cullingMask = 1 << 5;
                //cam.nearClipPlane = -50f;
                //cam.farClipPlane = 50f;
                //_UICamera = camera.GetOrAddComponent<Camera>();
                //_UICamera.cullingMask = 1 << Layer.UI;
                //_UICamera.transform.SetParent(_root);
                //_UICamera.orthographic = true;
                //_UICamera.clearFlags = CameraClearFlags.Depth;
            }
            _uiRootAdapter = _UILayers.GetComponent<UIRootAdapter>();
            if (_uiRootAdapter == null) {
                _uiRootAdapter = _UILayers.gameObject.AddComponent<UIRootAdapter>();
            }

            // 配置适配参数
            _uiRootAdapter.SwitchAdaptMode(_defaultAdaptMode,_UICamera);
            _uiRootAdapter.enabled = true;

        }

        /// <summary>
        /// 初始化EventSystem
        /// </summary>
        private void InitEventSystem() {
            if (FindObjectOfType<EventSystem>() == null) {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
                eventSystemObj.AddComponent<StandaloneInputModule>();
                DontDestroyOnLoad(eventSystemObj);
            }
        }
        /// <summary>
        /// 根据名称获取UIFormConfig
        /// </summary>
        /// <param name="uiFormName"></param>
        /// <returns></returns>
        public UIFormConfig GetUIFormConfig(string uiFormName) {
            UIFormConfig uif = GetFormByUIFormConfigRegistor(uiFormName);
            return uif;
        }
        /// <summary>
        /// 注册UI配置,如果已经存在则覆盖
        /// </summary>
        public void RegisterUIFormConfig(UIFormConfig config) {
            //Todo:如果已经存在显示实例或者在缓存池中，再显示指定名称UIForm可能不能正常使用当前注册进来的
            if (!_uiFormConfigs.ContainsKey(config.FormName)) {
                _uiFormConfigs[config.FormName] = config;
            }
        }

        /// <summary>
        /// 批量注册UI配置
        /// </summary>
        public void RegisterUIFormConfigs(List<UIFormConfig> configs) {
            foreach (var config in configs) {
                RegisterUIFormConfig(config);
            }
        }
        /// <summary>
        /// 自定义UI资源加载器
        /// </summary>
        /// <param name="uiResloaderType"></param>
        /// <param name="uiResloader"></param>
        public void RegisterResourceLoaders(UIResourceLoadType uiResloaderType,IUIResourceLoader uiResloader) {
            _resourceLoaders[uiResloaderType] = uiResloader;
        }
        /// <summary>
        /// 注册所有默认资源加载器
        /// </summary>
        private void RegisterResourceLoaders() {
            _resourceLoaders.Add(UIResourceLoadType.Resources,new ResourcesResourceLoader());
#if ADDRESSABLES_SUPPORT
            _resourceLoaders.Add(UIResourceLoadType.Addressables, new AddressablesLoader());
#endif
#if YOOASSET_SUPPORT
            _resourceLoaders.Add(UIResourceLoadType.YooAsset,new YooAssetResourceLoader());
#endif
            _resourceLoaders.Add(UIResourceLoadType.AssetBundle,new AssetBundleResourceLoader());
        }

        #region 外部核心接口
        /// <summary>
        /// 切换资源加载类型
        /// </summary>
        public void SwitchResourceLoader(UIResourceLoadType loadType) {
            if (_resourceLoaders.ContainsKey(loadType)) {
                _currentResourceLoader = _resourceLoaders[loadType];
                Debug.Log($"资源加载类型已切换为：{loadType}");
            }
            else {
                Debug.LogError($"不支持的资源加载类型：{loadType}");
            }
        }

        /// <summary>
        /// 切换UI适配模式
        /// </summary>
        public void SwitchUIAdaptMode(AdaptMode newMode) {
            _uiRootAdapter?.SwitchAdaptMode(newMode);
        }

        /// <summary>
        /// 获取当前UI适配模式
        /// </summary>
        public AdaptMode GetCurrentUIAdaptMode() {
            return _uiRootAdapter != null ? _uiRootAdapter.GetCurrentAdaptMode() : _defaultAdaptMode;
        }

        /// <summary>
        /// 显示界面（泛型版本）
        /// </summary>
        public void ShowForm<T>(object data = null,Action onComplete = null) where T : UIForm {
            ShowForm(typeof(T).Name,data,onComplete);
        }

        /// <summary>
        /// 显示界面（核心方法）
        /// </summary>
        public void ShowForm(string formName,object data = null,Action onComplete = null) {           
            // 检查配置
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {
                onComplete?.Invoke();
                return;
            }

            // 检查是否已加载
            if (_loadedForms.TryGetValue(formName,out UIForm form)) {
                ShowLoadedForm(form,config,data,onComplete);
                return;
            }

            // 从对象池获取
            GameObject formObj = _uiPool.GetForm(formName);
            if (formObj != null) {
                form = formObj.GetComponent<UIForm>();
                if (form != null) {
                    form.Init(config);
                    _loadedForms.Add(formName,form);
                    ShowLoadedForm(form,config,data,onComplete);
                    return;
                }
                else {
                    Destroy(formObj);
                }
            }

            // 异步加载预制体
            LoadUIPrefabAsync(config,(prefab) => {
                if (prefab == null) {
                    onComplete?.Invoke();
                    return;
                }

                // 实例化界面
                GameObject newFormObj = Instantiate(prefab);
                newFormObj.name = formName;
                UIForm newForm = newFormObj.GetComponent<UIForm>();
                if (newForm == null) {
                    Destroy(newFormObj);
                    Debug.LogError($"预制体 {formName} 未挂载 UIForm 子类脚本");
                    onComplete?.Invoke();
                    return;
                }

                // 初始化并显示
                newForm.Init(config);
                _loadedForms.Add(formName,newForm);
                ShowLoadedForm(newForm,config,data,onComplete);
            },(error) => {
                Debug.LogError(error);
                onComplete?.Invoke();
            });
        }
        /// <summary>
        /// 从配置界面中与注册界面中根据名称获取
        /// </summary>
        /// <param name="formName"></param>
        /// <returns></returns>
        private UIFormConfig GetFormByUIFormConfigRegistor(string formName) {           
            UIFormConfig cf = null;
            _uiFormConfigs.TryGetValue(formName,out cf);             
            if (cf != null) {
                return cf;
            }
            cf = _uiConfig.GetFormConfig(formName);
            return cf;
        }

        public void CloseForm<T>(Action onComplete = null) where T : UIForm {
            CloseForm(typeof(T).Name,onComplete);
        }
        /// <summary>
        /// 关闭界面
        /// </summary>
        public void CloseForm(string formName,Action onComplete = null) {
            if (!_loadedForms.TryGetValue(formName,out UIForm form)) {
                Debug.LogWarning($"未加载界面：{formName}");
                onComplete?.Invoke();
                return;
            }

            UIFormConfig config = form.Config;
            Stack<string> targetStack = _layerFormStacks[config.Layer];

            // 隐藏界面
            form.Hide(false,() => {
                // 出栈并显示上一个界面
                if (config.IsAddToStack && targetStack.Count > 0) {
                    string prevFormName = targetStack.Pop();
                    ShowForm(prevFormName,null,onComplete);
                }
                else {
                    onComplete?.Invoke();
                }

                // 回收至对象池（非永久界面）
                if (!config.IsPermanent) {
                    _uiPool.RecycleForm(form.gameObject,formName);
                    _loadedForms.Remove(formName);
                }
            });
        }

        /// <summary>
        /// 关闭指定层级的所有界面
        /// </summary>
        public void CloseLayerAllForms(UILayer layer,Action onComplete = null) {
            Stack<string> targetStack = _layerFormStacks[layer];
            List<UIForm> layerForms = new List<UIForm>();

            foreach (var form in _loadedForms.Values) {
                if (form.Config.Layer == layer) {
                    layerForms.Add(form);
                }
            }

            if (layerForms.Count == 0) {
                targetStack.Clear();
                onComplete?.Invoke();
                return;
            }

            int closeCount = 0;
            foreach (var form in layerForms) {
                form.Hide(true,() => {
                    closeCount++;
                    if (closeCount >= layerForms.Count) {
                        targetStack.Clear();
                        foreach (var f in layerForms) {
                            _loadedForms.Remove(f.FormName);
                            _uiPool.RecycleForm(f.gameObject,f.FormName);
                        }
                        onComplete?.Invoke();
                    }
                });
            }
        }

        /// <summary>
        /// 关闭所有界面
        /// </summary>
        public void CloseAllForms(Action onComplete = null) {
            List<UIForm> allForms = new List<UIForm>(_loadedForms.Values);
            if (allForms.Count == 0) {
                foreach (var stack in _layerFormStacks.Values) {
                    stack.Clear();
                }
                onComplete?.Invoke();
                return;
            }

            int closeCount = 0;
            foreach (var form in allForms) {
                form.Hide(true,() => {
                    closeCount++;
                    if (closeCount >= allForms.Count) {
                        _loadedForms.Clear();
                        foreach (var stack in _layerFormStacks.Values) {
                            stack.Clear();
                        }
                        _uiPool.Clear();
                        onComplete?.Invoke();
                    }
                });
            }
        }

        /// <summary>
        /// 显示普通弹窗（快捷方法）
        /// </summary>
        public void ShowPopup(string title,string content,Action onConfirm = null,Action onCancel = null,Action onComplete = null) {
            ShowForm<UIPopup>(null,() => {
                var popup = GetForm<UIPopup>();
                popup?.ShowPopup(title,content,onConfirm,onCancel,onComplete);
            });
        }

        /// <summary>
        /// 显示输入框弹窗（快捷方法）
        /// </summary>
        public void ShowInputPopup(string title,string hint,InputFieldType inputType = InputFieldType.Normal,
                                   string defaultValue = "",int maxLength = 20,Action<string> onConfirm = null,
                                   Action onCancel = null,Action onComplete = null) {
            ShowForm<UIInputPopup>(null,() => {
                var inputPopup = GetForm<UIInputPopup>();
                if (inputPopup == null) {
                    Debug.LogError("输入框弹窗 UIInputPopup 加载失败");
                    onComplete?.Invoke();
                    return;
                }
                inputPopup.ShowInputPopup(title,hint,inputType,defaultValue,maxLength,onConfirm,onCancel,onComplete);
            });
        }

        /// <summary>
        /// 显示通知（快捷方法）
        /// </summary>
        public void ShowNotify(string content,NotifyPosition position = NotifyPosition.TopRight,float duration = 3f) {
            UIEventBus.Trigger<(string, NotifyPosition, float)>(UIEventNames.NotifyShow,(content, position, duration));
        }
        #endregion

        #region 内部辅助方法
        /// <summary>
        /// 显示已加载的界面（处理层级栈逻辑）
        /// </summary>
        private void ShowLoadedForm(UIForm form,UIFormConfig config,object data,Action onComplete) {
            if (config.IsAddToStack) {
                Stack<string> targetStack = _layerFormStacks[config.Layer];
                List<UIForm> sameLayerShowingForms = new List<UIForm>();

                // 收集同层级显示中的界面
                foreach (var loadedForm in _loadedForms.Values) {
                    if (loadedForm.IsShowing && loadedForm.Config.Layer == config.Layer && loadedForm != form) {
                        sameLayerShowingForms.Add(loadedForm);
                    }
                }

                // 隐藏同层级界面并入栈
                if (sameLayerShowingForms.Count > 0) {
                    int hideCount = 0;
                    foreach (var sameLayerForm in sameLayerShowingForms) {
                        sameLayerForm.Hide(false,() => {
                            targetStack.Push(sameLayerForm.FormName);
                            hideCount++;

                            if (hideCount == sameLayerShowingForms.Count) {
                                form.Show(data,onComplete);
                            }
                        });
                    }
                    return;
                }
            }

            // 直接显示
            form.Show(data,onComplete);
        }

        /// <summary>
        /// 异步加载UI预制体
        /// </summary>
        private void LoadUIPrefabAsync(UIFormConfig config,Action<GameObject> onSuccess,Action<string> onFailed) {
            if (config.RefPrefab != null) {
                onSuccess?.Invoke(config.RefPrefab);
                return;
            }
            _currentResourceLoader.LoadUIPrefabAsync(config.PrefabPath,config.AssetBundleName,onSuccess,onFailed);
        }

        /// <summary>
        /// 获取指定层级的父节点
        /// </summary>
        public Transform GetLayerParent(UILayer layer) {
            _layerParents.TryGetValue(layer,out Transform parent);
            return parent;
        }

        /// <summary>
        /// 获取已加载的界面
        /// </summary>
        public T GetForm<T>() where T : UIForm {
            string formName = typeof(T).Name;
            _loadedForms.TryGetValue(formName,out UIForm form);
            return form as T;
        }

        /// <summary>
        /// 检查指定层级是否有显示中的界面
        /// </summary>
        public bool IsLayerHasShowingForm(UILayer layer) {
            foreach (var form in _loadedForms.Values) {
                if (form.Config.Layer == layer && form.IsShowing) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 检查界面是否显示中
        /// </summary>
        public bool IsFormShowing(string formName) {
            return _loadedForms.TryGetValue(formName,out UIForm form) && form.IsShowing;
        }

        /// <summary>
        /// 获取指定层级的栈顶界面名称
        /// </summary>
        public string GetLayerTopFormName(UILayer layer) {
            var stack = _layerFormStacks[layer];
            return stack.Count > 0 ? stack.Peek() : null;
        }
        #endregion

        private void OnDestroy() {
            // 清空事件总线
            UIEventBus.ClearAllEvents();
        }
    }



}
