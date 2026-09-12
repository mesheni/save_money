using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

public partial class DebtRowVM
{
    public required string Id { get; init; }
    public required string PersonName { get; init; }
    public required string Icon { get; init; }
    public required string Subtitle { get; init; }
    public required string RemainingText { get; init; }
    public required string TotalText { get; init; }
    public required bool IsOverdue { get; init; }
    public required double Progress { get; init; }
}

public partial class DebtsViewModel(AppDatabase db) : ObservableObject
{
    private readonly AppDatabase _db = db;

    public ObservableCollection<DebtRowVM> Rows { get; } = [];

    public IReadOnlyList<string> DirectionOptions { get; } = ["Мне должны", "Я должен"];

    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    private string Direction => DirectionIndex == 1 ? DebtDirection.IOwe : DebtDirection.OwedToMe;

    public Task InitializeAsync()
    {
        Reload();
        return Task.CompletedTask;
    }

    private void Reload()
    {
        Rows.Clear();
        long total = 0;

        foreach (var debt in _db.Debts.GetActive().Where(d => d.Direction == Direction))
        {
            var remaining = _db.Debts.GetRemaining(debt);
            total += remaining;

            var overdue = debt.DueDateUnix is { } due && due < DateTimeOffset.Now.ToUnixTimeSeconds();
            var dueText = debt.DueDateUnix is { } d
                ? overdue
                    ? $"⚠ просрочено ({DateTimeOffset.FromUnixTimeSeconds(d).ToLocalTime():d MMM})"
                    : $"до {DateTimeOffset.FromUnixTimeSeconds(d).ToLocalTime():d MMM}"
                : "";

            Rows.Add(new DebtRowVM
            {
                Id = debt.Id,
                PersonName = debt.PersonName,
                Icon = DirectionIndex == 1 ? "📤" : "📥",
                Subtitle = dueText,
                RemainingText = MoneyFormat.Rubles(remaining),
                TotalText = $"из {MoneyFormat.Rubles(debt.AmountMinor)}",
                IsOverdue = overdue,
                Progress = debt.AmountMinor > 0
                    ? Math.Min(1.0, (double)(debt.AmountMinor - remaining) / debt.AmountMinor)
                    : 0,
            });
        }

        SummaryText = DirectionIndex == 1
            ? $"Я должен: {MoneyFormat.Rubles(total)}"
            : $"Мне должны: {MoneyFormat.Rubles(total)}";
    }

    partial void OnDirectionIndexChanged(int value) => Reload();

    [RelayCommand]
    private void Add() =>
        Shell.Current.GoToAsync($"debtnew?dir={(DirectionIndex == 1 ? DebtDirection.IOwe : DebtDirection.OwedToMe)}");

    [RelayCommand]
    private void Open(DebtRowVM? row)
    {
        if (row is not null)
        {
            Shell.Current.GoToAsync($"debt?id={row.Id}");
        }
    }

    public async Task<bool> DeleteAsync(DebtRowVM? row)
    {
        if (row is null)
        {
            return false;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить долг?", $"Долг «{row.PersonName}» и его платежи будут удалены.", "Удалить", "Отмена");
        if (!confirmed)
        {
            return false;
        }

        var debt = _db.Debts.Get(row.Id);
        if (debt is null)
        {
            return false;
        }

        foreach (var payment in _db.Debts.GetPayments(debt.Id))
        {
            _db.Debts.SoftDeletePayment(payment);
        }

        _db.Debts.SoftDelete(debt);
        Reload();
        return true;
    }
}

[QueryProperty(nameof(Dir), "dir")]
[QueryProperty(nameof(DebtId), "id")]
public partial class DebtEditViewModel(AppDatabase db) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private string? _id;
    private Debt? _editing;
    private bool _loaded;

    public ObservableCollection<string> AccountOptions { get; } = [];

    private readonly List<string> _accountIds = [];

    public IReadOnlyList<string> DirectionOptions { get; } = ["Мне должны", "Я должен"];

    [ObservableProperty]
    public partial string Title { get; set; } = "Новый долг";

    [ObservableProperty]
    public partial int DirectionIndex { get; set; }

    [ObservableProperty]
    public partial string PersonName { get; set; } = "";

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasDueDate { get; set; }

    [ObservableProperty]
    public partial DateTime DueDate { get; set; } = DateTime.Now.AddDays(14);

    [ObservableProperty]
    public partial int AccountIndex { get; set; }

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    public Func<string, string, Task>? AlertAsync { get; set; }

    public string? Dir
    {
        get => null;
        set => DirectionIndex = value == DebtDirection.IOwe ? 1 : 0;
    }

    public string? DebtId
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
        AccountOptions.Add("— без счёта —"); // индекс 0

        foreach (var account in _db.Accounts.GetAll())
        {
            _accountIds.Add(account.Id);
            AccountOptions.Add(account.Name);
        }

        if (_id is null)
        {
            return Task.CompletedTask;
        }

        var debt = _db.Debts.Get(_id);
        if (debt is null)
        {
            return Task.CompletedTask;
        }

        _editing = debt;
        Title = "Долг";
        DirectionIndex = debt.Direction == DebtDirection.IOwe ? 1 : 0;
        PersonName = debt.PersonName;
        AmountText = MoneyFormat.ForInput(debt.AmountMinor);
        Note = debt.Note ?? "";
        HasDueDate = debt.DueDateUnix is not null;
        if (debt.DueDateUnix is { } due)
        {
            DueDate = DateTimeOffset.FromUnixTimeSeconds(due).LocalDateTime;
        }

        var accountIdx = debt.AccountId is null ? 0 : _accountIds.IndexOf(debt.AccountId);
        AccountIndex = accountIdx < 0 ? 0 : accountIdx + 1;

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(PersonName))
        {
            await AlertAsync?.Invoke("Ошибка", "Введите имя человека")!;
            return;
        }

        var amount = MoneyFormat.ParseInput(AmountText);
        if (amount <= 0)
        {
            await AlertAsync?.Invoke("Ошибка", "Введите сумму больше нуля")!;
            return;
        }

        var debt = _editing ?? new Debt();
        debt.Direction = DirectionIndex == 1 ? DebtDirection.IOwe : DebtDirection.OwedToMe;
        debt.PersonName = PersonName.Trim();
        debt.AmountMinor = amount;
        debt.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        debt.DueDateUnix = HasDueDate
            ? new DateTimeOffset(DueDate.Date.AddHours(12), TimeZoneInfo.Local.GetUtcOffset(DueDate.Date)).ToUnixTimeSeconds()
            : null;
        debt.AccountId = AccountIndex > 0 ? _accountIds[AccountIndex - 1] : null;

        _db.Debts.Save(debt);
        await Shell.Current.GoToAsync("..");
    }
}

public partial class PaymentRowVM
{
    public required string DateText { get; init; }
    public required string AmountText { get; init; }
}

[QueryProperty(nameof(DebtId), "id")]
public partial class DebtDetailViewModel(AppDatabase db, DebtService debts) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly DebtService _debts = debts;
    private Debt? _debt;

    public ObservableCollection<PaymentRowVM> Payments { get; } = [];

    [ObservableProperty]
    public partial string PersonTitle { get; set; } = "";

    [ObservableProperty]
    public partial string DirectionText { get; set; } = "";

    [ObservableProperty]
    public partial string TotalText { get; set; } = "";

    [ObservableProperty]
    public partial string RemainingText { get; set; } = "";

    [ObservableProperty]
    public partial string DueText { get; set; } = "";

    [ObservableProperty]
    public partial bool IsSettled { get; set; }

    [ObservableProperty]
    public partial bool CanLinkTransaction { get; set; }

    [ObservableProperty]
    public partial string PaymentAmountText { get; set; } = "";

    [ObservableProperty]
    public partial DateTime PaymentDate { get; set; } = DateTime.Now;

    [ObservableProperty]
    public partial bool CreateTransaction { get; set; } = true;

    public Func<string, string, Task>? AlertAsync { get; set; }

    public string? DebtId { get; set; }

    public async Task InitializeAsync()
    {
        if (DebtId is null || _debt is not null)
        {
            return;
        }

        _debt = _db.Debts.Get(DebtId);
        if (_debt is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        CanLinkTransaction = !string.IsNullOrEmpty(_debt.AccountId);
        Reload();
    }

    private void Reload()
    {
        if (_debt is null)
        {
            return;
        }

        PersonTitle = _debt.PersonName;
        DirectionText = _debt.Direction == DebtDirection.IOwe ? "Я должен" : "Мне должны";
        TotalText = MoneyFormat.Rubles(_debt.AmountMinor);
        RemainingText = MoneyFormat.Rubles(_db.Debts.GetRemaining(_debt));
        DueText = _debt.DueDateUnix is { } due
            ? $"Срок: {DateTimeOffset.FromUnixTimeSeconds(due).ToLocalTime():d MMMM yyyy}"
            : "";
        IsSettled = _debt.Status == DebtStatus.Settled;

        Payments.Clear();
        foreach (var payment in _db.Debts.GetPayments(_debt.Id))
        {
            Payments.Add(new PaymentRowVM
            {
                DateText = DateTimeOffset.FromUnixTimeSeconds(payment.DateUnix).ToLocalTime().ToString("d MMM yyyy"),
                AmountText = MoneyFormat.Rubles(payment.AmountMinor),
            });
        }
    }

    [RelayCommand]
    private async Task AddPaymentAsync()
    {
        if (_debt is null)
        {
            return;
        }

        var amount = MoneyFormat.ParseInput(PaymentAmountText);
        if (amount <= 0)
        {
            await AlertAsync?.Invoke("Ошибка", "Введите сумму больше нуля")!;
            return;
        }

        var dateUnix = new DateTimeOffset(PaymentDate.Date.AddHours(12),
            TimeZoneInfo.Local.GetUtcOffset(PaymentDate.Date)).ToUnixTimeSeconds();

        _debts.AddPayment(_debt, amount, dateUnix, CreateTransaction && CanLinkTransaction);

        PaymentAmountText = "";
        Reload();
    }

    [RelayCommand]
    private async Task SettleAsync()
    {
        if (_debt is null)
        {
            return;
        }

        _debt.Status = DebtStatus.Settled;
        _debt.SettledDateUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
        _db.Debts.Save(_debt);
        Reload();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenEdit()
    {
        if (_debt is not null)
        {
            Shell.Current.GoToAsync($"debtnew?id={_debt.Id}");
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (_debt is null)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить долг?", $"Долг «{_debt.PersonName}» и его платежи будут удалены.", "Удалить", "Отмена");
        if (!confirmed)
        {
            return;
        }

        foreach (var payment in _db.Debts.GetPayments(_debt.Id))
        {
            _db.Debts.SoftDeletePayment(payment);
        }

        _db.Debts.SoftDelete(_debt);
        await Shell.Current.GoToAsync("..");
    }
}
