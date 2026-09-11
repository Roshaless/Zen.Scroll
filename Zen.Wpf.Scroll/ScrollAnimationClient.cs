using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Zen.Scroll;

public abstract class ScrollAnimationClient(ScrollViewer scrollViewer)
{
    protected ScrollViewer RootScrollViewer { get; } = scrollViewer;

    protected List<ScrollAnimation> ActiveAnimations { get; } = [];

    public abstract Vector MinimumScrollOffset { get; }

    public abstract Vector MaximumScrollOffset { get; }

    public abstract Vector CurrentOffset { get; }

    public abstract Vector CurrentScale { get; }

    public bool IsActive { get; private set; }

    public abstract void UpdateScaleTarget(Vector scale);

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
        if (ActiveAnimations.Contains(animation) is not true)
        {
            ActiveAnimations.Add(animation);
        }

        if (IsActive is not true && ActiveAnimations.Count > 0)
        {
            IsActive = true;
            OnStart();
        }
    }

    public void Stop(ScrollAnimation animation)
    {
        if (ActiveAnimations.Remove(animation) is not true)
            return;

        if (ActiveAnimations.Count == 0)
        {
            IsActive = false;
            Stop();
        }
    }

    public void Stop()
    {
        foreach (var animation in ActiveAnimations)
        {
            animation.Stop();
        }

        ActiveAnimations.Clear();
        IsActive = false;
        OnStop();
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (IsActive is not true)
            return;

        for (var i = 0; i < ActiveAnimations.Count; i++)
        {
            var animation = ActiveAnimations[i];
            if (animation.ServiceAnimation(animation.TimeSinceStart()) is not true)
                animation.Stop();
        }
    }
}