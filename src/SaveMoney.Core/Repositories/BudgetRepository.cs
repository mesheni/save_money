using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class BudgetRepository(SQLiteConnection db) : Repository<Budget>(db)
{
    public List<Budget> GetAllActive() =>
        Db.Table<Budget>()
          .Where(b => !b.IsDeleted)
          .OrderBy(b => b.CreatedAt)
          .ToList();
}
