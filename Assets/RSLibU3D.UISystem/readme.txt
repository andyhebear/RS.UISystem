设计一套基于UGUI的界面管理系统
1:核心模块
UIManager（核心管理器）：单例模式，负责界面注册、加载、显示 / 隐藏、栈管理。
UIForm（界面基类）：所有 UGUI 界面的父类，封装通用逻辑（生命周期、显示隐藏动画、数据传递）,区分界面栈自动显示/隐藏还是手动显示/隐藏。
UILayer（层级定义）：枚举定义界面层级（如底层、普通层,弹窗层、导航层、顶层、消息通知层）。
UIConfig（配置类）：存储界面路径、层级、是否常驻等配置。
UIPool（对象池）：复用频繁创建销毁的界面（如弹窗），减少 GC。
    public enum UILayerType 
    {
        BackgroundLayer,//用于显示UI背景，如背景黑边图等等
        NormalLayer,//：普通层（主界面、功能界面）
        PopUpLayer,/// 弹窗层（确认弹窗、提示弹窗,输入框窗口）       
        GuideLayer,//导航层(比如初始界面操作引导)        
        TopLayer,      // 顶层（加载界面、公告界面）
        NotifyLayer,// 最顶层消息通知层（比如显示动态滚动消息，左上/左下/中心/右上，右下）
    }
  UI框架gameobject布局结构
    |-UIRoot(绑定UIManager)
    |--UICamera
    |--UILayers(绑定Canvas)
    |---BackgroundLayer
    |---NormalLayer
    |--...
     
2:系统特性
层级管理：通过 UILayer 枚举控制界面显示顺序，支持叠加显示（如弹窗在普通界面上方）。
界面栈：普通层界面自动入栈(各个UILayerType界面栈独立)，关闭时返回上一个界面（类似手机 App 的返回逻辑）。
对象池复用：频繁创建的界面（如弹窗）会被池化，减少 GC 和实例化开销。
异步加载：支持异步加载界面，避免卡顿，同时支持显示 / 隐藏动画。
数据传递：显示界面时可传递任意类型数据，满足复杂业务需求。
通用弹窗：内置通用弹窗，支持标题、内容、确认 / 取消回调，可直接使用。
通用输入文本窗，支持输入整数，浮点数还是字符串，确认 / 取消回调，可直接使用。
自动初始化：自动创建 UIRoot、UIRoot下UICamera,UILayers(子节点创建对应的UILayer)以及判断是否存在全局EventSystem(不存在就创建)，简化配置。
扩展性强：界面基类 UIForm 提供钩子方法，子类可重写动画、生命周期回调。
资源加载：支持 Resources.Load加载以及 AssetBundle, Addressables,yooasset 加载（适合大型项目）。
动画系统：支持界面切换时展现显示隐藏动画。
事件系统：添加全局 UI 事件总线，支持界面间通信（如 UIEventManager.On("UserLogin", OnUserLogin)）。
性能监控：添加界面加载时间、内存占用统计，便于优化。
多分辨率适配：配置适配策略（如固定宽度/固定高度/固定纵横比黑边/SafeArea）。
遮挡管理：添加遮罩层，防止弹窗背后的界面交互（可在 UIForm 中添加遮罩逻辑）。
不使用 Task、async/await，用协程与 Action 回调 处理异步完成逻辑。
用 Unity 协程（Coroutine）实现显示 / 隐藏动画。
统一回调风格，所有异步操作通过回调通知结果。
多分辨率适配：UIRoot的子节点UILayers配置了CanvasScaler(renderMode=screenSpaceCamera,renderCamera=UICamera)，支持不同屏幕尺寸(clear flags: Depthonly,culling mask:UI,projection:orthographic)
UIRoot的子节点UICamera的clearflags=SolidColor,backgoundcolor=color.clear,cullingmask=UI,projection=orthographic,
UILayers的子节点是各种UILayerType对应的gameobject

