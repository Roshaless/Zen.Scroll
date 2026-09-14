using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Zen.Scroll;

// When zooming overflows the content, the scrollbar's Value and Maximum are written by this class
// (Value in visual-offset terms so it matches the transformed content), and ScrollBarCommandHandler
// takes over Thumb drags and paging commands; when not taken over, the template bindings are restored
// so disabling animations returns to native ScrollViewer behavior.
internal sealed class ScrollBarTakeover(ScrollViewer scrollViewer, ScrollAnimationTracker tracker)
{
    // Writes smaller than this are skipped so the Thumb doesn't re-arrange every frame.
    private const double ScrollBarMinDelta = 1d;

    private static readonly Binding DefaultHorizontalMaximumBinding = new()
    {
        Mode = BindingMode.OneWay,
        RelativeSource = RelativeSource.TemplatedParent,
        Path = new PropertyPath(ScrollViewer.ScrollableWidthProperty)
    };

    private static readonly Binding DefaultHorizontalOffsetBinding = new()
    {
        Mode = BindingMode.OneWay,
        RelativeSource = RelativeSource.TemplatedParent,
        Path = new PropertyPath(ScrollViewer.HorizontalOffsetProperty)
    };

    private static readonly Binding DefaultVerticalMaximumBinding = new()
    {
        Mode = BindingMode.OneWay,
        RelativeSource = RelativeSource.TemplatedParent,
        Path = new PropertyPath(ScrollViewer.ScrollableHeightProperty)
    };

    private static readonly Binding DefaultVerticalOffsetBinding = new()
    {
        Mode = BindingMode.OneWay,
        RelativeSource = RelativeSource.TemplatedParent,
        Path = new PropertyPath(ScrollViewer.VerticalOffsetProperty)
    };

    private ScrollBar? HorizontalScrollBar;
    private ScrollBar? VerticalScrollBar;

    // null = undecided (parts were just swapped).
    private bool? IsTakenOver;

    private double LastX = double.NaN;
    private double LastY = double.NaN;

    public void SetParts(ScrollBar? horizontal, ScrollBar? vertical)
    {
        // Only restore parts that were actually swapped: parts still in use may be mid-takeover, and
        // restoring their bindings there would fight the takeover writes.
        if (ReferenceEquals(horizontal, HorizontalScrollBar) is not true)
        {
            RestoreBindings(HorizontalScrollBar, DefaultHorizontalMaximumBinding, DefaultHorizontalOffsetBinding);
            HorizontalScrollBar = horizontal;
            LastX = double.NaN;
        }

        if (ReferenceEquals(vertical, VerticalScrollBar) is not true)
        {
            RestoreBindings(VerticalScrollBar, DefaultVerticalMaximumBinding, DefaultVerticalOffsetBinding);
            VerticalScrollBar = vertical;
            LastY = double.NaN;
        }

        IsTakenOver = null;
    }

    public void Reset()
    {
        RestoreBindings(HorizontalScrollBar, DefaultHorizontalMaximumBinding, DefaultHorizontalOffsetBinding);
        RestoreBindings(VerticalScrollBar, DefaultVerticalMaximumBinding, DefaultVerticalOffsetBinding);

        HorizontalScrollBar?.InvalidateProperty(UIElement.VisibilityProperty);
        VerticalScrollBar?.InvalidateProperty(UIElement.VisibilityProperty);

        ScrollBarCommandHandler.Detach(scrollViewer);

        HorizontalScrollBar = null;
        VerticalScrollBar = null;
        IsTakenOver = null;
        LastX = double.NaN;
        LastY = double.NaN;
    }

    public void Sync(bool isZoomed)
    {
        var anyAxisVisible = !IsHidden(scrollViewer.HorizontalScrollBarVisibility) ||
                             !IsHidden(scrollViewer.VerticalScrollBarVisibility);

        // Take over only when both axes overflow: with one axis zoomed the scrollbar can still be
        // driven natively by the ScrollViewer.
        var takeover = anyAxisVisible && isZoomed;

        if (IsTakenOver == takeover)
            return;

        IsTakenOver = takeover;

        // The bar belongs to the bindings while not taken over, so the remembered value is stale;
        // dropping it keeps the first write after engaging from being skipped as "too small".
        LastX = double.NaN;
        LastY = double.NaN;

        if (takeover)
        {
            ScrollBarCommandHandler.Attach(scrollViewer, tracker);

            // Maximum and Value are owned exclusively now: clear the template bindings explicitly,
            // otherwise they snap the bar back to native values whenever our value doesn't change
            // (can't rely on "our write implicitly clears the binding").
            ClearBindings(HorizontalScrollBar);
            ClearBindings(VerticalScrollBar);

            HorizontalScrollBar?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            VerticalScrollBar?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }
        else
        {
            ScrollBarCommandHandler.Detach(scrollViewer);

            RestoreBindings(HorizontalScrollBar, DefaultHorizontalMaximumBinding, DefaultHorizontalOffsetBinding);
            RestoreBindings(VerticalScrollBar, DefaultVerticalMaximumBinding, DefaultVerticalOffsetBinding);

            HorizontalScrollBar?.InvalidateProperty(UIElement.VisibilityProperty);
            VerticalScrollBar?.InvalidateProperty(UIElement.VisibilityProperty);
        }
    }

    public void SetMaximum(Vector scrollableOffset)
    {
        // Maximum is only ours while taken over; otherwise the template binding to
        // ScrollViewer.ScrollableWidth/Height is already correct.
        if (IsTakenOver is not true) return;

        if (HorizontalScrollBar is { } horizontal && horizontal.Maximum != scrollableOffset.X)
        {
            horizontal.Maximum = scrollableOffset.X;
        }

        if (VerticalScrollBar is { } vertical && vertical.Maximum != scrollableOffset.Y)
        {
            vertical.Maximum = scrollableOffset.Y;
        }
    }

    // Writes every frame so the bar tracks the animated offset instead of the 40ms fold, but always
    // through SetCurrentValue: unlike SetValue it leaves the template binding in place, so the bar
    // still follows the ScrollViewer for drags, track clicks and programmatic offsets. At each fold
    // the binding settles on the same value (the content offset is zeroed there), so there is no
    // visible snap between the two writers.
    public void SetValue(double x, double y)
    {
        // !(<) so the initial NaN also passes.
        if (HorizontalScrollBar is { } horizontal && !(Math.Abs(x - LastX) < ScrollBarMinDelta))
        {
            horizontal.SetCurrentValue(RangeBase.ValueProperty, x);
            LastX = x;
        }

        if (VerticalScrollBar is { } vertical && !(Math.Abs(y - LastY) < ScrollBarMinDelta))
        {
            vertical.SetCurrentValue(RangeBase.ValueProperty, y);
            LastY = y;
        }
    }

    private static void ClearBindings(ScrollBar? scrollBar)
    {
        if (scrollBar is not null)
        {
            BindingOperations.ClearBinding(scrollBar, RangeBase.MaximumProperty);
            BindingOperations.ClearBinding(scrollBar, RangeBase.ValueProperty);
        }
    }

    private static void RestoreBindings(ScrollBar? scrollBar, Binding maximumBinding, Binding offsetBinding)
    {
        if (scrollBar is null) return;

        scrollBar.SetBinding(RangeBase.MaximumProperty, maximumBinding);
        scrollBar.SetBinding(RangeBase.ValueProperty, offsetBinding);
    }

    private static bool IsHidden(ScrollBarVisibility visibility) =>
        visibility is ScrollBarVisibility.Disabled or ScrollBarVisibility.Hidden;
}
