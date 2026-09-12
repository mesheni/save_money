using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>Строка плоского списка категорий: корень уровня 0, подкатегория уровня 1.</summary>
public partial class CategoryRowVM : ObservableObject
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required bool IsRoot { get; init; }
    public required bool CanDelete { get; init; }

    public double Indent => IsRoot ? 0 : 40;

    public Thickness RowMargin => new(Indent, 0, 0, 0);

    public bool ShowAddChild => IsRoot;
}

public partial class CategoriesViewModel(AppDatabase db, CategoryService categories) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly CategoryService _categories = categories;

    public ObservableCollection<CategoryRowVM> Rows { get; } = [];

    public IReadOnlyList<string> KindOptions { get; } = ["Расходы", "Доходы"];

    [ObservableProperty]
    public partial int KindIndex { get; set; }

    private string Kind => KindIndex == 1 ? CategoryKind.Income : CategoryKind.Expense;

    public Task InitializeAsync()
    {
        Reload();
        return Task.CompletedTask;
    }

    private void Reload()
    {
        Rows.Clear();
        foreach (var root in _db.Categories.GetRoots(Kind))
        {
            Rows.Add(new CategoryRowVM
            {
                Id = root.Id,
                Name = root.Name,
                Icon = root.Icon ?? "❓",
                IsRoot = true,
                CanDelete = !root.IsDefault,
            });

            foreach (var child in _db.Categories.GetChildren(root.Id))
            {
                Rows.Add(new CategoryRowVM
                {
                    Id = child.Id,
                    Name = child.Name,
                    Icon = child.Icon ?? "❓",
                    IsRoot = false,
                    CanDelete = !child.IsDefault,
                });
            }
        }
    }

    partial void OnKindIndexChanged(int value) => Reload();

    [RelayCommand]
    private void AddRoot() => Shell.Current.GoToAsync($"category?kind={Kind}");

    [RelayCommand]
    private void AddChild(CategoryRowVM? row)
    {
        if (row?.IsRoot == true)
        {
            Shell.Current.GoToAsync($"category?kind={Kind}&parent={row.Id}");
        }
    }

    public async Task<bool> DeleteAsync(CategoryRowVM? row)
    {
        if (row is null || !row.CanDelete)
        {
            return false;
        }

        var category = _db.Categories.Get(row.Id);
        if (category is null)
        {
            return false;
        }

        var message = row.IsRoot
            ? $"«{row.Name}» и её подкатегории будут удалены."
            : $"Подкатегория «{row.Name}» будет удалена.";

        var confirmed = await Shell.Current.DisplayAlertAsync("Удалить категорию?", message, "Удалить", "Отмена");
        if (!confirmed)
        {
            return false;
        }

        _categories.DeleteWithChildren(category);
        Reload();
        return true;
    }
}

[QueryProperty(nameof(ParentId), "parent")]
[QueryProperty(nameof(Kind), "kind")]
public partial class CategoryEditViewModel(AppDatabase db, CategoryService categories) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly CategoryService _categories = categories;
    private string? _parentId;
    private bool _initialized;

    public static readonly string[] IconOptions =
    [
        "🏷", "🛒", "🍔", "🚗", "🏠", "💊", "👕", "🎬",
        "🎮", "📚", "🎁", "✈️", "🐾", "💳", "☕", "🍕",
        "🏃", "👶", "🎓", "🧾", "💰", "🏦", "📦", "❓",
    ];

    public ObservableCollection<IconChip> Icons { get; } = [];

    [ObservableProperty]
    public partial string Title { get; set; } = "Новая категория";

    [ObservableProperty]
    public partial string Name { get; set; } = "";

    [ObservableProperty]
    public partial string Kind { get; set; } = CategoryKind.Expense;

    public Func<string, string, Task>? AlertAsync { get; set; }

    public string? ParentId
    {
        get => _parentId;
        set => _parentId = value;
    }

    public Task InitializeAsync()
    {
        if (_initialized)
        {
            return Task.CompletedTask;
        }

        _initialized = true;

        foreach (var emoji in IconOptions)
        {
            Icons.Add(new IconChip { Emoji = emoji });
        }

        Icons[0].IsSelected = true;

        if (_parentId is not null)
        {
            var parent = _db.Categories.Get(_parentId);
            Title = parent is null ? "Новая подкатегория" : $"В «{parent.Name}»";
            Kind = parent?.Kind ?? Kind;
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private void SelectIcon(IconChip? chip)
    {
        if (chip is null)
        {
            return;
        }

        foreach (var item in Icons)
        {
            item.IsSelected = item == chip;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            await AlertAsync?.Invoke("Ошибка", "Введите название")!;
            return;
        }

        var icon = Icons.FirstOrDefault(i => i.IsSelected)?.Emoji;

        if (_parentId is not null)
        {
            _categories.AddChild(_parentId, Name, icon);
        }
        else
        {
            _categories.AddRoot(Name, Kind, icon);
        }

        await Shell.Current.GoToAsync("..");
    }
}
