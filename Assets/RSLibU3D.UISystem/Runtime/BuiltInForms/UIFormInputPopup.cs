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
    internal class UIFormInputPopup : UIFormBase
    {
        [SerializeField] private Text _titleText;//标题
        [SerializeField] private Text _hintText;//提示词
        [SerializeField] private InputField _inputField;
        [SerializeField] private Button _confirmBtn;
        [SerializeField] private Text _confirmBtnText;
        [SerializeField] private Button _cancelBtn;
        [SerializeField] private Text _cancelBtnText;

        private InputType _inputType;
        private bool _isPassword;
        private Action<object> _onConfirm;
        private Action _onCancel;

        protected override void Awake() {
            base.Awake();
            _closeOnMaskClick = true; // 点击遮罩关闭

            // 绑定按钮事件
            _confirmBtn?.onClick.AddListener(OnConfirmClick);
            _cancelBtn?.onClick.AddListener(OnCancelClick);

            // 初始化按钮文本
            _confirmBtnText?.SetText("确定");
            _cancelBtnText?.SetText("取消");
        }

       

        /// <summary>
        /// 显示输入框弹窗
        /// </summary>
        public void ShowInputPopup(string title,string hint,InputType inputType,bool isPassword = false,
                                  string defaultValue = "",int maxLength = 20,
                                  Action<object> onConfirm = null,Action onCancel = null,
                                  Action onComplete = null) {
            _titleText?.SetText(title);
            _hintText?.SetText(hint);
            _inputType = inputType;
            _isPassword = isPassword;
            _onConfirm = onConfirm;
            _onCancel = onCancel;

            // 配置输入框
            UpdateInputFieldConfig(defaultValue,maxLength);

            Show(null,onComplete);

            // 自动激活输入框
            _inputField?.ActivateInputField();
        }

        /// <summary>
        /// 更新输入框配置
        /// </summary>
        private void UpdateInputFieldConfig(string defaultValue,int maxLength) {
            if (_inputField == null) return;

            _inputField.text = defaultValue;
            _inputField.characterLimit = maxLength;
            _inputField.contentType = GetInputContentType();
            _inputField.inputType = _isPassword ? InputField.InputType.Password : InputField.InputType.Standard;
            _inputField.placeholder.GetComponent<Text>()?.SetText(GetPlaceholderText());
        }

        /// <summary>
        /// 根据输入类型获取Content Type
        /// </summary>
        private InputField.ContentType GetInputContentType() {
            switch (_inputType) {
                case InputType.Integer:
                    return InputField.ContentType.IntegerNumber;
                case InputType.Float:
                    return InputField.ContentType.DecimalNumber;
                default:
                    return InputField.ContentType.Standard;
            }
        }

        /// <summary>
        /// 获取占位文本
        /// </summary>
        private string GetPlaceholderText() {
            if (_isPassword)
                return "请输入密码";

            switch (_inputType) {
                case InputType.Integer:
                    return "请输入整数";
                case InputType.Float:
                    return "请输入数字";
                default:
                    return "请输入内容";
            }
        }

        /// <summary>
        /// 确认按钮点击
        /// </summary>
        private void OnConfirmClick() {
            if (_inputField == null) {
                _onConfirm?.Invoke(null);
                Close();
                return;
            }

            string inputText = _inputField.text.Trim();
            object result = null;

            try {
                switch (_inputType) {
                    case InputType.Integer:
                        result = int.Parse(inputText);
                        break;
                    case InputType.Float:
                        result = float.Parse(inputText);
                        break;
                    case InputType.String:
                        result = inputText;
                        break;
                }
            }
            catch (Exception e) {
                Debug.LogWarning($"输入类型转换失败：{e.Message}");
                //ShowPopup("输入错误","请输入正确格式的数据",DialogButtonType.OK,null);
                return;
            }

            _onConfirm?.Invoke(result);
            Close();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void OnCancelClick() {
            _onCancel?.Invoke();
            Close();
        }

        protected override void OnInitialized(UIFormConfig config) {
            Config.MaskType = UIMaskType.Translucent;
            //Config.MaskColor = new Color(0,0,0,0.3f);
            createMask();
            ShowMask();
            if (_maskRawImage != null) {
                _maskRawImage.color = new Color(0,0,0,0.3f);
            }
        }

        protected override void OnUIFormReseted(object data) {
          
        }

        protected override void OnShowBefore() {
          
        }

        protected override void OnShowCompleted() {
            
        }

        protected override void OnHideBefore() {
            
        }

        protected override void OnHideCompleted(bool isDestroy) {
           
        }

        protected override void OnUIFormRecycled() {
         
        }

        public override void SubscribeEvents() {
           
        }

        public override void UnsubscribeAllEvents() {
        
        }
    }


}
