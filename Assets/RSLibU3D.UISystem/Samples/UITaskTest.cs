using UnityEngine;
using System.Collections;
using System;
using RS.Unity3DLib.UISystem;
namespace RS.Unity3DLib.UISystem.Tests
{
    /// <summary>
    /// UITask功能测试类
    /// 用于验证IUIForm接口、UIFormBase类、UIManager中ShowForm和CloseForm方法的修改
    /// </summary>
    public class UITaskTest : MonoBehaviour
    {
        /// <summary>
        /// 启动测试
        /// </summary>
        public void StartTest() {
            StartCoroutine(TestShowAndCloseForm());
        }

        /// <summary>
        /// 测试显示和关闭界面的协程
        /// </summary>
        private IEnumerator TestShowAndCloseForm() {
            Debug.Log("=== 开始测试UITask功能 ===");

            // 测试1: 显示界面并等待完成
            Debug.Log("测试1: 调用ShowForm<T>并等待完成");
            UITask<TestUIMainForm> showTask = null;


            // 假设TestUIMainForm是一个存在的测试界面
            showTask = UIManager.Instance.ShowForm<TestUIMainForm>();
            Debug.Log("ShowForm调用成功，等待界面显示完成...");

            // 等待界面显示完成
            yield return showTask;

            if (showTask.IsCompleted && !showTask.IsFaulted) {
                UIFormBase form = showTask.Result;
                Debug.Log($"测试1成功: 界面显示完成，表单类型: {form.GetType().Name}");

                // 测试2: 关闭界面并等待完成
                Debug.Log("测试2: 调用CloseForm<T>并等待完成");
                UITask<bool> closeTask = UIManager.Instance.CloseForm<TestUIMainForm>();
                Debug.Log("CloseForm调用成功，等待界面关闭完成...");

                // 等待界面关闭完成
                yield return closeTask;

                if (closeTask.IsCompleted && !closeTask.IsFaulted) {
                    bool closeSuccess = closeTask.Result;
                    Debug.Log($"测试2成功: 界面关闭完成，结果: {closeSuccess}");
                }
                else if (closeTask.IsFaulted) {
                    Debug.LogError($"测试2失败: 界面关闭过程出错 - {closeTask.Exception?.Message}");
                }
            }
            else if (showTask.IsFaulted) {
                Debug.LogError($"测试1失败: 界面显示过程出错 - {showTask.Exception?.Message}");
            }

        }

        /// <summary>
        /// 便捷测试方法：通过名称显示并关闭界面
        /// </summary>
        public void TestByName(string formName) {
            StartCoroutine(TestShowAndCloseByName(formName));
        }

        private IEnumerator TestShowAndCloseByName(string formName) {
            Debug.Log($"=== 开始测试指定界面: {formName} ===");


            // 测试显示
            Debug.Log($"显示界面: {formName}");
            UITask<UIFormBase> showTask = UIManager.Instance.ShowForm(formName);
            yield return showTask;

            if (showTask.IsCompleted && !showTask.IsFaulted) {
                Debug.Log($"界面 {formName} 显示成功");

                // 等待2秒后关闭
                Debug.Log($"等待2秒后关闭界面...");
                yield return new WaitForSeconds(2f);

                // 测试关闭
                Debug.Log($"关闭界面: {formName}");
                UITask<bool> closeTask = UIManager.Instance.CloseForm(formName);
                yield return closeTask;

                if (closeTask.IsCompleted && !closeTask.IsFaulted) {
                    Debug.Log($"界面 {formName} 关闭成功，结果: {closeTask.Result}");
                }
                else {
                    Debug.LogError($"关闭界面 {formName} 失败: {closeTask.Exception?.Message}");
                }
            }
            else {
                Debug.LogError($"显示界面 {formName} 失败: {showTask.Exception?.Message}");
            }

        }

        /// <summary>
        /// 测试错误处理 - 尝试显示不存在的界面
        /// </summary>
        public void TestErrorHandling() {
            StartCoroutine(TestNonExistentForm());
        }

        private IEnumerator TestNonExistentForm() {
            Debug.Log("=== 开始测试错误处理功能 ===");

            string nonExistentForm = "NonExistentForm_12345";
            Debug.Log($"尝试显示不存在的界面: {nonExistentForm}");

            UITask<UIFormBase> showTask = UIManager.Instance.ShowForm(nonExistentForm);
            yield return showTask;

            if (showTask.IsFaulted) {
                Debug.Log($"错误处理测试成功: 正确捕获到异常 - {showTask.Exception?.Message}");
            }
            else {
                Debug.LogWarning($"错误处理测试异常: 未正确处理不存在的界面");
            }

            Debug.Log("=== 错误处理功能测试结束 ===");
        }
    }
}
