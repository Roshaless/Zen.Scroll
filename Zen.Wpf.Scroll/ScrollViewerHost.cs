using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Zen.Scroll;

internal sealed class ScrollViewerHost
{
    public static Binding DefaultHorizontalOffsetBinding = new()
    {
        Mode = BindingMode.OneWay,
        RelativeSource = RelativeSource.TemplatedParent,
        Path = new PropertyPath(ScrollViewer.HorizontalOffsetProperty)
    };

    public static Binding DefaultVerticalOffsetBinding = new()
    {
        Mode = BindingMode.OneWay,
        RelativeSource = RelativeSource.TemplatedParent,
        Path = new PropertyPath(ScrollViewer.VerticalOffsetProperty)
    };

    private readonly ScrollViewer _scrollViewer;
    private readonly ScrollContentTracker _tracker;

    private TranslateTransform _translateTransform;

    private Vector _minimumOffset;
    private Vector _maximumOffset;
    private Vector _currentOffset;
    private Vector _scrollableOffset;

    private Vector _minimumTransformOffset;
    private Vector _maximumTransformOffset;

    public Vector MinimumOffset => _minimumOffset;

    public Vector MaximumOffset => _maximumOffset;

    public Vector ScrollableOffset => _scrollableOffset;

    public Vector AnimatedOffset => new(
        _currentOffset.X + -_translateTransform.X,
        _currentOffset.Y + -_translateTransform.Y);

    public ScrollViewerHost(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
        _tracker = new ScrollContentTracker(_scrollViewer);
        _translateTransform = new TranslateTransform();
    }

    public void SetIsEnabled(bool isEnabled)
    {
        if (isEnabled)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _tracker.ScrollContentChanged += OnScrollContentChanged;
            _tracker.Initialize();
        }
        else
        {
            if (_tracker.IsInitialized)
            {
                _tracker.VerticalScrollBar.SetBinding(RangeBase.ValueProperty, DefaultVerticalOffsetBinding);
                _tracker.HorizontalScrollBar.SetBinding(RangeBase.ValueProperty, DefaultHorizontalOffsetBinding);
            }

            _tracker.Uninitialize();
            _translateTransform.X = 0;
            _translateTransform.Y = 0;

            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _tracker.ScrollContentChanged -= OnScrollContentChanged;
        }
    }

    private void OnScrollContentChanged(object? sender, ScrollContentChangedEventArgs e)
    {
        if (e.OldContent is { } oldContent)
        {
            oldContent.SizeChanged -= OnSizeChanged;
            oldContent.RenderTransformOrigin = default;
            oldContent.RenderTransform = null;
        }
        if (e.NewContent is { } newContent)
        {
            newContent.SizeChanged += OnSizeChanged;
            newContent.RenderTransformOrigin = new Point(0, 0);
            newContent.RenderTransform = _translateTransform;
        }
        UpdateProperties();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateProperties();
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.Handled)
            return;

        e.Handled = true;

        if (e.HorizontalChange != 0)
        {
            _translateTransform.X = 0;
            _tracker.HorizontalScrollBar?.Value = e.HorizontalOffset;
        }
        if (e.VerticalChange != 0)
        {
            _translateTransform.Y = 0;
            _tracker.VerticalScrollBar?.Value = e.VerticalOffset;
        }

        UpdateScrollOffsets(e.HorizontalOffset, e.VerticalOffset);
    }

    private void UpdateScrollOffsets(double horizontalOffset, double verticalOffset)
    {
        _currentOffset = new Vector(
            horizontalOffset, verticalOffset);

        _minimumTransformOffset = -new Vector(
            _maximumOffset.X - _currentOffset.X,
            _maximumOffset.Y - _currentOffset.Y);

        _maximumTransformOffset = new Vector(
            _currentOffset.X - _minimumOffset.X,
            _currentOffset.Y - _minimumOffset.Y);
    }

    private void UpdateProperties()
    {
        if (_tracker.IsInitialized is not true) return;

        var elementSize = _tracker.ScrollContentObject.DesiredSize;
        var containerSize = _tracker.ScrollContentPresenter.DesiredSize;

        _maximumOffset = new Vector(
            Math.Max(elementSize.Width - containerSize.Width, 0d),
            Math.Max(elementSize.Height - containerSize.Height, 0d));

        _tracker.VerticalScrollBar.Maximum = _maximumOffset.Y;
        _tracker.HorizontalScrollBar.Maximum = _maximumOffset.X;

        UpdateScrollOffsets(_scrollViewer.HorizontalOffset, _scrollViewer.VerticalOffset);
    }

    public bool CanScroll(Orientation orientation)
    {
        if (_tracker.IsInitialized is not true) return false;
        if (orientation is Orientation.Horizontal && _scrollViewer.ComputedHorizontalScrollBarVisibility is Visibility.Collapsed) return false;
        if (orientation is Orientation.Vertical && _scrollViewer.ComputedVerticalScrollBarVisibility is Visibility.Collapsed) return false;

        var elementSize = _tracker.ScrollContentObject.DesiredSize;
        var containerSize = _tracker.ScrollContentPresenter.DesiredSize;

        return orientation is Orientation.Vertical ?
            containerSize.Height < elementSize.Height :
            containerSize.Width < elementSize.Width;
    }

    private void EnsureRenderTransform(FrameworkElement element)
    {
        if (element.RenderTransform != _translateTransform)
        {
            element.RenderTransformOrigin = new Point(0, 0);
            element.RenderTransform = _translateTransform = new TranslateTransform() { X = 0, Y = 0 };
        }
    }

    public void AnimateScroll(Vector offset)
    {
        if (_tracker.IsInitialized is not true) return;

        // Compute transform offset and boundary check
        var transformOffset = new Vector(
            -(offset.X - _currentOffset.X), -(offset.Y - _currentOffset.Y))
            .ConstrainedBetween(_minimumTransformOffset, _maximumTransformOffset);

        // Scroll content offset instead of logical offset
        _translateTransform.X = transformOffset.X;
        _translateTransform.Y = transformOffset.Y;

        // Scroll to the absolute offset
        _tracker.HorizontalScrollBar.Value = offset.X;
        _tracker.VerticalScrollBar.Value = offset.Y;

        const double SmallChange = 16d;
        if (Math.Abs(transformOffset.Y) > SmallChange ||
            Math.Abs(transformOffset.X) > SmallChange)
            LogicalScroll(AnimatedOffset);
    }

    public void LogicalScroll(Vector offset)
    {
        _scrollViewer.ScrollToHorizontalOffset(offset.X);
        _scrollViewer.ScrollToVerticalOffset(offset.Y);
    }

    public void BeginAnimation()
    {
        if (_tracker.IsInitialized)
        {
            EnsureRenderTransform(_tracker.ScrollContentObject);

            // Synchronize the offset of the ScrollViewer
            _currentOffset = new Vector(_scrollViewer.HorizontalOffset, _scrollViewer.VerticalOffset);
        }
    }

    public void EndAnimation()
    {
        LogicalScroll(AnimatedOffset);
    }
}
