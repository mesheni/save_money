using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public class TransactionAndBalanceTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private Account AddAccount(string name, long initialMinor = 0)
    {
        var account = new Account { Name = name, InitialBalanceMinor = initialMinor };
        _test.Db.Accounts.Save(account);
        return account;
    }

    private void AddTransaction(Account account, string kind, long amountMinor,
        string? category = null, long dateUnix = 1_000, string? transferTo = null)
    {
        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = account.Id,
            CategoryId = category,
            AmountMinor = amountMinor,
            Kind = kind,
            DateUnix = dateUnix,
            TransferAccountId = transferTo,
        });
    }

    [Fact]
    public void Balance_IncomeExpenseAndTransfer()
    {
        var balanceService = new BalanceService(_test.Db);
        var cash = AddAccount("Наличные", initialMinor: 50_000);
        var card = AddAccount("Карта");

        AddTransaction(cash, TransactionKind.Income, 100_000);
        AddTransaction(cash, TransactionKind.Expense, 30_000, dateUnix: 1_100);
        AddTransaction(cash, TransactionKind.Transfer, 20_000, dateUnix: 1_200, transferTo: card.Id);
        AddTransaction(cash, TransactionKind.Expense, 5_000, dateUnix: 1_300);

        // 500 + 1000 - 300 - 200 - 50 = 950 ₽ наличными
        Assert.Equal(95_000, balanceService.GetAccountBalance(cash));
        // 200 ₽ пришло переводом
        Assert.Equal(20_000, balanceService.GetAccountBalance(card));
        // Общий = 950 + 200 = 1150 ₽
        Assert.Equal(115_000, balanceService.GetTotalBalance());
    }

    [Fact]
    public void SoftDelete_ExcludesTransactionFromBalance()
    {
        var balanceService = new BalanceService(_test.Db);
        var account = AddAccount("Карта");

        var tx = new Transaction { AccountId = account.Id, AmountMinor = 42_000, Kind = TransactionKind.Expense };
        _test.Db.Transactions.Save(tx);
        Assert.Equal(-42_000, balanceService.GetAccountBalance(account));

        _test.Db.Transactions.SoftDelete(tx);
        Assert.Equal(0, balanceService.GetAccountBalance(account));
        Assert.Equal(0, _test.Db.Transactions.CountAll());
    }

    [Fact]
    public void GetRecent_OrdersByDateDescending()
    {
        var account = AddAccount("Карта");

        AddTransaction(account, TransactionKind.Expense, 100, dateUnix: 100);
        AddTransaction(account, TransactionKind.Expense, 200, dateUnix: 300);
        AddTransaction(account, TransactionKind.Expense, 300, dateUnix: 200);

        var recent = _test.Db.Transactions.GetRecent();

        Assert.Equal([300, 200, 100], recent.Select(t => t.DateUnix).ToList());
    }

    [Fact]
    public void Transfer_WithDifferentAmount_UsesTransferAmount()
    {
        var balanceService = new BalanceService(_test.Db);
        var from = AddAccount("Откуда");
        var to = AddAccount("Куда");

        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = from.Id,
            TransferAccountId = to.Id,
            AmountMinor = 100_000,
            TransferAmountMinor = 99_000, // 100 ₽ комиссия
            Kind = TransactionKind.Transfer,
        });

        Assert.Equal(-100_000, balanceService.GetAccountBalance(from));
        Assert.Equal(99_000, balanceService.GetAccountBalance(to));
    }
}
