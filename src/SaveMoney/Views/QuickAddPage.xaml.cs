using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class QuickAddPage : ContentPage
{
    private readonly QuickAddViewModel _viewModel;

    public QuickAddPage(QuickAddViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    private void OnAppearing(object? sender, EventArgs e) => _viewModel.LoadBalanceCommand.Execute(null);
}
