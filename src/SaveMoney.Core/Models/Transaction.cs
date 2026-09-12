using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>
/// Транзакция. AmountMinor всегда положительная, знак определяет Kind.
/// Для перевода: AccountId — откуда, TransferAccountId — куда.
/// </summary>
[Table("transactions")]
public class Transaction : BaseEntity
{
    [Indexed, Column("account_id")]
    public string AccountId { get; set; } = "";

    [Indexed, Column("category_id")]
    public string? CategoryId { get; set; }

    /// <summary>Сумма в копейках, всегда &gt; 0.</summary>
    [Column("amount_minor")]
    public long AmountMinor { get; set; }

    /// <summary>См. <see cref="TransactionKind"/>.</summary>
    [Column("kind")]
    public string Kind { get; set; } = TransactionKind.Expense;

    [Indexed, Column("date_unix")]
    public long DateUnix { get; set; }

    [Column("note"), MaxLength(500)]
    public string? Note { get; set; }

    /// <summary>Магазин/получатель — по нему угадывается категория.</summary>
    [Column("payee"), MaxLength(200)]
    public string? Payee { get; set; }

    /// <summary>См. <see cref="TransactionSource"/>.</summary>
    [Column("source")]
    public string Source { get; set; } = TransactionSource.Manual;

    [Indexed, Column("transfer_account_id")]
    public string? TransferAccountId { get; set; }

    /// <summary>Задан, если перевод между счетами с разными суммами (комиссия/курс).</summary>
    [Column("transfer_amount_minor")]
    public long? TransferAmountMinor { get; set; }

    // --- Поля для парсера банковских уведомлений (v1.1) ---

    [Column("raw_text"), MaxLength(1000)]
    public string? RawText { get; set; }

    [Column("bank_code"), MaxLength(50)]
    public string? BankCode { get; set; }

    /// <summary>Черновик, распознанный парсером и ждущий подтверждения пользователем.</summary>
    [Column("is_pending_review")]
    public bool IsPendingReview { get; set; }
}
