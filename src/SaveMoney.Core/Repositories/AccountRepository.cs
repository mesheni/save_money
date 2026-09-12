using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class AccountRepository(SQLiteConnection db) : Repository<Account>(db)
{
    public List<Account> GetAllActive() =>
        Db.Table<Account>()
          .Where(a => !a.IsDeleted && !a.Archived)
          .OrderBy(a => a.Sort)
          .ThenBy(a => a.Name)
          .ToList();

    /// <summary>Все не удалённые, включая архивированные — для истории и редактирования старых операций.</summary>
    public List<Account> GetAll() =>
        Db.Table<Account>()
          .Where(a => !a.IsDeleted)
          .OrderBy(a => a.Sort)
          .ThenBy(a => a.Name)
          .ToList();

    public Account? GetByName(string name) =>
        Db.Table<Account>().FirstOrDefault(a => !a.IsDeleted && a.Name == name);
}
