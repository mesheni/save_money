using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SaveMoney.ViewModels;

public record MoreMenuItem(string Emoji, string Title, string Subtitle, string? Route)
{
    public bool IsEnabled => Route is not null;
}

public partial class MoreViewModel : ObservableObject
{
    public IReadOnlyList<MoreMenuItem> Items { get; } =
    [
        new("💳", "Счета", "Наличные, карты, балансы", "accounts"),
        new("🏷", "Категории", "Расходы, доходы, подкатегории", "categories"),
        new("🤝", "Долги", "Мне должны / я должен", "debts"),
        new("🎯", "Бюджеты", "Лимиты, план/факт по циклу", "budgets"),
        new("📅", "До зарплаты", "Хватит ли до ЗП, ₽/день", "payday"),
        new("🔁", "Регулярные платежи", "ЖКХ, подписки, аренда", "recurring"),
        new("⚙️", "Настройки и экспорт", "Тема, CSV, резервная копия", "settings"),
        new("👥", "Синк и семья", "v2.0 — в разработке", null),
    ];

    public string VersionText =>
        $"SaveMoney {AppInfo.VersionString} · Фаза 3";

    [RelayCommand]
    private void Open(MoreMenuItem? item)
    {
        if (item?.Route is { } route)
        {
            Shell.Current.GoToAsync(route);
        }
    }
}
