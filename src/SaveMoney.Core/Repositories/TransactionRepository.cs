using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class TransactionRepository(SQLiteConnection db) : Repository<Transaction>(db)
{
    public List<Transaction> GetRecent(int limit = 50, int offset = 0) =>
        Db.Table<Transaction>()
          .Where(t => !t.IsDeleted)
          .OrderByDescending(t => t.DateUnix)
          .ThenByDescending(t => t.CreatedAt)
          .Skip(offset)
          .Take(limit)
          .ToList();

    public List<Transaction> GetBetween(long fromUnix, long toUnix) =>
        Db.Table<Transaction>()
          .Where(t => !t.IsDeleted && t.DateUnix >= fromUnix && t.DateUnix < toUnix)
          .OrderByDescending(t => t.DateUnix)
          .ToList();

    public List<Transaction> GetPendingReview() =>
        Db.Table<Transaction>()
          .Where(t => !t.IsDeleted && t.IsPendingReview)
          .OrderByDescending(t => t.DateUnix)
          .ToList();

    /// <summary>Последние транзакции с этим мерчантом — основа угадывания категории.</summary>
    public List<Transaction> FindByPayee(string payee, int limit = 10) =>
        Db.Table<Transaction>()
          .Where(t => !t.IsDeleted && t.Payee == payee)
          .OrderByDescending(t => t.DateUnix)
          .Take(limit)
          .ToList();

    public long CountAll() =>
        Db.ExecuteScalar<long>("SELECT COUNT(*) FROM transactions WHERE is_deleted = 0");
}
