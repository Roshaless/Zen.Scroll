using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Zen.Scroll;

public sealed class ScrollAnimationController : ScrollAnimationClient
{
    private const int ScrollUpdateIntervalMs = 40;
    private const int ScrollUpdateIdleTimeoutMs = 160;
    private readonly ScrollAnimationTracker Tracker;
    private readonly DispatcherTimer ScrollUpdateTimer;
    private long LastScrollActivityTimestamp;
    private bool HasPendingScrollUpdate;

    public override Vector MinimumScrollOffset => default;

    public override Vector MaximumScrollOffset => Tracker.ScrollableOffset;

    public override Vector CurrentOffset => Tracker.AnimatedOffset;

    public ScrollAnimationController(ScrollViewer scrollViewer) : base(scrollViewer)
    {
        Tracker = new ScrollAnimationTracker(scrollViewer);
        ScrollViewer.SetCanContentScroll(scrollViewer, false);
        VirtualizingPanel.SetScrollUnit(scrollViewer, ScrollUnit.Pixel);
        VirtualizingPanel.SetVirtualizationMode(scrollViewer, VirtualizationMode.Recycling);

        ScrollUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(ScrollUpdateIntervalMs),
        };
        ScrollUpdateTimer.Tick += OnScrollUpdateTimerTick;
    }

    private void OnScrollUpdateTimerTick(object? sender, EventArgs e)
    {
        if (HasPendingScrollUpdate)
        {
            HasPendingScrollUpdate = false;
            Tracker.ApplyAnimatedOffset();
        }

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

    protected override void OnStart()
    {
        Tracker.EnsureInitialized();
        RequestScrollUpdate();
        base.OnStart();
    }

    protected override void OnStop()
    {
        Tracker.ApplyAnimatedOffset();
        StopScrollUpdateTimer();
        base.OnStop();
    }
    public override void UpdateScrollTarget(Vector offset)
    {
        Tracker.AnimateScrollTo(offset);
    }

    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            RootScrollViewer.MouseWheel += OnPreviewMouseWheel;
            SetHandlesMouseWheelScrolling(RootScrollViewer, false);
            Tracker.Initialize();
        }
        else
        {
            RootScrollViewer.MouseWheel -= OnPreviewMouseWheel;
            SetHandlesMouseWheelScrolling(RootScrollViewer, true);
            Tracker.Uninitialize();
        }
    }

    private void OnPreviewMouseWheel(object? sender, MouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        if (Keyboard.Modifiers is ModifierKeys.Shift && Tracker.CanHorizontalScroll)
        {
            e.Handled = true;
            Animation?.ScrollBy(new Vector(e.Delta, 0));
            return;
        }

        if (Tracker.CanVerticallyScroll)
        {
            e.Handled = true;
            Animation?.ScrollBy(new Vector(0, e.Delta));
            return;
        }
    }


    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_HandlesMouseWheelScrolling")]
    private static extern void SetHandlesMouseWheelScrolling(ScrollViewer scrollViewer, bool value);
}
