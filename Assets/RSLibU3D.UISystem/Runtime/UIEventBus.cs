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

        #region 无参数事件
        public static void Subscribe(string eventName,Action callback) {
            if (string.IsNullOrEmpty(eventName) || callback == null) return;

            lock (_lockObj) {
                if (!_eventDict.ContainsKey(eventName)) {
                    _eventDict.Add(eventName,null);
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
    }


}
