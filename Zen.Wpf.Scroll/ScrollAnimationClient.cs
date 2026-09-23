using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zen.Scroll;

public abstract class ScrollAnimationClient(ScrollViewer scrollViewer)
{
    protected ScrollViewer RootScrollViewer { get; } = scrollViewer;

    private readonly List<MotionAnimation> ActiveAnimations = [];

    public abstract Vector MinimumScrollOffset { get; }

    public abstract Vector MaximumScrollOffset { get; }

    public abstract Vector CurrentOffset { get; }

    public abstract double CurrentScale { get; }

    public abstract double MinimumScale { get; }

    public abstract double MaximumScale { get; }

    public abstract double ScrollDelta { get; }

    public abstract double ScrollDuration { get; }

    public abstract double ZoomDelta { get; }

    public bool IsActive { get; private set; }

    public abstract void UpdateScaleTarget(double scale);

    public abstract void UpdateScrollDelta(Vector delta);

    protected virtual void OnStart()
    {
        CompositionTarget.Rendering += OnRendering;
    }

    protected virtual void OnStop()
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    public void Start(MotionAnimation animation)
    {
        if (ActiveAnimations.Contains(animation) is not true)
        {
            ActiveAnimations.Add(animation);
        }

        if (IsActive is not true)
        {
            IsActive = true;
            OnStart();
        }
    }

    public void Stop(MotionAnimation animation)
    {
        if (ActiveAnimations.Remove(animation) is not true)
            return;

        if (ActiveAnimations.Count == 0)
        {
            Stop();
        }
    }

    public void Stop()
    {
        // Iterate backwards: animation.Stop() removes itself from the list via Stop(animation).
        // Do not use "while (Count > 0)": a stopped animation does not remove itself
        // (ScrollAnimation.Stop returns early when inactive), so that loop could never terminate.
        for (var i = ActiveAnimations.Count - 1; i >= 0; i--)
        {
            ActiveAnimations[i].Stop();
        }

        ActiveAnimations.Clear();

        // The last animation's stop re-enters this method through Stop(animation) and already
        // finished the teardown; without this guard OnStop would fire twice.
        if (IsActive is not true) return;

        IsActive = false;
        OnStop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (IsActive is not true)
            return;

        for (var i = ActiveAnimations.Count - 1; i >= 0; i--)
        {
            var animation = ActiveAnimations[i];
            if (animation.ServiceAnimation(animation.TimeSinceStart()) is not true)
                animation.Stop();
        }

        OnFrameRendered();
    }

    protected virtual void OnFrameRendered() { }

    // A wheel delta that is not a whole line is from a touchpad (continuous fractional deltas).
    public bool IsTouchPadScroll(Vector value) => value.X % ScrollDelta != 0 || value.Y % ScrollDelta != 0;
}
