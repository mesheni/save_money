using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>Регулярный платёж (ЖКХ, подписки, аренда): шаблон транзакции + расписание.</summary>
[Table("recurring_payments")]
public class RecurringPayment : BaseEntity
{
    [Column("account_id")]
    public string AccountId { get; set; } = "";

    [Column("category_id")]
    public string? CategoryId { get; set; }

    [Column("amount_minor")]
    public long AmountMinor { get; set; }

    /// <summary>См. <see cref="TransactionKind"/> (обычно expense).</summary>
    [Column("kind")]
    public string Kind { get; set; } = TransactionKind.Expense;

    [Column("payee"), MaxLength(200)]
    public string? Payee { get; set; }

    [Column("note"), MaxLength(500)]
    public string? Note { get; set; }

    /// <summary>monthly — в конкретный день месяца, weekly, every_n_days.</summary>
    [Column("recurrence_type")]
    public string RecurrenceType { get; set; } = "monthly";

    /// <summary>День месяца для monthly (1–31). Для остальных — 0.</summary>
    [Column("day_of_month")]
    public int DayOfMonth { get; set; }

    /// <summary>Шаг в днях для every_n_days.</summary>
    [Column("interval_days")]
    public int IntervalDays { get; set; }

    [Column("next_date_unix")]
    public long NextDateUnix { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;
}
