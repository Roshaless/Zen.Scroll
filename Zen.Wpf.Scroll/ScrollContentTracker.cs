using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Zen.Scroll;

internal sealed class ScrollContentTracker
{
    private readonly ScrollViewer _scrollViewer;
    private readonly DependencyPropertyDescriptor _contentPropertyDescriptor;
    private ScrollContentPresenter? _scrollContentPresenter;
    private FrameworkElement? _scrollContentObject;

    public ScrollBar? VerticalScrollBar { get; private set; }

    public ScrollBar? HorizontalScrollBar { get; private set; }

    public ScrollContentPresenter? ScrollContentPresenter => _scrollContentPresenter;

    public FrameworkElement? ScrollContentObject => _scrollContentObject;

    public bool IsInitialized
    {
        [MemberNotNullWhen(true, nameof(VerticalScrollBar), nameof(HorizontalScrollBar))]
        [MemberNotNullWhen(true, nameof(ScrollContentObject), nameof(ScrollContentPresenter))]
        get => VerticalScrollBar is not null && HorizontalScrollBar is not null &&
               ScrollContentObject is not null && ScrollContentPresenter is not null;
    }

    public event EventHandler<ScrollContentChangedEventArgs>? ScrollContentChanged;

    public ScrollContentTracker(ScrollViewer scrollViewer)
    {
        Debug.Assert(scrollViewer is not null);

        _scrollViewer = scrollViewer;
        _contentPropertyDescriptor = DependencyPropertyDescriptor.FromProperty(
            ContentPresenter.ContentProperty, typeof(ScrollContentPresenter));
    }

    public void Initialize()
    {
        _scrollViewer.SizeChanged += OnLoaded;
        _scrollViewer.Unloaded += OnUnloaded;

        OnLoaded(null, EventArgs.Empty);
    }

    public void Uninitialize()
    {
        _scrollViewer.SizeChanged -= OnLoaded;
        _scrollViewer.Unloaded -= OnUnloaded;

        OnUnloaded(_scrollViewer, EventArgs.Empty);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        if (sender is not ScrollViewer { ActualHeight: > 0, ActualWidth: > 0 } scrollViewer)
            return;

        var verticalScrollBar = scrollViewer.GetElement<ScrollBar>("PART_VerticalScrollBar");
        var horizontalScrollBar = scrollViewer.GetElement<ScrollBar>("PART_HorizontalScrollBar");
        var scrollContentPresenter = scrollViewer.GetElement<ScrollContentPresenter>("PART_ScrollContentPresenter");

        if (verticalScrollBar is null || horizontalScrollBar is null || scrollContentPresenter is null)
            return;

        scrollViewer.SizeChanged -= OnLoaded;

        VerticalScrollBar = verticalScrollBar;
        HorizontalScrollBar = horizontalScrollBar;

        _scrollContentPresenter = scrollContentPresenter;
        _scrollContentPresenter.Loaded += ScrollContentPresenter_OnLoaded;
        _scrollContentPresenter.Unloaded += ScrollContentPresenter_OnUnloaded;
        _scrollContentObject = _scrollContentPresenter.Content as FrameworkElement;

        _contentPropertyDescriptor.AddValueChanged(_scrollContentPresenter, OnScrollContentChanged);
        ScrollContentChanged?.Invoke(this, new ScrollContentChangedEventArgs(null, _scrollContentObject));
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        VerticalScrollBar = null;
        HorizontalScrollBar = null;

        if (_scrollContentPresenter is not null)
        {
            _scrollContentPresenter.Loaded -= ScrollContentPresenter_OnLoaded;
            _scrollContentPresenter.Unloaded -= ScrollContentPresenter_OnUnloaded;
            _contentPropertyDescriptor.RemoveValueChanged(_scrollContentPresenter, OnScrollContentChanged);
            ScrollContentChanged?.Invoke(this, new ScrollContentChangedEventArgs(_scrollContentObject, null));
        }

        _scrollContentPresenter = null;
        _scrollContentObject = null;
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
        if (sender is not ScrollContentPresenter contentPresenter) return;

        var oldContent = _scrollContentObject;
        var newContent = contentPresenter.Content as FrameworkElement;

        _scrollContentObject = newContent;
        ScrollContentChanged?.Invoke(this, new ScrollContentChangedEventArgs(oldContent, newContent));
    }
}
