using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SaveMoney.ViewModels;

public record MoreMenuItem(string Title, string Subtitle, string? Route)
{
    public bool IsEnabled => Route is not null;
}

public partial class MoreViewModel : ObservableObject
{
    public IReadOnlyList<MoreMenuItem> Items { get; } =
    [
        new("Счета", "Наличные, карты, балансы", "accounts"),
        new("Категории", "Расходы, доходы, подкатегории", "categories"),
        new("Долги", "Фаза 2 — в разработке", null),
        new("Бюджеты и «до зарплаты»", "Фаза 2 — в разработке", null),
        new("Настройки и экспорт", "Фаза 3 — в разработке", null),
    ];

    public string VersionText =>
        $"SaveMoney {AppInfo.VersionString} · Фаза 1";

    [RelayCommand]
    private void Open(MoreMenuItem? item)
    {
        if (item?.Route is { } route)
        {
            Shell.Current.GoToAsync(route);
        }
    }
}
