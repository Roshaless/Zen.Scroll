using System.Windows;
using System.Windows.Media;

namespace Zen.Scroll;

// Bitmap cache management for content: while scrolling/zooming, WPF composites the cached bitmap
// instead of re-rendering complex vector content every frame. The render scale must be the scale the
// content effectively appears at on screen (including zoom from outer ScrollViewers); that value is
// written once per zoom stop onto the owning ScrollViewer's ScaleProperty and flows down the tree
// through property inheritance, so every cached element re-rasterizes without any lookup.
internal static class ContentCache
{
    // Bitmap pixels = content area × RenderAtScale²; both bounds protect video memory.
    private const double MaxArea = 8_000_000d;

    private const double MaxRenderScale = 4d;

    // Below this delta, rebuilding the cache costs a re-render for no visible gain.
    private const double MinRenderScaleDelta = 0.01d;

    private static readonly DependencyProperty ScaleProperty =
        DependencyProperty.RegisterAttached("Scale", typeof(double), typeof(ContentCache),
            new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.Inherits, OnScaleChanged));

    // The inheritance change fires for the whole subtree; only marked elements carry a cache.
    private static readonly DependencyProperty IsCachedProperty =
        DependencyProperty.RegisterAttached("IsCached", typeof(bool),
            typeof(ContentCache), new PropertyMetadata(false));

    // Written by the owning (outermost) tracker only: nested ScrollViewers never zoom or write, so the
    // inherited value IS the effective scale for the whole content tree. Must write the absolute scale —
    // on the writer the property is a local value, so reading it back returns the previous write, and
    // multiplying by it would compound the zoom every time (and never come back down).
    public static void SetScale(FrameworkElement owner, double scale) => owner.SetValue(ScaleProperty, scale);

    public static void ClearScale(FrameworkElement owner) => owner.ClearValue(ScaleProperty);

    public static void Clear(FrameworkElement? content)
    {
        if (content is null) return;

        content.CacheMode = null;
        content.ClearValue(IsCachedProperty);
    }

    // Meant only for a ScrollViewer's content object: the cache rasterizes the element and its whole
    // subtree into a bitmap; on ordinary elements (buttons, cards, ...) it would just burn video memory.
    public static void Update(FrameworkElement? content)
    {
        if (content is null) return;

        var area = content.ActualWidth * content.ActualHeight;
        if (area <= 0d || area > MaxArea)
        {
            content.CacheMode = null;
            content.ClearValue(IsCachedProperty);
            return;
        }

        content.CacheMode ??= new BitmapCache();
        content.SetValue(IsCachedProperty, true);
        ApplyScale(content);
    }

    private static void ApplyScale(FrameworkElement content)
    {
        if (content.CacheMode is not BitmapCache cache) return;

        var area = content.ActualWidth * content.ActualHeight;
        if (area <= 0d) return;

        var maxScale = Math.Min(MaxRenderScale, Math.Sqrt(MaxArea / area));

        var target = Math.Max((double)content.GetValue(ScaleProperty), 1d);
        if (target > maxScale)
        {
            target = maxScale;
        }

        if (Math.Abs(cache.RenderAtScale - target) < MinRenderScaleDelta) return;

        cache.RenderAtScale = target;
    }

    private static void OnScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && (bool)element.GetValue(IsCachedProperty))
        {
            ApplyScale(element);
        }
    }
}
