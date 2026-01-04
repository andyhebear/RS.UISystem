using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif


namespace RS.Unity3DLib.UISystem.UIEffect
{
    [RequireComponent(typeof(RawImage))]
    public class UIRawImageBackgroundBlur : MonoBehaviour
    {
        // ################################ 常量定义 ################################
        private const string ShaderName = "RS/UISystem/StaticBlur";
        private const CameraEvent CaptureCameraEvent = CameraEvent.AfterEverything;
        private static int s_CopyId;
        private static int s_EffectId1;
        private static int s_EffectId2;
        private const float ROTATION_EPSILON = 0.01f; // 旋转检测精度阈值（避免浮点误差）
        private const float SCALE_EPSILON = 0.001f;  // 缩放检测精度阈值
        // ################################ 枚举定义（对齐原脚本） ################################
        /// <summary> 模糊等级（与 Shader 的 UI_BLUR_* 对应） </summary>
        public enum BlurMode
        {
            None = 0,
            Fast,    // 4采样点（性能最优）
            Medium,  // 6采样点（平衡）
            Detail   // 8采样点（效果最好）
        }

        /// <summary> 降采样率（控制纹理尺寸） </summary>
        public enum DesamplingRate
        {
            None = 0,
            x1 = 1,
            x2 = 2,
            x4 = 4,
            x8 = 8,
        }

        // ################################ 序列化参数（对齐原脚本+新增优化） ################################
        [Header("模糊核心配置")]
        [SerializeField] [Range(0,4)] private float m_BlurIntensity = 1f; // 模糊强度（0=无模糊）
        [SerializeField] private BlurMode m_BlurMode = BlurMode.Fast;    // 模糊等级
        [SerializeField] private DesamplingRate m_DesamplingRate = DesamplingRate.x2; // 捕获降采样（控制最终纹理尺寸）
        [SerializeField] private DesamplingRate m_ReductionRate = DesamplingRate.x1;  // 模糊计算尺寸降采样
        [SerializeField] [Range(1,8)] private int m_Iterations = 1;       // 模糊迭代次数（叠加效果）
        [SerializeField] private FilterMode m_FilterMode = FilterMode.Bilinear; // 纹理过滤模式

        [Header("动态模糊配置")]
        [SerializeField] private bool m_EnableDynamicBlur = true;          // 是否启用动态更新
        [SerializeField] [Range(0.05f,0.5f)] private float m_UpdateInterval = 0.2f; // 动态更新间隔
        [Header("限时更新设置")]
        [Tooltip("是否开启更新限时（超过时长后停止调用 Capture()）")]
        public bool enableCaptureTimeLimit = true;
        [Tooltip("更新限时时长（秒）：超过此时间后不再执行 Capture()")]
        public float captureTimeLimit = 1f; // 示例：1秒后停止 Capture()
        [Header("显示配置(全屏覆盖)")]
        [SerializeField] private bool m_KeepCanvasSize = true;             // 适配根 Canvas 尺寸
        //[SerializeField] private bool m_BlockRaycasts = false;             // 是否阻挡点击（默认不阻挡）
        [SerializeField] private Material m_CustomEffectMaterial;          // 自定义模糊材质（可选）

        [Header("画质配置")]
        [SerializeField] [Range(0,4)] private int m_AntiAliasLevel = 2;   // 抗锯齿等级（0=无，2=2xMSAA，4=4xMSAA）

        // ################################ 私有字段 ################################
        private RawImage _targetRawImage;          // 显示模糊纹理的 RawImage
        private RectTransform _rawImageRect;       // RawImage 的 RectTransform
        private CanvasScaler _rootCanvas;                // 根 Canvas（仅查找，不自动创建）
        private Camera _captureCamera;             // 截图相机（非UI相机，按depth排序）
        private RenderTexture _capturedRT;         // 捕获+模糊后的最终纹理
        private RenderTexture _rtToRelease;        // 待释放的临时 RenderTexture
        private CommandBuffer _captureBuffer;      // 屏幕捕获的 CommandBuffer
        private Material _effectMaterial;          // 模糊效果材质（自动生成/自定义）
        private float _lastUpdateTime;             // 动态更新计时
        private bool _isCapturing;                 // 是否正在捕获中（避免重复调用）
        private bool _isParamsChanged = false;     // 参数变更标记（强制重建CommandBuffer）
                                                   // 
                                                   // 缓存用于检测变化的状态（基于「根Canvas相对基准」）
        private Vector3 _lastHierarchyLocalScale;  // 上一帧「层级累积本地缩放」
        private Quaternion _lastHierarchyLocalRotation; // 上一帧「层级累积本地旋转」
        private Vector2 _lastRootCanvasRectSize;   // 上一帧根Canvas的Rect尺寸
        private float _captureStartTime; // Capture() 计时起点（脚本激活时）        
       
        // ################################ 公共属性（对外访问+参数变更标记） ################################
        public float BlurIntensity {
            get => m_BlurIntensity;
            set {
                m_BlurIntensity = Mathf.Clamp(value,0,4);
                _isParamsChanged = true;
            }
        }

        public BlurMode CurrentBlurMode {
            get => m_BlurMode;
            set {
                m_BlurMode = value;
                UpdateShaderKeywords();
                _isParamsChanged = true;
            }
        }

        public int Iterations {
            get => m_Iterations;
            set {
                m_Iterations = Mathf.Clamp(value,1,8);
                _isParamsChanged = true;
            }
        }

        public DesamplingRate CaptureDesamplingRate {
            get => m_DesamplingRate;
            set {
                m_DesamplingRate = value;
                _isParamsChanged = true;
            }
        }

        public DesamplingRate BlurDesamplingRate {
            get => m_ReductionRate;
            set {
                m_ReductionRate = value;
                _isParamsChanged = true;
            }
        }

        public bool IsDynamicBlurEnabled {
            get => m_EnableDynamicBlur;
            set => m_EnableDynamicBlur = value;
        }

        public int AntiAliasLevel {
            get => m_AntiAliasLevel;
            set {
                m_AntiAliasLevel = Mathf.Clamp(value,0,4);
                _isParamsChanged = true;
            }
        }

        public RenderTexture CapturedTexture => _capturedRT;

        // ################################ 生命周期方法 ################################
        private void Awake() {
            _InitializeComponents();
            _InitializeShaderAndMaterial();
            _InitializeBufferIds();
        }

        private void OnEnable() {
            if (_rootCanvas == null || _captureCamera == null || !_targetRawImage.enabled)
                return;

            if (m_KeepCanvasSize) {
                _AdaptToCanvasSize(); // 初始化适配：中心锚点+支点+居中+1:1缩放+全屏
                _CacheHierarchyBaseStates(); // 缓存初始层级状态
            }

            Capture(); // 首次捕获模糊纹理
            _lastUpdateTime = Time.time - m_UpdateInterval; // 确保立即触发动态更新
            _captureStartTime = Time.time;           
        }

        private void Update() {
            if (_rootCanvas == null || _captureCamera == null || !_targetRawImage.enabled)
                return;
            // 动态校准：检测父节点位置/旋转/缩放、Canvas尺寸/位置变化，实时修正
            if (m_KeepCanvasSize)
                _DynamicCalibrateHierarchyAndSize();
            // 动态模糊：定时更新（避免重复捕获）
            if (m_EnableDynamicBlur && !_isCapturing && Time.time - _lastUpdateTime >= m_UpdateInterval) {
                //限时更新控制
                if (enableCaptureTimeLimit&& Time.time - _captureStartTime <= captureTimeLimit) {
                    Capture();
                }
                else {
                    Capture();
                }
                _lastUpdateTime = Time.time;
            }
        }

        private void OnDisable() {
            _Release(false); // 禁用时不释放最终纹理（避免闪白）
        }

        private void OnDestroy() {
            _Release(true); // 销毁时彻底释放所有资源

            // 销毁运行时创建的Material（排除自定义材质，避免内存泄漏）
            if (_effectMaterial != null && _effectMaterial != m_CustomEffectMaterial) {
#if UNITY_EDITOR              
                    DestroyImmediate(_effectMaterial);     
#else            
                    Destroy(_effectMaterial);                
#endif
                _effectMaterial = null;
            }
        }

        // ################################ 核心初始化方法 ################################
        /// <summary> 初始化 RawImage、Canvas、Camera 等核心组件（不自动创建Canvas） </summary>
        private void _InitializeComponents() {
            // 获取/添加 RawImage 组件
            _targetRawImage = GetComponent<RawImage>();
            if (_targetRawImage == null)
                _targetRawImage = gameObject.AddComponent<RawImage>();

            // 配置 RawImage 基础属性
            _rawImageRect = _targetRawImage.rectTransform;
            //_targetRawImage.raycastTarget = m_BlockRaycasts; // 控制是否阻挡点击
            _targetRawImage.maskable = false;                // 不参与 UI Mask 裁剪
            //_targetRawImage.enabled = true;

            // 查找根 Canvas（不自动创建，找不到则报错）
            var rootCanvas = _FindRootCanvas(transform);
            _rootCanvas = rootCanvas?.GetComponent<CanvasScaler>();
            if (_rootCanvas == null) {
                Debug.LogError($"[{nameof(UIRawImageBackgroundBlur)}] 未找到父级 Canvas！请将该组件挂载到 Canvas 下的物体，或给物体添加 Canvas 组件");
                _targetRawImage.enabled = false;
#if UNITY_EDITOR
                //GameObject canvasObj = new GameObject("BlurRootCanvas",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
                //canvasObj.transform.SetParent(transform.parent);
                //_rootCanvas = canvasObj.GetComponent<Canvas>();
                //_rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                //transform.SetParent(canvasObj.transform);
#endif
                return;
            }
            // 

            // 筛选非UI相机作为截图相机（按depth降序排序，取最上层）
            _captureCamera = _FindNonUICamera();
            _ValidateCamera();
        }
        /// <summary> 递归查找根Canvas（最顶层，无父Canvas的节点） </summary>
        private Canvas _FindRootCanvas(Transform currentTransform) {
            if (currentTransform == null)
                return null;

            // 查找当前节点及所有父节点中的Canvas，筛选根Canvas（父节点无Canvas）
            Canvas currentCanvas = currentTransform.GetComponent<Canvas>();
            if (currentCanvas != null) {
                Transform parent = currentTransform.parent;
                while (parent != null) {
                    if (parent.GetComponent<Canvas>() != null)
                        return _FindRootCanvas(parent); // 父节点有Canvas，继续向上查找
                    parent = parent.parent;
                }
                return currentCanvas; // 父节点无Canvas，当前为根Canvas
            }

            // 当前节点无Canvas，向上递归
            return _FindRootCanvas(currentTransform.parent);
        }
        /// <summary> 初始化 Shader 和材质（自动生成变体/使用自定义材质） </summary>
        private void _InitializeShaderAndMaterial() {
            if (!_targetRawImage.enabled)
                return;

            // 优先使用自定义材质
            if (m_CustomEffectMaterial != null) {
                _effectMaterial = new Material(m_CustomEffectMaterial) { hideFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable };
                UpdateShaderKeywords();
                return;
            }

            // 加载 StaticBlur Shader
            Shader blurShader = Shader.Find(ShaderName);
            if (blurShader == null) {
                Debug.LogError($"[{nameof(UIRawImageBackgroundBlur)}] 未找到 Shader：{ShaderName}，请检查Shader路径是否正确");
                _targetRawImage.enabled = false;
                return;
            }

            // 生成/获取 Material 变体（Editor模式复用原逻辑，运行时直接创建）
#if UNITY_EDITOR
           var effectMaterial = _GetOrGenerateMaterialVariant(blurShader,m_BlurMode);
            _effectMaterial = new Material(effectMaterial);
            _effectMaterial.name = $"{ShaderName}_{m_BlurMode}";
            _effectMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            UpdateShaderKeywords();
#else
            _effectMaterial = new Material(blurShader);
            _effectMaterial.name = $"{ShaderName}_{m_BlurMode}";
            _effectMaterial.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            UpdateShaderKeywords();
#endif
        }

        /// <summary> 初始化 CommandBuffer 所需的纹理ID（静态变量，仅初始化一次） </summary>
        private void _InitializeBufferIds() {
            if (s_CopyId == 0) {
                s_CopyId = Shader.PropertyToID("_StaticBluredScreen_ScreenCopyId");
                s_EffectId1 = Shader.PropertyToID("_StaticBluredScreen_EffectId1");
                s_EffectId2 = Shader.PropertyToID("_StaticBluredScreen_EffectId2");
            }
        }

        /// <summary> 筛选场景中可用的非UI相机（按depth降序排序，取最上层） </summary>
        /// <returns>depth最大的非UI相机，无则返回主相机，仍无则返回null</returns>
        private Camera _FindNonUICamera() {
            // 获取所有启用的相机（排除禁用的）
            Camera[] allCameras = Camera.allCameras.Where(cam => cam.enabled).ToArray();
            if (allCameras.Length == 0) {
                Debug.LogWarning($"[{nameof(UIRawImageBackgroundBlur)}] 场景中无任何启用的相机！");
                return null;
            }

            // 筛选非UI相机：cullingMask 不只是 UI 层（默认UI层索引为5）
            int uiLayerMask = 1 << LayerMask.NameToLayer("UI");
            Camera[] nonUICameras = allCameras
                .Where(cam => cam.cullingMask != uiLayerMask) // 排除仅渲染UI的相机
                .ToArray();

            if (nonUICameras.Length == 0) {
                Debug.LogWarning($"[{nameof(UIRawImageBackgroundBlur)}] 未找到纯非UI相机，将尝试使用主相机兜底");
                return Camera.main;
            }

            // 按相机depth降序排序（depth越大，渲染层级越靠上，优先选择）
            Camera selectedCamera = nonUICameras.OrderByDescending(cam => cam.depth).First();

            // Editor模式下输出排序信息（方便调试）
#if UNITY_EDITOR
            string cameraOrderLog = $"非UI相机排序（depth降序）：\n";
            foreach (var cam in nonUICameras.OrderByDescending(c => c.depth)) {
                cameraOrderLog += $"  - {cam.name} (depth: {cam.depth}, cullingMask: 0x{cam.cullingMask:X})\n";
            }
            cameraOrderLog += $"最终选中：{selectedCamera.name} (depth: {selectedCamera.depth})";
            Debug.Log($"[{nameof(UIRawImageBackgroundBlur)}] {cameraOrderLog}");
#endif

            return selectedCamera;
        }

        /// <summary> 验证相机有效性（适配非UI相机逻辑） </summary>
        private void _ValidateCamera() {
            if (_captureCamera == null) {
                Debug.LogError($"[{nameof(UIRawImageBackgroundBlur)}] 未找到可用的非UI相机！请检查：\n1. 场景中是否存在启用的非UI相机（cullingMask 需包含非UI层，如 Default 层）；\n2. 若仅存在一个相机，确保其 cullingMask 不只是 UI 层");
                _targetRawImage.enabled = false;
                return;
            }

            // 警告：如果最终使用的相机仍包含UI层（可选，根据需求决定是否保留）
            int uiLayerMask = 1 << LayerMask.NameToLayer("UI");
            if ((_captureCamera.cullingMask & uiLayerMask) != 0) {
                Debug.LogWarning($"[{nameof(UIRawImageBackgroundBlur)}] 截图相机（{_captureCamera.name}）包含 UI 层，可能导致模糊纹理中叠加UI内容！建议给截图相机设置独立的 cullingMask（仅包含场景层）");
            }
        }

        // ################################ 核心功能方法 ################################
        /// <summary> 捕获屏幕并应用模糊（核心方法，修复参数变更不生效问题） </summary>
        public void Capture() {
            if (_effectMaterial == null || !_targetRawImage.enabled)
                return;
            if (_captureCamera == null) {
                _captureCamera = _FindNonUICamera();
                if (_captureCamera == null) return;
            }

            _isCapturing = true;

            // 1. 计算降采样后的尺寸（捕获尺寸 + 模糊计算尺寸）
            int captureW, captureH;
            _GetDesamplingSize(m_DesamplingRate,out captureW,out captureH);

            int blurW, blurH;
            _GetDesamplingSize(m_ReductionRate,out blurW,out blurH);

            // 2. 重建 RenderTexture（尺寸/抗锯齿等级变化时）
            if (_capturedRT == null ||
                _capturedRT.width != captureW ||
                _capturedRT.height != captureH ||
                _capturedRT.antiAliasing != m_AntiAliasLevel) {
                _rtToRelease = _capturedRT;
                _capturedRT = new RenderTexture(
                    captureW,captureH,0,
                    RenderTextureFormat.ARGB32,
                    QualitySettings.activeColorSpace == ColorSpace.Linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB
                );
                _capturedRT.filterMode = m_FilterMode;
                _capturedRT.useMipMap = false;
                _capturedRT.wrapMode = TextureWrapMode.Clamp;
                _capturedRT.antiAliasing = m_AntiAliasLevel; // 抗锯齿配置
                _capturedRT.hideFlags = HideFlags.HideAndDontSave;
            }

            // 3. 重建 CommandBuffer（参数变更/Buffer为空时）
            if (_isParamsChanged || _captureBuffer == null) {
                // 销毁旧Buffer（避免内存泄漏）
                if (_captureBuffer != null) {
                    if (_captureCamera != null)
                        _captureCamera.RemoveCommandBuffer(CaptureCameraEvent,_captureBuffer);
                    _captureBuffer.Release();
                    _captureBuffer = null;
                }

                _captureBuffer = new CommandBuffer();
                _captureBuffer.name = $"DynamicBlur_CaptureBuffer_{m_BlurMode}";
                _capturedRT.name = _captureBuffer.name;

                // 3.1 复制当前屏幕到临时纹理
                _captureBuffer.GetTemporaryRT(s_CopyId,-1,-1,0,m_FilterMode);
                _captureBuffer.Blit(BuiltinRenderTextureType.CurrentActive,s_CopyId);

                // 3.2 设置模糊参数（w=1始终启用UV翻转，修复迭代时翻转错误）
                _captureBuffer.SetGlobalVector("_EffectFactor",new Vector4(0,0,m_BlurIntensity,0));
                //_captureBuffer.SetGlobalVector("_EffectFactor",new Vector4(0,0,m_BlurIntensity,1));
                // 3.3 无模糊模式：直接复制屏幕纹理
                if (m_BlurMode == BlurMode.None) {
                    _captureBuffer.Blit(s_CopyId,new RenderTargetIdentifier(_capturedRT));
                    _captureBuffer.ReleaseTemporaryRT(s_CopyId);

                }
                // 3.4 有模糊模式：应用Shader + 迭代叠加
                else {

                    _captureBuffer.GetTemporaryRT(s_EffectId1,blurW,blurH,0,m_FilterMode);
                    _captureBuffer.Blit(s_CopyId,s_EffectId1,_effectMaterial);    // Apply effect (copied screen -> effect1).
                    _captureBuffer.ReleaseTemporaryRT(s_CopyId);
                    // 第一次模糊：初始不翻转UV（w=0），避免单次迭代时倒置   
                    // Iterate the operation.
                    _captureBuffer.SetGlobalVector("_EffectFactor",new Vector4(0,0,BlurIntensity,0));
                    if (1 < m_Iterations) {
                        _captureBuffer.GetTemporaryRT(s_EffectId2,blurW,blurH,0,m_FilterMode);
                        for (int i = 1; i < m_Iterations; i++) {
                            // Apply effect (effect1 -> effect2, or effect2 -> effect1).
                            _captureBuffer.Blit(i % 2 == 0 ? s_EffectId2 : s_EffectId1,i % 2 == 0 ? s_EffectId1 : s_EffectId2,_effectMaterial);
                        }
                    }

                    _captureBuffer.Blit(m_Iterations % 2 == 0 ? s_EffectId2 : s_EffectId1,_capturedRT);
                    _captureBuffer.ReleaseTemporaryRT(s_EffectId1);
                    if (1 < m_Iterations) {
                        _captureBuffer.ReleaseTemporaryRT(s_EffectId2);
                    }
                }

                _isParamsChanged = false; // 重置参数变更标记
            }
           
            // 4. 执行 CommandBuffer（添加到相机，下一帧生效）
            _captureCamera.AddCommandBuffer(CaptureCameraEvent,_captureBuffer);
            StartCoroutineApplyCapturedTexture();
        }
        private void StartCoroutineApplyCapturedTexture() {
            if (!this.enabled || _rootCanvas == null) return;
            _rootCanvas.StartCoroutine(_CoApplyCapturedTexture());
        }
        /// <summary> 延迟一帧应用捕获的纹理（确保CommandBuffer执行完成） </summary>
        private System.Collections.IEnumerator _CoApplyCapturedTexture() {
            yield return new WaitForEndOfFrame();

            // 移除CommandBuffer（避免重复执行）
            if (_captureCamera != null && _captureBuffer != null)
                _captureCamera.RemoveCommandBuffer(CaptureCameraEvent,_captureBuffer);

            // 应用纹理到RawImage
            if (_capturedRT != null && _targetRawImage != null)
                _targetRawImage.texture = _capturedRT;

            // 释放待回收的RenderTexture
            if (_rtToRelease != null) {
                _rtToRelease.Release();
                _rtToRelease = null;
            }

            _isCapturing = false;
        }

        /// <summary> 手动触发模糊更新（背景变化后调用，如滚动列表、动画结束） </summary>
        public void ForceUpdateBlur() {
            if (this.enabled == false) return;
            if (!_isCapturing && _captureCamera != null && _effectMaterial != null && _targetRawImage.enabled) {
                Capture();
                _lastUpdateTime = Time.time;
            }
        }

        /// <summary> 释放资源 </summary>
        /// <param name="releaseFinalRT">是否释放最终捕获的纹理（销毁时true，禁用时false）</param>
        private void _Release(bool releaseFinalRT) {
            // 移除并释放CommandBuffer
            if (_captureCamera != null && _captureBuffer != null) {
                _captureCamera.RemoveCommandBuffer(CaptureCameraEvent,_captureBuffer);
                _captureBuffer.Release();
                _captureBuffer = null;
            }

            // 释放临时纹理
            if (_rtToRelease != null) {
                _rtToRelease.Release();
                _rtToRelease = null;
            }

            // 销毁时释放最终纹理
            if (releaseFinalRT && _capturedRT != null) {
                _capturedRT.Release();
                _capturedRT = null;
                if (_targetRawImage != null)
                    _targetRawImage.texture = null;
            }

            _isCapturing = false;
        }
#region 动态计算保证全屏填充
        /// <summary> 适配根Canvas尺寸（全屏+置于最下层） </summary>
        private void _AdaptToCanvasSize() {
            if (_rootCanvas == null || _rawImageRect == null)
                return;

            RectTransform rootCanvasRect = _rootCanvas.transform as RectTransform;
            if (rootCanvasRect == null)
                return;
            var size = (_rootCanvas.transform as RectTransform).rect.size;
           
            _rawImageRect.anchorMin = new Vector2(0.5f,0.5f);
            _rawImageRect.anchorMax = new Vector2(0.5f,0.5f);
            _rawImageRect.pivot = new Vector2(0.5f,0.5f);          
            //_rawImageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,size.x);
            //_rawImageRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,size.y);
            // 2. 固定尺寸：与根Canvas完全一致（不依赖拉伸，直接设置sizeDelta）
            _rawImageRect.sizeDelta = rootCanvasRect.rect.size;
            // 3. 位置锁定：强制相对于根Canvas(0,0,0)居中（锚点在中心，anchoredPosition=0即完全对齐）
            _rawImageRect.anchoredPosition3D = Vector3.zero; // 锁定在根Canvas中心（0,0,0）
                                                             // 3. 三重抵消：位置+旋转+缩放（核心逻辑，完全脱离父节点影响）
                                                             // 5. 层级累积变换补偿：抵消嵌套层级的缩放/旋转，确保相对于根Canvas 1:1缩放+0旋转
            _CompensateHierarchyScale();  // 核心：相对于根Canvas缩放=1:1
            _CompensateHierarchyRotation();// 辅助：相对于根Canvas旋转=0
            _targetRawImage.maskable = false;

        }
        /// <summary> 动态校准：保持支点中心+居中位置+1:1缩放+全屏 </summary>
        private void _DynamicCalibrateHierarchyAndSize() {
            if (_rootCanvas == null || _rawImageRect == null)
                return;

            // 1. 锁定支点为中心（防止意外修改）
            if (_rawImageRect.pivot != new Vector2(0.5f,0.5f)) {
                _rawImageRect.pivot = new Vector2(0.5f,0.5f);
                _isParamsChanged = true;
            }

            // 2. 锁定锚点为中心单点（防止意外修改）
            if (_rawImageRect.anchorMin != new Vector2(0.5f,0.5f) || _rawImageRect.anchorMax != new Vector2(0.5f,0.5f)) {
                _rawImageRect.anchorMin = new Vector2(0.5f,0.5f);
                _rawImageRect.anchorMax = new Vector2(0.5f,0.5f);
                _isParamsChanged = true;
            }

            // 3. 锁定居中位置（防止意外偏移，确保始终相对于根Canvas(0,0,0)）
            if (_rawImageRect.anchoredPosition3D.sqrMagnitude > 0.001f) {
                _rawImageRect.anchoredPosition3D = Vector3.zero;
                _isParamsChanged = true;
            }

            // 4. 检测层级累积缩放变化 → 重新补偿缩放（保持相对于根Canvas 1:1）
            Vector3 currentHierarchyScale = _CalculateHierarchyLocalScale();
            if (Vector3.Distance(currentHierarchyScale,_lastHierarchyLocalScale) > SCALE_EPSILON) {
                _CompensateHierarchyScale();
                _lastHierarchyLocalScale = currentHierarchyScale;
                _isParamsChanged = true;
            }

            // 5. 检测层级累积旋转变化 → 重新补偿旋转（保持相对于根Canvas 0旋转）
            Quaternion currentHierarchyRotation = _CalculateHierarchyLocalRotation();
            if (Quaternion.Angle(currentHierarchyRotation,_lastHierarchyLocalRotation) > ROTATION_EPSILON) {
                _CompensateHierarchyRotation();
                _lastHierarchyLocalRotation = currentHierarchyRotation;
                _isParamsChanged = true;
            }

            // 6. 检测根Canvas Rect尺寸变化 → 同步更新尺寸（保持全屏）
            Vector2 currentRootCanvasSize = (_rootCanvas.transform as RectTransform).rect.size;
            if (Vector2.Distance(currentRootCanvasSize,_lastRootCanvasRectSize) > SCALE_EPSILON) {
                _rawImageRect.sizeDelta = currentRootCanvasSize; // 同步根Canvas尺寸
                _lastRootCanvasRectSize = currentRootCanvasSize;
                _isParamsChanged = true;
            }
        }

        // 以下方法保持不变（层级缩放/旋转计算、补偿逻辑）
        /// <summary> 计算「层级累积本地缩放」：从_rawImageRect到根Canvas的所有父节点本地缩放乘积 </summary>
        private Vector3 _CalculateHierarchyLocalScale() {
            if (_rootCanvas == null || _rawImageRect == null)
                return Vector3.one;

            Vector3 accumulatedScale = Vector3.one;
            Transform currentParent = _rawImageRect.parent;

            // 遍历从「_rawImageRect的直接父节点」到「根Canvas的子节点」（不包含根Canvas自身）
            while (currentParent != null && currentParent != _rootCanvas.transform) {
                RectTransform parentRect = currentParent as RectTransform;
                if (parentRect != null) {
                    // 累积本地缩放（取绝对值，避免负缩放导致翻转；限制最小值，避免除以零）
                    accumulatedScale.x *= Mathf.Max(Mathf.Abs(parentRect.localScale.x),SCALE_EPSILON);
                    accumulatedScale.y *= Mathf.Max(Mathf.Abs(parentRect.localScale.y),SCALE_EPSILON);
                    accumulatedScale.z = 1f; // 2D UI忽略Z轴缩放
                }
                currentParent = currentParent.parent;
            }

            return accumulatedScale;
        }

        /// <summary> 计算「层级累积本地旋转」：从_rawImageRect到根Canvas的所有父节点本地旋转乘积 </summary>
        private Quaternion _CalculateHierarchyLocalRotation() {
            if (_rootCanvas == null || _rawImageRect == null)
                return Quaternion.identity;

            Quaternion accumulatedRotation = Quaternion.identity;
            Transform currentParent = _rawImageRect.parent;

            // 遍历从「_rawImageRect的直接父节点」到「根Canvas的子节点」（不包含根Canvas自身）
            while (currentParent != null && currentParent != _rootCanvas.transform) {
                RectTransform parentRect = currentParent as RectTransform;
                if (parentRect != null) {
                    // 累积本地旋转（按层级顺序相乘）
                    accumulatedRotation *= parentRect.localRotation;
                }
                currentParent = currentParent.parent;
            }

            return accumulatedRotation;
        }

        /// <summary> 缩放补偿：让_rawImageRect相对于根Canvas的缩放=1:1 </summary>
        private void _CompensateHierarchyScale() {
            // 1. 计算层级累积缩放（嵌套父节点的缩放乘积）
            Vector3 hierarchyScale = _CalculateHierarchyLocalScale();

            // 2. 补偿逻辑：_rawImageRect本地缩放 = 1 / 累积缩放（抵消层级缩放，最终相对于根Canvas缩放=1:1）
            Vector3 compensateScale = new Vector3(
                1f / hierarchyScale.x,
                1f / hierarchyScale.y,
                1f // Z轴固定为1，2D UI无需缩放
            );

            // 3. 应用补偿缩放
            _rawImageRect.localScale = compensateScale;
        }

        /// <summary> 旋转补偿：让_rawImageRect相对于根Canvas的旋转=0 </summary>
        private void _CompensateHierarchyRotation() {
            // 1. 计算层级累积旋转（嵌套父节点的旋转乘积）
            Quaternion hierarchyRotation = _CalculateHierarchyLocalRotation();

            // 2. 补偿逻辑：_rawImageRect本地旋转 = 累积旋转的逆（抵消层级旋转，最终相对于根Canvas旋转=0）
            Quaternion compensateRotation = Quaternion.Inverse(hierarchyRotation);

            // 3. 应用补偿旋转
            _rawImageRect.localRotation = compensateRotation;
        }

        /// <summary> 缓存层级基准状态（用于动态检测变化） </summary>
        private void _CacheHierarchyBaseStates() {
            _lastHierarchyLocalScale = _CalculateHierarchyLocalScale();
            _lastHierarchyLocalRotation = _CalculateHierarchyLocalRotation();
            _lastRootCanvasRectSize = (_rootCanvas.transform as RectTransform).rect.size;
        }

#endregion
        /// <summary> 更新Shader Keyword（根据BlurMode启用对应的模糊等级） </summary>
        public void UpdateShaderKeywords() {
            if (_effectMaterial == null)
                return;

            // 先禁用所有模糊Keyword
            _effectMaterial.DisableKeyword("UI_BLUR_FAST");
            _effectMaterial.DisableKeyword("UI_BLUR_MEDIUM");
            _effectMaterial.DisableKeyword("UI_BLUR_DETAIL");

            // 根据当前模糊等级启用对应的Keyword
            switch (m_BlurMode) {
                case BlurMode.Fast:
                    _effectMaterial.EnableKeyword("UI_BLUR_FAST");
                    break;
                case BlurMode.Medium:
                    _effectMaterial.EnableKeyword("UI_BLUR_MEDIUM");
                    break;
                case BlurMode.Detail:
                    _effectMaterial.EnableKeyword("UI_BLUR_DETAIL");
                    break;
            }
        }

        /// <summary> 计算降采样后的尺寸（取2的幂+最小尺寸限制） </summary>
        private void _GetDesamplingSize(DesamplingRate rate,out int w,out int h) {
            if (_captureCamera == null) {
                w = Screen.width;
                h = Screen.height;
                return;
            }

            // 基础尺寸：相机像素尺寸（适配非UI相机的渲染尺寸）
            w = _captureCamera.pixelWidth;
            h = _captureCamera.pixelHeight;

            // 应用降采样（取最接近的2的幂，优化GPU性能）
            if (rate != DesamplingRate.None) {
                int scale = (int)rate;
                w = Mathf.ClosestPowerOfTwo(w / scale);
                h = Mathf.ClosestPowerOfTwo(h / scale);
            }

            // 确保尺寸不小于16（避免过小导致纹理异常）
            w = Mathf.Max(16,w);
            h = Mathf.Max(16,h);
        }

        // ################################ Editor 模式辅助方法（复用原脚本逻辑） ################################
#if UNITY_EDITOR
        /// <summary> 获取或生成Material变体（对齐原StaticBluredScreen逻辑） </summary>
        private Material _GetOrGenerateMaterialVariant(Shader shader,BlurMode blurMode) {
            if (shader == null)
                return null;

            // 查找已存在的变体
            Material existingMat = _FindExistingMaterialVariant(shader,blurMode);
            if (existingMat != null)
                return existingMat;

            // 生成新变体
            Debug.Log($"[{nameof(UIRawImageBackgroundBlur)}] 生成 Material 变体：{_GetVariantName(shader,blurMode)}");
            Material newMat = new Material(shader);

            // 启用对应的模糊Keyword
            if (blurMode != BlurMode.None)
                newMat.EnableKeyword("UI_BLUR_" + blurMode.ToString().ToUpper());

            newMat.name = _GetVariantName(shader,blurMode);
            newMat.hideFlags |= HideFlags.NotEditable;

            // 保存变体到资源目录
            string materialPath = _GetDefaultMaterialPath(shader);
            bool isMainAsset = (blurMode == BlurMode.None);

            if (isMainAsset) {
                Directory.CreateDirectory(Path.GetDirectoryName(materialPath));
                AssetDatabase.CreateAsset(newMat,materialPath);
            }
            else {
                newMat.hideFlags |= HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(newMat,materialPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return newMat;
        }

        /// <summary> 查找已存在的Material变体 </summary>
        private Material _FindExistingMaterialVariant(Shader shader,BlurMode blurMode) {
            string variantName = _GetVariantName(shader,blurMode);
            string shaderFileName = Path.GetFileName(shader.name);

            return AssetDatabase.FindAssets($"t:Material {shaderFileName}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path))
                .OfType<Material>()
                .FirstOrDefault(mat => mat.name == variantName);
        }

        /// <summary> 获取Material保存路径（对齐原脚本） </summary>
        private string _GetDefaultMaterialPath(Shader shader) {
            string shaderFileName = Path.GetFileName(shader.name);
            // 编辑器环境下直接获取shader文件路径
            string shaderPath = AssetDatabase.GetAssetPath(shader);
            string directoryPath = Path.GetDirectoryName(shaderPath);          
            string existingPath = AssetDatabase.FindAssets($"t:Material {shaderFileName}")
                .Select(guid => AssetDatabase.GUIDToAssetPath(guid))
                .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == shaderFileName);
            if (string.IsNullOrEmpty(existingPath)) {
               existingPath = directoryPath + $"/{Path.GetFileNameWithoutExtension(shaderFileName)}.mat";
            }
            return existingPath ;
        }
        //static string ResolveEditorDir() {
        //    var editorClass = $"{typeof(UIRawImageBackgroundBlur).Name}.cs";
        //    var editorPath = AssetDatabase.GetAllAssetPaths().First(path => { return path.Contains(editorClass); });
        //    return editorPath.Replace(editorClass,string.Empty);
        //}
        /// <summary> 获取Material变体名称（对齐原脚本） </summary>
        private string _GetVariantName(Shader shader,BlurMode blurMode) {
            string baseName = Path.GetFileName(shader.name);
            return blurMode == BlurMode.None ? baseName : $"{baseName}-{blurMode}";
        }

        /// <summary> Editor模式下参数变化时实时更新 </summary>
        private void OnValidate() {
            // 参数范围限制
            m_BlurIntensity = Mathf.Clamp(m_BlurIntensity,0,4);
            m_Iterations = Mathf.Clamp(m_Iterations,1,8);
            m_AntiAliasLevel = Mathf.Clamp(m_AntiAliasLevel,0,4);
            m_UpdateInterval = Mathf.Clamp(m_UpdateInterval,0.05f,0.5f);

            // 运行时参数变更实时生效
            if (Application.isPlaying && _effectMaterial != null && _targetRawImage.enabled) {
                UpdateShaderKeywords();
                _isParamsChanged = true;
                ForceUpdateBlur();
            }
        }
#endif
    }



}