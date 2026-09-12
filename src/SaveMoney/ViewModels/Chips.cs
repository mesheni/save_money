using CommunityToolkit.Mvvm.ComponentModel;

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
    public string Icon { get; init; } = "❓";

    public string Display => $"{Icon} {Name}";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Чип выбора цвета (палитра счёта).</summary>
public partial class ColorChip : ObservableObject
{
    public required string Hex { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Чип выбора иконки (эмодзи при создании категории).</summary>
public partial class IconChip : ObservableObject
{
    public required string Emoji { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
