using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


namespace RS.Unity3DLib.UISystem
{
    /// <summary>
    /// UI层级枚举（对应UILayers下的子节点）
    /// 层级顺序：BackgroundLayer < NormalLayer < PopUpLayer < GuideLayer < TopLayer < NotifyLayer
    /// </summary>
    public enum UILayer
    {
        BackgroundLayer, // 背景层（黑边、背景图）
        NormalLayer,     // 普通层（主界面、功能界面）
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
        Center      // 中心
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
