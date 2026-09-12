using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class CategoriesPage : ContentPage
{
    private readonly CategoriesViewModel _viewModel;

    public CategoriesPage(CategoriesViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}
