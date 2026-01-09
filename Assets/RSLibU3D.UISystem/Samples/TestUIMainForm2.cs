using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RS.Unity3DLib;
using UnityEngine.UI;
using RS.Unity3DLib.UISystem;
namespace RS.Unity3DLib.UISystem.Tests
{
    public class TestUIMainForm2 : RS.Unity3DLib.UISystem.UIFormBase
    {
        [SerializeField]
        private Button btn_test1;
        public override void SubscribeEvents() {
            Debug.Log($"{nameof(TestUIMainForm2)} SubscribeEvents:");
            btn_test1.onClick.AddListener(() => {
                RS.Unity3DLib.UISystem.UIManager.Instance.CloseForm<TestUIMainForm2>();
            });
        }

        public override void UnsubscribeAllEvents() {
            btn_test1.onClick.RemoveAllListeners();
            Debug.Log($"{nameof(TestUIMainForm2)} UnsubscribeAllEvents:");
        }
        protected override void Awake() {
            base.Awake();
        }

        protected override void OnHideBefore() {

        }

        protected override void OnHideCompleted(bool isDestroy) {

        }

        protected override void OnInitialized(UIFormConfig config) {

        }

        protected override void OnShowBefore() {

        }

        protected override void OnShowCompleted() {

        }

        protected override void OnUIFormRecycled() {

        }

        protected override void OnUIFormReseted(object data) {

        }

        // Start is called before the first frame update
        protected override void Start() {
            base.Start();
        }

        // Update is called once per frame
        void Update() {

        }
    }
}