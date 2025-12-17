

#if ADDRESSABLES_SUPPORT
//#define ADDRESSABLES_ENABLED
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif
#if YOOASSET_SUPPORT
using YooAsset;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// 资源加载方式
    /// </summary>
    public enum UIResourceLoadType
    {
        Resources,       // Resources 同步加载
        AssetBundle,     // AssetBundle 异步加载
        Addressables,    // Addressables 异步加载
        YooAsset,        // YooAsset 异步加载
        PrefabRef,       //预设对象实例引用,特殊处理
    }

    /// <summary>
    /// UI资源加载接口（统一不同加载方式的调用）
    /// </summary>
    public interface IUIResourceLoader
    {
        /// <summary>
        /// 异步加载UI预制体
        /// </summary>
        /// <param name="path">资源路径/标签（根据加载方式不同含义不同）</param>
        /// <param name="bundleName">AssetBundle包名（仅AssetBundle方式有效）</param>
        /// <param name="onSuccess">加载成功回调（返回预制体）</param>
        /// <param name="onFailed">加载失败回调（返回错误信息）</param>
        void LoadUIPrefabAsync(string path,string bundleName,Action<GameObject> onSuccess,Action<string> onFailed);
    }
 
    /// <summary>
    /// Resources 加载器（内置实现）
    /// </summary>
    internal class ResourcesResourceLoader : IUIResourceLoader
    {
        public void LoadUIPrefabAsync(string path,string bundleName,Action<GameObject> onSuccess,Action<string> onFailed) {
            // Resources 同步加载（用协程模拟异步回调）
            StartCoroutine(LoadCoroutine(path,onSuccess,onFailed));
        }

        private IEnumerator LoadCoroutine(string path,Action<GameObject> onSuccess,Action<string> onFailed) {
            //yield return null; // 下一帧执行，模拟异步
            string pathNoExtension = System.IO.Path.GetFileNameWithoutExtension(path);
            //Resources.Load加载预设不带扩展符
            var prefabResult = Resources.LoadAsync<GameObject>(pathNoExtension);
            yield return prefabResult;
            var prefab = prefabResult.asset as GameObject;
            if (prefab != null) {
                onSuccess?.Invoke(prefab);
            }
            else {
                onFailed?.Invoke($"Resources加载失败，路径：{path}");
            }          
        }

        // 用 UIManager 实例启动协程
        private Coroutine StartCoroutine(IEnumerator enumerator) {
            return UIManager.Instance.StartCoroutine(enumerator);
        }
    }

    /// <summary>
    /// 本地AssetBundle 资源加载器,内置实现
    /// </summary>
    public class AssetBundleResourceLoader : IUIResourceLoader
    {
        // 已加载的AssetBundle缓存
        private readonly Dictionary<string,AssetBundle> _loadedBundles = new();
        // AssetBundle 加载路径前缀（根据项目配置调整）
        private readonly string _bundleLoadPath;
        /// <summary>
        /// 默认路径AssetBundles/
        /// </summary>
        /// <param name="bundleLoadPath"></param>
        public AssetBundleResourceLoader(string bundleLoadPath = "AssetBundles/") {
            _bundleLoadPath = bundleLoadPath;
        }
        /// <summary>
        /// 本地文件路径
        /// </summary>
        /// <param name="path"></param>
        /// <param name="bundleName"></param>
        /// <param name="onSuccess"></param>
        /// <param name="onFailed"></param>
        public void LoadUIPrefabAsync(string path,string bundleName,Action<GameObject> onSuccess,Action<string> onFailed) {
            if (string.IsNullOrEmpty(bundleName)) {
                onFailed?.Invoke("AssetBundle包名不能为空");
                return;
            }

            // 先加载AssetBundle
            LoadAssetBundleAsync(bundleName,(bundle) => {
                // 从Bundle中加载预制体
                var request = bundle.LoadAssetAsync<GameObject>(path);
                request.completed += (op) => {
                    var prefab = request.asset as GameObject;
                    if (prefab != null) {
                        onSuccess?.Invoke(prefab);
                    }
                    else {
                        onFailed?.Invoke($"从Bundle {bundleName} 加载预制体 {path} 失败");
                    }
                };
            },onFailed);
        }

        /// <summary>
        /// 异步加载AssetBundle
        /// </summary>
        private void LoadAssetBundleAsync(string bundleName,Action<AssetBundle> onSuccess,Action<string> onFailed) {
            if (_loadedBundles.TryGetValue(bundleName,out var bundle)) {
                onSuccess?.Invoke(bundle);
                return;
            }

            var fullPath = $"{_bundleLoadPath}{bundleName}";
            var request = AssetBundle.LoadFromFileAsync(fullPath);
            request.completed += (op) => {
                var loadedBundle = request.assetBundle;
                if (loadedBundle != null) {
                    _loadedBundles.Add(bundleName,loadedBundle);
                    onSuccess?.Invoke(loadedBundle);
                }
                else {
                    onFailed?.Invoke($"加载AssetBundle {bundleName} 失败，路径：{fullPath}");
                }
            };
        }

        /// <summary>
        /// 卸载指定AssetBundle
        /// </summary>
        public void UnloadBundle(string bundleName,bool unloadAllLoadedObjects = false) {
            if (_loadedBundles.TryGetValue(bundleName,out var bundle)) {
                bundle.Unload(unloadAllLoadedObjects);
                _loadedBundles.Remove(bundleName);
            }
        }

        /// <summary>
        /// 卸载所有AssetBundle
        /// </summary>
        public void UnloadAllBundles(bool unloadAllLoadedObjects = false) {
            foreach (var bundle in _loadedBundles.Values) {
                bundle.Unload(unloadAllLoadedObjects);
            }
            _loadedBundles.Clear();
        }
    }

#if ADDRESSABLES_SUPPORT

 /// <summary>
    /// Addressables 资源加载器（需导入 Addressables 包）
    /// </summary>
    public class AddressablesResourceLoader : IUIResourceLoader
    {
        public void LoadUIPrefabAsync(string path, string bundleName, Action<GameObject> onSuccess, Action<string> onFailed)
        {
            // Addressables 用 path 作为资源标签/路径
            var handle = Addressables.LoadAssetAsync<GameObject>(path);
            handle.Completed += (op) =>
            {
                switch (op.Status)
                {
                    case AsyncOperationStatus.Succeeded:
                        onSuccess?.Invoke(op.Result);
                        break;
                    case AsyncOperationStatus.Failed:
                        onFailed?.Invoke($"Addressables加载失败：{op.Exception.Message}");
                        break;
                }
                // 释放句柄（避免内存泄漏）
                Addressables.Release(handle);
            };
        }
    }
#endif
#if YOOASSET_SUPPORT
/// <summary>
    /// YooAsset 资源加载器（需导入 YooAsset 包）
    /// </summary>
    public class YooAssetResourceLoader : IUIResourceLoader
    {
        // YooAsset 资源包名称（根据项目配置调整）
        private readonly string _packageName;

        public YooAssetResourceLoader(string packageName = "DefaultPackage")
        {
            _packageName = packageName;
        }

        public void LoadUIPrefabAsync(string path, string bundleName, Action<GameObject> onSuccess, Action<string> onFailed)
        {
            var package = YooAssets.GetPackage(_packageName);
            if (package == null)
            {
                onFailed?.Invoke($"YooAsset包 {_packageName} 未找到");
                return;
            }

            // 加载资源
            var operation = package.LoadAssetAsync<GameObject>(path);
            operation.Completed += (op) =>
            {
                if (op.Success)
                {
                    onSuccess?.Invoke(op.AssetObject as GameObject);
                }
                else
                {
                    onFailed?.Invoke($"YooAsset加载失败：{op.Error}");
                }
            };
        }
    }

#endif

}
