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
        "#92B7CE" or "#A6C5D8" => "Голубой",
        "#8CBDB8" => "Бирюзовый",
        "#91B5A2" or "#A7C69E" or "#C3CE9A" => "Зелёный",
        "#E3C98F" => "Жёлтый",
        "#D9A18A" or "#E8B39B" => "Абрикосовый",
        "#D5A1B3" => "Розовый",
        "#C3A5C9" or "#ADA6CE" => "Лавандовый",
        "#A9B5D2" => "Синий",
        "#BAA99F" => "Коричневый",
        "#AFB9BA" or "#9EAEBB" => "Серый",
        _ => $"Ранее выбранный цвет {Hex}",
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
