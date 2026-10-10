using SaveMoney.Core.Models;

namespace SaveMoney.Core.Database;

/// <summary>
/// Дефолтные RU-категории (~65 с подкатегориями). Сеются один раз при первом запуске;
/// все помечаются IsDefault — их можно переименовывать, но нельзя удалять.
/// Иконки — ключи IconKeys (раньше эмодзи), рендерятся глифами Material Symbols.
/// </summary>
public static class DefaultCategories
{
    private sealed record CatDef(string Name, string Icon, string[] Children);

    private static readonly List<CatDef> Expenses =
    [
        new("Продукты", IconKeys.Groceries, ["Супермаркет", "Магнит", "Пятёрочка", "Рынок", "Доставка продуктов"]),
        new("Кафе и рестораны", IconKeys.Cafe, ["Фастфуд", "Кофейня", "Доставка еды", "Ресторан", "Бар"]),
        new("Транспорт", IconKeys.Transport, ["Такси", "Общественный транспорт", "Бензин", "Парковка", "Каршеринг", "ТО и мойка"]),
        new("ЖКХ и связь", IconKeys.Home, ["Электричество", "Вода", "Отопление", "Домофон", "Интернет", "Мобильная связь"]),
        new("Здоровье", IconKeys.Health, ["Аптека", "Врачи", "Анализы", "Стоматология"]),
        new("Дом и быт", IconKeys.Household, ["Бытовая химия", "Ремонт", "Мебель", "Техника", "Товары для дома"]),
        new("Одежда и обувь", IconKeys.Clothes, []),
        new("Уход за собой", IconKeys.Beauty, ["Стрижка", "Косметика", "Маникюр"]),
        new("Развлечения", IconKeys.Entertainment, ["Кино", "Игры", "Подписки", "Книги", "Хобби"]),
        new("Спорт", IconKeys.Sport, []),
        new("Дети", IconKeys.Kids, ["Детский сад и кружки", "Одежда детям", "Игрушки"]),
        new("Образование", IconKeys.Education, []),
        new("Подарки", IconKeys.Gift, []),
        new("Путешествия", IconKeys.Travel, ["Билеты", "Жильё", "Экскурсии"]),
        new("Питомцы", IconKeys.Pets, ["Корм", "Ветеринар"]),
        new("Кредиты и долги", IconKeys.Credit, ["Платёж по кредиту", "Проценты и комиссии", "Возврат долга"]),
        new("Налоги и пошлины", IconKeys.Taxes, []),
        new("Прочее", IconKeys.Other, []),
    ];

    private static readonly List<CatDef> Incomes =
    [
        new("Зарплата", IconKeys.Salary, []),
        new("Подработка", IconKeys.Freelance, []),
        new("Кэшбэк и бонусы", IconKeys.Bonus, []),
        new("Проценты по вкладу", IconKeys.Bank, []),
        new("Возврат долга", IconKeys.Return, []),
        new("Подарки полученные", IconKeys.Present, []),
        new("Прочий доход", IconKeys.Other, []),
    ];

    /// <summary>Сеет категории, только если таблица пуста. Идемпотентно.</summary>
    public static void SeedIfNeeded(AppDatabase database)
    {
        var db = database.Connection;
        var existing = db.ExecuteScalar<long>("SELECT COUNT(*) FROM categories");
        if (existing > 0)
        {
            return;
        }

        SeedKind(db, CategoryKind.Expense, Expenses);
        SeedKind(db, CategoryKind.Income, Incomes);
    }

    private static void SeedKind(SQLite.SQLiteConnection db, string kind, List<CatDef> items)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var root = new Category
            {
                Name = items[i].Name,
                Icon = items[i].Icon,
                Kind = kind,
                Sort = i,
                IsDefault = true,
            };
            db.Insert(root);

            for (var j = 0; j < items[i].Children.Length; j++)
            {
                db.Insert(new Category
                {
                    Name = items[i].Children[j],
                    Kind = kind,
                    ParentId = root.Id,
                    Sort = j,
                    IsDefault = true,
                });
            }
        }
    }
}
