using Microsoft.Maui.Controls;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _viewModel;
    private bool _entranceAnimated;

    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Живые подписи: балансы/долги/лимиты могли измениться с прошлого показа.
        _viewModel.RefreshSubtitles();

        if (!_entranceAnimated)
        {
            _entranceAnimated = true;
            await RootGrid.Children.OfType<View>().FadeInUpStaggeredAsync();
        }
    }
}
