using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>
/// Журнал обработанных банковских уведомлений (v1.1): дедупликация по хэшу текста,
/// чтобы одно уведомление (показанное дважды системой) не породило две транзакции.
/// </summary>
[Table("notification_log")]
public class NotificationLogEntry : BaseEntity
{
    /// <summary>SHA-256 текста уведомления в hex.</summary>
    [Column("hash"), MaxLength(64)]
    public string Hash { get; set; } = "";

    [Column("app_package"), MaxLength(200)]
    public string AppPackage { get; set; } = "";

    [Column("bank_code"), MaxLength(50)]
    public string? BankCode { get; set; }

    [Column("received_unix")]
    public long ReceivedUnix { get; set; }

    [Column("transaction_id")]
    public string? TransactionId { get; set; }
}
