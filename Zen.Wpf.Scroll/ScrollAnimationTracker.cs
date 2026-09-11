using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace Zen.Scroll;

internal sealed class ScrollAnimationTracker
{
    private static readonly Vector MinContentScale = new(0.01, 0.01);
    private static readonly Vector MaxContentScale = new(100, 100);
    private static readonly Vector UnitVector = new(1, 1);

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
    private readonly ScaleTransform ContentScaleTransform;
    private readonly TransformGroup ContentTransformGroup;
    private readonly DependencyPropertyDescriptor ContentPropertyDescriptor;

    public bool IsZoomDisabled { get; private set; }

    public ScrollBar? VerticalScrollBar { get; private set; }

    public ScrollBar? HorizontalScrollBar { get; private set; }

    public ScrollContentPresenter? ScrollContentPresenter { get; private set; }

    public FrameworkElement? ScrollContentObject { get; private set; }

    public Vector ContentScaleCenter { get; set; }

    public Vector ScrollOffset { get; private set; }

    public Vector ScrollableOffset { get; private set; }

    public Vector UnscaledScrollableOffset { get; private set; }

    public Vector ViewportSize => new(ScrollContentPresenter?.ViewportWidth ?? 0, ScrollContentPresenter?.ViewportHeight ?? 0);

    public Vector ContentScale { get; private set; } = new(1, 1);

    public Vector ContentOffset { get; private set; } = new(0, 0);

    public Vector AnimatedOffset => ScrollOffset - ContentOffset.ScaledBy(ContentScale);

    public bool CanHorizontalScroll => ContentScale.X > 1 || RootScrollViewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled;

    public bool CanVerticallyScroll => ContentScale.Y > 1 || RootScrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled;

    public bool CanZoom => !IsZoomDisabled && (CanHorizontalScroll || CanVerticallyScroll);

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
        ContentScaleTransform = new ScaleTransform(1, 1);
        ContentTransformGroup = new TransformGroup()
        {
            Children = { ContentTransform, ContentScaleTransform }
        };
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

    public void AnimateScrollBy(Vector delta)
    {
        AnimateScrollTo(AnimatedOffset + delta);
    }

    public void AnimateScrollTo(Vector offset)
    {
        if (IsInitialized is not true) return;

        var targetOffset = offset.ValidOr(ScrollOffset)
            .ConstrainedBetween(default, ScrollableOffset);

        ApplyOffsetChanged(targetOffset - ScrollOffset);
    }

    private void ApplyOffsetChanged(Vector offsetChanged)
    {
        offsetChanged = offsetChanged.ConstrainedBetween(-ScrollOffset, ScrollableOffset - ScrollOffset);

        SetContentTransform(offsetChanged);
        SetScrollBarValue(ScrollOffset.X + offsetChanged.X, ScrollOffset.Y + offsetChanged.Y);
    }

    public void ApplyAnimatedOffset()
    {
        LogicalScroll(AnimatedOffset);
    }

    public void LogicalScroll(Vector offset)
    {
        if (IsInitialized is not true) return;

        var target = offset.ValidOr(ScrollOffset)
            .ConstrainedBetween(default, UnscaledScrollableOffset);

        if (target.X != ScrollOffset.X)
        {
            RootScrollViewer.ScrollToHorizontalOffset(target.X);
        }
        if (target.Y != ScrollOffset.Y)
        {
            RootScrollViewer.ScrollToVerticalOffset(target.Y);
        }

        var animatedOffset = AnimatedOffset;
        ScrollOffset = target;
        ApplyOffsetChanged(animatedOffset - ScrollOffset);
    }

    private void SetScrollBarValue(double x, double y)
    {
        HorizontalScrollBar?.SetValue(RangeBase.ValueProperty, x);
        VerticalScrollBar?.SetValue(RangeBase.ValueProperty, y);
    }

    public void SetContentScale(Vector scale)
    {
        if (IsInitialized is not true) return;

        scale = scale.ValidOr(ContentScale)
            .ConstrainedBetween(MinContentScale, MaxContentScale);

        var oldScale = ContentScale;
        if (oldScale == scale) return;

        var scaleRatio = new Vector(scale.X / oldScale.X, scale.Y / oldScale.Y);

        // 以当前可视偏移为基准、围绕光标 ContentScaleCenter 缩放，使光标下的内容点保持不动。
        // targetOffset = AnimatedOffset + (AnimatedOffset + Center) * (scaleRatio - 1)
        //              = AnimatedOffset * scaleRatio + Center * (scaleRatio - 1)
        var animatedOffset = AnimatedOffset;
        var ratio = scaleRatio - UnitVector;
        var targetOffset = new Vector(
            animatedOffset.X * scaleRatio.X + ContentScaleCenter.X * ratio.X,
            animatedOffset.Y * scaleRatio.Y + ContentScaleCenter.Y * ratio.Y);

        ContentScaleTransform.ScaleX = scale.X;
        ContentScaleTransform.ScaleY = scale.Y;
        ContentScale = scale;

        SyncScrollableOffset();

        if (ScrollOffset.X > ScrollableOffset.X || ScrollOffset.Y > ScrollableOffset.Y)
        {
            LogicalScroll(new Vector(
                Math.Min(ScrollOffset.X, ScrollableOffset.X),
                Math.Min(ScrollOffset.Y, ScrollableOffset.Y)));
        }

        AnimateScrollTo(targetOffset);
    }

    private void SetContentTransform(Vector offsetChanged)
    {
        ContentTransform.X = -offsetChanged.X / Math.Max(ContentScaleTransform.ScaleX, MinContentScale.X);
        ContentTransform.Y = -offsetChanged.Y / Math.Max(ContentScaleTransform.ScaleY, MinContentScale.Y);
        ContentOffset = new Vector(ContentTransform.X, ContentTransform.Y);
    }

    public void SyncScrollableOffset()
    {
        if (IsInitialized is not true) return;

        SyncScrollOffset();

        var scale = ContentScale;
        var viewport = ViewportSize;

        ScrollableOffset = new Vector(
            Math.Max(0, ScrollContentPresenter.ExtentWidth * scale.X - viewport.X),
            Math.Max(0, ScrollContentPresenter.ExtentHeight * scale.Y - viewport.Y));

        UnscaledScrollableOffset = new Vector(RootScrollViewer.ScrollableWidth, RootScrollViewer.ScrollableHeight);

        if (HorizontalScrollBar.Maximum != ScrollableOffset.X)
        {
            HorizontalScrollBar.Maximum = ScrollableOffset.X;
        }
        if (VerticalScrollBar.Maximum != ScrollableOffset.Y)
        {
            VerticalScrollBar.Maximum = ScrollableOffset.Y;
        }

        SyncScrollBarVisibility();
    }

    private void SyncScrollOffset()
    {
        ScrollOffset = new Vector(RootScrollViewer.HorizontalOffset, RootScrollViewer.VerticalOffset);
    }

    private void SyncScrollBarVisibility()
    {
        static bool IsHidden(ScrollBarVisibility visibility) =>
            visibility is ScrollBarVisibility.Disabled or ScrollBarVisibility.Hidden;

        var anyAxisVisible = !IsHidden(RootScrollViewer.HorizontalScrollBarVisibility) ||
                             !IsHidden(RootScrollViewer.VerticalScrollBarVisibility);

        if (anyAxisVisible && ContentScale.X - 1d > 1e-3 && ContentScale.Y - 1d > 1e-3)
        {
            ScrollBarCommandHandler.Attach(RootScrollViewer, this);
            HorizontalScrollBar?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            VerticalScrollBar?.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
        }
        else
        {
            ScrollBarCommandHandler.Detach(RootScrollViewer);
            HorizontalScrollBar?.InvalidateProperty(UIElement.VisibilityProperty);
            VerticalScrollBar?.InvalidateProperty(UIElement.VisibilityProperty);
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (RootScrollViewer is not ScrollViewer { ActualHeight: > 0, ActualWidth: > 0 }) return;

        VerticalScrollBar = RootScrollViewer.GetElement<ScrollBar>("PART_VerticalScrollBar");
        HorizontalScrollBar = RootScrollViewer.GetElement<ScrollBar>("PART_HorizontalScrollBar");
        ScrollContentPresenter = RootScrollViewer.GetElement<ScrollContentPresenter>("PART_ScrollContentPresenter");
        if (ScrollContentPresenter is null || HorizontalScrollBar is null || VerticalScrollBar is null) return;

        // Do not enable the zoom feature on DataGrid and GridView.
        IsZoomDisabled = RootScrollViewer.TemplatedParent is DataGrid or ListView;

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

        ScrollBarCommandHandler.Detach(RootScrollViewer);

        SyncScrollBarVisibility();
        UninitializeTransforms();
        ResetTransforms();

        VerticalScrollBar = null;
        HorizontalScrollBar = null;
        ScrollContentPresenter = null;
        ScrollContentObject = null;
    }

    private void ScrollViewer_OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Handled)
            return;

        if (e.OriginalSource != RootScrollViewer)
            return;

        e.Handled = true;

        // 仅在范围相关属性（extent / viewport）变化时重算滚动范围，
        // 否则只同步实时偏移，避免做无谓的乘算与依赖属性读写。
        if (e.ExtentWidthChange != 0 || e.ExtentHeightChange != 0 || e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)
        {
            SyncScrollableOffset();
        }
        else
        {
            SyncScrollOffset();
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
        UninitializeTransforms();

        var oldContent = ScrollContentObject;
        var newContent = ScrollContentPresenter?.Content as FrameworkElement;
        if (newContent != oldContent && oldContent is not null)
        {
            oldContent.SizeChanged -= ScrollViewer_OnSizeChanged;
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

    private void ResetTransforms()
    {
        ContentTransform.X = 0;
        ContentTransform.Y = 0;
        ContentOffset = default;
        ContentScale = UnitVector;
        ContentScaleTransform.ScaleX = 1;
        ContentScaleTransform.ScaleY = 1;
        ScrollableOffset = default;
        ScrollOffset = default;
    }

    private void InitializeTransforms()
    {
        if (ScrollContentObject is not null)
        {
            ScrollContentObject.RenderTransformOrigin = new Point(0, 0);
            ScrollContentObject.RenderTransform = ContentTransformGroup;
        }
    }

    private void UninitializeTransforms()
    {
        if (ScrollContentObject is not null)
        {
            ScrollContentObject.RenderTransformOrigin = default;
            ScrollContentObject.RenderTransform = null;
        }
    }
}
