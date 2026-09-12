using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>Строка истории для отображения.</summary>
public partial class HistoryItemVM : ObservableObject
{
    public required string Id { get; init; }
    public required string Icon { get; init; }
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public required string AmountText { get; init; }
    public required bool IsExpense { get; init; }
}

/// <summary>Группа дня — сама коллекция строк, чтобы использовать сгруппированный CollectionView.</summary>
public partial class HistoryGroupVM : ObservableCollection<HistoryItemVM>
{
    public HistoryGroupVM(IEnumerable<HistoryItemVM> items) : base(items)
    {
    }

    public required DateTime Date { get; init; }

    public string HeaderText => Date.Date == DateTime.Today ? "Сегодня"
        : Date.Date == DateTime.Today.AddDays(-1) ? "Вчера"
        : Date.ToString("d MMMM");

    public string IncomeText => IncomeMinor > 0 ? $"+{MoneyFormat.Rubles(IncomeMinor)}" : "";
    public string ExpenseText => ExpenseMinor > 0 ? MoneyFormat.Rubles(-ExpenseMinor) : "";

    public long IncomeMinor { get; init; }
    public long ExpenseMinor { get; init; }
}

public partial class HistoryViewModel(AppDatabase db, HistoryService history) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly HistoryService _history = history;
    private readonly List<string> _accountIds = [];
    private bool _loaded;

    public ObservableCollection<HistoryGroupVM> Groups { get; } = [];

    public ObservableCollection<string> AccountOptions { get; } = ["Все счета"];

    public IReadOnlyList<string> PeriodOptions { get; } = ["Неделя", "Месяц", "3 месяца", "Всё"];

    [ObservableProperty]
    public partial int PeriodIndex { get; set; } = 1;

    [ObservableProperty]
    public partial int AccountIndex { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasItems { get; set; } = true;

    public async Task InitializeAsync()
    {
        var selectedId = AccountIndex > 0 && AccountIndex <= _accountIds.Count ? _accountIds[AccountIndex - 1] : null;

        _accountIds.Clear();
        AccountOptions.Clear();
        AccountOptions.Add("Все счета");
        foreach (var account in _db.Accounts.GetAll())
        {
            _accountIds.Add(account.Id);
            AccountOptions.Add(account.Name);
        }

        var newIndex = 0;
        if (selectedId is not null)
        {
            var idx = _accountIds.IndexOf(selectedId);
            if (idx >= 0)
            {
                newIndex = idx + 1;
            }
        }

        AccountIndex = newIndex;
        _loaded = true;
        Refresh();
    }

    [RelayCommand]
    public async Task DeleteAsync(HistoryItemVM? item)
    {
        if (item is null)
        {
            return;
        }

        var tx = _db.Transactions.Get(item.Id);
        if (tx is not null)
        {
            _db.Transactions.SoftDelete(tx);
        }

        await RefreshFromServiceAsync();
    }

    [RelayCommand]
    private void Edit(HistoryItemVM? item)
    {
        if (item is null)
        {
            return;
        }

        Shell.Current.GoToAsync($"transaction?id={item.Id}");
    }

    partial void OnPeriodIndexChanged(int value) => Refresh();
    partial void OnAccountIndexChanged(int value) => Refresh();

    private void Refresh()
    {
        if (!_loaded)
        {
            return;
        }

        var to = DateTimeOffset.Now.AddDays(1).ToUnixTimeSeconds();
        var fromLocal = PeriodIndex switch
        {
            0 => DateTimeOffset.Now.AddDays(-7),
            1 => DateTimeOffset.Now.AddMonths(-1),
            2 => DateTimeOffset.Now.AddMonths(-3),
            _ => DateTimeOffset.Now.AddYears(-10)
        };
        var from = fromLocal.ToUnixTimeSeconds();

        var accountId = AccountIndex > 0 && AccountIndex <= _accountIds.Count ? _accountIds[AccountIndex - 1] : null;
        var groups = _history.GetGroups(from, to, accountId);

        Groups.Clear();
        long income = 0, expense = 0;
        foreach (var g in groups)
        {
            income += g.IncomeMinor;
            expense += g.ExpenseMinor;
            Groups.Add(new HistoryGroupVM(g.Items.Select(i => new HistoryItemVM
            {
                Id = i.Id,
                Icon = i.Icon,
                Title = i.Title,
                Subtitle = i.Subtitle,
                AmountText = MoneyFormat.SignedRubles(i.SignedAmountMinor),
                IsExpense = i.IsExpense,
            }))
            {
                Date = g.Date,
                IncomeMinor = g.IncomeMinor,
                ExpenseMinor = g.ExpenseMinor,
            });
        }

        HasItems = groups.Count > 0;
        SummaryText = HasItems
            ? $"Доход {MoneyFormat.Rubles(income)} · Расход {MoneyFormat.Rubles(expense)}"
            : "";
    }

    private Task RefreshFromServiceAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }
}
