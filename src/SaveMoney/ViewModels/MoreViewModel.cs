using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Models;

namespace SaveMoney.ViewModels;

public record MoreMenuItem(string IconKey, string Title, string Subtitle, string? Route)
{
    public bool IsEnabled => Route is not null;
}

public partial class MoreViewModel : ObservableObject
{
    public IReadOnlyList<MoreMenuItem> Items { get; } =
    [
        new(IconKeys.Wallet, "Счета", "Наличные, карты, балансы", "accounts"),
        new(IconKeys.Category, "Категории", "Расходы, доходы, подкатегории", "categories"),
        new(IconKeys.Debts, "Долги", "Мне должны / я должен", "debts"),
        new(IconKeys.Budgets, "Бюджеты", "Лимиты, план/факт по циклу", "budgets"),
        new(IconKeys.Payday, "До зарплаты", "Хватит ли до ЗП, ₽/день", "payday"),
        new(IconKeys.Recurring, "Регулярные платежи", "ЖКХ, подписки, аренда", "recurring"),
        new(IconKeys.Settings, "Настройки и экспорт", "Тема, CSV, резервная копия", "settings"),
        new(IconKeys.Family, "Синк и семья", "v2.0 — в разработке", null),
    ];

    public string VersionText =>
        $"SaveMoney {AppInfo.VersionString}";

    [RelayCommand]
    private void Open(MoreMenuItem? item)
    {
        if (item?.Route is { } route)
        {
            Shell.Current.GoToAsync(route);
        }
    }
}
