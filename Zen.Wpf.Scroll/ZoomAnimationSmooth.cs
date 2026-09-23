namespace Zen.Scroll;

public sealed class ZoomAnimationSmooth : ZoomAnimation
{
    private const double DefaultTimeConstantMs = 60;
    private double From, To, DurationSeconds;

    public override void ZoomBy(double delta)
    {
        ZoomBy(delta, DefaultTimeConstantMs);
    }

    public override void ZoomBy(double delta, double duration)
    {
        var targetBase = IsActive ? To : ScrollClient.CurrentScale;
        ZoomTo(ScrollClient.CurrentScale, targetBase + delta, duration);
    }

    public override void ZoomTo(double from, double to)
    {
        ZoomTo(from, to, DefaultTimeConstantMs);
    }

    public override void ZoomTo(double from, double to, double duration)
    {
        // The same bounds the tracker clamps to (read from the attached properties), so hitting a
        // boundary is detected against the range the caller actually configured.
        var minimumScale = ScrollClient.MinimumScale;
        var maximumScale = ScrollClient.MaximumScale;

        From = Math.Clamp(from, minimumScale, maximumScale);
        To = Math.Clamp(to, minimumScale, maximumScale);

        // Already at a boundary (e.g. zooming further past the cap): nothing to animate.
        if (To == From)
            return;

        DurationSeconds = Math.Max(duration, 1) / MillisecondsPerSecond;
        Start();
    }


    protected override void OnStop()
    {
        To = From;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;

        // The asymptote is the target scale, so the scale at any time is the target minus what is still to
        // come: one product serves both the value and the stop test. v₀·τ is the requested change
        // (ZoomTo − ZoomFrom), so the velocity itself does not have to be kept.
        var tail = (To - From) * Math.Exp(-elapsedSeconds / DurationSeconds);
        ScrollClient.UpdateScaleTarget(To - tail);
        return elapsedSeconds <= MaxAnimationSeconds;
    }
}
