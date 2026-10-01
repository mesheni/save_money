using Microsoft.Maui.Controls;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class MorePage : ContentPage
{
    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RootGrid.Children.OfType<View>().FadeInUpStaggeredAsync();
    }
}
