using CommunityToolkit.Mvvm.Messaging;

namespace SaveMoney.Messaging;

/// <summary>
/// Пользователь выбрал категорию на экране «Все категории» (CategoryPickerPage).
/// Экран ввода выделяет её у себя и закрывает тап по сохранению (quick save).
/// </summary>
/// <param name="CategoryId">Id выбранной категории (корневой или подкатегории).</param>
public record CategoryPickedMessage(string CategoryId)
{
    public static void Broadcast(string categoryId) =>
        WeakReferenceMessenger.Default.Send(new CategoryPickedMessage(categoryId));
}
