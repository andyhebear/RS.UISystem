using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RS.Unity3DLib;

public class testUISystem1 : MonoBehaviour
{
    // Start is called before the first frame update
    void Start() {
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowForm<TestUIMainForm>();
        StartCoroutine(testShow());
    }
    IEnumerator testShow() {
        yield return new WaitForSeconds(3f);
        bool waitMsgBox = true;
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowPopup("test","test message box",() => {
            waitMsgBox = false;
            Debug.Log("click ok");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("click ok",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        },() => {
            waitMsgBox = false;
            Debug.Log("click cancel");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("click cancel",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        });
        while (waitMsgBox) {
            yield return null;
        }
        yield return new WaitForSeconds(1f);
        waitMsgBox = true;
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowInputPopup("test","test message box",RS.Unity3DLib.UISystem.InputFieldType.Normal,"1",20,(z) => {
            waitMsgBox = false;
            Debug.Log("input :" + z);
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("input: " + z,
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        },() => {
            waitMsgBox = false;
            Debug.Log("input cancel");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("input cancel",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        },() => {
            waitMsgBox = false;
            Debug.Log("input complete");
        });


    }
    // Update is called once per frame
    void Update() {

    }
}
