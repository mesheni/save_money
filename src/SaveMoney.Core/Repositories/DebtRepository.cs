using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class DebtRepository(SQLiteConnection db) : Repository<Debt>(db)
{
    private readonly Repository<DebtPayment> _payments = new(db);

    public void SoftDeletePayment(DebtPayment payment) => _payments.SoftDelete(payment);

    public List<Debt> GetActive() =>
        Db.Table<Debt>()
          .Where(d => !d.IsDeleted && d.Status == DebtStatus.Active)
          .OrderBy(d => d.DueDateUnix)
          .ToList();

    public List<DebtPayment> GetPayments(string debtId) =>
        Db.Table<DebtPayment>()
          .Where(p => !p.IsDeleted && p.DebtId == debtId)
          .OrderBy(p => p.DateUnix)
          .ToList();

    /// <summary>Сколько осталось погасить по долгу.</summary>
    public long GetRemaining(Debt debt)
    {
        var paid = Db.ExecuteScalar<long>(
            "SELECT COALESCE(SUM(amount_minor), 0) FROM debt_payments WHERE debt_id = ? AND is_deleted = 0",
            debt.Id);
        return debt.AmountMinor - paid;
    }

    /// <summary>Добавляет платёж и закрывает долг, если погашен полностью.</summary>
    public void AddPayment(DebtPayment payment)
    {
        _payments.Save(payment);

        var debt = Get(payment.DebtId);
        if (debt is not { Status: DebtStatus.Active })
        {
            return;
        }

        if (GetRemaining(debt) <= 0)
        {
            debt.Status = DebtStatus.Settled;
            debt.SettledDateUnix = payment.DateUnix;
            Save(debt);
        }
    }
}
