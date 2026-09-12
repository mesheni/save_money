using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>
/// Правило «магазин → категория»: подстрока в Payee без учёта регистра.
/// Заполняется вручную и/или автоматически из истории ввода.
/// </summary>
[Table("merchant_rules")]
public class MerchantRule : BaseEntity
{
    [Column("pattern"), MaxLength(200)]
    public string Pattern { get; set; } = "";

    [Column("category_id")]
    public string CategoryId { get; set; } = "";

    /// <summary>Счёт по умолчанию для этого мерчанта (карта, с которой обычно платим).</summary>
    [Column("account_id")]
    public string? AccountId { get; set; }

    /// <summary>Счётчик использования — правило с большим весом выигрывает при конфликте.</summary>
    [Column("use_count")]
    public int UseCount { get; set; }
}
