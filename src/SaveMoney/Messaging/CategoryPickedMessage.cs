namespace SaveMoney.Messaging;

/// <summary>
/// Пользователь выбрал категорию на экране «Все категории» (CategoryPickerPage).
/// Экран ввода выделяет её и при quick save сразу сохраняет.
/// Статическое событие вместо WeakReferenceMessenger: state-регистрации
/// мессенджера доставляли это сообщение ненадёжно (выбор не применялся).
/// Подписчики — экземпляры QuickAddViewModel, живут столько же, сколько процесс.
/// </summary>
public static class CategoryPickedMessage
{
    public static event Action<string>? Picked;

    public static void Broadcast(string categoryId) => Picked?.Invoke(categoryId);
}
