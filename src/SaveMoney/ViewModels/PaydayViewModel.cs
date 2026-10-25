using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>Предстоящий регулярный платеж до зарплаты.</summary>
public partial class UpcomingPaymentRow(string title, string dateText, string amountText) : ObservableObject
{
    public string Title { get; } = title;
    public string DateText { get; } = dateText;
    public string AmountText { get; } = amountText;
}

/// <summary>
/// Экран «До зарплаты» по макету: прогноз «можно тратить», прогресс цикла,
/// разбивка (остаток − платежи), предстоящие платежи, блок бюджетов и настройки.
/// </summary>
public partial class PaydayViewModel(
    AppDatabase db,
    BalanceService balance,
    CycleService cycle,
    RecurringService recurring,
    BudgetService budgets) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly BalanceService _balance = balance;
    private readonly CycleService _cycle = cycle;
    private readonly RecurringService _recurring = recurring;
    private readonly BudgetService _budgets = budgets;

    public ObservableCollection<string> PaydayDayOptions { get; } =
        new(Enumerable.Range(1, 28).Select(d => $"числа {d}"));

    [ObservableProperty]
    public partial int PaydayDayIndex { get; set; }

    [ObservableProperty]
    public partial DateTime CycleAnchorDate { get; set; } = DateTime.Now.Date.AddDays(1 - DateTime.Now.Day);

    [ObservableProperty]
    public partial string CycleDaysText { get; set; } = "30";

    // --- Прогноз ---
    [ObservableProperty]
    public partial string PaydayEndText { get; set; } = "";

    [ObservableProperty]
    public partial string CanSpendText { get; set; } = "";

    /// <summary>Отрицательный прогноз — сумма красным.</summary>
    public bool CanSpendNegative { get; private set; }

    [ObservableProperty]
    public partial string PerDayText { get; set; } = "";

    [ObservableProperty]
    public partial string DaysLeftText { get; set; } = "";

    // --- Прогресс цикла ---
    public double CycleProgress { get; private set; }

    [ObservableProperty]
    public partial string CycleStartText { get; set; } = "";

    [ObservableProperty]
    public partial string CycleEndText { get; set; } = "";

    [ObservableProperty]
    public partial string CycleProgressText { get; set; } = "";

    // --- Разбивка ---
    [ObservableProperty]
    public partial string BalanceText { get; set; } = "";

    [ObservableProperty]
    public partial string UpcomingTotalText { get; set; } = "";

    public bool HasUpcomingTotal { get; private set; }

    // --- Предстоящие платежи ---
    public ObservableCollection<UpcomingPaymentRow> UpcomingItems { get; } = [];

    public bool HasUpcomingItems => UpcomingItems.Count > 0;

    public bool HasNoUpcomingItems => UpcomingItems.Count == 0;

    // --- Бюджеты ---
    public ObservableCollection<BudgetStatusRow> BudgetItems { get; } = [];

    public bool HasBudgets => BudgetItems.Count > 0;

    public bool HasNoBudgets => BudgetItems.Count == 0;

    [ObservableProperty]
    public partial string BudgetCycleText { get; set; } = "";

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
        PaydayEndText = daysLeft == 0
            ? "Зарплата сегодня"
            : $"Можно тратить до {payday:d MMMM}";

        var paydayEndUnix = new DateTimeOffset(payday.Date.AddDays(1),
            TimeZoneInfo.Local.GetUtcOffset(payday.Date)).ToUnixTimeSeconds();
        var upcoming = _recurring.GetUpcomingBetween(nowUnix, paydayEndUnix);
        var upcomingExpense = upcoming.Where(r => r.Kind == TransactionKind.Expense).Sum(r => r.AmountMinor);
        var upcomingIncome = upcoming.Where(r => r.Kind == TransactionKind.Income).Sum(r => r.AmountMinor);

        var total = _balance.GetTotalBalance();
        var canSpend = total - upcomingExpense + upcomingIncome;
        CanSpendNegative = canSpend < 0;
        CanSpendText = MoneyFormat.Rubles(canSpend);

        var perDay = canSpend / Math.Max(1, daysLeft);
        PerDayText = $"≈ {MoneyFormat.Rubles(perDay)} в день";
        DaysLeftText = daysLeft == 0 ? "" : $"· осталось {daysLeft} дн.";

        // Прогресс цикла: сколько дней прошло из длины.
        var info = _cycle.GetCurrentCycle();
        var elapsed = (now.Date - info.Start.Date).Days;
        CycleProgress = Math.Clamp((double)elapsed / Math.Max(1, info.TotalDays), 0, 1);
        CycleStartText = info.Start.ToString("d MMMM");
        CycleEndText = info.End.AddDays(-1).ToString("d MMMM");
        CycleProgressText = elapsed <= 0
            ? $"Цикл {info.TotalDays} дн."
            : $"Прошло {elapsed} из {info.TotalDays} дн.";

        // Разбивка.
        BalanceText = MoneyFormat.Rubles(total);
        var upcomingNet = upcomingExpense - upcomingIncome;
        HasUpcomingTotal = upcomingNet != 0;
        UpcomingTotalText = HasUpcomingTotal ? MoneyFormat.SignedRubles(-upcomingNet) : MoneyFormat.Rubles(0);

        // Предстоящие платежи списком.
        UpcomingItems.Clear();
        foreach (var r in upcoming.OrderBy(r => r.NextDateUnix).Take(6))
        {
            var title = string.IsNullOrWhiteSpace(r.Payee) ? "Платёж" : r.Payee!;
            var date = DateTimeOffset.FromUnixTimeSeconds(r.NextDateUnix).ToLocalTime();
            UpcomingItems.Add(new UpcomingPaymentRow(
                title,
                date.ToString("d MMM"),
                MoneyFormat.Rubles(r.AmountMinor)));
        }
        OnPropertyChanged(nameof(HasUpcomingItems));
        OnPropertyChanged(nameof(HasNoUpcomingItems));

        // Бюджеты текущего цикла.
        var fromUnix = new DateTimeOffset(info.Start, TimeZoneInfo.Local.GetUtcOffset(info.Start)).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(info.End, TimeZoneInfo.Local.GetUtcOffset(info.End)).ToUnixTimeSeconds();
        BudgetItems.Clear();
        foreach (var row in _budgets.GetStatuses(fromUnix, endUnix).Take(3))
        {
            BudgetItems.Add(row);
        }
        BudgetCycleText = $"Бюджеты · цикл {info.Display}";
        OnPropertyChanged(nameof(HasBudgets));
        OnPropertyChanged(nameof(HasNoBudgets));
    }

    [RelayCommand]
    private void OpenBudget(BudgetStatusRow? row)
    {
        if (row is not null)
        {
            Shell.Current.GoToAsync($"budget?id={row.BudgetId}");
        }
    }

    [RelayCommand]
    private async Task AddBudgetAsync()
    {
        await Shell.Current.GoToAsync("budget");
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!int.TryParse(CycleDaysText, out var days))
        {
            await AlertAsync?.Invoke("Ошибка", "Введите целое число дней")!;
            return;
        }

        if (days < 1 || days > 365)
        {
            await AlertAsync?.Invoke("Ошибка", "Длина цикла — от 1 до 365 дней")!;
            return;
        }

        _cycle.SetPaydayDay(PaydayDayIndex + 1);
        _cycle.SetSettings(CycleAnchorDate, days);
        Recompute();
    }

    partial void OnCanSpendTextChanged(string value) => OnPropertyChanged(nameof(CanSpendNegative));
}
