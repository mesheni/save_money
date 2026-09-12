using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SaveMoney.Core.Database;
using SaveMoney.Core.Services;
using SaveMoney.ViewModels;
using SaveMoney.Views;

namespace SaveMoney;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "savemoney.db");
        builder.Services.AddSingleton(new AppDatabase(dbPath));
        builder.Services.AddSingleton<BalanceService>();

        builder.Services.AddTransient<QuickAddViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<ReportsViewModel>();
        builder.Services.AddTransient<MoreViewModel>();

        builder.Services.AddTransient<QuickAddPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<ReportsPage>();
        builder.Services.AddTransient<MorePage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
