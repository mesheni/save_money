using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>Пункт меню «Ещё»: заголовок, живой подпись (состояние раздела) и цвет подписи.</summary>
public partial class MoreMenuItem(string iconKey, string title, string route, bool isEnabled = true) : ObservableObject
{
    public string IconKey { get; } = iconKey;
    public string Title { get; } = title;
    public string Route { get; } = route;
    public bool IsEnabled { get; } = isEnabled;

    [ObservableProperty]
    public partial string Subtitle { get; set; } = "";

    /// <summary>Красный подпись — тревога (превышенные лимиты, просрочка).</summary>
    [ObservableProperty]
    public partial bool IsAlert { get; set; }
}

public partial class MoreViewModel : ObservableObject
{
    private readonly AppDatabase _db;
    private readonly BalanceService _balance;
    private readonly CycleService _cycle;
    private readonly RecurringService _recurring;
    private readonly BudgetService _budgets;

    public ObservableCollection<MoreMenuItem> MoneyItems { get; } =
    [
        new(IconKeys.Wallet, "Счета", "accounts"),
        new(IconKeys.Category, "Категории", "categories"),
        new(IconKeys.Debts, "Долги", "debts"),
    ];

    public ObservableCollection<MoreMenuItem> PlanningItems { get; } =
    [
        new(IconKeys.Payday, "До зарплаты", "payday"),
        new(IconKeys.Budgets, "Бюджеты", "budgets"),
        new(IconKeys.Recurring, "Регулярные платежи", "recurring"),
    ];

    public ObservableCollection<MoreMenuItem> AppItems { get; } =
    [
        new(IconKeys.Settings, "Настройки и экспорт", "settings"),
        new(IconKeys.Family, "Синхронизация и семья", "sync", isEnabled: false),
    ];

    public MoreViewModel(AppDatabase db, BalanceService balance, CycleService cycle,
        RecurringService recurring, BudgetService budgets)
    {
        _db = db;
        _balance = balance;
        _cycle = cycle;
        _recurring = recurring;
        _budgets = budgets;

        var sync = AppItems[1];
        sync.Subtitle = "Появится в версии 2.0";
        AppItems[0].Subtitle = "Тема, цикл, CSV, резервная копия";
        MoneyItems[1].Subtitle = "Свои иконки и подкатегории";
    }

    public string VersionText =>
        $"SaveMoney {AppInfo.VersionString}";

    /// <summary>Пересобрать живые подписи (Appearing и после операций).</summary>
    public void RefreshSubtitles()
    {
        var accounts = _db.Accounts.GetAllActive();
        MoneyItems[0].Subtitle =
            $"{accounts.Count} {Plural(accounts.Count, "счёт", "счёта", "счетов")} · {MoneyFormat.Rubles(_balance.GetTotalBalance())}";

        MoneyItems[2].Subtitle = BuildDebtsSubtitle();

        PlanningItems[0].Subtitle = BuildPaydaySubtitle();
        PlanningItems[1].Subtitle = BuildBudgetsSubtitle();
        PlanningItems[2].Subtitle = BuildRecurringSubtitle();
    }

    private string BuildDebtsSubtitle()
    {
        var debts = _db.Debts.GetActive();
        if (debts.Count == 0)
        {
            return "Нет открытых долгов";
        }

        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        var owedToMe = debts.Where(d => d.Direction == DebtDirection.OwedToMe)
            .Sum(d => _db.Debts.GetRemaining(d));
        var hasOverdue = debts.Any(d => d.DueDateUnix is > 0 && d.DueDateUnix < now);

        var text = owedToMe > 0
            ? $"Мне должны {MoneyFormat.Rubles(owedToMe)}"
            : $"{debts.Count} {Plural(debts.Count, "долг", "долга", "долгов")} к погашению";
        return hasOverdue ? $"{text} · есть просрочка" : text;
    }

    private string BuildPaydaySubtitle()
    {
        if (_db.Settings.GetLong(SettingKeys.CycleAnchorUnix, 0) == 0)
        {
            return "Настройте день зарплаты";
        }

        var (perDay, daysLeft) = PaydayForecast();
        return $"{MoneyFormat.Rubles(perDay)} в день · {daysLeft} {Plural(daysLeft, "день", "дня", "дней")}";
    }

    private string BuildBudgetsSubtitle()
    {
        var cycle = _cycle.GetCurrentCycle();
        var fromUnix = new DateTimeOffset(cycle.Start, TimeZoneInfo.Local.GetUtcOffset(cycle.Start)).ToUnixTimeSeconds();
        var endUnix = new DateTimeOffset(cycle.End, TimeZoneInfo.Local.GetUtcOffset(cycle.End)).ToUnixTimeSeconds();
        var statuses = _budgets.GetStatuses(fromUnix, endUnix);

        if (statuses.Count == 0)
        {
            return "Лимиты не заданы";
        }

        var over = statuses.Count(s => s.IsOver);
        return over > 0
            ? $"Превышено {over} {Plural(over, "лимит", "лимита", "лимитов")} из {statuses.Count}"
            : $"{statuses.Count} {Plural(statuses.Count, "лимит", "лимита", "лимитов")} · всё в рамках";
    }

    private string BuildRecurringSubtitle()
    {
        var active = _db.RecurringPayments.GetAllActive();
        if (active.Count == 0)
        {
            return "Платежей нет";
        }

        var next = active.Min(p => p.NextDateUnix);
        var nextDate = DateTimeOffset.FromUnixTimeSeconds(next).ToLocalTime();
        return $"{active.Count} {Plural(active.Count, "платёж", "платежа", "платежей")} · ближайший {nextDate:d MMMM}";
    }

    private (long PerDay, int DaysLeft) PaydayForecast()
    {
        var now = DateTime.Now;
        var nowUnix = new DateTimeOffset(now, TimeZoneInfo.Local.GetUtcOffset(now)).ToUnixTimeSeconds();
        var payday = _cycle.GetNextPayday(now);
        var daysLeft = Math.Max(1, _cycle.GetDaysUntilPayday(now));
        var paydayEndUnix = new DateTimeOffset(payday.Date.AddDays(1),
            TimeZoneInfo.Local.GetUtcOffset(payday.Date)).ToUnixTimeSeconds();
        var upcoming = _recurring.GetUpcomingBetween(nowUnix, paydayEndUnix);
        var canSpend = _balance.GetTotalBalance()
            - upcoming.Where(r => r.Kind == TransactionKind.Expense).Sum(r => r.AmountMinor)
            + upcoming.Where(r => r.Kind == TransactionKind.Income).Sum(r => r.AmountMinor);
        return (canSpend / daysLeft, daysLeft);
    }

    private static string Plural(int n, string one, string few, string many) => n % 10 == 1 && n % 100 != 11
        ? one
        : n % 10 is >= 2 and <= 4 && (n % 100 < 10 || n % 100 >= 20) ? few : many;

    [RelayCommand]
    private void Open(MoreMenuItem? item)
    {
        if (item is { IsEnabled: true })
        {
            Shell.Current.GoToAsync(item.Route);
        }
    }
}
