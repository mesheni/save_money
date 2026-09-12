using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

public partial class BudgetRowVM
{
    public required string BudgetId { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string FactText { get; init; }
    public required string PlanText { get; init; }
    public required string PercentText { get; init; }
    public required double Progress { get; init; }
    public required bool IsOver { get; init; }
}

public partial class BudgetsViewModel(AppDatabase db, BudgetService budgets, CycleService cycle) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly BudgetService _budgets = budgets;
    private readonly CycleService _cycle = cycle;

    public ObservableCollection<BudgetRowVM> Rows { get; } = [];

    [ObservableProperty]
    public partial string CycleText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasBudgets { get; set; } = true;

    public Task InitializeAsync()
    {
        var info = _cycle.GetCurrentCycle();
        CycleText = $"Цикл {info.Display} · осталось {info.DaysLeft} дн.";

        Rows.Clear();
        foreach (var row in _budgets.GetStatuses(
                     ToUnix(info.Start), ToUnix(info.End)))
        {
            Rows.Add(new BudgetRowVM
            {
                BudgetId = row.BudgetId,
                Name = $"{row.Icon} {row.Name}",
                Icon = row.Icon,
                FactText = $"{MoneyFormat.Rubles(row.FactMinor)} / {MoneyFormat.Rubles(row.PlanMinor)}",
                PlanText = row.IsOver ? "⚠ превышен" : "в рамках лимита",
                PercentText = row.PercentText,
                Progress = row.Percent / 100.0,
                IsOver = row.IsOver,
            });
        }

        HasBudgets = Rows.Count > 0;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Add() => Shell.Current.GoToAsync("budget");

    [RelayCommand]
    private void Edit(BudgetRowVM? row)
    {
        if (row is not null)
        {
            Shell.Current.GoToAsync($"budget?id={row.BudgetId}");
        }
    }

    public async Task<bool> DeleteAsync(BudgetRowVM? row)
    {
        if (row is null)
        {
            return false;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить бюджет?", $"Лимит «{row.Name}» будет удалён.", "Удалить", "Отмена");
        if (!confirmed)
        {
            return false;
        }

        var budget = _db.Budgets.Get(row.BudgetId);
        if (budget is not null)
        {
            _db.Budgets.SoftDelete(budget);
        }

        await InitializeAsync();
        return true;
    }

    public static long ToUnix(DateTime local) =>
        new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();
}

[QueryProperty(nameof(BudgetId), "id")]
public partial class BudgetEditViewModel(AppDatabase db, CycleService cycle) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly CycleService _cycle = cycle;
    private string? _id;
    private Budget? _editing;
    private List<string> _categoryIds = [];
    private bool _loaded;

    public ObservableCollection<string> CategoryOptions { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "Новый бюджет";

    [ObservableProperty]
    public partial int CategoryIndex { get; set; }

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    public Func<string, string, Task>? AlertAsync { get; set; }

    public string? BudgetId
    {
        get => _id;
        set => _id = value;
    }

    public Task InitializeAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        _loaded = true;

        var selectedId = CategoryIndex > 0 && CategoryIndex <= _categoryIds.Count
            ? _categoryIds[CategoryIndex - 1]
            : _editing?.CategoryId;

        CategoryOptions.Clear();
        _categoryIds = [];
        CategoryOptions.Add("Общий лимит (все расходы)");
        foreach (var root in _db.Categories.GetRoots(CategoryKind.Expense))
        {
            _categoryIds.Add(root.Id);
            CategoryOptions.Add($"{root.Icon} {root.Name}");
        }

        var newIndex = 0;
        if (selectedId is not null)
        {
            var idx = _categoryIds.IndexOf(selectedId);
            if (idx >= 0)
            {
                newIndex = idx + 1;
            }
        }

        CategoryIndex = newIndex;

        if (_id is null || _editing is not null)
        {
            return Task.CompletedTask;
        }

        _editing = _db.Budgets.Get(_id);
        if (_editing is not null)
        {
            Title = "Бюджет";
            AmountText = MoneyFormat.ForInput(_editing.AmountMinor);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var amount = MoneyFormat.ParseInput(AmountText);
        if (amount <= 0)
        {
            await AlertAsync?.Invoke("Ошибка", "Введите лимит больше нуля")!;
            return;
        }

        var cycleInfo = _cycle.GetCurrentCycle();
        var budget = _editing ?? new Budget
        {
            PeriodStartAnchorUnix = BudgetsViewModel.ToUnix(cycleInfo.Start),
            PeriodDays = cycleInfo.TotalDays,
        };
        budget.CategoryId = CategoryIndex > 0 ? _categoryIds[CategoryIndex - 1] : null;
        budget.AmountMinor = amount;

        _db.Budgets.Save(budget);
        await Shell.Current.GoToAsync("..");
    }
}
