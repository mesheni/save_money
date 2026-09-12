using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>
/// Базовая сущность, синхронизируемая в будущем (v2.0):
/// GUID-ключ, метки времени в UTC и мягкое удаление вместо DELETE.
/// Все имена колонок заданы явно в snake_case — raw-SQL в сервисах не зависит от имён свойств.
/// </summary>
public abstract class BaseEntity
{
    [PrimaryKey, Column("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Indexed, Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [Indexed, Column("is_deleted")]
    public bool IsDeleted { get; set; }
}
