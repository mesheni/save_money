using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class AccountsPage : ContentPage
{
    private readonly AccountsViewModel _viewModel;

    public AccountsPage(AccountsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OnDeleteInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { CommandParameter: AccountRow row })
        {
            await _viewModel.DeleteAsync(row);
        }
    }
}
