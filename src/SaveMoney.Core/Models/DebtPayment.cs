using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>Частичное погашение долга.</summary>
[Table("debt_payments")]
public class DebtPayment : BaseEntity
{
    [Indexed, Column("debt_id")]
    public string DebtId { get; set; } = "";

    [Column("amount_minor")]
    public long AmountMinor { get; set; }

    [Column("date_unix")]
    public long DateUnix { get; set; }

    [Column("note"), MaxLength(500)]
    public string? Note { get; set; }
}
