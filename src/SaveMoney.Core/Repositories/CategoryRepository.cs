using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class CategoryRepository(SQLiteConnection db) : Repository<Category>(db)
{
    public List<Category> GetAllActive() =>
        Db.Table<Category>()
          .Where(c => !c.IsDeleted)
          .OrderBy(c => c.Sort)
          .ThenBy(c => c.Name)
          .ToList();

    public List<Category> GetRoots(string kind) =>
        Db.Table<Category>()
          .Where(c => !c.IsDeleted && c.ParentId == null && c.Kind == kind)
          .OrderBy(c => c.Sort)
          .ToList();

    public List<Category> GetChildren(string parentId) =>
        Db.Table<Category>()
          .Where(c => !c.IsDeleted && c.ParentId == parentId)
          .OrderBy(c => c.Sort)
          .ToList();

    /// <summary>Мягкое удаление категории: системные (IsDefault) удалять нельзя.</summary>
    public override void SoftDelete(Category entity)
    {
        if (entity.IsDefault)
        {
            throw new InvalidOperationException($"Системную категорию «{entity.Name}» удалить нельзя");
        }
        base.SoftDelete(entity);
    }
}
