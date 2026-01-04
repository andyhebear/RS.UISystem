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
    /// 用于窗口显示加载过程中的等待界面
    /// </summary>
    public class UITopWaitingMgr : MonoBehaviour
    {
        #region 单例实现

        private static UITopWaitingMgr _instance;
        public static UITopWaitingMgr Instance {
            get {
                if (_instance == null) {
                    var go = new GameObject(nameof(UITopWaitingMgr));
                    _instance = go.AddComponent<UITopWaitingMgr>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        #endregion

        #region 数据结构定义

        /// <summary>
        /// 窗口加载数据类，存储每个窗口的加载界面信息
        /// </summary>
        private class WindowLoadingData
        {
            public GameObject Mask;          // 遮罩对象
            public int MaskCount;           // 引用计数
            public Text LoadingText;        // 加载文本
            public Image LoadingIcon;       // 加载图标
            public Coroutine RotateCoroutine; // 旋转动画协程
        }

        #endregion

        #region 私有字段

        private GameObject _maskPrefab;
        private Canvas _waitingCanvas;
        private CanvasScaler _canvasScaler;
        private GraphicRaycaster _graphicRaycaster;

        // 预制体的加载文本和图标引用
        private Text _loadingText;
        private Image _loadingIcon;

        // 存储每个窗口的加载界面数据，key为窗口ID，value为窗口加载数据
        private Dictionary<string,WindowLoadingData> _windowLoadingDatas = new Dictionary<string,WindowLoadingData>();

        // 默认窗口ID，用于向后兼容
        private const string DEFAULT_WINDOW_ID = "default";

        #endregion

        #region Unity生命周期

        private void Awake() {
            if (_instance != null && _instance != this) {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        #endregion

        #region 初始化方法

        /// <summary>
        /// 初始化遮罩管理器
        /// </summary>
        private void Initialize() {
            // 创建独立Canvas
            CreateIndependentCanvas();
            // 创建遮罩预制体
            CreateWaitingMaskPrefab();
        }

        /// <summary>
        /// 创建独立的Canvas
        /// </summary>
        private void CreateIndependentCanvas() {
            // 创建Canvas对象
            GameObject canvasObj = new GameObject("UITopWaitingCanvas");
            canvasObj.transform.SetParent(transform,false);

            // 添加Canvas组件并设置为ScreenSpaceOverlay模式
            _waitingCanvas = canvasObj.AddComponent<Canvas>();
            _waitingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _waitingCanvas.sortingOrder = 9999; // 设置为最高层级，确保始终显示在最顶层

            // 添加CanvasScaler组件
            _canvasScaler = canvasObj.AddComponent<CanvasScaler>();
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = new Vector2(1920,1080);
            _canvasScaler.matchWidthOrHeight = 0.5f;

            // 添加GraphicRaycaster组件
            _graphicRaycaster = canvasObj.AddComponent<GraphicRaycaster>();
        }

        /// <summary>
        /// 创建遮罩预制体
        /// </summary>
        private void CreateWaitingMaskPrefab() {
            _maskPrefab = new GameObject("UIWaitingMask");

            // 添加Image组件作为遮罩
            var image = _maskPrefab.AddComponent<Image>();
            image.color = new Color(0,0,0,0.5f); // 半透明黑色

            // 设置RectTransform
            var rectTransform = _maskPrefab.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            // 添加Button组件用于点击穿透处理
            var button = _maskPrefab.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            // 创建加载图标
            GameObject loadingIconObj = new GameObject("LoadingIcon");
            loadingIconObj.transform.SetParent(_maskPrefab.transform,false);
            _loadingIcon = loadingIconObj.AddComponent<Image>();
            var iconRect = loadingIconObj.GetComponent<RectTransform>();
            iconRect.sizeDelta = new Vector2(60,60);
            iconRect.anchorMin = new Vector2(0.5f,0.5f);
            iconRect.anchorMax = new Vector2(0.5f,0.5f);
            iconRect.anchoredPosition = new Vector2(0,20);

            // 创建加载文本
            GameObject loadingTextObj = new GameObject("LoadingText");
            loadingTextObj.transform.SetParent(_maskPrefab.transform,false);
            _loadingText = loadingTextObj.AddComponent<Text>();
#if UNITY_2023_1_OR_NEWER
            _loadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            _loadingText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
            _loadingText.fontSize = 24;
            _loadingText.alignment = TextAnchor.MiddleCenter;
            _loadingText.color = Color.white;
            _loadingText.text = "loading...";
            var textRect = loadingTextObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(300,30);
            textRect.anchorMin = new Vector2(0.5f,0.5f);
            textRect.anchorMax = new Vector2(0.5f,0.5f);
            textRect.anchoredPosition = new Vector2(0,-20);

            // 隐藏预制体
            _maskPrefab.SetActive(false);
        }

#endregion

#region 公共方法

        /// <summary>
        /// 显示等待界面遮罩
        /// </summary>
        public void ShowWaitingMask(string windowId = DEFAULT_WINDOW_ID,Action onClick = null) {
            if (string.IsNullOrEmpty(windowId)) {
                windowId = DEFAULT_WINDOW_ID;
            }

            // 获取或创建窗口加载数据
            WindowLoadingData loadingData;
            if (!_windowLoadingDatas.TryGetValue(windowId,out loadingData)) {
                loadingData = new WindowLoadingData();
                loadingData.MaskCount = 0;
                _windowLoadingDatas[windowId] = loadingData;
            }

            // 增加引用计数
            loadingData.MaskCount++;

            // 如果遮罩不存在，则创建并初始化
            if (loadingData.Mask == null) {
                // 实例化遮罩
                loadingData.Mask = GameObject.Instantiate(_maskPrefab,_waitingCanvas.transform);
                loadingData.Mask.name = $"UIWaitingMask_{windowId}";

                // 获取加载文本和图标引用
                loadingData.LoadingText = loadingData.Mask.transform.Find("LoadingText")?.GetComponent<Text>();
                loadingData.LoadingIcon = loadingData.Mask.transform.Find("LoadingIcon")?.GetComponent<Image>();

                // 初始化加载文本内容
                if (loadingData.LoadingText != null) {
                    loadingData.LoadingText.text = "loading...";
                }

                // 设置点击事件
                if (onClick != null) {
                    var button = loadingData.Mask.GetComponent<Button>();
                    if (button != null) {
                        button.onClick.AddListener(() => onClick());
                    }
                }

                // 确保遮罩可见
                loadingData.Mask.SetActive(true);
                // 重置初始透明度为0，准备淡入
                var image = loadingData.Mask.GetComponent<Image>();
                if (image != null) {
                    Color color = image.color;
                    color.a = 0f;
                    image.color = color;
                }

                // 播放加载图标旋转动画
                loadingData.RotateCoroutine = StartCoroutine(RotateLoadingIcon(loadingData));

                // 播放淡入动画
                StartCoroutine(FadeInWaitingMask(loadingData));
            }
        }

        /// <summary>
        /// 隐藏等待界面遮罩
        /// </summary>
        public void HideWaitingMask(string windowId = DEFAULT_WINDOW_ID) {
            if (string.IsNullOrEmpty(windowId)) {
                windowId = DEFAULT_WINDOW_ID;
            }

            if (!_windowLoadingDatas.TryGetValue(windowId,out WindowLoadingData loadingData)) {
                return;
            }

            // 减少引用计数
            loadingData.MaskCount--;

            // 确保引用计数不会小于0
            if (loadingData.MaskCount < 0) {
                loadingData.MaskCount = 0;
            }

            // 如果引用计数为0且遮罩存在，则隐藏并清理
            if (loadingData.MaskCount == 0 && loadingData.Mask != null) {

                // 停止旋转动画
                if (loadingData.RotateCoroutine != null) {
                    try {
                        StopCoroutine(loadingData.RotateCoroutine);
                    }
                    catch (System.Exception) {
                        // 忽略停止协程时可能发生的异常
                    }
                    loadingData.RotateCoroutine = null;
                }

                // 播放淡出动画
                StartCoroutine(FadeOutWaitingMask(loadingData));
            }
        }

        /// <summary>
        /// 强制隐藏所有等待界面遮罩
        /// </summary>
        public void HideAllWaitingMasks() {
            // 停止所有相关协程
            StopAllCoroutines();

            // 销毁所有遮罩对象并清空数据
            foreach (var loadingData in _windowLoadingDatas.Values) {
                if (loadingData.Mask != null) {
                    GameObject.Destroy(loadingData.Mask);
                }
            }

            _windowLoadingDatas.Clear();
        }

        /// <summary>
        /// 设置等待界面遮罩颜色
        /// </summary>
        public void SetWaitingMaskColor(Color color,string windowId = null) {
            // 设置预制体颜色
            if (_maskPrefab != null) {
                var image = _maskPrefab.GetComponent<Image>();
                if (image != null) {
                    image.color = color;
                }
            }

            // 如果指定了窗口ID，只设置特定窗口的颜色
            if (!string.IsNullOrEmpty(windowId)) {
                if (_windowLoadingDatas.TryGetValue(windowId,out WindowLoadingData loadingData) && loadingData.Mask != null) {
                    var image = loadingData.Mask.GetComponent<Image>();
                    if (image != null) {
                        image.color = color;
                    }
                }
            }
            else {
                // 否则设置所有已存在窗口的颜色
                foreach (var loadingData in _windowLoadingDatas.Values) {
                    if (loadingData.Mask != null) {
                        var image = loadingData.Mask.GetComponent<Image>();
                        if (image != null) {
                            image.color = color;
                        }
                    }
                }
            }
        }

#endregion

#region 公共方法 - 进度更新

        /// <summary>
        /// 更新加载进度和提示文本（内部实现）
        /// </summary>
        private void UpdateProgressInternal(float progress,string tip = "",string windowId = DEFAULT_WINDOW_ID) {
            if (!_windowLoadingDatas.TryGetValue(windowId,out WindowLoadingData loadingData) || loadingData.LoadingText == null) {
                return;
            }

            // 更新文本内容
            if (!string.IsNullOrEmpty(tip)) {
                loadingData.LoadingText.text = tip;
            }
            else {
                // 如果没有提供提示文本，可以显示进度百分比
                loadingData.LoadingText.text = $"加载中... {(int)(progress * 100)}%";
            }
        }

#endregion

#region 协程方法

        /// <summary>
        /// 加载图标旋转动画
        /// </summary>
        private IEnumerator RotateLoadingIcon(WindowLoadingData loadingData) {
            // 添加空检查
            if (loadingData == null) yield break;

            while (loadingData.Mask != null) {
                if (loadingData.LoadingIcon != null) {
                    try {
                        loadingData.LoadingIcon.rectTransform.Rotate(Vector3.forward,-360f * Time.deltaTime);
                    }
                    catch (System.Exception) {
                        // 忽略旋转时可能发生的异常
                        break;
                    }
                }
                yield return null;
            }
        }

        /// <summary>
        /// 等待界面遮罩淡入动画
        /// </summary>
        private IEnumerator FadeInWaitingMask(WindowLoadingData loadingData) {
            // 添加空检查
            if (loadingData == null) yield break;
            if (loadingData.Mask == null) yield break;

            var image = loadingData.Mask.GetComponent<Image>();
            if (image == null) yield break;

            Color originalColor = image.color;

            // 保存原始透明度（从预制体继承）
            float targetAlpha = _maskPrefab.GetComponent<Image>().color.a;

            float duration = 0.3f;
            float timer = 0f;

            while (timer < duration) {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(0f,targetAlpha,timer / duration);
                try {
                    image.color = new Color(originalColor.r,originalColor.g,originalColor.b,alpha);
                }
                catch (System.Exception) {
                    // 忽略颜色设置时可能发生的异常
                    break;
                }
                yield return null;
            }

            try {
                image.color = new Color(originalColor.r,originalColor.g,originalColor.b,targetAlpha);
            }
            catch (System.Exception) {
                // 忽略颜色设置时可能发生的异常
            }
        }

        /// <summary>
        /// 等待界面遮罩淡出动画
        /// </summary>
        private IEnumerator FadeOutWaitingMask(WindowLoadingData loadingData) {
            // 添加空检查
            if (loadingData == null) yield break;
            if (loadingData.Mask == null) yield break;

            var image = loadingData.Mask.GetComponent<Image>();
            if (image == null) yield break;

            Color originalColor = image.color;

            float duration = 0.2f;
            float timer = 0f;

            while (timer < duration) {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(originalColor.a,0f,timer / duration);
                try {
                    image.color = new Color(originalColor.r,originalColor.g,originalColor.b,alpha);
                }
                catch (System.Exception) {
                    // 忽略颜色设置时可能发生的异常
                    break;
                }
                yield return null;
            }

            // 存储窗口ID，用于后续从字典中移除
            string windowIdToRemove = null;
            foreach (var kvp in _windowLoadingDatas) {
                if (kvp.Value == loadingData) {
                    windowIdToRemove = kvp.Key;
                    break;
                }
            }

            // 销毁遮罩对象
            if (loadingData.Mask != null) {
                GameObject.Destroy(loadingData.Mask);
            }
            loadingData.Mask = null;
            loadingData.LoadingText = null;
            loadingData.LoadingIcon = null;

            // 如果窗口没有其他loading需求，从字典中移除
            if (!string.IsNullOrEmpty(windowIdToRemove) && _windowLoadingDatas.ContainsKey(windowIdToRemove)) {
                _windowLoadingDatas.Remove(windowIdToRemove);
            }
        }

#endregion

#region 静态便捷方法

        /// <summary>
        /// 显示指定窗口的等待界面
        /// </summary>
        public static void Show(string windowId = DEFAULT_WINDOW_ID,Action onClick = null) {
            Instance.ShowWaitingMask(windowId,onClick);
        }

        /// <summary>
        /// 隐藏指定窗口的等待界面
        /// </summary>
        public static void Hide(string windowId = DEFAULT_WINDOW_ID) {
            Instance.HideWaitingMask(windowId);
        }

        /// <summary>
        /// 隐藏所有窗口的等待界面
        /// </summary>
        public static void HideAll() {
            Instance.HideAllWaitingMasks();
        }

        /// <summary>
        /// 设置等待界面遮罩颜色
        /// </summary>
        public static void SetColor(Color color,string windowId = null) {
            Instance.SetWaitingMaskColor(color,windowId);
        }

        /// <summary>
        /// 更新指定窗口的加载进度和提示文本
        /// </summary>
        public static void UpdateProgress(float progress,string tip = "",string windowId = DEFAULT_WINDOW_ID) {
            Instance.UpdateProgressInternal(progress,tip,windowId);
        }

#endregion
    }
}
