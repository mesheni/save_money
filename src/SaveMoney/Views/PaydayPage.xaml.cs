using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class PaydayPage : ContentPage
{
    public PaydayPage(PaydayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        if (BindingContext is PaydayViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
