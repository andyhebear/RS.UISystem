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
    /// 普通弹窗（内置界面）
    /// </summary>
    public class UIPopup : UIForm
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _contentText;
        [SerializeField] private Button _confirmBtn;
        [SerializeField] private Button _cancelBtn;
        [SerializeField] private Button _closeBtn;
        [SerializeField] private RectTransform _popupRoot;
        //[SerializeField] private CanvasGroup _canvasGroup;

        private Action _onConfirm;
        private Action _onCancel;

        protected override void Awake() {
            base.Awake();

            //if (_canvasGroup == null)
            //    _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            // 绑定按钮事件
            _confirmBtn?.onClick.AddListener(OnConfirmClick);
            _cancelBtn?.onClick.AddListener(OnCancelClick);
            _closeBtn?.onClick.AddListener(OnCancelClick);
        }

        /// <summary>
        /// 显示弹窗（外部调用接口）
        /// </summary>
        public void ShowPopup(string title,string content,Action onConfirm = null,Action onCancel = null,Action onComplete = null) {
            _titleText.text = title;
            _contentText.text = content;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            Show(null,onComplete);
        }

        /// <summary>
        /// 显示动画（缩放+淡入）
        /// </summary>
        protected override IEnumerator ShowAnimationCoroutine(Action onComplete) {
            _popupRoot.localScale = Vector3.zero;
            if (_canvasGroup != null) {
                _canvasGroup.alpha = 0;
            }

            float duration = 0.2f;
            float time = 0;
            while (time < duration) {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                _popupRoot.localScale = Vector3.Lerp(Vector3.one * 0.01f,Vector3.one,t);
                if (_canvasGroup != null) {
                    _canvasGroup.alpha = Mathf.Lerp(0,1,t);
                }
                yield return null;
            }

            _popupRoot.localScale = Vector3.one;
            if (_canvasGroup != null) {
                _canvasGroup.alpha = 1;
            }
            OnShowComplete();
            onComplete?.Invoke();
        }

        /// <summary>
        /// 隐藏动画（缩放+淡出）
        /// </summary>
        protected override IEnumerator HideAnimationCoroutine(bool isDestroy,Action onComplete) {
            float duration = 0.2f;
            float time = 0;
            while (time < duration) {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / duration);
                _popupRoot.localScale = Vector3.Lerp(Vector3.one,Vector3.one * 0.01f,t);
                if (_canvasGroup != null) {
                    _canvasGroup.alpha = Mathf.Lerp(1,0,t);
                }
                yield return null;
            }

            gameObject.SetActive(false);
            OnHideComplete(isDestroy);
            onComplete?.Invoke();
        }

        private void OnConfirmClick() {
            _onConfirm?.Invoke();
            Close();
        }

        private void OnCancelClick() {
            _onCancel?.Invoke();
            Close();
        }

        protected override void SubscribeEvents() {
            // 无需订阅事件
        }

        protected override void UnsubscribeAllEvents() {
            // 无需取消订阅
        }

        protected override void OnDestroy() {
            base.OnDestroy();
            _onConfirm = null;
            _onCancel = null;
        }
    }


}
