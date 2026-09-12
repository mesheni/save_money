using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class MerchantRuleRepository(SQLiteConnection db) : Repository<MerchantRule>(db)
{
    public List<MerchantRule> GetAllActive() =>
        Db.Table<MerchantRule>()
          .Where(r => !r.IsDeleted)
          .OrderByDescending(r => r.UseCount)
          .ToList();

    /// <summary>Правило с точно таким же паттерном (без учёта регистра).</summary>
    public MerchantRule? FindByPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return null;
        }

        return Db.Table<MerchantRule>()
                 .Where(r => !r.IsDeleted)
                 .ToList()
                 .FirstOrDefault(r => string.Equals(r.Pattern, pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Ищет правило для мерчанта: паттерн — подстрока Payee без учёта регистра.</summary>
    public MerchantRule? Match(string payee)
    {
        if (string.IsNullOrWhiteSpace(payee))
        {
            return null;
        }

        var rules = Db.Table<MerchantRule>()
                      .Where(r => !r.IsDeleted)
                      .OrderByDescending(r => r.UseCount)
                      .ToList();

        return rules.FirstOrDefault(r =>
            payee.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Правило сработало — повышаем вес, чтобы в следующий раз выигрывало при конфликте.</summary>
    public void RegisterUse(MerchantRule rule)
    {
        rule.UseCount++;
        Save(rule);
    }
}
