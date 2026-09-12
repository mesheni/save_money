using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Строка статуса бюджета: план/факт за период.</summary>
public class BudgetStatusRow
{
    public required string BudgetId { get; init; }

    /// <summary>null — общий лимит на все расходы.</summary>
    public required string? CategoryId { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required long PlanMinor { get; init; }
    public required long FactMinor { get; init; }

    public bool IsGeneral => CategoryId is null;
    public bool IsOver => FactMinor > PlanMinor;
    public double Percent => PlanMinor > 0 ? Math.Min(100.0, FactMinor * 100.0 / PlanMinor) : 0;
    public string PercentText => PlanMinor > 0 ? $"{FactMinor * 100 / PlanMinor}%" : "";
}

/// <summary>План/факт по бюджетам за период. Бюджет на корневую категорию включает расходы подкатегорий.</summary>
public sealed class BudgetService(AppDatabase database)
{
    private readonly AppDatabase _db = database;

    public List<BudgetStatusRow> GetStatuses(long startUnix, long endUnix)
    {
        var budgets = _db.Budgets.GetAllActive();
        if (budgets.Count == 0)
        {
            return [];
        }

        var categories = _db.Categories.GetAllActive();
        var byId = categories.ToDictionary(c => c.Id);
        var expenses = _db.Transactions.GetBetween(startUnix, endUnix)
            .Where(t => t.Kind == TransactionKind.Expense)
            .ToList();

        var rows = new List<BudgetStatusRow>();
        foreach (var budget in budgets)
        {
            long fact;
            string name;
            string icon;

            if (budget.CategoryId is null)
            {
                fact = expenses.Sum(t => t.AmountMinor);
                name = "Общий лимит";
                icon = "🎯";
            }
            else
            {
                var ids = new HashSet<string> { budget.CategoryId };
                foreach (var child in categories.Where(c => c.ParentId == budget.CategoryId))
                {
                    ids.Add(child.Id);
                }

                fact = expenses
                    .Where(t => t.CategoryId is not null && ids.Contains(t.CategoryId))
                    .Sum(t => t.AmountMinor);

                var category = byId.TryGetValue(budget.CategoryId, out var c) ? c : null;
                name = category?.Name ?? "(категория удалена)";
                icon = category?.Icon ?? "❓";
            }

            rows.Add(new BudgetStatusRow
            {
                BudgetId = budget.Id,
                CategoryId = budget.CategoryId,
                Name = name,
                Icon = icon,
                PlanMinor = budget.AmountMinor,
                FactMinor = fact,
            });
        }

        return rows.OrderByDescending(r => r.Percent).ToList();
    }
}
