# 🌀 Zen.Scroll

[![NuGet Version](https://img.shields.io/nuget/v/Zen.Wpf.Scroll)](https://www.nuget.org/packages/Zen.Wpf.Scroll)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Zen.Wpf.Scroll)](https://www.nuget.org/packages/Zen.Wpf.Scroll)

**Zen.Scroll** 是一个 WPF 滚动动画库，为 `ScrollViewer` 及其内部包含 `ScrollViewer` 的控件（`ListView`、`DataGrid`、`GridView` 等）提供滚轮（含水平滚轮）、触控板与缩放的过渡动画。启用方式为在 `ScrollViewer` 上设置 `ScrollAnimation.IsEnabled` 附加属性，不涉及模板与布局的修改。

## ✨ 功能特性

- 🖱️ 滚轮滚动动画 —— 基于指数衰减模型，模拟物理滑动（初速与位移成正比）

- ✋ 触控板滚动 —— 依手势像素速度实时调整缓动曲线与时长

- 🔍 缩放 —— `Ctrl` + 滚轮，以鼠标位置为中心缩放

- 🎚️ 可调参数 —— 滚动步长、时间常数、缩放范围以附加属性配置

- 🎛️ 启用 —— 设置 `ScrollAnimation.IsEnabled` 附加属性，支持 XAML 与代码

- ⚡ 高性能 —— GPU 加速的视觉层变换，减少内容布局触发

- 🧩 无缝集成 —— 基于 `ScrollViewer` 扩展，无需重写布局或更改模板

- 📦 轻量 —— 纯 C# 实现，无额外依赖

---

## 📦 安装

通过 NuGet 包管理器安装：

```bash
dotnet add package Zen.Wpf.Scroll
```
或使用 Visual Studio 的 NuGet 包管理器搜索 `Zen.Wpf.Scroll` 安装

---

## 🚀 快速开始
### 1. 单个 ScrollViewer 启用
``` xml
<Window>
    <ScrollViewer ScrollAnimation.IsEnabled="True">
        <!-- 内容 -->
    </ScrollViewer>
</Window>
```


### 2. 全局启用（所有 `ScrollViewer`）
``` xml
<!-- App.xaml -->
<Application.Resources>
    <ResourceDictionary.MergedDictionaries>
        <!-- 通过字典合并的方式，确保其能够正确的覆盖默认样式并应用 -->
        <ResourceDictionary Source="/TemplateStyles.xaml" />
    </ResourceDictionary.MergedDictionaries>
</Application.Resources>

<!-- TemplateStyles.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Style BasedOn="{StaticResource {x:Type ScrollViewer}}" TargetType="ScrollViewer">
        <Setter Property="ScrollAnimation.IsEnabled" Value="true" />
    </Style>

    <Style
        x:Key="{x:Static GridView.GridViewScrollViewerStyleKey}"
        BasedOn="{StaticResource {x:Static GridView.GridViewScrollViewerStyleKey}}"
        TargetType="{x:Type ScrollViewer}">
        <Setter Property="ScrollAnimation.IsEnabled" Value="true" />
    </Style>
</ResourceDictionary>
```

### 3. 代码控制
``` csharp
// 启用/禁用
ScrollAnimation.SetIsEnabled(myScrollViewer, true);

// 检查状态
bool enabled = ScrollAnimation.GetIsEnabled(myScrollViewer);
```

### 4. 参数调节

``` xml
<Style TargetType="ScrollViewer">
    <Setter Property="ScrollAnimation.IsEnabled" Value="True" />
    <Setter Property="ScrollAnimation.ScrollDelta" Value="100" />
    <Setter Property="ScrollAnimation.ScrollDuration" Value="80" />
    <Setter Property="ScrollAnimation.MinimumScale" Value="1" />
    <Setter Property="ScrollAnimation.MaximumScale" Value="10" />
    <Setter Property="ScrollAnimation.ZoomDelta" Value="0.2" />
</Style>
```

| 附加属性 | 说明 |
| ---- | ---- |
| `ScrollDelta` | 每格滚轮的滚动量（像素），负数反向；绝对值下限 48 |
| `ScrollDuration` | 滚轮滚动的动画时长（毫秒），限制在 30~1000 |
| `MinimumScale` | 缩放下限，限制在 1~20 |
| `MaximumScale` | 缩放上限，限制在 `MinimumScale`~20 |
| `ZoomDelta` | `Ctrl` + 滚轮每格的缩放量，负数反向 |

> 超出范围的值会被就近修正，参数变更立即生效。

---

## ⚙️ 工作原理（架构概览）

### 分层结构

| 组件 | 职责 |
| ---- | ---- |
| `ScrollAnimation` / `ZoomAnimation` | 滚动 / 缩放的抽象入口 |
| `ScrollAnimationSmooth` / `ZoomAnimationSmooth` | 默认实现：滚轮走指数衰减，触控板按手势速度改曲线，缩放以鼠标位置为中心 |
| `ScrollAnimationClient` | 动画宿主抽象：定义逐帧驱动方法与只读的滚动/缩放状态 |
| `ScrollAnimationController` | 输入拦截（滚轮 / 水平滚轮 / `Ctrl` 缩放 / `Shift` 横向）、动画生命周期与输入节流、可调参数缓存 |
| `ScrollAnimationTracker` | 跟踪滚动/缩放状态，合成内容变换与滚动条更新（含可视量 ⇄ 内容量的纯计算） |
| `ScrollBarCommandHandler` | 接管滚动条命令：拖动与翻页走内容变换路径，动画期间丢弃显式跳转 |

### 调用链：

```mermaid
graph TD
    A[滚轮 / 水平滚轮 / 触控板 / Ctrl 缩放输入] --> B{Controller 拦截输入}
    B --> G[动画开始：节流鼠标移动]
    G --> C[滚动 / 缩放动画计算本帧增量]
    C --> D[Client 提交待应用目标]
    C --> E[等待每帧 FlushFrame 合成]
    D --> F[ MatrixTransform 内容变换（GPU 合成）与滚动条 ]
    E --> F
    F --> H[动画结束：折算内容偏移]

```

### 性能优化策略
|  优化点   | 实现方式  |
|  ----  | ----  |
| 视觉层驱动  | 基于内容坐标变换，完全 GPU 加速，依赖 WPF 渲染管线 |
| 鼠标输入节流 | 动画期间鼠标移动输入按固定间隔节流（默认 40ms），滚轮与按键不受影响 |
| 双变换同步 | 动画期间由内容变换承载位移，停止时折算回真实滚动位置 |
| 参数读取 | 可调参数在附加属性变更时推送到控制器缓存，动画与逐帧逻辑只读字段 |

> 动画期间的鼠标移动节流间隔可通过 `MouseMoveThrottler.IntervalMs` 调整，默认 40ms。

---

## 📝 许可证
本项目采用 Apache License 2.0 许可证，详情请参阅 LICENSE 文件。

Enjoy smooth scrolling! 🌀