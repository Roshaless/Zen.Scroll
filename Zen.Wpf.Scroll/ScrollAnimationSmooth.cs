using System.Windows;

namespace Zen.Scroll;

public sealed class ScrollAnimationSmooth : ScrollAnimation
{
    // Caps the initial velocity per animation so very long distances don't look like a jump.
    private const double MaxInitialVelocity = 8000;
    private const double ResponseSeconds = 0.045;
    private const double DampingRatio = 0.9;
    private const double Omega = 1d / ResponseSeconds;
    private const double TouchPadStiffness = Omega * Omega;
    private const double TouchPadDamping = 2d * DampingRatio * Omega;
    private Vector StartOffset;
    private Vector DestinationOffset;
    private Vector ScrollDistance;
    private Vector LastEmittedOffset;
    private Vector TouchVelocity;
    private TimeSpan LastElapsed;
    private double DurationSeconds;
    private bool UseTouchPadScroll;

    public override void ScrollBy(Vector delta)
    {
        ScrollByCore(delta, ScrollClient.ScrollDuration);
    }

    public override void ScrollBy(Vector delta, double duration)
    {
        ScrollByCore(delta, duration);
    }

    // Programmatic: there is no wheel event behind it, so never the touchpad curve.
    public override void ScrollTo(Vector from, Vector to)
    {
        ScrollTo(from, to, ScrollClient.ScrollDuration);
    }

    public override void ScrollTo(Vector from, Vector to, double duration)
    {
        ScrollToCore(from, to, duration);
    }

    // While flying, new input accumulates onto the existing destination.
    private void ScrollByCore(Vector delta, double duration)
    {
        var fromOffset = ScrollClient.CurrentOffset;
        UseTouchPadScroll = ScrollClient.IsTouchPadScroll(delta);
        ScrollToCore(fromOffset, (IsActive ? DestinationOffset : fromOffset) - delta, duration);
    }

    private void ScrollToCore(Vector from, Vector to, double duration)
    {
        var destinationOffset = to.ConstrainedBetween(ScrollClient.MinimumScrollOffset, ScrollClient.MaximumScrollOffset);
        if (destinationOffset != from)
        {
            StartScroll(from, destinationOffset, duration);
        }
        else if (!UseTouchPadScroll && ScrollClient.IsActive)
        {
            Stop();
        }
    }

    private void StartScroll(Vector fromOffset, Vector destinationOffset, double duration)
    {
        ScrollDistance = destinationOffset - fromOffset;
        DurationSeconds = duration / MillisecondsPerSecond;

        // The asymptote of x(t) = D·(1 − e^(−t/τ)) is the requested distance exactly, so the curve
        // starts at D/τ. A long distance would start too fast: cap that speed and stretch τ with it, or
        // the curve would end early and stop short of the destination. The curve itself only needs D, so
        // the speed is not kept.
        if (ScrollDistance.Length / DurationSeconds > MaxInitialVelocity)
        {
            DurationSeconds = ScrollDistance.Length / MaxInitialVelocity;
        }

        StartOffset = fromOffset;
        DestinationOffset = destinationOffset;
        LastEmittedOffset = fromOffset;

        // Touchpad scrolls are continuous and can be interrupted by a new gesture, so do not restart the animation.
        if (UseTouchPadScroll && IsActive)
            return;

        Start();
    }

    protected override void OnStop()
    {
        ScrollDistance = default;
        LastEmittedOffset = default;
        UseTouchPadScroll = false;
        TouchVelocity = default;
        LastElapsed = default;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        return UseTouchPadScroll is not true
            ? ServiceAnimationMouseWheel(elapsedTime)
            : ServiceAnimationTouchPadScroll(elapsedTime);
    }

    private bool ServiceAnimationMouseWheel(TimeSpan elapsedTime)
    {
        LastElapsed = elapsedTime;
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var decay = Math.Exp(-elapsedSeconds / DurationSeconds);
        EmitOffset(DestinationOffset - ScrollDistance * decay);
        return elapsedSeconds <= MaxAnimationSeconds;
    }

    private bool ServiceAnimationTouchPadScroll(TimeSpan elapsedTime)
    {
        var dt = (elapsedTime - LastElapsed).TotalSeconds;
        LastElapsed = elapsedTime;
        if (dt <= 0) return true;
        var error = DestinationOffset - LastEmittedOffset;
        TouchVelocity += (error * TouchPadStiffness - TouchVelocity * TouchPadDamping) * dt;
        EmitOffset(LastEmittedOffset + TouchVelocity * dt);
        return error.Length > 0.1d || TouchVelocity.Length > 1d;
    }

    // Dispatch this frame's displacement as a delta instead of an absolute target: the fold timer
    // zeroes ContentOffset every 40ms, and an absolute target would tear against it; a delta added
    // onto the current actual offset is idempotent and the two paths stay out of each other's way.
    private void EmitOffset(Vector offset)
    {
        var delta = offset - LastEmittedOffset;
        LastEmittedOffset = offset;
        if (delta.X == 0d && delta.Y == 0d) return;
        ScrollClient.UpdateScrollDelta(delta);
    }
}
