using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using SkiaSharp;
using SaveMoney.Core.Database;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

public partial class TopCategoryRow
{
    public required string Display { get; init; }
    public required string AmountText { get; init; }
    public required string PercentText { get; init; }
    public required double Progress { get; init; }
}

/// <summary>Экран отчётов: период (включая цикл и произвольный), круговая, динамика, топ категорий, CSV.</summary>
public partial class ReportsViewModel(
    AppDatabase db,
    ReportService reports,
    CycleService cycle,
    CsvExportService csv) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly ReportService _reports = reports;
    private readonly CycleService _cycle = cycle;
    private readonly CsvExportService _csv = csv;

    // Палитра секторов круговой — из контракта Vibrant.
    private static readonly SKColor[] Palette =
    [
        new(0xFF, 0x6B, 0x00),
        new(0x2E, 0x9D, 0x57),
        new(0xFF, 0xB0, 0x20),
        new(0x4C, 0x42, 0x6C),
        new(0xE5, 0x48, 0x4D),
        new(0x79, 0x6F, 0x91),
        new(0xC2, 0x51, 0x00),
        new(0xEF, 0xC9, 0x68),
    ];

    public IReadOnlyList<string> PeriodOptions { get; } =
        ["Неделя", "Месяц", "3 месяца", "Текущий цикл", "Всё", "Свой период"];

    [ObservableProperty]
    public partial int PeriodIndex { get; set; } = 1;

    [ObservableProperty]
    public partial DateTime FromDate { get; set; } = DateTime.Now.AddMonths(-1);

    [ObservableProperty]
    public partial DateTime ToDate { get; set; } = DateTime.Now;

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    public partial string PeriodExpenseText { get; set; } = "—";

    [ObservableProperty]
    public partial bool HasData { get; set; } = true;

    [ObservableProperty]
    public partial ISeries[] PieSeries { get; set; } = [];

    [ObservableProperty]
    public partial ISeries[] ColumnSeries { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] XAxes { get; set; } = [new Axis()];

    public Axis[] YAxes { get; } = [new Axis { MinLimit = 0 }];

    public ObservableCollection<TopCategoryRow> TopRows { get; } = [];

    public Func<string, string, Task>? AlertAsync { get; set; }

    public bool IsCustom => PeriodIndex == 5;

    public Task InitializeAsync()
    {
        Recompute();
        return Task.CompletedTask;
    }

    partial void OnPeriodIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustom));
        Recompute();
    }

    partial void OnFromDateChanged(DateTime value) => Recompute();
    partial void OnToDateChanged(DateTime value) => Recompute();

    private (long From, long To) GetRange()
    {
        var now = DateTimeOffset.Now;
        var fromLocal = PeriodIndex switch
        {
            0 => now.AddDays(-7).LocalDateTime,
            1 => now.AddMonths(-1).LocalDateTime,
            2 => now.AddMonths(-3).LocalDateTime,
            3 => _cycle.GetCurrentCycle().Start,
            5 => FromDate.Date,
            _ => new DateTime(2000, 1, 1),
        };
        var toLocal = PeriodIndex == 5 ? ToDate.Date.AddDays(1) : now.LocalDateTime.AddDays(1);

        return (ToUnix(fromLocal), ToUnix(toLocal));
    }

    private void Recompute()
    {
        var (from, to) = GetRange();
        var (income, expense) = _reports.GetTotals(from, to);

        HasData = income != 0 || expense != 0;
        PeriodExpenseText = HasData ? MoneyFormat.Rubles(expense) : "—";
        SummaryText = HasData
            ? $"Доход {MoneyFormat.Rubles(income)}"
            : "Нет операций за период";

        BuildPie(from, to);
        BuildColumns(from, to);
        BuildTop(from, to);
    }

    private void BuildPie(long from, long to)
    {
        var slices = _reports.GetExpenseByCategory(from, to);
        if (slices.Count == 0)
        {
            PieSeries = [];
            return;
        }

        const int maxSlices = 7;
        var series = new List<ISeries>();
        var sliceIndex = 0;
        foreach (var slice in slices.Take(maxSlices))
        {
            series.Add(new PieSeries<double>
            {
                Name = $"{slice.Icon} {slice.Name}",
                Values = [slice.AmountMinor / 100.0],
                Fill = new SolidColorPaint(Palette[sliceIndex % Palette.Length]),
            });
            sliceIndex++;
        }

        var rest = slices.Skip(maxSlices).Sum(s => s.AmountMinor);
        if (rest > 0)
        {
            series.Add(new PieSeries<double>
            {
                Name = "Прочие",
                Values = [rest / 100.0],
                Fill = new SolidColorPaint(new SKColor(0xEA, 0xDF, 0xBA)),
            });
        }

        PieSeries = series.ToArray();
    }

    private void BuildColumns(long from, long to)
    {
        var points = _reports.GetDynamics(from, to);

        ColumnSeries =
        [
            new ColumnSeries<double>
            {
                Name = "Расход",
                Values = points.Select(p => p.ExpenseMinor / 100.0).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0xE5, 0x48, 0x4D)),
            },
            new ColumnSeries<double>
            {
                Name = "Доход",
                Values = points.Select(p => p.IncomeMinor / 100.0).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0x2E, 0x9D, 0x57)),
            },
        ];

        XAxes = [new Axis { Labels = points.Select(p => p.Label).ToArray(), LabelsRotation = 45 }];
    }

    private void BuildTop(long from, long to)
    {
        var slices = _reports.GetExpenseByCategory(from, to);

        TopRows.Clear();
        foreach (var slice in slices.Take(10))
        {
            TopRows.Add(new TopCategoryRow
            {
                Display = $"{slice.Icon} {slice.Name}",
                AmountText = MoneyFormat.Rubles(slice.AmountMinor),
                PercentText = $"{slice.SharePercent:F0}%",
                Progress = Math.Min(1.0, slice.SharePercent / 100.0),
            });
        }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        try
        {
            var (from, to) = GetRange();
            var path = _csv.ExportTransactions(from, to, Path.Combine(FileSystem.AppDataDirectory, "exports"));
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = Path.GetFileName(path),
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            await AlertAsync?.Invoke("Ошибка экспорта", ex.Message)!;
        }
    }

    private static long ToUnix(DateTime local) =>
        new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUnixTimeSeconds();
}
