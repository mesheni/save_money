using CommunityToolkit.Mvvm.Messaging;

namespace SaveMoney.Messaging;

/// <summary>
/// Данные, влияющие на агрегаты (баланс по счетам, итоги), изменились:
/// записана/изменена/удалена операция, отредактирован счёт, материализованы
/// регулярные платежи. Подписчики (например, вкладка «Ввод») перечитывают свои
/// значения сразу, не дожидаясь следующего Appearing.
/// </summary>
public record TransactionsChangedMessage
{
    /// <summary>Отправить всем подписчикам (WeakReferenceMessenger — слабые ссылки, утечек нет).</summary>
    public static void Broadcast() => WeakReferenceMessenger.Default.Send(new TransactionsChangedMessage());
}
