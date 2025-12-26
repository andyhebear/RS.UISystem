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

        #region 私有字段

        private GameObject _maskPrefab;
        private GameObject _currentMask;
        private int _maskCount = 0;

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
            // 创建遮罩预制体
            CreateWaitingMaskPrefab();
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

            // 隐藏预制体
            _maskPrefab.SetActive(false);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 显示等待界面遮罩
        /// </summary>
        public void ShowWaitingMask(Transform parent,Action onClick = null) {
            _maskCount++;

            if (_currentMask == null) {
                _currentMask = GameObject.Instantiate(_maskPrefab,parent);

                // 设置点击事件
                if (onClick != null) {
                    var button = _currentMask.GetComponent<Button>();
                    button.onClick.AddListener(() => onClick());
                }

                // 播放淡入动画
                StartCoroutine(FadeInWaitingMask());
            }
        }

        /// <summary>
        /// 隐藏等待界面遮罩
        /// </summary>
        public void HideWaitingMask() {
            _maskCount--;

            if (_maskCount <= 0 && _currentMask != null) {
                _maskCount = 0;

                // 播放淡出动画
                StartCoroutine(FadeOutWaitingMask());
            }
        }

        /// <summary>
        /// 强制隐藏所有等待界面遮罩
        /// </summary>
        public void HideAllWaitingMasks() {
            _maskCount = 0;

            if (_currentMask != null) {
                StopAllCoroutines();
                GameObject.Destroy(_currentMask);
                _currentMask = null;
            }
        }

        /// <summary>
        /// 设置等待界面遮罩颜色
        /// </summary>
        public void SetWaitingMaskColor(Color color) {
            if (_maskPrefab != null) {
                var image = _maskPrefab.GetComponent<Image>();
                if (image != null) {
                    image.color = color;
                }
            }

            if (_currentMask != null) {
                var image = _currentMask.GetComponent<Image>();
                if (image != null) {
                    image.color = color;
                }
            }
        }

        #endregion

        #region 协程方法

        /// <summary>
        /// 等待界面遮罩淡入动画
        /// </summary>
        private IEnumerator FadeInWaitingMask() {
            if (_currentMask == null) yield break;

            var image = _currentMask.GetComponent<Image>();
            if (image == null) yield break;

            Color originalColor = image.color;
            image.color = new Color(originalColor.r,originalColor.g,originalColor.b,0f);

            float duration = 0.3f;
            float timer = 0f;

            while (timer < duration) {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(0f,originalColor.a,timer / duration);
                image.color = new Color(originalColor.r,originalColor.g,originalColor.b,alpha);
                yield return null;
            }

            image.color = originalColor;
        }

        /// <summary>
        /// 等待界面遮罩淡出动画
        /// </summary>
        private IEnumerator FadeOutWaitingMask() {
            if (_currentMask == null) yield break;

            var image = _currentMask.GetComponent<Image>();
            if (image == null) yield break;

            Color originalColor = image.color;

            float duration = 0.2f;
            float timer = 0f;

            while (timer < duration) {
                timer += Time.deltaTime;
                float alpha = Mathf.Lerp(originalColor.a,0f,timer / duration);
                image.color = new Color(originalColor.r,originalColor.g,originalColor.b,alpha);
                yield return null;
            }

            GameObject.Destroy(_currentMask);
            _currentMask = null;
        }

        #endregion

        #region 静态便捷方法

        public static void Show(Transform parent,Action onClick = null) {
            Instance.ShowWaitingMask(parent,onClick);
        }

        public static void Hide() {
            Instance.HideWaitingMask();
        }

        public static void HideAll() {
            Instance.HideAllWaitingMasks();
        }

        public static void SetColor(Color color) {
            Instance.SetWaitingMaskColor(color);
        }

        #endregion
    }
}
