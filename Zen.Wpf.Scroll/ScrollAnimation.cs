using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace Zen.Scroll;

public abstract class ScrollAnimation(ScrollAnimationClient scrollClient)
{
    private readonly ScrollAnimationClient _scrollClient = scrollClient;

    protected ScrollAnimationClient ScrollClient => _scrollClient;

    public bool IsActive => _scrollClient.IsActive;

    public long StartTimestamp { get; private set; }

    public abstract double Duration { get; }

    public void Start()
    {
        StartTimestamp = Stopwatch.GetTimestamp();
        _scrollClient.Start(this);
        OnStart();
    }

    public void Stop()
    {
        _scrollClient.Stop();
        OnStop();
    }

    public void Pause()
    {
        //StartTimestamp = 0;
        _scrollClient.Pause();
        OnPause();
    }

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }

    protected virtual void OnPause() { }

    public abstract bool ServiceAnimation(TimeSpan elapsedTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan TimeSinceStart() => Stopwatch.GetElapsedTime(StartTimestamp);


    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), 
            typeof(ScrollAnimation), new PropertyMetadata(false, OnIsEnabledChanged));

    private static readonly DependencyProperty ControllerProperty =
        DependencyProperty.RegisterAttached("Controller", typeof(ScrollAnimationClient),
            typeof(ScrollAnimation), new PropertyMetadata(null));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            return;
        }

        var controller = GetController(scrollViewer);
        if (controller is null)
        {
            controller = new ScrollAnimationController(scrollViewer);
            SetController(scrollViewer, controller);
        }

       controller.SetIsEnabled((bool)e.NewValue);
    }

    public static bool GetIsEnabled(ScrollViewer obj)
    {
        return (bool)obj.GetValue(IsEnabledProperty);
    }

    public static void SetIsEnabled(ScrollViewer obj, bool value)
    {
        obj.SetValue(IsEnabledProperty, value);
    }

    private static ScrollAnimationClient? GetController(ScrollViewer obj)
    {
        return (ScrollAnimationClient)obj.GetValue(ControllerProperty);
    }

    private static void SetController(ScrollViewer obj, ScrollAnimationClient? value)
    {
        obj.SetValue(ControllerProperty, value);
    }
}
