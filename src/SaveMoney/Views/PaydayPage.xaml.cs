using Microsoft.Maui.Controls;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class PaydayPage : ContentPage
{

    public PaydayPage(PaydayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await SaveMoney.Helpers.MonetDialogs.AlertAsync(title, message, "ОК");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is PaydayViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
