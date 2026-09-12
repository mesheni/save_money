using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and our project templates, see: http://aka.ms/winui-project-info.

namespace SaveMoney.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    private static readonly string CrashLogPath =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "savemoney-crash.log");

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        this.InitializeComponent();
        this.UnhandledException += (sender, e) =>
        {
            try
            {
                System.IO.File.AppendAllText(CrashLogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] {e.Exception}\n{e.Exception.StackTrace}\n\n");
            }
            catch
            {
                // логирование не должно ронять приложение повторно
            }
        };
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
