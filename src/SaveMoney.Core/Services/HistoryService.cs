using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Строка истории: транзакция с денормализованными именами для отображения.</summary>
public class HistoryItem
{
    public required string Id { get; init; }
    public required long DateUnix { get; init; }
    public required string Kind { get; init; }
    public required long AmountMinor { get; init; }

    /// <summary>Расход — отрицательная, доход/перевод — положительная (для показа одним полем).</summary>
    public long SignedAmountMinor => Kind == TransactionKind.Expense ? -AmountMinor : AmountMinor;

    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public string Icon { get; init; } = "❓";
    public bool IsExpense => Kind == TransactionKind.Expense;
}

/// <summary>Группа транзакций одного локального дня.</summary>
public class HistoryDayGroup
{
    public required DateTime Date { get; init; }
    public long IncomeMinor { get; set; }
    public long ExpenseMinor { get; set; }
    public required List<HistoryItem> Items { get; init; }
}

/// <summary>История с группировкой по локальным дням и фильтрами периода/счёта.</summary>
public sealed class HistoryService(AppDatabase database)
{
    /// <param name="fromUnix">Включительно.</param>
    /// <param name="toUnix">Не включая.</param>
    /// <param name="accountId">Фильтр по счёту: свои операции + переводы, где счёт любая из сторон.</param>
    public List<HistoryDayGroup> GetGroups(long fromUnix, long toUnix, string? accountId = null)
    {
        var transactions = database.Transactions.GetBetween(fromUnix, toUnix);

        if (accountId is not null)
        {
            transactions = transactions
                .Where(t => t.AccountId == accountId || t.TransferAccountId == accountId)
                .ToList();
        }

        var accounts = database.Accounts.GetAll().ToDictionary(a => a.Id, a => a.Name);
        var categories = database.Categories.GetAllActive().ToDictionary(c => c.Id);

        string AccountName(string? id) =>
            id is not null && accounts.TryGetValue(id, out var name) ? name : "(счёт удалён)";

        var groups = new Dictionary<DateTime, HistoryDayGroup>();

        foreach (var tx in transactions)
        {
            var title = tx.Kind switch
            {
                TransactionKind.Transfer => $"{AccountName(tx.AccountId)} → {AccountName(tx.TransferAccountId)}",
                _ when tx.CategoryId is not null && categories.TryGetValue(tx.CategoryId, out var cat) => cat.Name,
                _ => "Без категории",
            };

            var icon = tx.Kind == TransactionKind.Transfer
                ? "🔁"
                : tx.CategoryId is not null && categories.TryGetValue(tx.CategoryId, out var c)
                    ? c.Icon ?? "❓"
                    : "❓";

            var subtitleParts = new List<string>();
            if (tx.Kind != TransactionKind.Transfer)
            {
                subtitleParts.Add(AccountName(tx.AccountId));
            }
            if (!string.IsNullOrWhiteSpace(tx.Payee))
            {
                subtitleParts.Add(tx.Payee!);
            }
            if (!string.IsNullOrWhiteSpace(tx.Note))
            {
                subtitleParts.Add(tx.Note!);
            }

            var item = new HistoryItem
            {
                Id = tx.Id,
                DateUnix = tx.DateUnix,
                Kind = tx.Kind,
                AmountMinor = tx.TransferAmountMinor ?? tx.AmountMinor,
                Title = title,
                Subtitle = string.Join(" · ", subtitleParts),
                Icon = icon,
            };

            var localDate = DateTimeOffset
                .FromUnixTimeSeconds(tx.DateUnix)
                .ToLocalTime()
                .Date;

            if (!groups.TryGetValue(localDate, out var group))
            {
                group = new HistoryDayGroup { Date = localDate, Items = [] };
                groups[localDate] = group;
            }

            group.Items.Add(item);
            switch (tx.Kind)
            {
                case TransactionKind.Income:
                    group.IncomeMinor += item.AmountMinor;
                    break;
                case TransactionKind.Expense:
                    group.ExpenseMinor += item.AmountMinor;
                    break;
            }
        }

        var result = groups.Values.OrderByDescending(g => g.Date).ToList();
        foreach (var group in result)
        {
            ((List<HistoryItem>)group.Items).Sort((a, b) => b.DateUnix.CompareTo(a.DateUnix));
        }

        return result;
    }
}
