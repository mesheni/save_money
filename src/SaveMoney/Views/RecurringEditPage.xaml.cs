using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class RecurringEditPage : ContentPage
{
    public RecurringEditPage(RecurringEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        if (BindingContext is RecurringEditViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }
}
