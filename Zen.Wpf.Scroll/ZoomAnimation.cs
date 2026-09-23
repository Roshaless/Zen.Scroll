namespace Zen.Scroll;

public abstract class ZoomAnimation : MotionAnimation
{
    public abstract void ZoomBy(double delta);

    public abstract void ZoomBy(double delta, double duration);

    // Absolute values in the same space as the ScrollBy delta: offsets for scroll, scale for zoom.
    public abstract void ZoomTo(double from, double to);

    public abstract void ZoomTo(double from, double to, double duration);
}
