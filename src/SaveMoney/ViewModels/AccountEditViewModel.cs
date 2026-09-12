using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

[QueryProperty(nameof(AccountId), "id")]
public partial class AccountEditViewModel(AppDatabase db) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private string? _id;
    private Account? _editing;

    public static readonly string[] Palette =
    [
        "#3498DB", "#2ECC71", "#1ABC9C", "#9B59B6",
        "#E74C3C", "#E67E22", "#F1C40F", "#34495E",
    ];

    public ObservableCollection<ColorChip> Colors { get; } = [];

    public IReadOnlyList<string> KindOptions { get; } = ["Наличные", "Карта", "Банковский счёт"];

    [ObservableProperty]
    public partial string Title { get; set; } = "Новый счёт";

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial int KindIndex { get; set; } = 1;

    [ObservableProperty]
    public partial string InitialBalanceText { get; set; } = "";

    [ObservableProperty]
    public partial bool IncludeInTotal { get; set; } = true;

    [ObservableProperty]
    public partial bool Archived { get; set; }

    [ObservableProperty]
    public partial bool IsExisting { get; set; }

    public Func<string, string, Task>? AlertAsync { get; set; }

    public string? AccountId
    {
        get => _id;
        set => _id = value;
    }

    public Task InitializeAsync()
    {
        if (Colors.Count == 0)
        {
            foreach (var hex in Palette)
            {
                Colors.Add(new ColorChip { Hex = hex });
            }
        }

        if (_id is null || _editing is not null)
        {
            if (_editing is null)
            {
                SelectColor(Palette[0]);
            }

            return Task.CompletedTask;
        }

        var account = _db.Accounts.Get(_id);
        if (account is null)
        {
            return Task.CompletedTask;
        }

        _editing = account;
        IsExisting = true;
        Title = "Счёт";
        Name = account.Name;
        KindIndex = account.Kind switch
        {
            AccountKind.Cash => 0,
            AccountKind.Card => 1,
            AccountKind.Bank => 2,
            _ => 1,
        };
        InitialBalanceText = MoneyFormat.ForInput(account.InitialBalanceMinor);
        IncludeInTotal = account.IncludeInTotal;
        Archived = account.Archived;
        SelectColor(account.Color ?? Palette[0]);

        return Task.CompletedTask;
    }

    private void SelectColor(string hex)
    {
        foreach (var chip in Colors)
        {
            chip.IsSelected = chip.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void SelectColor(ColorChip? chip)
    {
        if (chip is not null)
        {
            SelectColor(chip.Hex);
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await AlertAsync?.Invoke("Ошибка", "Введите название счёта")!;
            return;
        }

        var kind = KindIndex switch
        {
            0 => AccountKind.Cash,
            2 => AccountKind.Bank,
            _ => AccountKind.Card,
        };

        var account = _editing ?? new Account();
        account.Name = Name.Trim();
        account.Kind = kind;
        account.InitialBalanceMinor = MoneyFormat.ParseInput(InitialBalanceText);
        account.IncludeInTotal = IncludeInTotal;
        account.Archived = Archived;
        account.Color = Colors.FirstOrDefault(c => c.IsSelected)?.Hex;

        _db.Accounts.Save(account);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (_editing is null)
        {
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить счёт?",
            $"«{_editing.Name}» исчезнет из списков. Транзакции по нему останутся в истории.",
            "Удалить", "Отмена");
        if (!confirmed)
        {
            return;
        }

        _db.Accounts.SoftDelete(_editing);
        await Shell.Current.GoToAsync("..");
    }
}
