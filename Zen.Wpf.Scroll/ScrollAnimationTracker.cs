using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Zen.Scroll;

internal sealed class ScrollAnimationTracker
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

    private readonly ScrollViewer RootScrollViewer;
    private readonly TranslateTransform ContentTransform;
    private readonly TranslateTransform VerticalScrollBarTransform;
    private readonly TranslateTransform HorizontalScrollBarTransform;
    private readonly DependencyPropertyDescriptor ContentPropertyDescriptor;
    private Vector ScrollBarPixelPerUnit;

    public ScrollBar? VerticalScrollBar { get; private set; }

    public ScrollBar? HorizontalScrollBar { get; private set; }

    public ScrollContentPresenter? ScrollContentPresenter { get; private set; }

    public FrameworkElement? ScrollContentObject { get; private set; }

    public Vector ScrollOffset { get; private set; }

    public Vector ScrollableOffset { get; private set; }

    public Vector AnimatedOffset => ScrollOffset + -ContentTransformOffset;

    public Vector ContentTransformOffset => new(ContentTransform.X, ContentTransform.Y);

    public bool CanHorizontalScroll => HorizontalScrollBar?.Track is Track { Thumb: not null };

    public bool CanVerticallyScroll => VerticalScrollBar?.Track is Track { Thumb: not null };

    public bool IsInitialized
    {
        [MemberNotNullWhen(true, nameof(VerticalScrollBar), nameof(HorizontalScrollBar), nameof(ScrollContentObject), nameof(ScrollContentPresenter))]
        get => VerticalScrollBar is not null && HorizontalScrollBar is not null && ScrollContentObject is not null && ScrollContentPresenter is not null;
    }

    public ScrollAnimationTracker(ScrollViewer scrollViewer)
    {
        RootScrollViewer = scrollViewer;
        ContentPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(
            ContentPresenter.ContentProperty, typeof(ScrollContentPresenter));

        ContentTransform = new TranslateTransform();
        VerticalScrollBarTransform = new TranslateTransform();
        HorizontalScrollBarTransform = new TranslateTransform();
    }

    public void AnimateScrollTo(Vector offset)
    {
        if (IsInitialized is not true) return;

        // Compute transform offsets
        var offsetChanged = offset.ConstrainedBetween(default, ScrollableOffset) - ScrollOffset;
        var scrollBarTransform = offsetChanged / ScrollBarPixelPerUnit;

        // Scroll to the absolute offset
        SetContentTransform(offsetChanged);
        SetScrollBarTransform(scrollBarTransform);
    }

    public void LogicalScroll(Vector offset)
    {
        if (!double.IsNaN(offset.X) && offset.X != ScrollOffset.X)
        {
            RootScrollViewer.ScrollToHorizontalOffset(offset.X);
        }
        if (!double.IsNaN(offset.Y) && offset.Y != ScrollOffset.Y)
        {
            RootScrollViewer.ScrollToVerticalOffset(offset.Y);
        }
    }

    public void ApplyAnimatedOffset()
    {
        LogicalScroll(AnimatedOffset);
    }

    public void EnsureInitialized()
    {
        ScrollBarPixelPerUnit = default;
        SetScrollBarTransform(default);
        SetContentTransform(default);
        SyncScrollableOffset();

        if (IsInitialized)
        {
            if (HorizontalScrollBar.Track is Track { Thumb: not null } horizontalTrack)
            {
                ScrollBarPixelPerUnit.X = ScrollableOffset.X / (horizontalTrack.ActualWidth - horizontalTrack.Thumb.ActualWidth);
            }
            if (VerticalScrollBar.Track is Track { Thumb: not null } VerticalTrack)
            {
                ScrollBarPixelPerUnit.Y = ScrollableOffset.Y / (VerticalTrack.ActualHeight - VerticalTrack.Thumb.ActualHeight);
            }
        }
    }

    public void SyncScrollableOffset()
    {
        if (IsInitialized is not true) return;

        ScrollOffset = new Vector(
            HorizontalScrollBar.Value,
            VerticalScrollBar.Value);

        ScrollableOffset = new Vector(
            Math.Max(0, ScrollContentPresenter.ExtentWidth - ScrollContentPresenter.ViewportWidth),
            Math.Max(0, ScrollContentPresenter.ExtentHeight - ScrollContentPresenter.ViewportHeight));

        HorizontalScrollBar.Maximum = ScrollableOffset.X;
        VerticalScrollBar.Maximum = ScrollableOffset.Y;
    }

    public void SetContentTransform(Vector transform)
    {
        ContentTransform.X = -transform.X;
        ContentTransform.Y = -transform.Y;
    }

    public void SetScrollBarTransform(Vector transform)
    {
        HorizontalScrollBarTransform.X = transform.X;
        VerticalScrollBarTransform.Y = transform.Y;
    }

    public void Initialize()
    {
        RootScrollViewer.Loaded += OnLoaded;
        RootScrollViewer.SizeChanged += OnLoaded;
        RootScrollViewer.Unloaded += OnUnloaded;

        OnLoaded(null, EventArgs.Empty);
    }

    public void Uninitialize()
    {
        RootScrollViewer.Loaded -= OnLoaded;
        RootScrollViewer.SizeChanged -= OnLoaded;
        RootScrollViewer.Unloaded -= OnUnloaded;

        OnUnloaded(null, EventArgs.Empty);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (RootScrollViewer is not ScrollViewer { ActualHeight: > 0, ActualWidth: > 0 }) return;

        VerticalScrollBar = RootScrollViewer.GetElement<ScrollBar>("PART_VerticalScrollBar");
        HorizontalScrollBar = RootScrollViewer.GetElement<ScrollBar>("PART_HorizontalScrollBar");
        ScrollContentPresenter = RootScrollViewer.GetElement<ScrollContentPresenter>("PART_ScrollContentPresenter");
        if (ScrollContentPresenter is null || HorizontalScrollBar is null || VerticalScrollBar is null) return;

        // Fix Default Style
        if (RootScrollViewer.GetElement("Corner") is Rectangle Corner)
            Corner.SetValue(Shape.FillProperty, Brushes.Transparent);

        // Use the SizeChanged event instead of the Loaded event to avoid triggering when the
        // ScrollViewer has loaded but the ScrollContentPresenter has not yet been measured.

        RootScrollViewer.SizeChanged -= OnLoaded;
        RootScrollViewer.SizeChanged += ScrollViewer_OnSizeChanged;
        RootScrollViewer.ScrollChanged += ScrollViewer_OnScrollChanged;
        ScrollContentPresenter.Loaded += ScrollContentPresenter_OnLoaded;
        ScrollContentPresenter.Unloaded += ScrollContentPresenter_OnUnloaded;
        ScrollContentObject = ScrollContentPresenter.Content as FrameworkElement;
        ContentPropertyDescriptor.AddValueChanged(ScrollContentPresenter, OnScrollContentChanged);
        OnScrollContentChanged(null, e);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (ScrollContentPresenter is not null)
        {
            ScrollContentPresenter.Loaded -= ScrollContentPresenter_OnLoaded;
            ScrollContentPresenter.Unloaded -= ScrollContentPresenter_OnUnloaded;
            ContentPropertyDescriptor.RemoveValueChanged(ScrollContentPresenter, OnScrollContentChanged);
        }

        // Reset the properties to their default bindings to ensure that
        // the scrolling behavior is normal when animations are not enabled.
        VerticalScrollBar?.SetBinding(RangeBase.ValueProperty, DefaultVerticalOffsetBinding);
        HorizontalScrollBar?.SetBinding(RangeBase.ValueProperty, DefaultHorizontalOffsetBinding);

        RootScrollViewer.SizeChanged -= ScrollViewer_OnSizeChanged;
        RootScrollViewer.ScrollChanged -= ScrollViewer_OnScrollChanged;
        ScrollContentObject?.SizeChanged -= ScrollViewer_OnSizeChanged;

        UninitializeTransforms();
        VerticalScrollBar = null;
        HorizontalScrollBar = null;
        ScrollContentPresenter = null;
        ScrollContentObject = null;
    }

    private void ScrollViewer_OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (e.Handled)
            return;

        e.Handled = true;
        if (e.HorizontalChange != 0)
        {
            ContentTransform.X = 0;
            HorizontalScrollBarTransform.X = 0;
            ScrollOffset = ScrollOffset.WithX(e.HorizontalOffset);
        }
        if (e.VerticalChange != 0)
        {
            ContentTransform.Y = 0;
            VerticalScrollBarTransform.Y = 0;
            ScrollOffset = ScrollOffset.WithY(e.VerticalOffset);
        }
    }

    private void ScrollViewer_OnSizeChanged(object? sender, EventArgs e)
    {
        SyncScrollableOffset();
    }

    private void ScrollContentPresenter_OnLoaded(object? sender, EventArgs e)
    {
        OnScrollContentChanged(sender, e);
    }

    private void ScrollContentPresenter_OnUnloaded(object? sender, EventArgs e)
    {
        OnScrollContentChanged(sender, e);
    }

    private void OnScrollContentChanged(object? sender, EventArgs e)
    {
        var oldContent = ScrollContentObject;
        var newContent = ScrollContentPresenter?.Content as FrameworkElement;
        if (newContent != oldContent && oldContent is not null)
        {
            oldContent.SizeChanged -= ScrollViewer_OnSizeChanged;
            oldContent.RenderTransformOrigin = default;
            oldContent.RenderTransform = null;
        }

        if (newContent is not null)
        {
            newContent.SizeChanged -= ScrollViewer_OnSizeChanged;
            newContent.SizeChanged += ScrollViewer_OnSizeChanged;
        }

        ScrollContentObject = newContent;

        SyncScrollableOffset();
        InitializeTransforms();
    }

    private void InitializeTransforms()
    {
        if (ScrollContentObject is not null)
        {
            ScrollContentObject.RenderTransformOrigin = new Point(0, 0);
            ScrollContentObject.RenderTransform = ContentTransform;
        }
        if (HorizontalScrollBar?.Track is not null)
        {
            HorizontalScrollBar.Track.RenderTransformOrigin = default;
            HorizontalScrollBar.Track.RenderTransform = HorizontalScrollBarTransform;
        }
        if (VerticalScrollBar?.Track is not null)
        {
            VerticalScrollBar.Track.RenderTransformOrigin = default;
            VerticalScrollBar.Track.RenderTransform = VerticalScrollBarTransform;
        }
    }

    private void UninitializeTransforms()
    {
        if (ScrollContentObject is not null)
        {
            ScrollContentObject.RenderTransformOrigin = default;
            ScrollContentObject.RenderTransform = null;
        }
        if (HorizontalScrollBar?.Track is not null)
        {
            HorizontalScrollBar.Track.RenderTransformOrigin = default;
            HorizontalScrollBar.Track.RenderTransform = null;
        }
        if (VerticalScrollBar?.Track is not null)
        {
            VerticalScrollBar.Track.RenderTransformOrigin = default;
            VerticalScrollBar.Track.RenderTransform = null;
        }
    }
}
