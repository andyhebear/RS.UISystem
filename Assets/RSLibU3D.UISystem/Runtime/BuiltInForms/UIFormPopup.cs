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
    internal class UIFormPopup : UIFormBase
    {
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _contentText;
        [SerializeField] private Button _okBtn;
        [SerializeField] private Text _okBtnText;
        [SerializeField] private Button _cancelBtn;
        [SerializeField] private Text _cancelBtnText;
        [SerializeField] private Button _yesBtn;
        [SerializeField] private Text _yesBtnText;
        [SerializeField] private Button _noBtn;
        [SerializeField] private Text _noBtnText;

        private Action<DialogButtonResult> _onResult;

        protected override void Awake() {
            base.Awake();
            _closeOnMaskClick = true; // 点击遮罩关闭弹窗

            // 绑定按钮事件
            _okBtn?.onClick.AddListener(() => OnButtonClick(DialogButtonResult.OK));
            _cancelBtn?.onClick.AddListener(() => OnButtonClick(DialogButtonResult.Cancel));
            _yesBtn?.onClick.AddListener(() => OnButtonClick(DialogButtonResult.Yes));
            _noBtn?.onClick.AddListener(() => OnButtonClick(DialogButtonResult.No));

            // 初始化按钮文本
            _okBtnText?.SetText("确定");
            _cancelBtnText?.SetText("取消");
            _yesBtnText?.SetText("是");
            _noBtnText?.SetText("否");
        }

    

        /// <summary>
        /// 显示通用弹窗
        /// </summary>
        public void ShowPopup(string title,string content,DialogButtonType buttonType,
                             Action<DialogButtonResult> onResult,Action onComplete = null) {
            _titleText?.SetText(title);
            _contentText?.SetText(content);
            _onResult = onResult;

            // 更新按钮显示
            UpdateButtonVisibility(buttonType);

            Show(null,onComplete);
        }

        /// <summary>
        /// 根据按钮类型更新显示状态
        /// </summary>
        private void UpdateButtonVisibility(DialogButtonType buttonType) {
            // 隐藏所有按钮
            _okBtn?.gameObject.SetActive(false);
            _cancelBtn?.gameObject.SetActive(false);
            _yesBtn?.gameObject.SetActive(false);
            _noBtn?.gameObject.SetActive(false);

            // 显示对应按钮
            switch (buttonType) {
                case DialogButtonType.OK:
                    _okBtn?.gameObject.SetActive(true);
                    break;
                case DialogButtonType.OKCancel:
                    _okBtn?.gameObject.SetActive(true);
                    _cancelBtn?.gameObject.SetActive(true);
                    break;
                case DialogButtonType.YesNo:
                    _yesBtn?.gameObject.SetActive(true);
                    _noBtn?.gameObject.SetActive(true);
                    break;
                case DialogButtonType.YesNoCancel:
                    _yesBtn?.gameObject.SetActive(true);
                    _noBtn?.gameObject.SetActive(true);
                    _cancelBtn?.gameObject.SetActive(true);
                    break;
            }
        }

        /// <summary>
        /// 按钮点击处理
        /// </summary>
        private void OnButtonClick(DialogButtonResult result) {
            _onResult?.Invoke(result);
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
