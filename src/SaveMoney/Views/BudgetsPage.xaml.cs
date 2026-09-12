using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class BudgetsPage : ContentPage
{
    private readonly BudgetsViewModel _viewModel;

    public BudgetsPage(BudgetsViewModel viewModel)
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
        if (sender is SwipeItem { CommandParameter: BudgetRowVM row })
        {
            await _viewModel.DeleteAsync(row);
        }
    }
}
