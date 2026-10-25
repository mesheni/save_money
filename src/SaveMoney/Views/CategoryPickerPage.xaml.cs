using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class CategoryPickerPage : ContentPage
{
    public CategoryPickerPage(CategoryPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        viewModel.CloseAsync = async () => await Navigation.PopAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ((CategoryPickerViewModel)BindingContext).Initialize();
    }
}
