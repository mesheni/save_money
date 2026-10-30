using CommunityToolkit.Mvvm.ComponentModel;
using SaveMoney.Core.Models;

namespace SaveMoney.ViewModels;

/// <summary>Чип счёта в строке выбора счёта.</summary>
public partial class AccountChip : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public override string ToString() => Name;
}

/// <summary>Чип категории (корень или подкатегория) в строке выбора.</summary>
public partial class CategoryChip : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Icon { get; init; } = IconKeys.Other;
    public string AccessibleName => $"{Name}{(IsSelected ? ", выбрана" : string.Empty)}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(AccessibleName));
}

/// <summary>Чип выбора цвета (палитра счёта).</summary>
public partial class ColorChip : ObservableObject
{
    public required string Hex { get; init; }

    public string AccessibleName => $"{Hex switch
    {
        "#5B8DEF" => "Синий",
        "#4C6EF5" => "Ярко-синий",
        "#35A7D8" => "Голубой",
        "#3AB0A2" => "Бирюзовый",
        "#2E9455" => "Зелёный",
        "#7FB93C" => "Салатовый",
        "#E8B31A" => "Жёлтый",
        "#FF9F45" => "Оранжевый",
        "#FF6B00" => "Ярко-оранжевый",
        "#DC3D43" => "Красный",
        "#E56A9A" => "Розовый",
        "#9C7BC7" => "Сиреневый",
        "#6C5CE7" => "Фиолетовый",
        "#A1887F" => "Коричневый",
        "#6B7280" => "Серый",
        _ => "Сине-серый",
    }}{(IsSelected ? ", выбран" : string.Empty)}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(AccessibleName));
}

/// <summary>Чип выбора иконки (ключ IconKeys при создании категории).</summary>
public partial class IconChip : ObservableObject
{
    public required string Key { get; init; }
    public string AccessibleName => $"{Key switch
    {
        "ShoppingCart" => "Покупки",
        "Restaurant" => "Кафе и рестораны",
        "DirectionsCar" => "Транспорт",
        "Home" => "Дом",
        "HealthAndSafety" => "Здоровье",
        "School" => "Образование",
        "Pets" => "Питомцы",
        "Flight" => "Путешествия",
        _ => "Другая иконка",
    }}{(IsSelected ? ", выбрана" : string.Empty)}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(AccessibleName));
}
