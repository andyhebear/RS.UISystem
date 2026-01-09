using System.Collections;
using UnityEngine;
using RS.Unity3DLib.UISystem;
namespace  RS.Unity3DLib.UISystem.Tests
{
    /// <summary>
    /// 多窗口Loading功能测试脚本
    /// 用于验证每个窗口可以独立显示和控制自己的loading
    /// </summary>
    public class testMultiWindowLoading : MonoBehaviour
    {
        private string windowId1 = "TestWindow1";
        private string windowId2 = "TestWindow2";
        private string windowId3 = "TestWindow3";
        
        private bool isTestRunning = false;
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 300, 400));
            
            GUILayout.Label("多窗口Loading测试");
            GUILayout.Space(10);
            
            // 手动测试按钮
            if (!isTestRunning && GUILayout.Button("启动自动测试流程", GUILayout.Height(40)))
            {
                StartCoroutine(AutoTestSequence());
            }
            
            GUILayout.Space(20);
            
            // 窗口1控制
            GUILayout.Label("窗口1控制:");
            if (GUILayout.Button("显示窗口1 Loading"))
            {
                UITopWaitingMgr.Show(windowId1, "窗口1加载中...");
            }
            if (GUILayout.Button("隐藏窗口1 Loading"))
            {
                UITopWaitingMgr.Hide(windowId1);
            }
            if (GUILayout.Button("更新窗口1 进度"))
            {
                UITopWaitingMgr.UpdateProgress(Random.value, $"窗口1进度: {Random.Range(0, 100)}%", windowId1);
            }
            
            GUILayout.Space(10);
            
            // 窗口2控制
            GUILayout.Label("窗口2控制:");
            if (GUILayout.Button("显示窗口2 Loading"))
            {
                UITopWaitingMgr.Show(windowId2, "窗口2加载中...");
            }
            if (GUILayout.Button("隐藏窗口2 Loading"))
            {
                UITopWaitingMgr.Hide(windowId2);
            }
            if (GUILayout.Button("更新窗口2 进度"))
            {
                UITopWaitingMgr.UpdateProgress(Random.value, $"窗口2进度: {Random.Range(0, 100)}%", windowId2);
            }
            
            GUILayout.Space(10);
            
            // 窗口3控制（使用默认进度条模式）
            GUILayout.Label("窗口3控制 (默认模式):");
            if (GUILayout.Button("显示窗口3 Loading"))
            {
                UITopWaitingMgr.Show(windowId3);
            }
            if (GUILayout.Button("隐藏窗口3 Loading"))
            {
                UITopWaitingMgr.Hide(windowId3);
            }
            
            GUILayout.Space(10);
            
            // 隐藏所有loading
            if (GUILayout.Button("隐藏所有Loading", GUILayout.Height(30)))
            {
                UITopWaitingMgr.HideAll();
            }
            
            GUILayout.EndArea();
        }
        
        /// <summary>
        /// 自动测试序列
        /// 验证多窗口loading的独立控制功能
        /// </summary>
        private IEnumerator AutoTestSequence()
        {
            isTestRunning = true;
            
            Debug.Log("开始多窗口Loading自动测试...");
            
            // 步骤1: 显示所有窗口的loading
            Debug.Log("步骤1: 显示所有窗口的loading");
            UITopWaitingMgr.Show(windowId1, "窗口1加载中...");
            yield return new WaitForSeconds(1.0f);
            
            UITopWaitingMgr.Show(windowId2, "窗口2加载中...");
            yield return new WaitForSeconds(1.0f);
            
            UITopWaitingMgr.Show(windowId3);
            yield return new WaitForSeconds(1.0f);
            
            // 步骤2: 更新窗口1和窗口2的进度
            Debug.Log("步骤2: 更新窗口1和窗口2的进度");
            for (int i = 0; i <= 100; i += 20)
            {
                float progress = i / 100f;
                UITopWaitingMgr.UpdateProgress(progress, $"窗口1进度: {i}%", windowId1);
                UITopWaitingMgr.UpdateProgress(progress, $"窗口2进度: {i}%", windowId2);
                yield return new WaitForSeconds(0.5f);
            }
            
            // 步骤3: 隐藏窗口2的loading
            Debug.Log("步骤3: 隐藏窗口2的loading");
            UITopWaitingMgr.Hide(windowId2);
            yield return new WaitForSeconds(1.0f);
            
            // 步骤4: 继续更新窗口1的进度
            Debug.Log("步骤4: 继续更新窗口1的进度");
            for (int i = 0; i <= 100; i += 10)
            {
                float progress = i / 100f;
                UITopWaitingMgr.UpdateProgress(progress, $"窗口1最终进度: {i}%", windowId1);
                yield return new WaitForSeconds(0.3f);
            }
            
            // 步骤5: 隐藏窗口1的loading
            Debug.Log("步骤5: 隐藏窗口1的loading");
            UITopWaitingMgr.Hide(windowId1);
            yield return new WaitForSeconds(1.0f);
            
            // 步骤6: 最后隐藏窗口3的loading
            Debug.Log("步骤6: 隐藏窗口3的loading");
            UITopWaitingMgr.Hide(windowId3);
            
            Debug.Log("多窗口Loading自动测试完成!");
            
            yield return new WaitForSeconds(1.0f);
            isTestRunning = false;
        }
        
        /// <summary>
        /// 使用UIManager测试窗口加载时的loading
        /// 注意：此方法需要实际的窗口配置才能运行
        /// </summary>
        private void TestWithUIManager()
        {
            // 这里可以添加使用UIManager加载窗口的测试代码
            // 例如：UIManager.Instance.ShowForm("TestWindow1");
            // 然后通过 UIManager.Instance.UpdateLoadingProgress(progress, tip, "TestWindow1") 更新进度
        }
    }
}