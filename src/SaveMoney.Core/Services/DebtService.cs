using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>
/// Долги поверх репозитория: платёж по долгу может создать транзакцию
/// на связанном счёте (возврат — доход, выплата долга — расход, категория «Возврат долга»).
/// </summary>
public sealed class DebtService(AppDatabase database, TransactionService transactions)
{
    private const string RepaymentCategoryName = "Возврат долга";

    private readonly AppDatabase _db = database;
    private readonly TransactionService _transactions = transactions;

    public void AddPayment(Debt debt, long amountMinor, long dateUnix, bool createTransaction)
    {
        _db.Debts.AddPayment(new DebtPayment
        {
            DebtId = debt.Id,
            AmountMinor = amountMinor,
            DateUnix = dateUnix,
        });

        if (!createTransaction || string.IsNullOrEmpty(debt.AccountId) || amountMinor <= 0)
        {
            return;
        }

        var kind = debt.Direction == DebtDirection.OwedToMe
            ? TransactionKind.Income
            : TransactionKind.Expense;

        var category = _db.Categories.GetAllActive()
            .FirstOrDefault(c => c.Kind == kind && c.Name == RepaymentCategoryName);
        if (category is null)
        {
            return;
        }

        _transactions.Save(new Transaction
        {
            AccountId = debt.AccountId!,
            CategoryId = category.Id,
            AmountMinor = amountMinor,
            Kind = kind,
            Payee = debt.PersonName,
            Note = $"Долг: {debt.PersonName}",
            DateUnix = dateUnix,
            Source = TransactionSource.Manual,
        });
    }
}
