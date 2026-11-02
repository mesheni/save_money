using SaveMoney.Helpers;
using SaveMoney.Views;

namespace SaveMoney.Platforms.Android;

/// <summary>Shared whole-page transition for tab taps and push/pop navigation.</summary>
public static class ScreenMotion
{
    private static PageSlide? _slide;
    public static bool Interactive { get; set; }
    public static bool Busy => _slide is not null;

    public static int TabIndex(Page? page) => page switch
    {
        QuickAddPage => 0, HistoryPage => 1, ReportsPage => 2, MorePage => 3, _ => -1,
    };

    public static void Navigating(Shell shell, ShellNavigatingEventArgs args)
    {
        if (Interactive) return;
        Reset();
        if (!Motion.AnimationsEnabled || shell.Navigation.ModalStack.Count != 0) return;
        var routes = new[] { "quickadd", "history", "reports", "more" };
        var path = args.Target.Location.OriginalString.TrimEnd('/');
        var target = Array.FindIndex(routes, r => path.EndsWith("/" + r, StringComparison.Ordinal));
        var origin = TabIndex(shell.CurrentPage);
        var backwards = args.Source is ShellNavigationSource.Pop or ShellNavigationSource.PopToRoot
            || origin >= 0 && target >= 0 && target < origin;
        _slide = PageSlide.Capture(shell.CurrentPage, backwards ? -1 : 1);
    }

    public static async void Navigated(Shell shell)
    {
        if (Interactive || _slide is not { } slide) return;
        try
        {
            if (shell.CurrentPage is { } page && await slide.BindAsync(page))
                await slide.AnimateAsync(true);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine(ex); }
        finally
        {
            slide.Dispose();
            if (ReferenceEquals(_slide, slide)) _slide = null;
        }
    }

    public static void Reset()
    {
        _slide?.Dispose();
        _slide = null;
    }
}
