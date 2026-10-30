namespace SaveMoney.Messaging;

/// <summary>
/// Пользователь выбрал категорию на экране «Все категории» (CategoryPickerPage).
/// Экран ввода выделяет её и при quick save сразу сохраняет.
/// Результат доставляется только экрану, последним открывшему выбор категории.
/// </summary>
public static class CategoryPickedMessage
{
    private static Action<string>? _recipient;

    public static void Register(Action<string> recipient) => _recipient = recipient;
    public static void Unregister(Action<string> recipient)
    {
        if (_recipient == recipient) _recipient = null;
    }
    public static void Broadcast(string categoryId)
    {
        var recipient = _recipient;
        _recipient = null;
        recipient?.Invoke(categoryId);
    }
}
