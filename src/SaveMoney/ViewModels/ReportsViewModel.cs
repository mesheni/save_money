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
    public required string Icon { get; init; }
    public required string Name { get; init; }
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

    // Палитра секторов круговой — «Clean»: акцент + нейтральная серая шкала.
    private static readonly SKColor[] Palette =
    [
        new(0xFF, 0x6B, 0x00),
        new(0x5A, 0x5A, 0x64),
        new(0x78, 0x78, 0x82),
        new(0x93, 0x93, 0x9D),
        new(0xAB, 0xAB, 0xB5),
        new(0xC2, 0xC2, 0xCB),
        new(0xD6, 0xD6, 0xDE),
        new(0xE8, 0xE8, 0xEE),
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

    /// <summary>Расход за период числом (в рублях) — для count-up анимации на странице.</summary>
    public double PeriodExpenseValue { get; private set; }

    [ObservableProperty]
    public partial bool HasData { get; set; } = true;

    [ObservableProperty]
    public partial ISeries[] PieSeries { get; set; } = [];

    [ObservableProperty]
    public partial ISeries[] ColumnSeries { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] XAxes { get; set; } = [new Axis()];

    [ObservableProperty]
    public partial Axis[] YAxes { get; set; } = [new Axis { MinLimit = 0 }];

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
        PeriodExpenseValue = expense / 100.0;
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
        // Нулевые суммы выкидываем: срез с 0 при нормализации LiveCharts
        // рисуется полным кругом («0,00 ₽» выглядел как заполненный донат).
        var slices = _reports.GetExpenseByCategory(from, to)
            .Where(s => s.AmountMinor > 0)
            .ToList();
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
                Name = slice.Name,
                Values = [slice.AmountMinor / 100.0],
                Fill = new SolidColorPaint(Palette[sliceIndex % Palette.Length]),
                InnerRadius = 60,
                Pushout = 10,
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
                Fill = new SolidColorPaint(new SKColor(0xD9, 0xD9, 0xE0)),
                InnerRadius = 60,
                Pushout = 10,
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
                // Сплошная заливка: LinearGradientPaint на Android рисует столбцы
                // волосяной линией вместо полного столбца.
                Fill = new SolidColorPaint(new SKColor(0xDC, 0x3D, 0x43)),
                MaxBarWidth = 26,
            },
            new ColumnSeries<double>
            {
                Name = "Доход",
                Values = points.Select(p => p.IncomeMinor / 100.0).ToArray(),
                Fill = new SolidColorPaint(new SKColor(0x2E, 0x94, 0x55)),
                MaxBarWidth = 26,
            },
        ];

        // Подписи осей перекрашиваем под текущую тему: дефолтная серая краска
        // LiveCharts не читается на тёмной карточке.
        var labelsPaint = new SolidColorPaint(TextPaintForTheme());
        XAxes = [new Axis { Labels = points.Select(p => p.Label).ToArray(), LabelsRotation = 45, LabelsPaint = labelsPaint }];
        YAxes = [new Axis { MinLimit = 0, LabelsPaint = labelsPaint }];
    }

    private static SKColor TextPaintForTheme()
    {
        // Токены TextMuted: светлая #8E8E98 / тёмная #7C7C86
        return Application.Current?.RequestedTheme == AppTheme.Dark
            ? new SKColor(0x7C, 0x7C, 0x86)
            : new SKColor(0x8E, 0x8E, 0x98);
    }

    private void BuildTop(long from, long to)
    {
        var slices = _reports.GetExpenseByCategory(from, to)
            .Where(s => s.AmountMinor > 0);

        TopRows.Clear();
        foreach (var slice in slices.Take(10))
        {
            TopRows.Add(new TopCategoryRow
            {
                Icon = slice.Icon,
                Name = slice.Name,
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
            var path = await Task.Run(() =>
                _csv.ExportTransactions(from, to, Path.Combine(FileSystem.AppDataDirectory, "exports")));
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
