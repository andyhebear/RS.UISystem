using RS.Unity3DLib.UISystem.UIEffect;
using RS.Unity3DLib.UISystem.UIEx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>  
    /// UI界面核心接口，定义生命周期、状态管理与核心功能规范
    /// 所有UI界面基类必须实现此接口
    ///UIForm接口 生命周期：「初始化 → 显示 → 隐藏 → 回收 / 销毁」
    /// </summary>
    public interface IUIForm
    {
        #region 公共属性（状态与配置）
        /// <summary>
        /// 界面唯一标识（与UIFormConfig.FormName一致）
        /// </summary>
        string FormName { get; }

        /// <summary>
        /// 界面配置信息
        /// </summary>
        UIFormConfig Config { get; }

        /// <summary>
        /// 当前界面状态（严格控制流转）
        /// </summary>
        UIFormState CurrentState { get; }

        /// <summary>
        /// 是否处于显示中状态（包含显示动画过程）
        /// </summary>
        bool IsShowing { get; }
        #endregion

        #region 核心生命周期
        /// <summary>
        /// 初始化界面（绑定配置、设置层级、初始化基础组件）
        /// 状态流转：Uninitialized → Loaded
        /// </summary>
        /// <param name="config">界面配置</param>
        IEnumerator Init(UIFormConfig config,object data,bool refreshDataWithFromPoolRestore);

        /// <summary>
        /// 显示界面（触发显示动画，激活交互）
        /// 状态流转：Loaded → Showing → Shown
        /// </summary>
        /// <param name="data">传递给界面的数据</param>
        /// <param name="onComplete">显示完成回调</param>
        void Show(object data = null,Action onComplete = null);

        /// <summary>
        /// 隐藏界面（触发隐藏动画，禁用交互）
        /// 状态流转：Shown → Hiding → Loaded（临时隐藏）/ Destroyed（销毁）
        /// </summary>
        /// <param name="isDestroying">是否彻底销毁</param>
        /// <param name="onComplete">隐藏完成回调</param>
        void Hide(bool isDestroying,Action onComplete = null);

        ///// <summary>
        ///// 回收至对象池（清空数据，保留实例，等待复用）
        ///// 状态流转：Loaded → Uninitialized
        ///// </summary>
        //void Recycle();


        #endregion

        #region 事件订阅管理（避免内存泄漏）
        /// <summary>
        /// 订阅事件（初始化/恢复时调用）
        /// </summary>
        void SubscribeEvents();

        /// <summary>
        /// 取消所有事件订阅（回收/销毁时调用）
        /// </summary>
        void UnsubscribeAllEvents();
        #endregion

        #region 动画接口
        ///// <summary>
        ///// 显示动画协程
        ///// </summary>
        ///// <param name="onComplete">动画完成回调</param>
        //IEnumerator ShowAnimationCoroutine(Action onComplete);

        ///// <summary>
        ///// 隐藏动画协程
        ///// </summary>
        ///// <param name="onComplete">动画完成回调</param>
        //IEnumerator HideAnimationCoroutine(Action onComplete);
        #endregion

        #region 内部状态控制（仅供框架调用）
        ///// <summary>
        ///// 强制设置界面状态（框架内部使用，禁止外部调用）
        ///// </summary>
        ///// <param name="state">目标状态</param>
        //void SetState(UIFormState state);
        #endregion
    }

    /// <summary>
    /// UI界面基类，所有UI界面都需继承此类,生命周期：「初始化 → 显示 → 隐藏 → 回收 / 销毁」
    /// </summary>
    public abstract class UIFormBase : MonoBehaviour, IUIForm
    {

        #region IUIForm 接口属性实现
        public string FormName { get; private set; }
        public UIFormConfig Config { get; protected set; }
        public UIFormState CurrentState { get; private set; } = UIFormState.Uninitialized;
        internal void _setFormState(UIFormState state) {
            CurrentState = state;
        }
        public bool IsShowing => CurrentState == UIFormState.Shown || CurrentState == UIFormState.Showing;
        #endregion

        #region    #region 保护字段（子类可访问，基类管理）
        protected object Data;
        /// <summary>
        /// 初始默认隐藏页面后 data就设置为null,现在已经改造
        /// 当执行 _realse时候 data才设置为null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T getDataT<T>() {
            return (T)Data;
        }
        public void setData(object data) {
            Data = data;
        }
        /// <summary>
        /// 层级父节点（由UIManager分配）
        /// </summary>
        protected Transform _layerParent;
        //
        [SerializeField] protected CanvasGroup _canvasGroup;
        //protected RectTransform _mask; // 遮罩组件引用
        protected RawImage _maskRawImage;
        protected UIRawImageBackgroundBlur _maskBlurComponent; // Kawase模糊组件
        protected bool _closeOnMaskClick = false; // 点击遮罩是否关闭界面
        #endregion

        protected virtual void Awake() {
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                    _canvasGroup.blocksRaycasts = true;
                }
            }
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            // 初始化遮罩（延迟到Init配置）
            //Debug.Log($"Awake CreateMask:{this.GetType().Name}");
            gameObject.SetActive(false);

        }

        protected virtual void Start() { }

        protected virtual void OnDestroy() {
            CurrentState = UIFormState.Destroyed;
            UnsubscribeAllEvents();
            // 彻底隐藏遮罩并释放资源
            HideMask(true);
            if (_maskRawImage != null) {
                Destroy(_maskRawImage.gameObject);
            }
        }
#if UNITY_EDITOR
        private void OnValidate() {
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
#endif
        /// <summary>
        /// 初始化界面（UIManager调用）
        /// </summary>
        public IEnumerator Init(UIFormConfig config,object data,bool resetUI) {
            if (CurrentState == UIFormState.Destroyed) {
                Debug.LogError($"UIForm {config.FormName} 已销毁，无法初始化");
                yield break;
            }
            if (CurrentState != UIFormState.Uninitialized) {
                Debug.LogWarning($"[{FormName}] 重复初始化，当前状态：{CurrentState}");
                //return;
            }
            Config = config;
            FormName = config.FormName;
            //          
            // 初始化基础组件（CanvasGroup、RectTransform）
            InitializeComponents(config);
            // 初始化遮罩（根据配置）
            InitializeMask(config);
            
            // 执行子类异步初始化
            yield return OnInitializedAsync(config);
            
            // 初始化状态
            CurrentState = UIFormState.Loaded;
            if (resetUI) {
                // 子类扩展：重置
                OnUIFormReseted(data);
            }
            SubscribeEvents();
            yield return null;
        }
        /// <summary>
        /// 子类异步初始化（虚方法，子类可重写，支持耗时操作）
        /// </summary>
        /// <param name="config">界面配置</param>
        protected virtual IEnumerator OnInitializedAsync(UIFormConfig config) { 
            // 默认实现调用原有的同步初始化方法
            OnInitialized(config);
            yield return null;
        }
        
        /// <summary>
        /// 子类初始化（同步版本，保持向后兼容）
        /// </summary>
        /// <param name="config">界面配置</param>
        protected virtual void OnInitialized(UIFormConfig config) { }
        /// <summary>
        /// 数据刷新回调（子类重写，刷新UI显示）
        /// </summary>
        protected abstract void OnUIFormReseted(object data);
        private void InitializeComponents(UIFormConfig config) {
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.alpha = 0;
            _canvasGroup.interactable = false;
            //_canvasGroup.blocksRaycasts = false;
            _layerParent = UIManager.Instance.GetLayerParent(config.Layer);
            if (_layerParent != null) {
                transform.SetParent(_layerParent);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;

                // 修正RectTransform配置，避免UI偏移
                RectTransform rectTrans = this.GetComponent<RectTransform>();
                if (rectTrans != null) {
                    rectTrans.anchorMin = Vector2.zero;
                    rectTrans.anchorMax = Vector2.one;
                    rectTrans.offsetMin = Vector2.zero;
                    rectTrans.offsetMax = Vector2.zero;
                    rectTrans.pivot = new Vector2(0.5f,0.5f);
                }
            }
            else {
                Debug.LogError($"UIForm: 未找到层级父节点 {config.Layer}");
            }
        }
        #region mask
        /// <summary>
        /// 根据配置初始化遮罩
        /// </summary>
        private void InitializeMask(UIFormConfig config) {
            if (config.MaskType == UIMaskType.None) {
                if (_maskRawImage != null) { HideMask(); }
                return;
            }
            if (_maskRawImage != null) {
                //UpdateMaskType(config.MaskType);
                return;
            }
            createMask();
            RectTransform rect = _maskRawImage.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();

            _maskRawImage.raycastTarget = true; // 阻挡点击
            _maskRawImage.maskable = false;
            UpdateMaskType(config.MaskType);
            BindMaskClick();

        }
        private void UpdateMaskType(UIMaskType maskType) {
            if (_maskRawImage == null) return;

            switch (maskType) {
                case UIMaskType.None:
                    _maskRawImage.gameObject.SetActive(false);
                    RemoveBlurComponent();
                    break;
                case UIMaskType.Translucent:
                    _maskRawImage.gameObject.SetActive(true);
                    _maskRawImage.texture = null;
                    _maskRawImage.color = new Color(0.8f,0.8f,0.8f,0.5f);
                    RemoveBlurComponent();
                    break;
                case UIMaskType.Transparent:
                    _maskRawImage.gameObject.SetActive(true);
                    _maskRawImage.texture = null;
                    _maskRawImage.color = new Color(1,1,1,0);
                    RemoveBlurComponent();
                    break;
                case UIMaskType.Blurred:
                    _maskRawImage.gameObject.SetActive(true);
                    _maskRawImage.color = new Color(0.8f,0.8f,0.8f,0.95f);
                    AddAndConfigureBlurComponent();
                    break;
            }
        }
        protected void createMask() {
            if (_maskRawImage != null) return;
            GameObject maskObj = new GameObject("Mask",typeof(RectTransform),typeof(RawImage),typeof(Button));
            //var _mask = maskObj.GetComponent<RectTransform>();
            maskObj.transform.SetParent(this.transform);
            maskObj.layer = LayerMask.NameToLayer("UI");
            //
            // 初始化 RawImage（基础遮罩用纯色，模糊遮罩用动态纹理）
            var maskRawImage = maskObj.GetComponent<RawImage>();
            this._maskRawImage = maskRawImage;
            // RawImage基础配置（适配全屏模糊）
            _maskRawImage.uvRect = new Rect(0,0,1,1); // 全屏显示纹理
            _maskRawImage.raycastTarget = true; // 允许射线检测（阻挡点击穿透）
                                                //_maskRawImage.enabled = true;
            {
                //Shader blurShader = Resources.Load<Shader>("Shaders/StaticBlur");
                //if (blurShader != null) {
                //    var _blurMaterial = new Material(blurShader);
                //    _blurMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
                //    maskRawImage.effectMaterial = _blurMaterial; // 初始使用指定材质
                //}
                //else {
                //    Debug.LogError("UIRawImageBlur: 未找到 Kawase 模糊Shader（路径：Resources/Shaders/KawaseBlurShader）");
                //    enabled = false;
                //}
            }
            // 隐藏Button默认视觉效果（避免遮罩显示多余样式）
            Button maskBtn = maskObj.GetComponent<Button>();
            maskBtn.transition = Selectable.Transition.None;
            maskBtn.targetGraphic = _maskRawImage;
        }
        private void AddAndConfigureBlurComponent() {
            //return;
            _maskBlurComponent = _maskRawImage.GetComponent<UIRawImageBackgroundBlur>();
            if (_maskBlurComponent == null)
                _maskBlurComponent = _maskRawImage.gameObject.AddComponent<UIRawImageBackgroundBlur>();
            // Kawase 模糊最优参数（平衡速度和效果）
            //_maskBlurComponent.blurRadius = 10; // 模糊半径（1-10 可调）
            //_maskBlurComponent.downSample = 2; // 降采样（2=1/2尺寸，性能最优）
            //_maskBlurComponent.ApplyBlur(); // 应用模糊效果
        }

        // 移除模糊组件（避免不同遮罩类型冲突）
        private void RemoveBlurComponent() {
            if (_maskBlurComponent != null) {
                Destroy(_maskBlurComponent);
                _maskBlurComponent = null;
            }
        }
        /// <summary>
        /// 绑定遮罩点击事件
        /// </summary>
        private void BindMaskClick() {
            Button maskBtn = _maskRawImage.GetComponent<Button>() ?? _maskRawImage.gameObject.AddComponent<Button>();
            maskBtn.onClick.RemoveAllListeners();
            maskBtn.onClick.AddListener(OnMaskClick);
        }
        /// <summary>
        /// 遮罩点击回调
        /// </summary>
        protected virtual void OnMaskClick() {
            if (_closeOnMaskClick) {
                Close();
            }
        }
        /// <summary>
        /// 显示遮罩
        /// </summary>
        protected void ShowMask() {
            if (_maskRawImage != null && Config.MaskType != UIMaskType.None) {
                _maskRawImage.gameObject.SetActive(true);
                //_maskRawImage.enabled = true;
                // 重新应用遮罩类型配置，确保模糊组件存在
                UpdateMaskType(Config.MaskType);
            }
        }

        /// <summary>
        /// 隐藏遮罩
        /// </summary>
        protected void HideMask(bool isDestroying = false) {
            if (_maskRawImage != null) {
                _maskRawImage.gameObject.SetActive(false);
                //_maskRawImage.enabled = false;

                // 仅在彻底销毁时才移除模糊组件
                if (isDestroying) {
                    if (_maskBlurComponent != null) {
                        Destroy(_maskBlurComponent);
                        _maskBlurComponent = null;
                    }
                    // 释放纹理资源
                    //if (_maskRawImage.texture is RenderTexture rt) {
                    //    RenderTexture.ReleaseTemporary(rt);
                    //    _maskRawImage.texture = null;
                    //}
                }
            }
        }
        #endregion

        /// <summary>
        /// 显示界面（外部调用入口）
        /// </summary>
        public virtual void Show(object data = null,Action onComplete = null) {
            if (CurrentState == UIFormState.Destroyed) {
                Debug.LogError($"UIForm:[ {FormName} ]已销毁，无法显示");
                onComplete?.Invoke();
                return;
            }
            if (IsShowing) {
                onComplete?.Invoke();
                return;
            }
            Data = data;
            gameObject.SetActive(true);
            CurrentState = UIFormState.Showing;
            _canvasGroup.interactable = false; // 显示动画期间禁止交互
            //_canvasGroup.blocksRaycasts = true;
            // 显示遮罩         
            ShowMask();
            // 子类扩展：显示前回调
            OnShowBefore();
            // 执行显示动画
            UIManager.Instance.StartCoroutine(ShowAnimationCoroutine(onComplete));
        }
        /// <summary>
        /// 显示动画（子类可重写）
        /// </summary>
        protected IEnumerator ShowAnimationCoroutine(Action onComplete) {
            var ve = doShowAnimation();
            while (ve.MoveNext()) {
                yield return ve.Current;
            }
            _canvasGroup.interactable = true;
            _canvasGroup.alpha = 1f;
            CurrentState = UIFormState.Shown;
            OnShowCompleted();
            onComplete?.Invoke();
        }
        protected virtual IEnumerator doShowAnimation() {
            this.transform.localScale = Vector3.one * 0.01f;
            float duration = 0.3f;
            float time = 0;
            while (time < duration) {
                time += Time.deltaTime;
                float t = time / duration;
                this.transform.localScale = Vector3.Lerp(Vector3.one * 0.01f,Vector3.one,t);
                _canvasGroup.alpha = Mathf.Lerp(0,1,t);
                yield return null;
            }
            this.transform.localScale = Vector3.one;
            _canvasGroup.alpha = 1f;
        }
        /// <summary>
        /// 显示动画执行前回调（子类重写，处理显示动画前的逻辑）
        /// </summary>
        protected abstract void OnShowBefore();
        /// <summary>
        /// 显示动画完成回调（子类重写，处理显示后的逻辑）
        /// </summary>
        protected abstract void OnShowCompleted();

        /// <summary>
        /// 隐藏界面（外部调用入口）
        /// </summary>
        public virtual void Hide(bool isDestroying,Action onComplete = null) {
            if (!IsShowing) {
                onComplete?.Invoke();
                return;
            }
            CurrentState = UIFormState.Hiding;
            _canvasGroup.interactable = false;
            //_canvasGroup.blocksRaycasts = false;
            // 隐藏遮罩
            HideMask(isDestroying);
            // 子类扩展：隐藏前回调
            OnHideBefore();
            // 执行隐藏动画
            UIManager.Instance.StartCoroutine(HideAnimationCoroutine(isDestroying,onComplete));

        }

        /// <summary>
        /// 隐藏动画（子类可重写）
        /// </summary>
        protected IEnumerator HideAnimationCoroutine(bool isDestroying,Action onComplete) {
            var ve = doHideAnimation();
            while (ve.MoveNext()) {
                yield return ve.Current;
            }
            // 
            gameObject.SetActive(false);
            //_canvasGroup.alpha = 0f;
            //_canvasGroup.interactable = false;
            //_canvasGroup.blocksRaycasts = false;
            OnHideCompleted(isDestroying);
            CurrentState = isDestroying ? UIFormState.Destroyed : UIFormState.Loaded;

            onComplete?.Invoke();
        }
        protected virtual IEnumerator doHideAnimation() {
            //var localscale = this.transform.localScale;
            float duration = 0.3f;
            float time = 0;
            while (time < duration) {
                time += Time.deltaTime;
                float t = time / duration;
                //this.transform.localScale = Vector3.Lerp(localscale,Vector3.one * 0.01f,t);
                _canvasGroup.alpha = Mathf.Lerp(1,0.5f,t);
                yield return null;
            }
            //_canvasGroup.alpha = 0f;
        }
        protected abstract void OnHideBefore();
        /// <summary>
        /// 隐藏完成回调（子类重写）
        /// </summary>
        protected abstract void OnHideCompleted(bool isDestroying);
        /// <summary>
        /// 回收到缓存池中
        /// </summary>
        internal void Recycle() {
            if (CurrentState == UIFormState.Destroyed) return;

            // 回收逻辑：取消订阅 + 清空数据 + 状态重置 + 隐藏对象
            UnsubscribeAllEvents();
            Data = null;
            CurrentState = UIFormState.Uninitialized;
            gameObject.SetActive(false);
            // 子类扩展：回收完成回调
            OnUIFormRecycled();
        }
        protected abstract void OnUIFormRecycled();
        /// <summary>
        /// 关闭界面（外部调用入口，触发隐藏并回收）
        /// </summary>
        public void Close() {
            UIManager.Instance.CloseForm(FormName);
        }

        /// <summary>
        /// 事件订阅（子类重写，统一管理订阅）
        /// </summary>
        public abstract void SubscribeEvents();

        /// <summary>
        /// 取消所有事件订阅（子类必须重写，避免内存泄漏）
        /// </summary>
        public abstract void UnsubscribeAllEvents();



    }
}
