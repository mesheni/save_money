using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>
/// Экран быстрого ввода (и редактирования по route "transaction?id=...").
/// Расход/доход/перевод, кастомная клавиатура, угадывание категории по магазину.
/// Виджет открывает его с предзаполненной суммой: "transaction?amount=100".
/// </summary>
[QueryProperty(nameof(EditId), "id")]
[QueryProperty(nameof(PresetAmount), "amount")]
public partial class QuickAddViewModel(
    AppDatabase db,
    TransactionService transactions,
    BalanceService balance) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly TransactionService _transactions = transactions;
    private readonly BalanceService _balance = balance;

    private string? _pendingEditId;
    private Transaction? _editing;

    private static readonly Color SelectedColor = Color.FromArgb("#512BD4");
    private static readonly Color UnselectedColor = Color.FromArgb("#EFEFF4");

    public ObservableCollection<AccountChip> Accounts { get; } = [];
    public ObservableCollection<AccountChip> TargetAccounts { get; } = [];
    public ObservableCollection<CategoryChip> RootCategories { get; } = [];
    public ObservableCollection<CategoryChip> SubCategories { get; } = [];

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    public string AmountDisplay => string.IsNullOrEmpty(AmountText) ? "0" : AmountText;

    partial void OnAmountTextChanged(string value) => OnPropertyChanged(nameof(AmountDisplay));

    [ObservableProperty]
    public partial string BalanceText { get; set; } = "…";

    [ObservableProperty]
    public partial bool IsIncome { get; set; }

    [ObservableProperty]
    public partial bool IsTransfer { get; set; }

    [ObservableProperty]
    public partial bool HasSubCategories { get; set; }

    [ObservableProperty]
    public partial string Payee { get; set; } = "";

    [ObservableProperty]
    public partial string Note { get; set; } = "";

    [ObservableProperty]
    public partial DateTime Date { get; set; } = DateTime.Now;

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string ModeText { get; set; } = "Расход";

    public bool IsOperation => !IsTransfer;
    public bool IsExpenseMode => !IsIncome && !IsTransfer;

    public Color ExpenseColor => IsExpenseMode ? SelectedColor : UnselectedColor;
    public Color IncomeColor => IsIncome ? SelectedColor : UnselectedColor;
    public Color TransferColor => IsTransfer ? SelectedColor : UnselectedColor;

    /// <summary>Страница подставляет показ ошибок (DisplayAlert).</summary>
    public Func<string, string, Task>? AlertAsync { get; set; }

    /// <summary>Страница подставляет закрытие экрана после правки.</summary>
    public Func<Task>? CloseAsync { get; set; }

    public string? EditId
    {
        get => _pendingEditId;
        set => _pendingEditId = value;
    }

    /// <summary>Сумма, предзаполненная с виджета ("100" или "1250,50").</summary>
    public string? PresetAmount { get; set; }

    public async Task InitializeAsync()
    {
        if (!IsEditing && !string.IsNullOrEmpty(PresetAmount))
        {
            AmountText = PresetAmount;
        }

        LoadAccounts();
        ReloadCategories();
        RefreshBalance();

        if (_pendingEditId is not null && _editing is null)
        {
            await LoadForEditAsync(_pendingEditId);
        }
    }

    private void LoadAccounts(string? selectId = null)
    {
        var previous = Accounts.FirstOrDefault(a => a.IsSelected)?.Id;
        var all = _db.Accounts.GetAll();

        Accounts.Clear();
        foreach (var account in all)
        {
            Accounts.Add(new AccountChip { Id = account.Id, Name = account.Name });
        }

        var selectedId = selectId ?? previous ?? Accounts.FirstOrDefault()?.Id;
        SelectChip(Accounts, Accounts.FirstOrDefault(a => a.Id == selectedId));
        RebuildTargetAccounts();
    }

    private void RebuildTargetAccounts(string? selectId = null)
    {
        var currentId = Accounts.FirstOrDefault(a => a.IsSelected)?.Id;
        var previousTarget = TargetAccounts.FirstOrDefault(a => a.IsSelected)?.Id;

        TargetAccounts.Clear();
        foreach (var chip in Accounts.Where(a => a.Id != currentId))
        {
            TargetAccounts.Add(chip);
        }

        var targetId = selectId ?? previousTarget ?? TargetAccounts.FirstOrDefault()?.Id;
        foreach (var chip in TargetAccounts)
        {
            chip.IsSelected = chip.Id == targetId;
        }
    }

    private void ReloadCategories()
    {
        var kind = IsIncome ? CategoryKind.Income : CategoryKind.Expense;
        var roots = _db.Categories.GetRoots(kind);

        RootCategories.Clear();
        foreach (var root in roots)
        {
            RootCategories.Add(new CategoryChip { Id = root.Id, Name = root.Name, Icon = root.Icon ?? "❓" });
        }

        var lastId = _transactions.GetLastCategoryId(kind);
        CategoryChip? toSelect = RootCategories.FirstOrDefault(c => c.Id == lastId)
            ?? RootCategories.FirstOrDefault();

        if (toSelect is null)
        {
            SubCategories.Clear();
            HasSubCategories = false;
            return;
        }

        SelectRoot(toSelect, trySelectChildId: lastId);
    }

    private void SelectRoot(CategoryChip root, string? trySelectChildId = null)
    {
        SelectChip(RootCategories, root);

        var children = _db.Categories.GetChildren(root.Id);
        SubCategories.Clear();
        foreach (var child in children)
        {
            SubCategories.Add(new CategoryChip { Id = child.Id, Name = child.Name, Icon = child.Icon ?? "❓" });
        }

        HasSubCategories = children.Count > 0;
        var selectedChild = trySelectChildId is null ? null : SubCategories.FirstOrDefault(c => c.Id == trySelectChildId);
        SelectChip(SubCategories, selectedChild);
    }

    private static void SelectChip(ObservableCollection<AccountChip> chips, AccountChip? chip)
    {
        foreach (var item in chips)
        {
            item.IsSelected = item == chip;
        }
    }

    private static void SelectChip(ObservableCollection<CategoryChip> chips, CategoryChip? chip)
    {
        foreach (var item in chips)
        {
            item.IsSelected = item == chip;
        }
    }

    private void RefreshBalance() => BalanceText = MoneyFormat.Rubles(_balance.GetTotalBalance());

    private void UpdateModeText()
    {
        ModeText = IsTransfer ? "Перевод" : IsIncome ? "Доход" : "Расход";
    }

    // --- Команды ---

    [RelayCommand]
    private void Digit(string key)
    {
        if (key == "⌫")
        {
            AmountText = AmountText.Length > 0 ? AmountText[..^1] : "";
            return;
        }

        if (key == ",")
        {
            if (AmountText.Contains(',') || AmountText.Length == 0)
            {
                return;
            }

            AmountText += ",";
            return;
        }

        if (AmountText.Length >= 12)
        {
            return;
        }

        var decimalPart = AmountText.SkipWhile(c => c != ',').Skip(1).Count();
        if (AmountText.Contains(',') && decimalPart >= 2)
        {
            return;
        }

        AmountText += key;
    }

    [RelayCommand]
    private void SetExpense()
    {
        IsIncome = false;
        IsTransfer = false;
        UpdateModeText();
        ReloadCategories();
        NotifyModeChanged();
    }

    [RelayCommand]
    private void SetIncome()
    {
        IsIncome = true;
        IsTransfer = false;
        UpdateModeText();
        ReloadCategories();
        NotifyModeChanged();
    }

    [RelayCommand]
    private void SetTransfer()
    {
        IsTransfer = true;
        UpdateModeText();
        NotifyModeChanged();
    }

    private void NotifyModeChanged()
    {
        OnPropertyChanged(nameof(IsOperation));
        OnPropertyChanged(nameof(IsExpenseMode));
        OnPropertyChanged(nameof(ExpenseColor));
        OnPropertyChanged(nameof(IncomeColor));
        OnPropertyChanged(nameof(TransferColor));
    }

    [RelayCommand]
    private void SelectAccount(AccountChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        SelectChip(Accounts, chip);
        RebuildTargetAccounts();
    }

    [RelayCommand]
    private void SelectTarget(AccountChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        foreach (var item in TargetAccounts)
        {
            item.IsSelected = item == chip;
        }
    }

    [RelayCommand]
    private void SelectRoot(CategoryChip? chip)
    {
        if (chip is not null)
        {
            SelectRoot(chip);
        }
    }

    [RelayCommand]
    private void SelectSub(CategoryChip? chip)
    {
        if (chip is not null)
        {
            SelectChip(SubCategories, chip);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var amountMinor = MoneyFormat.ParseInput(AmountText);
        if (amountMinor <= 0)
        {
            await AlertAsync?.Invoke("Ошибка", "Введите сумму больше нуля")!;
            return;
        }

        var accountChip = Accounts.FirstOrDefault(a => a.IsSelected);
        if (accountChip is null)
        {
            await AlertAsync?.Invoke("Ошибка", "Сначала создайте счёт на вкладке «Ещё» → «Счета»")!;
            return;
        }

        var kind = IsTransfer ? TransactionKind.Transfer : IsIncome ? TransactionKind.Income : TransactionKind.Expense;
        var dateUnix = ToUnix(Date);

        var tx = _editing ?? new Transaction { Source = TransactionSource.Manual };
        tx.AccountId = accountChip.Id;
        tx.AmountMinor = amountMinor;
        tx.Kind = kind;
        tx.Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim();
        tx.DateUnix = dateUnix;

        if (IsTransfer)
        {
            var target = TargetAccounts.FirstOrDefault(a => a.IsSelected);
            if (target is null)
            {
                await AlertAsync?.Invoke("Ошибка", "Выберите счёт-получатель")!;
                return;
            }

            tx.TransferAccountId = target.Id;
            tx.TransferAmountMinor = null;
            tx.CategoryId = null;
            tx.Payee = null;
        }
        else
        {
            var payee = string.IsNullOrWhiteSpace(Payee) ? null : Payee.Trim();
            var categoryId = SubCategories.FirstOrDefault(c => c.IsSelected)?.Id
                ?? RootCategories.FirstOrDefault(c => c.IsSelected)?.Id;

            // Категория не выбрана, но введён магазин — угадываем по прошлым покупкам.
            if (categoryId is null && payee is not null)
            {
                categoryId = _transactions.GuessCategoryId(payee);
            }

            tx.TransferAccountId = null;
            tx.TransferAmountMinor = null;
            tx.CategoryId = categoryId;
            tx.Payee = kind == TransactionKind.Expense ? payee : null;
        }

        try
        {
            _transactions.Save(tx);
        }
        catch (ArgumentException ex)
        {
            await AlertAsync?.Invoke("Ошибка", ex.Message)!;
            return;
        }

        if (IsEditing)
        {
            if (CloseAsync is not null)
            {
                await CloseAsync();
            }

            return;
        }

        AmountText = "";
        Note = "";
        Payee = "";
        Date = DateTime.Now;
        RefreshBalance();
        ReloadCategories();
    }

    private async Task LoadForEditAsync(string id)
    {
        var tx = _db.Transactions.Get(id);
        if (tx is null)
        {
            return;
        }

        _editing = tx;
        IsEditing = true;

        if (tx.Kind == TransactionKind.Income)
        {
            SetIncome();
        }
        else if (tx.Kind == TransactionKind.Transfer)
        {
            SetTransfer();
        }
        else
        {
            SetExpense();
        }

        AmountText = MoneyFormat.ForInput(tx.AmountMinor);
        Date = DateTimeOffset.FromUnixTimeSeconds(tx.DateUnix).ToLocalTime().DateTime;
        Note = tx.Note ?? "";
        Payee = tx.Payee ?? "";

        LoadAccounts(selectId: tx.AccountId);
        if (tx.Kind == TransactionKind.Transfer && tx.TransferAccountId is not null)
        {
            RebuildTargetAccounts(selectId: tx.TransferAccountId);
        }

        SelectCategoryById(tx.CategoryId);
        ModeText = $"{ModeText} · правка";
    }

    private void SelectCategoryById(string? categoryId)
    {
        if (categoryId is null)
        {
            return;
        }

        var rootChip = RootCategories.FirstOrDefault(c => c.Id == categoryId);
        if (rootChip is not null)
        {
            SelectRoot(rootChip);
            return;
        }

        var childChip = SubCategories.FirstOrDefault(c => c.Id == categoryId);
        if (childChip is not null)
        {
            var parentChip = RootCategories.FirstOrDefault(r =>
                _db.Categories.Get(childChip.Id)?.ParentId == r.Id);
            if (parentChip is not null)
            {
                SelectRoot(parentChip, trySelectChildId: childChip.Id);
            }
        }
    }

    private static long ToUnix(DateTime local) =>
        new DateTimeOffset(local.Year, local.Month, local.Day, DateTime.Now.Hour, DateTime.Now.Minute, 0,
            TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();

    partial void OnIsIncomeChanged(bool value) => NotifyModeChanged();
    partial void OnIsTransferChanged(bool value) => NotifyModeChanged();
}
