using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Zen.Scroll;

internal static class ScrollBarCommandHandler
{
    private static readonly DependencyProperty TrackerProperty =
        DependencyProperty.RegisterAttached("Tracker", typeof(ScrollAnimationTracker),
            typeof(ScrollBarCommandHandler), new PropertyMetadata(null));

    private static ScrollAnimationTracker? GetTracker(ScrollViewer scrollViewer)
    {
        return scrollViewer.GetValue(TrackerProperty) as ScrollAnimationTracker;
    }

    public static void Attach(ScrollViewer scrollViewer, ScrollAnimationTracker tracker)
    {
        // 已挂载同一 tracker 时直接跳过，避免 SyncScrollableOffset 频繁调用时
        // 反复 Detach/Attach，导致 CommandManager 处理程序被反复注销再注册。
        if (ReferenceEquals(GetTracker(scrollViewer), tracker))
            return;

        Detach(scrollViewer);

        scrollViewer.SetValue(TrackerProperty, tracker);
        CommandManager.AddPreviewCanExecuteHandler(scrollViewer, OnPreviewCanExecute);
        CommandManager.AddPreviewExecutedHandler(scrollViewer, OnPreviewExecuted);
    }

    public static void Detach(ScrollViewer scrollViewer)
    {
        if (scrollViewer.GetValue(TrackerProperty) is null)
            return;

        scrollViewer.ClearValue(TrackerProperty);
        CommandManager.RemovePreviewCanExecuteHandler(scrollViewer, OnPreviewCanExecute);
        CommandManager.RemovePreviewExecutedHandler(scrollViewer, OnPreviewExecuted);
    }

    private static void OnPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (IsScrollCommand(e.Command) is not true) return;
        if (e.Handled || sender is not ScrollViewer scrollViewer) return;
        if (GetTracker(scrollViewer) is not ScrollAnimationTracker tracker) return;
        if (IsFromNestedScrollViewer(e.OriginalSource, scrollViewer)) return;
        if (TryResolveScrollTarget(tracker, e.Command, e.Parameter, out _, out _) is not true) return;

        e.CanExecute = true;
        e.Handled = true;
    }

    private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (IsScrollCommand(e.Command) is not true) return;
        if (e.Handled || sender is not ScrollViewer scrollViewer) return;
        if (GetTracker(scrollViewer) is not ScrollAnimationTracker tracker) return;
        if (IsFromNestedScrollViewer(e.OriginalSource, scrollViewer)) return;
        if (TryResolveScrollTarget(tracker, e.Command, e.Parameter, out var isVertical, out var target) is not true) return;

        e.Handled = true;

        var current = tracker.AnimatedOffset;
        tracker.AnimateScrollTo(isVertical ? current.WithY(target) : current.WithX(target));

        if (IsDeferScrollCommand(e.Command) is not true)
        {
            tracker.ApplyAnimatedOffset();
        }
    }

    private static bool IsFromNestedScrollViewer(object? source, ScrollViewer root)
    {
        DependencyObject? current = source as DependencyObject;

        while (current is not null)
        {
            if (current is ScrollViewer scrollViewer)
            {
                return !ReferenceEquals(scrollViewer, root);
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static bool TryResolveScrollTarget(ScrollAnimationTracker tracker, ICommand command, object? parameter, out bool isVertical, out double target)
    {
        const double ScrollLineDelta = 16d;

        isVertical = true;
        target = 0;

        if (tracker.IsInitialized is not true) return false;

        var current = tracker.AnimatedOffset;
        var viewport = tracker.ViewportSize;
        if (command == ScrollBar.LineUpCommand)
        {
            target = current.Y - ScrollLineDelta;
        }
        else if (command == ScrollBar.LineDownCommand)
        {
            target = current.Y + ScrollLineDelta;
        }
        else if (command == ScrollBar.PageUpCommand)
        {
            target = current.Y - viewport.Y;
        }
        else if (command == ScrollBar.PageDownCommand)
        {
            target = current.Y + viewport.Y;
        }
        else if (command == ScrollBar.ScrollToTopCommand)
        {
            target = 0;
        }
        else if (command == ScrollBar.ScrollToBottomCommand)
        {
            target = tracker.ScrollableOffset.Y;
        }
        else if (command == ScrollBar.LineLeftCommand)
        {
            isVertical = false;
            target = current.X - ScrollLineDelta;
        }
        else if (command == ScrollBar.LineRightCommand)
        {
            isVertical = false;
            target = current.X + ScrollLineDelta;
        }
        else if (command == ScrollBar.PageLeftCommand)
        {
            isVertical = false;
            target = current.X - viewport.X;
        }
        else if (command == ScrollBar.PageRightCommand)
        {
            isVertical = false;
            target = current.X + viewport.X;
        }
        else if (command == ScrollBar.ScrollToLeftEndCommand)
        {
            isVertical = false;
            target = 0;
        }
        else if (command == ScrollBar.ScrollToRightEndCommand)
        {
            isVertical = false;
            target = tracker.ScrollableOffset.X;
        }
        else if (IsDeferScrollCommand(command) || IsScrollToOffsetCommand(command))
        {
            if (parameter is not double value) return false;

            isVertical = command == ScrollBar.DeferScrollToVerticalOffsetCommand ||
                         command == ScrollBar.ScrollToVerticalOffsetCommand;
            target = value;
        }
        else
        {
            return false;
        }

        return true;
    }

    private static bool IsDeferScrollCommand(ICommand command) =>
        command == ScrollBar.DeferScrollToHorizontalOffsetCommand ||
        command == ScrollBar.DeferScrollToVerticalOffsetCommand;

    private static bool IsScrollToOffsetCommand(ICommand command) =>
        command == ScrollBar.ScrollToHorizontalOffsetCommand ||
        command == ScrollBar.ScrollToVerticalOffsetCommand;

    private static bool IsScrollCommand(ICommand command) =>
        command is RoutedCommand { OwnerType: var ownerType } && ownerType == typeof(ScrollBar);
}
