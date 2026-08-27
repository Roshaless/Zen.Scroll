using System.Windows;
using System.Windows.Input;


namespace Zen.Scroll;

internal class ScrollAnimationSmooth(ScrollAnimationClient scrollClient) : ScrollAnimation(scrollClient)
{
    private const double MaxDuration = 1000d;
    private const double MinDuration = 16d;
    private const double FastDuration = 167d;
    private const double DefaultDuration = 200d;

    private readonly UnitBezier _bezier = new(0, 1, 0, 1);
    private Vector _startOffset;
    private Vector _destinationOffset;
    private Vector _scrolledOffset;
    private Vector _scrollDelta;
    private Vector _scrollVelocity;
    private Vector _lastMouseWheelDelta;
    private double _duration;

    public override double Duration => _duration;

    public void ScrollBy(Vector delta)
    {
        Scroll(delta);
    }

    public void ScrollToDestination(Vector fromOffset, Vector destinationOffset)
    {
        Scroll(fromOffset, destinationOffset);
    }

    public void ScrollWithWheelDelta(Vector delta)
    {
        static bool IsSnapScroll(Vector value) =>
            value.X % Mouse.MouseWheelDeltaForOneLine != 0 ||
            value.Y % Mouse.MouseWheelDeltaForOneLine != 0;


        if (IsSnapScroll(delta))
        {
            var currentVelocity = Vector.Divide(delta, TimeSinceStart().TotalMilliseconds);
            var speedRadio = Math.Clamp(0.1 + (1 - 0.2) / (1.0 + 0.3 * currentVelocity.Length), 0, 1);
            var scalar = Math.Max(Math.Abs(currentVelocity.Length), 1d);

            _bezier.SetParameters(0, 1 - speedRadio, Math.Clamp(1 - 1 / scalar, 0, 0.42), 1);
            Scroll(delta, Math.Clamp((MaxDuration - ((MaxDuration - Mouse.MouseWheelDeltaForOneLine) * speedRadio)) / scalar, MinDuration, MaxDuration));
        }
        else
        {
            _lastMouseWheelDelta = delta;
            Scroll(delta);
        }
    }

    private void Scroll(Vector delta)
    {
        if (delta.Length != 0)
        {
            var isActive = IsActive;
            var fromOffset = ScrollClient.CurrentOffset;
            var destinationOffset = isActive ? _destinationOffset - delta : fromOffset - delta;

            Scroll(fromOffset, destinationOffset);
        }
    }

    private void Scroll(Vector delta, double duration)
    {
        if (delta.Length != 0)
        {
            var isActive = IsActive;
            var fromOffset = ScrollClient.CurrentOffset;
            var destinationOffset = isActive ? _destinationOffset - delta : fromOffset - delta;

            PrivateScroll(fromOffset, destinationOffset, duration);
        }
    }


    private void Scroll(Vector fromOffset, Vector destinationOffset)
    {
        var isActive = IsActive;
        if (isActive)
        {

            var state = Math.Min(Vector.Divide(_scrolledOffset, _scrollDelta.Length).Length, 1);
            _bezier.SetParameters(0.42d * (1d - state), 0d, 0.52d + 0.22 * state, 1d);
            PrivateScroll(fromOffset, destinationOffset, FastDuration);
        }
        else
        {
            _bezier.SetParameters(0.42d, 0d, 0.58d, 1d);
            PrivateScroll(fromOffset, destinationOffset, DefaultDuration);
        }
    }

    private void PrivateScroll(Vector fromOffset, Vector destinationOffset, double duration)
    {
        var isActive = IsActive;
        if (isActive)
            Pause();

        _startOffset = fromOffset;
        _destinationOffset = destinationOffset.ConstrainedBetween(
            ScrollClient.MinimumScrollOffset, ScrollClient.MaximumScrollOffset);

        if (_startOffset == _destinationOffset)
            return;

        _duration = duration;
        if (_duration <= 0d)
            return;

        _scrolledOffset = default;
        _scrollDelta = _destinationOffset - _startOffset;

        var velocity = Vector.Divide(_scrollDelta, TimeSinceStart().TotalMilliseconds);
        _scrollVelocity = new Vector(_scrollVelocity.X + Math.Abs(velocity.X), _scrollVelocity.Y + Math.Abs(velocity.Y));

        ScrollClient.ScrollToOffset(_startOffset);
        Start();
    }

    protected override void OnStop()
    {
        _scrollVelocity = new Vector(-1500, -1500);
    }

    public override bool ServiceAnimation(TimeSpan elapsedTime)
    {
        var elapsedMs = elapsedTime.TotalMilliseconds;
        var progress = Math.Min(elapsedMs / _duration, 1.0);
        progress = _bezier.Solve(progress, elapsedMs);

        var dx = (progress * _scrollDelta.X);
        var dy = (progress * _scrollDelta.Y);
        var cur = ScrollClient.CurrentOffset;

        ScrollClient.ScrollToOffset(new Vector(
            cur.X + dx - _scrolledOffset.X,
            cur.Y + dy - _scrolledOffset.Y));

        _scrolledOffset = new(dx, dy);
        return elapsedMs <= _duration;
    }
}
