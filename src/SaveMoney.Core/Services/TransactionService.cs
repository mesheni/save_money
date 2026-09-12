using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>
/// Сохранение транзакций + «умное» запоминание: после сохранения с заполненным Payee
/// создаётся/усиливается merchant-правило, категория последнего ввода запоминается по kind.
/// </summary>
public sealed class TransactionService(AppDatabase database)
{
    private const string LastCategoryPrefix = "last_category_";

    /// <summary>Категория, использованная в последней транзакции этого kind — кандидат по умолчанию при вводе.</summary>
    public string? GetLastCategoryId(string kind) =>
        database.Settings.Get(LastCategoryPrefix + kind);

    /// <summary>Угадать категорию по магазину из накопленных правил. Не меняет счётчики.</summary>
    public string? GuessCategoryId(string? payee)
    {
        if (string.IsNullOrWhiteSpace(payee))
        {
            return null;
        }

        return database.MerchantRules.Match(payee)?.CategoryId;
    }

    /// <summary>Вставка или обновление транзакции + обучение правилам.</summary>
    public Transaction Save(Transaction transaction)
    {
        if (transaction.AmountMinor <= 0)
        {
            throw new ArgumentException("Сумма должна быть больше нуля", nameof(transaction));
        }

        if (transaction.Kind == TransactionKind.Transfer)
        {
            if (string.IsNullOrEmpty(transaction.TransferAccountId))
            {
                throw new ArgumentException("Для перевода не указан счёт-получатель", nameof(transaction));
            }

            if (transaction.TransferAccountId == transaction.AccountId)
            {
                throw new ArgumentException("Счёт-получатель совпадает со счётом-источником", nameof(transaction));
            }

            transaction.CategoryId = null;
            transaction.Payee = null;
        }

        database.Transactions.Save(transaction);

        if (transaction.Kind != TransactionKind.Transfer
            && !string.IsNullOrWhiteSpace(transaction.Payee)
            && transaction.CategoryId is not null)
        {
            LearnMerchantRule(transaction.Payee!.Trim(), transaction.CategoryId, transaction.AccountId);
        }

        if (transaction.CategoryId is not null && !transaction.IsPendingReview)
        {
            database.Settings.Set(LastCategoryPrefix + transaction.Kind, transaction.CategoryId);
        }

        return transaction;
    }

    private void LearnMerchantRule(string payee, string categoryId, string accountId)
    {
        var existing = database.MerchantRules.FindByPattern(payee);
        if (existing is not null)
        {
            existing.CategoryId = categoryId;
            existing.AccountId = accountId;
            existing.UseCount++;
            database.MerchantRules.Save(existing);
            return;
        }

        database.MerchantRules.Save(new MerchantRule
        {
            Pattern = payee,
            CategoryId = categoryId,
            AccountId = accountId,
            UseCount = 1,
        });
    }
}
