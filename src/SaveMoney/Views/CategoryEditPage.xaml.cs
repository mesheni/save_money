using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class CategoryEditPage : ContentPage
{
    private readonly CategoryEditViewModel _viewModel;

    public CategoryEditPage(CategoryEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}
