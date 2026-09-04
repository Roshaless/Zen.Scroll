using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zen.Scroll;

public abstract class ScrollAnimationClient(ScrollViewer scrollViewer)
{
    protected ScrollViewer RootScrollViewer { get; } = scrollViewer;

    public abstract Vector MinimumScrollOffset { get; }

    public abstract Vector MaximumScrollOffset { get; }

    public abstract Vector CurrentOffset { get; }

    public bool IsActive { get; private set; }

    public ScrollAnimation? Animation { get; set; }

    public abstract void UpdateScrollTarget(Vector offset);

    protected virtual void OnStart()
    {
        CompositionTarget.Rendering += OnRendering;
    }

    protected virtual void OnStop()
    {
        CompositionTarget.Rendering -= OnRendering;
    }

    public void Start(ScrollAnimation animation)
    {
        if (IsActive)
        {
            if (Animation == animation)
            {
                return;
            }

            Stop();
        }

        IsActive = true;
        Animation = animation;

        OnStart();
    }

    public void Stop()
    {
        if (IsActive)
        {
            IsActive = false;
            OnStop();
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (IsActive is not true)
            return;

        var animation = Animation;
        if (animation is null)
            return;

        if (!animation.ServiceAnimation(animation.TimeSinceStart()))
            animation.Stop();
    }
}