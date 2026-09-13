using System.Windows;

namespace Zen.Scroll;

public sealed class ZoomAnimationSmooth : ScrollAnimation
{
    private const double DefaultTimeConstantMs = 60;
    private Vector MinimumScale = new(1, 1);
    private Vector MaximumScale = new(10, 10);
    private Vector ZoomFrom;
    private Vector ZoomTo;
    private Vector ZoomVelocity;
    private double ZoomDurationSeconds;

    public override void ScrollBy(Vector delta) => ScrollBy(delta, DefaultTimeConstantMs);

    public override void ScrollBy(Vector delta, double duration)
    {
        if (IsAttached is not true) return;

        ZoomFrom = ScrollClient.CurrentScale;
        ZoomTo = ((IsActive ? ZoomTo : ZoomFrom) + delta).ConstrainedBetween(MinimumScale, MaximumScale);

        // Already at a boundary (e.g. zooming further past the cap): nothing to animate.
        if (ZoomTo == ZoomFrom)
            return;

        ZoomDurationSeconds = Math.Max(duration, 1) / MillisecondsPerSecond;
        ZoomVelocity = (ZoomTo - ZoomFrom) / ZoomDurationSeconds;
        Start();
    }

    protected override void OnStop()
    {
        ZoomVelocity = default;
        ZoomTo = ZoomFrom;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var decay = Math.Exp(-elapsedSeconds / ZoomDurationSeconds);
        var newScale = ZoomFrom + ZoomVelocity * ZoomDurationSeconds * (1 - decay);
        ScrollClient.UpdateScaleTarget(newScale);
        return elapsedSeconds <= MaxAnimationSeconds;
    }
}
