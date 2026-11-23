using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class AccountRepository(SQLiteConnection db) : Repository<Account>(db)
{
    public List<Account> GetAllActive() =>
        Db.Table<Account>()
          .Where(a => !a.IsDeleted && !a.Archived)
          .OrderBy(a => a.Sort)
          .ToList()
          .OrderBy(a => a.Name, StringComparer.CurrentCulture)
          .ToList();

    // SQLite сортирует строки с BINARY-коллацией: «Ё» перед «А», латиница перед
    // кириллицей, регистр значим. Порядок внутри одного Sort делаем на клиенте
    // по CurrentCulture; OrderBy стабильный, группировка по Sort сохраняется.

    /// <summary>Все не удалённые, включая архивированные — для истории и редактирования старых операций.</summary>
    public List<Account> GetAll() =>
        Db.Table<Account>()
          .Where(a => !a.IsDeleted)
          .OrderBy(a => a.Sort)
          .ToList()
          .OrderBy(a => a.Name, StringComparer.CurrentCulture)
          .ToList();

    /// <summary>
    /// Удаление счёта выключает его регулярные платежи: иначе они продолжали бы
    /// создавать операции на удалённом счёте, невидимые ни в одном балансе.
    /// </summary>
    public override void SoftDelete(Account entity) => Db.RunInTransaction(() =>
    {
        var recurring = new Repository<RecurringPayment>(Db);
        foreach (var payment in Db.Table<RecurringPayment>()
                     .Where(r => r.AccountId == entity.Id && !r.IsDeleted && r.IsActive).ToList())
        {
            payment.IsActive = false;
            recurring.Save(payment);
        }

        base.SoftDelete(entity);
    });

    public Account? GetByName(string name) =>
        Db.Table<Account>().FirstOrDefault(a => !a.IsDeleted && a.Name == name);
}
