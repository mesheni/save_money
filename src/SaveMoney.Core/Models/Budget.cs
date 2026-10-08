using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>
/// Бюджет-лимит. CategoryId == null — общий лимит на период.
/// Период берётся из цикла «до зарплаты» на момент расчёта (BudgetService.GetStatuses).
/// </summary>
[Table("budgets")]
public class Budget : BaseEntity
{
    [Column("category_id")]
    public string? CategoryId { get; set; }

    [Column("amount_minor")]
    public long AmountMinor { get; set; }
}
