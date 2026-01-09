using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if !UNITY_WEBGL
using System.Threading;
#endif
namespace RS.Unity3DLib.UISystem
{



    #region 核心协程工具类（WebGL 异步基础，统一管理主线程协程）
    /// <summary>
    /// 协程工具类（WebGL 兼容，统一管理主线程异步操作）
    /// </summary>
    public static class UICoroutineHelper
    {
        private static MonoBehaviour _host;
        private static bool _isInitialized;
        private static int _mainThreadId;

        /// <summary>
        /// 初始化协程宿主（需在主线程调用，建议在游戏启动时执行）
        /// </summary>
        internal static void Initialize(MonoBehaviour host,int mainThreadId=0) {
            if (host == null) throw new ArgumentNullException(nameof(host));
            _host = host;
            _mainThreadId = mainThreadId;
            _isInitialized = true;
        }

        /// <summary>
        /// 启动延时异步操作（WebGL 主线程执行）
        /// </summary>
        /// <param name="delaySeconds">延时秒数</param>
        /// <param name="onComplete">延时结束回调（主线程执行）</param>
        public static void RunDelayed(float delaySeconds,Action onComplete) {
            if (!_isInitialized) throw new InvalidOperationException("CoroutineHelper 未初始化！请先调用 Initialize");
            if (onComplete == null) throw new ArgumentNullException(nameof(onComplete));

            _host.StartCoroutine(DelayedCoroutine(delaySeconds,onComplete));
        }

        /// <summary>
        /// 启动异步操作（无延时，立即在协程中执行，确保主线程）
        /// </summary>
        public static void RunAsync(Action onExecute) {
            if (!_isInitialized) throw new InvalidOperationException("CoroutineHelper 未初始化！请先调用 Initialize");
            if (onExecute == null) throw new ArgumentNullException(nameof(onExecute));

            _host.StartCoroutine(AsyncCoroutine(onExecute));
        }

        /// <summary>
        /// 将回调调度到主线程执行（WebGL 安全）
        /// </summary>
        public static void DispatchToMainThread(Action action) {
            if (action == null) throw new ArgumentNullException(nameof(action));
#if UNITY_WEBGL
                action.Invoke();
#else
            // 已在主线程则直接执行，否则通过协程调度
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId) {
                action.Invoke();
            }
            else {
                RunAsync(action);
            }
#endif
        }

        private static IEnumerator DelayedCoroutine(float delay,Action onComplete) {
            yield return new WaitForSeconds(delay);
            try {
                onComplete.Invoke();
            }
            catch (Exception ex) {
                Debug.LogError($"延时回调执行失败：{ex.Message}");
            }
        }

        private static IEnumerator AsyncCoroutine(Action onExecute) {
            yield return null; // 下一帧执行，确保主线程
            try {
                onExecute.Invoke();
            }
            catch (Exception ex) {
                Debug.LogError($"异步回调执行失败：{ex.Message}");
            }
        }

        internal static void StartCoroutine(IEnumerator enumerator) {
            if (!_isInitialized) throw new InvalidOperationException("CoroutineHelper 未初始化！请先调用 Initialize");

            _host.StartCoroutine(enumerator);
        }
        // WebGL 安全的等待方法
        public static IEnumerator WaitUntil(Func<bool> predicate) {
            while (!predicate()) {
                yield return null;
            }
        }

        // WebGL 安全的等待方法，带超时
        public static IEnumerator WaitUntil(Func<bool> predicate,float timeoutSeconds) {
            float startTime = Time.realtimeSinceStartup;
            while (!predicate() && (Time.realtimeSinceStartup - startTime) < timeoutSeconds) {
                yield return null;
            }
        }
    }
#endregion

#region 非泛型 UITask（基类）
    /// <summary>
    /// 无返回值异步任务（支持 await/yield return，兼容 WebGL）
    /// </summary>
    public class UITask : IDisposable
    {
        /// <summary>任务状态：未完成</summary>
        public bool IsCompleted => _isCompleted;
        /// <summary>任务是否执行失败</summary>
        public bool IsFaulted => _exception != null;
        /// <summary>任务失败的异常信息</summary>
        public Exception Exception => _exception;
#if !UNITY_WEBGL
        // 线程安全相关：私有锁（防子类篡改）+ volatile 内存可见性
        private readonly object _lockObj = new object();
#endif
        private volatile bool _isCompleted;
        private volatile Exception _exception;
        // 回调列表（私有，避免外部修改）
        private List<Action> _continuations = new List<Action>();
        private bool _isDisposed;

#region 静态工厂方法（简化创建）
        /// <summary>
        /// 创建一个空的 SimpleTask（需手动调用 SetCompleted/SetException 结束）
        /// </summary>
        public static UITask Create() => new UITask();

        /// <summary>
        /// 创建一个延时完成的 SimpleTask（WebGL 兼容）
        /// </summary>
        /// <param name="delaySeconds">延时秒数</param>
        public static UITask Delay(float delaySeconds) {
            var task = new UITask();
            UICoroutineHelper.RunDelayed(delaySeconds,task.SetCompleted);
            return task;
        }

        /// <summary>
        /// 并行等待多个 SimpleTask 全部完成（非泛型版 WhenAll）
        /// </summary>
        public static UITask WhenAll(params UITask[] tasks) {
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));
            if (tasks.Length == 0) return CompletedTask();

            var completedTask = new UITask();
            int remaining = tasks.Length;

            foreach (var task in tasks) {
                if (task == null) throw new ArgumentException("任务数组中不能包含 null");

                task.GetAwaiter().OnCompleted(() => {
                    // 任一任务失败，直接标记整体失败
                    if (task.IsFaulted) {
                        completedTask.SetException(task.Exception);
                        return;
                    }

                    // 所有任务完成后，标记整体完成
                    if (Interlocked.Decrement(ref remaining) == 0) {
                        completedTask.SetCompleted();
                    }
                });
            }

            return completedTask;
        }

        /// <summary>
        /// 创建一个已完成的 SimpleTask
        /// </summary>
        public static UITask CompletedTask() {
            var task = new UITask();
            task.SetCompleted();
            return task;
        }

        /// <summary>
        /// 创建一个已失败的 SimpleTask
        /// </summary>
        public static UITask FailedTask(Exception ex) {
            var task = new UITask();
            task.SetException(ex);
            return task;
        }
#endregion

#region 任务结束方法（公开 API）
        /// <summary>
        /// 标记任务正常完成（无返回值）
        /// </summary>
        /// <exception cref="InvalidOperationException">任务已完成或已释放</exception>
        public void SetCompleted() {
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                ThrowIfDisposed();
                if (_isCompleted) throw new InvalidOperationException("任务已完成，无法重复标记结束");

                _isCompleted = true;
                ExecuteContinuations();
            }
        }

        /// <summary>
        /// 标记任务异常完成
        /// </summary>
        /// <param name="ex">异常信息（不能为 null）</param>
        /// <exception cref="ArgumentNullException">ex 为 null</exception>
        /// <exception cref="InvalidOperationException">任务已完成或已释放</exception>
        public void SetException(Exception ex) {
            if (ex == null) throw new ArgumentNullException(nameof(ex),"异常信息不能为 null");
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                ThrowIfDisposed();
                if (_isCompleted) throw new InvalidOperationException("任务已完成，无法重复标记异常");

                _exception = ex;
                _isCompleted = true;
                ExecuteContinuations();
            }
        }
#endregion

#region Await 支持（INotifyCompletion）
        /// <summary>
        /// 获取 await 所需的 Awaiter
        /// </summary>
        public UITaskAwaiter GetAwaiter() => new UITaskAwaiter(this);

        /// <summary>
        /// 注册 await 完成后的回调
        /// </summary>
        internal void OnCompleted(Action continuation) {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                ThrowIfDisposed();
                if (_isCompleted) {
                    // 已完成则立即调度到主线程执行（WebGL 安全）
                    UICoroutineHelper.DispatchToMainThread(continuation);
                    return;
                }
                _continuations.Add(continuation);
            }
        }
#endregion

#region Unity Yield Return 支持
        /// <summary>
        /// 获取协程等待指令（支持 yield return task）
        /// </summary>
        public CustomYieldInstruction WaitForCompletion() => new UITaskYieldInstruction(this);

        /// <summary>
        /// 隐式转换为 CustomYieldInstruction（直接 yield return task）
        /// </summary>
        public static implicit operator CustomYieldInstruction(UITask task) {
            return task?.WaitForCompletion() ?? throw new ArgumentNullException(nameof(task));
        }

        /// <summary>
        /// 协程等待指令实现
        /// </summary>
        private class UITaskYieldInstruction : CustomYieldInstruction
        {
            private readonly UITask _task;

            public UITaskYieldInstruction(UITask task) {
                _task = task ?? throw new ArgumentNullException(nameof(task));
            }

            public override bool keepWaiting => !_task.IsCompleted && !_task.IsFaulted;
        }
#endregion

#region 内部辅助方法
        /// <summary>
        /// 执行所有注册的回调（WebGL 主线程安全）
        /// </summary>
        private void ExecuteContinuations() {
            // 复制回调数组（避免执行中列表被修改，数组比 List 更高效）
            Action[] callbacks = _continuations.ToArray();
            _continuations.Clear(); // 清空列表，避免重复执行

            // 调度到主线程执行所有回调（WebGL 必须）
            foreach (var callback in callbacks) {
                UICoroutineHelper.DispatchToMainThread(() => {
                    try {
                        callback?.Invoke();
                    }
                    catch (Exception ex) {
                        Debug.LogError($"SimpleTask 回调执行失败：{ex.Message}\n{ex.StackTrace}");
                    }
                });
            }
        }

        /// <summary>
        /// 检查是否已释放，若已释放则抛出异常
        /// </summary>
        private void ThrowIfDisposed() {
            if (_isDisposed) throw new ObjectDisposedException(nameof(UITask),"任务已释放，无法执行操作");
        }
#endregion

#region IDisposable 实现（内存安全）
        /// <summary>
        /// 释放任务资源（清空回调列表，避免内存泄漏）
        /// </summary>
        public void Dispose() {
            lock (_lockObj) {
                _isDisposed = true;
                _continuations.Clear();
                _continuations = null;
                _exception = null;
            }
        }
#endregion
    }
#endregion

#region 泛型 UITask<T>（有返回值）
    /// <summary>
    /// 有返回值异步任务（支持 await/yield return，兼容 WebGL）
    /// </summary>
    /// <typeparam name="T">任务返回值类型</typeparam>
    public class UITask<T> : IDisposable
    {
        /// <summary>任务状态：未完成</summary>
        public bool IsCompleted => _isCompleted;
        /// <summary>任务是否执行失败</summary>
        public bool IsFaulted => _exception != null;
        /// <summary>任务失败的异常信息</summary>
        public Exception Exception => _exception;
        /// <summary>任务返回结果（仅任务完成且未失败时可获取）</summary>
        public T Result {
            get {
#if !UNITY_WEBGL
                lock (_lockObj)
#endif    
                    {
                    ThrowIfDisposed();
                    if (!_isCompleted) throw new InvalidOperationException("任务未完成，无法获取结果");
                    if (_exception != null) throw new AggregateException("任务执行失败",_exception);
                    return _result;
                }
            }
        }
#if !UNITY_WEBGL
        private readonly object _lockObj = new object();
#endif
        private volatile bool _isCompleted;
        private volatile Exception _exception;
        private T _result;
        private List<Action> _continuations = new List<Action>();
        private bool _isDisposed;

#region 静态工厂方法（简化创建）
        /// <summary>
        /// 创建一个空的 SimpleTask<T>（需手动调用 SetResult/SetException 结束）
        /// </summary>
        public static UITask<T> Create() => new UITask<T>();

        /// <summary>
        /// 创建一个延时返回结果的 SimpleTask<T>（WebGL 兼容）
        /// </summary>
        public static UITask<T> Delay(float delaySeconds,T result) {
            var task = new UITask<T>();
            UICoroutineHelper.RunDelayed(delaySeconds,() => task.SetResult(result));
            return task;
        }

        /// <summary>
        /// 并行等待多个 SimpleTask<T> 全部完成（泛型版 WhenAll）
        /// </summary>
        public static UITask<T[]> WhenAll(params UITask<T>[] tasks) {
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));
            if (tasks.Length == 0) {
                var emptyTask = new UITask<T[]>();
                emptyTask.SetResult(Array.Empty<T>()); // 直接设置 T[] 类型结果，类型匹配
                return emptyTask;
            }
            var completedTask = new UITask<T[]>();
            int remaining = tasks.Length;
            var results = new T[tasks.Length];

            for (int i = 0; i < tasks.Length; i++) {
                int index = i;
                var task = tasks[i];

                if (task == null) throw new ArgumentException("任务数组中不能包含 null");

                task.GetAwaiter().OnCompleted(() => {
                    if (task.IsFaulted) {
                        completedTask.SetException(task.Exception);
                        return;
                    }

                    results[index] = task.Result;
                    if (Interlocked.Decrement(ref remaining) == 0) {
                        completedTask.SetResult(results);
                    }
                });
            }

            return completedTask;
        }

        /// <summary>
        /// 创建一个已完成的 SimpleTask<T>
        /// </summary>
        public static UITask<T> CompletedTask(T result) {
            var task = new UITask<T>();
            task.SetResult(result);
            return task;
        }

        /// <summary>
        /// 创建一个已失败的 SimpleTask<T>
        /// </summary>
        public static UITask<T> FailedTask(Exception ex) {
            var task = new UITask<T>();
            task.SetException(ex);
            return task;
        }
#endregion

#region 任务结束方法（公开 API）
        /// <summary>
        /// 标记任务正常完成并设置返回结果
        /// </summary>
        /// <param name="value">任务返回结果</param>
        /// <exception cref="InvalidOperationException">任务已完成或已释放</exception>
        public void SetResult(T value) {
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                ThrowIfDisposed();
                if (_isCompleted) throw new InvalidOperationException("任务已完成，无法重复设置结果");

                _result = value;
                _isCompleted = true;
                ExecuteContinuations();
            }
        }

        /// <summary>
        /// 标记任务异常完成
        /// </summary>
        /// <param name="ex">异常信息（不能为 null）</param>
        /// <exception cref="ArgumentNullException">ex 为 null</exception>
        /// <exception cref="InvalidOperationException">任务已完成或已释放</exception>
        public void SetException(Exception ex) {
            if (ex == null) throw new ArgumentNullException(nameof(ex),"异常信息不能为 null");
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                ThrowIfDisposed();
                if (_isCompleted) throw new InvalidOperationException("任务已完成，无法重复标记异常");

                _exception = ex;
                _isCompleted = true;
                ExecuteContinuations();
            }
        }
#endregion

#region Await 支持（INotifyCompletion）
        /// <summary>
        /// 获取 await 所需的 Awaiter
        /// </summary>
        public UITaskAwaiter<T> GetAwaiter() => new UITaskAwaiter<T>(this);

        /// <summary>
        /// 注册 await 完成后的回调
        /// </summary>
        internal void OnCompleted(Action continuation) {
            if (continuation == null) throw new ArgumentNullException(nameof(continuation));
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                ThrowIfDisposed();
                if (_isCompleted) {
                    UICoroutineHelper.DispatchToMainThread(continuation);
                    return;
                }
                _continuations.Add(continuation);
            }
        }
#endregion

#region Unity Yield Return 支持
        /// <summary>
        /// 获取协程等待指令（支持 yield return task）
        /// </summary>
        public CustomYieldInstruction WaitForCompletion() => new UITaskTYieldInstruction(this);

        /// <summary>
        /// 隐式转换为 CustomYieldInstruction（直接 yield return task）
        /// </summary>
        public static implicit operator CustomYieldInstruction(UITask<T> task) {
            return task?.WaitForCompletion() ?? throw new ArgumentNullException(nameof(task));
        }

        /// <summary>
        /// 协程等待指令实现（复用逻辑，无冗余）
        /// </summary>
        private class UITaskTYieldInstruction : CustomYieldInstruction
        {
            private readonly UITask<T> _task;

            public UITaskTYieldInstruction(UITask<T> task) {
                _task = task ?? throw new ArgumentNullException(nameof(task));
            }

            public override bool keepWaiting => !_task.IsCompleted && !_task.IsFaulted;
        }
#endregion

#region 内部辅助方法（与非泛型版一致，避免冗余）
        private void ExecuteContinuations() {
            Action[] callbacks = _continuations.ToArray();
            _continuations.Clear();

            foreach (var callback in callbacks) {
                UICoroutineHelper.DispatchToMainThread(() => {
                    try {
                        callback?.Invoke();
                    }
                    catch (Exception ex) {
                        Debug.LogError($"SimpleTask<T> 回调执行失败：{ex.Message}\n{ex.StackTrace}");
                    }
                });
            }
        }

        private void ThrowIfDisposed() {
            if (_isDisposed) throw new ObjectDisposedException(nameof(UITask<T>),"任务已释放，无法执行操作");
        }
#endregion

#region IDisposable 实现（内存安全）
        /// <summary>
        /// 释放任务资源（清空回调列表，避免内存泄漏）
        /// </summary>
        public void Dispose() {
#if !UNITY_WEBGL
            lock (_lockObj)
#endif
                {
                _isDisposed = true;
                _continuations?.Clear();
                _continuations = null;
                _exception = null;
                _result = default; // 释放值类型引用（引用类型置空）
            }
        }
#endregion
    }
#endregion

#region Awaiter 实现（适配 C# await 语法）
    /// <summary>
    /// 非泛型 SimpleTask 的 Awaiter（适配 await 语法）
    /// </summary>
    public struct UITaskAwaiter : INotifyCompletion
    {
        private readonly UITask _task;

        /// <summary>
        /// 创建 Awaiter（内部使用，无需手动创建）
        /// </summary>
        public UITaskAwaiter(UITask task) {
            _task = task ?? throw new ArgumentNullException(nameof(task));
        }

        /// <summary>
        /// 任务是否已完成
        /// </summary>
        public bool IsCompleted => _task.IsCompleted;

        /// <summary>
        /// 获取任务结果（无返回值，若任务失败则抛出异常）
        /// </summary>
        public void GetResult() {
            if (_task.IsFaulted) {
                throw _task.Exception ?? new InvalidOperationException("任务执行失败，但未指定异常信息");
            }
        }

        /// <summary>
        /// 注册任务完成后的回调（内部使用，await 语法自动调用）
        /// </summary>
        public void OnCompleted(Action continuation) => _task.OnCompleted(continuation);
    }

    /// <summary>
    /// 泛型 SimpleTask<T> 的 Awaiter（适配 await 语法）
    /// </summary>
    /// <typeparam name="T">返回值类型</typeparam>
    public struct UITaskAwaiter<T> : INotifyCompletion
    {
        private readonly UITask<T> _task;

        /// <summary>
        /// 创建 Awaiter（内部使用，无需手动创建）
        /// </summary>
        public UITaskAwaiter(UITask<T> task) {
            _task = task ?? throw new ArgumentNullException(nameof(task));
        }

        /// <summary>
        /// 任务是否已完成
        /// </summary>
        public bool IsCompleted => _task.IsCompleted;

        /// <summary>
        /// 获取任务结果（若任务失败则抛出异常）
        /// </summary>
        public T GetResult() => _task.Result;

        /// <summary>
        /// 注册任务完成后的回调（内部使用，await 语法自动调用）
        /// </summary>
        public void OnCompleted(Action continuation) => _task.OnCompleted(continuation);
    }
#endregion

#region 扩展方法（提升易用性）
    /// <summary>
    /// SimpleTask 扩展方法类
    /// </summary>
    public static class UITaskExtensions
    {
#if !UNITY_WEBGL
        // 这些方法在 WebGL 中不可用，因为它们使用了多线程
        /// <summary>
        /// 将 Task 转换为 SimpleTask（兼容现有 Task 代码）
        /// </summary>
        public static UITask ToSimpleTask(this System.Threading.Tasks.Task task) {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var simpleTask = UITask.Create();
            task.ContinueWith(t => {
                if (t.IsFaulted) {
                    simpleTask.SetException(t.Exception == null ? new InvalidOperationException("Task 执行失败") : t.Exception);
                }
                else {
                    simpleTask.SetCompleted();
                }
            },System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            return simpleTask;
        }

        /// <summary>
        /// 将 Task<T> 转换为 SimpleTask<T>（兼容现有 Task 代码）
        /// </summary>
        public static UITask<T> ToSimpleTask<T>(this System.Threading.Tasks.Task<T> task) {
            if (task == null) throw new ArgumentNullException(nameof(task));

            var simpleTask = UITask<T>.Create();
            task.ContinueWith(t => {
                if (t.IsFaulted) {
                    simpleTask.SetException(t.Exception == null ? new InvalidOperationException("Task 执行失败") : t.Exception);
                }
                else {
                    simpleTask.SetResult(t.Result);
                }
            },System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());
            return simpleTask;
        }

        // 这些方法在 WebGL 中不可用，因为它们使用了多线程
        // 使用条件编译确保它们不会在 WebGL 构建中编译

        // 将 SimpleTask 转换为 System.Threading.Tasks.Task
        public static async System.Threading.Tasks.Task AsTask(this UITask simpleTask) {
            if (simpleTask == null)
                throw new ArgumentNullException(nameof(simpleTask));

            await System.Threading.Tasks.Task.Run(() => {
                while (!simpleTask.IsCompleted) {
                    System.Threading.Thread.Sleep(10);
                }

                if (simpleTask.IsFaulted)
                    throw simpleTask.Exception;
            });
        }

        // 将 SimpleTask<T> 转换为 System.Threading.Tasks.Task<T>
        public static async System.Threading.Tasks.Task<T> AsTask<T>(this UITask<T> simpleTask) {
            if (simpleTask == null)
                throw new ArgumentNullException(nameof(simpleTask));

            return await System.Threading.Tasks.Task.Run(() => {
                while (!simpleTask.IsCompleted) {
                    System.Threading.Thread.Sleep(10);
                }

                return simpleTask.Result;
            });
        }
#else
        //// WebGL 替代方案 - 使用协程而不是多线程
        //public static SimpleTask AsSimpleTask(this System.Threading.Tasks.Task task) {
        //    var simpleTask = new SimpleTask();
        //    CoroutineHelper.StartCoroutine(TaskToSimpleTaskCoroutine(task,simpleTask));
        //    return simpleTask;
        //}

        //public static SimpleTask<T> AsSimpleTask<T>(this System.Threading.Tasks.Task<T> task) {
        //    var simpleTask = new SimpleTask<T>();
        //    CoroutineHelper.StartCoroutine(TaskToSimpleTaskCoroutine(task,simpleTask));
        //    return simpleTask;
        //}
        //private static IEnumerator TaskToSimpleTaskCoroutine(System.Threading.Tasks.Task task,SimpleTask simpleTask) {
        //    while (!task.IsCompleted) {
        //        yield return null;
        //    }

        //    if (task.IsFaulted)
        //        simpleTask.SetException(task.Exception);
        //    else
        //        simpleTask.SetCompleted();
        //}

        //private static IEnumerator TaskToSimpleTaskCoroutine<T>(System.Threading.Tasks.Task<T> task,SimpleTask<T> simpleTask) {
        //    while (!task.IsCompleted) {
        //        yield return null;
        //    }

        //    if (task.IsFaulted)
        //        simpleTask.SetException(task.Exception);
        //    else
        //        simpleTask.SetResult(task.Result);
        //}
#endif
        // 配置等待（在主线程继续）- WebGL 总是主线程
        public static UITask ConfigureAwait(this UITask task,bool continueOnCapturedContext = true) {
            // WebGL 中只有主线程，所以总是继续在主线程
            return task;
        }

        public static UITask<T> ConfigureAwait<T>(this UITask<T> task,bool continueOnCapturedContext = true) {
            // WebGL 中只有主线程，所以总是继续在主线程
            return task;
        }
    }
#endregion
    //V2改善
    //// 非泛型版本（基类）
    //public class SimpleTask
    //{
    //    // 线程安全相关：volatile保证内存可见性，lockObj保证原子操作
    //    protected volatile bool isCompleted;
    //    protected volatile Exception exception;
    //    protected readonly object lockObj = new object();
    //    // 改为List<Action>支持多回调
    //    protected List<Action> continuations = new List<Action>();

    //    public bool IsCompleted => isCompleted;
    //    public bool IsFaulted => exception != null;
    //    public Exception Exception => exception;

    //    // 适配await语法
    //    public SimpleTaskAwaiter GetAwaiter() => new SimpleTaskAwaiter(this);

    //    // 完成任务（内部调用，同一程序集可见）
    //    public  void SetCompleted() {
    //        lock (lockObj) {
    //            if (isCompleted) return; // 防止重复完成
    //            isCompleted = true;
    //            // 复制回调列表并执行（避免执行中回调被修改）
    //            var callbacks = new List<Action>(continuations);
    //            continuations.Clear();
    //            // 执行所有回调
    //            foreach (var callback in callbacks) {
    //                callback?.Invoke();
    //            }
    //        }
    //    }

    //    // 设置异常（内部调用）
    //    public virtual void SetException(Exception ex) {
    //        if (ex == null) throw new ArgumentNullException(nameof(ex));

    //        lock (lockObj) {
    //            if (isCompleted) return; // 防止重复完成
    //            exception = ex;
    //            isCompleted = true;
    //            // 复制回调列表并执行
    //            var callbacks = new List<Action>(continuations);
    //            continuations.Clear();
    //            foreach (var callback in callbacks) {
    //                callback?.Invoke();
    //            }
    //        }
    //    }

    //    // 注册回调（内部调用）
    //    internal virtual void OnCompleted(Action cont) {
    //        if (cont == null) throw new ArgumentNullException(nameof(cont));

    //        lock (lockObj) {
    //            if (isCompleted) {
    //                cont.Invoke(); // 已完成则立即执行回调
    //                return;
    //            }
    //            continuations.Add(cont); // 添加到回调列表（支持多回调）
    //        }
    //    }

    //    // Unity yield return 支持：直接yield return task即可
    //    public virtual CustomYieldInstruction WaitForCompletion() {
    //        return new SimpleTaskYieldInstruction(this);
    //    }

    //    // 隐式转换，简化yield return用法
    //    public static implicit operator CustomYieldInstruction(SimpleTask task) {
    //        return task.WaitForCompletion();
    //    }

    //    // 协程等待指令（内部类）
    //    private class SimpleTaskYieldInstruction : CustomYieldInstruction
    //    {
    //        private readonly SimpleTask _task;

    //        public SimpleTaskYieldInstruction(SimpleTask task) {
    //            _task = task ?? throw new ArgumentNullException(nameof(task));
    //        }

    //        // Unity协程会等待到keepWaiting为false
    //        public override bool keepWaiting => !_task.IsCompleted;
    //    }
    //}

    //// 泛型版本（继承自非泛型）
    //public class SimpleTask<T> : SimpleTask
    //{
    //    private  T result; // 

    //    public T Result {
    //        get {
    //            if (IsFaulted)
    //                throw Exception ?? new InvalidOperationException("任务执行失败，但未指定异常");
    //            if (!IsCompleted)
    //                throw new InvalidOperationException("任务未完成，无法获取结果");
    //            return result;
    //        }
    //    }

    //    // 重写GetAwaiter，返回泛型Awaiter（new关键字正确使用）
    //    public new SimpleTaskAwaiter<T> GetAwaiter() => new SimpleTaskAwaiter<T>(this);

    //    // 公开SetResult（外部可调用，需注意避免重复调用）
    //    public  void SetResult(T value) {
    //        lock (lockObj) {
    //            if (isCompleted) return; // 防止重复设置结果
    //            result = value;
    //            SetCompleted(); // 调用基类的完成逻辑
    //        }
    //    }

    //    // 公开SetException（去掉多余的new关键字）
    //    public override void SetException(Exception ex) {
    //        base.SetException(ex); // 直接调用基类的异常逻辑
    //    }

    //    // 重写WaitForCompletion，返回泛型等待指令
    //    public override CustomYieldInstruction WaitForCompletion() {
    //        return new SimpleTaskYieldInstruction(this);
    //    }

    //    // 隐式转换（泛型→CustomYieldInstruction）
    //    public static implicit operator CustomYieldInstruction(SimpleTask<T> task) {
    //        return task.WaitForCompletion();
    //    }

    //    // 泛型协程等待指令（内部类，复用外部类的T）
    //    private class SimpleTaskYieldInstruction : CustomYieldInstruction
    //    {
    //        private readonly SimpleTask<T> _task;

    //        public SimpleTaskYieldInstruction(SimpleTask<T> task) {
    //            _task = task ?? throw new ArgumentNullException(nameof(task));
    //        }

    //        public override bool keepWaiting => !_task.IsCompleted;
    //    }
    //}

    //// 非泛型Awaiter（适配await语法）
    //public struct SimpleTaskAwaiter : INotifyCompletion
    //{
    //    private readonly SimpleTask _task;

    //    public SimpleTaskAwaiter(SimpleTask task) {
    //        _task = task ?? throw new ArgumentNullException(nameof(task));
    //    }

    //    public bool IsCompleted => _task.IsCompleted;

    //    // 等待完成后调用，若有异常则抛出
    //    public void GetResult() {
    //        if (_task.IsFaulted)
    //            throw _task.Exception ?? new InvalidOperationException("任务执行失败");
    //    }

    //    // 注册回调（交给任务处理）
    //    public void OnCompleted(Action continuation) => _task.OnCompleted(continuation);
    //}

    //// 泛型Awaiter（适配await语法）
    //public struct SimpleTaskAwaiter<T> : INotifyCompletion
    //{
    //    private readonly SimpleTask<T> _task;

    //    public SimpleTaskAwaiter(SimpleTask<T> task) {
    //        _task = task ?? throw new ArgumentNullException(nameof(task));
    //    }

    //    public bool IsCompleted => _task.IsCompleted;

    //    // 等待完成后获取结果，异常会在Result中抛出
    //    public T GetResult() => _task.Result;

    //    // 注册回调（交给任务处理）
    //    public void OnCompleted(Action continuation) => _task.OnCompleted(continuation);
    //}




    //V1改善
    //// 非泛型版本
    //public class SimpleTask
    //{
    //    protected bool isCompleted;
    //    protected Exception exception;
    //    protected Action continuation;

    //    public bool IsCompleted => isCompleted;
    //    public bool IsFaulted => exception != null;
    //    public Exception Exception => exception;

    //    public  SimpleTaskAwaiter GetAwaiter() => new SimpleTaskAwaiter(this);

    //    internal void SetCompleted() {
    //        isCompleted = true;
    //        continuation?.Invoke();
    //    }

    //    internal void SetException(Exception ex) {
    //        exception = ex;
    //        isCompleted = true;
    //        continuation?.Invoke();
    //    }

    //    internal void OnCompleted(Action cont) {
    //        continuation = cont;
    //    }

    //    // Unity yield return 支持
    //    public CustomYieldInstruction WaitForCompletion() {
    //        return new SimpleTaskYieldInstruction(this);
    //    }

    //    public static implicit operator CustomYieldInstruction(SimpleTask task) {
    //        return task.WaitForCompletion();
    //    }

    //    // 内部类，实现 CustomYieldInstruction
    //    private class SimpleTaskYieldInstruction : CustomYieldInstruction
    //    {
    //        private SimpleTask task;

    //        public SimpleTaskYieldInstruction(SimpleTask task) {
    //            this.task = task;
    //        }

    //        public override bool keepWaiting => !task.IsCompleted;
    //    }
    //}




    //// 泛型版本，继承自非泛型版本
    //public class SimpleTask<T> : SimpleTask
    //{
    //    T result;

    //    public T Result {
    //        get {
    //            if (exception != null)
    //                throw exception;
    //            return result;
    //        }
    //    }

    //    public new SimpleTaskAwaiter<T> GetAwaiter() => new SimpleTaskAwaiter<T>(this);

    //    public void SetResult(T value) {
    //        result = value;
    //        SetCompleted();
    //    }

    //    // 隐藏基类的 SetException，使用泛型版本自己的逻辑
    //    public new void SetException(Exception ex) {
    //        base.SetException(ex);
    //    }

    //    // Unity yield return 支持
    //    public new CustomYieldInstruction WaitForCompletion() {
    //        return new SimpleTaskYieldInstruction<T>(this);
    //    }

    //    public static implicit operator CustomYieldInstruction(SimpleTask<T> task) {
    //        return task.WaitForCompletion();
    //    }

    //    // 内部类，实现 CustomYieldInstruction
    //    private class SimpleTaskYieldInstruction<TResult> : CustomYieldInstruction
    //    {
    //        private SimpleTask<TResult> task;

    //        public SimpleTaskYieldInstruction(SimpleTask<TResult> task) {
    //            this.task = task;
    //        }

    //        public override bool keepWaiting => !task.IsCompleted;
    //    }
    //}

    //// 非泛型的 Awaiter
    //public struct SimpleTaskAwaiter : INotifyCompletion
    //{
    //    SimpleTask task;
    //    public SimpleTaskAwaiter(SimpleTask task) => this.task = task;
    //    public bool IsCompleted => task.IsCompleted;

    //    public void GetResult() {
    //        if (task.Exception != null)
    //            throw task.Exception;
    //    }

    //    public void OnCompleted(Action continuation) => task.OnCompleted(continuation);
    //}

    //// 泛型的 Awaiter
    //public struct SimpleTaskAwaiter<T> : INotifyCompletion
    //{
    //    SimpleTask<T> task;
    //    public SimpleTaskAwaiter(SimpleTask<T> task) => this.task = task;
    //    public bool IsCompleted => task.IsCompleted;
    //    public T GetResult() => task.Result;
    //    public void OnCompleted(Action continuation) => task.OnCompleted(continuation);
    //}










    //初始设计
    //public class SimpleTask<T>
    //{
    //	T result;
    //	bool isCompleted;
    //	Action continuation;

    //	public bool IsCompleted => isCompleted;
    //	public T Result => result;

    //	public SimpleTaskAwaiter<T> GetAwaiter() => new SimpleTaskAwaiter<T>(this);

    //	internal void SetResult(T value) {
    //		result = value;
    //		isCompleted = true;
    //		continuation?.Invoke();
    //	}

    //	internal void OnCompleted(Action cont) {
    //		continuation = cont;
    //	}
    //}

    //public struct SimpleTaskAwaiter<T> : INotifyCompletion
    //{
    //	SimpleTask<T> task;
    //	public SimpleTaskAwaiter(SimpleTask<T> task) => this.task = task;
    //	public bool IsCompleted => task.IsCompleted;
    //	public T GetResult() => task.Result;
    //	public void OnCompleted(Action continuation) => task.OnCompleted(continuation);
    //}
}
