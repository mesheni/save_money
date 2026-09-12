using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>Счёт: наличные, карта, банковский счёт.</summary>
[Table("accounts")]
public class Account : BaseEntity
{
    [Column("name"), MaxLength(100)]
    public string Name { get; set; } = "";

    /// <summary>См. <see cref="AccountKind"/>.</summary>
    [Column("kind")]
    public string Kind { get; set; } = AccountKind.Cash;

    /// <summary>Начальный остаток в копейках (может быть отрицательным — кредитка).</summary>
    [Column("initial_balance_minor")]
    public long InitialBalanceMinor { get; set; }

    [Column("include_in_total")]
    public bool IncludeInTotal { get; set; } = true;

    [Column("archived")]
    public bool Archived { get; set; }

    [Column("color"), MaxLength(20)]
    public string? Color { get; set; }

    [Column("icon"), MaxLength(20)]
    public string? Icon { get; set; }

    [Column("sort")]
    public int Sort { get; set; }
}
