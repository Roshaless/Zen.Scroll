using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Zen.Scroll;

public sealed class ScrollAnimationController : ScrollAnimationClient
{
    // How often the in-flight content offset is folded back into the real scroll position (ms).
    private const int ScrollUpdateIntervalMs = 40;

    private const int ScrollUpdateIdleTimeoutMs = 160;

    // Ctrl+wheel zoom sensitivity: the wheel delta is divided by this to get the zoom delta.
    private const double ZoomWheelSensitivity = 600d;

    private readonly ScrollAnimationTracker Tracker;
    private readonly DispatcherTimer ScrollUpdateTimer;
    private long LastScrollActivityTimestamp;
    private bool HasPendingScrollUpdate;

    public ScrollAnimation? ZoomAnimation
    {
        get; set => field = SwapAnimation(field, value);
    }

    public ScrollAnimation? ScrollAnimation
    {
        get; set => field = SwapAnimation(field, value);
    }

    public override Vector MinimumScrollOffset => default;

    public override Vector MaximumScrollOffset => Tracker.ScrollableOffset;

    public override Vector CurrentOffset => Tracker.AnimatedOffset;

    public override Vector CurrentScale => Tracker.ContentScale;

    public ScrollAnimationController(ScrollViewer scrollViewer) : base(scrollViewer)
    {
        Tracker = new ScrollAnimationTracker(scrollViewer, this);

        // Scroll in pixels (not logical items) so it composes with the pixel-based content
        // transform; recycling virtualization lowers allocation churn on long lists.
        ScrollViewer.SetCanContentScroll(scrollViewer, false);
        VirtualizingPanel.SetScrollUnit(scrollViewer, ScrollUnit.Pixel);
        VirtualizingPanel.SetVirtualizationMode(scrollViewer, VirtualizationMode.Recycling);

        ScrollUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ScrollUpdateIntervalMs),
        };
        ScrollUpdateTimer.Tick += OnScrollUpdateTimerTick;
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
        }
        else
        {
            RootScrollViewer.MouseWheel -= OnMouseWheel;
            SetHandlesMouseWheelScrolling(RootScrollViewer, true);
            Tracker.Uninitialize();
        }
    }

    protected override void OnStart()
    {
        Tracker.SyncScrollableOffset();
        RequestScrollUpdate();
        base.OnStart();
    }

    protected override void OnStop()
    {
        Tracker.ApplyAnimatedOffset();
        Tracker.CommitContentCacheScale();
        StopScrollUpdateTimer();
        base.OnStop();
    }

    protected override void OnFrameRendered()
    {
        Tracker.FlushFrame();
        RequestScrollUpdate();
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
            ZoomAnimation?.ScrollBy(new Vector(e.Delta, e.Delta) / ZoomWheelSensitivity);
            ScrollAnimation?.Stop();
            return;
        }

        if (Keyboard.Modifiers is ModifierKeys.Shift && Tracker.CanHorizontalScroll)
        {
            e.Handled = true;
            ScrollAnimation?.ScrollBy(new Vector(e.Delta, 0));
            return;
        }

        if (Tracker.CanVerticallyScroll)
        {
            e.Handled = true;
            ScrollAnimation?.ScrollBy(new Vector(0, e.Delta));
        }
    }

    private void OnScrollUpdateTimerTick(object? sender, EventArgs e)
    {
        if (HasPendingScrollUpdate)
        {
            HasPendingScrollUpdate = false;
            Tracker.ApplyAnimatedOffset();
        }

        // Stop the timer after a while without scroll activity.
        if (Environment.TickCount64 - LastScrollActivityTimestamp >= ScrollUpdateIdleTimeoutMs)
        {
            StopScrollUpdateTimer();
        }
    }

    private void RequestScrollUpdate()
    {
        HasPendingScrollUpdate = true;
        LastScrollActivityTimestamp = Environment.TickCount64;

        if (ScrollUpdateTimer.IsEnabled is not true)
        {
            ScrollUpdateTimer.Start();
        }
    }

    private void StopScrollUpdateTimer()
    {
        HasPendingScrollUpdate = false;
        if (ScrollUpdateTimer.IsEnabled)
        {
            ScrollUpdateTimer.Stop();
        }
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
