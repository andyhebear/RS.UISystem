using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// UI根节点适配管理器（支持6种适配模式+SafeArea）
    /// 挂载在 UILayers 节点上
    /// </summary>
    [RequireComponent(typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster))]
    public class UICanvasAdapter : MonoBehaviour
    {
        [Header("基础适配配置")]
        [SerializeField] private AdaptMode _adaptMode = AdaptMode.BlackBars; // 默认黑边模式
        [SerializeField] internal Color _backSideColor = Color.black;
        [SerializeField] internal Vector2 _referenceResolution = new(1920,1080); // 设计分辨率
        [SerializeField] private float _referenceAspectRatio; // 自动计算，无需手动填写
        [SerializeField] private Camera _uiCamera; // 绑定UICamera

        //[Header("SafeArea配置")]
        //[SerializeField] private bool _enableSafeArea = true; // 是否启用SafeArea适配
        //[SerializeField] private RectTransform _safeAreaRoot; // 需要适配SafeArea的UI根节点

        private Canvas _canvas;
        private CanvasScaler _canvasScaler;
        private Rect _lastSafeArea;
        private Vector2 _lastScreenSize;
        private float _lastAspectRatio;

        private void Awake() {
            _canvas = GetComponent<Canvas>();
            _canvasScaler = GetComponent<CanvasScaler>();

            // 自动计算设计纵横比
            _referenceAspectRatio = _referenceResolution.x / _referenceResolution.y;

            // 初始化Canvas基础配置
            InitCanvasConfig();

            // 初始化适配模式
            UpdateAdaptMode();

            //// 初始化SafeArea

            //if (_enableSafeArea && _safeAreaRoot != null) {
            //    _lastSafeArea = Screen.safeArea;
            //    UpdateSafeArea();
            //}

            _lastScreenSize = new Vector2(Screen.width,Screen.height);
            _lastAspectRatio = _lastScreenSize.x / _lastScreenSize.y;
        }
        //private void Start() {
        //    // 自动计算设计纵横比
        //    _referenceAspectRatio = _referenceResolution.x / _referenceResolution.y;
        //    _lastScreenSize = new Vector2(Screen.width,Screen.height);
        //    _lastAspectRatio = _lastScreenSize.x / _lastScreenSize.y;
        //}
        //private void Update() {
        //    // 屏幕尺寸/纵横比变化时重新适配
        //    float currentAspectRatio = (float)Screen.width / Screen.height;
        //    if (new Vector2(Screen.width,Screen.height) != _lastScreenSize ||
        //        Mathf.Abs(currentAspectRatio - _lastAspectRatio) > 0.01f) {
        //        _lastScreenSize = new Vector2(Screen.width,Screen.height);
        //        _lastAspectRatio = currentAspectRatio;
        //        UpdateAdaptMode();

        //        // 同步更新黑边（BlackBars模式下）
        //        if (_adaptMode == AdaptMode.BlackBars) {
        //            UpdateBlackBarsDelayed(); // 延迟更新，避免闪烁
        //        }

        //    }

        //    //// SafeArea变化时更新（延迟更新，避免频繁触发）
        //    //if (_enableSafeArea && _safeAreaRoot != null && Screen.safeArea != _lastSafeArea) {
        //    //    _lastSafeArea = Screen.safeArea;
        //    //    Invoke(nameof(UpdateSafeAreaDelayed),0.05f);
        //    //}
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
                                                       // 屏幕尺寸/纵横比变化时重新适配
                float currentAspectRatio = (float)Screen.width / Screen.height;
                if (new Vector2(Screen.width,Screen.height) != _lastScreenSize ||
                    Mathf.Abs(currentAspectRatio - _lastAspectRatio) > 0.01f) {
                    _lastScreenSize = new Vector2(Screen.width,Screen.height);
                    _lastAspectRatio = currentAspectRatio;
                    UpdateAdaptMode();

                    // 同步更新黑边（BlackBars模式下）
                    if (_adaptMode == AdaptMode.BlackBars) {
                        UpdateBlackBarsDelayed(); // 延迟更新，避免闪烁
                    }

                }
            }
        }
        /// <summary>
        /// 初始化Canvas基础配置（ScreenSpaceCamera模式）
        /// </summary>
        private void InitCanvasConfig() {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _uiCamera;
            //_canvas.planeDistance = 10f;
            _canvas.sortingLayerName = "UI";
            _canvas.pixelPerfect = true;
            _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            _canvasScaler.referenceResolution = _referenceResolution;
            //
            SetCanvas();
            SetCanvasScaler();
        }

        /// <summary>
        /// 根据当前适配模式更新UI缩放逻辑
        /// </summary>
        private void UpdateAdaptMode() {
            float currentAspectRatio = _lastAspectRatio;

            switch (_adaptMode) {
                case AdaptMode.MatchWidth:
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    _canvasScaler.matchWidthOrHeight = 0f;
                    //_canvasScaler.allowScaleBeyondReference = true;
                    break;

                case AdaptMode.MatchHeight:
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    _canvasScaler.matchWidthOrHeight = 1f;
                    //_canvasScaler.allowScaleBeyondReference = true;
                    break;

                case AdaptMode.MatchWidthOrHeight:
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                    // 修复：当前宽高比 > 设计宽高比 → 匹配高度；否则匹配宽度
                    _canvasScaler.matchWidthOrHeight = currentAspectRatio > _referenceAspectRatio ? 1f : 0f;
                    //_canvasScaler.allowScaleBeyondReference = true;
                    break;

                case AdaptMode.Expand:
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                    //_canvasScaler.allowScaleBeyondReference = true;
                    break;

                case AdaptMode.Shrink:
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                    _canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;
                    //_canvasScaler.allowScaleBeyondReference = false;
                    break;

                case AdaptMode.BlackBars:
                    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    float scaleX = Screen.width / _referenceResolution.x;
                    float scaleY = Screen.height / _referenceResolution.y;
                    _canvasScaler.scaleFactor = Mathf.Min(scaleX,scaleY);
                    if (_uiCamera != null) {
                        _uiCamera.orthographicSize = _referenceResolution.y / 2f / _canvasScaler.scaleFactor;
                    }
                    break;
            }
        }

        /// <summary>
        /// 延迟更新黑边（避免切换时闪烁）
        /// </summary>
        private void UpdateBlackBarsDelayed() {
            UIBackgroundMask backgroundMask = this.transform.Find("UIBackgroundMask")?.GetComponent<UIBackgroundMask>();
            if (backgroundMask != null) {
                //
                //设计分辨率
                backgroundMask._referenceResolution = _referenceResolution;
                backgroundMask.SetMaskColor(_backSideColor);
                backgroundMask.UpdateMaskSize();

            }
            else {
                //创建黑边物体
                GameObject bc = new GameObject("UIBackgroundMask",typeof(RectTransform),typeof(UIBackgroundMask));
                bc.layer = LayerMask.NameToLayer("UI");
                bc.transform.SetParent(this.transform);
                bc.transform.SetAsLastSibling();
                bc.transform.localPosition = Vector3.zero;
                RectTransform rectTrans = bc.GetComponent<RectTransform>();
                if (rectTrans != null) {
                    rectTrans.anchorMin = Vector2.zero;
                    rectTrans.anchorMax = Vector2.one;
                    rectTrans.offsetMin = Vector2.zero;
                    rectTrans.offsetMax = Vector2.zero;
                    rectTrans.localScale = Vector3.one;
                }
                //
                backgroundMask = bc.GetComponent<UIBackgroundMask>();
                backgroundMask._uiCamera = _uiCamera;
                //设计分辨率
                backgroundMask._referenceResolution = _referenceResolution;
                backgroundMask.SetMaskColor(_backSideColor);
                backgroundMask.UpdateMaskSize();
            }
        }

        ///// <summary>
        ///// 延迟更新SafeArea（避免频繁触发）
        ///// </summary>
        //private void UpdateSafeAreaDelayed() {
        //    UpdateSafeArea();
        //    UIEventBus.Trigger(UIEventNames.SafeAreaUpdated);
        //}

        ///// <summary>
        ///// 更新SafeArea适配
        ///// </summary>
        //private void UpdateSafeArea() {
        //    Rect safeArea = Screen.safeArea;

        //    Vector2 minAnchor = new Vector2(
        //        safeArea.x / Screen.width,
        //        safeArea.y / Screen.height
        //    );
        //    Vector2 maxAnchor = new Vector2(
        //        (safeArea.x + safeArea.width) / Screen.width,
        //        (safeArea.y + safeArea.height) / Screen.height
        //    );

        //    _safeAreaRoot.anchorMin = minAnchor;
        //    _safeAreaRoot.anchorMax = maxAnchor;
        //    _safeAreaRoot.offsetMin = Vector2.zero;
        //    _safeAreaRoot.offsetMax = Vector2.zero;
        //}

        /// <summary>
        /// 动态切换适配模式（外部调用接口）
        /// </summary>
        public void SwitchAdaptMode(AdaptMode newMode,Camera uiCamera) {
            _adaptMode = newMode;
            if (uiCamera != null) {
                _uiCamera = uiCamera;
                this._canvas.worldCamera = uiCamera;
            }
            UpdateAdaptMode();
            if (newMode == AdaptMode.BlackBars) {
                UpdateBlackBarsDelayed();
            }
            else {
                // 非黑边模式隐藏黑边
                UIBackgroundMask backgroundMask = this.transform.Find("UIBackgroundMask")?.GetComponent<UIBackgroundMask>();
                backgroundMask?.HideAllMasks();
            }

            Debug.Log($"UI适配模式已切换为：{newMode}");
        }

        #region 外部访问接口
        public AdaptMode GetCurrentAdaptMode() => _adaptMode;
        public Vector2 GetReferenceResolution() => _referenceResolution;
        public float GetCurrentScaleFactor() => _canvasScaler.scaleFactor;
        #endregion

        void SetCanvas(int sortOrder = 1,string sortingLayerName = "Default",RenderMode renderMode = RenderMode.ScreenSpaceCamera,bool pixelPerfect = true,UnityEngine.Camera camera = null) {
            _canvas.renderMode = renderMode;
            _canvas.worldCamera = camera;
            _canvas.sortingOrder = sortOrder;
            _canvas.sortingLayerName = sortingLayerName;
            _canvas.pixelPerfect = pixelPerfect;
        }



        void SetCanvasScaler(CanvasScaler.ScaleMode scaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize,
            Vector2? referenceResolution = null,
            CanvasScaler.ScreenMatchMode screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
            float matchWidthOrHeight = 1f,
            float referencePixelsPerUnit = 100f) {
            _canvasScaler.fallbackScreenDPI = 96f;
            _canvasScaler.defaultSpriteDPI = 96f;
            _canvasScaler.scaleFactor = 1f;
            //
            _canvasScaler.uiScaleMode = scaleMode;
            _canvasScaler.referenceResolution = referenceResolution ?? _referenceResolution;
            _canvasScaler.screenMatchMode = screenMatchMode;
            _canvasScaler.matchWidthOrHeight = matchWidthOrHeight;
            _canvasScaler.referencePixelsPerUnit = referencePixelsPerUnit;
        }
        //void SetCanvasScalerConstantPixelSize(
        //       float scaleFactor = 1f,
        //       float referencePixelsPerUnit = 100f) {
        //    _canvasScaler.uiScaleMode =  CanvasScaler.ScaleMode.ConstantPixelSize;
        //    _canvasScaler.scaleFactor = scaleFactor;
        //    _canvasScaler.referencePixelsPerUnit = referencePixelsPerUnit;
        //}
        //void SetCanvasScalerConstantPhysicalSize(
        //   CanvasScaler.Unit physicalUnit = CanvasScaler.Unit.Points,
        //   float fallbackScreenDPI = 96f,
        //   float defaultSpriteDPI = 96f,
        //   float referencePixelsPerUnit = 100f) {
        //    _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPhysicalSize;
        //    _canvasScaler.physicalUnit = physicalUnit;
        //    _canvasScaler.fallbackScreenDPI = fallbackScreenDPI;
        //    _canvasScaler.defaultSpriteDPI = defaultSpriteDPI;
        //    _canvasScaler.referencePixelsPerUnit = referencePixelsPerUnit;
        //}
    }

    /// <summary>
    /// 黑边管理器（自动创建）
    /// </summary>
    public class UIBackgroundMask : MonoBehaviour
    {
        [SerializeField] internal Camera _uiCamera;
        [SerializeField] private Color _maskColor = Color.black;
        [SerializeField] internal Vector2 _referenceResolution = new(1920,1080);

        private RectTransform _leftMask;
        private RectTransform _rightMask;
        private RectTransform _topMask;
        private RectTransform _bottomMask;
        private RectTransform _selfRect;
        private float _referenceAspectRatio;
           
        [SerializeField]
        private List<RectTransform> _uiLayers=new List<RectTransform>();
        private void Awake() {
            _selfRect = this.GetComponent<RectTransform>();
            _referenceAspectRatio = _referenceResolution.x / _referenceResolution.y;

            _leftMask = CreateMask("LeftMask",new Vector2(0,0.5f),new Vector2(0,0),new Vector2(0,1f));
            _rightMask = CreateMask("RightMask",new Vector2(1,0.5f),new Vector2(1,0f),new Vector2(1,1f));
            _topMask = CreateMask("TopMask",new Vector2(0.5f,1f),new Vector2(0f,1),new Vector2(1f,1));
            _bottomMask = CreateMask("BottomMask",new Vector2(0.5f,0f),new Vector2(0f,0),new Vector2(1f,0));

            HideAllMasks();
        }
        private void Start() {
            if (_uiLayers.Count == 0) {
                _uiLayers = new List<RectTransform>();
                foreach (UILayer layer in Enum.GetValues(typeof(UILayer))) {
                    var layobj = this.transform.parent.Find(layer.ToString()) as RectTransform;
                    if (layobj != null) {
                        _uiLayers.Add(layobj);
                    }
                }
            }
            UpdateMaskSize();
        }
        private RectTransform CreateMask(string name,Vector2 pivot,Vector2 anchorMin,Vector2 anchorMax) {
            var mk = this.transform.Find(name);
            GameObject maskObj = mk?.gameObject;
            if (maskObj == null) {
                maskObj = new GameObject(name,typeof(RectTransform));
                maskObj.transform.SetParent(transform);
            }
            maskObj.transform.localScale = Vector3.one;
            maskObj.SetActive(false);

            RectTransform rectTrans = maskObj.GetComponent<RectTransform>();
            rectTrans.anchorMin = anchorMin;
            rectTrans.anchorMax = anchorMax;
            rectTrans.pivot = pivot;
            rectTrans.offsetMin = Vector2.zero;
            rectTrans.offsetMax = Vector2.zero;

            Image maskImage = maskObj.GetComponent<Image>();
            if (maskImage == null) maskImage = maskObj.AddComponent<Image>();
            maskImage.color = _maskColor;
            maskImage.raycastTarget = false;
            maskImage.maskable = false;

            return rectTrans;
        }

        /// <summary>
        /// 更新黑边大小
        /// </summary>
        public void UpdateMaskSize() {
            if (_uiCamera == null) {
                Debug.LogError("UIBackgroundMask: 未绑定UICamera");
                HideAllMasks();
                return;
            }

            float currentAspectRatio = (float)Screen.width / Screen.height;

            if (Mathf.Abs(currentAspectRatio - _referenceAspectRatio) < 0.01f) {
                HideAllMasks();
                return;
            }

            if (currentAspectRatio > _referenceAspectRatio) {
                ShowHorizontalMasks(currentAspectRatio);
            }
            else {
                ShowVerticalMasks(currentAspectRatio);
            }
        }

        private void ShowHorizontalMasks(float currentAspectRatio) {
            float validScreenWidth = Screen.height * _referenceAspectRatio;
            float maskWidth = (Screen.width - validScreenWidth) / 2;

            //_leftMask.sizeDelta = new Vector2(maskWidth,Screen.height);
            //_leftMask.anchoredPosition = new Vector2(maskWidth / 2,0);
            _leftMask.sizeDelta = new Vector2(maskWidth,0f);
            _leftMask.gameObject.SetActive(true);

            //_rightMask.sizeDelta = new Vector2(maskWidth,Screen.height);
            //_rightMask.anchoredPosition = new Vector2(Screen.width - maskWidth / 2,0);
            _rightMask.sizeDelta = new Vector2(maskWidth,0f);
            _rightMask.gameObject.SetActive(true);

            _topMask.gameObject.SetActive(false);
            _bottomMask.gameObject.SetActive(false);
            //
            foreach (var rectTrans in _uiLayers) {
                AdaptCenterContain(rectTrans,true,maskWidth);
            }
        }

        private void ShowVerticalMasks(float currentAspectRatio) {
            float validScreenHeight = Screen.width / _referenceAspectRatio;
            float maskHeight = (Screen.height - validScreenHeight) / 2;

            //_topMask.sizeDelta = new Vector2(Screen.width,maskHeight);
            _topMask.sizeDelta = new Vector2(0f,maskHeight);
            //_topMask.anchoredPosition = new Vector2(0,Screen.height - maskHeight / 2);
            _topMask.gameObject.SetActive(true);

            //_bottomMask.sizeDelta = new Vector2(Screen.width,maskHeight);
            //_bottomMask.anchoredPosition = new Vector2(0,maskHeight / 2);
            _bottomMask.sizeDelta = new Vector2(0f,maskHeight);
            _bottomMask.gameObject.SetActive(true);

            _leftMask.gameObject.SetActive(false);
            _rightMask.gameObject.SetActive(false);
            //
            foreach (var rectTrans in _uiLayers) {
                AdaptCenterContain(rectTrans,false,maskHeight);
            }
        }
        /// <summary>
        /// 适配中间容器（填充模式）- 动态计算偏移量，让容器填充黑边内区域
        /// </summary>
        public virtual void AdaptCenterContain(RectTransform center,bool horizontal,float offsetSize) {
            if (_selfRect == null||center==null ) return;          
            // 限制最小黑边（避免容器超出遮罩）
            float offset = Mathf.Max(offsetSize,0);
            // 关键：通过offsetMin/offsetMax实现填充拉伸
            // offsetMin：左、下偏移（等于黑边宽度）→ 容器左边距=黑边宽度
            // offsetMax：右、上偏移（负的黑边宽度）→ 容器右边距=黑边宽度
            if (horizontal) {
                center.offsetMin = new Vector2(offset,0);
                center.offsetMax = new Vector2(-offset,0);
            }
            else {
                center.offsetMin = new Vector2(0,offset);
                center.offsetMax = new Vector2(0,-offset);
            }
                    
        }

     
        /// <summary>
        /// 隐藏所有黑边
        /// </summary>
        public void HideAllMasks() {
            _leftMask?.gameObject.SetActive(false);
            _rightMask?.gameObject.SetActive(false);
            _topMask?.gameObject.SetActive(false);
            _bottomMask?.gameObject.SetActive(false);
            foreach (var rectTrans in _uiLayers) {
                rectTrans.anchorMin = Vector2.zero;
                rectTrans.anchorMax = Vector2.one;
                rectTrans.offsetMin = Vector2.zero;
                rectTrans.offsetMax = Vector2.zero;
                rectTrans.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 动态修改黑边颜色
        /// </summary>
        public void SetMaskColor(Color newColor) {
            _maskColor = newColor;
            if (_leftMask != null) _leftMask.GetComponent<Image>().color = newColor;
            if (_rightMask != null) _rightMask.GetComponent<Image>().color = newColor;
            if (_topMask != null) _topMask.GetComponent<Image>().color = newColor;
            if (_bottomMask != null) _bottomMask.GetComponent<Image>().color = newColor;
        }
    }
}
