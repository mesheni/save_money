using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Messaging;

namespace SaveMoney.ViewModels;

/// <summary>Чип списка «Все категории»: плоская строка с отступом для подкатегорий.</summary>
public partial class CategoryRow(string id, string name, string icon, int depth) : ObservableObject
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public string Icon { get; } = icon;
    public int Depth { get; } = depth;
    public string Display => $"{Icon} {Name}";
    public string AccessibleName => Depth > 0 ? $"Подкатегория {Name}" : $"Категория {Name}";
}

/// <summary>
/// Полный список категорий для выбора на экране ввода (роут "categorypicker?kind=expense|income").
/// Тап по строке выбирает категорию, рассылает CategoryPickedMessage и закрывает экран.
/// </summary>
[QueryProperty(nameof(Kind), "kind")]
public partial class CategoryPickerViewModel(AppDatabase db) : ObservableObject
{
    private readonly AppDatabase _db = db;

    public ObservableCollection<CategoryRow> Items { get; } = [];

    /// <summary>"expense" / "income" из query.</summary>
    public string Kind { get; set; } = "expense";

    public void Initialize()
    {
        var kind = Kind == "income" ? CategoryKind.Income : CategoryKind.Expense;
        Items.Clear();
        foreach (var root in _db.Categories.GetRoots(kind))
        {
            Items.Add(new CategoryRow(root.Id, root.Name, root.Icon ?? IconKeys.Other, depth: 0));
            foreach (var child in _db.Categories.GetChildren(root.Id))
            {
                Items.Add(new CategoryRow(child.Id, child.Name, child.Icon ?? IconKeys.Other, depth: 1));
            }
        }
    }

    public Func<Task>? CloseAsync { get; set; }

    [RelayCommand]
    private async Task PickAsync(CategoryRow? row)
    {
        if (row is null)
        {
            return;
        }

        CategoryPickedMessage.Broadcast(row.Id);
        if (CloseAsync is not null)
        {
            await CloseAsync();
        }
    }
}
