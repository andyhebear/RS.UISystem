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
        /// 显示界面（泛型版本）- 优化版
        /// </summary>
        public UITask<T> ShowForm<T>(object data = null,Action onComplete = null) where T : UIFormBase {
            UITask<T> task = UITask<T>.Create();
            string formName = typeof(T).Name;

            HandleShowForm<T>(formName,data,task,onComplete);
            return task;
        }

        /// <summary>
        /// 显示界面（核心方法-名称版）- 优化版
        /// </summary>
        public UITask<UIFormBase> ShowForm(string formName,object data = null,Action onComplete = null) {
            UITask<UIFormBase> task = UITask<UIFormBase>.Create();

            HandleShowForm<UIFormBase>(formName,data,task,onComplete);
            return task;
        }

        /// <summary>
        /// 处理界面显示的通用逻辑，减少代码重复
        /// </summary>
        private void HandleShowForm<T>(string formName,object data,UITask<T> task,Action onComplete) where T : UIFormBase {
            // 检查配置
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {
                string errorMsg = $"未找到界面配置：{formName}";
                Debug.LogError(errorMsg);
                task.SetException(new Exception(errorMsg));
                onComplete?.Invoke();
                return;
            }

            // 统一处理显示结果
            ShowFormAsync(formName,data,(form) => {
                try {
                    if (form == null) {
                        throw new Exception($"界面加载或显示失败：{formName}");
                    }

                    T typedForm = form as T;
                    if (typedForm != null) {
                        task.SetResult(typedForm);
                    }
                    else {
                        throw new Exception($"界面类型转换失败：{formName}，期望类型：{typeof(T).Name}");
                    }
                }
                catch (Exception ex) {
                    task.SetException(ex);
                    Debug.LogError($"显示界面失败：{formName} - {ex.Message}\n{ex.StackTrace}");
                }
                finally {
                    onComplete?.Invoke();
                }
            });
        }

        /// <summary>
        /// 异步显示界面并返回UIFormBase对象 - 优化版
        /// 统一处理配置获取、状态检查和加载逻辑
        /// </summary>
        private void ShowFormAsync(string formName,object data,Action<UIFormBase> onComplete) {
            // 获取界面配置
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {
                Debug.LogError($"未找到界面配置：{formName}");
                onComplete?.Invoke(null);
                return;
            }

            // 统一处理已加载界面的状态检查和显示
            if (TryHandleLoadedForm(formName, data, onComplete)) {
                return;
            }

            // 检查是否正在加载
            if (_loadingForms.ContainsKey(formName)) {
                Debug.LogWarning($"界面 {formName} 正在加载中，请勿重复调用");
                onComplete?.Invoke(null);
                return;
            }

            // 从对象池获取或加载新界面
            TryGetFromPoolAndShowAsync(formName,config,data,onComplete);
        }

        /// <summary>
        /// 统一处理已加载界面的状态检查和显示逻辑
        /// </summary>
        private bool TryHandleLoadedForm(string formName, object data, Action<UIFormBase> onComplete) {
            if (!_loadedForms.TryGetValue(formName, out UIFormBase loadedForm)) {
                return false;
            }

            // 处理已销毁的界面
            if (loadedForm.CurrentState == UIFormState.Destroyed) {
                _loadedForms.Remove(formName);
                var config = GetFormByUIFormConfigRegistor(formName);
                if (config != null) {
                    TryGetFromPoolAndShowAsync(formName, config, data, onComplete);
                    return true;
                }
                return false;
            }

            // 正常显示已加载的界面
            ShowLoadedFormAsync(loadedForm, loadedForm.Config, data, onComplete);
            return true;
        }
        private void TryGetFromPoolAndShow(string formName,UIFormConfig config,object data,Action onComplete) {
            TryGetFromPoolAndShowAsync(formName,config,data,(form) => {
                onComplete?.Invoke();
            });
        }

        private void TryGetFromPoolAndShowAsync(string formName,UIFormConfig config,object data,Action<UIFormBase> onComplete) {
            if (config.Lifecycle != UIFormLifecycle.IsPermanent) {
                GameObject formObj = _uiPool.GetForm(formName);
                if (formObj != null) {
                    UIFormBase form = formObj.GetComponent<UIFormBase>();
                    if (form != null) {
                        form.Init(config,data,true);
                        form._setFormState(UIFormState.Loaded);
                        _loadedForms[formName] = form;
                        ShowLoadedFormAsync(form,config,data,onComplete);
                        return;
                    }
                    else {
                        Destroy(formObj);
                    }
                }
            }
            LoadAndShowFormAsync(formName,data,onComplete);
        }
        /// <summary>
        /// 加载并显示界面
        /// </summary>
        private void LoadAndShowForm(string formName,object data,Action onComplete) {
            LoadAndShowFormAsync(formName,data,(form) => {
                onComplete?.Invoke();
            });
        }

        private void LoadAndShowFormAsync(string formName,object data,Action<UIFormBase> onComplete) {
            UIFormConfig config = GetFormByUIFormConfigRegistor(formName);
            if (config == null) {
                Debug.LogError($"未找到界面配置：{formName}");
                onComplete?.Invoke(null);
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
                    onComplete?.Invoke(null);
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
                    onComplete?.Invoke(null);
                    return;
                }

                // 初始化界面（注意：现在Init返回IEnumerator）
                StartCoroutine(HandleFormInit(form,config,data,() => {
                    form._setFormState(UIFormState.Loaded);
                    _loadedForms[formName] = form;

                    // 隐藏加载界面并显示目标界面
                    if (config.ShowLoading) {
                        // 使用UITopWaitingMgr隐藏加载界面
                        UITopWaitingMgr.Hide(formName);
                    }

                    ShowLoadedFormAsync(form,config,data,onComplete);
                    _loadingForms.Remove(formName);
                }));
            },(error) => {
                Debug.LogError($"加载界面 {formName} 失败：{error}");
                if (config.ShowLoading) UITopWaitingMgr.Hide(formName);
                _loadingForms.Remove(formName);
                onComplete?.Invoke(null);
            });
        }

        /// <summary>
        /// 处理界面初始化的协程 - 优化版
        /// 改进错误处理和资源管理
        /// </summary>
        private IEnumerator HandleFormInit(UIFormBase form,UIFormConfig config,object data,Action onComplete) {
            if (form == null) {
                Debug.LogError("HandleFormInit: form参数为null");
                onComplete?.Invoke();
                yield break;
            }

            IEnumerator initCoroutine = null;
            bool hasError = false;
            Exception firstException = null;

            try {
                initCoroutine = form.Init(config, data, false);
            }
            catch (Exception e) {
                hasError = true;
                firstException = e;
                Debug.LogError($"界面初始化异常: {form.FormName} - {e.Message}\n{e.StackTrace}");
            }
            UIFormOpResult result = new UIFormOpResult();
            if (initCoroutine != null) {
                yield return ProcessInitCoroutine(form, initCoroutine,result);
            }

            // 如果有错误，标记界面状态为异常
            if (hasError && form != null) {
                try {
                    form._setFormState(UIFormState.Error);
                    Debug.LogError($"界面 {form.FormName} 初始化失败，状态已设置为Error");
                }
                catch (Exception stateEx) {
                    Debug.LogError($"设置界面状态异常: {form.FormName} - {stateEx.Message}");
                }
            }

            // 调用完成回调（无论成功或失败）
            onComplete?.Invoke();
        }
        class UIFormOpResult {
            public bool hasError;
            public Exception firstException;
            UIFormBase form;
        }
        /// <summary>
        /// 处理初始化协程的执行
        /// </summary>
        private IEnumerator ProcessInitCoroutine(UIFormBase form, IEnumerator initCoroutine,UIFormOpResult result) {
            bool wait = true;
            
            while (wait) {
                try {
                    wait = initCoroutine.MoveNext();
                }
                catch (Exception ee) {
                    //if (!hasError) {
                        result.hasError = true;
                        result.firstException = ee;
                    //}
                    Debug.LogError($"界面初始化协程异常: {form.FormName} - {ee.Message}\n{ee.StackTrace}");
                    wait = false;
                }

                if (wait) {
                    //try {
                        yield return initCoroutine.Current;
                    //}
                    //catch (Exception yieldEx) {
                     ///   if (!hasError) {
                     //       hasError = true;
                     //       firstException = yieldEx;
                     //   }
                      //  Debug.LogError($"界面初始化yield异常: {form.FormName} - {yieldEx.Message}");
                    //}
                }
            }
            
            //if (!hasError) {
                Debug.Log($"界面初始化协程结束: {form.FormName}");
           // }
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
        /// 关闭界面(泛型版) - 优化版
        /// </summary>
        public UITask<bool> CloseForm<T>(Action onComplete = null) where T : UIFormBase {
            UITask<bool> task = UITask<bool>.Create();
            string formName = typeof(T).Name;

            HandleCloseForm(formName,task,onComplete);
            return task;
        }

        /// <summary>
        /// 关闭界面(核心-名称版) - 优化版
        /// </summary>
        public UITask<bool> CloseForm(string formName,Action onComplete = null) {
            UITask<bool> task = UITask<bool>.Create();

            HandleCloseForm(formName,task,onComplete);
            return task;
        }

        /// <summary>
        /// 处理界面关闭的通用逻辑，减少代码重复
        /// </summary>
        private void HandleCloseForm(string formName,UITask<bool> task,Action onComplete) {
            try {
                CloseFormAsync(formName,(success) => {
                    try {
                        task.SetResult(success);
                        if (!success) {
                            Debug.LogWarning($"关闭界面失败：{formName}");
                        }
                    }
                    catch (Exception ex) {
                        task.SetException(ex);
                        Debug.LogError($"关闭界面异常：{formName} - {ex.Message}\n{ex.StackTrace}");
                    }
                    finally {
                        onComplete?.Invoke();
                    }
                });
            }
            catch (Exception ex) {
                task.SetException(ex);
                Debug.LogError($"关闭界面调用异常：{formName} - {ex.Message}\n{ex.StackTrace}");
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 异步关闭界面的核心实现
        /// </summary>
        private void CloseFormAsync(string formName,Action<bool> onComplete) {
            if (!_loadedForms.TryGetValue(formName,out UIFormBase form)) {
                Debug.LogWarning($"未加载界面：{formName}");
                onComplete?.Invoke(false);
                return;
            }

            UIFormConfig config = form.Config;
            Stack<string> targetStack = _layerFormStacks[config.Layer]; // 仅操作当前层级的栈
            bool isDestroy = config.Lifecycle == UIFormLifecycle.AutoDestroy; // 标记是否需要销毁
            // 隐藏界面
            form.Hide(isDestroy,() => {
                // 延迟一帧检查，确保界面状态已更新
                StartCoroutine(CheckAndShowPreviousForm(formName, config, targetStack, isDestroy, form, onComplete));
            });
        }

        /// <summary>
        /// 延迟检查并显示上一个界面的协程，避免竞态条件
        /// </summary>
        private IEnumerator CheckAndShowPreviousForm(string formName, UIFormConfig config, Stack<string> targetStack, bool isDestroy, UIFormBase form, Action<bool> onComplete) {
            // 延迟一帧，确保界面状态已更新
            yield return null;
            
            // 根据生命周期策略处理后续逻辑（在检查其他界面前执行，避免界面被错误销毁）
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
                // 优化：使用异步方式显示上一个界面，并正确处理任务
                yield return ShowPreviousFormAndComplete(prevFormName,onComplete);
            }
            else {
                onComplete?.Invoke(true);
            }
        }
        

        /// <summary>
        /// 显示上一个界面并完成回调的协程
        /// 优化了嵌套异步任务的处理，确保所有异步操作正确完成
        /// </summary>
        private IEnumerator ShowPreviousFormAndComplete(string formName,Action<bool> onComplete) {
            // 使用UITask等待上一个界面显示完成
            UITask<UIFormBase> showTask = ShowForm(formName);

            // 等待任务完成
            yield return showTask;

            // 检查任务是否成功完成
            bool success = showTask != null && !showTask.IsFaulted;
            
            // 完成原始回调
            if (onComplete != null) {
                onComplete(success);
            }
        }

        /// <summary>
        /// 关闭指定层级的所有界面（适配生命周期）- 优化版
        /// 改进并发处理和异常管理
        /// </summary>
        public void CloseLayerAllForms(UILayer layer,Action onComplete = null) {
            Stack<string> targetStack = _layerFormStacks[layer];
            
            // 使用HashSet避免重复处理
            HashSet<string> formNamesToClose = new HashSet<string>(targetStack);
            
            // 添加已加载的同层级界面
            foreach (var kvp in _loadedForms) {
                if (kvp.Value.Config.Layer == layer) {
                    formNamesToClose.Add(kvp.Key);
                }
            }

            int totalCount = formNamesToClose.Count;
            if (totalCount == 0) {
                onComplete?.Invoke();
                return;
            }

            // 使用并发安全的计数器
            int closeCount = 0;
            bool hasError = false;
            Exception firstException = null;

            foreach (string formName in formNamesToClose) {
                try {
                    CloseForm(formName,() => {
                        try {
                            lock (targetStack) {
                                closeCount++;
                                
                                // 检查是否所有界面都已关闭
                                if (closeCount == totalCount) {
                                    targetStack.Clear();
                                    onComplete?.Invoke();
                                }
                            }
                        }
                        catch (Exception ex) {
                            lock (targetStack) {
                                if (!hasError) {
                                    hasError = true;
                                    firstException = ex;
                                    Debug.LogError($"关闭层级界面异常：{ex.Message}\n{ex.StackTrace}");
                                }
                                
                                // 即使出错也要确保回调被调用
                                closeCount++;
                                if (closeCount == totalCount) {
                                    targetStack.Clear();
                                    onComplete?.Invoke();
                                }
                            }
                        }
                    });
                }
                catch (Exception ex) {
                    lock (targetStack) {
                        if (!hasError) {
                            hasError = true;
                            firstException = ex;
                            Debug.LogError($"关闭界面调用异常：{formName} - {ex.Message}\n{ex.StackTrace}");
                        }
                        
                        // 继续处理其他界面
                        closeCount++;
                        if (closeCount == totalCount) {
                            targetStack.Clear();
                            onComplete?.Invoke();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 关闭所有界面 - 优化版
        /// 改进并发处理和异常管理
        /// </summary>
        public void CloseAllForms(Action onComplete = null) {
            List<string> formNames = new List<string>(_loadedForms.Keys);
            int totalCount = formNames.Count;

            if (totalCount == 0) {
                onComplete?.Invoke();
                return;
            }

            // 使用并发安全的计数器
            int closeCount = 0;
            bool hasError = false;
            Exception firstException = null;

            foreach (string formName in formNames) {
                try {
                    CloseForm(formName,() => {
                        try {
                            lock (_layerFormStacks) {
                                closeCount++;
                                
                                // 检查是否所有界面都已关闭
                                if (closeCount == totalCount) {
                                    // 清空所有层级的栈
                                    foreach (var stack in _layerFormStacks.Values) {
                                        stack.Clear();
                                    }
                                    onComplete?.Invoke();
                                }
                            }
                        }
                        catch (Exception ex) {
                            lock (_layerFormStacks) {
                                if (!hasError) {
                                    hasError = true;
                                    firstException = ex;
                                    Debug.LogError($"关闭所有界面异常：{ex.Message}\n{ex.StackTrace}");
                                }
                                
                                // 即使出错也要确保回调被调用
                                closeCount++;
                                if (closeCount == totalCount) {
                                    foreach (var stack in _layerFormStacks.Values) {
                                        stack.Clear();
                                    }
                                    onComplete?.Invoke();
                                }
                            }
                        }
                    });
                }
                catch (Exception ex) {
                    lock (_layerFormStacks) {
                        if (!hasError) {
                            hasError = true;
                            firstException = ex;
                            Debug.LogError($"关闭界面调用异常：{formName} - {ex.Message}\n{ex.StackTrace}");
                        }
                        
                        // 继续处理其他界面
                        closeCount++;
                        if (closeCount == totalCount) {
                            foreach (var stack in _layerFormStacks.Values) {
                                stack.Clear();
                            }
                            onComplete?.Invoke();
                        }
                    }
                }
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
            ShowForm<UIFormLoading>(null,() => {
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
        /// 更新指定窗口的loading进度
        /// </summary>
        /// <param name="progress">进度值，范围0-1</param>
        /// <param name="tip">提示文本</param>
        /// <param name="windowId">窗口ID，对应ShowForm时使用的formName</param>
        [Obsolete("UITopWaitingMgr显示Loading界面不在这里提供接口")]
        internal void UpdateLoadingProgress(float progress,string tip = "",string windowId = null) {
            // 如果未指定windowId，则使用默认窗口ID
            //string idToUse = string.IsNullOrEmpty(windowId) ? UITopWaitingMgr.DEFAULT_WINDOW_ID : windowId;
            //UITopWaitingMgr.UpdateProgress(progress, tip, idToUse);
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
        /// 显示已加载的界面（处理层级栈逻辑）- 优化版
        /// 减少重复的状态检查，优化性能
        /// </summary>
        private void ShowLoadedForm(UIFormBase form,UIFormConfig config,object data,Action onComplete) {
            ShowLoadedFormAsync(form,config,data,(formObj) => {
                onComplete?.Invoke();
            });
        }

        private void ShowLoadedFormAsync(UIFormBase form, UIFormConfig config, object data, Action<UIFormBase> onComplete) {
            // 统一的状态检查和处理
            if (!ShouldShowForm(form, config, out string skipReason)) {
                Debug.Log($"界面 {form.Config.FormName} {skipReason}，直接返回现有实例");
                onComplete?.Invoke(form);
                return;
            }
            
            // 只有在界面需要加入栈时才处理同层级逻辑
            if (config.IsAddToStack) {
                HandleStackLayerLogic(form, config, data, onComplete);
            } else {
                // 直接显示，不处理层级栈
                form.Show(data, () => {
                    onComplete?.Invoke(form);
                });
            }
        }

        /// <summary>
        /// 检查界面是否应该显示
        /// </summary>
        private bool ShouldShowForm(UIFormBase form, UIFormConfig config, out string skipReason) {
            skipReason = string.Empty;
            
            // 检查是否已经在显示或正在进行动画
            if (form.IsShowing) {
                skipReason = "已经在显示中";
                return false;
            }
            
            if (form.CurrentState == UIFormState.Hiding) {
                skipReason = "正在进行隐藏动画";
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// 处理层级栈逻辑
        /// </summary>
        private void HandleStackLayerLogic(UIFormBase form, UIFormConfig config, object data, Action<UIFormBase> onComplete) {
            Stack<string> targetStack = _layerFormStacks[config.Layer];
            List<UIFormBase> sameLayerShowingForms = GetSameLayerShowingForms(config.Layer, form);

            // 如果没有同层级界面，直接显示并入栈
            if (sameLayerShowingForms.Count == 0) {
                targetStack.Push(form.FormName);
                form.Show(data, () => {
                    onComplete?.Invoke(form);
                });
                return;
            }

            // 隐藏同层级界面并入栈
            int hideCount = 0;
            foreach (var sameLayerForm in sameLayerShowingForms) {
                // 固定传递 isDestroy=false，仅临时隐藏，不销毁
                sameLayerForm.Hide(false, () => {
                    lock (targetStack) {
                        targetStack.Push(sameLayerForm.FormName);
                        hideCount++;

                        if (hideCount == sameLayerShowingForms.Count) {
                            // 显示当前界面并入栈
                            form.Show(data, () => {
                                lock (targetStack) {
                                    targetStack.Push(form.FormName);
                                }
                                onComplete?.Invoke(form);
                            });
                        }
                    }
                });
            }
        }

        /// <summary>
        /// 获取同层级显示中的界面
        /// </summary>
        private List<UIFormBase> GetSameLayerShowingForms(UILayer layer, UIFormBase excludeForm) {
            List<UIFormBase> sameLayerForms = new List<UIFormBase>();
            
            foreach (var loadedForm in _loadedForms.Values) {
                if (loadedForm.IsShowing && 
                    loadedForm.Config.Layer == layer && 
                    loadedForm != excludeForm) {
                    sameLayerForms.Add(loadedForm);
                }
            }
            
            return sameLayerForms;
        }

        /// <summary>
        /// 异步加载UI预制体
        /// </summary>
        private void LoadUIPrefabAsync(UIFormConfig config,Action<GameObject> onSuccess,Action<string> onFailed) {
            if (config.RefPrefab != null) {
                onSuccess?.Invoke(config.RefPrefab);
                return;
            }
            _currentResourceLoader.LoadUIPrefabAsync(config.PrefabPath,null,config.AssetBundleName,(result) => {
                if (result.Success) {
                    onSuccess?.Invoke(result.Prefab);
                }
                else {
                    onFailed?.Invoke(result.ErrorMessage);
                }
            });
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
        public bool IsFormShowing<T>() where T : UIFormBase {
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
        public bool IsFormLoading<T>() where T : UIFormBase {
            string formName = typeof(T).Name;
            return IsFormLoading(formName);
        }
        /// <summary>
        /// 获取指定层级的栈顶界面名称 - 优化版
        /// 改进并发安全性
        /// </summary>
        public string GetLayerTopFormName(UILayer layer) {
            lock (_layerFormStacks) {
                if (_layerFormStacks.TryGetValue(layer, out var stack) && stack != null && stack.Count > 0) {
                    try {
                        return stack.Peek();
                    }
                    catch (InvalidOperationException) {
                        // 栈为空时返回null
                        return null;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// 安全地移除层级栈中的界面 - 优化版
        /// 改进并发安全性和错误处理
        /// </summary>
        /// <param name="layer"></param>
        /// <param name="formName"></param>
        /// <returns>是否成功移除</returns>
        private bool SafeRemoveFromLayerStack(UILayer layer, string formName) {
            if (string.IsNullOrEmpty(formName)) {
                return false;
            }

            lock (_layerFormStacks) {
                if (_layerFormStacks.TryGetValue(layer, out var stack) && stack != null) {
                    try {
                        // 使用临时列表来避免在遍历中修改集合
                        var tempList = new List<string>(stack);
                        bool found = false;
                        
                        for (int i = tempList.Count - 1; i >= 0; i--) {
                            if (tempList[i] == formName) {
                                tempList.RemoveAt(i);
                                found = true;
                                break;
                            }
                        }
                        
                        if (found) {
                            // 重建栈
                            _layerFormStacks[layer] = new Stack<string>(tempList.Reverse<string>());
                            return true;
                        }
                    }
                    catch (Exception ex) {
                        Debug.LogError($"从层级栈移除界面异常: {layer} - {formName} - {ex.Message}");
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 安全地清空层级栈 - 优化版
        /// 改进并发安全性
        /// </summary>
        /// <param name="layer"></param>
        private void SafeClearLayerStack(UILayer layer) {
            lock (_layerFormStacks) {
                if (_layerFormStacks.TryGetValue(layer, out var stack) && stack != null) {
                    try {
                        stack.Clear();
                    }
                    catch (Exception ex) {
                        Debug.LogError($"清空层级栈异常: {layer} - {ex.Message}");
                    }
                }
            }
        }
        #endregion


    }



}
