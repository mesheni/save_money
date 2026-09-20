using Microsoft.Extensions.DependencyInjection;
using SaveMoney.Core.Database;
using SaveMoney.Core.Services;

namespace SaveMoney;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();

        // Сохранённая тема применяется до создания окна, чтобы не мигать светлой.
        var theme = MauiProgram.Services.GetRequiredService<AppDatabase>()
            .Settings.Get(SaveMoney.Core.Models.SettingKeys.Theme);
        UserAppTheme = theme switch
        {
            "light" => AppTheme.Light,
            "dark" => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        MaterializeRecurring();
    }

    protected override void OnResume()
    {
        // Android держит приложение в фоне днями: если догонять платежи только
        // при холодном старте, балансы и бюджеты молча устаревают.
        MaterializeRecurring();
    }

    private static void MaterializeRecurring()
    {
        // Создаём транзакции по наступившим регулярным платежам (идемпотентно).
        MauiProgram.Services.GetRequiredService<RecurringService>()
            .MaterializeDue(DateTimeOffset.Now.ToUnixTimeSeconds());
    }
}
