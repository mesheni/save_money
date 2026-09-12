using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using SaveMoney.Core.Database;
using SaveMoney.Core.Services;
using SaveMoney.ViewModels;
using SaveMoney.Views;

namespace SaveMoney;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = default!;

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
        builder.Services.AddSingleton<TransactionService>();
        builder.Services.AddSingleton<HistoryService>();
        builder.Services.AddSingleton<CategoryService>();
        builder.Services.AddSingleton<CycleService>();
        builder.Services.AddSingleton<RecurringService>();
        builder.Services.AddSingleton<BudgetService>();
        builder.Services.AddSingleton<DebtService>();

        builder.Services.AddTransient<QuickAddViewModel>();
        builder.Services.AddTransient<HistoryViewModel>();
        builder.Services.AddTransient<MoreViewModel>();
        builder.Services.AddTransient<AccountsViewModel>();
        builder.Services.AddTransient<AccountEditViewModel>();
        builder.Services.AddTransient<CategoriesViewModel>();
        builder.Services.AddTransient<CategoryEditViewModel>();
        builder.Services.AddTransient<DebtsViewModel>();
        builder.Services.AddTransient<DebtEditViewModel>();
        builder.Services.AddTransient<DebtDetailViewModel>();
        builder.Services.AddTransient<BudgetsViewModel>();
        builder.Services.AddTransient<BudgetEditViewModel>();
        builder.Services.AddTransient<PaydayViewModel>();
        builder.Services.AddTransient<RecurringViewModel>();
        builder.Services.AddTransient<RecurringEditViewModel>();

        builder.Services.AddTransient<QuickAddPage>();
        builder.Services.AddTransient<HistoryPage>();
        builder.Services.AddTransient<MorePage>();
        builder.Services.AddTransient<AccountsPage>();
        builder.Services.AddTransient<AccountEditPage>();
        builder.Services.AddTransient<CategoriesPage>();
        builder.Services.AddTransient<CategoryEditPage>();
        builder.Services.AddTransient<DebtsPage>();
        builder.Services.AddTransient<DebtEditPage>();
        builder.Services.AddTransient<DebtDetailPage>();
        builder.Services.AddTransient<BudgetsPage>();
        builder.Services.AddTransient<BudgetEditPage>();
        builder.Services.AddTransient<PaydayPage>();
        builder.Services.AddTransient<RecurringPage>();
        builder.Services.AddTransient<RecurringEditPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        Services = app.Services;
        return app;
    }
}
