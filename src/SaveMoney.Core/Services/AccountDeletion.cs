using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Что потеряется при удалении счёта — текст для диалога подтверждения.</summary>
public static class AccountDeletion
{
    public static string DescribeImpact(AppDatabase db, Account account)
    {
        var lines = new List<string>
        {
            $"«{account.Name}» исчезнет из списков. Транзакции по нему останутся в истории.",
        };

        var balance = new BalanceService(db).GetAccountBalance(account);
        if (balance != 0)
        {
            lines.Add(account.IncludeInTotal && !account.Archived
                ? $"Остаток {MoneyFormat.Rubles(balance)} пропадёт из общего баланса — возможно, сначала стоит перевести его на другой счёт."
                : $"На счёте остаток {MoneyFormat.Rubles(balance)}.");
        }

        var recurring = db.RecurringPayments.GetAllActive().Count(r => r.AccountId == account.Id && r.IsActive);
        if (recurring > 0)
        {
            lines.Add($"Регулярные платежи с этого счёта ({recurring}) будут выключены.");
        }

        var debts = db.Connection.Table<Debt>()
            .Where(d => !d.IsDeleted && d.AccountId == account.Id && d.Status == DebtStatus.Active)
            .Count();
        if (debts > 0)
        {
            lines.Add($"Открытые долги, привязанные к счёту ({debts}), не смогут создавать операции, пока не выберете им другой счёт.");
        }

        return string.Join("\n\n", lines);
    }
}
