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

public enum DynamicsGranularity
{
    Day,
    Week,
    Month,
}

/// <summary>Данные для экрана отчётов.</summary>
public sealed class ReportService(AppDatabase database)
{
    private readonly AppDatabase _db = database;

    /// <summary>Расходы по категориям за период — обёртка GetByCategory (совместимость с тестами).</summary>
    public List<CategorySlice> GetExpenseByCategory(long fromUnix, long toUnix) =>
        GetByCategory(fromUnix, toUnix, CategoryKind.Expense);

    /// <summary>Расходы (или доходы) по категориям за период, по убыванию; без категории — отдельной строкой.</summary>
    public List<CategorySlice> GetByCategory(long fromUnix, long toUnix, string kind = TransactionKind.Expense)
    {
        var expenses = _db.Transactions.GetBetween(fromUnix, toUnix)
            .Where(t => t.Kind == kind)
            .ToList();
        if (expenses.Count == 0)
        {
            return [];
        }

        var categories = _db.Categories.GetAll().ToDictionary(c => c.Id);
        var total = expenses.Sum(t => t.AmountMinor);

        var slices = new Dictionary<string, (long Sum, string Name, string Icon)>();
        var noneSum = 0L;

        foreach (var tx in expenses)
        {
            if (tx.CategoryId is not null && categories.TryGetValue(tx.CategoryId, out var category))
            {
                var sum = slices.TryGetValue(tx.CategoryId, out var existing) ? existing.Sum : 0;
                slices[tx.CategoryId] = (sum + tx.AmountMinor, category.Name, category.Icon ?? IconKeys.Other);
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
                Icon = IconKeys.Other,
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
        var granularity = GetGranularity(fromUnix, toUnix);
        // Период через границу года: «05.01» без года неоднозначно.
        var dayFormat = from.Year != to.AddDays(-1).Year ? "dd.MM.yy" : "dd.MM";

        var buckets = new List<(DateTime Start, DateTime End, string Label)>();
        var cursor = from;
        while (cursor < to)
        {
            var end = granularity switch
            {
                DynamicsGranularity.Month => new DateTime(cursor.Year, cursor.Month, 1).AddMonths(1),
                DynamicsGranularity.Week => cursor.AddDays(7),
                _ => cursor.AddDays(1),
            };
            var label = granularity == DynamicsGranularity.Month ? cursor.ToString("MM.yyyy") : cursor.ToString(dayFormat);
            buckets.Add((cursor, end, label));
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

    /// <summary>Шаг столбцов динамики: дни до 45 дней, недели до года, дальше — месяцы
    /// (иначе «Всё» за несколько лет давало сотни недельных столбцов).</summary>
    public static DynamicsGranularity GetGranularity(long fromUnix, long toUnix)
    {
        var days = (DateTimeOffset.FromUnixTimeSeconds(toUnix).ToLocalTime().Date
            - DateTimeOffset.FromUnixTimeSeconds(fromUnix).ToLocalTime().Date).TotalDays;
        return days > 366 ? DynamicsGranularity.Month
            : days > 45 ? DynamicsGranularity.Week
            : DynamicsGranularity.Day;
    }

    public (long IncomeMinor, long ExpenseMinor) GetTotals(long fromUnix, long toUnix)
    {
        var transactions = _db.Transactions.GetBetween(fromUnix, toUnix);
        return (
            transactions.Where(t => t.Kind == TransactionKind.Income).Sum(t => t.AmountMinor),
            transactions.Where(t => t.Kind == TransactionKind.Expense).Sum(t => t.AmountMinor));
    }
}
