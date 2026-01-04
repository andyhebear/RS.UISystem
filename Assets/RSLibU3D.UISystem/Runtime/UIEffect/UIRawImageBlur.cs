//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using UnityEngine;
//using UnityEngine.UI;
//using System.Collections;
//#if UNITY_EDITOR
//using UnityEditor;
//using UnityEditor.UI;
//#endif

//namespace RS.Unity3DLib.UISystem.UIEffect
//{
//    /// <summary>
//    /// 背景快速模糊实现，实现效果失败
//    /// </summary>    
//    public class UIRawImageBlur : MonoBehaviour
//    {
//        // 常量配置（集中管理，便于维护）
//        private const string _blurShaderName = "RS/UISystem/FastBlur";
//        private const RenderTextureFormat _rtFormat = RenderTextureFormat.ARGB32; // 兼容所有平台（含WebGL）
//        private const FilterMode _rtFilterMode = FilterMode.Bilinear; // 平衡模糊效果和性能

//        /// <summary> 降采样率（控制纹理尺寸） </summary>
//        public enum DesamplingRate : int
//        {

//            x1 = 1,
//            x2 = 2,
//            x4 = 4,
//            x8 = 8,
//        }
//        // 模糊偏好：Standard（性能优先）/ Sgx（质量优先）
//        public enum BlurPreference
//        {
//            /// <summary>
//            /// 性能优先
//            /// </summary>
//            StandardGauss = 0,
//            /// <summary>
//            /// 质量优先
//            /// </summary>
//            SgxGauss = 1,
//        }

//        // 模糊类型：静态（只抓1帧）/ 动态（实时更新）
//        public enum BlurType
//        {
//            /// <summary>
//            /// 静态（只抓1帧）
//            /// </summary>
//            Static = 0,
//            /// <summary>
//            /// 动态（实时更新）
//            /// </summary>
//            Dynamic = 1,
//        }
//        [SerializeField, Header("模糊RawImage对象")] private RawImage _blurLayer;
//        [SerializeField, Header("采样分辨率系数")] private DesamplingRate _Downsample = DesamplingRate.x4; // 固定1/4分辨率（提升性能）
//        // 
//        [Range(0.0f,10.0f), Header("模糊尺寸")]
//        public float blurSize = 3.0f;
//        [Range(1,5)]
//        public int blurIterations = 2;
//        [SerializeField] private BlurPreference blurPref = BlurPreference.StandardGauss;
//        [SerializeField, Header("更新模式")] private BlurType _currentBlurType;
//        /// <summary>
//        /// blurPref==BlurPreference.Dynamic时起效
//        /// </summary>
//        [Header("BlurType.Dynamic状态下限时更新Capture设置")]
//        [Tooltip("是否开启更新限时（超过时长后停止调用Capture）")]
//        public bool enableCaptureTimeLimit = true;
//        [Tooltip("更新限时时长（秒）：超过此时间后不再执行更新Capture，最小值0.1秒")]
//        public float captureTimeLimit = 1f; // 示例：1秒后停止 CaptureScreenToRT()

//        // 动态模糊优化参数
//        [Header("动态模糊优化")]
//        [Tooltip("动态模糊更新间隔（秒），0=每帧更新")]
//        public float dynamicUpdateInterval = 0.2f;

//        // 内部缓存（私有只读，避免外部修改）
//        private Material _blurMaterial;
//        private RenderTexture _sourceRT;
//        private RenderTexture _tempRT1;
//        private RenderTexture _tempRT2;

       
//        private bool _isBlurEnabled;
//        private bool _isDynamicUpdating;
//        private float _lastDynamicUpdateTime;

//        // 
//        private float _captureStartTime; // 截图计时起点（EnableBlur时开始）
//                                         //private bool _isCaptureTimeLimitExceeded; // 是否已超过截图限时
//        private bool _isCapturingCoroutineRunning = false;
//        // 状态校验（编辑器模式下可视化）
//        private bool IsInitialized => _blurMaterial != null && _blurLayer != null;

//        private void Awake() {
//            if (_blurLayer == null) {
//                _blurLayer = GetComponent<RawImage>();
//            }
//            // 初始化核心组件（容错性增强）
//            InitBlurMaterial();
//            InitRawImage();

//            // Shader未找到时直接禁用脚本，避免后续报错
//            if (!IsInitialized) {
//                enabled = false;
//#if UNITY_EDITOR
//                EditorUtility.DisplayDialog("模糊脚本初始化失败","请检查Shader路径：" + _blurShaderName,"确定");
//#endif
//            }
//        }

//        private void OnDestroy() {
//            ReleaseAllResources();
//        }
//        private void OnEnable() {
//            //Todo:根据配置开始capture
//            EnableBlur(this._currentBlurType);
//        }
//        // 编辑器模式下资源释放
//        private void OnDisable() {
//            DisableBlur();          
//        }
//        // 初始化模糊材质
//        private void InitBlurMaterial() {
//            if (_blurMaterial != null) return;

//            Shader blurShader = Shader.Find(_blurShaderName);
//            if (blurShader == null) {
//                Debug.LogError($"[UIRawImageBlur] 未找到模糊Shader：{_blurShaderName}，请检查Shader路径");
//                return;
//            }

//            _blurMaterial = new Material(blurShader) {
//                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor,
//                enableInstancing = false
//            };
//        }

//        // 初始化RawImage状态
//        private void InitRawImage() {
//            if (_blurLayer == null) return;

//            //_blurLayer.enabled = false;
//            _blurLayer.texture = null;
//            _blurLayer.raycastTarget = false;
//            _blurLayer.uvRect = new Rect(0,0,1,1);
//        }

//        /// <summary>
//        /// 启用模糊（外部调用API，兼容原有逻辑）
//        /// </summary>
//        internal void EnableBlur(BlurType blurType) {
//            if (!IsInitialized) {
//                Debug.LogError("[UIRawImageBlur] 脚本未初始化，无法启用模糊");
//                return;
//            }
             
//            StopAllCoroutines();
//            _isCapturingCoroutineRunning = false; // 重置协程标记
//            _currentBlurType = blurType;
//            _isBlurEnabled = true;
//            _isDynamicUpdating = blurType == BlurType.Dynamic;        
//            _lastDynamicUpdateTime = Time.time - dynamicUpdateInterval; // 确保首次更新立即执行
//            // ========== 初始化限时计时 ==========
//            _captureStartTime = Time.time;
//            //_isCaptureTimeLimitExceeded = false;

//            if (blurType == BlurType.Static) {
//                StartCoroutine(StaticBlurCoroutine());
//            }
//            else {
//                //_blurLayer.enabled = true;
//            }
//        }

//        /// <summary>
//        /// 禁用模糊（外部调用API，兼容原有逻辑）
//        /// </summary>
//        internal void DisableBlur() {
//            _isBlurEnabled = false;
//            _isDynamicUpdating = false;
//            //_isCaptureTimeLimitExceeded = false; // 重置限时状态

//            //if (_blurLayer != null) {
//            //    //_blurLayer.enabled = false;
//            //    _blurLayer.texture = null;
//            //}
//            //if (!Application.isPlaying) {
//            //    ReleaseAllResources();
//            //}
           
//        }

//        // 静态模糊协程（添加限时检查）
//        private IEnumerator StaticBlurCoroutine() {
//            yield return new WaitForEndOfFrame();

//            // 中途禁用/销毁/超时，直接退出
//            if (!_isBlurEnabled || !gameObject.activeInHierarchy) yield break;

//            // 未超时，执行截图和模糊
//            CaptureScreenToRT();
//            if (_sourceRT != null) {
//                RenderBlurEffect();
//                //_blurLayer.enabled = true;
//            }
//        }

//        // 动态模糊LateUpdate（添加限时检查）
//        private void Update() {
//            if (!_isBlurEnabled || _currentBlurType != BlurType.Dynamic || !_isDynamicUpdating) return;

//            // 检查是否超时：超时则停止动态更新
//            if (enableCaptureTimeLimit && Time.time - _captureStartTime >= captureTimeLimit) {
//                _isDynamicUpdating = false;
//                Debug.Log($"[UIRawImageBlur] 截图限时已到（{captureTimeLimit}秒），停止更新截图");
//                return;
//            }

//            // 未超时，按间隔更新
//            if (Time.time - _lastDynamicUpdateTime < dynamicUpdateInterval) return;

//            // 避免重复启动协程（关键：防止同一帧多次触发）
//            if (!_isCapturingCoroutineRunning) {
//                StartCoroutine(DynamicBlurCaptureCoroutine());
//            }
//        }
//        private IEnumerator DynamicBlurCaptureCoroutine() {
//            _isCapturingCoroutineRunning = true;

//            yield return new WaitForEndOfFrame(); // 帧结束后截图，解决报错

//            // 中途禁用/超时，直接退出
//            if (!_isBlurEnabled || !_isDynamicUpdating) {
//                _isCapturingCoroutineRunning = false;
//                yield break;
//            }

//            // 执行截图和模糊
//            CaptureScreenToRT();
//            if (_sourceRT != null) {
//                RenderBlurEffect();
//            }

//            // 更新最后一次更新时间
//            _lastDynamicUpdateTime = Time.time;
//            _isCapturingCoroutineRunning = false;
//        }



//        // 截图逻辑（未超时才执行，无需修改内部逻辑）
//        private void CaptureScreenToRT() {
//            // 安全校验：避免极端分辨率
//            int targetWidth = Mathf.Max(1,Screen.width / (int)_Downsample);
//            int targetHeight = Mathf.Max(1,Screen.height / (int)_Downsample);

//            // RT复用
//            if (_sourceRT == null || _sourceRT.width != targetWidth || _sourceRT.height != targetHeight) {
//                ReleaseRenderTexture(ref _sourceRT);
//                _sourceRT = CreateTemporaryRT(targetWidth,targetHeight);
//            }

//            // 抓取屏幕（销毁临时纹理，避免内存泄漏）
//            RenderTexture.active = _sourceRT;
//            GL.Clear(true,true,Color.clear);
//            //Texture2D tempScreenTex = ScreenCapture.CaptureScreenshotAsTexture();
//            //Graphics.Blit(tempScreenTex,_sourceRT);
//            //if (tempScreenTex != null) {// 额外校验：避免纹理创建失败
//            //    DestroyImmediate(tempScreenTex);
//            //}
//            ScreenCapture.CaptureScreenshotIntoRenderTexture(_sourceRT);
//            RenderTexture.active = null;
//        }

//        // 模糊渲染逻辑（无需修改）
//        private void RenderBlurEffect() {
//            if (_sourceRT == null || !IsInitialized) return;

//            int rtWidth = _sourceRT.width;
//            int rtHeight = _sourceRT.height;
//            float widthMod = 1.0f / (int)_Downsample;

//            // 复用临时RT
//            if (_tempRT1 == null || _tempRT1.width != rtWidth || _tempRT1.height != rtHeight) {
//                ReleaseRenderTexture(ref _tempRT1);
//                ReleaseRenderTexture(ref _tempRT2);
//                _tempRT1 = CreateTemporaryRT(rtWidth,rtHeight);
//                _tempRT2 = CreateTemporaryRT(rtWidth,rtHeight);
//            }

//            // 1. 降采样
//            _blurMaterial.SetVector("_Parameter",new Vector4(blurSize * widthMod,-blurSize * widthMod));
//            Graphics.Blit(_sourceRT,_tempRT1,_blurMaterial,0);

//            // 2. 迭代模糊
//            int passOffset = blurPref == BlurPreference.StandardGauss ? 0 : 2;
//            RenderTexture currentRT = _tempRT1;
//            RenderTexture targetRT = _tempRT2;

//            for (int i = 0; i < blurIterations; i++) {
//                float iterationOffset = i * 1.0f;
//                _blurMaterial.SetVector("_Parameter",new Vector4(
//                    blurSize * widthMod + iterationOffset,
//                    -blurSize * widthMod - iterationOffset
//                ));

//                // 垂直模糊 → 水平模糊
//                Graphics.Blit(currentRT,targetRT,_blurMaterial,1 + passOffset);
//                (currentRT, targetRT) = (targetRT, currentRT);
//                Graphics.Blit(currentRT,targetRT,_blurMaterial,2 + passOffset);
//                (currentRT, targetRT) = (targetRT, currentRT);
//            }

//            // 3. 赋值给RawImage
//            _blurLayer.texture = currentRT;
//        }

//        // 统一创建临时RT
//        private RenderTexture CreateTemporaryRT(int width,int height) {
//            RenderTexture rt = RenderTexture.GetTemporary(
//                width,height,
//                depthBuffer: 0
//                //format: _rtFormat,
//                //readWrite: RenderTextureReadWrite.Linear
//            );
//            rt.filterMode = _rtFilterMode;
//            rt.wrapMode = TextureWrapMode.Clamp;
//            return rt;
//        }

//        // 安全释放RT
//        private void ReleaseRenderTexture(ref RenderTexture rt) {
//            if (rt != null) {
//                RenderTexture.ReleaseTemporary(rt);
//                rt = null;
//            }
//        }

//        // 释放所有资源
//        private void ReleaseAllResources() {
//            StopAllCoroutines();
//            _isCapturingCoroutineRunning = false; // 重置协程标记
//            // 取消RawImage引用
//            if (_blurLayer != null) {
//                _blurLayer.texture = null;
//            }

//            // 释放RT
//            ReleaseRenderTexture(ref _sourceRT);
//            ReleaseRenderTexture(ref _tempRT1);
//            ReleaseRenderTexture(ref _tempRT2);

//            // 释放材质
//            if (_blurMaterial != null) {
//#if UNITY_EDITOR
//                if (!Application.isPlaying) {
//                    DestroyImmediate(_blurMaterial);
//                }
//                else
//#endif
//            {
//                    Destroy(_blurMaterial);
//                }
//                _blurMaterial = null;
//            }

//            // 重置限时状态
//            _captureStartTime = 0;
//            //_isCaptureTimeLimitExceeded = false;
//        }

        

//#if UNITY_EDITOR
//        // 编辑器参数校验
//        private void OnValidate() {
//            // 自动获取RawImage
//            if (_blurLayer == null) {
//                _blurLayer = GetComponent<RawImage>();
//            }

//            if (_blurLayer != null) {
//                //_blurLayer.raycastTarget = false;
//                _blurLayer.uvRect = new Rect(0,0,1,1);
//            }
//            InitBlurMaterial();
//            // 校验参数范围
//            blurIterations = Mathf.Clamp(blurIterations,1,5);
//            dynamicUpdateInterval = Mathf.Max(0f,dynamicUpdateInterval);
//            captureTimeLimit = Mathf.Max(0.1f,captureTimeLimit); // 限时最小0.1秒，避免误设为0
//        }
//#endif
//    }
//}
