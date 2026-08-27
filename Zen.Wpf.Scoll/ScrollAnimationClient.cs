using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Zen.Scroll;

public abstract class ScrollAnimationClient
{
    private readonly ScrollViewer _scrollViewer;
    private readonly ScrollViewerHost _scrollViewerHost;
    private ScrollAnimation? _animation;
    private bool _isActive;
    private bool _isPaused;

    public bool IsActive => _isActive;

    public bool IsPaused => _isPaused;

    public Vector MinimumScrollOffset => _scrollViewerHost.MinimumOffset;

    public Vector MaximumScrollOffset => _scrollViewerHost.MaximumOffset;

    public Vector CurrentOffset => _scrollViewerHost.AnimatedOffset;

    public ScrollViewer ScrollViewer => _scrollViewer;

    private bool _isEnabled;

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_HandlesMouseWheelScrolling")]
    private static extern void SetHandlesMouseWheelScrolling(ScrollViewer scrollViewer, bool value);

    public ScrollAnimationClient(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
        _scrollViewerHost = new ScrollViewerHost(_scrollViewer);
    }

    public void SetIsEnabled(bool isEnabled)
    {
        _isEnabled = isEnabled;
        if (isEnabled)
        {
            _scrollViewer.MouseWheel += OnPreviewMouseWheel;
            SetHandlesMouseWheelScrolling(_scrollViewer, false);
        }
        else
        {
            _scrollViewer.MouseWheel -= OnPreviewMouseWheel;
            SetHandlesMouseWheelScrolling(_scrollViewer, true);
        }

        _scrollViewerHost.SetIsEnabled(isEnabled);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers is ModifierKeys.Shift && _scrollViewerHost.CanScroll(Orientation.Horizontal))
        {
            OnMouseWheel(sender, new(e, new Vector(e.Delta, 0), Orientation.Horizontal));
            return;
        }

        if (_scrollViewerHost.CanScroll(Orientation.Vertical))
        {
            OnMouseWheel(sender, new(e, new Vector(0, e.Delta), Orientation.Vertical));
            return;
        }
    }

    protected abstract void OnMouseWheel(object sender, ScrollAnimationMouseWheelEventArgs e);

    public void ScrollToOffset(Vector offset)
    {
        _scrollViewerHost.AnimateScroll(offset);
    }

    public void Start(ScrollAnimation animation)
    {
        if (!_isActive)
        {
            _isActive = true;
            _isPaused = false;
            _animation = animation;
            _scrollViewerHost.BeginAnimation();
            CompositionTarget.Rendering += OnRendering;
        }
        else
        {
            if (_isPaused)
            {
                if (_animation == animation)
                {
                    _isPaused = false;
                }
            }
        }
    }

    public void Stop()
    {
        if (_isActive)
        {
            _isActive = false;
            _isPaused = false;
            _animation = null;
            _scrollViewerHost.EndAnimation();
            CompositionTarget.Rendering -= OnRendering;
        }
    }

    public void Pause()
    {
        if (_isActive && !_isPaused)
        {
            _isPaused = true;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        var animation = _animation;
        if (animation is null)
            return;

        if (!_isActive || _isPaused)
            return;

        if (!animation!.ServiceAnimation(animation.TimeSinceStart()))
            animation!.Stop();
    }
}

public class ScrollAnimationMouseWheelEventArgs(MouseWheelEventArgs args, Vector delta, Orientation orientation) : EventArgs
{
    public MouseWheelEventArgs Args { get; set; } = args;

    public Vector Delta { get; set; } = delta;

    public Orientation Orientation { get; set; } = orientation;

    public bool Handled
    {
        get => Args.Handled;
        set => Args.Handled = value;
    }
}
