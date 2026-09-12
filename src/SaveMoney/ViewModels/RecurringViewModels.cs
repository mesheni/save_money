using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

public partial class RecurringRowVM
{
    public required string Id { get; init; }
    public required string TitleText { get; init; }
    public required string AmountText { get; init; }
    public required bool IsExpense { get; init; }
    public required string NextDateText { get; init; }
    public required string AccountText { get; init; }
    public required bool IsActive { get; init; }
}

public partial class RecurringViewModel(AppDatabase db) : ObservableObject
{
    private readonly AppDatabase _db = db;

    public ObservableCollection<RecurringRowVM> Rows { get; } = [];

    public Task InitializeAsync()
    {
        Rows.Clear();
        var accounts = _db.Accounts.GetAll().ToDictionary(a => a.Id, a => a.Name);
        var categories = _db.Categories.GetAllActive().ToDictionary(c => c.Id);

        foreach (var r in _db.RecurringPayments.GetAllActive())
        {
            Rows.Add(new RecurringRowVM
            {
                Id = r.Id,
                TitleText = string.IsNullOrWhiteSpace(r.Payee) ? "Платёж" : r.Payee!,
                AmountText = MoneyFormat.SignedRubles(r.Kind == TransactionKind.Expense ? -r.AmountMinor : r.AmountMinor),
                IsExpense = r.Kind == TransactionKind.Expense,
                NextDateText = $"далее {DateTimeOffset.FromUnixTimeSeconds(r.NextDateUnix).ToLocalTime():d MMM}",
                AccountText = accounts.TryGetValue(r.AccountId, out var name) ? name : "(счёт удалён)",
                IsActive = r.IsActive,
            });
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Add() => Shell.Current.GoToAsync("recurringnew");

    [RelayCommand]
    private void Edit(RecurringRowVM? row)
    {
        if (row is not null)
        {
            Shell.Current.GoToAsync($"recurringnew?id={row.Id}");
        }
    }

    public async Task<bool> DeleteAsync(RecurringRowVM? row)
    {
        if (row is null)
        {
            return false;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить регулярный платёж?", "Транзакции, созданные ранее, останутся.", "Удалить", "Отмена");
        if (!confirmed)
        {
            return false;
        }

        var r = _db.RecurringPayments.Get(row.Id);
        if (r is not null)
        {
            _db.RecurringPayments.SoftDelete(r);
        }

        await InitializeAsync();
        return true;
    }
}

[QueryProperty(nameof(RecurringId), "id")]
public partial class RecurringEditViewModel(AppDatabase db) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private string? _id;
    private RecurringPayment? _editing;
    private List<string> _accountIds = [];
    private List<string> _categoryIds = [];
    private bool _loaded;

    public ObservableCollection<string> AccountOptions { get; } = [];
    public ObservableCollection<string> CategoryOptions { get; } = [];
    public ObservableCollection<string> DayOfMonthOptions { get; } =
        new(Enumerable.Range(1, 28).Select(d => d.ToString()));

    public IReadOnlyList<string> KindOptions { get; } = ["Расход", "Доход"];
    public IReadOnlyList<string> RecurrenceOptions { get; } = ["Ежемесячно", "Еженедельно", "Каждые N дней"];

    [ObservableProperty]
    public partial string Title { get; set; } = "Новый платёж";

    [ObservableProperty]
    public partial string NameText { get; set; } = "";

    [ObservableProperty]
    public partial int KindIndex { get; set; }

    [ObservableProperty]
    public partial int AccountIndex { get; set; }

    [ObservableProperty]
    public partial int CategoryIndex { get; set; }

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    [ObservableProperty]
    public partial int RecurrenceIndex { get; set; }

    [ObservableProperty]
    public partial int DayOfMonthIndex { get; set; }

    [ObservableProperty]
    public partial string IntervalDaysText { get; set; } = "14";

    [ObservableProperty]
    public partial DateTime NextDate { get; set; } = DateTime.Now.Date.AddDays(1);

    [ObservableProperty]
    public partial bool IsActive { get; set; } = true;

    public bool IsMonthly => RecurrenceIndex == 0;
    public bool IsEveryN => RecurrenceIndex == 2;

    public Func<string, string, Task>? AlertAsync { get; set; }

    public string? RecurringId
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

        foreach (var account in _db.Accounts.GetAll())
        {
            _accountIds.Add(account.Id);
            AccountOptions.Add(account.Name);
        }

        if (_id is not null)
        {
            _editing = _db.RecurringPayments.Get(_id);
        }

        if (_editing is not null)
        {
            Title = "Платёж";
            KindIndex = _editing.Kind == TransactionKind.Income ? 1 : 0;
            NameText = _editing.Payee ?? "";
            AmountText = MoneyFormat.ForInput(_editing.AmountMinor);
            RecurrenceIndex = _editing.RecurrenceType switch
            {
                "weekly" => 1,
                "every_n_days" => 2,
                _ => 0,
            };
            IntervalDaysText = Math.Max(1, _editing.IntervalDays).ToString();
            DayOfMonthIndex = Math.Clamp(_editing.DayOfMonth, 1, 28) - 1;
            NextDate = DateTimeOffset.FromUnixTimeSeconds(_editing.NextDateUnix).LocalDateTime;
            IsActive = _editing.IsActive;

            var accountIdx = _accountIds.IndexOf(_editing.AccountId);
            AccountIndex = Math.Max(0, accountIdx);
        }

        ReloadCategories(_editing?.CategoryId);

        return Task.CompletedTask;
    }

    private void ReloadCategories(string? selectId = null)
    {
        var kind = KindIndex == 1 ? CategoryKind.Income : CategoryKind.Expense;

        CategoryOptions.Clear();
        _categoryIds = [];
        CategoryOptions.Add("— без категории —");
        foreach (var root in _db.Categories.GetRoots(kind))
        {
            _categoryIds.Add(root.Id);
            CategoryOptions.Add($"{root.Icon} {root.Name}");
        }

        var newIndex = 0;
        if (selectId is not null)
        {
            var idx = _categoryIds.IndexOf(selectId);
            if (idx >= 0)
            {
                newIndex = idx + 1;
            }
        }

        CategoryIndex = newIndex;
    }

    partial void OnKindIndexChanged(int value) => ReloadCategories();

    partial void OnRecurrenceIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(IsEveryN));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (AccountIndex < 0 || AccountIndex >= _accountIds.Count)
        {
            await AlertAsync?.Invoke("Ошибка", "Выберите счёт (создайте его в разделе «Счета»)")!;
            return;
        }

        var amount = MoneyFormat.ParseInput(AmountText);
        if (amount <= 0)
        {
            await AlertAsync?.Invoke("Ошибка", "Введите сумму больше нуля")!;
            return;
        }

        var recurring = _editing ?? new RecurringPayment();
        recurring.Kind = KindIndex == 1 ? TransactionKind.Income : TransactionKind.Expense;
        recurring.AccountId = _accountIds[AccountIndex];
        recurring.CategoryId = CategoryIndex > 0 ? _categoryIds[CategoryIndex - 1] : null;
        recurring.Payee = string.IsNullOrWhiteSpace(NameText) ? null : NameText.Trim();
        recurring.AmountMinor = amount;
        recurring.RecurrenceType = RecurrenceIndex switch
        {
            1 => "weekly",
            2 => "every_n_days",
            _ => "monthly",
        };
        recurring.DayOfMonth = RecurrenceIndex == 0 ? DayOfMonthIndex + 1 : 0;
        recurring.IntervalDays = RecurrenceIndex == 2
            ? Math.Max(1, int.TryParse(IntervalDaysText, out var n) ? n : 1)
            : 0;
        recurring.NextDateUnix = new DateTimeOffset(NextDate.Date.AddHours(12),
            TimeZoneInfo.Local.GetUtcOffset(NextDate.Date)).ToUnixTimeSeconds();
        recurring.IsActive = IsActive;

        _db.RecurringPayments.Save(recurring);
        await Shell.Current.GoToAsync("..");
    }
}
