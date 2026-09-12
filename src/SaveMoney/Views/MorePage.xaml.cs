using Microsoft.Maui.Controls;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class MorePage : ContentPage
{
    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
