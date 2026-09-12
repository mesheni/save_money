using SaveMoney.Core.Models;

namespace SaveMoney.Core.Database;

/// <summary>
/// Дефолтные RU-категории (~65 с подкатегориями). Сеются один раз при первом запуске;
/// все помечаются IsDefault — их можно переименовывать, но нельзя удалять.
/// </summary>
public static class DefaultCategories
{
    private sealed record CatDef(string Name, string Icon, string[] Children);

    private static readonly List<CatDef> Expenses =
    [
        new("Продукты", "🛒", ["Супермаркет", "Магнит", "Пятёрочка", "Рынок", "Доставка продуктов"]),
        new("Кафе и рестораны", "🍔", ["Фастфуд", "Кофейня", "Доставка еды", "Ресторан", "Бар"]),
        new("Транспорт", "🚗", ["Такси", "Общественный транспорт", "Бензин", "Парковка", "Каршеринг", "ТО и мойка"]),
        new("ЖКХ и связь", "🏠", ["Электричество", "Вода", "Отопление", "Домофон", "Интернет", "Мобильная связь"]),
        new("Здоровье", "💊", ["Аптека", "Врачи", "Анализы", "Стоматология"]),
        new("Дом и быт", "🧹", ["Бытовая химия", "Ремонт", "Мебель", "Техника", "Товары для дома"]),
        new("Одежда и обувь", "👕", []),
        new("Уход за собой", "💇", ["Стрижка", "Косметика", "Маникюр"]),
        new("Развлечения", "🎬", ["Кино", "Игры", "Подписки", "Книги", "Хобби"]),
        new("Спорт", "🏃", []),
        new("Дети", "👶", ["Детский сад и кружки", "Одежда детям", "Игрушки"]),
        new("Образование", "🎓", []),
        new("Подарки", "🎁", []),
        new("Путешествия", "✈️", ["Билеты", "Жильё", "Экскурсии"]),
        new("Питомцы", "🐾", ["Корм", "Ветеринар"]),
        new("Кредиты и долги", "💳", ["Платёж по кредиту", "Проценты и комиссии", "Возврат долга"]),
        new("Налоги и пошлины", "🧾", []),
        new("Прочее", "❓", []),
    ];

    private static readonly List<CatDef> Incomes =
    [
        new("Зарплата", "💰", []),
        new("Подработка", "🛠️", []),
        new("Кэшбэк и бонусы", "🎁", []),
        new("Проценты по вкладу", "🏦", []),
        new("Возврат долга", "↩️", []),
        new("Подарки полученные", "🎀", []),
        new("Прочий доход", "❓", []),
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
