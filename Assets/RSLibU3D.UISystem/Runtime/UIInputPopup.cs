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
    /// 输入框弹窗（内置界面）
    /// </summary>
    public class UIInputPopup : UIForm
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _hintText;
        [SerializeField] private InputField _inputField;
        [SerializeField] private Button _confirmBtn;
        [SerializeField] private Button _cancelBtn;
        [SerializeField] private Button _closeBtn;
        [SerializeField] private RectTransform _popupRoot;
        //[SerializeField] private CanvasGroup _canvasGroup;

        private InputFieldType _inputType;
        private string _defaultValue;
        private int _maxLength;
        private Action<string> _onConfirm;
        private Action _onCancel;

        protected override void Awake() {
            base.Awake();

            //if (_canvasGroup == null)
            //    _canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (_inputField == null) {
                Debug.LogError("UIInputPopup: 未赋值 InputField 组件");
                return;
            }

            // 绑定按钮事件
            _confirmBtn?.onClick.AddListener(OnConfirmClick);
            _cancelBtn?.onClick.AddListener(OnCancelClick);
            _closeBtn?.onClick.AddListener(OnCancelClick);

            // 初始化输入框默认配置
            _inputField.contentType = InputField.ContentType.Standard;
            _inputField.characterLimit = 20;
        }

        /// <summary>
        /// 显示输入框弹窗（外部调用接口）
        /// </summary>
        public void ShowInputPopup(string title,string hint,InputFieldType inputType = InputFieldType.Normal,
                                   string defaultValue = "",int maxLength = 20,Action<string> onConfirm = null,
                                   Action onCancel = null,Action onComplete = null) {
            if (_inputField == null) {
                onComplete?.Invoke();
                return;
            }

            // 赋值配置和回调
            _titleText.text = title;
            _hintText.text = hint;
            _inputType = inputType;
            _defaultValue = defaultValue;
            _maxLength = maxLength;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // 配置输入框
            UpdateInputFieldConfig();

            // 显示弹窗
            Show(null,onComplete);
        }

        /// <summary>
        /// 更新输入框配置
        /// </summary>
        private void UpdateInputFieldConfig() {
            _inputField.text = _defaultValue;
            _inputField.characterLimit = _maxLength;

            switch (_inputType) {
                case InputFieldType.Normal:
                    _inputField.contentType = InputField.ContentType.Standard;
                    _inputField.inputType = InputField.InputType.Standard;
                    _inputField.placeholder.GetComponent<Text>().text = "请输入内容";
                    break;
                case InputFieldType.Password:
                    _inputField.contentType = InputField.ContentType.Password;
                    _inputField.inputType = InputField.InputType.Password;
                    _inputField.placeholder.GetComponent<Text>().text = "请输入密码";
                    break;
            }

            // 自动激活输入框
            _inputField.ActivateInputField();
        }

        /// <summary>
        /// 显示动画
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
            _inputField?.ActivateInputField();
            OnShowComplete();
            onComplete?.Invoke();
        }

        /// <summary>
        /// 隐藏动画
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
            string inputText = _inputField?.text.Trim() ?? "";
            _onConfirm?.Invoke(inputText);
            Close();
        }

        private void OnCancelClick() {
            _onCancel?.Invoke();
            Close();
        }

        /// <summary>
        /// 清空输入框（外部调用接口）
        /// </summary>
        public void ClearInput() {
            _inputField.text = "";
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
