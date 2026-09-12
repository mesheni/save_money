using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>Строка списка счетов с балансом.</summary>
public partial class AccountRow : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public string? ColorHex { get; init; }
    public required bool IsArchived { get; init; }

    [ObservableProperty]
    public partial string BalanceText { get; set; } = "";

    public string Icon => Kind switch
    {
        AccountKind.Cash => "💵",
        AccountKind.Card => "💳",
        AccountKind.Bank => "🏦",
        _ => "💰",
    };

    public string KindText => Kind switch
    {
        AccountKind.Cash => "наличные",
        AccountKind.Card => "карта",
        AccountKind.Bank => "банк. счёт",
        _ => "",
    };

    public string ArchiveBadge => IsArchived ? " · в архиве" : "";

    public string Subtitle => $"{KindText}{ArchiveBadge}";
}

public partial class AccountsViewModel(AppDatabase db, BalanceService balance) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly BalanceService _balance = balance;

    public ObservableCollection<AccountRow> Rows { get; } = [];

    [ObservableProperty]
    public partial string TotalText { get; set; } = "";

    public Task InitializeAsync()
    {
        Rows.Clear();
        foreach (var account in _db.Accounts.GetAll())
        {
            Rows.Add(new AccountRow
            {
                Id = account.Id,
                Name = account.Name,
                Kind = account.Kind,
                ColorHex = account.Color,
                IsArchived = account.Archived,
                BalanceText = MoneyFormat.Rubles(_balance.GetAccountBalance(account)),
            });
        }

        TotalText = $"Всего: {MoneyFormat.Rubles(_balance.GetTotalBalance())}";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void Add() => Shell.Current.GoToAsync("account");

    [RelayCommand]
    private void Edit(AccountRow? row)
    {
        if (row is not null)
        {
            Shell.Current.GoToAsync($"account?id={row.Id}");
        }
    }

    public async Task<bool> DeleteAsync(AccountRow? row)
    {
        if (row is null)
        {
            return false;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Удалить счёт?",
            $"«{row.Name}» исчезнет из списков. Транзакции по нему останутся в истории.",
            "Удалить", "Отмена");
        if (!confirmed)
        {
            return false;
        }

        var account = _db.Accounts.Get(row.Id);
        if (account is not null)
        {
            _db.Accounts.SoftDelete(account);
        }

        await InitializeAsync();
        return true;
    }
}
