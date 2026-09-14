using System.Windows;

namespace Zen.Scroll;

public sealed class ZoomAnimationSmooth : ScrollAnimation
{
    private const double DefaultTimeConstantMs = 60;
    private Vector ZoomFrom;
    private Vector ZoomTo;
    private Vector ZoomVelocity;
    private double ZoomDurationSeconds;

    public override void ScrollBy(Vector delta) => ScrollBy(delta, DefaultTimeConstantMs);

    public override void ScrollBy(Vector delta, double duration)
    {
        // While flying, new input accumulates onto the existing destination.
        var targetBase = IsActive ? ZoomTo : ScrollClient.CurrentScale;
        ScrollTo(ScrollClient.CurrentScale, targetBase + delta, duration);
    }

    public override void ScrollTo(Vector from, Vector to) => ScrollTo(from, to, DefaultTimeConstantMs);

    public override void ScrollTo(Vector from, Vector to, double duration)
    {
        // The same bounds the tracker clamps to (read from the attached properties), so hitting a
        // boundary is detected against the range the caller actually configured.
        var minimumScale = ScrollClient.MinimumScale;
        var maximumScale = ScrollClient.MaximumScale;

        ZoomFrom = from.ConstrainedBetween(minimumScale, maximumScale);
        ZoomTo = to.ConstrainedBetween(minimumScale, maximumScale);

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
