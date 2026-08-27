using System.Diagnostics.CodeAnalysis;
using System.Windows;

namespace Zen.Scroll;

internal sealed class ScrollContentChangedEventArgs : EventArgs
{
    public required FrameworkElement? OldContent { get; init; }

    public required FrameworkElement? NewContent { get; init; }

    [SetsRequiredMembers]
    public ScrollContentChangedEventArgs(FrameworkElement? oldContent, FrameworkElement? newContent)
    {
        OldContent = oldContent;
        NewContent = newContent;
    }

    public ScrollContentChangedEventArgs()
    {

    }
}
