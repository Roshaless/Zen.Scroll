using System.Windows;
using System.Windows.Media.Animation;

namespace Zen.Scroll;

// Wheel: exponential decay x(t) = v₀·τ·(1 − e^(−t/τ)) with the initial velocity proportional
// to the distance. Touchpad: a KeySpline whose control points and duration follow the gesture
// pixel speed (see TouchPadCurve). Both dispatch per-frame displacement as deltas (see EmitOffset).
public sealed class ScrollAnimationSmooth : ScrollAnimation
{
    // Caps the initial velocity per animation so very long distances don't look like a jump.
    private const double MaxInitialVelocity = 8000;
    private readonly KeySpline TouchPadEase = new();
    private Vector StartOffset;
    private Vector DestinationOffset;
    private Vector ScrollDistance;
    private Vector InitialVelocity;
    private Vector LastEmittedOffset;
    private double DurationSeconds;
    private bool UseTouchPadScroll;

    public override void ScrollBy(Vector delta)
    {
        if (ScrollClient.IsTouchPadScroll(delta))
        {
            UseTouchPadScroll = true;
            ScrollByTouchPad(delta);
            return;
        }

        UseTouchPadScroll = false;
        ScrollByCore(delta, ScrollClient.ScrollDuration);
    }

    public override void ScrollBy(Vector delta, double duration)
    {
        UseTouchPadScroll = false;
        ScrollByCore(delta, duration);
    }

    // Programmatic: there is no wheel event behind it, so never the touchpad curve.
    public override void ScrollTo(Vector from, Vector to)
    {
        ScrollTo(from, to, ScrollClient.ScrollDuration);
    }

    public override void ScrollTo(Vector from, Vector to, double duration)
    {
        UseTouchPadScroll = false;
        ScrollToCore(from, to, duration);
    }

    private void ScrollByTouchPad(Vector delta)
    {
        var intervalMs = TimeSinceStart().TotalMilliseconds;
        var curve = TouchPadCurve.From(delta.Length, intervalMs);
        TouchPadEase.ControlPoint1 = new Point(0d, curve.ControlPoint1Y);
        TouchPadEase.ControlPoint2 = new Point(curve.ControlPoint2X, 1d);
        ScrollByCore(delta, curve.DurationMs);
    }

    // While flying, new input accumulates onto the existing destination.
    private void ScrollByCore(Vector delta, double duration)
    {
        var fromOffset = ScrollClient.CurrentOffset;
        ScrollToCore(fromOffset, (IsActive ? DestinationOffset : fromOffset) - delta, duration);
    }

    private void ScrollToCore(Vector from, Vector to, double duration)
    {
        var destinationOffset = to.ConstrainedBetween(ScrollClient.MinimumScrollOffset, ScrollClient.MaximumScrollOffset);

        if (destinationOffset != from)
        {
            StartScroll(from, destinationOffset, duration);
        }
        else if (ScrollClient.IsActive)
        {
            Stop();
        }
    }

    private void StartScroll(Vector fromOffset, Vector destinationOffset, double duration)
    {
        ScrollDistance = destinationOffset - fromOffset;
        DurationSeconds = duration / MillisecondsPerSecond;

        // The asymptote of x(t) = v₀·τ·(1 − e^(−t/τ)) equals the requested distance exactly.
        InitialVelocity = ScrollDistance / DurationSeconds;

        // When clamping the velocity, stretch the duration with it,
        // or the curve would end early and stop short of the destination.
        var initialVelocityAbs = InitialVelocity.Length;
        if (initialVelocityAbs > MaxInitialVelocity)
        {
            InitialVelocity *= MaxInitialVelocity / initialVelocityAbs;
            DurationSeconds = ScrollDistance.Length / MaxInitialVelocity;
        }

        StartOffset = fromOffset;
        DestinationOffset = destinationOffset;
        LastEmittedOffset = fromOffset;
        Start();
    }

    protected override void OnStop()
    {
        ScrollDistance = default;
        InitialVelocity = default;
        LastEmittedOffset = default;
        UseTouchPadScroll = false;
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        return UseTouchPadScroll is not true
            ? ServiceAnimationMouseWheel(elapsedTime)
            : ServiceAnimationTouchPadScroll(elapsedTime);
    }

    private bool ServiceAnimationMouseWheel(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var decay = Math.Exp(-elapsedSeconds / DurationSeconds);
        EmitOffset(StartOffset + InitialVelocity * DurationSeconds * (1 - decay));
        return elapsedSeconds <= MaxAnimationSeconds;
    }

    private bool ServiceAnimationTouchPadScroll(TimeSpan elapsedTime)
    {
        var elapsedSeconds = elapsedTime.TotalSeconds;
        var progress = Math.Min(elapsedSeconds / DurationSeconds, 1.0);
        progress = TouchPadEase.GetSplineProgress(progress);
        EmitOffset(StartOffset + ScrollDistance * progress);
        return elapsedSeconds <= DurationSeconds;
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

    // Touchpad curve parameters derived from gesture pixel speed:
    // faster → steeper start tangent, later deceleration point, shorter duration.
    private readonly struct TouchPadCurve
    {
        private const double SpeedSensitivity = 0.3d;

        private const double SlowSpeedRatio = 0.9d;

        private const double FastSpeedRatio = 0.1d;

        private const double MinSpeed = 1d;

        // Duration = this distance ÷ (speed + SpeedOffset).
        private const double BaseDistancePx = 850d;

        // Bias that keeps the slow end from inflating, making the duration monotone in speed.
        private const double SpeedOffset = 1.3d;

        private const double MaxDurationMs = 300d;

        // Never shorter than one frame.
        private const double MinDurationMs = 16d;

        private const double MaxControlPoint2X = 0.42d;

        private TouchPadCurve(double controlPoint1Y, double controlPoint2X, double durationMs)
        {
            ControlPoint1Y = controlPoint1Y;
            ControlPoint2X = controlPoint2X;
            DurationMs = durationMs;
        }

        public double ControlPoint1Y { get; }

        public double ControlPoint2X { get; }

        public double DurationMs { get; }

        public static TouchPadCurve From(double distance, double intervalMs)
        {
            var speed = intervalMs > 0d ? distance / intervalMs : 0d;

            // Falls from ~1 (slow) towards ~0 (fast), so it always stays in (Fast, Slow].
            var speedRatio = FastSpeedRatio + (SlowSpeedRatio - FastSpeedRatio) / (1d + SpeedSensitivity * speed);

            // divisor ≥ 1 so the control point can't go negative.
            var divisor = Math.Max(speed, MinSpeed);

            // A single monotonically decreasing duration curve: the old base(speedRatio)/max(speed,1)
            // rose then fell (longest, 428ms, at 1px/ms) — the medium speeds felt the stickiest and
            // the curve had a kink there. This form keeps the slow end at ≈300ms, stays within a few
            // percent of the old fast end, and is monotone throughout.
            var durationMs = Math.Clamp(BaseDistancePx / (speed + SpeedOffset), MinDurationMs, MaxDurationMs);
            return new TouchPadCurve(1d - speedRatio, Math.Min(1d - 1d / divisor, MaxControlPoint2X), durationMs);
        }
    }
}
