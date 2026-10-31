using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using SaveMoney.Messaging;

namespace SaveMoney.ViewModels;

/// <summary>
/// Экран быстрого ввода (и редактирования по route "transaction?id=...").
/// Расход/доход/перевод, сетка категорий + кастомная клавиатура, угадывание
/// категории по магазину. Виджет открывает его с предзаполненной суммой:
/// "transaction?amount=100".
/// </summary>
[QueryProperty(nameof(EditId), "id")]
[QueryProperty(nameof(PresetDebtId), "repayDebt")]
[QueryProperty(nameof(PresetAccountId), "repayAccount")]
[QueryProperty(nameof(PresetAmount), "amount")]
[QueryProperty(nameof(WidgetSource), "source")]
public partial class QuickAddViewModel : ObservableObject,
    IRecipient<TransactionsChangedMessage>
{
    private readonly AppDatabase _db;
    private readonly TransactionService _transactions;
    private readonly BalanceService _balance;
    private readonly CycleService _cycle;
    private readonly RecurringService _recurring;
    private readonly DebtService _debts;

    private string? _pendingEditId;
    private Transaction? _editing;
    private bool _initialized;
    private bool _settingDate;
    private bool _dateExplicit;
    public bool IsStandaloneRepayment { get; private set; }

    /// <summary>Время суток исходной транзакции — правка не должна переписывать его на «сейчас».</summary>
    private TimeSpan _editTimeOfDay = DateTime.Now.TimeOfDay;

    public QuickAddViewModel(AppDatabase db, TransactionService transactions, BalanceService balance,
        CycleService cycle, RecurringService recurring, DebtService debts)
    {
        _db = db;
        _transactions = transactions;
        _balance = balance;
        _cycle = cycle;
        _recurring = recurring;
        _debts = debts;

        // Баланс и пилюля «в день» обновляются сразу после любой операции.
        WeakReferenceMessenger.Default.Register<QuickAddViewModel, TransactionsChangedMessage>(this, static (r, m) => r.Receive(m));

    }

    private void OnCategoryPicked(string categoryId)
    {
        CategoryPickedMessage.Unregister(OnCategoryPicked);
        SelectCategoryById(categoryId);
        TryQuickSave();
    }

    /// <summary>Что-то изменило операции/счета — перечитываем баланс, пилюлю «в день»,
    /// долги, счета и категории (в т.ч. добавленные на других экранах).</summary>
    public void Receive(TransactionsChangedMessage message)
    {
        LoadAccounts();
        ReloadCategories();
        RefreshBalance();
        RefreshPaydayHint();
        RefreshDebtsSummary();
        if (IsRepayment) RefreshRepaymentTargets();
    }

    [ObservableProperty]
    public partial string AmountText { get; set; } = "";

    public string AmountDisplay => string.IsNullOrEmpty(AmountText) ? "0" : AmountText;
    public double AmountFontSize => AmountDisplay.Length > 9 ? 24 : AmountDisplay.Length > 6 ? 32 : 40;

    /// <summary>Знак слева от суммы: «−» расход, «+» доход, пусто у перевода.</summary>
    public string SignText => IsTransfer ? "" : IsIncome ? "+" : "−";

    partial void OnAmountTextChanged(string value)
    {
        OnPropertyChanged(nameof(AmountDisplay));
        OnPropertyChanged(nameof(AmountFontSize));
        OnPropertyChanged(nameof(RepaymentRemainingText));
    }

    [ObservableProperty]
    public partial string BalanceText { get; set; } = "…";

    /// <summary>Баланс числом (в рублях) — для count-up анимации на странице.</summary>
    public double BalanceValue => _balance.GetTotalBalance() / 100.0;

    [ObservableProperty]
    public partial bool IsIncome { get; set; }

    [ObservableProperty]
    public partial bool IsTransfer { get; set; }

    [ObservableProperty]
    public partial bool HasSubCategories { get; set; }

    /// <summary>Магазин/заметка одним полем: у расхода пишется в Payee (по нему угадывается
    /// категория), у дохода и перевода — в Note.</summary>
    [ObservableProperty]
    public partial string MemoText { get; set; } = "";

    [ObservableProperty]
    public partial DateTime Date { get; set; } = DateTime.Now;

    /// <summary>Подпись на клавише даты: «Сегодня» или «25 окт». Format-хак DatePicker
    /// с literal «Сегодня» на Android не отрисовывается — текст пишет Label.</summary>
    public string DateButtonText =>
        Date.Date == DateTime.Now.Date ? "Сегодня" : Date.ToString("d MMM");

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string ModeText { get; set; } = "Расход";

    public bool IsOperation => !IsTransfer && !IsRepayment;
    public bool IsExpenseMode => !IsIncome && !IsTransfer && !IsRepayment;

    /// <summary>Текст подписи над сеткой категорий.</summary>
    [ObservableProperty]
    public partial string CategoryLabelText { get; set; } = "Категория";

    /// <summary>Плитки сетки категорий: до 8 корневых, выбранная всегда первой (когда вне топа).</summary>
    public ObservableCollection<CategoryChip> TopCategories { get; } = [];

    public ObservableCollection<CategoryChip> RootCategories { get; } = [];
    public ObservableCollection<CategoryChip> SubCategories { get; } = [];
    public ObservableCollection<AccountChip> Accounts { get; } = [];
    public ObservableCollection<AccountChip> TargetAccounts { get; } = [];

    [ObservableProperty]
    public partial string AccountPickerText { get; set; } = "";

    [ObservableProperty]
    public partial string TargetPickerText { get; set; } = "";

    /// <summary>«N ₽ в день до зарплаты» — пилюля в шапке; пусто, если цикл не настраивался.</summary>
    [ObservableProperty]
    public partial string PaydayHintText { get; set; } = "";

    public bool HasPaydayHint => PaydayHintText.Length > 0;

    /// <summary>«Долги +17 000 ₽» — сальдо открытых долгов в шапке; скрыто, когда долгов нет.</summary>
    [ObservableProperty]
    public partial string DebtsSummaryText { get; set; } = "";

    public bool HasDebtsSummary => DebtsSummaryText.Length > 0;

    private RepaymentTarget? _repaymentTarget;
    public string? PresetDebtId { get; set; }
    public string? PresetAccountId { get; set; }
    public ObservableCollection<RepaymentTarget> RepaymentTargets { get; } = [];
    [ObservableProperty] public partial bool IsRepayment { get; set; }
    [ObservableProperty] public partial bool IsChoosingRepayment { get; set; } = true;
    [ObservableProperty] public partial string DebtButtonText { get; set; } = "Выбрать задолженность";
    public string SaveButtonText => "✓";
    public string SaveDescription => IsRepayment ? "Погасить долг" : "Сохранить операцию";
    public string MemoPlaceholder => IsRepayment ? "Заметка к погашению" : "Магазин или заметка";
    public bool HasNoRepaymentTargets => RepaymentTargets.Count == 0;
    public bool HasRepaymentTarget => _repaymentTarget is not null;
    public string RepaymentRemainingText => _repaymentTarget is null ? ""
        : $"Осталось: {MoneyFormat.Rubles(_repaymentTarget.RemainingMinor)}";

    private void RefreshRepaymentTargets()
    {
        var selected = _repaymentTarget;
        RepaymentTargets.Clear();
        foreach (var target in new RepaymentService(_db).GetTargets(_editing?.Id)) RepaymentTargets.Add(target);
        _repaymentTarget = RepaymentTargets.FirstOrDefault(t => t.Id == selected?.Id && t.IsCreditAccount == selected.IsCreditAccount);
        DebtButtonText = _repaymentTarget?.Name ?? "Выбрать задолженность";
        if (_repaymentTarget is null) IsChoosingRepayment = true;
        OnPropertyChanged(nameof(HasNoRepaymentTargets));
        OnPropertyChanged(nameof(HasRepaymentTarget));
        OnPropertyChanged(nameof(RepaymentRemainingText));
    }

    [RelayCommand]
    private void SelectRepayment(RepaymentTarget? target)
    {
        _repaymentTarget = target;
        DebtButtonText = target?.Name ?? "Выбрать задолженность";
        IsChoosingRepayment = target is null;
        if (target?.IsCreditAccount == true && Accounts.FirstOrDefault(a => a.IsSelected)?.Id == target.Id)
            SelectChip(Accounts, Accounts.FirstOrDefault(a => a.Id != target.Id));
        RefreshPickerTexts();
        OnPropertyChanged(nameof(HasRepaymentTarget));
        OnPropertyChanged(nameof(RepaymentRemainingText));
    }

    [RelayCommand] private Task AddPersonalDebtAsync() => Shell.Current.GoToAsync("debtnew?dir=i_owe");
    [RelayCommand] private Task AddConsumerLoanAsync() => Shell.Current.GoToAsync("debtnew?dir=i_owe&kind=consumer_loan");
    [RelayCommand] private Task AddCreditAccountAsync() => Shell.Current.GoToAsync("account?kind=credit");

    /// <summary>Страница подставляет показ ошибок (DisplayAlert).</summary>
    public Func<string, string, Task>? AlertAsync { get; set; }

    /// <summary>Страница подставляет выбор варианта (ActionSheet): (заголовок, варианты) → выбор или null.</summary>
    public Func<string, string[], Task<string?>>? PickAsync { get; set; }

    /// <summary>Страница подставляет закрытие экрана после правки.</summary>
    public Func<Task>? CloseAsync { get; set; }

    /// <summary>Страница подставляет тост «Сохранено» после успешной записи.</summary>
    public Func<Task>? SavedAsync { get; set; }

    public string? EditId
    {
        get => _pendingEditId;
        set => _pendingEditId = value;
    }

    /// <summary>Сумма, предзаполненная с виджета ("100" или "1250,50").</summary>
    public string? PresetAmount { get; set; }

    /// <summary>Признак «экран открыт с виджета» — транзакция сохранится с source=widget.</summary>
    public string? WidgetSource { get; set; }

    public async Task InitializeAsync()
    {
        CategoryPickedMessage.Unregister(OnCategoryPicked);
        // При возврате обновляем категории, сохраняя текущий выбор.
        if (_initialized)
        {
            var selectedCategory = SubCategories.FirstOrDefault(c => c.IsSelected)?.Id
                ?? RootCategories.FirstOrDefault(c => c.IsSelected)?.Id;
            ReloadCategories();
            SelectCategoryById(selectedCategory);
            RefreshBalance();
            RefreshPaydayHint();
            RefreshDebtsSummary();
            RefreshCategoryLabel();
            LoadAccounts();
            if (IsRepayment) RefreshRepaymentTargets();
            return;
        }

        _initialized = true;

        if (!IsEditing)
        {
            if (!string.IsNullOrEmpty(PresetAmount))
            {
                AmountText = PresetAmount;
                PresetAmount = null; // подставляем один раз — повторное Appearing не должно возвращать сумму
            }

            // Вкладка «Ввод» живёт всё время процесса: без этого новые записи
            // после дней в фоне молча датировались последним открытием.
            // Время суток — то же: _editTimeOfDay из конструктора протухал так же,
            // а ToUnix штампует им каждую новую запись.
            SetDefaultDate();
            _editTimeOfDay = DateTime.Now.TimeOfDay;
        }

        LoadAccounts();
        ReloadCategories();
        RefreshBalance();
        RefreshPaydayHint();
        RefreshDebtsSummary();
        RefreshCategoryLabel();

        if (_pendingEditId is not null && _editing is null)
        {
            await LoadForEditAsync(_pendingEditId);
        }
        if (PresetDebtId is not null || PresetAccountId is not null)
        {
            IsStandaloneRepayment = true;
            SetRepayment();
            var target = RepaymentTargets.FirstOrDefault(t => PresetAccountId is not null
                ? t.IsCreditAccount && t.Id == PresetAccountId
                : !t.IsCreditAccount && t.Id == PresetDebtId);
            SelectRepayment(target);
            PresetDebtId = null;
            PresetAccountId = null;
            if (target is null && AlertAsync is not null)
                await AlertAsync("Задолженность недоступна", "Она уже погашена или недоступна. Выберите другую задолженность.");
        }
    }

    private void LoadAccounts(string? selectId = null)
    {
        var previous = Accounts.FirstOrDefault(a => a.IsSelected)?.Id;
        // Historical ordinary operations may still reference an archived account.
        var all = IsEditing && !IsRepayment ? _db.Accounts.GetAll() : _db.Accounts.GetAllActive();

        Accounts.Clear();
        foreach (var account in all)
        {
            Accounts.Add(new AccountChip { Id = account.Id, Name = account.Name });
        }

        var preferredId = selectId ?? previous ?? (IsEditing ? _editing?.AccountId : null);
        // Never silently move an edited operation away from a missing source.
        var selectedId = IsEditing ? preferredId : Accounts.Any(a => a.Id == preferredId)
            ? preferredId : Accounts.FirstOrDefault()?.Id;
        SelectChip(Accounts, Accounts.FirstOrDefault(a => a.Id == selectedId));
        RebuildTargetAccounts();
    }

    private void RebuildTargetAccounts(string? selectId = null)
    {
        var currentId = Accounts.FirstOrDefault(a => a.IsSelected)?.Id;
        var previousTargetId = TargetAccounts.FirstOrDefault(a => a.IsSelected)?.Id;

        TargetAccounts.Clear();
        foreach (var account in Accounts.Where(a => a.Id != currentId))
        {
            // Отдельные экземпляры, а не те же чипы: общие объекты подсвечивали
            // получателя прямо в строке «Счёт» (две оранжевых чипа) и ломали выбор
            // счёта-источника при сохранении перевода.
            TargetAccounts.Add(new AccountChip { Id = account.Id, Name = account.Name });
        }

        var targetId = selectId ?? previousTargetId ?? TargetAccounts.FirstOrDefault()?.Id;
        foreach (var chip in TargetAccounts)
        {
            chip.IsSelected = chip.Id == targetId;
        }

        RefreshPickerTexts();
    }

    private void RefreshPickerTexts()
    {
        AccountPickerText = (IsRepayment ? "Оплата: " : "") + (Accounts.FirstOrDefault(a => a.IsSelected)?.Name ?? "Выбрать счёт");
        TargetPickerText = TargetAccounts.FirstOrDefault(a => a.IsSelected)?.Name ?? "";
    }

    private void ReloadCategories()
    {
        var kind = IsIncome ? CategoryKind.Income : CategoryKind.Expense;
        var roots = _db.Categories.GetRoots(kind);

        RootCategories.Clear();
        foreach (var root in roots)
        {
            RootCategories.Add(new CategoryChip { Id = root.Id, Name = root.Name, Icon = root.Icon ?? IconKeys.Other });
        }

        var lastId = _transactions.GetLastCategoryId(kind);
        CategoryChip? toSelect = RootCategories.FirstOrDefault(c => c.Id == lastId)
            ?? RootCategories.FirstOrDefault();

        if (toSelect is null)
        {
            SubCategories.Clear();
            TopCategories.Clear();
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
            SubCategories.Add(new CategoryChip { Id = child.Id, Name = child.Name, Icon = child.Icon ?? IconKeys.Other });
        }

        HasSubCategories = children.Count > 0;
        var selectedChild = trySelectChildId is null ? null : SubCategories.FirstOrDefault(c => c.Id == trySelectChildId);
        SelectChip(SubCategories, selectedChild);
        BuildTopCategories(root.Id);
    }

    /// <summary>Сетка — первые 8 корневых; выбранная вне топа выталкивается в начало, чтобы оставаться видимой.</summary>
    private void BuildTopCategories(string selectedRootId)
    {
        TopCategories.Clear();
        var ordered = RootCategories
            .OrderByDescending(c => c.Id == selectedRootId)
            .Take(8);
        foreach (var chip in ordered)
        {
            TopCategories.Add(chip);
        }
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

    /// <summary>Пилюля «N ₽ в день до зарплаты»: прогноз PaydayViewModel, скрыт пока цикл не настраивался.</summary>
    private void RefreshPaydayHint()
    {
        // CycleAnchorUnix == 0 — пользователь ни разу не сохранял настройки цикла.
        var configured = _db.Settings.GetLong(SettingKeys.CycleAnchorUnix, 0) > 0;
        if (!configured)
        {
            PaydayHintText = "";
        }
        else
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
            var perDay = canSpend / daysLeft;
            PaydayHintText = $"{MoneyFormat.Rubles(perDay)} в день до зарплаты";
        }

        OnPropertyChanged(nameof(HasPaydayHint));
    }

    /// <summary>Сальдо открытых долгов: «мне должны» минус «я должен», по остаткам.</summary>
    private void RefreshDebtsSummary()
    {
        var active = _db.Debts.GetActive();
        var creditDebt = _db.Accounts.GetAllActive().Where(a => a.Kind == AccountKind.Credit)
            .Sum(a => Math.Min(0, _balance.GetAccountBalance(a)));
        if (active.Count == 0 && creditDebt == 0)
        {
            DebtsSummaryText = "";
            OnPropertyChanged(nameof(HasDebtsSummary));
            return;
        }

        var net = creditDebt + active.Sum(d =>
        {
            var remaining = _db.Debts.GetRemaining(d);
            return d.Direction == DebtDirection.OwedToMe ? remaining : -remaining;
        });

        if (net == 0)
        {
            DebtsSummaryText = "";
        }
        else
        {
            DebtsSummaryText = $"Долги {MoneyFormat.SignedRubles(net)}";
        }

        OnPropertyChanged(nameof(HasDebtsSummary));
    }

    /// <summary>Хвост подписи « · тап сохраняет» — только когда включён быстрый ввод.</summary>
    private void RefreshCategoryLabel()
    {
        var quick = _db.Settings.GetBool(SettingKeys.QuickSaveOnCategory, false);
        CategoryLabelText = quick ? "Категория · тап сохраняет расход" : "Категория";
    }

    private void UpdateModeText()
    {
        ModeText = IsRepayment ? "Погашение" : IsTransfer ? "Перевод" : IsIncome ? "Доход" : "Расход";
    }    // --- Команды ---

    [RelayCommand]
    private void Digit(string key)
    {
        if (key == "backspace")
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

        if (key == "00")
        {
            // «00» в пустом поле бессмысленна; после запятой не должно оставить больше 2 знаков.
            if (AmountText.Length == 0)
            {
                return;
            }

            var digits = AmountText.SkipWhile(c => c != ',').Skip(1).Count();
            if (AmountText.Contains(',') && digits >= 1)
            {
                return;
            }

            if (AmountText.Length + 2 > 12)
            {
                return;
            }

            AmountText += "00";
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
        IsRepayment = false;
        IsIncome = false;
        IsTransfer = false;
        UpdateModeText();
        ReloadCategories();
        NotifyModeChanged();
    }

    [RelayCommand]
    private void SetIncome()
    {
        IsRepayment = false;
        IsIncome = true;
        IsTransfer = false;
        UpdateModeText();
        ReloadCategories();
        NotifyModeChanged();
    }

    [RelayCommand]
    private void SetTransfer()
    {
        IsRepayment = false;
        IsIncome = false;
        IsTransfer = true;
        UpdateModeText();
        NotifyModeChanged();
        RefreshPickerTexts();
    }

    [RelayCommand]
    private void SetRepayment()
    {
        IsIncome = false;
        IsTransfer = false;
        IsRepayment = true;
        LoadAccounts(_db.Settings.Get(RepaymentService.LastAccountKey));
        RefreshRepaymentTargets();
        UpdateModeText();
        NotifyModeChanged();
    }

    private void NotifyModeChanged()
    {
        RefreshPickerTexts();
        OnPropertyChanged(nameof(IsOperation));
        OnPropertyChanged(nameof(IsExpenseMode));
        OnPropertyChanged(nameof(SignText));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(SaveDescription));
        OnPropertyChanged(nameof(MemoPlaceholder));
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

        RefreshPickerTexts();
    }

    /// <summary>Тап по кнопке счёта в строке полей — ActionSheet со списком счетов.</summary>
    [RelayCommand]
    private async Task PickAccountAsync()
    {
        var available = Accounts.Where(a => !IsRepayment || _repaymentTarget?.IsCreditAccount != true || a.Id != _repaymentTarget.Id).ToList();
        var options = available.Select((a, i) => $"{a.Name} · {i + 1}").ToArray();
        if (options.Length == 0)
        {
            return;
        }

        var picked = await PickAsync?.Invoke("Счёт", options)!;
        if (picked is null)
        {
            return;
        }

        var index = Array.IndexOf(options, picked);
        if (index < 0) return;
        SelectChip(Accounts, available[index]);
        RebuildTargetAccounts();
    }

    /// <summary>Тап по кнопке «Куда» (перевод) — ActionSheet со счетами-получателями.</summary>
    [RelayCommand]
    private async Task PickTargetAsync()
    {
        var options = TargetAccounts.Select(a => a.Name).ToArray();
        if (options.Length == 0)
        {
            return;
        }

        var picked = await PickAsync?.Invoke("Куда", options)!;
        if (picked is null)
        {
            return;
        }

        foreach (var chip in TargetAccounts)
        {
            chip.IsSelected = chip.Name == picked;
        }

        RefreshPickerTexts();
    }

    [RelayCommand]
    private void PickDebt()
    {
        RefreshRepaymentTargets();
        IsChoosingRepayment = true;
    }

    [RelayCommand]
    private void SelectRoot(CategoryChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        // Вызываем двухарговую перегрузку явно: SelectRoot(chip) разрешился бы
        // в саму команду (меньше параметров — лучше) и ушёл в бесконечную рекурсию.
        SelectRoot(chip, trySelectChildId: null);
        if (!HasSubCategories)
        {
            TryQuickSave();
        }
    }

    [RelayCommand]
    private void SelectSub(CategoryChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        SelectChip(SubCategories, chip);
        TryQuickSave();
    }

    [RelayCommand]
    private async Task OpenPaydayAsync()
    {
        await Shell.Current.GoToAsync("payday");
    }

    [RelayCommand]
    private async Task OpenAllCategoriesAsync()
    {
        var kind = IsIncome ? "income" : "expense";
        CategoryPickedMessage.Register(OnCategoryPicked);
        await Shell.Current.GoToAsync($"categorypicker?kind={kind}");
    }

    /// <summary>
    /// Настройка «сохранять сразу при выборе категории» (ввод за 2 касания):
    /// тап по категории с уже введённой суммой немедленно записывает операцию.
    /// </summary>
    private void TryQuickSave()
    {
        if (IsEditing
            || IsTransfer || IsRepayment
            || MoneyFormat.ParseInput(AmountText) <= 0
            || !_db.Settings.GetBool(SettingKeys.QuickSaveOnCategory, false))
        {
            return;
        }

        _ = SaveAsync();
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

        var kind = IsRepayment ? TransactionKind.DebtRepayment : IsTransfer ? TransactionKind.Transfer : IsIncome ? TransactionKind.Income : TransactionKind.Expense;
        if (!IsEditing)
        {
            if (!_dateExplicit) SetDefaultDate();
            _editTimeOfDay = DateTime.Now.TimeOfDay;
        }
        var dateUnix = ToUnix(Date);

        var originalKind = _editing?.Kind;
        var tx = _editing ?? new Transaction
        {
            Source = WidgetSource == "widget" ? TransactionSource.Widget : TransactionSource.Manual,
        };
        tx.AccountId = accountChip.Id;
        tx.AmountMinor = amountMinor;
        tx.Kind = kind;
        tx.DateUnix = dateUnix;

        tx.DebtId = null;
        if (IsRepayment)
        {
            if (_repaymentTarget is null)
            {
                await AlertAsync!.Invoke("Погашение", "Выберите задолженность");
                return;
            }
            tx.DebtId = _repaymentTarget.IsCreditAccount ? null : _repaymentTarget.Id;
            tx.TransferAccountId = _repaymentTarget.IsCreditAccount ? _repaymentTarget.Id : null;
            tx.CategoryId = null;
            tx.Payee = null;
            tx.Note = string.IsNullOrWhiteSpace(MemoText) ? null : MemoText.Trim();
        }
        else if (IsTransfer)
        {
            var target = TargetAccounts.FirstOrDefault(a => a.IsSelected);
            if (target is null)
            {
                await AlertAsync?.Invoke("Ошибка", "Выберите счёт-получатель")!;
                return;
            }

            tx.TransferAccountId = target.Id;
            if (originalKind != TransactionKind.Transfer) tx.TransferAmountMinor = null;
            tx.CategoryId = null;
            tx.Payee = null;
            tx.Note = string.IsNullOrWhiteSpace(MemoText) ? null : MemoText.Trim();
        }
        else
        {
            // Одно поле «Магазин или заметка»: у расхода это Payee (по нему угадывается
            // категория), у дохода — Note. Правка расхода с заметкой: MemoText писался
            // в Payee, значит и перезапись идёт туда — Note расхода затирается осознанно.
            var memo = string.IsNullOrWhiteSpace(MemoText) ? null : MemoText.Trim();
            var categoryId = SubCategories.FirstOrDefault(c => c.IsSelected)?.Id
                ?? RootCategories.FirstOrDefault(c => c.IsSelected)?.Id;

            // Категория не выбрана, но введён магазин — угадываем по прошлым покупкам.
            if (categoryId is null && memo is not null)
            {
                categoryId = _transactions.GuessCategoryId(memo);
            }

            tx.TransferAccountId = null;
            tx.TransferAmountMinor = null;
            tx.CategoryId = categoryId;
            tx.Payee = kind == TransactionKind.Expense ? memo : null;
            tx.Note = kind == TransactionKind.Expense ? null : memo;
        }

        try
        {
            _transactions.Save(tx);

            TransactionsChangedMessage.Broadcast();
        }
        catch (Exception ex)
        {
            await AlertAsync?.Invoke("Ошибка", ex.Message)!;
            return;
        }

        if (IsEditing || IsStandaloneRepayment)
        {
            if (CloseAsync is not null)
            {
                await CloseAsync();
            }

            return;
        }

        AmountText = "";
        MemoText = "";
        SetDefaultDate();
        _repaymentTarget = null;
        DebtButtonText = "Выбрать задолженность";
        if (IsRepayment) RefreshRepaymentTargets();
        RefreshBalance();
        RefreshDebtsSummary();
        ReloadCategories();

        if (WidgetSource == "widget")
        {
            // Экран с виджета открыт поверх вкладки «Ввод» — после сохранения возвращаемся на вкладку.
            await Shell.Current.Navigation.PopAsync();
        }
        else
        {
            // Тост не показываем на экране правки и на экране с виджета (страница закрывается).
            if (SavedAsync is not null)
            {
                await SavedAsync();
            }
        }
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

        if (tx.Kind == TransactionKind.DebtRepayment)
        {
            SetRepayment();
            SelectRepayment(RepaymentTargets.FirstOrDefault(t => t.Id == (tx.DebtId ?? tx.TransferAccountId)
                && t.IsCreditAccount == (tx.DebtId is null)));
        }
        else if (tx.Kind == TransactionKind.Income)
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
        var txDate = DateTimeOffset.FromUnixTimeSeconds(tx.DateUnix).ToLocalTime().DateTime;
        Date = txDate;
        _editTimeOfDay = txDate.TimeOfDay;
        MemoText = tx.Payee ?? tx.Note ?? "";

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
            SelectRoot(rootChip, trySelectChildId: null);
            return;
        }

        // Подкатегория другого корня: её нет в текущей ленте — ищем родителя в БД,
        // переключаемся на него и выделяем ребёнка через trySelectChildId.
        var category = _db.Categories.Get(categoryId);
        var parentId = category?.ParentId;
        if (parentId is null)
        {
            return;
        }

        var parentChip = RootCategories.FirstOrDefault(c => c.Id == parentId);
        if (parentChip is not null)
        {
            SelectRoot(parentChip, trySelectChildId: categoryId);
        }
    }

    private long ToUnix(DateTime local) =>
        new DateTimeOffset(local.Year, local.Month, local.Day, _editTimeOfDay.Hours, _editTimeOfDay.Minutes, 0,
            TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();

    partial void OnIsRepaymentChanged(bool value) => NotifyModeChanged();
    partial void OnIsIncomeChanged(bool value) => NotifyModeChanged();
    partial void OnIsTransferChanged(bool value) => NotifyModeChanged();

    private void SetDefaultDate()
    {
        _settingDate = true;
        Date = DateTime.Now;
        _settingDate = false;
        _dateExplicit = false;
    }

    partial void OnDateChanged(DateTime value)
    {
        if (!_settingDate) _dateExplicit = true;
        OnPropertyChanged(nameof(DateButtonText));
    }
}
