using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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

        [SerializeField] private Canvas _UILayers; // 绑定UILayers节点
        [SerializeField] private Camera _UICamera; // 绑定UICamera节点
        [SerializeField] private UIResourceLoadType _defaultLoadType = UIResourceLoadType.Resources;

        [Header("适配配置")]
        [SerializeField] private AdaptMode _defaultAdaptMode = AdaptMode.BlackBars;
        [Header("黑边模式的填充色")]
        [SerializeField] private Color _backSideColor = Color.black;
        [Header("设计分辨率")]
        [SerializeField] private Vector2 _referenceResolution = new(1920,1080); // 设计分辨率
        [Header("设计camera Depth排序")]
        [SerializeField] private int _cameraDepth = 100;
        [Header("设计canvas排序")]
        [SerializeField] private int _canvasSortingOrder = 100;
        [Header("SafeArea配置")]
        [SerializeField] private bool _enableSafeArea = true; // 是否启用SafeArea适配
        // 核心数据结构
        private readonly Dictionary<UILayer,Stack<string>> _layerFormStacks = new Dictionary<UILayer,Stack<string>>(); // 层级独立栈
        private readonly Dictionary<string,UIFormBase> _loadedForms = new Dictionary<string,UIFormBase>(); // 已加载界面缓存
        private readonly Dictionary<UILayer,Transform> _layerParents = new Dictionary<UILayer,Transform>(); // 层级父节点缓存
        private readonly Dictionary<UIResourceLoadType,IUIResourceLoader> _resourceLoaders = new Dictionary<UIResourceLoadType,IUIResourceLoader>(); // 资源加载器缓存
        private readonly Dictionary<string,bool> _loadingForms = new Dictionary<string,bool>();
        private UIPool _uiPool;
        private UICanvasAdapter _uiRootAdapter;
        private IUIResourceLoader _currentResourceLoader;

        private void Awake() {
            // 单例初始化
            if (Instance != null && Instance != this) {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            UICoroutineHelper.Initialize(this,System.Threading.Thread.CurrentThread.ManagedThreadId);
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
        private void OnDestroy() {
            // 清空事件总线
            if (Instance != this) {
                UIEventBus.ClearAllEvents();
                Instance = null;
            }
        }
        private void Start() {
            ShowForm<UIFormNotify>();
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
                _UILayers = this.transform.Find("UILayers")?.GetComponent<Canvas>();
                if (_UILayers == null) {
                    GameObject ul = new GameObject("UILayers",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                    ul.layer = LayerMask.NameToLayer("UI");
                    ul.transform.SetParent(this.transform);
                    ul.transform.localPosition = Vector3.zero;
                    _UILayers = ul.GetComponent<Canvas>();
                    _UILayers.sortingOrder = _canvasSortingOrder;
                }
            }
            var uilayers = Enum.GetValues(typeof(UILayer));
            float maxDistance = uilayers.Length * 10f;
            foreach (UILayer layer in uilayers) {
                string layerName = layer.ToString();
                Transform child = _UILayers.transform.Find(layerName);
                if (child != null) {
                    _layerParents.Add(layer,child);
                }
                else {
                    //Debug.LogError($"UIManager: UILayers下未找到层级节点 {layerName}");
                    GameObject lay = new GameObject(layerName,typeof(RectTransform));
                    lay.layer = LayerMask.NameToLayer("UI");
                    lay.transform.SetParent(_UILayers.transform);
                    child = lay.transform;
                    child.localPosition = new Vector3(0f,0f,maxDistance - ((int)layer * 10f));
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
                _UICamera.depth = _cameraDepth;
                //
                _UICamera.nearClipPlane = 1f;
                _UICamera.farClipPlane = 500f;
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
            _uiRootAdapter = _UILayers.GetComponent<UICanvasAdapter>();
            if (_uiRootAdapter == null) {
                _uiRootAdapter = _UILayers.gameObject.AddComponent<UICanvasAdapter>();
            }
            // 配置适配参数\
            _uiRootAdapter._backSideColor = this._backSideColor;
            _uiRootAdapter._referenceResolution = this._referenceResolution;
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
        /// 初始化场景监听
        /// </summary>
        private void InitSceneListener() {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// 场景卸载回调
        /// </summary>
        private void OnSceneUnloaded(Scene scene) {
            //// 回收非永久UI
            //List<string> formsToRemove = new List<string>();
            //foreach (var kvp in _loadedForms) {
            //    if (!kvp.Value.Config.IsPermanent) {
            //        formsToRemove.Add(kvp.Key);
            //    }
            //}

            //foreach (string formName in formsToRemove) {
            //    CloseForm(formName,true);
            //}

            //// 清空栈和加载状态
            //foreach (var stack in _layerFormStacks.Values) {
            //    stack.Clear();
            //}
            //_loadingForms.Clear();
            //UIEventBus.ClearAllEvents();
        }

        /// <summary>
        /// 场景加载回调
        /// </summary>
        private void OnSceneLoaded(Scene scene,LoadSceneMode mode) {
            //// 重新适配SafeArea           
            //_uiRootAdapter?.RefreshSafeArea();
            //// 加载场景默认UI
            //List<string> defaultForms = UIConfig.GetSceneDefaultForms(scene.name);
            //foreach (string formName in defaultForms) {
            //    ShowForm(formName);
            //}
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
            _uiRootAdapter?.SwitchAdaptMode(newMode,_UICamera);
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
        public void ShowForm<T>(object data = null,Action onComplete = null) where T : UIFormBase {
            // 检查配置
            Type formType = typeof(T).GetType();
            string formName = typeof(T).Name;
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {

                onComplete?.Invoke();
                return;
            }
            else {
                //todo:动态根据泛型类型属性查找
                //UIFormDetailAttribute attribute = formType.GetCustomAttribute<UIFormDetailAttribute>(inherit: false);
                //if (attribute == null) {
                //    onComplete?.Invoke();
                //    return;
                //}
                ////创建uiconfig并注册到
                //UIFormConfig cofig = new UIFormConfig();
                ShowForm(typeof(T).Name,data,onComplete);
            }
        }

        /// <summary>
        /// 显示界面（核心方法-名称版）
        /// </summary>
        public void ShowForm(string formName,object data = null,Action onComplete = null) {
            // 检查配置
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {
                onComplete?.Invoke();
                return;
            }
            // 检查是否已加载
            if (_loadedForms.TryGetValue(formName,out UIFormBase loadedForm)) {
                if (loadedForm.CurrentState == UIFormState.Destroyed) {
                    _loadedForms.Remove(formName);
                    TryGetFromPoolAndShow(formName,config,data,onComplete);
                }
                else {
                    ShowLoadedForm(loadedForm,loadedForm.Config,data,onComplete);
                }
                return;
            }

            // 检查是否正在加载
            if (_loadingForms.ContainsKey(formName)) {
                Debug.LogWarning($"界面 {formName} 正在加载中，请勿重复调用");
                onComplete?.Invoke();
                return;
            }

            // 加载并显示
            TryGetFromPoolAndShow(formName,config,data,onComplete);

        }
        private void TryGetFromPoolAndShow(string formName,UIFormConfig config,object data,Action onComplete) {
            if (config.Lifecycle != UIFormLifecycle.IsPermanent) {
                GameObject formObj = _uiPool.GetForm(formName);
                if (formObj != null) {
                    UIFormBase form = formObj.GetComponent<UIFormBase>();
                    if (form != null) {
                        form.Init(config,data,true);
                        form._setFormState(UIFormState.Loaded);
                        _loadedForms[formName] = form;
                        ShowLoadedForm(form,config,data,onComplete);
                        return;
                    }
                    else {
                        Destroy(formObj);
                    }
                }
            }
            LoadAndShowForm(formName,data,onComplete);
        }
        /// <summary>
        /// 加载并显示界面
        /// </summary>
        private void LoadAndShowForm(string formName,object data,Action onComplete) {
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {
                Debug.LogError($"未找到界面配置：{formName}");
                onComplete?.Invoke();
                return;
            }

            // 显示加载界面
            if (config.ShowLoading) {
                // 使用UITopWaitingMgr显示加载界面，使用formName作为windowId
                UITopWaitingMgr.Show(formName);
            }

            // 标记为加载中
            _loadingForms.Add(formName,true);

            // 异步加载预制体
            LoadUIPrefabAsync(config,(prefab) => {
                if (prefab == null) {
                    if (config.ShowLoading) {
                        UITopWaitingMgr.Hide(formName);
                    }
                    _loadingForms.Remove(formName);
                    onComplete?.Invoke();
                    return;
                }

                // 实例化界面
                GameObject formObj = Instantiate(prefab,GetLayerParent(config.Layer));
                formObj.name = formName;
                UIFormBase form = formObj.GetComponent<UIFormBase>();
                if (form == null) {
                    Debug.LogError($"界面预制体 {formName} 未挂载UIForm脚本");
                    Destroy(formObj);
                    if (config.ShowLoading) {
                        UITopWaitingMgr.Hide(formName);
                    }
                    _loadingForms.Remove(formName);
                    onComplete?.Invoke();
                    return;
                }

                // 初始化界面
                form.Init(config,data,false);
                form._setFormState(UIFormState.Loaded);
                _loadedForms[formName] = form;

                // 隐藏加载界面并显示目标界面
                if (config.ShowLoading) {
                    // 使用UITopWaitingMgr隐藏加载界面
                    UITopWaitingMgr.Hide(formName);
                }
                
                ShowLoadedForm(form,config,data,onComplete);
                _loadingForms.Remove(formName);
            },(error) => {
                Debug.LogError($"加载界面 {formName} 失败：{error}");
                if (config.ShowLoading) UITopWaitingMgr.Hide(formName);
                _loadingForms.Remove(formName);
                onComplete?.Invoke();
            });
        }


        /// <summary>
        /// 从配置界面中与注册界面中根据名称获取
        /// </summary>
        /// <param name="formName"></param>
        /// <returns></returns>
        private UIFormConfig GetFormByUIFormConfigRegistor(string formName) {

            if (string.IsNullOrEmpty(formName)) {
                Debug.LogError("界面名称为空");
                return null;
            }
            UIFormConfig cf = null;
            _uiFormConfigs.TryGetValue(formName,out cf);
            if (cf != null) {
                return cf;
            }
            cf = _uiConfig.GetFormConfig(formName);
            return cf;
        }
        /// <summary>
        /// 关闭界面(泛型版)
        /// </summary>
        public void CloseForm<T>(Action onComplete = null) where T : UIFormBase {
            CloseForm(typeof(T).Name,onComplete);
        }
        /// <summary>
        /// 关闭界面(核心-名称版)
        /// </summary>
        public void CloseForm(string formName,Action onComplete = null) {
            if (!_loadedForms.TryGetValue(formName,out UIFormBase form)) {
                Debug.LogWarning($"未加载界面：{formName}");
                onComplete?.Invoke();
                return;
            }

            UIFormConfig config = form.Config;
            Stack<string> targetStack = _layerFormStacks[config.Layer]; // 仅操作当前层级的栈
            bool isDestroy = config.Lifecycle == UIFormLifecycle.AutoDestroy; // 标记是否需要销毁
            // 隐藏界面
            form.Hide(isDestroy,() => {
                // 检查是否还有其他显示中的界面在当前层级
                bool hasOtherShowingForms = false;
                foreach (var loadedForm in _loadedForms.Values) {
                    if (loadedForm.IsShowing && loadedForm.Config.Layer == config.Layer && loadedForm.FormName != formName) {
                        hasOtherShowingForms = true;
                        break;
                    }
                }

                // 如果当前层级没有其他显示中的界面，并且栈不为空，则显示栈顶界面
                if (!hasOtherShowingForms && targetStack.Count > 0) {
                    string prevFormName = targetStack.Pop();
                    ShowForm(prevFormName,null,onComplete);
                }
                else {
                    onComplete?.Invoke();
                }
                // 根据生命周期策略处理后续逻辑
                switch (config.Lifecycle) {
                    case UIFormLifecycle.IsPermanent:
                        // 常驻界面：仅隐藏，保留在_loadedForms中
                        break;
                    case UIFormLifecycle.AutoRecycle:
                        // 回收至对象池
                        form.Recycle();
                        _uiPool.RecycleForm(form.gameObject,formName);
                        _loadedForms.Remove(formName);
                        break;
                    case UIFormLifecycle.AutoDestroy:
                        // 直接销毁
                        Destroy(form.gameObject);
                        _loadedForms.Remove(formName);
                        break;
                }

            });
        }

        /// <summary>
        /// 关闭指定层级的所有界面（适配生命周期）
        /// </summary>
        public void CloseLayerAllForms(UILayer layer,Action onComplete = null) {
            Stack<string> targetStack = _layerFormStacks[layer];
            List<string> formNames = new List<string>(targetStack);
            formNames.AddRange(_loadedForms.Keys.Where(name => _loadedForms[name].Config.Layer == layer));

            int closeCount = 0;
            int totalCount = formNames.Count;

            if (totalCount == 0) {
                onComplete?.Invoke();
                return;
            }

            foreach (string formName in formNames) {
                CloseForm(formName,() => {
                    closeCount++;
                    if (closeCount == totalCount) {
                        targetStack.Clear();
                        onComplete?.Invoke();
                    }
                });
            }
        }

        /// <summary>
        /// 关闭所有界面
        /// </summary>
        public void CloseAllForms(Action onComplete = null) {
            List<string> formNames = new List<string>(_loadedForms.Keys);
            int closeCount = 0;
            int totalCount = formNames.Count;

            if (totalCount == 0) {
                onComplete?.Invoke();
                return;
            }

            foreach (string formName in formNames) {
                CloseForm(formName,() => {
                    closeCount++;
                    if (closeCount == totalCount) {
                        foreach (var stack in _layerFormStacks.Values) {
                            stack.Clear();
                        }
                        onComplete?.Invoke();
                    }
                });
            }

        }
       
        /// <summary>
        /// 显示内置的通用弹窗
        /// </summary>
        public void ShowPopup(string title,string content,DialogButtonType buttonType,
                             Action<DialogButtonResult> onResult,Action onShowComplete = null) {
            ShowForm<UIFormPopup>(null,() => {
                var popup = GetForm<UIFormPopup>();
                popup?.ShowPopup(title,content,buttonType,onResult,onShowComplete);
            });
        }

        /// <summary>
        /// 显示内置的通用输入框窗口
        /// </summary>
        public void ShowInputPopup(string title,string hint,InputType inputType,bool isPassword = false,
                                  string defaultValue = "",int maxLength = 20,
                                  Action<object> onConfirm = null,Action onCancel = null,
                                  Action onShowComplete = null) {
            ShowForm<UIFormInputPopup>(null,() => {
                var inputPopup = GetForm<UIFormInputPopup>();
                inputPopup?.ShowInputPopup(title,hint,inputType,isPassword,defaultValue,maxLength,
                                          onConfirm,onCancel,onShowComplete);
            });
        }

        /// <summary>
        /// 显示通知（快捷方法）
        /// </summary>
        public void ShowNotify(string content,NotifyPosition position = NotifyPosition.TopRight,float duration = 3f) {
            UIEventBus.Trigger<(string, NotifyPosition, float)>(UIEventNames.NotifyShow,(content, position, duration));
        }
        /// <summary>
        /// 显示内置的loading窗口
        /// </summary>
        /// <param name="context"></param>
        public void ShowLoadingForm(string context) {
            ShowForm<UIFormLoading>(null,()=> {
                var lf = GetForm<UIFormLoading>();
                lf?.UpdateProgress(0.2f,context);
              
            });
        }
        /// <summary>
        /// 更新内置的Loading窗口的进度，UITopWaitingMgr中的等待窗口更新进度自己管理，不在这里提供接口
        /// </summary>
        public void UpdateLoadingFormProgress(float progress,string tip = "") {
            var loadingForm = GetForm<UIFormLoading>();
            if (loadingForm != null) {
                loadingForm.UpdateProgress(progress,tip);
            }
        }
        /// <summary>
        /// 关闭内置的loading窗口
        /// </summary>
        public void CloseLoadingForm() {
            CloseForm<UIFormLoading>();
        }
        #endregion

        #region 内部辅助方法
        /// <summary>
        /// 显示已加载的界面（处理层级栈逻辑）
        /// </summary>
        private void ShowLoadedForm(UIFormBase form,UIFormConfig config,object data,Action onComplete) {
            if (config.IsAddToStack) {
                Stack<string> targetStack = _layerFormStacks[config.Layer];
                List<UIFormBase> sameLayerShowingForms = new List<UIFormBase>();

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
                        // 固定传递 isDestroy=false，仅临时隐藏，不销毁
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
        public T GetForm<T>() where T : UIFormBase {
            string formName = typeof(T).Name;
            _loadedForms.TryGetValue(formName,out UIFormBase form);
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
            return _loadedForms.TryGetValue(formName,out UIFormBase form) && form.IsShowing;
        }
        /// <summary>
        /// 检查界面是否显示中
        /// </summary>
        public bool IsFormShowing<T>()where T:UIFormBase {
            Type formType = typeof(T).GetType();
            string formName = typeof(T).Name;
            return IsFormShowing(formName);
        }
        /// <summary>
        /// 检查界面是否正在加载
        /// </summary>
        public bool IsFormLoading(string formName) {
            return _loadingForms.ContainsKey(formName);
        }
        /// <summary>
        /// 检查界面是否正在加载
        /// </summary>
        public bool IsFormLoading<T>()where T:UIFormBase {
            Type formType = typeof(T).GetType();
            string formName = typeof(T).Name;
            return IsFormLoading(formName);
        }
        /// <summary>
        /// 获取指定层级的栈顶界面名称
        /// </summary>
        public string GetLayerTopFormName(UILayer layer) {
            var stack = _layerFormStacks[layer];
            return stack.Count > 0 ? stack.Peek() : null;
        }
        #endregion


    }



}
