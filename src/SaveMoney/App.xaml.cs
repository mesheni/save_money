using Microsoft.Extensions.DependencyInjection;
using SaveMoney.Core.Services;

namespace SaveMoney;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    protected override void OnStart()
    {
        // Создаём транзакции по наступившим регулярным платежам (идемпотентно).
        MauiProgram.Services.GetRequiredService<RecurringService>()
            .MaterializeDue(DateTimeOffset.Now.ToUnixTimeSeconds());
    }
}
