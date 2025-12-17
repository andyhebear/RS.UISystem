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
    /// 通知层管理界面（内置界面）
    /// 支持多位置显示、自动隐藏、对象池复用，不阻塞UI交互
    /// </summary>
    public class UINotifyLayer : UIForm
    {
        [Header("通知项配置")]
        [SerializeField] private GameObject _notifyItemPrefab; // 通知项预制体（需包含Text组件）
        [SerializeField] private float _spacing = 10f; // 通知项之间的间距
        [SerializeField] private Vector2 _itemSize = new(300,60); // 通知项默认大小
        [SerializeField] private float _showDuration = 2f; // 默认显示时长（可通过接口覆盖）
        [SerializeField] private float _animDuration = 0.3f; // 显示/隐藏动画时长

        [Header("边距配置")]
        [SerializeField] private Vector2 _edgeOffset = new(20,20); // 通知项与屏幕边缘的间距

        // 通知项缓存池（按显示位置分类）
        private readonly Dictionary<NotifyPosition,Stack<GameObject>> _itemPools = new();
        // 活跃通知项列表（按显示位置分类，用于更新位置）
        private readonly Dictionary<NotifyPosition,List<GameObject>> _activeItems = new();

        protected override void Awake() {
            base.Awake();

            // 校验预制体
            if (_notifyItemPrefab == null) {
                Debug.LogError("UINotifyLayer: 未赋值 NotifyItemPrefab 预制体");
                return;
            }

            // 初始化缓存池和活跃列表（遍历所有通知位置）
            foreach (NotifyPosition position in Enum.GetValues(typeof(NotifyPosition))) {
                _itemPools.Add(position,new Stack<GameObject>());
                _activeItems.Add(position,new List<GameObject>());
            }

            // 初始化自身RectTransform（全屏填充，不影响其他UI）
            RectTransform rectTrans = GetComponent<RectTransform>();
            if (rectTrans != null) {
                rectTrans.anchorMin = Vector2.zero;
                rectTrans.anchorMax = Vector2.one;
                rectTrans.offsetMin = Vector2.zero;
                rectTrans.offsetMax = Vector2.zero;
                rectTrans.localScale = Vector3.one;
            }
        }

        /// <summary>
        /// 订阅通知显示事件（全局事件总线）
        /// </summary>
        protected override void SubscribeEvents() {
            UIEventBus.Subscribe<(string, NotifyPosition, float)>(UIEventNames.NotifyShow,OnNotifyShow);
        }

        /// <summary>
        /// 取消事件订阅（避免内存泄漏）
        /// </summary>
        protected override void UnsubscribeAllEvents() {
            UIEventBus.Unsubscribe<(string, NotifyPosition, float)>(UIEventNames.NotifyShow,OnNotifyShow);
        }

        /// <summary>
        /// 接收通知显示事件（核心回调）
        /// </summary>
        private void OnNotifyShow((string content, NotifyPosition position, float duration) param) {
            string content = param.content;
            NotifyPosition position = param.position;
            float duration = param.duration;

            // 校验参数
            if (string.IsNullOrEmpty(content)) {
                Debug.LogWarning("UINotifyLayer: 通知内容为空，跳过显示");
                return;
            }

            // 获取或创建通知项（从缓存池取，无则实例化）
            GameObject notifyItem = GetOrCreateNotifyItem(position);
            if (notifyItem == null) return;
            notifyItem.SetActive(true);
            // 设置通知内容
            Text notifyText = notifyItem.GetComponentInChildren<Text>();
            if (notifyText != null) {
                notifyText.text = content;
            }
            else {
                Debug.LogWarning("UINotifyLayer: 通知项预制体中未找到Text组件");
            }

            // 更新通知项位置（根据当前活跃列表调整堆叠顺序）
            UpdateNotifyItemPosition(notifyItem,position);

            // 显示通知项（带淡入动画）
            StartCoroutine(ShowNotifyItemCoroutine(notifyItem));

            // 添加到活跃列表
            _activeItems[position].Add(notifyItem);

            // 延迟隐藏通知项（支持自定义时长，默认2秒）
            float actualDuration = duration <= 0 ? _showDuration : duration;
            StartCoroutine(HideNotifyItemCoroutine(notifyItem,position,actualDuration));
        }

        /// <summary>
        /// 从缓存池获取或创建通知项
        /// </summary>
        private GameObject GetOrCreateNotifyItem(NotifyPosition position) {
            // 优先从对应位置的缓存池获取
            if (_itemPools[position].Count > 0) {
                GameObject item = _itemPools[position].Pop();
                item.transform.SetParent(transform);
                item.transform.localScale = Vector3.one;
                return item;
            }

            // 缓存池无可用项，实例化新通知项
            GameObject newItem = Instantiate(_notifyItemPrefab,transform);
            newItem.name = $"NotifyItem_{position}_{System.Guid.NewGuid().ToString().Substring(0,8)}";
            newItem.SetActive(false);

            // 配置通知项RectTransform（固定大小）
            RectTransform itemRect = newItem.GetComponent<RectTransform>();
            if (itemRect != null) {
                itemRect.sizeDelta = _itemSize;
                itemRect.anchorMin = Vector2.zero;
                itemRect.anchorMax = Vector2.zero;
                itemRect.pivot = new Vector2(0.5f,0.5f);
            }

            // 添加点击关闭事件（可选）
            Button closeBtn = newItem.GetComponentInChildren<Button>();
            if (closeBtn != null) {
                closeBtn.onClick.AddListener(() => {
                    // 立即隐藏并回收
                    StopCoroutine($"HideNotifyItemCoroutine_{newItem.GetInstanceID()}");
                    StartCoroutine(HideNotifyItemCoroutine(newItem,position,0));
                });
            }

            return newItem;
        }

        /// <summary>
        /// 更新通知项位置（处理堆叠逻辑）
        /// </summary>
        private void UpdateNotifyItemPosition(GameObject notifyItem,NotifyPosition position) {
            RectTransform itemRect = notifyItem.GetComponent<RectTransform>();
            if (itemRect == null) return;

            List<GameObject> activeItems = _activeItems[position];
            int itemIndex = activeItems.Count; // 当前通知项是第N个（从0开始）
            float totalHeight = (_itemSize.y + _spacing) * itemIndex; // 累计高度（包含间距）

            // 根据显示位置计算锚点和位置
            switch (position) {
                case NotifyPosition.TopLeft:
                    itemRect.anchoredPosition = new Vector2(
                        _edgeOffset.x + _itemSize.x / 2,
                        Screen.height - _edgeOffset.y - _itemSize.y / 2 - totalHeight
                    );
                    break;

                case NotifyPosition.TopRight:
                    itemRect.anchoredPosition = new Vector2(
                        Screen.width - _edgeOffset.x - _itemSize.x / 2,
                        Screen.height - _edgeOffset.y - _itemSize.y / 2 - totalHeight
                    );
                    break;

                case NotifyPosition.BottomLeft:
                    itemRect.anchoredPosition = new Vector2(
                        _edgeOffset.x + _itemSize.x / 2,
                        _edgeOffset.y + _itemSize.y / 2 + totalHeight
                    );
                    break;

                case NotifyPosition.BottomRight:
                    itemRect.anchoredPosition = new Vector2(
                        Screen.width - _edgeOffset.x - _itemSize.x / 2,
                        _edgeOffset.y + _itemSize.y / 2 + totalHeight
                    );
                    break;

                case NotifyPosition.Center:
                    itemRect.anchoredPosition = new Vector2(
                        Screen.width / 2,
                        Screen.height / 2 - totalHeight
                    );
                    break;
            }
        }

        /// <summary>
        /// 通知项显示动画（淡入+轻微上移）
        /// </summary>
        private IEnumerator ShowNotifyItemCoroutine(GameObject notifyItem) {
            notifyItem.SetActive(true);
            CanvasGroup canvasGroup = notifyItem.GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                canvasGroup = notifyItem.AddComponent<CanvasGroup>();
            }

            Vector3 originalPos = notifyItem.transform.localPosition;
            Vector3 startPos = originalPos + new Vector3(0,20,0); // 起始位置（上移20像素）

            notifyItem.transform.localPosition = startPos;
            canvasGroup.alpha = 0;

            float time = 0;
            while (time < _animDuration) {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / _animDuration);
                float easeT = Mathf.SmoothDamp(0,1,ref t,_animDuration); // 缓动动画

                notifyItem.transform.localPosition = Vector3.Lerp(startPos,originalPos,easeT);
                canvasGroup.alpha = Mathf.Lerp(0,1,easeT);
                yield return null;
            }

            notifyItem.transform.localPosition = originalPos;
            canvasGroup.alpha = 1;
        }

        /// <summary>
        /// 通知项隐藏动画（淡出+轻微下移）
        /// </summary>
        private IEnumerator HideNotifyItemCoroutine(GameObject notifyItem,NotifyPosition position,float delay) {
            // 延迟指定时间后执行隐藏
            if (delay > 0) {
                yield return new WaitForSeconds(delay);
            }

            CanvasGroup canvasGroup = notifyItem.GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                canvasGroup = notifyItem.AddComponent<CanvasGroup>();
            }

            Vector3 originalPos = notifyItem.transform.localPosition;
            Vector3 endPos = originalPos + new Vector3(0,-20,0); // 结束位置（下移20像素）

            float time = 0;
            while (time < _animDuration) {
                time += Time.deltaTime;
                float t = Mathf.Clamp01(time / _animDuration);
                float easeT = Mathf.SmoothDamp(1,0,ref t,_animDuration); // 缓动动画

                notifyItem.transform.localPosition = Vector3.Lerp(originalPos,endPos,1 - easeT);
                canvasGroup.alpha = Mathf.Lerp(1,0,1 - easeT);
                yield return null;
            }

            // 隐藏并回收至缓存池
            notifyItem.SetActive(false);
            RecycleNotifyItem(notifyItem,position);

            // 移除活跃列表，并更新其他通知项位置
            _activeItems[position].Remove(notifyItem);
            RefreshActiveItemsPosition(position);
        }

        /// <summary>
        /// 回收通知项至对应位置的缓存池
        /// </summary>
        private void RecycleNotifyItem(GameObject notifyItem,NotifyPosition position) {
            // 重置位置和状态
            notifyItem.transform.localPosition = Vector3.zero;
            notifyItem.transform.SetParent(null);

            // 回收至缓存池
            _itemPools[position].Push(notifyItem);
        }

        /// <summary>
        /// 刷新指定位置的活跃通知项位置（当有通知项被移除时）
        /// </summary>
        private void RefreshActiveItemsPosition(NotifyPosition position) {
            List<GameObject> activeItems = _activeItems[position];
            for (int i = 0; i < activeItems.Count; i++) {
                UpdateNotifyItemPosition(activeItems[i],position);
            }
        }

        /// <summary>
        /// 清空所有通知（外部调用接口）
        /// </summary>
        public void ClearAllNotifies() {
            foreach (var kvp in _activeItems) {
                NotifyPosition position = kvp.Key;
                List<GameObject> activeItems = kvp.Value;

                foreach (GameObject item in activeItems) {
                    StopCoroutine($"HideNotifyItemCoroutine_{item.GetInstanceID()}");
                    item.SetActive(false);
                    RecycleNotifyItem(item,position);
                }

                activeItems.Clear();
            }
        }

        /// <summary>
        /// 清空指定位置的通知（外部调用接口）
        /// </summary>
        public void ClearNotifiesByPosition(NotifyPosition position) {
            if (_activeItems.TryGetValue(position,out List<GameObject> activeItems)) {
                foreach (GameObject item in activeItems) {
                    StopCoroutine($"HideNotifyItemCoroutine_{item.GetInstanceID()}");
                    item.SetActive(false);
                    RecycleNotifyItem(item,position);
                }

                activeItems.Clear();
            }
        }

        /// <summary>
        /// 获取指定位置的活跃通知数量（外部调用接口）
        /// </summary>
        public int GetActiveNotifyCount(NotifyPosition position = NotifyPosition.TopRight) {
            return _activeItems.TryGetValue(position,out List<GameObject> activeItems) ? activeItems.Count : 0;
        }

        protected override IEnumerator ShowAnimationCoroutine(Action onComplete) {
            // 通知层本身无显示动画（仅管理子项）
            yield return null;
            OnShowComplete();
            onComplete?.Invoke();
        }

        protected override IEnumerator HideAnimationCoroutine(bool isDestroy,Action onComplete) {
            // 隐藏时清空所有通知
            ClearAllNotifies();
            yield return null;
            gameObject.SetActive(false);
            OnHideComplete(isDestroy);
            onComplete?.Invoke();
        }

        protected override void OnDestroy() {
            base.OnDestroy();

            // 销毁所有缓存池中的通知项（避免内存泄漏）
            foreach (var kvp in _itemPools) {
                Stack<GameObject> pool = kvp.Value;
                while (pool.Count > 0) {
                    Destroy(pool.Pop());
                }
            }

            _itemPools.Clear();
            _activeItems.Clear();
        }
    }
}
