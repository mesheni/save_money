using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Операции над категориями поверх репозитория: создание с сортировкой и каскадное удаление.</summary>
public sealed class CategoryService(AppDatabase database)
{
    /// <summary>Создаёт корневую категорию в конец списка.</summary>
    public Category AddRoot(string name, string kind, string? icon)
    {
        var siblings = database.Categories.GetRoots(kind);
        var category = new Category
        {
            Name = name.Trim(),
            Kind = kind,
            Icon = string.IsNullOrWhiteSpace(icon) ? "🏷" : icon,
            Sort = siblings.Count == 0 ? 0 : siblings.Max(c => c.Sort) + 1,
        };
        database.Categories.Save(category);
        return category;
    }

    /// <summary>Создаёт подкатегорию у корневой.</summary>
    public Category AddChild(string parentId, string name, string? icon)
    {
        var parent = database.Categories.Get(parentId)
            ?? throw new InvalidOperationException("Родительская категория не найдена");

        var category = new Category
        {
            Name = name.Trim(),
            Kind = parent.Kind,
            ParentId = parentId,
            Icon = string.IsNullOrWhiteSpace(icon) ? parent.Icon : icon,
            Sort = database.Categories.GetChildren(parentId).Count,
        };
        database.Categories.Save(category);
        return category;
    }

    /// <summary>Мягкое удаление категории вместе с подкатегориями. Системные — нельзя.</summary>
    public void DeleteWithChildren(Category category)
    {
        if (category.ParentId is null)
        {
            foreach (var child in database.Categories.GetChildren(category.Id))
            {
                database.Categories.SoftDelete(child);
            }
        }

        database.Categories.SoftDelete(category);
    }
}
