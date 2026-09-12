using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>Долг: «мне должны» или «я должен». Погашения — <see cref="DebtPayment"/>.</summary>
[Table("debts")]
public class Debt : BaseEntity
{
    /// <summary>См. <see cref="DebtDirection"/>.</summary>
    [Column("direction")]
    public string Direction { get; set; } = DebtDirection.OwedToMe;

    [Column("person_name"), MaxLength(200)]
    public string PersonName { get; set; } = "";

    /// <summary>Полная сумма долга в копейках.</summary>
    [Column("amount_minor")]
    public long AmountMinor { get; set; }

    [Column("due_date_unix")]
    public long? DueDateUnix { get; set; }

    /// <summary>Счёт, с которым связан долг: куда вернут деньги или откуда взял в долг.</summary>
    [Column("account_id")]
    public string? AccountId { get; set; }

    /// <summary>См. <see cref="DebtStatus"/>.</summary>
    [Column("status")]
    public string Status { get; set; } = DebtStatus.Active;

    [Column("settled_date_unix")]
    public long? SettledDateUnix { get; set; }

    [Column("note"), MaxLength(500)]
    public string? Note { get; set; }
}
