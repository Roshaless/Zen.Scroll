using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zen.Scroll;

public abstract class ScrollAnimation
{
    internal ScrollAnimationClient? InternalScrollClient;

    protected ScrollAnimationClient ScrollClient
    {
        get => InternalScrollClient ?? throw new InvalidOperationException("ScrollClient is not set.");
    }

    public bool IsActive { get; private set; }

    public long StartTimestamp { get; private set; }

    public abstract void ScrollBy(Vector delta);

    public abstract void ScrollBy(Vector delta, double duration);

    public void Start()
    {
        if (InternalScrollClient is null)
            throw new InvalidOperationException("ScrollClient is not set.");

        IsActive = true;
        StartTimestamp = Stopwatch.GetTimestamp();
        ScrollClient.Start(this);
        OnStart();
    }

    public void Stop()
    {
        if (IsActive is not true)
            return;

        IsActive = false;
        ScrollClient.Stop(this);
        OnStop();
    }

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }

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

    protected static bool IsTouchPadScroll(Vector value) =>
       value.X % Mouse.MouseWheelDeltaForOneLine != 0 ||
       value.Y % Mouse.MouseWheelDeltaForOneLine != 0;
}