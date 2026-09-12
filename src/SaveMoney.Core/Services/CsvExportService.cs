using System.Globalization;
using System.Text;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>
/// CSV-экспорт операций: UTF-8 с BOM, разделитель «;» (открывается русским Excel без настроек).
/// Бесплатен всегда — принцип «данные не блокируются никогда».
/// </summary>
public sealed class CsvExportService(AppDatabase database)
{
    private readonly AppDatabase _db = database;

    public string ExportTransactions(long fromUnix, long toUnix, string directory)
    {
        Directory.CreateDirectory(directory);

        var transactions = _db.Transactions.GetBetween(fromUnix, toUnix);
        var accounts = _db.Accounts.GetAll().ToDictionary(a => a.Id, a => a.Name);
        var categories = _db.Categories.GetAllActive().ToDictionary(c => c.Id);

        var sb = new StringBuilder();
        sb.AppendLine("Дата;Тип;Сумма;Категория;Счёт;Счёт-получатель;Магазин;Заметка");

        foreach (var tx in transactions)
        {
            var date = DateTimeOffset.FromUnixTimeSeconds(tx.DateUnix).ToLocalTime();
            var kindName = tx.Kind switch
            {
                TransactionKind.Expense => "Расход",
                TransactionKind.Income => "Доход",
                TransactionKind.Transfer => "Перевод",
                _ => tx.Kind,
            };

            var amountMinor = tx.Kind == TransactionKind.Expense ? -tx.AmountMinor : tx.AmountMinor;

            string CategoryName(string? id) =>
                id is not null && categories.TryGetValue(id, out var c) ? c.Name : "";

            var fields = new[]
            {
                date.ToString("dd.MM.yyyy HH:mm"),
                kindName,
                ToExcelNumber(amountMinor),
                CategoryName(tx.CategoryId),
                accounts.GetValueOrDefault(tx.AccountId, "(счёт удалён)"),
                tx.TransferAccountId is null ? "" : accounts.GetValueOrDefault(tx.TransferAccountId, "(счёт удалён)"),
                tx.Payee ?? "",
                tx.Note ?? "",
            };

            sb.AppendLine(string.Join(';', fields.Select(Escape)));
        }

        var fileName = $"savemoney_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        return path;
    }

    internal static string ToExcelNumber(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    internal static string Escape(string field)
    {
        if (field.Contains(';') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
