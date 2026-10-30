using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

public sealed record RepaymentTarget(string Id, bool IsCreditAccount, string Name, string Type, long RemainingMinor)
{
    public string Label => $"{Name} · {Type} · {MoneyFormat.Rubles(RemainingMinor)}";
}

/// <summary>Principal repayments: money movement and debt payment are one atomic change.</summary>
public sealed class RepaymentService(AppDatabase db)
{
    public const string LastAccountKey = "last_repayment_account";

    public List<RepaymentTarget> GetTargets(string? editingTransactionId = null)
    {
        var original = editingTransactionId is null ? null : db.Transactions.Get(editingTransactionId);
        var result = new List<RepaymentTarget>();
        foreach (var debt in db.Connection.Table<Debt>().Where(d => !d.IsDeleted && d.Direction == DebtDirection.IOwe).ToList())
        {
            var ownPayment = original?.Kind == TransactionKind.DebtRepayment && original.DebtId == debt.Id
                ? db.Debts.GetPayments(debt.Id).Where(p => p.TransactionId == original.Id).Sum(p => p.AmountMinor) : 0;
            var remaining = db.Debts.GetRemaining(debt) + ownPayment;
            if ((debt.Status == DebtStatus.Active || ownPayment > 0) && remaining > 0)
                result.Add(new(debt.Id, false, debt.PersonName,
                    debt.Kind == DebtKind.ConsumerLoan ? "Потребительский кредит" : "Личный долг", remaining));
        }
        var balance = new BalanceService(db);
        foreach (var account in db.Accounts.GetAllActive().Where(a => a.Kind == AccountKind.Credit))
        {
            var value = balance.GetAccountBalance(account);
            if (original?.Kind == TransactionKind.DebtRepayment)
            {
                if (original.TransferAccountId == account.Id) value -= original.AmountMinor;
                if (original.AccountId == account.Id) value += original.AmountMinor;
            }
            if (value < 0) result.Add(new(account.Id, true, account.Name, "Кредитный счёт", -value));
        }
        return result.OrderBy(t => t.Name).ThenBy(t => t.Type).ThenBy(t => t.Id).ToList();
    }

    public Transaction Save(Transaction transaction)
    {
        db.Connection.RunInTransaction(() =>
        {
            var original = db.Transactions.Get(transaction.Id);
            RemovePayment(original);
            // Exclude the original money movement while validating a replacement.
            if (original is not null) db.Transactions.SoftDelete(original);
            if (transaction.Kind == TransactionKind.DebtRepayment)
            {
                if (transaction.AmountMinor <= 0) throw new ArgumentException("Введите сумму больше нуля");
                if (!db.Accounts.GetAllActive().Any(a => a.Id == transaction.AccountId))
                    throw new ArgumentException("Выберите доступный счёт оплаты");
                if ((transaction.DebtId is null) == (transaction.TransferAccountId is null))
                    throw new ArgumentException("Выберите одну задолженность");
                var target = GetTargets().FirstOrDefault(t => t.Id == (transaction.DebtId ?? transaction.TransferAccountId)
                    && t.IsCreditAccount == (transaction.DebtId is null));
                if (target is null) throw new ArgumentException("Задолженность погашена или недоступна. Выберите другую.");
                if (target.IsCreditAccount && target.Id == transaction.AccountId)
                    throw new ArgumentException("Счёт оплаты совпадает с кредитным счётом");
                if (transaction.AmountMinor > target.RemainingMinor)
                    throw new ArgumentException($"Сумма превышает остаток: {MoneyFormat.Rubles(target.RemainingMinor)}");
                transaction.CategoryId = null;
                transaction.Payee = null;
                transaction.TransferAmountMinor = null;
                if (!target.IsCreditAccount)
                    db.Debts.AddPayment(new DebtPayment
                    {
                        DebtId = target.Id, TransactionId = transaction.Id,
                        AmountMinor = transaction.AmountMinor, DateUnix = transaction.DateUnix, Note = transaction.Note,
                    });
                db.Settings.Set(LastAccountKey, transaction.AccountId);
            }
            else transaction.DebtId = null;
            transaction.IsDeleted = false;
            db.Transactions.Save(transaction);
        });
        return transaction;
    }

    public void Delete(Transaction transaction) => db.Connection.RunInTransaction(() =>
    {
        RemovePayment(db.Transactions.Get(transaction.Id));
        db.Transactions.SoftDelete(transaction);
    });

    private void RemovePayment(Transaction? original)
    {
        if (original?.Kind != TransactionKind.DebtRepayment || original.DebtId is null) return;
        var payments = db.Debts.GetPayments(original.DebtId).Where(p => p.TransactionId == original.Id).ToList();
        if (payments.Count == 0) return;
        foreach (var payment in payments)
            db.Debts.SoftDeletePayment(payment);
        var debt = db.Debts.Get(original.DebtId);
        if (debt is null) return;
        debt.Status = db.Debts.GetRemaining(debt) > 0 ? DebtStatus.Active : DebtStatus.Settled;
        debt.SettledDateUnix = debt.Status == DebtStatus.Active ? null : original.DateUnix;
        db.Debts.Save(debt);
    }
}
