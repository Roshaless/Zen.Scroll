using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zen.Scroll;

public sealed class ScrollAnimationController : ScrollAnimationClient
{
    private readonly ScrollAnimationTracker Tracker;
    private Vector MinimumScaleValue;
    private Vector MaximumScaleValue;
    private double ScrollDeltaValue;
    private double ScrollDurationValue;
    private double ZoomDeltaValue;

    public override Vector MinimumScrollOffset => default;

    public override Vector MaximumScrollOffset => Tracker.ScrollableOffset;

    public override Vector CurrentOffset => Tracker.AnimatedOffset;

    public override Vector CurrentScale => Tracker.ContentScale;

    public override double ScrollDelta => ScrollDeltaValue;

    public override double ScrollDuration => ScrollDurationValue;

    public override double ZoomDelta => ZoomDeltaValue;

    public override Vector MinimumScale => MinimumScaleValue;

    public override Vector MaximumScale => MaximumScaleValue;

    public ScrollAnimation? ZoomAnimation
    {
        get; set => field = SwapAnimation(field, value);
    }

    public ScrollAnimation? ScrollAnimation
    {
        get; set => field = SwapAnimation(field, value);
    }

    public ScrollAnimationController(ScrollViewer scrollViewer) : base(scrollViewer)
    {
        Tracker = new ScrollAnimationTracker(scrollViewer, this);

        // Scroll in pixels (not logical items) so it composes with the pixel-based content
        // transform; recycling virtualization lowers allocation churn on long lists.
        ScrollViewer.SetCanContentScroll(scrollViewer, false);
        VirtualizingPanel.SetScrollUnit(scrollViewer, ScrollUnit.Pixel);
        VirtualizingPanel.SetVirtualizationMode(scrollViewer, VirtualizationMode.Recycling);
    }

    private ScrollAnimation? SwapAnimation(ScrollAnimation? oldValue, ScrollAnimation? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Stop();
            oldValue.InternalScrollClient = null;
        }

        newValue?.InternalScrollClient = this;
        return newValue;
    }

    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            RootScrollViewer.MouseWheel -= OnMouseWheel;
            RootScrollViewer.MouseWheel += OnMouseWheel;
            SetHandlesMouseWheelScrolling(RootScrollViewer, false);
            Tracker.Initialize();

            RefreshTuning();
        }
        else
        {
            RootScrollViewer.MouseWheel -= OnMouseWheel;
            SetHandlesMouseWheelScrolling(RootScrollViewer, true);

            Tracker.Uninitialize();
        }
    }

    internal void RefreshTuning()
    {
        ScrollDeltaValue = ScrollAnimation.GetScrollDelta(RootScrollViewer);
        ScrollDurationValue = ScrollAnimation.GetScrollDuration(RootScrollViewer);
        MinimumScaleValue = ScrollAnimation.GetMinimumScale(RootScrollViewer);
        MaximumScaleValue = ScrollAnimation.GetMaximumScale(RootScrollViewer);
        ZoomDeltaValue = ScrollAnimation.GetZoomDelta(RootScrollViewer);
    }

    protected override void OnStart()
    {
        Tracker.SyncScrollableOffset();
        base.OnStart();
    }

    protected override void OnStop()
    {
        Tracker.ApplyAnimatedOffset();
        Tracker.CommitContentCacheScale();
        base.OnStop();
    }

    protected override void OnFrameRendered()
    {
        Tracker.FlushFrame();
    }

    public override void UpdateScrollDelta(Vector delta) => Tracker.ApplyScrollDelta(delta);

    public override void UpdateScaleTarget(Vector scale) => Tracker.SetContentScale(scale);

    private void OnMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (e.Handled || Tracker.IsInitialized is not true)
            return;

        if (Keyboard.Modifiers is ModifierKeys.Control && Tracker.IsRootScrollViewer && Tracker.CanZoom)
        {
            e.Handled = true;
            Tracker.ContentScaleCenter = e.GetPosition(RootScrollViewer).ToVector();
            // One notch is ZoomDelta; normalizing by the notch unit keeps multi-notch and
            // high-resolution wheel deltas proportional.
            var zoomDelta = e.Delta / Mouse.MouseWheelDeltaForOneLine * ZoomDelta;
            ZoomAnimation?.ScrollBy(new Vector(zoomDelta, zoomDelta));
            ScrollAnimation?.Stop();
            return;
        }

        if (Keyboard.Modifiers is ModifierKeys.Shift && Tracker.CanHorizontalScroll)
        {
            e.Handled = true;
            ScrollAnimation?.ScrollBy(new Vector(IsMouseWheelDeltaForOneLine(e.Delta) ?
                e.Delta / Mouse.MouseWheelDeltaForOneLine * ScrollDelta : e.Delta, 0d));
            return;
        }

        if (Tracker.CanVerticallyScroll)
        {
            e.Handled = true;
            ScrollAnimation?.ScrollBy(new Vector(0d, IsMouseWheelDeltaForOneLine(e.Delta) ?
                e.Delta / Mouse.MouseWheelDeltaForOneLine * ScrollDelta : e.Delta));
        }
    }

    private static bool IsMouseWheelDeltaForOneLine(double delta)
    {
        return delta % Mouse.MouseWheelDeltaForOneLine == 0;
    }

#if NET8_0_OR_GREATER
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_HandlesMouseWheelScrolling")]
    private static extern void SetHandlesMouseWheelScrolling(ScrollViewer scrollViewer, bool value);
#else
    private static void SetHandlesMouseWheelScrolling(ScrollViewer scrollViewer, bool value)
    {
        var internalFlag = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        typeof(ScrollViewer).GetMethod("set_HandlesMouseWheelScrolling", internalFlag).Invoke(scrollViewer, [value]);
    }
#endif
}