using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using RS.Unity3DLib;
public class TestUIMainForm : RS.Unity3DLib.UISystem.UIForm
{
    [SerializeField]
    private Button btn_test1;
    protected override void SubscribeEvents() {
        Debug.Log($"{nameof(TestUIMainForm)} SubscribeEvents:");
        btn_test1.onClick.AddListener(() => {
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowForm<TestUIMainForm2>();
        });
    }

    protected override void UnsubscribeAllEvents() {
        Debug.Log($"{nameof(TestUIMainForm)} UnsubscribeAllEvents:");
    }
    protected override void Awake() {
        base.Awake();

    }
    // Start is called before the first frame update
    protected override void Start() {

    }

    // Update is called once per frame
    void Update() {

    }
}
