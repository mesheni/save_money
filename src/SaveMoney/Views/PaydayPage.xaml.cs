using Microsoft.Maui.Controls;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class PaydayPage : ContentPage
{
    private bool _entranceAnimated;

    public PaydayPage(PaydayViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (BindingContext is PaydayViewModel vm)
        {
            await vm.InitializeAsync();
            if (!_entranceAnimated)
            {
                _entranceAnimated = true;
                await RootStack.Children.OfType<View>().FadeInUpStaggeredAsync();
            }
        }
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}
