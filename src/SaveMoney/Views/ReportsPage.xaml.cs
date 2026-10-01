using System.ComponentModel;
using Microsoft.Maui.Controls;
using SaveMoney.Core.Services;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

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
