using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.ViewModels;

public sealed record CategoryRow(string Id, string Name, string Icon, bool IsSelected, bool HasChildren)
{
    public bool ShowChildrenArrow => HasChildren && !IsSelected;
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

    public void Initialize()
    {
        _parent = null;
        var selected = SelectedId is not null ? db.Categories.Get(SelectedId) : null;
        if (selected?.ParentId is { } parentId) _parent = db.Categories.Get(parentId);
        else if (selected is not null && db.Categories.GetChildren(selected.Id).Count > 0) _parent = selected;
        SearchText = "";
        Reload();
    }

    private void Reload()
    {
        var kind = Kind == "income" ? CategoryKind.Income : CategoryKind.Expense;
        var categories = _parent is null ? db.Categories.GetRoots(kind) : db.Categories.GetChildren(_parent.Id);
        Items.Clear();
        foreach (var category in categories.Where(c => string.IsNullOrWhiteSpace(SearchText)
            || c.Name.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            Items.Add(new CategoryRow(category.Id, category.Name, category.Icon ?? IconKeys.Other,
                category.Id == SelectedId, db.Categories.GetChildren(category.Id).Count > 0));
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
