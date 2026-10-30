using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using SaveMoney.Messaging;

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

    /// <summary>Перевод-зачисление: нейтральный цвет суммы (деньги не покидали кошелёк).</summary>
    public required bool IsNeutral { get; init; }
}

/// <summary>Группа дня — сама коллекция строк, чтобы использовать сгруппированный CollectionView.</summary>
public partial class HistoryGroupVM : ObservableCollection<HistoryItemVM>
{
    public HistoryGroupVM(IEnumerable<HistoryItemVM> items) : base(items)
    {
    }

    private DateTime _date;

    public required DateTime Date
    {
        init
        {
            _date = value;
            // Текст фиксируется при построении группы (в Refresh), а не в геттере
            // биндинга: список, переживший полночь на экране, не должен зависеть
            // от момента, когда CollectionView переоценит геттер.
            var day = value.ToString("d MMMM");
            HeaderTitleText = value.Date == DateTime.Today ? $"Сегодня, {day}"
                : value.Date == DateTime.Today.AddDays(-1) ? $"Вчера, {day}"
                : day;
        }
        get => _date;
    }

    public string HeaderTitleText { get; private set; } = "";

    /// <summary>Итог дня: доходы минус расходы (переводы не считаются).</summary>
    public string DayTotalText => MoneyFormat.SignedRubles(IncomeMinor - ExpenseMinor);

    public long IncomeMinor { get; init; }
    public long ExpenseMinor { get; init; }
}

public partial class HistoryViewModel(AppDatabase db, HistoryService history) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly HistoryService _history = history;
    private readonly List<string> _accountIds = [];
    private bool _loaded;
    private bool _needsRefresh = true;
    private DateTime _refreshDay;

    /// <summary>Id категории → фильтр (сама категория и её подкатегории).</summary>
    private string? _categoryId;

    /// <summary>Id → вариант ActionSheet (подкатегории помечены «— »).</summary>
    private readonly List<(string? Id, string Option)> _categoryOptions = [];

    public ObservableCollection<HistoryGroupVM> Groups { get; } = [];

    public ObservableCollection<string> AccountOptions { get; } = ["Все счета"];

    private static readonly string[] PeriodList = ["Неделя", "Месяц", "3 месяца", "Всё"];

    public IReadOnlyList<string> PeriodOptions => PeriodList;

    [ObservableProperty]
    public partial int PeriodIndex { get; set; } = 1;

    [ObservableProperty]
    public partial int AccountIndex { get; set; }

    [ObservableProperty]
    public partial bool IsSearchVisible { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; } = "";

    partial void OnSearchTextChanged(string value) => Refresh();

    [ObservableProperty]
    public partial bool HasItems { get; set; } = true;

    /// <summary>Пустой список. Отдельное положительное свойство: биндинг с «!»
    /// (IsVisible="{Binding !HasItems}") не выживает при SourceGen-инфляторе —
    /// надпись «Пока пусто» рисовалась поверх непустого списка.</summary>
    public bool HasNoRows => !HasItems;

    [ObservableProperty]
    public partial string EmptyStateText { get; set; } = "";

    partial void OnHasItemsChanged(bool value) => OnPropertyChanged(nameof(HasNoRows));

    // --- Пилюли-фильтры ---
    public string PeriodPillText => PeriodOptions[PeriodIndex];
    public bool PeriodPillActive => PeriodIndex != 1;
    public string AccountPillText => AccountOptions[AccountIndex];
    public bool AccountPillActive => AccountIndex > 0;

    [ObservableProperty]
    public partial string CategoryPillText { get; set; } = "Категории";

    public bool CategoryPillActive => _categoryId is not null;

    // --- Сводка за период (доходы/расходы, переводы не считаются) ---
    [ObservableProperty]
    public partial string IncomeSummaryText { get; set; } = "";

    [ObservableProperty]
    public partial string ExpenseSummaryText { get; set; } = "";

    [ObservableProperty]
    public partial string DiffSummaryText { get; set; } = "";

    /// <summary>Страница подставляет выбор варианта (ActionSheet): (заголовок, варианты) → выбор или null.</summary>
    public Func<string, string[], Task<string?>>? PickAsync { get; set; }

    public async Task InitializeAsync()
    {
        if (_loaded && !_needsRefresh && _refreshDay == DateTime.Today) return;
        if (!_loaded)
            CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger.Default.Register<HistoryViewModel, TransactionsChangedMessage>(
                this, static (recipient, _) => recipient._needsRefresh = true);
        _needsRefresh = false;
        _refreshDay = DateTime.Today;
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

    partial void OnPeriodIndexChanged(int value)
    {
        OnPropertyChanged(nameof(PeriodPillText));
        OnPropertyChanged(nameof(PeriodPillActive));
        Refresh();
    }

    partial void OnAccountIndexChanged(int value)
    {
        OnPropertyChanged(nameof(AccountPillText));
        OnPropertyChanged(nameof(AccountPillActive));
        Refresh();
    }

    partial void OnIsSearchVisibleChanged(bool value)
    {
        // Закрыли поиск — сбрасываем запрос, чтобы список вернулся к полному.
        if (!value)
        {
            SearchText = "";
        }
    }

    [RelayCommand]
    private void ToggleSearch() => IsSearchVisible = !IsSearchVisible;

    [RelayCommand]
    private async Task PickPeriodAsync()
    {
        var picked = await PickAsync?.Invoke("Период", [.. PeriodOptions])!;
        if (picked is null)
        {
            return;
        }

        var index = Array.IndexOf(PeriodList, picked);
        if (index >= 0)
        {
            PeriodIndex = index;
        }
    }

    [RelayCommand]
    private async Task PickAccountAsync()
    {
        var picked = await PickAsync?.Invoke("Счёт", [.. AccountOptions])!;
        if (picked is null)
        {
            return;
        }

        var index = AccountOptions.IndexOf(picked);
        if (index >= 0)
        {
            AccountIndex = index;
        }
    }

    [RelayCommand]
    private async Task PickCategoryAsync()
    {
        _categoryOptions.Clear();
        _categoryOptions.Add((null, "Все категории"));
        foreach (var root in _db.Categories.GetRoots(CategoryKind.Expense))
        {
            _categoryOptions.Add((root.Id, root.Name));
            foreach (var child in _db.Categories.GetChildren(root.Id))
            {
                _categoryOptions.Add((child.Id, $"—  {child.Name}"));
            }
        }

        var picked = await PickAsync?.Invoke("Категории", [.. _categoryOptions.Select(o => o.Option)])!;
        if (picked is null)
        {
            return;
        }

        _categoryId = _categoryOptions.FirstOrDefault(o => o.Option == picked).Id;
        CategoryPillText = picked == "Все категории" ? "Категории" : picked.TrimStart('—', ' ');
        OnPropertyChanged(nameof(CategoryPillActive));
        Refresh();
    }

    [RelayCommand]
    public async Task DeleteAsync(HistoryItemVM? item)
    {
        if (item is null)
        {
            return;
        }

        // Свайп легко зацепить случайно, а удаление убивает транзакцию безвозвратно.
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить операцию?", $"«{item.Title}» на {item.AmountText} будет удалена.", "Удалить", "Отмена");
        if (!confirmed)
        {
            return;
        }

        var tx = _db.Transactions.Get(item.Id);
        if (tx is not null)
        {
            new RepaymentService(_db).Delete(tx);
            TransactionsChangedMessage.Broadcast();
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

        // Фильтр по категории: сама категория и её подкатегории.
        List<string>? categoryIds = null;
        if (_categoryId is not null)
        {
            categoryIds = [_categoryId, .. _db.Categories.GetChildren(_categoryId).Select(c => c.Id)];
        }

        var search = SearchText.Trim();
        if (search.Length > 0 || categoryIds is not null)
        {
            foreach (var g in groups)
            {
                var items = g.Items.AsEnumerable();
                if (categoryIds is not null)
                {
                    items = items.Where(i => i.CategoryId is not null && categoryIds.Contains(i.CategoryId));
                }
                if (search.Length > 0)
                {
                    items = items.Where(i =>
                        i.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                        || (i.Subtitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
                }

                var filteredItems = items.ToList();
                ((List<HistoryItem>)g.Items).Clear();
                ((List<HistoryItem>)g.Items).AddRange(filteredItems);
            }
        }

        Groups.Clear();
        long income = 0, expense = 0;
        foreach (var g in groups)
        {
            // Дни, опустевшие после фильтрации, не показываем совсем.
            if (g.Items.Count == 0)
            {
                continue;
            }

            var visibleItems = g.Items.ToList();
            income += visibleItems.Where(i => i.Kind == TransactionKind.Income).Sum(i => i.AmountMinor);
            expense += visibleItems.Where(i => i.Kind == TransactionKind.Expense).Sum(i => i.AmountMinor);
            Groups.Add(new HistoryGroupVM(visibleItems.Select(i => new HistoryItemVM
            {
                Id = i.Id,
                Icon = i.Icon,
                Title = i.Title,
                Subtitle = i.Subtitle,
                // Перевод в общем списке нейтрален: деньги не покидали кошелёк — без знака.
                AmountText = i.IsNeutralTransfer ? MoneyFormat.Rubles(i.AmountMinor) : MoneyFormat.SignedRubles(i.SignedAmountMinor),
                IsExpense = i.IsExpense,
                IsNeutral = i.IsNeutralTransfer,
            }))
            {
                Date = g.Date,
                IncomeMinor = visibleItems.Where(i => i.Kind == TransactionKind.Income).Sum(i => i.AmountMinor),
                ExpenseMinor = visibleItems.Where(i => i.Kind == TransactionKind.Expense).Sum(i => i.AmountMinor),
            });
        }

        HasItems = Groups.Count > 0;
        EmptyStateText = search.Length > 0 || categoryIds is not null || AccountIndex > 0
            ? "По вашему запросу ничего не найдено"
            : _db.Transactions.GetRecent(1).Count == 0
                ? "Пока пусто — добавьте первую операцию на вкладке «Ввод»"
                : "За выбранный период операций нет";
        IncomeSummaryText = MoneyFormat.SignedRubles(income);
        ExpenseSummaryText = MoneyFormat.SignedRubles(-expense);
        DiffSummaryText = MoneyFormat.SignedRubles(income - expense);
    }

    private Task RefreshFromServiceAsync()
    {
        Refresh();
        return Task.CompletedTask;
    }
}
