using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class BudgetEditPage : ContentPage
{
    public BudgetEditPage(BudgetEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        if (BindingContext is BudgetEditViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
