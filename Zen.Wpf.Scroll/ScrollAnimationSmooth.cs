using System.Windows;

namespace Zen.Scroll;

public sealed class ScrollAnimationSmooth : ScrollAnimation
{
    private const double MaxInitialVelocity = 8000;
    private const double MillisecondsPerSecond = 1000;
    private const double DefaultTimeConstantMs = 120;
    private const double MinDurationOpposite = 70;
    private const double MinDurationSame = 100;
    private Vector PreviousTouchPadDelta;
    private Vector StartOffset;
    private Vector DestinationOffset;
    private Vector InitialVelocity;
    private double CurrentVelocity;
    private double TimeConstantSeconds;

    public override void ScrollBy(Vector delta)
    {
        if (IsTouchPadScroll(delta))
        {
            if ((PreviousTouchPadDelta * delta) < 0)
            {
                ScrollBy(delta, MinDurationOpposite);
            }
            else
            {
                var velocity = delta.Length / MinDurationSame;
                var value = DefaultTimeConstantMs - (CurrentVelocity + velocity) / DefaultTimeConstantMs;
                ScrollBy(delta, Math.Clamp(value, MinDurationSame, DefaultTimeConstantMs));
            }

            PreviousTouchPadDelta = delta;
        }
        else
        {
            ScrollBy(delta, DefaultTimeConstantMs);
        }
    }

    public override void ScrollBy(Vector delta, double duration)
    {
        var fromOffset = ScrollClient.CurrentOffset;
        var destinationOffset = (ScrollClient.IsActive ? DestinationOffset - delta : fromOffset - delta)
             .ConstrainedBetween(ScrollClient.MinimumScrollOffset, ScrollClient.MaximumScrollOffset);

        if (destinationOffset != fromOffset)
        {
            var durationSeconds = duration / MillisecondsPerSecond;
            var initialVelocity = (destinationOffset - fromOffset) / durationSeconds;
            StartScroll(fromOffset, destinationOffset, initialVelocity, durationSeconds);
        }
    }

    private void StartScroll(Vector fromOffset, Vector destinationOffset, Vector initialVelocity, double timeConstantSeconds)
    {
        var velocity = Math.Abs(initialVelocity.Length);
        if (velocity > MaxInitialVelocity)
        {
            initialVelocity = Vector.Multiply(initialVelocity, MaxInitialVelocity / velocity);
        }

        StartOffset = fromOffset;
        DestinationOffset = destinationOffset;
        InitialVelocity = initialVelocity;
        TimeConstantSeconds = timeConstantSeconds;
        ScrollClient.UpdateScrollTarget(fromOffset);
        Start();
    }

    protected override void OnStop()
    {
        InitialVelocity = default;
        CurrentVelocity = default;
        PreviousTouchPadDelta = default;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var decay = Math.Exp(-elapsedSeconds / TimeConstantSeconds);
        var newOffset = StartOffset + InitialVelocity * TimeConstantSeconds * (1 - decay);
        CurrentVelocity = Math.Abs((InitialVelocity * decay).Length);
        ScrollClient.UpdateScrollTarget(newOffset);
        return elapsedSeconds <= 1;
    }
}
