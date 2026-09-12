using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>Категория. ParentId == null — корневая, иначе подкатегория (один уровень вложенности).</summary>
[Table("categories")]
public class Category : BaseEntity
{
    [Indexed, Column("parent_id")]
    public string? ParentId { get; set; }

    [Column("name"), MaxLength(100)]
    public string Name { get; set; } = "";

    /// <summary>См. <see cref="CategoryKind"/>.</summary>
    [Column("kind")]
    public string Kind { get; set; } = CategoryKind.Expense;

    [Column("icon"), MaxLength(20)]
    public string? Icon { get; set; }

    [Column("color"), MaxLength(20)]
    public string? Color { get; set; }

    [Column("sort")]
    public int Sort { get; set; }

    /// <summary>Системная категория из сида: нельзя удалять, можно переименовывать.</summary>
    [Column("is_default")]
    public bool IsDefault { get; set; }
}
