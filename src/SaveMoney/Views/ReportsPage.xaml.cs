using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Maui.Controls;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;
using SkiaSharp;

namespace SaveMoney.Views;

public partial class ReportsPage : ContentPage
{
    private readonly ReportsViewModel _viewModel;

    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await SaveMoney.Helpers.MonetDialogs.AlertAsync(title, message, "ОК");
        viewModel.PickAsync = async (title, options) =>
        {
            var picked = await SaveMoney.Helpers.MonetDialogs.ChooseAsync(title, "Отмена", null, options);
            return picked == "Отмена" ? null : picked;
        };
        StyleCharts();
        Appearing += (_, _) => ThemePalette.Changed += OnPaletteChanged;
        Disappearing += (_, _) => ThemePalette.Changed -= OnPaletteChanged;
    }

    private void OnPaletteChanged(object? sender, EventArgs e)
    {
        StyleCharts();
        _viewModel.RefreshPalette();
    }

    /// <summary>
    /// Тултипы и моушн графиков: фоны/тексты из токенов Clean (по текущей теме,
    /// как и краска палитры во ViewModel), скорость и кривая — из дизайн-контракта.
    /// </summary>
    private void StyleCharts()
    {
        var tooltipBackground = new SolidColorPaint(SKColor.Parse(ThemePalette.Get("SurfaceContainerHigh").ToArgbHex()));
        var tooltipText = new SolidColorPaint(SKColor.Parse(ThemePalette.Get("OnSurface").ToArgbHex()));

        ApplyChartStyle(DonutChart, tooltipBackground, tooltipText);
        ApplyChartStyle(DaysChart, tooltipBackground, tooltipText);
    }

    private static void ApplyChartStyle(PieChart chart, SolidColorPaint background, SolidColorPaint text)
    {
        // Без явного фона поверхность Skia на WinUI рисуется чёрным прямоугольником.
        chart.BackgroundColor = Colors.Transparent;
        chart.TooltipBackgroundPaint = background;
        chart.TooltipTextPaint = text;
        chart.TooltipTextSize = 13;
        chart.AnimationsSpeed = TimeSpan.FromMilliseconds(Motion.AnimationsEnabled ? 240 : 0);
        chart.EasingFunction = t => (float)Motion.VibrantFunc(t);
    }

    private static void ApplyChartStyle(CartesianChart chart, SolidColorPaint background, SolidColorPaint text)
    {
        chart.BackgroundColor = Colors.Transparent;
        chart.TooltipBackgroundPaint = background;
        chart.TooltipTextPaint = text;
        chart.TooltipTextSize = 13;
        chart.AnimationsSpeed = TimeSpan.FromMilliseconds(Motion.AnimationsEnabled ? 240 : 0);
        chart.EasingFunction = t => (float)Motion.VibrantFunc(t);
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        StyleCharts();
        await _viewModel.InitializeAsync();
    }

    private async void OnDaysChartSizeChanged(object? sender, EventArgs e)
    {
        // При открытии показываем последние даты, а не пустое начало периода.
        if (DaysChart.Width > 0)
            await DayChartScroll.ScrollToAsync(DaysChart.Width, 0, false);
    }
}
