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
    /// 供内部使用
    /// </summary>
    internal class UIFormLoading : UIFormBase
    {
        [SerializeField] private Image _progressBar;
        [SerializeField] private Text _tipText;

        private static int _loadingCount = 0; // 并发加载计数

        protected override void Awake() {
            base.Awake();
            _closeOnMaskClick = false; // 点击遮罩不关闭
        }
        /// <summary>
        /// 更新加载进度
        /// </summary>
        public void UpdateProgress(float progress,string tip = "") {
            if (_progressBar != null) {
                progress = Mathf.Clamp01(progress);
                _progressBar.fillAmount = progress;
                _progressBar.transform.Rotate(Vector3.forward,Mathf.PI * 0.1f);
            }

            if (_tipText != null && !string.IsNullOrEmpty(tip)) {
                _tipText.text = tip;
            }
        }
        protected override void OnInitialized(UIFormConfig config) {
            // 强制设置遮罩类型为半透明
            Config.MaskType = UIMaskType.Translucent;
            //Config.MaskColor = new Color(0,0,0,0.3f);
            createMask();
            ShowMask();
            if (_maskRawImage != null) {
                _maskRawImage.color = new Color(0,0,0,0.3f);
            }
        }
        public override void SubscribeEvents() {

        }

        public override void UnsubscribeAllEvents() {

        }
        public override void Show(object data = null,Action onComplete = null) {
            _loadingCount++;
            if (_loadingCount > 1) {
                onComplete?.Invoke();
                return;
            }
            base.Show(data,onComplete);
        }

        public override void Hide(bool isDestroy = false,Action onComplete = null) {
            _loadingCount--;
            if (_loadingCount > 0) {
                onComplete?.Invoke();
                return;
            }
            base.Hide(isDestroy,onComplete);
        }
        protected override void OnHideBefore() {

        }

        protected override void OnHideCompleted(bool isDestroy) {

        }

        protected override IEnumerator doHideAnimation() {
            yield return null;
        }
        protected override IEnumerator doShowAnimation() {
            yield return null;
        }
        protected override void OnShowBefore() {

        }


        protected override void OnShowCompleted() {

        }
        protected override void OnUIFormRecycled() {

        }
        protected override void OnUIFormReseted(object data) {

        }
    }
   
}
