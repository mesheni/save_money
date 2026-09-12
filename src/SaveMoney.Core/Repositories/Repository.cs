using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

/// <summary>Базовый репозиторий: сохранение с UpdatedAt и мягкое удаление вместо DELETE.</summary>
public class Repository<T>(SQLiteConnection db) where T : BaseEntity, new()
{
    protected readonly SQLiteConnection Db = db;

    public virtual T? Get(string id) =>
        Db.Table<T>().FirstOrDefault(t => t.Id == id && !t.IsDeleted);

    public virtual void Save(T entity)
    {
        entity.UpdatedAt = DateTime.UtcNow;
        if (Db.Update(entity) == 0)
        {
            Db.Insert(entity);
        }
    }

    public virtual void SoftDelete(T entity)
    {
        entity.IsDeleted = true;
        entity.UpdatedAt = DateTime.UtcNow;
        Db.Update(entity);
    }
}
