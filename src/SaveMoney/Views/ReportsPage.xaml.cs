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
    private bool _entranceAnimated;

    public ReportsPage(ReportsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
        viewModel.PickAsync = async (title, options) =>
        {
            var picked = await DisplayActionSheetAsync(title, "Отмена", null, options);
            return picked == "Отмена" ? null : picked;
        };
        StyleCharts();
    }

    /// <summary>
    /// Тултипы и моушн графиков: фоны/тексты из токенов Clean (по текущей теме,
    /// как и краска палитры во ViewModel), скорость и кривая — из дизайн-контракта.
    /// </summary>
    private void StyleCharts()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var tooltipBackground = new SolidColorPaint(isDark
            ? new SKColor(0x1A, 0x1A, 0x1F)
            : new SKColor(0xFF, 0xFF, 0xFF));
        var tooltipText = new SolidColorPaint(isDark
            ? new SKColor(0xF2, 0xF2, 0xF4)
            : new SKColor(0x17, 0x17, 0x1C));

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
        chart.AnimationsSpeed = TimeSpan.FromMilliseconds(350);
        chart.EasingFunction = t => (float)Motion.VibrantFunc(t);
    }

    private static void ApplyChartStyle(CartesianChart chart, SolidColorPaint background, SolidColorPaint text)
    {
        chart.BackgroundColor = Colors.Transparent;
        chart.TooltipBackgroundPaint = background;
        chart.TooltipTextPaint = text;
        chart.TooltipTextSize = 13;
        chart.AnimationsSpeed = TimeSpan.FromMilliseconds(350);
        chart.EasingFunction = t => (float)Motion.VibrantFunc(t);
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();

        if (!_entranceAnimated)
        {
            _entranceAnimated = true;
            await RootStack.Children.OfType<View>().FadeInUpStaggeredAsync();
        }
    }
}
