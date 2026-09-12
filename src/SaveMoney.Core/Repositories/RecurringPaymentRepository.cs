using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class RecurringPaymentRepository(SQLiteConnection db) : Repository<RecurringPayment>(db)
{
    public List<RecurringPayment> GetAllActive() =>
        Db.Table<RecurringPayment>()
          .Where(r => !r.IsDeleted)
          .OrderBy(r => r.NextDateUnix)
          .ToList();
}
