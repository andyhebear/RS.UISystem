# UGUI 界面管理系统说明文档

## 一、系统概述
### 1. 核心定位
一款适用于 Unity 多场景项目的高性能基于UGUI的 UI 管理框架，专注于解决 UI 开发中的**多场景适配、动态资源加载、界面解耦、性能优化**等核心问题，提供完整的 UI 生命周期管理、内置常用组件，降低项目 UI 开发复杂度。

### 2. 核心特性
| 特性                | 描述                                                                 |
|---------------------|----------------------------------------------------------------------|
| 多场景无缝支持      | 单例管理器跨场景存活，场景切换时自动清理/保留 UI，支持场景专属 UI 配置 |
| 灵活资源加载        | 支持 Resources/Addressables/AssetBundle/RefPrefab 四种加载方式，适配本地/远程资源 |
| 全面适配方案        | 6 种多分辨率适配模式 + SafeArea 自动适配，支持黑边自定义与屏幕旋转适配 |
| 高性能设计          | 界面对象池复用、事件总线解耦、单 Canvas 全局渲染，减少 GC 与性能开销   |
| 内置常用界面        | 普通弹窗、输入框弹窗、多位置通知层，支持自定义样式与动画效果           |
| 完整生命周期管理    | 界面加载、显示、隐藏、回收、销毁全流程自动化，支持状态保存/恢复        |
| 易于扩展集成        | 模块化设计，支持自定义界面、加载器、适配规则，兼容现有 UGUI 项目       |

### 3. 技术依赖
- Unity 版本：2019+（兼容 2020/2021/2022 LTS 版本）
- UI 系统：UGUI（原生无第三方插件依赖）
- 可选依赖：Addressables/YooAsset 包（用于动态远程下载资源）
- .NET 框架：.NET Framework 4.x（支持 C# 7.3+ 语法），.NetStandard2.0 支持WebGL

## 二、架构设计
### 1. 整体架构图

### 2. 核心模块职责
| 模块名          | 核心职责                                                                 |
|-----------------|--------------------------------------------------------------------------|
| UIManager       | 单例核心，负责界面加载、显示、关闭、层级管理、场景切换适配、资源加载调度 |
| UIEventBus      | 全局事件总线，实现界面间/模块间解耦通信（支持无参/单参/双参事件）        |
| UIPool          | 界面对象池，按界面名称分类缓存，避免频繁实例化/销毁，降低 GC             |
| 适配模块        | 含 UIRootAdapter（多分辨率适配）和 UIBackgroundMask（黑边管理）          |
| UIForm          | 所有 UI 界面基类，定义生命周期方法、事件订阅/取消、动画接口规范           |
| UIConfig        | ScriptableObject 配置文件，管理界面配置、场景-UI 映射关系                |
| 资源加载器      | 统一资源加载接口，支持 Resources/Addressables/AssetBundle/RefPrefab 四种实现       |
| 内置界面        | 提供 UIPopup（普通弹窗）、UIInputPopup（输入框弹窗）、UINotifyLayer（通知层） |

## 三、系统特性

1. **层级管理**: 通过UILayerType枚举控制界面显示顺序，支持叠加显示
2. **界面栈**: 普通层界面自动入栈，关闭时返回上一个界面
3. **对象池复用**: 频繁创建的界面会被池化，减少GC和实例化开销
4. **异步加载**: 支持异步加载界面，避免卡顿
5. **数据传递**: 显示界面时可传递任意类型数据
6. **通用组件**: 内置通用弹窗和输入框，可直接使用
7. **自动初始化**: 自动创建UIRoot、UICamera、UILayers等
8. **扩展性强**: UIForm提供钩子方法，子类可重写动画、生命周期回调
9. **多种资源加载**: 支持Resources、AssetBundle、Addressables、YooAsset等加载方式
10. **动画系统**: 支持界面切换时的显示隐藏动画
11. **事件系统**: 全局UI事件总线，支持界面间通信
13. **多分辨率适配**: 支持多种适配策略，适应不同屏幕尺寸
14. **遮挡管理**: 添加遮罩层，防止弹窗背后的界面交互

## 四、注意事项

1. 所有UI界面应继承自UIForm类
2. UI资源路径需要与实际路径匹配
3. 使用异步加载时，注意处理加载失败的情况
4. 对于频繁显示隐藏的UI，建议使用对象池
5. 性能监控系统会增加一定开销，建议在开发阶段使用，发布版本可关闭

## 五、快速集成指南
### 1. 目录结构建议

### 2. 环境配置步骤
#### 2.1  UI 层级节点说明
1. 在场景中创建空对象，命名为 `UIManager`（作为所有 UI 的根节点）绑定UIManager。
2.  `UILayers` 组件说明：
   - `Canvas`：渲染模式设为 `ScreenSpaceCamera`，排序层设为 `UI`。
   - `CanvasScaler`：默认参考分辨率设为 `1920x1080`。
   - `GraphicRaycaster`：用于 UI 交互检测。
   - `UIRootAdapter`（框架脚本）：绑定适配相关配置。
3.  `UILayers` 的子节点（对应 UI 层级，按顺序排列）说明：
   - BackgroundLayer（背景层，用于背景图）
   - NormalLayer（普通层，用于主界面、功能界面）
   - PopUpLayer（弹窗层，用于确认弹窗、提示框）
   - GuideLayer（引导层，用于新手引导）
   - TopLayer（顶层，用于加载界面、公告）
   - NotifyLayer（通知层，用于滚动消息、Toast）
4. `UILayers` 下子节点`BackgroundLayer`是根据适配模式动态创建的，会自动添加 `UIBackgroundMask` 组件来实现管理黑边功能。

#### 2.2 创建 UIConfig 配置文件
1. 在 Project 窗口右键 → `UGUI/UIConfig`，创建配置文件（命名为 `UIConfig`）。
2. 配置 `FormConfigs`（每个 UI 界面的基础配置）：
   - `FormName`：界面名称（必须与脚本名一致）。
   - `Layer`：所属 UI 层级（对应 `UILayers` 下的子节点）。
   - `IsAddToStack`：是否加入层级栈（支持返回上一级界面）。
   - `IsPermanent`：是否永久存在（场景切换时不回收）。
   - `PrefabPath`：预制体路径（Resources 加载路径或 Addressables Key）。
   - `LoadType`：资源加载类型（Resources/Addressables/AssetBundle）。
   - `RefPrefab`：  关联资源对象,如果当前对象不为NULL则优先使用，因为已经存在实例了
   - `RefPrefabsFromScene`：  关联的资源对象来自当前场景，随场景销毁而销毁，如果已经加入到缓存池中后则不会随关联场景销毁


#### 2.3 配置 UIManager
1. 在场景中创建空对象，命名为 `UIManager`。
2. 为其添加 `UIManager` 组件，并赋值以下配置：
   - `UIConfig`：选择步骤 2.2 创建的 `UIConfig` 配置文件。
   - `UILayers`：选择步骤 2.1 创建的 `UILayers` 节点。
   - `UICamera`：指定 UI 专用相机（配置为正交相机，CullingMask 仅勾选 UI 层）。
   - `Default Load Type`：默认资源加载类型（建议开发阶段用 Resources）。
   - `Default Adapt Mode`：默认适配模式（建议核心界面用 BlackBars）。

### 3. 自定义 UI 开发步骤
#### 3.1 创建 UI 预制体
1. 在 `UI/Prefabs` 目录下创建 UI 预制体（如 `MainUI.prefab`）。
2. 为预制体添加自定义脚本（继承 `UIForm` 基类）。
3. 配置预制体的 `RectTransform`：
   - 锚点（Anchor Min/Max）设为 `(0,0)/(1,1)`，填充父节点。
   - Offset Min/Max 设为 `(0,0)/(0,0)`，避免 UI 偏移。

#### 3.2 编写自定义界面脚本
```csharp
using UnityEngine;
using UnityEngine.UI;
using UGUIManager;

public class MainUI : UIForm
{
    [SerializeField] private Button _startBtn;
    [SerializeField] private Text _userNameText;

    protected override void Awake()
    {
        base.Awake();
        // 绑定按钮事件
        _startBtn?.onClick.AddListener(OnStartBtnClick);
    }

    /// <summary>
    /// 事件订阅（必须重写，统一管理订阅逻辑）
    /// </summary>
    protected override void SubscribeEvents()
    {
        UIEventBus.Subscribe<string>(UIEventNames.UserNameChanged, OnUserNameChanged);
    }

    /// <summary>
    /// 取消所有事件订阅（必须重写，避免内存泄漏）
    /// </summary>
    protected override void UnsubscribeAllEvents()
    {
        UIEventBus.Unsubscribe<string>(UIEventNames.UserNameChanged, OnUserNameChanged);
    }

    /// <summary>
    /// 显示动画（可选重写，默认无动画）
    /// </summary>
    protected override IEnumerator ShowAnimationCoroutine(Action onComplete)
    {
        CanvasGroup canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0;

        float duration = 0.3f;
        float time = 0;
        while (time < duration)
        {
            time += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0, 1, time / duration);
            yield return null;
        }

        OnShowComplete();
        onComplete?.Invoke();
    }

    /// <summary>
    /// 开始按钮点击回调
    /// </summary>
    private void OnStartBtnClick()
    {
        // 触发游戏开始事件
        UIEventBus.Trigger(UIEventNames.GameStart);
        // 显示战斗界面
        UIManager.Instance.ShowForm<BattleUI>();
    }

    /// <summary>
    /// 接收用户名变更事件
    /// </summary>
    private void OnUserNameChanged(string userName)
    {
        _userNameText.text = $"当前用户：{userName}";
    }
}
