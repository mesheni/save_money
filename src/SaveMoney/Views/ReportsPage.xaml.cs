using System.ComponentModel;
using LiveChartsCore.SkiaSharpView.Maui;
using LiveChartsCore.SkiaSharpView.Painting;
using Microsoft.Maui.Controls;
using SaveMoney.Core.Services;
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
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        StyleCharts();
    }

    /// <summary>
    /// Тултипы и моушн графиков: фоны/тексты из токенов Vibrant (по текущей теме,
    /// как и краска осей во ViewModel), скорость и кривая — из дизайн-контракта.
    /// </summary>
    private void StyleCharts()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var tooltipBackground = new SolidColorPaint(isDark
            ? new SKColor(0x2C, 0x24, 0x52)
            : new SKColor(0xFF, 0xFF, 0xFF));
        var tooltipText = new SolidColorPaint(isDark
            ? new SKColor(0xF4, 0xF1, 0xE8)
            : new SKColor(0x1D, 0x18, 0x36));

        ApplyChartStyle(DonutChart, tooltipBackground, tooltipText);
        ApplyChartStyle(DynamicsChart, tooltipBackground, tooltipText);
    }

    private static void ApplyChartStyle(PieChart chart, SolidColorPaint background, SolidColorPaint text)
    {
        chart.TooltipBackgroundPaint = background;
        chart.TooltipTextPaint = text;
        chart.TooltipTextSize = 13;
        chart.AnimationsSpeed = TimeSpan.FromMilliseconds(350);
        chart.EasingFunction = t => (float)Motion.VibrantFunc(t);
    }

    private static void ApplyChartStyle(CartesianChart chart, SolidColorPaint background, SolidColorPaint text)
    {
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

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Count-up суммы расхода при каждой пересборке отчёта (смена периода/дат).
        if (e.PropertyName == nameof(ReportsViewModel.PeriodExpenseText)
            && _viewModel.HasData
            && PeriodExpenseLabel is not null)
        {
            await PeriodExpenseLabel.CountUpAsync(
                _viewModel.PeriodExpenseValue,
                value => MoneyFormat.Rubles((long)Math.Round(value * 100)));
        }
    }
}
