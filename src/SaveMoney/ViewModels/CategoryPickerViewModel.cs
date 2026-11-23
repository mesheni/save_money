using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.ViewModels;

public sealed record CategoryRow(string Id, string Name, string Icon, bool IsSelected, bool HasChildren)
{
    public bool ShowChildrenArrow => HasChildren;
    public bool ShowCheck => IsSelected && !HasChildren;
    public string AccessibleName => $"{Name}{(IsSelected ? ", выбрана" : "")}{(HasChildren ? ", есть подкатегории" : "")}";
}

[QueryProperty(nameof(Kind), "kind")]
public partial class CategoryPickerViewModel(AppDatabase db) : ObservableObject
{
    public ObservableCollection<CategoryRow> Items { get; } = [];
    public string Kind { get; set; } = "expense";
    public string? SelectedId { get; set; }
    private Category? _parent;
    public bool HasParent => _parent is not null;
    public string ParentName => _parent?.Name ?? "Категории";
    public bool HasNoItems => Items.Count == 0;
    public Func<string, Task>? CompleteAsync { get; set; }

    [ObservableProperty] public partial string SearchText { get; set; } = "";
    partial void OnSearchTextChanged(string value) => Reload();

    /// <summary>Корень выбранной категории (сама она или её родитель) — отмечается в списке корней.</summary>
    private string? _selectedRootId;

    /// <summary>
    /// Экран всегда открывается со списком корневых категорий: раньше он открывался
    /// внутри текущей, и для смены категории нужен был лишний шаг «Все категории».
    /// </summary>
    public void Initialize()
    {
        _parent = null;
        var selected = SelectedId is not null ? db.Categories.Get(SelectedId) : null;
        _selectedRootId = selected?.ParentId ?? selected?.Id;
        SearchText = "";
        Reload();
    }

    private void Reload()
    {
        var kind = Kind == "income" ? CategoryKind.Income : CategoryKind.Expense;
        var query = SearchText.Trim();
        Items.Clear();

        if (query.Length > 0)
        {
            // Поиск сквозной: и корни, и подкатегории («такси» находит «Транспорт → Такси»).
            var all = db.Categories.GetAllActive().Where(c => c.Kind == kind).ToList();
            var byId = all.ToDictionary(c => c.Id);
            foreach (var category in all.Where(c => c.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)))
            {
                var parent = category.ParentId is { } pid && byId.TryGetValue(pid, out var p) ? p : null;
                if (category.ParentId is not null && parent is null) continue; // родитель удалён
                Items.Add(new CategoryRow(category.Id,
                    parent is null ? category.Name : $"{parent.Name} → {category.Name}",
                    category.Icon ?? parent?.Icon ?? IconKeys.Other,
                    category.Id == SelectedId,
                    parent is null && all.Any(c => c.ParentId == category.Id)));
            }
        }
        else
        {
            var categories = _parent is null ? db.Categories.GetRoots(kind) : db.Categories.GetChildren(_parent.Id);
            foreach (var category in categories)
                Items.Add(new CategoryRow(category.Id, category.Name, category.Icon ?? _parent?.Icon ?? IconKeys.Other,
                    category.Id == SelectedId || (_parent is null && category.Id == _selectedRootId),
                    db.Categories.GetChildren(category.Id).Count > 0));
        }

        OnPropertyChanged(nameof(HasParent));
        OnPropertyChanged(nameof(ParentName));
        OnPropertyChanged(nameof(HasNoItems));
    }

    [RelayCommand]
    private void ShowRoots()
    {
        _parent = null;
        SearchText = "";
        Reload();
    }

    [RelayCommand]
    private async Task UseParentAsync()
    {
        if (_parent is not null && CompleteAsync is not null) await CompleteAsync(_parent.Id);
    }

    [RelayCommand]
    private async Task PickAsync(CategoryRow? row)
    {
        if (row is null) return;
        if (row.HasChildren)
        {
            _parent = db.Categories.Get(row.Id);
            SearchText = "";
            Reload();
        }
        else if (CompleteAsync is not null) await CompleteAsync(row.Id);
    }
}
