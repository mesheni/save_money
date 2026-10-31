using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using SaveMoney.Messaging;

namespace SaveMoney.ViewModels;

public partial class DebtRowVM
{
    public bool IsCreditAccount { get; init; }
    public bool IsPersonalDebt => !IsCreditAccount;
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

    [ObservableProperty]
    public partial bool HasNoRows { get; set; } = true;

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
                    ? $"просрочено ({DateTimeOffset.FromUnixTimeSeconds(d).ToLocalTime():d MMM})"
                    : $"до {DateTimeOffset.FromUnixTimeSeconds(d).ToLocalTime():d MMM}"
                : "";

            Rows.Add(new DebtRowVM
            {
                Id = debt.Id,
                PersonName = debt.PersonName,
                Icon = DirectionIndex == 1 ? IconKeys.DebtOut : IconKeys.DebtIn,
                Subtitle = dueText,
                RemainingText = MoneyFormat.Rubles(remaining),
                TotalText = $"из {MoneyFormat.Rubles(debt.AmountMinor)}",
                IsOverdue = overdue,
                Progress = debt.AmountMinor > 0
                    ? Math.Min(1.0, (double)(debt.AmountMinor - remaining) / debt.AmountMinor)
                    : 0,
            });
        }

        if (DirectionIndex == 1)
        {
            foreach (var target in new RepaymentService(_db).GetTargets().Where(t => t.IsCreditAccount))
            {
                total += target.RemainingMinor;
                Rows.Add(new DebtRowVM
                {
                    Id = target.Id, IsCreditAccount = true, PersonName = target.Name,
                    Icon = IconKeys.DebtOut, Subtitle = target.Type,
                    RemainingText = MoneyFormat.Rubles(target.RemainingMinor),
                    TotalText = "", IsOverdue = false, Progress = 0,
                });
            }
        }

        SummaryText = DirectionIndex == 1
            ? $"Я должен: {MoneyFormat.Rubles(total)}"
            : $"Мне должны: {MoneyFormat.Rubles(total)}";
        HasNoRows = Rows.Count == 0;
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
            Shell.Current.GoToAsync(row.IsCreditAccount
                ? $"transaction?repayAccount={Uri.EscapeDataString(row.Id)}"
                : $"debt?id={Uri.EscapeDataString(row.Id)}");
        }
    }

    public async Task<bool> DeleteAsync(DebtRowVM? row)
    {
        if (row is null || row.IsCreditAccount)
        {
            return false;
        }

        var confirmed = await SaveMoney.Helpers.MonetDialogs.AlertAsync(
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
[QueryProperty(nameof(PresetKind), "kind")]
[QueryProperty(nameof(DebtId), "id")]
public partial class DebtEditViewModel(AppDatabase db) : ObservableObject
{
    [ObservableProperty]
    public partial string NameError { get; set; } = "";
    [ObservableProperty]
    public partial string AmountError { get; set; } = "";
    [ObservableProperty]
    public partial string AccountError { get; set; } = "";

    public IReadOnlyList<string> KindOptions { get; } = ["Личный долг", "Потребительский кредит"];
    [ObservableProperty] public partial int KindIndex { get; set; }
    public bool IsPersonalDebt => KindIndex == 0;
    partial void OnKindIndexChanged(int value)
    {
        if (value == 1) DirectionIndex = 1;
        OnPropertyChanged(nameof(IsPersonalDebt));
    }
    public string? PresetKind { get => null; set { if (value == DebtKind.ConsumerLoan) { KindIndex = 1; DirectionIndex = 1; } } }

    private readonly AppDatabase _db = db;
    private string? _id;
    private Debt? _editing;
    private bool _loaded;

    /// <summary>Счёт долга удалён: в пикере добавлена заглушка, сохранение до явного выбора заблокировано.</summary>
    private bool _accountMissing;

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
        KindIndex = debt.Kind == DebtKind.ConsumerLoan ? 1 : 0;
        AmountText = MoneyFormat.ForInput(debt.AmountMinor);
        Note = debt.Note ?? "";
        HasDueDate = debt.DueDateUnix is not null;
        if (debt.DueDateUnix is { } due)
        {
            DueDate = DateTimeOffset.FromUnixTimeSeconds(due).LocalDateTime;
        }

        var accountIdx = debt.AccountId is null ? -1 : _accountIds.IndexOf(debt.AccountId);
        if (debt.AccountId is null)
        {
            AccountIndex = 0;
        }
        else if (accountIdx < 0)
        {
            // Счёт долга удалён: заглушка вместо молчаливой отвязки при сохранении.
            _accountMissing = true;
            AccountOptions.Add("(счёт удалён)");
            AccountIndex = AccountOptions.Count - 1;
        }
        else
        {
            AccountIndex = accountIdx + 1;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        NameError = "";
        AmountError = "";
        AccountError = "";

        if (string.IsNullOrWhiteSpace(PersonName))
        {
            NameError = "Введите имя человека";
            return;
        }

        var amount = MoneyFormat.ParseInput(AmountText);
        if (amount <= 0)
        {
            AmountError = "Введите сумму больше нуля";
            return;
        }

        if (_accountMissing && AccountIndex == AccountOptions.Count - 1)
        {
            AccountError = "Счёт этого долга удалён — выберите другой счёт или «без счёта»";
            return;
        }

        var debt = _editing ?? new Debt();
        debt.Kind = KindIndex == 1 ? DebtKind.ConsumerLoan : DebtKind.Personal;
        debt.Direction = KindIndex == 1 || DirectionIndex == 1 ? DebtDirection.IOwe : DebtDirection.OwedToMe;
        debt.PersonName = PersonName.Trim();
        debt.AmountMinor = amount;
        debt.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        debt.DueDateUnix = HasDueDate
            ? new DateTimeOffset(DueDate.Date.AddHours(12), TimeZoneInfo.Local.GetUtcOffset(DueDate.Date)).ToUnixTimeSeconds()
            : null;
        debt.AccountId = AccountIndex > 0 && AccountIndex <= _accountIds.Count
            ? _accountIds[AccountIndex - 1]
            : null;

        try
        {
            _db.Debts.Save(debt);
        }
        catch (Exception ex)
        {
            await AlertAsync?.Invoke("Ошибка", ex.Message)!;
            return;
        }

        await Shell.Current.Navigation.PopAsync();
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

    /// <summary>Положительная пара к IsSettled: биндинг с «!» не выживает при SourceGen.</summary>
    public bool IsOpen => !IsSettled;
    public bool CanRepay => IsOpen && _debt?.Direction == DebtDirection.IOwe;
    public bool CanReceivePayment => IsOpen && _debt?.Direction == DebtDirection.OwedToMe;
    [RelayCommand]
    private Task RepayAsync() => Shell.Current.GoToAsync($"transaction?repayDebt={DebtId}");

    partial void OnIsSettledChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(CanRepay));
        OnPropertyChanged(nameof(CanReceivePayment));
    }

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
        if (DebtId is null)
        {
            return;
        }

        // Перечитываем при каждом появлении: после правки долга экран должен
        // показать актуальные данные, а не снимок первого открытия.
        _debt = _db.Debts.Get(DebtId);
        if (_debt is null)
        {
            await Shell.Current.Navigation.PopAsync();
            return;
        }

        // Существование счёта, а не только непустой Id: счёт мог быть удалён —
        // транзакция на удалённом счёте выпала бы из всех балансов.
        CanLinkTransaction = !string.IsNullOrEmpty(_debt.AccountId)
            && _db.Accounts.GetAll().Any(a => a.Id == _debt.AccountId);
        Reload();
    }

    private void Reload()
    {
        if (_debt is null)
        {
            return;
        }

        OnPropertyChanged(nameof(CanRepay));
        OnPropertyChanged(nameof(CanReceivePayment));
        PersonTitle = _debt.PersonName;
        DirectionText = _debt.Kind == DebtKind.ConsumerLoan ? "Потребительский кредит" : _debt.Direction == DebtDirection.IOwe ? "Я должен" : "Мне должны";
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

        var createTransaction = CreateTransaction && CanLinkTransaction;
        try
        {
            _debts.AddPayment(_debt, amount, dateUnix, createTransaction);
            _debt = _db.Debts.Get(_debt.Id);
        }
        catch (Exception ex)
        {
            await AlertAsync!.Invoke("Ошибка", ex.Message);
            return;
        }

        if (createTransaction)
        {
            // Связанная транзакция меняет балансы — обновляем их сразу, а не по Appearing.
            TransactionsChangedMessage.Broadcast();
        }

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

        var confirmed = await SaveMoney.Helpers.MonetDialogs.AlertAsync(
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
        await Shell.Current.Navigation.PopAsync();
    }
}
