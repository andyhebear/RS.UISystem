using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RS.Unity3DLib;
using RS.Unity3DLib.UISystem;
public class testUISystem1 : MonoBehaviour
{
    // Start is called before the first frame update
    void Start() {
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowForm<TestUIMainForm>();
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowForm<TestUIMainForm2>();
        StartCoroutine(testShow());
    }
    IEnumerator testShow() {
        UIManager.Instance.ShowLoadingForm("waiting 1.");
        yield return new  WaitForSeconds(3f);
        UIManager.Instance.CloseLoadingForm();
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowPopup("test22","test message box",RS.Unity3DLib.UISystem.DialogButtonType.OKCancel,(ret) => {
            
            Debug.Log("click ok");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("click ok",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        },() => {

            Debug.Log("anim complate");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("amim complate",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        });
        yield return new WaitForSeconds(3f);
        bool waitMsgBox = true;
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowPopup("test","test message box", RS.Unity3DLib.UISystem.DialogButtonType.OKCancel,(ret) => {
            waitMsgBox = false;
            Debug.Log("click ok");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("click ok",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        },() => {
            
            Debug.Log("anim complate");
            RS.Unity3DLib.UISystem.UIManager.Instance.ShowNotify("amim complate",
                RS.Unity3DLib.UISystem.NotifyPosition.BottomRight,3);
        });
        while (waitMsgBox) {
            yield return null;
        }
        yield return new WaitForSeconds(1f);
        waitMsgBox = true;
        RS.Unity3DLib.UISystem.UIManager.Instance.ShowInputPopup("test","test message box", RS.Unity3DLib.UISystem.InputType.String,false,"1",20,(z) => {
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
            
            Debug.Log("input complete");
        });


    }
    // Update is called once per frame
    void Update() {

    }
}
