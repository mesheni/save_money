using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Maui.Graphics;
using SkiaSharp;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

public partial class TopCategoryRow
{
    public required string Name { get; init; }
    public required string AmountText { get; init; }
    public required string PercentText { get; init; }
    public required double Progress { get; init; }

    /// <summary>Цвет квадрата и полоски — из палитры кольца.</summary>
    public required Color BarColor { get; init; }
}

public sealed record DailyReportRow(string DateText, string AmountText);

/// <summary>
/// Экран отчётов по макету: пилюля периода, сегмент «расходы/доходы», кольцо с суммой
/// и дельтой к прошлому периоду, топ категорий, столбики по дням, CSV.
/// </summary>
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

    // Палитра секторов кольца — акцент + нейтральная шкала (как в макете).
    // Тёмная тема — свои ступени: тёмно-графитовые срезы на тёмном фоне не видны.
    private static readonly SKColor[] PaletteLight =
    [
        new(0xFF, 0x6B, 0x00),
        new(0x17, 0x17, 0x1C),
        new(0x6B, 0x6B, 0x7A),
        new(0xA9, 0xA9, 0xB6),
        new(0xDA, 0xDA, 0xE1),
        new(0xC7, 0xC7, 0xCF),
        new(0x8C, 0x8C, 0x99),
        new(0xB5, 0xB5, 0xBE),
    ];

    private static readonly SKColor[] PaletteDark =
    [
        new(0xFF, 0x7A, 0x1A),
        new(0xF2, 0xF2, 0xF4),
        new(0xB9, 0xB9, 0xC2),
        new(0x8E, 0x8E, 0x98),
        new(0x6B, 0x6B, 0x7A),
        new(0x54, 0x54, 0x60),
        new(0x45, 0x45, 0x50),
        new(0x7C, 0x7C, 0x86),
    ];

    private static SKColor[] Palette =>
        Application.Current?.RequestedTheme == AppTheme.Dark ? PaletteDark : PaletteLight;

    private static SKColor BarsGray =>
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? new SKColor(0x45, 0x45, 0x50)
            : new SKColor(0xDA, 0xDA, 0xE1);

    private static readonly SKColor BarsAccent = new(0xFF, 0x6B, 0x00);

    private static readonly string[] PeriodList = ["Неделя", "Месяц", "3 месяца", "Текущий цикл", "Всё", "Свой период"];

    public IReadOnlyList<string> PeriodOptions => PeriodList;

    [ObservableProperty]
    public partial int PeriodIndex { get; set; } = 1;

    [ObservableProperty]
    public partial DateTime FromDate { get; set; } = DateTime.Now.AddMonths(-1);

    [ObservableProperty]
    public partial DateTime ToDate { get; set; } = DateTime.Now;

    /// <summary>true — на экране расходы, false — доходы.</summary>
    [ObservableProperty]
    public partial bool IsExpenses { get; set; } = true;

    public bool IsIncomes => !IsExpenses;

    [ObservableProperty]
    public partial string PeriodTotalCaption { get; set; } = "Расходы за период";

    [ObservableProperty]
    public partial string PeriodTotalText { get; set; } = "—";

    /// <summary>«на 8% меньше, чем в прошлом» — пусто, если в прошлом периоде нечего сравнивать.</summary>
    [ObservableProperty]
    public partial string DeltaText { get; set; } = "";

    /// <summary>Зелёная дельта = «меньше» у расходов и «больше» у доходов.</summary>
    [ObservableProperty]
    public partial bool DeltaGood { get; set; }

    public bool HasDelta => DeltaText.Length > 0;

    [ObservableProperty]
    public partial string AvgPerDayText { get; set; } = "";

    [ObservableProperty]
    public partial bool HasData { get; set; } = true;

    /// <summary>Нет операций выбранного типа за период. Computed-свойство обязано
    /// дублировать уведомление: при SourceGen «!»-биндинги не переоцениваются сами.</summary>
    public bool HasNoData => !HasData;

    partial void OnHasDataChanged(bool value) => OnPropertyChanged(nameof(HasNoData));

    [ObservableProperty]
    public partial ISeries[] PieSeries { get; set; } = [];

    [ObservableProperty]
    public partial ISeries[] DaySeries { get; set; } = [];

    [ObservableProperty]
    public partial Axis[] DayXAxes { get; set; } = [new Axis { IsVisible = false }];

    [ObservableProperty]
    public partial Axis[] DayYAxes { get; set; } = [new Axis { IsVisible = false, MinLimit = 0 }];

    public string DaysCaption => IsExpenses ? "Расходы по дням" : "Доходы по дням";

    public ObservableCollection<TopCategoryRow> TopRows { get; } = [];

    public ObservableCollection<DailyReportRow> DailyRows { get; } = [];

    public Func<string, string, Task>? AlertAsync { get; set; }

    /// <summary>Страница подставляет выбор варианта (ActionSheet): (заголовок, варианты) → выбор или null.</summary>
    public Func<string, string[], Task<string?>>? PickAsync { get; set; }

    public bool IsCustom => PeriodIndex == 5;

    public string PeriodPillText => PeriodOptions[PeriodIndex];
    public bool PeriodPillActive => PeriodIndex != 1;

    public Task InitializeAsync()
    {
        Recompute();
        return Task.CompletedTask;
    }

    partial void OnPeriodIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustom));
        OnPropertyChanged(nameof(PeriodPillText));
        OnPropertyChanged(nameof(PeriodPillActive));
        Recompute();
    }

    partial void OnFromDateChanged(DateTime value) => Recompute();
    partial void OnToDateChanged(DateTime value) => Recompute();

    partial void OnIsExpensesChanged(bool value)
    {
        OnPropertyChanged(nameof(IsIncomes));
        OnPropertyChanged(nameof(DaysCaption));
        Recompute();
    }

    [RelayCommand]
    private async Task PickPeriodAsync()
    {
        var picked = await PickAsync?.Invoke("Период", [.. PeriodOptions])!;
        if (picked is null)
        {
            return;
        }

        var index = Array.IndexOf(PeriodList, picked);
        if (index >= 0)
        {
            PeriodIndex = index;
        }
    }

    [RelayCommand]
    private void ShowExpenses() => IsExpenses = true;

    [RelayCommand]
    private void ShowIncomes() => IsExpenses = false;

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
        var kind = IsExpenses ? TransactionKind.Expense : TransactionKind.Income;

        var (income, expense) = _reports.GetTotals(from, to);
        var total = IsExpenses ? expense : income;

        HasData = income != 0 || expense != 0;
        PeriodTotalCaption = IsExpenses ? "Расходы за период" : "Доходы за период";
        PeriodTotalText = HasData ? MoneyFormat.Rubles(total) : "—";

        // Дельта к предыдущему периоду той же длины.
        var length = Math.Max(1, to - from);
        var (prevTotal, days) = ComputePrev(from, length, kind);
        if (prevTotal > 0 && total > 0)
        {
            var change = (total - prevTotal) * 100.0 / prevTotal;
            var percent = Math.Abs((int)Math.Round(change));
            var decreased = total < prevTotal;
            DeltaText = percent == 0 ? "как в прошлом периоде" : $"на {percent}% {(decreased ? "меньше" : "больше")}, чем в прошлом";
            // У расходов меньше = хорошо; у доходов наоборот.
            DeltaGood = IsExpenses ? decreased : !decreased;
        }
        else
        {
            DeltaText = "";
        }

        OnPropertyChanged(nameof(HasDelta));

        AvgPerDayText = days > 0 ? $"В среднем {MoneyFormat.Rubles(total / days)} в день" : "";

        BuildPie(from, to, kind);
        BuildDayColumns(from, to);
        BuildTop(from, to, kind);
    }

    private (long Total, int Days) ComputePrev(long from, long length, string kind)
    {
        if (PeriodIndex == 4)
        {
            // «Всё» не имеет предыдущего периода.
            return (0, 0);
        }

        var prevFrom = from - length;
        var prevTo = from;
        var (income, expense) = _reports.GetTotals(prevFrom, prevTo);
        var days = (int)Math.Round(length / 86400.0);
        return (kind == TransactionKind.Expense ? expense : income, days);
    }

    private void BuildPie(long from, long to, string kind)
    {
        // Нулевые суммы выкидываем: срез с 0 при нормализации LiveCharts
        // рисуется полным кругом («0,00 ₽» выглядел как заполненный донат).
        var slices = _reports.GetByCategory(from, to, kind)
            .Where(s => s.AmountMinor > 0)
            .ToList();
        if (slices.Count == 0)
        {
            PieSeries = [];
            return;
        }

        const int maxSlices = 5;
        var series = new List<ISeries>();
        var sliceIndex = 0;
        foreach (var slice in slices.Take(maxSlices))
        {
            series.Add(new PieSeries<double>
            {
                Name = slice.Name,
                Values = [slice.AmountMinor / 100.0],
                Fill = new SolidColorPaint(Palette[sliceIndex % Palette.Length]),
                InnerRadius = 45,
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
                Fill = new SolidColorPaint(Palette[^1]),
                InnerRadius = 45,
            });
        }

        PieSeries = series.ToArray();
    }

    private void BuildDayColumns(long from, long to)
    {
        var points = _reports.GetDynamics(from, to);
        DailyRows.Clear();

        if (points.Count == 0)
        {
            DaySeries = [];
            return;
        }

        // Одна серия серых столбиков; последний день периода — акцентный
        // (вторая серия со значениями только в последней точке).
        var grayValues = new double?[points.Count];
        var accentValues = new double?[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            var value = (IsExpenses ? points[i].ExpenseMinor : points[i].IncomeMinor) / 100.0;
            var amountMinor = IsExpenses ? points[i].ExpenseMinor : points[i].IncomeMinor;
            DailyRows.Add(new DailyReportRow(points[i].Label, MoneyFormat.Rubles(amountMinor)));
            var isLast = i == points.Count - 1;
            grayValues[i] = isLast ? null : value;
            accentValues[i] = isLast ? value : null;
        }

        DaySeries =
        [
            new ColumnSeries<double?>
            {
                Values = grayValues,
                Fill = new SolidColorPaint(BarsGray),
                MaxBarWidth = 12,
            },
            new ColumnSeries<double?>
            {
                Values = accentValues,
                Fill = new SolidColorPaint(BarsAccent),
                MaxBarWidth = 12,
            },
        ];

        // Мини-диаграмма без осей (как в макете): линия-основа задаётся фоном графика.
        DayXAxes = [new Axis { IsVisible = false, Labels = points.Select(p => p.Label).ToArray() }];
        DayYAxes = [new Axis { IsVisible = false, MinLimit = 0 }];
    }

    private void BuildTop(long from, long to, string kind)
    {
        var slices = _reports.GetByCategory(from, to, kind)
            .Where(s => s.AmountMinor > 0);

        TopRows.Clear();
        var index = 0;
        foreach (var slice in slices.Take(5))
        {
            var paint = Palette[index % Palette.Length];
            TopRows.Add(new TopCategoryRow
            {
                Name = slice.Name,
                AmountText = MoneyFormat.Rubles(slice.AmountMinor),
                PercentText = $"{slice.SharePercent:F0}%",
                Progress = Math.Min(1.0, slice.SharePercent / 100.0),
                BarColor = Color.FromRgb(paint.Red, paint.Green, paint.Blue),
            });
            index++;
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
