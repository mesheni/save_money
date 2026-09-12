using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using System.Text;
using Xunit;

namespace SaveMoney.Tests;

public class ReportAndCsvTests : IDisposable
{
    private readonly TestDatabase _test = new();

    public void Dispose() => _test.Dispose();

    private static long Unix(int year, int month, int day, int hour = 12) =>
        new DateTimeOffset(year, month, day, hour, 0, 0, TimeSpan.FromHours(3)).ToUnixTimeSeconds();

    private void Save(string kind, long amount, string? categoryId, long date) =>
        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = "a1",
            CategoryId = categoryId,
            AmountMinor = amount,
            Kind = kind,
            DateUnix = date,
        });

    // --- ReportService ---

    [Fact]
    public void Breakdown_GroupsByCategory_AndComputesShare()
    {
        var groceries = _test.Db.Categories.GetAllActive().First(c => c.Name == "Продукты");
        var transport = _test.Db.Categories.GetAllActive().First(c => c.Name == "Транспорт");

        Save(TransactionKind.Expense, 7_000, groceries.Id, Unix(2026, 9, 1));
        Save(TransactionKind.Expense, 3_000, transport.Id, Unix(2026, 9, 2));
        Save(TransactionKind.Expense, 1_000, null, Unix(2026, 9, 3));
        Save(TransactionKind.Income, 50_000, null, Unix(2026, 9, 4)); // не учитывается
        Save(TransactionKind.Expense, 9_000, groceries.Id, Unix(2026, 10, 1)); // вне периода

        var slices = new ReportService(_test.Db).GetExpenseByCategory(Unix(2026, 9, 1), Unix(2026, 10, 1));

        Assert.Equal(3, slices.Count);
        Assert.Equal(7_000, slices[0].AmountMinor); // по убыванию
        Assert.Equal("Продукты", slices[0].Name);
        Assert.Equal(63.6, slices[0].SharePercent, 1); // 7000 из 11000
        Assert.Equal("Без категории", slices[^1].Name);
    }

    [Fact]
    public void Dynamics_DailyBuckets_ShortPeriod()
    {
        Save(TransactionKind.Expense, 1_000, null, Unix(2026, 9, 1, 8));
        Save(TransactionKind.Expense, 2_000, null, Unix(2026, 9, 1, 20));
        Save(TransactionKind.Income, 5_000, null, Unix(2026, 9, 2));

        var points = new ReportService(_test.Db).GetDynamics(Unix(2026, 9, 1, 0), Unix(2026, 9, 5, 0));

        Assert.Equal(4, points.Count); // 4 дня
        Assert.Equal(3_000, points[0].ExpenseMinor);
        Assert.Equal(5_000, points[1].IncomeMinor);
        Assert.Equal(0, points[2].ExpenseMinor);
    }

    [Fact]
    public void Dynamics_WeekBuckets_LongPeriod()
    {
        var points = new ReportService(_test.Db).GetDynamics(Unix(2026, 1, 1), Unix(2026, 3, 1));

        // ~60 дней → недели
        var expected = (int)Math.Ceiling(59 / 7.0);
        Assert.Equal(expected, points.Count);
    }

    [Fact]
    public void Totals_IncomeAndExpense()
    {
        Save(TransactionKind.Expense, 1_000, null, Unix(2026, 9, 1));
        Save(TransactionKind.Income, 4_000, null, Unix(2026, 9, 1));
        Save(TransactionKind.Expense, 500, null, Unix(2026, 9, 2));

        var (income, expense) = new ReportService(_test.Db).GetTotals(Unix(2026, 9, 1), Unix(2026, 9, 3));
        Assert.Equal(4_000, income);
        Assert.Equal(1_500, expense);
    }

    // --- CsvExportService ---

    [Fact]
    public void Csv_Export_WritesHeader_Rows_AndBom()
    {
        var account = new Account { Name = "Сбер" };
        _test.Db.Accounts.Save(account);
        var cat = _test.Db.Categories.GetAllActive().First(c => c.Name == "Продукты");

        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = account.Id,
            CategoryId = cat.Id,
            AmountMinor = 12_345,
            Kind = TransactionKind.Expense,
            DateUnix = Unix(2026, 9, 10, 18),
            Payee = "Пятёрочка; Ленина",
            Note = "Продукты \"к вечеру\"",
        });
        _test.Db.Transactions.Save(new Transaction
        {
            AccountId = account.Id,
            TransferAccountId = "other",
            AmountMinor = 5_000,
            Kind = TransactionKind.Transfer,
            DateUnix = Unix(2026, 9, 11, 10),
        });

        var dir = Path.Combine(Path.GetTempPath(), $"savemoney-csv-{Guid.NewGuid():N}");
        var path = new CsvExportService(_test.Db).ExportTransactions(Unix(2026, 9, 1), Unix(2026, 9, 30), dir);

        try
        {
            var content = File.ReadAllText(path, Encoding.UTF8);
            var lines = content.TrimEnd().Split('\n');

            Assert.Equal("Дата;Тип;Сумма;Категория;Счёт;Счёт-получатель;Магазин;Заметка", lines[0].TrimEnd('\r'));
            Assert.Equal(3, lines.Length);
            // сортировка по убыванию даты: перевод 11.09 раньше расхода 10.09
            Assert.Contains("11.09.2026 10:00;Перевод;50,00;;Сбер;(счёт удалён);;", lines[1]);
            Assert.Contains("10.09.2026 18:00;Расход;-123,45;Продукты;Сбер;;\"Пятёрочка; Ленина\";\"Продукты \"\"к вечеру\"\"\"", lines[2]);

            // BOM для Excel
            var bytes = File.ReadAllBytes(path);
            Assert.Equal(0xEF, bytes[0]);
            Assert.Equal(0xBB, bytes[1]);
            Assert.Equal(0xBF, bytes[2]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
