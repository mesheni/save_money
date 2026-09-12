using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>
/// Регулярные платежи: вычисление следующей даты и автосоздание транзакций
/// по наступившим срокам (вызывается при старте приложения).
/// </summary>
public sealed class RecurringService(AppDatabase database, TransactionService transactions)
{
    private readonly AppDatabase _db = database;
    private readonly TransactionService _transactions = transactions;

    public long ComputeNextDateUnix(RecurringPayment recurring, long fromDateUnix)
    {
        var from = DateTimeOffset.FromUnixTimeSeconds(fromDateUnix).LocalDateTime;
        var next = recurring.RecurrenceType switch
        {
            "weekly" => from.AddDays(7),
            "every_n_days" => from.AddDays(Math.Max(1, recurring.IntervalDays)),
            _ => NextMonthly(from, recurring.DayOfMonth),
        };
        return new DateTimeOffset(next, TimeZoneInfo.Local.GetUtcOffset(next)).ToUnixTimeSeconds();
    }

    private static DateTime NextMonthly(DateTime from, int dayOfMonth)
    {
        if (dayOfMonth < 1)
        {
            dayOfMonth = 1;
        }

        var year = from.Year;
        var month = from.Month;
        while (true)
        {
            var candidate = new DateTime(year, month, Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month)),
                from.Hour, from.Minute, from.Second);
            if (candidate > from)
            {
                return candidate;
            }

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }
    }

    public List<RecurringPayment> GetDue(long nowUnix) =>
        _db.RecurringPayments.GetAllActive()
          .Where(r => r.IsActive && r.NextDateUnix <= nowUnix)
          .ToList();

    /// <summary>Регулярные платежи, срок которых попадает в [from, to) — для дашборда «до зарплаты».</summary>
    public List<RecurringPayment> GetUpcomingBetween(long fromUnix, long toUnix) =>
        _db.RecurringPayments.GetAllActive()
          .Where(r => r.IsActive && r.NextDateUnix >= fromUnix && r.NextDateUnix < toUnix)
          .ToList();

    /// <summary>
    /// Создаёт транзакции по наступившим срокам и переносит next_date в будущее.
    /// Если приложение не открывали несколько периодов, транзакция создаётся одна
    /// (на самый ранний просроченный срок), next_date догоняет все пропуски.
    /// Возвращает число созданных транзакций. Идемпотентно при повторном вызове.
    /// </summary>
    public int MaterializeDue(long nowUnix)
    {
        var created = 0;
        foreach (var recurring in GetDue(nowUnix))
        {
            _transactions.Save(new Transaction
            {
                AccountId = recurring.AccountId,
                CategoryId = recurring.CategoryId,
                AmountMinor = recurring.AmountMinor,
                Kind = recurring.Kind,
                Payee = recurring.Payee,
                Note = recurring.Note,
                DateUnix = recurring.NextDateUnix,
                Source = TransactionSource.Recurring,
            });
            created++;

            var next = recurring.NextDateUnix;
            while (next <= nowUnix)
            {
                next = ComputeNextDateUnix(recurring, next);
            }

            recurring.NextDateUnix = next;
            _db.RecurringPayments.Save(recurring);
        }

        return created;
    }
}
