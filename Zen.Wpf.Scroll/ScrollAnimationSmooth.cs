using System.Windows;
using System.Windows.Media.Animation;

namespace Zen.Scroll;

public sealed class ScrollAnimationSmooth : ScrollAnimation
{
    private const double MaxInitialVelocity = 8000;
    private const double MillisecondsPerSecond = 1000;
    private const double DefaultTimeConstantMs = 120;
    private readonly KeySpline TouchPadEase = new();
    private Vector StartOffset;
    private Vector DestinationOffset;
    private Vector ScrollDelta;
    private Vector ScrolledOffset;
    private Vector InitialVelocity;
    private double DurationSeconds;
    private bool UseTouchPadScroll;

    public override void ScrollBy(Vector delta)
    {
        if (IsTouchPadScroll(delta))
        {
            var currentVelocity = Vector.Divide(delta, TimeSinceStart().TotalMilliseconds);
            var speedRadio = Math.Clamp(0.1 + (1 - 0.2) / (1.0 + 0.3 * currentVelocity.Length), 0, 1);
            var scalar = Math.Max(Math.Abs(currentVelocity.Length), 1d);

            UseTouchPadScroll = true;
            TouchPadEase.ControlPoint1 = new Point(0, 1 - speedRadio);
            TouchPadEase.ControlPoint2 = new Point(Math.Clamp(1 - (1 / scalar), 0, 0.42), 1);
            ScrollBy(delta, Math.Clamp((1000 - (800 * speedRadio)) / scalar, 16, 1000));
        }
        else
        {
            UseTouchPadScroll = false;
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
            StartScroll(fromOffset, destinationOffset, duration);
        }
    }

    private void StartScroll(Vector fromOffset, Vector destinationOffset, double duration)
    {
        var durationSeconds = duration / MillisecondsPerSecond;
        var initialVelocity = (destinationOffset - fromOffset) / durationSeconds;
        var initialVelocityAbs = Math.Abs(initialVelocity.Length);
        if (initialVelocityAbs > MaxInitialVelocity)
        {
            initialVelocity = Vector.Multiply(initialVelocity, MaxInitialVelocity / initialVelocityAbs);
        }

        StartOffset = fromOffset;
        DestinationOffset = destinationOffset;
        ScrollDelta = destinationOffset - fromOffset;
        InitialVelocity = initialVelocity;
        DurationSeconds = durationSeconds;
        ScrollClient.UpdateScrollTarget(fromOffset);
        Start();
    }

    protected override void OnStop()
    {
        ScrollDelta = default;
        ScrolledOffset = default;
        InitialVelocity = default;
        UseTouchPadScroll = false;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        if (UseTouchPadScroll is not true)
            return ServiceAnimationMouseWheel(elapsedTime);

        return ServiceAnimationTouchPadScroll(elapsedTime);
    }

    public bool ServiceAnimationMouseWheel(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var decay = Math.Exp(-elapsedSeconds / DurationSeconds);
        var newOffset = StartOffset + InitialVelocity * DurationSeconds * (1 - decay);
        // CurrentVelocity = Math.Abs((InitialVelocity * decay).Length);
        ScrollClient.UpdateScrollTarget(newOffset);
        return elapsedSeconds <= 1;
    }

    public bool ServiceAnimationTouchPadScroll(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var progress = Math.Min(elapsedSeconds / DurationSeconds, 1.0);
        progress = TouchPadEase.GetSplineProgress(progress);
        ScrolledOffset = Vector.Multiply(ScrollDelta, progress);
        ScrollClient.UpdateScrollTarget(StartOffset + ScrolledOffset);
        return elapsedSeconds <= DurationSeconds;
    }
}
