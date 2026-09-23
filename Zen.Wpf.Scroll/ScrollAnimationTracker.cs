using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Zen.Scroll;

// Scroll/zoom state of a single ScrollViewer and the visual composition of it:
// animations only submit per-frame targets; everything is folded into a content transform
// and scrollbar values here.
internal sealed class ScrollAnimationTracker
{
    private readonly ScrollViewer RootScrollViewer;
    private readonly ScrollAnimationClient Client;
    private readonly ScrollBarTakeover ScrollBarTakeover;
    private readonly MatrixTransform ContentTransform;
    private readonly DependencyPropertyDescriptor ContentPropertyDescriptor;
    private readonly DependencyPropertyDescriptor TemplatePropertyDescriptor;

    public ScrollContentPresenter? ScrollContentPresenter { get; private set; }
    public FrameworkElement? ScrollContentObject { get; private set; }

    public bool IsInitialized
    {
        [MemberNotNullWhen(true, nameof(ScrollContentObject), nameof(ScrollContentPresenter))]
        get => ScrollContentObject is not null && ScrollContentPresenter is not null;
    }

    // Gates the descendant cache refresh so a pure scroll stop doesn't walk the tree.
    private bool HasScaleChanged { get; set; }
    private bool ScrollableDirty { get; set; }
    private bool HasPendingContentScale { get; set; }
    private bool HasPendingScrollOffset { get; set; }
    private double PendingContentScale { get; set; } = UnitScale;
    private Vector PendingScrollOffset { get; set; }

    public Vector ContentOffset { get; private set; }
    public Vector ContentExtent { get; private set; }
    public Vector ContentViewport { get; private set; }
    public double ContentScale { get; private set; } = UnitScale;
    public Vector ContentScaleCenter { get; internal set; }
    public Vector ScrollOffset { get; private set; }
    public Vector ScrollableOffset { get; private set; }
    public Vector UnscaledScrollableOffset { get; private set; }

    // Visual offset = scrollbar value − contentOffset·scale; always matches what is on screen.
    public Vector AnimatedOffset => new(
        ScrollOffset.X - ContentOffset.X * ContentScale,
        ScrollOffset.Y - ContentOffset.Y * ContentScale);

    public Vector ViewportSize => new
        (ScrollContentPresenter?.ViewportWidth ?? 0,
        ScrollContentPresenter?.ViewportHeight ?? 0);

    public bool IsAnimating => Client.IsActive;

    public bool IsRootScrollViewer { get; private set; }

    public bool IsZoomDisabled { get; private set; }

    public ScrollAnimationTracker(ScrollViewer scrollViewer, ScrollAnimationClient client)
    {
        RootScrollViewer = scrollViewer;
        Client = client;
        ContentPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(
            ContentPresenter.ContentProperty, typeof(ScrollContentPresenter));
        TemplatePropertyDescriptor = DependencyPropertyDescriptor.FromProperty(
            Control.TemplateProperty, typeof(ScrollViewer));

        ContentTransform = new MatrixTransform();
        ScrollBarTakeover = new ScrollBarTakeover(scrollViewer, this);
    }

    public bool CanHorizontalScroll(double delta)
    {
        if (RootScrollViewer.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            var offsetX = AnimatedOffset.X;
            var scrollableWidth = ScrollableOffset.X;

            var epsilon = ScrollEpsilon > Math.Abs(delta) ? ScaleEpsilon : ScrollEpsilon;
            if ((delta > 0d && offsetX > 0d && !offsetX.AreClose(0d, epsilon)) ||
                (delta < 0d && offsetX < scrollableWidth && !offsetX.AreClose(scrollableWidth, epsilon)))
            {
                return true;
            }
        }

        return IsScaled(ContentScale);
    }

    public bool CanVerticalScroll(double delta)
    {
        if (RootScrollViewer.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled)
        {
            var offsetY = AnimatedOffset.Y;
            var scrollableHeight = ScrollableOffset.Y;

            var epsilon = ScrollEpsilon > Math.Abs(delta) ? ScaleEpsilon : ScrollEpsilon;
            if ((delta > 0d && offsetY > 0d && !offsetY.AreClose(0d, epsilon)) ||
                (delta < 0d && offsetY < scrollableHeight && !offsetY.AreClose(scrollableHeight, epsilon)))
            {
                return true;
            }
        }

        return IsScaled(ContentScale);
    }

    public bool CanZoom(double delta)
    {
        if (IsZoomDisabled) return false;

        if (RootScrollViewer.HorizontalScrollBarVisibility == default || RootScrollViewer.VerticalScrollBarVisibility == default)
        {
            return delta > 0 ? ContentScale.LessThan(Client.MaximumScale) : ContentScale.GreaterThan(Client.MinimumScale);
        }

        return false;
    }

    public void Initialize()
    {
        RootScrollViewer.Loaded += OnResolveRequested;
        RootScrollViewer.SizeChanged += OnResolveRequested;
        RootScrollViewer.Unloaded += OnUnloaded;

        OnResolveRequested(null, EventArgs.Empty);
    }

    public void Uninitialize()
    {
        RootScrollViewer.Loaded -= OnResolveRequested;
        RootScrollViewer.SizeChanged -= OnResolveRequested;
        RootScrollViewer.SizeChanged -= OnSizeChanged;
        RootScrollViewer.Unloaded -= OnUnloaded;

        OnUnloaded(null, EventArgs.Empty);
    }

    // Retry entry before the parts are ready; both Loaded and SizeChanged are hooked up to it.
    private void OnResolveRequested(object? sender, EventArgs e)
    {
        if (RootScrollViewer is not { ActualHeight: > 0, ActualWidth: > 0 }) return;
        if (ResolveParts() is not true) return;

        // Unsubscribe before subscribing to avoid double subscription.
        RootScrollViewer.SizeChanged -= OnResolveRequested;
        RootScrollViewer.SizeChanged -= OnSizeChanged;
        RootScrollViewer.SizeChanged += OnSizeChanged;
        RootScrollViewer.ScrollChanged -= OnScrollChanged;
        RootScrollViewer.ScrollChanged += OnScrollChanged;
        TemplatePropertyDescriptor.RemoveValueChanged(RootScrollViewer, OnTemplateChanged);
        TemplatePropertyDescriptor.AddValueChanged(RootScrollViewer, OnTemplateChanged);

        OnScrollContentChanged(null, e);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        DetachPresenter();

        RootScrollViewer.SizeChanged -= OnResolveRequested;
        RootScrollViewer.SizeChanged -= OnSizeChanged;
        RootScrollViewer.ScrollChanged -= OnScrollChanged;
        TemplatePropertyDescriptor.RemoveValueChanged(RootScrollViewer, OnTemplateChanged);
        ScrollContentObject?.SizeChanged -= OnSizeChanged;

        ScrollBarTakeover.Reset();

        UninitializeTransforms();

        // The written-out accumulated scale must be cleared too,
        // or re-attached content is rasterized with the old zoom.
        ContentCache.ClearScale(RootScrollViewer);

        ContentOffset = default;
        ContentScale = UnitScale;
        ContentScaleCenter = default;
        ContentTransform.Matrix = Matrix.Identity;

        PendingContentScale = UnitScale;
        HasPendingContentScale = false;
        HasScaleChanged = false;
        PendingScrollOffset = default;
        HasPendingScrollOffset = false;
        ScrollableDirty = false;

        ScrollableOffset = default;
        ScrollOffset = default;
        ContentExtent = default;
        ContentViewport = default;
        UnscaledScrollableOffset = default;

        IsRootScrollViewer = false;
        IsZoomDisabled = false;

        ScrollContentObject = null;
    }

    // The old presenter's subscriptions must be removed before replacing it,
    // otherwise it stays referenced and keeps firing on content changes.
    private bool ResolveParts()
    {
        var presenter = RootScrollViewer.GetElement<ScrollContentPresenter>("PART_ScrollContentPresenter");
        var verticalScrollBar = RootScrollViewer.GetElement<ScrollBar>("PART_VerticalScrollBar");
        var horizontalScrollBar = RootScrollViewer.GetElement<ScrollBar>("PART_HorizontalScrollBar");
        if (horizontalScrollBar is null || verticalScrollBar is null || presenter is null) return false;

        if (ReferenceEquals(presenter, ScrollContentPresenter) is not true)
        {
            DetachPresenter();
            ScrollContentPresenter = presenter;
            presenter.Loaded += OnScrollContentChanged;
            presenter.Unloaded += OnScrollContentChanged;
            ContentPropertyDescriptor.AddValueChanged(presenter, OnScrollContentChanged);
        }

        ScrollBarTakeover.SetParts(horizontalScrollBar, verticalScrollBar);

        // Do not enable the zoom feature on DataGrid and GridView.
        IsZoomDisabled = RootScrollViewer.TemplatedParent is DataGrid or ListView;

        IsRootScrollViewer = true;
        for (DependencyObject? parent = VisualTreeHelper.GetParent(RootScrollViewer);
             parent is not null;
             parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is ScrollViewer)
            {
                IsRootScrollViewer = false;
                break;
            }
        }

        return true;
    }

    private void DetachPresenter()
    {
        if (ScrollContentPresenter is not { } presenter) return;

        presenter.Loaded -= OnScrollContentChanged;
        presenter.Unloaded -= OnScrollContentChanged;
        ContentPropertyDescriptor.RemoveValueChanged(presenter, OnScrollContentChanged);
        ScrollContentPresenter = null;
    }

    // A template swap rebuilds every part; the old ones are already detached from the tree,
    // so re-resolve only after the new template has finished applying.
    private void OnTemplateChanged(object? sender, EventArgs e)
    {
        RootScrollViewer.Dispatcher.BeginInvoke(new Action(Reattach), DispatcherPriority.Loaded);

        void Reattach()
        {
            if (RootScrollViewer.IsLoaded is not true) return;
            if (ResolveParts() is not true) return;

            OnScrollContentChanged(null, EventArgs.Empty);
        }
    }

    public void AnimateScrollTo(Vector offset)
    {
        if (IsInitialized is not true) return;

        PendingScrollOffset = offset.ValidOr(ScrollOffset)
            .ConstrainedBetween(default, ScrollableOffset);
        HasPendingScrollOffset = true;

        if (Client.IsActive is not true)
            FlushFrame();
    }

    // Several deltas can arrive within one frame (input rate above frame rate):
    // accumulate onto the frame's pending target, or a later delta would overwrite an earlier one.
    public void ApplyScrollDelta(Vector delta) =>
        AnimateScrollTo((HasPendingScrollOffset ? PendingScrollOffset : AnimatedOffset) + delta);

    // Writes the accumulated zoom to the inherited DP once per zoom stop; every cached content
    // in the tree (including nested ScrollViewers) re-rasterizes through property inheritance.
    public void CommitContentCacheScale()
    {
        if (HasScaleChanged is not true) return;

        HasScaleChanged = false;
        ContentCache.SetScale(RootScrollViewer, ContentScale);
    }

    public void SetContentScale(double scale)
    {
        if (IsInitialized is not true) return;

        scale = scale.IsValidSize() ? scale : ContentScale;
        scale = Math.Clamp(scale, Client.MinimumScale, Client.MaximumScale);

        // Only record the pending scale: folding the anchor offset into PendingScrollOffset here
        // would clobber the frame's scroll target.
        if (scale == ContentScale) return;

        PendingContentScale = scale;
        HasPendingContentScale = true;
        HasScaleChanged = true;
        ScrollableDirty = true;

        if (Client.IsActive is not true)
            FlushFrame();
    }

    public void FlushFrame()
    {
        if (IsInitialized is not true) return;

        if (HasPendingContentScale)
        {
            var scale = ContentScale;
            var newScale = PendingContentScale;

            // Zoom anchored on the cursor: screen = scale·(p + contentOffset) − scrollOffset, so the content
            // point under the cursor is p* = (center + scrollOffset)/scale − contentOffset; scaling the
            // reciprocal-difference at that point keeps p* fixed before and after the zoom.
            var contentOffset = new Vector(
                ContentOffset.X + (ContentScaleCenter.X + ScrollOffset.X) * (1d / newScale - 1d / scale),
                ContentOffset.Y + (ContentScaleCenter.Y + ScrollOffset.Y) * (1d / newScale - 1d / scale));

            if (HasPendingScrollOffset)
            {
                // Fold this frame's scroll into the same content-offset delta:
                // ΔcontentOffset = (current visual − target visual) / newScale; an axis without distance stays 0.
                contentOffset += new Vector(
                    (AnimatedOffset.X - PendingScrollOffset.X) / newScale,
                    (AnimatedOffset.Y - PendingScrollOffset.Y) / newScale);
            }

            // Recompute the frame's visible target from the current ScrollOffset so the ContentOffset that
            // ApplyOffsetChanged rebuilds matches this frame's target (not the previous frame's).
            PendingScrollOffset = new Vector(
                ScrollOffset.X - contentOffset.X * newScale,
                ScrollOffset.Y - contentOffset.Y * newScale);
            HasPendingScrollOffset = true;

            ContentScale = newScale;
            ScrollableDirty = true;
            HasPendingContentScale = false;
        }

        if (ScrollableDirty)
        {
            UpdateScrollableRange();
            if (ScrollOffset.X.GreaterThan(ScrollableOffset.X) || ScrollOffset.Y.GreaterThan(ScrollableOffset.Y))
            {
                LogicalScroll(new Vector(
                    Math.Min(ScrollOffset.X, ScrollableOffset.X),
                    Math.Min(ScrollOffset.Y, ScrollableOffset.Y)));
            }

            ScrollableDirty = false;
        }

        if (HasPendingScrollOffset)
        {
            var targetOffset = PendingScrollOffset.ValidOr(ScrollOffset)
                .ConstrainedBetween(default, ScrollableOffset);

            ApplyOffsetChanged(targetOffset - ScrollOffset);
        }

        HasPendingScrollOffset = false;
    }

    // One MatrixTransform carries both scale and translation, keeping DP writes per frame to a minimum.
    private void ApplyOffsetChanged(Vector offsetChanged)
    {
        offsetChanged = offsetChanged.ConstrainedBetween(
            -ScrollOffset, ScrollableOffset - ScrollOffset);

        var scale = ContentScale;
        ContentOffset = new Vector(
            -offsetChanged.X / Math.Max(scale, MinDivisor.X),
            -offsetChanged.Y / Math.Max(scale, MinDivisor.Y));
        ContentTransform.Matrix = new Matrix(
            scale, 0, 0, scale,
            ContentOffset.X * scale, ContentOffset.Y * scale);

        ScrollBarTakeover.SetValue(ScrollOffset.X + offsetChanged.X, ScrollOffset.Y + offsetChanged.Y);
    }

    public void ApplyAnimatedOffset() => LogicalScroll(AnimatedOffset);

    private void LogicalScroll(Vector offset)
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

    public void SyncScrollableOffset()
    {
        if (IsInitialized is not true) return;

        SyncScrollOffset();

        // Mirror how the presenter arranges the content: max(extent, viewport), so a content smaller
        // than the viewport counts at the viewport size it is stretched to. Taking the bare extent
        // as the zoom base leaves (extent × scale − viewport) negative long after the zoomed content
        // already overflows the viewport, so no scroll range appears although it is visibly cut off.
        ContentExtent = new Vector(
            Math.Max(ScrollContentPresenter.ExtentWidth, ScrollContentPresenter.ViewportWidth),
            Math.Max(ScrollContentPresenter.ExtentHeight, ScrollContentPresenter.ViewportHeight));
        ContentViewport = new Vector(ScrollContentPresenter.ViewportWidth, ScrollContentPresenter.ViewportHeight);
        UnscaledScrollableOffset = new Vector(RootScrollViewer.ScrollableWidth, RootScrollViewer.ScrollableHeight);

        UpdateScrollableRange();
    }

    private void SyncScrollOffset()
    {
        // While animations are running, ScrollOffset is maintained by LogicalScroll; reading back the
        // ScrollViewer's async offset here would overwrite the just-committed value and accumulate drift
        // (seen as the content shifting away after zooming in and back out).
        if (Client.IsActive)
            return;

        ScrollOffset = new Vector(RootScrollViewer.HorizontalOffset, RootScrollViewer.VerticalOffset);
    }

    private void UpdateScrollableRange()
    {
        if (IsInitialized is not true) return;

        var scale = ContentScale;
        ScrollableOffset = new Vector(
            Math.Max(0, ContentExtent.X * scale - ContentViewport.X),
            Math.Max(0, ContentExtent.Y * scale - ContentViewport.Y));

        ScrollBarTakeover.SetMaximum(ScrollableOffset);
        ScrollBarTakeover.Sync(IsScaled(scale));
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Handled)
            return;

        // ScrollChanged bubbles up from nested ScrollViewers; marking it handled would swallow the app's own
        // scroll notifications too, so distinguish by OriginalSource instead.
        if (e.OriginalSource != RootScrollViewer)
            return;

        if (e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0 ||
            e.ExtentHeightChange != 0 || e.ViewportHeightChange != 0)
        {
            SyncScrollableOffset();
        }
        else
        {
            SyncScrollOffset();
        }
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        SyncScrollableOffset();
        ContentCache.Update(ScrollContentObject);
    }

    private void OnScrollContentChanged(object? sender, EventArgs e)
    {
        UninitializeTransforms();

        var oldContent = ScrollContentObject;
        var newContent = ScrollContentPresenter?.Content as FrameworkElement;
        if (newContent != oldContent && oldContent is not null)
        {
            oldContent.SizeChanged -= OnSizeChanged;
        }

        ScrollContentObject = newContent;

        if (newContent is not null)
        {
            newContent.SizeChanged -= OnSizeChanged;
            newContent.SizeChanged += OnSizeChanged;
        }

        SyncScrollableOffset();

        if (ScrollContentObject is not null)
        {
            ScrollContentObject.RenderTransformOrigin = new Point(0, 0);
            ScrollContentObject.RenderTransform = ContentTransform;
            ContentCache.Update(ScrollContentObject);
        }
    }

    private void UninitializeTransforms()
    {
        if (ScrollContentObject is not null)
        {
            ScrollContentObject.RenderTransformOrigin = default;
            ScrollContentObject.RenderTransform = null;
            ContentCache.Clear(ScrollContentObject);
        }
    }

    private const double ScaleEpsilon = 1e-3;
    private const double ScrollEpsilon = 1.6e1;
    // The unscaled identity, not the configured lower bound: content starts at 1x no matter how far
    // out the caller allows zooming.
    private static readonly double UnitScale = 1.0d;
    private static readonly Vector MinDivisor = new(0.01, 0.01);
    private static bool IsScaled(double scale) => scale - 1d > ScaleEpsilon;
}
