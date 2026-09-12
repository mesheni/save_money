using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Срез расходов по категории для круговой диаграммы и топ-списка.</summary>
public class CategorySlice
{
    public required string? CategoryId { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required long AmountMinor { get; init; }
    public double SharePercent { get; init; }
}

/// <summary>Точка динамики (день или неделя).</summary>
public class DynamicsPoint
{
    public required string Label { get; init; }
    public required long ExpenseMinor { get; init; }
    public required long IncomeMinor { get; init; }
}

/// <summary>Данные для экрана отчётов.</summary>
public sealed class ReportService(AppDatabase database)
{
    private readonly AppDatabase _db = database;

    /// <summary>Расходы по категориям за период, по убыванию; без категории — отдельной строкой.</summary>
    public List<CategorySlice> GetExpenseByCategory(long fromUnix, long toUnix)
    {
        var expenses = _db.Transactions.GetBetween(fromUnix, toUnix)
            .Where(t => t.Kind == TransactionKind.Expense)
            .ToList();
        if (expenses.Count == 0)
        {
            return [];
        }

        var categories = _db.Categories.GetAllActive().ToDictionary(c => c.Id);
        var total = expenses.Sum(t => t.AmountMinor);

        var slices = new Dictionary<string, (long Sum, string Name, string Icon)>();
        var noneSum = 0L;

        foreach (var tx in expenses)
        {
            if (tx.CategoryId is not null && categories.TryGetValue(tx.CategoryId, out var category))
            {
                var sum = slices.TryGetValue(tx.CategoryId, out var existing) ? existing.Sum : 0;
                slices[tx.CategoryId] = (sum + tx.AmountMinor, category.Name, category.Icon ?? "❓");
            }
            else
            {
                noneSum += tx.AmountMinor;
            }
        }

        var result = slices
            .Select(kv => new CategorySlice
            {
                CategoryId = kv.Key,
                Name = kv.Value.Name,
                Icon = kv.Value.Icon,
                AmountMinor = kv.Value.Sum,
                SharePercent = total > 0 ? kv.Value.Sum * 100.0 / total : 0,
            })
            .ToList();

        if (noneSum > 0)
        {
            result.Add(new CategorySlice
            {
                CategoryId = null,
                Name = "Без категории",
                Icon = "❓",
                AmountMinor = noneSum,
                SharePercent = total > 0 ? noneSum * 100.0 / total : 0,
            });
        }

        return result.OrderByDescending(s => s.AmountMinor).ToList();
    }

    /// <summary>
    /// Динамика расходов/доходов. Для периодов длиннее 45 дней группировка по неделям
    /// (метка — начало недели), иначе по дням.
    /// </summary>
    public List<DynamicsPoint> GetDynamics(long fromUnix, long toUnix)
    {
        var transactions = _db.Transactions.GetBetween(fromUnix, toUnix)
            .Where(t => t.Kind is TransactionKind.Expense or TransactionKind.Income)
            .ToList();

        var from = DateTimeOffset.FromUnixTimeSeconds(fromUnix).ToLocalTime().Date;
        var to = DateTimeOffset.FromUnixTimeSeconds(toUnix).ToLocalTime().Date;
        var byWeek = (to - from).TotalDays > 45;

        var buckets = new List<(DateTime Start, DateTime End, string Label)>();
        var cursor = from;
        while (cursor < to)
        {
            var end = byWeek ? cursor.AddDays(7) : cursor.AddDays(1);
            buckets.Add((cursor, end, cursor.ToString(byWeek ? "dd.MM" : "dd.MM")));
            cursor = end;
        }

        var result = new List<DynamicsPoint>();
        foreach (var (start, end, label) in buckets)
        {
            var startUnix = new DateTimeOffset(start, TimeZoneInfo.Local.GetUtcOffset(start)).ToUnixTimeSeconds();
            var endUnix = new DateTimeOffset(end, TimeZoneInfo.Local.GetUtcOffset(end)).ToUnixTimeSeconds();

            result.Add(new DynamicsPoint
            {
                Label = label,
                ExpenseMinor = transactions
                    .Where(t => t.Kind == TransactionKind.Expense && t.DateUnix >= startUnix && t.DateUnix < endUnix)
                    .Sum(t => t.AmountMinor),
                IncomeMinor = transactions
                    .Where(t => t.Kind == TransactionKind.Income && t.DateUnix >= startUnix && t.DateUnix < endUnix)
                    .Sum(t => t.AmountMinor),
            });
        }

        return result;
    }

    public (long IncomeMinor, long ExpenseMinor) GetTotals(long fromUnix, long toUnix)
    {
        var transactions = _db.Transactions.GetBetween(fromUnix, toUnix);
        return (
            transactions.Where(t => t.Kind == TransactionKind.Income).Sum(t => t.AmountMinor),
            transactions.Where(t => t.Kind == TransactionKind.Expense).Sum(t => t.AmountMinor));
    }
}
