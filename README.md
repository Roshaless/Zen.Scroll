# 🌀 Zen.Scroll

[![NuGet Version](https://img.shields.io/nuget/v/Zen.Wpf.Scroll)](https://www.nuget.org/packages/Zen.Wpf.Scroll)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zen.Wpf.Scroll)](https://www.nuget.org/packages/Zen.Wpf.Scroll)

**Zen.Scroll** 是一个轻量级滚动动画功能实现库，为 `ScrollViewer` 及其派生控件（如 `ListView`、`DataGrid`、`GridView` 等）提供**流畅的滚动动画效果**。它通过附加属性以启用滚动动画，让滚动体验更加自然、顺滑，并且支持触控板滚动处理。

## ✨ 功能特性

- 🖱️ **平滑滚动动画**：基于 `Cubic Bezier` 物理缓动函数，提供自然的滚动反馈。
- ⚡ **高性能**：使用 `UnitBezier` 和动态时长计算，滚动响应迅速。
- 🛠️ **灵活配置**：通过附加属性 `ScrollAnimation.IsEnabled` 可单独控制每个 `ScrollViewer` 的开关。

---

## 📦 安装

通过 NuGet 包管理器安装：

```bash
dotnet add package Zen.Wpf.Scroll
```
或使用 Visual Studio 的 NuGet 包管理器搜索 `Zen.Wpf.Scroll` 安装

---

## 🚀 快速开始
### 1. 在 App.xaml 中全局启用 
通过在 `Application.Resources` 中添加 **默认样式覆盖** 并设置 **启用滚动动画**：
``` xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <!-- 其它资源字典引用 -->
        </ResourceDictionary.MergedDictionaries>

        <!-- 全局启用 -->
        <Style BasedOn="{StaticResource {x:Type ScrollViewer}}" TargetType="ScrollViewer">
            <Setter Property="ScrollAnimation.IsEnabled" Value="true" />
        </Style>

        <Style x:Key="{x:Static GridView.GridViewScrollViewerStyleKey}"
               BasedOn="{StaticResource {x:Static GridView.GridViewScrollViewerStyleKey}}"
               TargetType="ScrollViewer">
            <Setter Property="ScrollAnimation.IsEnabled" Value="true" />
        </Style>
    </ResourceDictionary>
</Application.Resources>
```


### 2. 直接应用于单个控件
如果你只想在特定控件上启用动画，可以直接设置附加属性：
``` xml
<ScrollViewer ScrollAnimation.IsEnabled="True">
    <!-- 内容 -->
</ScrollViewer>
```

## 3. 控件级样式
也可以通过显式样式应用（避免被其它样式覆盖）：
``` xml
<Style x:Key="MyScrollViewerStyle" TargetType="ScrollViewer">
    <Setter Property="ScrollAnimation.IsEnabled" Value="true" />
</Style>
```

---

## 📝 许可证
本项目采用 Apache License 2.0 许可证，详情请参阅 LICENSE 文件。

## 🙏 致谢
本项目的滚动动画缓动算法参考了 **WebKit** 开源项目中的 **UnitBezier.h** 实现（采用 BSD 2-Clause 许可证）。并在其基础上进行了移植和适配，使其适用于 WPF 的滚动动画场景。

感谢 **Apple Inc.** 和 **WebKit** 贡献者 的杰出工作，让我们能够借鉴其成熟的计算方法，实现了流畅的滚动体验。

相关引用：
- [WebKit: Source/WebCore/platform/graphics/UnitBezier.h](https://github.com/WebKit/WebKit/blob/main/Source/WebCore/platform/graphics/UnitBezier.h)

Enjoy smooth scrolling! 🌀