using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace Zen.Scroll;

// Scrollbar commands (dragging, paging, line, scroll-to-end) run inside the content-transform
// system instead of the ScrollViewer's native offset commits: they then retarget the running
// animation rather than being fought by it.
internal static class ScrollBarCommandHandler
{
    private const double ScrollLineDelta = 16d;

    private static readonly DependencyProperty TrackerProperty =
        DependencyProperty.RegisterAttached("Tracker", typeof(ScrollAnimationTracker),
            typeof(ScrollBarCommandHandler), new PropertyMetadata(null));

    public static void Attach(ScrollViewer scrollViewer, ScrollAnimationTracker tracker)
    {
        // Skip when already attached to the same tracker: SyncScrollableOffset calls this often,
        // and re-registering the CommandManager handlers every time would be churn.
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
        if (e.Handled) return;
        if (TryGetTracker(sender, e.Command, e.OriginalSource, out var tracker) is not true) return;

        // See OnPreviewExecuted: an explicit jump is not available while an animation is running.
        if (tracker.IsAnimating)
        {
            e.CanExecute = false;
            e.Handled = true;
            return;
        }

        if (TryResolveScrollTarget(tracker, e.Command, e.Parameter, out _, out _) is not true) return;

        e.CanExecute = true;
        e.Handled = true;
    }

    private static void OnPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Handled) return;
        if (TryGetTracker(sender, e.Command, e.OriginalSource, out var tracker) is not true) return;

        // An explicit jump has nowhere to land while an animation is still running: the animation keeps
        // re-emitting towards the target it captured before and drags the content back. Drop the command
        // rather than let the two fight — it works again as soon as the animation settles. Dragging
        // (the deferred commands) is left alone: it is continuous input, not a jump.
        if (tracker.IsAnimating)
        {
            e.Handled = true;
            return;
        }

        if (TryResolveScrollTarget(tracker, e.Command, e.Parameter, out var isVertical, out var target) is not true) return;

        e.Handled = true;

        var current = tracker.AnimatedOffset;
        tracker.AnimateScrollTo(isVertical ? current.WithY(target) : current.WithX(target));

        if (IsDeferScrollCommand(e.Command) is not true)
        {
            tracker.ApplyAnimatedOffset();
        }
    }

    private static ScrollAnimationTracker? GetTracker(ScrollViewer scrollViewer) =>
        scrollViewer.GetValue(TrackerProperty) as ScrollAnimationTracker;

    private static bool TryGetTracker(object sender, ICommand command, object? originalSource, out ScrollAnimationTracker tracker)
    {
        tracker = null!;

        if (IsScrollCommand(command) is not true) return false;
        if (sender is not ScrollViewer scrollViewer) return false;
        if (GetTracker(scrollViewer) is not ScrollAnimationTracker value) return false;
        if (IsFromNestedScrollViewer(originalSource, scrollViewer)) return false;

        tracker = value;
        return true;
    }

    // Commands bubble up from nested ScrollViewers; only handle the ones that belong to this one.
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

    private static bool IsScrollCommand(ICommand command) =>
        command is RoutedCommand { OwnerType: var ownerType } && ownerType == typeof(ScrollBar);

    private static bool IsScrollToOffsetCommand(ICommand command) =>
        command == ScrollBar.ScrollToHorizontalOffsetCommand ||
        command == ScrollBar.ScrollToVerticalOffsetCommand;
}
