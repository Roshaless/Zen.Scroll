using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Zen.Scroll;

public abstract class MotionAnimation
{
    // Exponential decay never truly reaches zero; use this as the common stop condition.
    protected const double MaxAnimationSeconds = 1d;

    protected const double MillisecondsPerSecond = 1000d;

    internal ScrollAnimationClient? InternalScrollClient;

    protected ScrollAnimationClient ScrollClient => InternalScrollClient ?? throw new InvalidOperationException("ScrollClient is not set.");

    public bool IsActive { get; private set; }

    public long StartTimestamp { get; private set; }

    [MemberNotNullWhen(true, nameof(InternalScrollClient))]
    public bool CheckAccess() => InternalScrollClient is not null;

    public void Start()
    {
        if (CheckAccess())
        {
            IsActive = true;
            StartTimestamp = Stopwatch.GetTimestamp();
            ScrollClient.Start(this);
            OnStart();
        }
    }

    public void Stop()
    {
        if (IsActive is not true)
            return;

        if (CheckAccess())
        {
            IsActive = false;
            ScrollClient.Stop(this);
            OnStop();
        }
    }

    protected virtual void OnStart() { }

    protected virtual void OnStop() { }

    // Returns false when the animation has finished; the caller is responsible for Stop().
    public abstract bool ServiceAnimation(TimeSpan elapsedTime);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TimeSpan TimeSinceStart() => Stopwatch.GetElapsedTime(StartTimestamp);
}