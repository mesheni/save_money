using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Пересчёт балансов счетов и общего баланса.</summary>
public sealed class BalanceService(AppDatabase database)
{
    public long GetAccountBalance(Account account)
    {
        var db = database.Connection;

        var netIncomeExpense = db.ExecuteScalar<long>(
            """
            SELECT COALESCE(SUM(CASE kind WHEN 'expense' THEN -amount_minor ELSE amount_minor END), 0)
            FROM transactions
            WHERE is_deleted = 0 AND account_id = ? AND kind IN ('expense', 'income')
            """,
            account.Id);

        var transferredOut = db.ExecuteScalar<long>(
            """
            SELECT COALESCE(SUM(amount_minor), 0)
            FROM transactions
            WHERE is_deleted = 0 AND kind = 'transfer' AND account_id = ?
            """,
            account.Id);

        var transferredIn = db.ExecuteScalar<long>(
            """
            SELECT COALESCE(SUM(COALESCE(transfer_amount_minor, amount_minor)), 0)
            FROM transactions
            WHERE is_deleted = 0 AND kind = 'transfer' AND transfer_account_id = ?
            """,
            account.Id);

        return account.InitialBalanceMinor + netIncomeExpense - transferredOut + transferredIn;
    }

    /// <summary>Суммарный баланс по счетам, включённым в общий итог.</summary>
    public long GetTotalBalance()
    {
        return database.Accounts.GetAllActive()
            .Where(a => a.IncludeInTotal)
            .Sum(GetAccountBalance);
    }
}
