using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zen.Scroll;

public abstract class ScrollAnimation
{
    // Exponential decay never truly reaches zero; use this as the common stop condition.
    protected const double MaxAnimationSeconds = 1d;

    protected const double MillisecondsPerSecond = 1000d;

    internal ScrollAnimationClient? InternalScrollClient;

    protected ScrollAnimationClient ScrollClient =>
        InternalScrollClient ?? throw new InvalidOperationException("ScrollClient is not set.");

    protected bool IsAttached => InternalScrollClient is not null;

    public bool IsActive { get; private set; }

    public long StartTimestamp { get; private set; }

    public abstract void ScrollBy(Vector delta);

    public abstract void ScrollBy(Vector delta, double duration);

    // Absolute values in the same space as the ScrollBy delta: offsets for scroll, scale for zoom.
    public abstract void ScrollTo(Vector from, Vector to);

    public abstract void ScrollTo(Vector from, Vector to, double duration);

    [MemberNotNullWhen(true, nameof(InternalScrollClient))]
    public bool CheckAccess() => InternalScrollClient is not null;

    public void Start()
    {
        if (CheckAccess())
        {
            IsActive = true;
            StartTimestamp = Stopwatch.GetTimestamp();
            ScrollClient.Start(this);
            OnStart();
        }
    }

    public void Stop()
    {
        if (IsActive is not true)
            return;

        if (CheckAccess())
        {
            CheckAccess();
            IsActive = false;
            ScrollClient.Stop(this);
            OnStop();
        }
    }

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }

    // Returns false when the animation has finished; the caller is responsible for Stop().
    public abstract bool ServiceAnimation(TimeSpan elapsedTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan TimeSinceStart() => Stopwatch.GetElapsedTime(StartTimestamp);

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool),
            typeof(ScrollAnimation), new PropertyMetadata(OnIsEnabledChanged));

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached("Controller", typeof(ScrollAnimationController),
            typeof(ScrollAnimation), new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        if (scrollViewer.GetValue(ControllerProperty) is not ScrollAnimationController controller)
        {
            controller = new ScrollAnimationController(scrollViewer);
            scrollViewer.SetValue(ControllerProperty, controller);
        }

        // On disable only the animations are cleared and deactivated; the controller itself is kept
        // so that re-enabling reuses the same instance.
        if (e.NewValue is true)
        {
            controller.ZoomAnimation ??= new ZoomAnimationSmooth();
            controller.ScrollAnimation ??= new ScrollAnimationSmooth();
            controller.SetIsEnabled(true);
        }
        else
        {
            controller.ZoomAnimation = null;
            controller.ScrollAnimation = null;
            controller.SetIsEnabled(false);
        }
    }

    public static bool GetIsEnabled(ScrollViewer scrollViewer)
    {
        return (bool)scrollViewer.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(ScrollViewer scrollViewer, bool value)
    {
        scrollViewer.SetValue(IsEnabledProperty, value);
    }

    // A wheel delta that is not a whole line is from a touchpad (continuous fractional deltas).
    protected static bool IsTouchPadScroll(Vector value) =>
       value.X % Mouse.MouseWheelDeltaForOneLine != 0 ||
       value.Y % Mouse.MouseWheelDeltaForOneLine != 0;
}
