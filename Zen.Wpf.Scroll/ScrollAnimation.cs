using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Zen.Scroll;

public abstract class ScrollAnimation
{
    // Exponential decay never truly reaches zero; use this as the common stop condition.
    protected const double MaxAnimationSeconds = 1d;

    protected const double MillisecondsPerSecond = 1000d;

    internal ScrollAnimationClient? InternalScrollClient;

    protected ScrollAnimationClient ScrollClient => InternalScrollClient ?? throw new InvalidOperationException("ScrollClient is not set.");

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

    public static bool GetIsEnabled(ScrollViewer scrollViewer) =>
        (bool)scrollViewer.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(ScrollViewer scrollViewer, bool value) =>
        scrollViewer.SetValue(IsEnabledProperty, value);

    public static readonly DependencyProperty ScrollDeltaProperty =
        DependencyProperty.RegisterAttached("ScrollDelta", typeof(double),
            typeof(ScrollAnimation), TuningMetadata(100.0d, CoerceScrollDelta));

    public static readonly DependencyProperty ScrollDurationProperty =
        DependencyProperty.RegisterAttached("ScrollDuration", typeof(double),
            typeof(ScrollAnimation), TuningMetadata(70d, CoerceScrollDuration));

    public static readonly DependencyProperty MinimumScaleProperty =
        DependencyProperty.RegisterAttached("MinimumScale", typeof(Vector),
            typeof(ScrollAnimation), TuningMetadata(new Vector(1, 1), CoerceMinimumScale));

    public static readonly DependencyProperty MaximumScaleProperty =
        DependencyProperty.RegisterAttached("MaximumScale", typeof(Vector),
            typeof(ScrollAnimation), TuningMetadata(new Vector(10, 10), CoerceMaximumScale));

    public static readonly DependencyProperty ZoomDeltaProperty =
        DependencyProperty.RegisterAttached("ZoomDelta", typeof(double),
            typeof(ScrollAnimation), new PropertyMetadata(0.2d, OnTuningChanged));

    public static double GetScrollDelta(ScrollViewer scrollViewer) =>
        (double)scrollViewer.GetValue(ScrollDeltaProperty);

    public static void SetScrollDelta(ScrollViewer scrollViewer, double value) =>
        scrollViewer.SetValue(ScrollDeltaProperty, value);

    public static double GetScrollDuration(ScrollViewer scrollViewer) =>
        (double)scrollViewer.GetValue(ScrollDurationProperty);

    public static void SetScrollDuration(ScrollViewer scrollViewer, double value) =>
        scrollViewer.SetValue(ScrollDurationProperty, value);

    public static Vector GetMinimumScale(ScrollViewer scrollViewer) =>
        (Vector)scrollViewer.GetValue(MinimumScaleProperty);

    public static void SetMinimumScale(ScrollViewer scrollViewer, Vector value) =>
        scrollViewer.SetValue(MinimumScaleProperty, value);

    public static Vector GetMaximumScale(ScrollViewer scrollViewer) =>
        (Vector)scrollViewer.GetValue(MaximumScaleProperty);

    public static void SetMaximumScale(ScrollViewer scrollViewer, Vector value) =>
        scrollViewer.SetValue(MaximumScaleProperty, value);

    public static double GetZoomDelta(ScrollViewer scrollViewer) =>
        (double)scrollViewer.GetValue(ZoomDeltaProperty);

    public static void SetZoomDelta(ScrollViewer scrollViewer, double value) =>
        scrollViewer.SetValue(ZoomDeltaProperty, value);

    // Tuning metadata: a changed value is pushed to the controller (see OnTuningChanged) and an
    // out-of-range value is coerced to the nearest allowed one, so a caller cannot produce an
    // unusable curve. Properties without a limit keep the plain metadata.
    private static FrameworkPropertyMetadata TuningMetadata(object defaultValue, CoerceValueCallback coerce) =>
        new(defaultValue, OnTuningChanged) { CoerceValueCallback = coerce };

    // A changed value is pushed to the controller that already exists, so its cached members are
    // always current and nothing has to be re-read at the input boundaries. A value set before the
    // controller exists is picked up when it is created; configuring a ScrollViewer never enables
    // the feature by itself.
    private static void OnTuningChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // Raising the lower zoom bound above the current upper one drags the upper bound along.
        if (e.Property == MinimumScaleProperty)
        {
            d.CoerceValue(MaximumScaleProperty);
        }

        if (d is ScrollViewer scrollViewer && scrollViewer.GetValue(ControllerProperty) is ScrollAnimationController controller)
        {
            controller.RefreshTuning();
        }
    }

    // ScrollDelta and ZoomDelta keep their sign — positive scrolls or zooms one way, negative the
    // other. The rest have no meaningful direction, so their sign is folded away before the value is
    // fitted to its range.

    // Below half a line one notch barely moves, so 48 is the floor of the magnitude; the sign is
    // kept, so a negative step scrolls backwards by at least that much.
    private static object CoerceScrollDelta(DependencyObject d, object baseValue)
    {
        var delta = (double)baseValue;
        var magnitude = Math.Max(Math.Abs(delta), 48d);

        return delta < 0d ? -magnitude : magnitude;
    }

    // A second is where one gesture stops reading as a single motion; the lower bound keeps the
    // decay maths away from a division by zero.
    private static object CoerceScrollDuration(DependencyObject d, object baseValue) =>
        Math.Clamp(Math.Abs((double)baseValue), 30d, 1000d);

    // Zooming out below the original size leaves nothing to scroll, so 1 is a hard floor per axis;
    // the ceiling is the same 20 the upper bound uses, since nothing above it could ever be reached.
    private static object CoerceMinimumScale(DependencyObject d, object baseValue)
    {
        var scale = (Vector)baseValue;

        return new Vector(
            Math.Clamp(Math.Abs(scale.X), 1d, 20d),
            Math.Clamp(Math.Abs(scale.Y), 1d, 20d));
    }

    // 20x is where zooming stops being useful and starts to feel like a microscope. The upper bound
    // follows MinimumScale, so the range cannot invert: raising the lower bound above the current
    // upper one drags it along (see OnTuningChanged).
    private static object CoerceMaximumScale(DependencyObject d, object baseValue)
    {
        var scale = (Vector)baseValue;
        var minimum = (Vector)d.GetValue(MinimumScaleProperty);

        return new Vector(
            Math.Clamp(Math.Abs(scale.X), Math.Min(minimum.X, 20d), 20d),
            Math.Clamp(Math.Abs(scale.Y), Math.Min(minimum.Y, 20d), 20d));
    }
}
