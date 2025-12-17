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
    /// UI界面基类，所有UI界面都需继承此类
    /// </summary>
    public abstract class UIForm : MonoBehaviour
    {
        public string FormName { get; private set; }
        public UIFormConfig Config { get; private set; }
        public bool IsShowing { get; private set; }
        protected object Data { get; private set; }

        private Transform _layerParent;
        //private Canvas _canvas;
        [SerializeField] protected CanvasGroup _canvasGroup;
        protected virtual void Awake() {
            //_canvas = GetComponent<Canvas>();
            //if (_canvas == null) {
            //    _canvas = gameObject.AddComponent<Canvas>();
            //    gameObject.AddComponent<GraphicRaycaster>();
            //}
          
          
        }

        protected virtual void Start() { }

        protected virtual void OnDestroy() {
            // 强制取消事件订阅，避免内存泄漏
            UnsubscribeAllEvents();
        }

        /// <summary>
        /// 初始化界面（绑定配置+挂载到对应层级）
        /// </summary>
        public virtual void Init(UIFormConfig config) {
            Config = config;
            FormName = config.FormName;

            // 挂载到对应层级节点
            _layerParent = UIManager.Instance.GetLayerParent(config.Layer);
            if (_layerParent != null) {
                transform.SetParent(_layerParent);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale = Vector3.one;

                // 修正RectTransform配置，避免UI偏移
                RectTransform rectTrans = GetComponent<RectTransform>();
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

            // 设置渲染层级
            SetLayerSortingOrder(config.Layer);

            // 订阅事件
            SubscribeEvents();
        }

        /// <summary>
        /// 设置界面渲染层级（确保渲染顺序正确）
        /// </summary>
        private void SetLayerSortingOrder(UILayer layer) {
            //if (_canvas != null) {
            //    _canvas.overrideSorting = true;
            //    _canvas.sortingOrder = (int)layer * 100; // 每个层级间隔100，便于插入子界面
            //}
            if (_canvasGroup == null) {
                _canvasGroup = GetComponent<CanvasGroup>();
                if (_canvasGroup == null) {
                    _canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 显示界面（外部调用入口）
        /// </summary>
        public void Show(object data = null,Action onComplete = null) {
            Data = data;
            gameObject.SetActive(true);
            IsShowing = true;

            // 执行显示动画
            StartCoroutine(ShowAnimationCoroutine(onComplete));
        }

        /// <summary>
        /// 显示动画（子类可重写）
        /// </summary>
        protected virtual IEnumerator ShowAnimationCoroutine(Action onComplete) {
            // 默认无动画，直接回调
            yield return null;
            OnShowComplete();
            onComplete?.Invoke();
        }

        /// <summary>
        /// 显示完成回调（子类重写，处理显示后的逻辑）
        /// </summary>
        protected virtual void OnShowComplete() {
            _canvasGroup.alpha = 1f;
            _canvasGroup.interactable = true;
            _canvasGroup.blocksRaycasts = true;
        }

        /// <summary>
        /// 隐藏界面（外部调用入口）
        /// </summary>
        public void Hide(bool isDestroy = false,Action onComplete = null) {
            IsShowing = false;
            //_canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
            // 执行隐藏动画
            StartCoroutine(HideAnimationCoroutine(isDestroy,onComplete));
        }

        /// <summary>
        /// 隐藏动画（子类可重写）
        /// </summary>
        protected virtual IEnumerator HideAnimationCoroutine(bool isDestroy,Action onComplete) {
            // 默认无动画，直接回调
            yield return null;
            gameObject.SetActive(false);
            OnHideComplete(isDestroy);
            onComplete?.Invoke();
        }

        /// <summary>
        /// 隐藏完成回调（子类重写）
        /// </summary>
        protected virtual void OnHideComplete(bool isDestroy) {
            _canvasGroup.alpha = 0f;
            _canvasGroup.interactable = false;
            _canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// 关闭界面（外部调用入口，触发隐藏并回收）
        /// </summary>
        public void Close() {
            UIManager.Instance.CloseForm(FormName);
        }

        /// <summary>
        /// 事件订阅（子类重写，统一管理订阅）
        /// </summary>
        protected abstract void SubscribeEvents();

        /// <summary>
        /// 取消所有事件订阅（子类必须重写，避免内存泄漏）
        /// </summary>
        protected abstract void UnsubscribeAllEvents();
    }
}
