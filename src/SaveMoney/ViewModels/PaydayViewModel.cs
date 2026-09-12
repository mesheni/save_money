using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>
/// Дашборд «До зарплаты»: остаток минус предстоящие регулярные платежи, в пересчёте на день.
/// Здесь же настройки: день зарплаты и цикл (якорь + длина).
/// </summary>
public partial class PaydayViewModel(
    AppDatabase db,
    BalanceService balance,
    CycleService cycle,
    RecurringService recurring) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly BalanceService _balance = balance;
    private readonly CycleService _cycle = cycle;
    private readonly RecurringService _recurring = recurring;

    public ObservableCollection<string> PaydayDayOptions { get; } =
        new(Enumerable.Range(1, 28).Select(d => $"числа {d}"));

    [ObservableProperty]
    public partial int PaydayDayIndex { get; set; }

    [ObservableProperty]
    public partial DateTime CycleAnchorDate { get; set; } = DateTime.Now.Date.AddDays(1 - DateTime.Now.Day);

    [ObservableProperty]
    public partial string CycleDaysText { get; set; } = "30";

    [ObservableProperty]
    public partial string PaydayText { get; set; } = "";

    [ObservableProperty]
    public partial string BalanceText { get; set; } = "";

    [ObservableProperty]
    public partial string UpcomingText { get; set; } = "";

    [ObservableProperty]
    public partial string CanSpendText { get; set; } = "";

    [ObservableProperty]
    public partial string PerDayText { get; set; } = "";

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    public partial string CycleText { get; set; } = "";

    public Func<string, string, Task>? AlertAsync { get; set; }

    public Task InitializeAsync()
    {
        PaydayDayIndex = _cycle.GetPaydayDay() - 1;
        var (anchor, days) = _cycle.GetSettings();
        CycleAnchorDate = anchor;
        CycleDaysText = days.ToString();

        Recompute();
        return Task.CompletedTask;
    }

    private void Recompute()
    {
        var now = DateTime.Now;
        var nowUnix = new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUnixTimeSeconds();

        var payday = _cycle.GetNextPayday(now);
        var daysLeft = _cycle.GetDaysUntilPayday(now);
        PaydayText = daysLeft == 0
            ? "Зарплата сегодня!"
            : $"До зарплаты ({payday:d MMMM}): {daysLeft} дн.";

        var total = _balance.GetTotalBalance();
        BalanceText = $"Баланс: {MoneyFormat.Rubles(total)}";

        var paydayEndUnix = new DateTimeOffset(payday.Date.AddDays(1),
            TimeZoneInfo.Local.GetUtcOffset(payday.Date)).ToUnixTimeSeconds();
        var upcoming = _recurring.GetUpcomingBetween(nowUnix, paydayEndUnix);
        var upcomingExpense = upcoming.Where(r => r.Kind == TransactionKind.Expense).Sum(r => r.AmountMinor);
        var upcomingIncome = upcoming.Where(r => r.Kind == TransactionKind.Income).Sum(r => r.AmountMinor);

        UpcomingText = upcoming.Count == 0
            ? "Предстоящих регулярных платежей до зарплаты нет"
            : $"Предстоящие платежи: −{MoneyFormat.Rubles(upcomingExpense - upcomingIncome)} ({upcoming.Count})";

        var canSpend = total - upcomingExpense + upcomingIncome;
        CanSpendText = $"Можно тратить: {MoneyFormat.Rubles(canSpend)}";

        var perDay = canSpend / Math.Max(1, daysLeft);
        PerDayText = $"≈ {MoneyFormat.Rubles(perDay)} в день";

        StatusText = canSpend < 0
            ? "⚠ До зарплаты денег не хватит"
            : perDay < 500
                ? "Денег впритык — трать аккуратнее"
                : "✓ Хватит до зарплаты";

        var info = _cycle.GetCurrentCycle();
        CycleText = $"Текущий цикл: {info.Display} ({info.TotalDays} дн.)";
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var days = int.TryParse(CycleDaysText, out var d) ? d : 30;
        if (days < 1 || days > 365)
        {
            await AlertAsync?.Invoke("Ошибка", "Длина цикла — от 1 до 365 дней")!;
            return;
        }

        _cycle.SetPaydayDay(PaydayDayIndex + 1);
        _cycle.SetSettings(CycleAnchorDate, days);
        Recompute();
    }
}
