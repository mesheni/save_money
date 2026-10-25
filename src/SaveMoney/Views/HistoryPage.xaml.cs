using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _viewModel;

    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.PickAsync = async (title, options) =>
        {
            var picked = await DisplayActionSheetAsync(title, "Отмена", null, options);
            return picked == "Отмена" ? null : picked;
        };
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();
    }

    private async void OnDeleteInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItem { CommandParameter: HistoryItemVM item })
        {
            await _viewModel.DeleteAsync(item);
        }
    }
}
