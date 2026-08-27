using System.Windows.Controls;
using System.Windows.Input;

namespace Zen.Scroll;

internal class ScrollAnimationController : ScrollAnimationClient
{
    private readonly ScrollAnimationSmooth _scrollAnimation;

    public ScrollAnimationController(ScrollViewer scrollViewer) : base(scrollViewer)
    {
        _scrollAnimation = new ScrollAnimationSmooth(this);
        ScrollViewer.SetCanContentScroll(scrollViewer, false);
        VirtualizingPanel.SetScrollUnit(scrollViewer, ScrollUnit.Pixel);
        VirtualizingPanel.SetVirtualizationMode(scrollViewer, VirtualizationMode.Recycling);
    }

    protected override void OnMouseWheel(object sender, ScrollAnimationMouseWheelEventArgs e)
    {
        if (e.Handled)
            return;

        e.Handled = true;
        _scrollAnimation.ScrollWithWheelDelta(e.Delta);
    }
}
