using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class RecurringPage : ContentPage
{
    private readonly RecurringViewModel _viewModel;

    public RecurringPage(RecurringViewModel viewModel)
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
        if (sender is SwipeItem { CommandParameter: RecurringRowVM row })
        {
            await _viewModel.DeleteAsync(row);
        }
    }
}
