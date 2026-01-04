using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// 全局UI事件总线，用于界面间解耦通信
    /// 支持事件订阅、取消订阅、触发，线程安全（Unity主线程调用）
    /// </summary>
    public static class UIEventBus
    {
        // 事件字典：key=事件名，value=事件回调列表
        private static readonly Dictionary<string,Delegate> _eventDict = new();
        // 线程锁（防止多线程操作冲突）
        private static readonly object _lockObj = new();
        // 事件类型映射：用于跟踪每种类型的事件
        private static readonly Dictionary<Type, List<string>> _eventTypeMap = new();

        #region 无参数事件
        public static void Subscribe(string eventName,Action callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (!_eventDict.ContainsKey(eventName)) {
                    _eventDict.Add(eventName,null);
                    // 记录事件类型映射
                    RegisterEventTypeMapping(typeof(Action), eventName);
                }
                _eventDict[eventName] = (Action)_eventDict[eventName] + callback;
            }
        }

        public static void Unsubscribe(string eventName,Action callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName,out var existingDelegate) && existingDelegate != null) {
                    existingDelegate = (Action)existingDelegate - callback;
                    if (existingDelegate == null) {
                        _eventDict.Remove(eventName);
                        // 移除事件类型映射
                        UnregisterEventTypeMapping(typeof(Action), eventName);
                    }
                    else {
                        _eventDict[eventName] = existingDelegate;
                    }
                }
            }
        }

        public static void Trigger(string eventName) {
            if (string.IsNullOrEmpty(eventName)) return;

            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName,out var existingDelegate)) {
                    (existingDelegate as Action)?.Invoke();
                }
            }
        }
        #endregion

        #region 单参数事件
        public static void Subscribe<T>(string eventName,Action<T> callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (!_eventDict.ContainsKey(eventName)) {
                    _eventDict.Add(eventName,null);
                    // 记录事件类型映射
                    RegisterEventTypeMapping(typeof(Action<T>), eventName);
                }
                _eventDict[eventName] = (Action<T>)_eventDict[eventName] + callback;
            }
        }

        public static void Unsubscribe<T>(string eventName,Action<T> callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName,out var existingDelegate) && existingDelegate != null) {
                    existingDelegate = (Action<T>)existingDelegate - callback;
                    if (existingDelegate == null) {
                        _eventDict.Remove(eventName);
                        // 移除事件类型映射
                        UnregisterEventTypeMapping(typeof(Action<T>), eventName);
                    }
                    else {
                        _eventDict[eventName] = existingDelegate;
                    }
                }
            }
        }

        public static void Trigger<T>(string eventName,T param) {
            if (string.IsNullOrEmpty(eventName)) return;

            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName,out var existingDelegate)) {
                    (existingDelegate as Action<T>)?.Invoke(param);
                }
            }
        }
        #endregion

        #region 双参数事件
        public static void Subscribe<T1, T2>(string eventName,Action<T1,T2> callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (!_eventDict.ContainsKey(eventName)) {
                    _eventDict.Add(eventName,null);
                    // 记录事件类型映射
                    RegisterEventTypeMapping(typeof(Action<T1,T2>), eventName);
                }
                _eventDict[eventName] = (Action<T1,T2>)_eventDict[eventName] + callback;
            }
        }

        public static void Unsubscribe<T1, T2>(string eventName,Action<T1,T2> callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName,out var existingDelegate) && existingDelegate != null) {
                    existingDelegate = (Action<T1,T2>)existingDelegate - callback;
                    if (existingDelegate == null) {
                        _eventDict.Remove(eventName);
                        // 移除事件类型映射
                        UnregisterEventTypeMapping(typeof(Action<T1,T2>), eventName);
                    }
                    else {
                        _eventDict[eventName] = existingDelegate;
                    }
                }
            }
        }

        public static void Trigger<T1, T2>(string eventName,T1 param1,T2 param2) {
            if (string.IsNullOrEmpty(eventName)) return;

            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName,out var existingDelegate)) {
                    (existingDelegate as Action<T1,T2>)?.Invoke(param1,param2);
                }
            }
        }
        #endregion

        /// <summary>
        /// 清空所有事件（仅在场景切换/游戏退出时使用）
        /// </summary>
        public static void ClearAllEvents() {
            lock (_lockObj) {
                _eventDict.Clear();
                _eventTypeMap.Clear();
            }
        }

        /// <summary>
        /// 检查事件是否存在订阅者
        /// </summary>
        public static bool HasSubscriber(string eventName) {
            lock (_lockObj) {
                return _eventDict.ContainsKey(eventName) && _eventDict[eventName] != null;
            }
        }

        /// <summary>
        /// 按前缀清理事件
        /// </summary>
        /// <param name="prefix">事件名前缀</param>
        public static void ClearEventsByPrefix(string prefix) {
            if (string.IsNullOrEmpty(prefix)) return;

            lock (_lockObj) {
                var eventsToRemove = _eventDict.Where(kv => kv.Key.StartsWith(prefix)).Select(kv => kv.Key).ToList();
                foreach (var eventName in eventsToRemove) {
                    _eventDict.Remove(eventName);
                    // 清理类型映射
                    foreach (var typeList in _eventTypeMap.Values) {
                        typeList.Remove(eventName);
                    }
                }
                // 清理空的类型映射
                CleanEmptyTypeMaps();
            }
        }

        /// <summary>
        /// 清理特定类型的事件（无参数事件）
        /// </summary>
        public static void ClearEventsOfType() {
            ClearEventsOfType<Action>();
        }

        /// <summary>
        /// 清理特定类型的事件（单参数事件）
        /// </summary>
        public static void ClearEventsOfType<T>() {
            ClearEventsOfTypeInternal(typeof(Action<T>));
        }

        /// <summary>
        /// 清理特定类型的事件（双参数事件）
        /// </summary>
        public static void ClearEventsOfType<T1, T2>() {
            ClearEventsOfTypeInternal(typeof(Action<T1, T2>));
        }

        /// <summary>
        /// 内部清理方法
        /// </summary>
        private static void ClearEventsOfTypeInternal(Type eventType) {
            lock (_lockObj) {
                if (_eventTypeMap.TryGetValue(eventType, out var eventNames)) {
                    var eventNamesToRemove = new List<string>(eventNames);
                    foreach (var eventName in eventNamesToRemove) {
                        _eventDict.Remove(eventName);
                    }
                    _eventTypeMap.Remove(eventType);
                }
            }
        }

        /// <summary>
        /// 记录事件类型映射
        /// </summary>
        private static void RegisterEventTypeMapping(Type eventType, string eventName) {
            if (!_eventTypeMap.ContainsKey(eventType)) {
                _eventTypeMap[eventType] = new List<string>();
            }
            if (!_eventTypeMap[eventType].Contains(eventName)) {
                _eventTypeMap[eventType].Add(eventName);
            }
        }

        /// <summary>
        /// 移除事件类型映射
        /// </summary>
        private static void UnregisterEventTypeMapping(Type eventType, string eventName) {
            if (_eventTypeMap.TryGetValue(eventType, out var eventNames)) {
                eventNames.Remove(eventName);
                if (eventNames.Count == 0) {
                    _eventTypeMap.Remove(eventType);
                }
            }
        }

        /// <summary>
        /// 清理空的类型映射
        /// </summary>
        private static void CleanEmptyTypeMaps() {
            var emptyTypes = _eventTypeMap.Where(kv => kv.Value.Count == 0).Select(kv => kv.Key).ToList();
            foreach (var type in emptyTypes) {
                _eventTypeMap.Remove(type);
            }
        }

        /// <summary>
        /// 获取指定事件的订阅者数量
        /// </summary>
        public static int GetSubscriberCount(string eventName) {
            lock (_lockObj) {
                if (_eventDict.TryGetValue(eventName, out var existingDelegate) && existingDelegate != null) {
                    return existingDelegate.GetInvocationList().Length;
                }
                return 0;
            }
        }

        /// <summary>
        /// 获取所有已注册的事件名列表
        /// </summary>
        public static List<string> GetAllEventNames() {
            lock (_lockObj) {
                return new List<string>(_eventDict.Keys);
            }
        }

        /// <summary>
        /// 获取事件总线中事件总数
        /// </summary>
        public static int GetTotalEventCount() {
            lock (_lockObj) {
                return _eventDict.Count;
            }
        }
    }

    /// <summary>
    /// UI事件名常量定义（统一管理，避免拼写错误）
    /// </summary>
    public static class UIEventNames
    {
        //public const string UserNameChanged = "UserNameChanged"; // 用户名修改事件（参数：string）
        public const string SettingSaved = "SettingSaved";       // 设置保存事件（参数：float 音量，bool 音乐开关）
        //public const string GameStart = "GameStart";             // 游戏开始事件（无参数）
        public const string NotifyShow = "NotifyShow";           // 通知显示事件（参数：string 内容，NotifyPosition 位置，float 时长）
        public const string SafeAreaUpdated = "SafeAreaUpdated"; // SafeArea更新事件（无参数）
 
        // 通用UI事件
        public const string UI_SHOWN = "UI_SHOWN";
        public const string UI_HIDDEN = "UI_HIDDEN";
        public const string UI_CLOSED = "UI_CLOSED";
        public const string UI_REFRESH = "UI_REFRESH";

        // 示例事件
        public const string BUTTON_CLICKED = "BUTTON_CLICKED";
        public const string MENU_SELECTED = "MENU_SELECTED";
        public const string DIALOG_CONFIRM = "DIALOG_CONFIRM";
        public const string DIALOG_CANCEL = "DIALOG_CANCEL";
    }

}
