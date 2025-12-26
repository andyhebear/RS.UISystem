using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    ///带凹槽移动设备的安全区域实现。使用方法：
    /// （1）将此组件添加到任何 GUI 面板的顶层。
    /// （2）如果面板使用全屏背景图像，则创建一个直接子级，并将该组件放在该子级上，其他所有元素作为其子级放在下方。
    /// 这将使背景图像能够拉伸到凹槽后方的整个屏幕范围，看起来更美观。
    /// （3）对于其他使用水平和垂直全屏背景条纹组合的情况，根据需要在不同元素上使用 X 轴和 Y 轴适配控件。
    /// Safe area implementation for notched mobile devices. Usage:
    ///  (1) Add this component to the top level of any GUI panel. 
    ///  (2) If the panel uses a full screen background image, then create an immediate child and put the component on that instead, with all other elements childed below it.
    ///      This will allow the background image to stretch to the full extents of the screen behind the notch, which looks nicer.
    ///  (3) For other cases that use a mixture of full horizontal and vertical background stripes, use the Conform X & Y controls on separate elements as needed.
    /// </summary>
    [ExecuteAlways]
    public class UISafeArea : MonoBehaviour
    {
        #region Simulations
        /// <summary>
        /// 因物理凹槽或软件主页栏而使用安全区域的模拟设备。仅用于编辑器中。
        /// Simulation device that uses safe area due to a physical notch or software home bar. For use in Editor only.
        /// </summary>
        public enum SimDevice
        {
            /// <summary>
            /// Don't use a simulated safe area - GUI will be full screen as normal.
            /// </summary>
            None,
            /// <summary>
            /// Simulate the iPhone X and Xs (identical safe areas).
            /// </summary>
            iPhoneX,
            /// <summary>
            /// Simulate the iPhone Xs Max and XR (identical safe areas).
            /// </summary>
            iPhoneXsMax,
            /// <summary>
            /// Simulate the Google Pixel 3 XL using landscape left.
            /// </summary>
            Pixel3XL_LSL,
            /// <summary>
            /// Simulate the Google Pixel 3 XL using landscape right.
            /// </summary>
            Pixel3XL_LSR
        }

        /// <summary>
        /// 仅在编辑器中使用的模拟模式。可以在运行时对其进行编辑，以在不同的安全区域之间切换。
        /// Simulation mode for use in editor only. This can be edited at runtime to toggle between different safe areas.
        /// </summary>
#if UNITY_EDITOR
        public const SimDevice Sim = SimDevice.iPhoneX;
#else
        public const SimDevice Sim = SimDevice.None;
#endif
        /// <summary>
        /// Normalised safe areas for iPhone X with Home indicator (ratios are identical to Xs, 11 Pro). Absolute values:
        ///  PortraitU x=0, y=102, w=1125, h=2202 on full extents w=1125, h=2436;
        ///  PortraitD x=0, y=102, w=1125, h=2202 on full extents w=1125, h=2436 (not supported, remains in Portrait Up);
        ///  LandscapeL x=132, y=63, w=2172, h=1062 on full extents w=2436, h=1125;
        ///  LandscapeR x=132, y=63, w=2172, h=1062 on full extents w=2436, h=1125.
        ///  Aspect Ratio: ~19.5:9.
        /// </summary>
        private readonly Rect[] NSA_iPhoneX = new Rect[]
        {
            new Rect(0f, 102f / 2436f, 1f, 2202f / 2436f), // Portrait
            new Rect(132f / 2436f, 63f / 1125f, 2172f / 2436f, 1062f / 1125f) // Landscape
        };

        /// <summary>
        /// Normalised safe areas for iPhone Xs Max with Home indicator (ratios are identical to XR, 11, 11 Pro Max). Absolute values:
        ///  PortraitU x=0, y=102, w=1242, h=2454 on full extents w=1242, h=2688;
        ///  PortraitD x=0, y=102, w=1242, h=2454 on full extents w=1242, h=2688 (not supported, remains in Portrait Up);
        ///  LandscapeL x=132, y=63, w=2424, h=1179 on full extents w=2688, h=1242;
        ///  LandscapeR x=132, y=63, w=2424, h=1179 on full extents w=2688, h=1242.
        ///  Aspect Ratio: ~19.5:9.
        /// </summary>
        private readonly Rect[] NSA_iPhoneXsMax = new Rect[]
        {
            new Rect(0f, 102f / 2688f, 1f, 2454f / 2688f), // Portrait
            new Rect(132f / 2688f, 63f / 1242f, 2424f / 2688f, 1179f / 1242f) // Landscape
        };

        /// <summary>
        /// Normalised safe areas for Pixel 3 XL using landscape left. Absolute values:
        ///  PortraitU x=0, y=0, w=1440, h=2789 on full extents w=1440, h=2960;
        ///  PortraitD x=0, y=0, w=1440, h=2789 on full extents w=1440, h=2960;
        ///  LandscapeL x=171, y=0, w=2789, h=1440 on full extents w=2960, h=1440;
        ///  LandscapeR x=0, y=0, w=2789, h=1440 on full extents w=2960, h=1440.
        ///  Aspect Ratio: 18.5:9.
        /// </summary>
        private readonly Rect[] NSA_Pixel3XL_LSL = new Rect[]
        {
            new Rect(0f, 0f, 1f, 2789f / 2960f), // Portrait
            new Rect(0f, 0f, 2789f / 2960f, 1f) // Landscape
        };

        /// <summary>
        /// Normalised safe areas for Pixel 3 XL using landscape right. Absolute values and aspect ratio same as above.
        /// </summary>
        private readonly Rect[] NSA_Pixel3XL_LSR = new Rect[]
        {
            new Rect(0f, 0f, 1f, 2789f / 2960f), // Portrait
            new Rect(171f / 2960f, 0f, 2789f / 2960f, 1f) // Landscape
        };
#endregion
        public bool EnableSafeArea;
        private RectTransform Panel;
        private Rect LastSafeArea = new Rect(0,0,0,0);
        private Vector2Int LastScreenSize = new Vector2Int(0,0);
        private ScreenOrientation LastOrientation = ScreenOrientation.AutoRotation;
        [SerializeField] private bool ConformX = false; //在 X 轴上遵循屏幕安全区域（默认值为 true，禁用则忽略） Conform to screen safe area on X-axis (default true, disable to ignore)
        [SerializeField] private bool ConformY = true; //在 X 轴上遵循屏幕安全区域（默认值为 true，禁用则忽略） Conform to screen safe area on Y-axis (default true, disable to ignore)
        [SerializeField] private bool Logging = false; // 记录ApplySafeArea

        private void Awake() {
            Panel = GetComponent<RectTransform>();

            if (Panel == null) {
                Debug.LogError("Cannot apply safe area - no RectTransform found on " + name);
                Destroy(gameObject);
            }

            Refresh();
        }

        //private void Update() {
        //    Refresh();
        //}
        private System.Collections.IEnumerator _screenChangeCoroutine;
        private void OnEnable() {
            //Refresh();
            // 启动屏幕变化监听
            _screenChangeCoroutine = WaitForScreenChange();
            StartCoroutine(_screenChangeCoroutine);
        }

        private void OnDisable() {
            // 停止屏幕变化监听
            if (_screenChangeCoroutine != null) {
                StopCoroutine(_screenChangeCoroutine);
                _screenChangeCoroutine = null;
            }
        }
        /// <summary>
        /// 等待屏幕变化
        /// </summary>
        private System.Collections.IEnumerator WaitForScreenChange() {
            Vector2 lastResolution = new Vector2(Screen.width,Screen.height);
            Rect lastSafeArea = Screen.safeArea;
            while (true) {
                yield return new WaitForSeconds(0.5f); // 减少检查间隔，提高响应速度
                Refresh();
            }
        }
        private void Refresh() {
            if (!EnableSafeArea) return;
            var safeArea = GetSafeArea();

            if (safeArea != LastSafeArea
                || Screen.width != LastScreenSize.x
                || Screen.height != LastScreenSize.y
                || Screen.orientation != LastOrientation) {
                //针对自动旋转关闭且手动强制屏幕方向的修复方案。
                // Fix for having auto-rotate off and manually forcing a screen orientation.
                // See https://forum.unity.com/threads/569236/#post-4473253 and https://forum.unity.com/threads/569236/page-2#post-5166467
                LastScreenSize.x = Screen.width;
                LastScreenSize.y = Screen.height;
                LastOrientation = Screen.orientation;

                ApplySafeArea(safeArea);
            }
        }

        private Rect GetSafeArea() {
            var safeArea = Screen.safeArea;

            if (Application.isEditor && Sim != SimDevice.None)
#pragma warning disable CS0162 //检测到无法执行的代码 Unreachable code detected
            {
                Rect nsa = new Rect(0,0,Screen.width,Screen.height);

                switch (Sim) {
                    case SimDevice.iPhoneX:
                        if (Screen.height > Screen.width) // Portrait
                            nsa = NSA_iPhoneX[0];
                        else // Landscape
                            nsa = NSA_iPhoneX[1];
                        break;
                    case SimDevice.iPhoneXsMax:
                        if (Screen.height > Screen.width) // Portrait
                            nsa = NSA_iPhoneXsMax[0];
                        else // Landscape
                            nsa = NSA_iPhoneXsMax[1];
                        break;
                    case SimDevice.Pixel3XL_LSL:
                        if (Screen.height > Screen.width) // Portrait
                            nsa = NSA_Pixel3XL_LSL[0];
                        else // Landscape
                            nsa = NSA_Pixel3XL_LSL[1];
                        break;
                    case SimDevice.Pixel3XL_LSR:
                        if (Screen.height > Screen.width) // Portrait
                            nsa = NSA_Pixel3XL_LSR[0];
                        else // Landscape
                            nsa = NSA_Pixel3XL_LSR[1];
                        break;
                    default:
                        break;
                }

                safeArea = new Rect(Screen.width * nsa.x,Screen.height * nsa.y,Screen.width * nsa.width,Screen.height * nsa.height);
            }
#pragma warning restore CS0162 //检测到无法执行的代码 Unreachable code detected

            return safeArea;
        }

        private void ApplySafeArea(Rect r) {
            LastSafeArea = r;

            // Ignore x-axis?
            if (!ConformX) {
                r.x = 0;
                r.width = Screen.width;
            }

            // Ignore y-axis?
            if (!ConformY) {
                r.y = 0;
                r.height = Screen.height;
            }
            //检查部分三星设备上的无效屏幕启动状态（见下文）
            // Check for invalid screen startup state on some Samsung devices (see below)
            if (Screen.width > 0 && Screen.height > 0) {
                //将安全区域矩形从绝对像素转换为归一化锚点坐标
                // Convert safe area rectangle from absolute pixels to normalised anchor coordinates
                Vector2 anchorMin = r.position;
                Vector2 anchorMax = r.position + r.size;
                anchorMin.x /= Screen.width;
                anchorMin.y /= Screen.height;
                anchorMax.x /= Screen.width;
                anchorMax.y /= Screen.height;
                //针对部分三星设备（例如 Note 10+、A71、S20）的修复，这些设备存在刷新被调用两次且第一次返回非数字（NaN）锚点坐标的问题
                // Fix for some Samsung devices (e.g. Note 10+, A71, S20) where Refresh gets called twice and the first time returns NaN anchor coordinates
                // See https://forum.unity.com/threads/569236/page-2#post-6199352
                if (anchorMin.x >= 0 && anchorMin.y >= 0 && anchorMax.x >= 0 && anchorMax.y >= 0) {
                    Panel.anchorMin = anchorMin;
                    Panel.anchorMax = anchorMax;
                }
            }

            if (Logging) {
                Debug.LogFormat("New safe area applied to {0}: x={1}, y={2}, w={3}, h={4} on full extents w={5}, h={6}",
                    name,r.x,r.y,r.width,r.height,Screen.width,Screen.height);
            }
        }
    }
}
