using System.Windows;

namespace Zen.Scroll;

public sealed class ZoomAnimationSmooth : ScrollAnimation
{
    private const double DefaultTimeConstantMs = 60;
    private Vector ZoomFrom;
    private Vector ZoomTo;
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
        Start();
    }

    protected override void OnStop()
    {
        ZoomTo = ZoomFrom;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;

        // The asymptote is the target scale, so the scale at any time is the target minus what is still to
        // come: one product serves both the value and the stop test. v₀·τ is the requested change
        // (ZoomTo − ZoomFrom), so the velocity itself does not have to be kept.
        var tail = (ZoomTo - ZoomFrom) * Math.Exp(-elapsedSeconds / ZoomDurationSeconds);
        ScrollClient.UpdateScaleTarget(ZoomTo - tail);
        return elapsedSeconds <= MaxAnimationSeconds;
    }
}
