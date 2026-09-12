using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using Xunit;

namespace SaveMoney.Tests;

public class CycleAndRecurringTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    // --- CycleService ---

    [Fact]
    public void Cycle_DefaultAnchor_IsFirstOfMonth_With30DaySteps()
    {
        var service = new CycleService(_test.Db);
        var cycle = service.GetCurrentCycle();

        var now = DateTime.Now.Date;
        Assert.Equal(new DateTime(now.Year, now.Month, 1), cycle.Start);
        Assert.Equal(30, cycle.TotalDays);
        Assert.True(cycle.End > now);
        Assert.True(cycle.Start <= now);
    }

    [Fact]
    public void Cycle_CustomAnchorAndDays_StepsForward()
    {
        var service = new CycleService(_test.Db);
        service.SetSettings(new DateTime(2026, 9, 1), 7);

        // Сегодняшняя дата может быть любой; цикл с якорем 01.09 длиной 7 дней
        // содержит текущую дату: start <= today < start+7
        var cycle = service.GetCurrentCycle();
        var today = DateTime.Now.Date;
        Assert.True(cycle.Start <= today && today < cycle.End);
        Assert.Equal(7, cycle.TotalDays);
        Assert.Equal(0, (cycle.Start - new DateTime(2026, 9, 1)).Days % 7);
    }

    [Fact]
    public void Payday_NextDate_And_DaysLeft()
    {
        var service = new CycleService(_test.Db);
        service.SetPaydayDay(5);

        var now = new DateTime(2026, 9, 12, 10, 0, 0);
        Assert.Equal(new DateTime(2026, 10, 5), service.GetNextPayday(now));
        Assert.Equal(23, service.GetDaysUntilPayday(now));

        service.SetPaydayDay(20);
        Assert.Equal(new DateTime(2026, 9, 20), service.GetNextPayday(now));
    }

    [Fact]
    public void Payday_Day_ClampedTo28()
    {
        var service = new CycleService(_test.Db);
        service.SetPaydayDay(31);
        Assert.Equal(28, service.GetPaydayDay());
    }

    // --- RecurringService ---

    private static long Unix(DateTime local) =>
        new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();

    [Fact]
    public void Recurring_MonthlyNextDate_HandlesMonthLengths()
    {
        var service = MakeService();

        var r = new RecurringPayment { RecurrenceType = "monthly", DayOfMonth = 15 };
        var fromSep1 = Unix(new DateTime(2026, 9, 1, 12, 0, 0));
        Assert.Equal(new DateTime(2026, 9, 15, 12, 0, 0),
            DateTimeOffset.FromUnixTimeSeconds(service.ComputeNextDateUnix(r, fromSep1)).LocalDateTime);

        var fromSep20 = Unix(new DateTime(2026, 9, 20, 12, 0, 0));
        Assert.Equal(new DateTime(2026, 10, 15, 12, 0, 0),
            DateTimeOffset.FromUnixTimeSeconds(service.ComputeNextDateUnix(r, fromSep20)).LocalDateTime);

        // 30-е число в феврале сжимается до конца месяца
        var r30 = new RecurringPayment { RecurrenceType = "monthly", DayOfMonth = 30 };
        var fromFeb1 = Unix(new DateTime(2027, 2, 1, 12, 0, 0));
        Assert.Equal(new DateTime(2027, 2, 28, 12, 0, 0),
            DateTimeOffset.FromUnixTimeSeconds(service.ComputeNextDateUnix(r30, fromFeb1)).LocalDateTime);
    }

    [Fact]
    public void Recurring_WeeklyAndEveryNDays()
    {
        var service = MakeService();
        var from = Unix(new DateTime(2026, 9, 12, 12, 0, 0));

        var weekly = new RecurringPayment { RecurrenceType = "weekly" };
        Assert.Equal(new DateTime(2026, 9, 19, 12, 0, 0),
            DateTimeOffset.FromUnixTimeSeconds(service.ComputeNextDateUnix(weekly, from)).LocalDateTime);

        var every5 = new RecurringPayment { RecurrenceType = "every_n_days", IntervalDays = 5 };
        Assert.Equal(new DateTime(2026, 9, 17, 12, 0, 0),
            DateTimeOffset.FromUnixTimeSeconds(service.ComputeNextDateUnix(every5, from)).LocalDateTime);
    }

    [Fact]
    public void Recurring_MaterializeDue_CreatesTransaction_AndAdvances_Idempotently()
    {
        var service = MakeService();
        var account = new Account { Name = "Карта" };
        _test.Db.Accounts.Save(account);

        var nextDate = Unix(DateTime.Now.AddDays(-2));
        _test.Db.RecurringPayments.Save(new RecurringPayment
        {
            AccountId = account.Id,
            AmountMinor = 9_900,
            Kind = TransactionKind.Expense,
            Payee = "Netflix",
            RecurrenceType = "monthly",
            DayOfMonth = 10,
            NextDateUnix = nextDate,
        });

        var created = service.MaterializeDue(DateTimeOffset.Now.ToUnixTimeSeconds());
        Assert.Equal(1, created);

        var tx = Assert.Single(_test.Db.Transactions.GetRecent());
        Assert.Equal(9_900, tx.AmountMinor);
        Assert.Equal(TransactionSource.Recurring, tx.Source);
        Assert.Equal("Netflix", tx.Payee);

        // next_date ушёл в будущее — повторный вызов ничего не создаёт
        var saved = Assert.Single(_test.Db.RecurringPayments.GetAllActive());
        Assert.True(saved.NextDateUnix > DateTimeOffset.Now.ToUnixTimeSeconds());
        Assert.Equal(0, service.MaterializeDue(DateTimeOffset.Now.ToUnixTimeSeconds()));
    }

    private RecurringService MakeService()
    {
        var transactions = new TransactionService(_test.Db);
        return new RecurringService(_test.Db, transactions);
    }
}
