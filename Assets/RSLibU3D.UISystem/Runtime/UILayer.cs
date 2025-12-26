using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace RS.Unity3DLib.UISystem
{

    /// <summary>
    /// UI层级枚举（对应UILayers下的子节点）
    /// 层级顺序：BackgroundLayer < WindowLayer< PanelLayer< PopUpLayer < GuideLayer < TopLayer < NotifyLayer
    /// </summary>
    public enum UILayer
    {
        BackgroundLayer, // 背景层（背景图）
        WindowLayer,     // 普通层（主界面、功能界面）
        PanelLayer,      // 界面面板层（属性面板）
        PopUpLayer,      // 弹窗层（确认弹窗、输入框弹窗）
        GuideLayer,      // 引导层（新手引导、操作提示）
        TopLayer,        // 顶层（加载界面、公告界面）
        NotifyLayer      // 通知层（滚动消息、 Toast 提示）
    }
    /// <summary>
    /// 适配模式（6种核心模式）
    /// </summary>
    public enum AdaptMode
    {
        MatchWidth,          // 匹配宽度
        MatchHeight,         // 匹配高度
        MatchWidthOrHeight,  // 匹配宽度或高度
        Expand,              // 扩展模式
        Shrink,              // 缩小模式
        BlackBars            // 黑边模式
    }
    /// <summary>
    /// 通知显示位置（用于NotifyLayer）
    /// </summary>
    public enum NotifyPosition
    {
        TopLeft,    // 左上
        TopRight,   // 右上
        BottomLeft, // 左下
        BottomRight,// 右下
        Center,      // 中心
        TopCenter, //上中
        BottomCenter,//下中
        LeftCenter,//左中
        RightCenter,//右中
    }
    /// <summary>
    /// 输入框类型（用于UIInputPopup）
    /// </summary>
    public enum InputFieldType
    {
        Normal,  // 普通文本
        Password // 密码（隐藏输入）
    }
    /// <summary>
    /// 输入类型
    /// </summary>
    public enum InputType
    {
        String,  // 字符串
        Integer, // 整数
        Float    // 浮点数
    }
    /// <summary>
    /// 通用弹窗按钮类型
    /// </summary>
    public enum DialogButtonType
    {
        OK,           // 只有确定按钮
        OKCancel,     // 确定和取消按钮
        YesNo,        // 是和否按钮
        YesNoCancel   // 是、否和取消按钮
    }
    /// <summary>
    /// 通用弹窗按钮结果
    /// </summary>
    public enum DialogButtonResult
    {
        OK,      // 确定
        Cancel,  // 取消
        Yes,     // 是
        No       // 否
    }
    /*
    | 显示类型	|核心逻辑（仅当前层级）	             |适用场景	                       |与其他类型的区别
    |-----------|------------------------------------|---------------------------------|--------------
    |Normal	    |入栈 → 关闭时出栈并显示上一个界面	 |普通功能页（如列表→详情）	   |需记录历史，支持返回上一页
    |Single	    |不入栈 → 无栈操作	                 |独立临时页（如提示弹窗、帮助页） |不占用栈空间，关闭后无界面恢复
    |Reverse	|入栈 → 关闭时仅出栈不显示上一页	 |临时插入页（如登录验证、隐私协议）|需记录历史（防止回退到前序），但关闭后直接返回更上层
    |HideOther	|隐藏当前层级其他界面 → 入栈 → 关闭时出栈显示上一个	|模态独占页（如表单填写、确认弹窗）	|独占当前层级（同层级仅自己显示），支持返回上一页，非跨层隐藏
     */
    ///// <summary>
    ///// UI显示类型,设计只影响当前层级，不跨层影响,太难维护逻辑，不使用UIShowType
    ///// </summary>
    //public enum UIShowType
    //{
    //    /// <summary>
    //    /// 入栈，关闭时出栈并显示上一个界面（同层级）
    //    /// 适用：普通界面切换，需要返回上一页（如列表→详情）
    //    /// 隐藏当前层级"正在显示"的界面（通常1个），入栈后显示新界面 → 适用于普通流程切换（如列表→详情）
    //    /// </summary>
    //    Normal,
    //    /// <summary>
    //    /// 不入栈，无栈操作（同层级）
    //    /// 适用：独立界面，不需要记录在历史中（如提示弹窗、帮助页）
    //    /// </summary>
    //    Single,
    //    /// <summary>
    //    /// 入栈，关闭时只出栈不显示上一页（同层级）
    //    /// 适用：临时界面，关闭后不返回上一页（如登录验证、隐私协议）
    //    /// </summary>
    //    Reverse,
    //    /// <summary>
    //    /// 隐藏当前层级其他界面 → 入栈，关闭时出栈并显示上一个界面（同层级）
    //    /// 适用：模态独占界面，需当前层级仅自己显示（如表单填写、重要确认弹窗）
    //    /// 隐藏当前层级"所有显示中"的界面（可能多个），入栈后显示新界面 → 适用于模态独占场景（如表单填写、支付确认）
    //    /// </summary>
    //    HideOther
    //}
    /// <summary>
    /// UI状态
    /// </summary>
    public enum UIFormState
    {
        Uninitialized, // 未初始化（刚创建未调用Init）
        Loading,       // 加载中（异步加载预制体过程）
        Loaded,        // 已加载（Init完成但未显示）
        Showing,       // 显示中（执行显示动画过程）
        Shown,         // 已显示（显示动画完成）
        Hiding,        // 隐藏中（执行隐藏动画过程）
        Destroyed      // 已销毁
    }
    /// <summary>
    /// 遮罩类型
    /// </summary>
    public enum UIMaskType
    {
        None,               // 无遮罩
        /// <summary>
        /// 半透明遮罩
        /// </summary>
        Translucent,        // 半透明遮罩（默认黑色50%透明）
        /// <summary>
        /// 全透明遮罩
        /// </summary>
        Transparent,         // 全透明遮罩（完全透明但阻挡交互）
        Blurred             //半透明背景虚化(模糊)遮罩
    }

    public static class UGUIEx
    {
        public static void SetText(this Text text,string content) {
            if (text != null) {
                text.text = content;
            }
        }
    }
    /// <summary>
    /// 适配模式说明工具类
    /// </summary>
    public static class AdaptModeDescription
    {
        public static string GetDescription(AdaptMode mode) {
            switch (mode) {
                case AdaptMode.MatchWidth:
                    return "匹配宽度：UI宽度固定为设计分辨率宽度，高度按屏幕比例自适应。" +
                           "左右无黑边，高度超出屏幕部分会被裁剪，适合横向滚动UI。";
                case AdaptMode.MatchHeight:
                    return "匹配高度：UI高度固定为设计分辨率高度，宽度按屏幕比例自适应。" +
                           "上下无黑边，宽度超出屏幕部分会被裁剪，适合纵向滚动UI。";
                case AdaptMode.MatchWidthOrHeight:
                    return "匹配宽度或高度：自动根据屏幕纵横比选择适配方向（宽屏匹配高度，高屏匹配宽度）。" +
                           "优先填满屏幕无黑边，可能存在部分UI裁剪，适合对比例不敏感的界面。";
                case AdaptMode.Expand:
                    return "扩展模式：拉伸UI至完全填满屏幕，不保持设计纵横比。" +
                           "无黑边无裁剪，但UI可能被拉伸变形，仅适合纯背景、广告等场景。";
                case AdaptMode.Shrink:
                    return "缩小模式：UI保持设计纵横比，完全显示在屏幕内，不裁剪。" +
                           "UI无变形无裁剪，但可能存在黑边，适合对UI完整性要求高的场景。";
                case AdaptMode.BlackBars:
                    return "黑边模式：保持设计纵横比，UI无拉伸无裁剪，超出屏幕部分显示黑边。" +
                           "UI比例完美，黑边可自定义颜色，适合核心界面、弹窗等场景。";
                default:
                    return "未知适配模式";
            }
        }

        public static string GetRecommendScene(AdaptMode mode) {
            switch (mode) {
                case AdaptMode.MatchWidth: return "横向滚动列表、主界面、水平布局功能界面";
                case AdaptMode.MatchHeight: return "纵向滚动列表、背包、聊天界面、垂直布局功能界面";
                case AdaptMode.MatchWidthOrHeight: return "工具类APP、小游戏、对比例不敏感的辅助界面";
                case AdaptMode.Expand: return "全屏广告、纯背景图、无交互展示界面";
                case AdaptMode.Shrink: return "地图、图表、数据展示界面、复杂UI";
                case AdaptMode.BlackBars: return "游戏主界面、弹窗、登录注册界面、核心视觉界面";
                default: return "无推荐场景";
            }
        }
    }
}
