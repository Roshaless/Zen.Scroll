using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

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
            HandlesMouseWheelScrolling(RootScrollViewer, false);
            Tracker.Initialize();

            if (Tracker.ScrollContentObject is not null)
                ResumeLayout(null!, Tracker.ScrollContentObject);

            RefreshTuning();
        }
        else
        {
            RootScrollViewer.MouseWheel -= OnMouseWheel;
            HandlesMouseWheelScrolling(RootScrollViewer, true);

            if (Tracker.ScrollContentObject is not null)
                ResumeLayout(null!, Tracker.ScrollContentObject);

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
        if (Tracker.ScrollContentObject is not null)
            SuspendLayout(Tracker.ScrollContentObject);

        Tracker.SyncScrollableOffset();
        base.OnStart();
    }

    protected override void OnStop()
    {
        if (Tracker.ScrollContentObject is not null)
            ResumeLayout(null!, Tracker.ScrollContentObject);

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

    private static Action<Visual> BuildSuspend(MethodInfo method)
    {
        // v => UIElement.PropagateSuspendLayout(v)
        var v = System.Linq.Expressions.Expression.Parameter(typeof(Visual), "v");
        var call = System.Linq.Expressions.Expression.Call(method, v);
        return System.Linq.Expressions.Expression.Lambda<Action<Visual>>(call, v).Compile();
    }

    private static Action<Visual, Visual> BuildResume(MethodInfo method)
    {
        // (parent, v) => UIElement.PropagateResumeLayout(parent, v)
        var parent = System.Linq.Expressions.Expression.Parameter(typeof(Visual), "parent");
        var v = System.Linq.Expressions.Expression.Parameter(typeof(Visual), "v");
        var call = System.Linq.Expressions.Expression.Call(method, parent, v);
        return System.Linq.Expressions.Expression.Lambda<Action<Visual, Visual>>(call, parent, v).Compile();
    }

    private static Action<ScrollViewer, bool> BuildHandlesMouseWheelScrolling(MethodInfo setter)
    {
        // (sv, value) => sv.set_HandlesMouseWheelScrolling(value)
        var sv = System.Linq.Expressions.Expression.Parameter(typeof(ScrollViewer), "sv");
        var value = System.Linq.Expressions.Expression.Parameter(typeof(bool), "value");
        var call = System.Linq.Expressions.Expression.Call(sv, setter, value);
        return System.Linq.Expressions.Expression.Lambda<Action<ScrollViewer, bool>>(call, sv, value).Compile();
    }

    private static readonly Action<Visual> SuspendLayout = BuildSuspend(typeof(UIElement).GetMethod(
        "PropagateSuspendLayout", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!);

    private static readonly Action<Visual, Visual> ResumeLayout = BuildResume(typeof(UIElement).GetMethod(
        "PropagateResumeLayout", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)!);

    private static readonly Action<ScrollViewer, bool> HandlesMouseWheelScrolling = BuildHandlesMouseWheelScrolling(
        typeof(ScrollViewer).GetMethod("set_HandlesMouseWheelScrolling", BindingFlags.NonPublic | BindingFlags.Instance)!);
}