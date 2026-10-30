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
            WHERE is_deleted = 0 AND is_pending_review = 0 AND account_id = ? AND kind IN ('expense', 'income')
            """,
            account.Id);

        var transferredOut = db.ExecuteScalar<long>(
            """
            SELECT COALESCE(SUM(amount_minor), 0)
            FROM transactions
            WHERE is_deleted = 0 AND is_pending_review = 0 AND kind IN ('transfer', 'debt_repayment') AND account_id = ?
            """,
            account.Id);

        var transferredIn = db.ExecuteScalar<long>(
            """
            SELECT COALESCE(SUM(COALESCE(transfer_amount_minor, amount_minor)), 0)
            FROM transactions
            WHERE is_deleted = 0 AND is_pending_review = 0 AND kind IN ('transfer', 'debt_repayment') AND transfer_account_id = ?
            """,
            account.Id);

        return account.InitialBalanceMinor + netIncomeExpense - transferredOut + transferredIn;
    }

    private sealed class AccountDelta
    {
        public string AccId { get; init; } = "";
        public long Delta { get; init; }
    }

    /// <summary>Суммарный баланс по счетам, включённым в общий итог.</summary>
    public long GetTotalBalance()
    {
        var accounts = database.Accounts.GetAllActive().Where(a => a.IncludeInTotal).ToList();
        if (accounts.Count == 0)
        {
            return 0;
        }

        // Один агрегат вместо трёх запросов на счёт (N+1): та же арифметика,
        // что в GetAccountBalance, собранная в одну таблицу дельт по счетам.
        var deltas = database.Connection.Query<AccountDelta>(
            """
            SELECT acc_id AS AccId, SUM(delta) AS Delta FROM (
                SELECT account_id AS acc_id,
                       CASE kind WHEN 'expense' THEN -amount_minor ELSE amount_minor END AS delta
                FROM transactions
                WHERE is_deleted = 0 AND is_pending_review = 0 AND kind IN ('expense', 'income')
                UNION ALL
                SELECT account_id, -amount_minor
                FROM transactions
                WHERE is_deleted = 0 AND is_pending_review = 0 AND kind IN ('transfer', 'debt_repayment')
                UNION ALL
                SELECT transfer_account_id, COALESCE(transfer_amount_minor, amount_minor)
                FROM transactions
                WHERE is_deleted = 0 AND is_pending_review = 0 AND kind IN ('transfer', 'debt_repayment')
            )
            GROUP BY acc_id
            """)
            .ToDictionary(d => d.AccId, d => d.Delta);

        return accounts.Sum(a => a.InitialBalanceMinor + deltas.GetValueOrDefault(a.Id));
    }
}
