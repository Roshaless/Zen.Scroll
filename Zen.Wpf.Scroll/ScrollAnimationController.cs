using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zen.Scroll;

public sealed class ScrollAnimationController : ScrollAnimationClient
{
    private readonly ScrollAnimationTracker Tracker;
    private double MinimumScaleValue;
    private double MaximumScaleValue;
    private double ScrollDeltaValue;
    private double ScrollDurationValue;
    private double ZoomDeltaValue;

    public override Vector MinimumScrollOffset => default;

    public override Vector MaximumScrollOffset => Tracker.ScrollableOffset;

    public override Vector CurrentOffset => Tracker.AnimatedOffset;

    public override double CurrentScale => Tracker.ContentScale;

    public override double ScrollDelta => ScrollDeltaValue;

    public override double ScrollDuration => ScrollDurationValue;

    public override double ZoomDelta => ZoomDeltaValue;

    public override double MinimumScale => MinimumScaleValue;

    public override double MaximumScale => MaximumScaleValue;

    public ZoomAnimation? ZoomAnimation
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

    private T? SwapAnimation<T>(T? oldValue, T? newValue) where T : MotionAnimation
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
        MouseMoveThrottler.Enable();
        base.OnStart();
    }

    protected override void OnStop()
    {
        Tracker.ApplyAnimatedOffset();
        Tracker.CommitContentCacheScale();
        MouseMoveThrottler.Disable();
        base.OnStop();
    }

    protected override void OnFrameRendered()
    {
        Tracker.FlushFrame();
    }

    public override void UpdateScaleTarget(double scale) => Tracker.SetContentScale(scale);

    public override void UpdateScrollDelta(Vector delta) => Tracker.ApplyScrollDelta(delta);

    private void OnMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (e.Handled || Tracker.IsInitialized is not true) return;
        if (Keyboard.Modifiers is ModifierKeys.Control)
        {
            if (Tracker.IsRootScrollViewer)
            {
                var zoomDelta = e.Delta / Mouse.MouseWheelDeltaForOneLine * ZoomDelta;
                if (Tracker.CanZoom(zoomDelta))
                {
                    e.Handled = true;
                    Tracker.ContentScaleCenter = e.GetPosition(RootScrollViewer).ToVector();
                    ZoomAnimation?.ZoomBy(zoomDelta);
                    ScrollAnimation?.Stop();
                }
            }
        }
        else
        {
            var delta = NormalizeWheelDelta(e.Delta);
            if (Keyboard.Modifiers is ModifierKeys.Shift || e.IsHorizontalMouseWheel)
            {
                delta = e.IsHorizontalMouseWheel ? -delta : delta;
                if (Tracker.CanHorizontalScroll(delta))
                {
                    e.Handled = true;
                    ScrollAnimation?.ScrollBy(new Vector(delta, 0d));
                }

                return;
            }

            if (Tracker.CanVerticalScroll(delta))
            {
                e.Handled = true;
                ScrollAnimation?.ScrollBy(new Vector(0d, delta));
            }
        }
    }

    private double NormalizeWheelDelta(int delta)
    {
        return IsMouseWheelDeltaForOneLine(delta) ? delta / Mouse.MouseWheelDeltaForOneLine * ScrollDelta : delta;
    }

    private static bool IsMouseWheelDeltaForOneLine(int delta)
    {
        return delta % Mouse.MouseWheelDeltaForOneLine == 0;
    }

    private static readonly Action<ScrollViewer, bool> HandlesMouseWheelScrollingSetter =
        ExpressionAccessor.BuildSetter<ScrollViewer, bool>("HandlesMouseWheelScrolling");

    private static void SetHandlesMouseWheelScrolling(ScrollViewer scrollViewer, bool value)
        => HandlesMouseWheelScrollingSetter(scrollViewer, value);
}