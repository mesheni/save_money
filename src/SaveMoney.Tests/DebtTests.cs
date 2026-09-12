using SaveMoney.Core.Models;
using Xunit;

namespace SaveMoney.Tests;

public class DebtTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    [Fact]
    public void PartialPayment_KeepsDebtActive()
    {
        var debt = new Debt { Direction = DebtDirection.OwedToMe, PersonName = "Петя", AmountMinor = 100_000 };
        _test.Db.Debts.Save(debt);

        _test.Db.Debts.AddPayment(new DebtPayment
        {
            DebtId = debt.Id,
            AmountMinor = 40_000,
            DateUnix = 2_000,
        });

        var updated = _test.Db.Debts.Get(debt.Id);
        Assert.NotNull(updated);
        Assert.Equal(DebtStatus.Active, updated.Status);
        Assert.Equal(60_000, _test.Db.Debts.GetRemaining(updated));
    }

    [Fact]
    public void FullPayment_SettlesDebt()
    {
        var debt = new Debt { Direction = DebtDirection.IOwe, PersonName = "Вася", AmountMinor = 50_000 };
        _test.Db.Debts.Save(debt);

        _test.Db.Debts.AddPayment(new DebtPayment { DebtId = debt.Id, AmountMinor = 50_000, DateUnix = 3_000 });

        var updated = _test.Db.Debts.Get(debt.Id);
        Assert.NotNull(updated);
        Assert.Equal(DebtStatus.Settled, updated.Status);
        Assert.Equal(3_000, updated.SettledDateUnix);

        Assert.DoesNotContain(updated, _test.Db.Debts.GetActive());
    }

    [Fact]
    public void GetActive_FiltersDeletedAndSettled()
    {
        var active = new Debt { PersonName = "А", AmountMinor = 100 };
        var settled = new Debt { PersonName = "Б", AmountMinor = 100, Status = DebtStatus.Settled };
        var deleted = new Debt { PersonName = "В", AmountMinor = 100 };
        _test.Db.Debts.Save(active);
        _test.Db.Debts.Save(settled);
        _test.Db.Debts.Save(deleted);
        _test.Db.Debts.SoftDelete(deleted);

        var list = _test.Db.Debts.GetActive();
        Assert.Equal([active.Id], list.Select(d => d.Id).ToList());
    }
}
