using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>
/// Бюджет-лимит. CategoryId == null — общий лимит на период.
/// Период задаётся якорной датой и длиной в днях: 30 — «как месяц», 14 — смена, 15 — полмесяца.
/// Это же механизм «сменного графика» и произвольного отчётного периода.
/// </summary>
[Table("budgets")]
public class Budget : BaseEntity
{
    [Column("category_id")]
    public string? CategoryId { get; set; }

    [Column("amount_minor")]
    public long AmountMinor { get; set; }

    /// <summary>Начало первого периода (unix). Текущий период вычисляется от якоря.</summary>
    [Column("period_start_anchor_unix")]
    public long PeriodStartAnchorUnix { get; set; }

    [Column("period_days")]
    public int PeriodDays { get; set; } = 30;
}
