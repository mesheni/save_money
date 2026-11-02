using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class CategoryPickerPage : ContentPage
{
    private readonly CategoryPickerViewModel _viewModel;
    private TaskCompletionSource<string?>? _result;
    private INavigation? _owner;
    private bool _choosing, _closing;

    public CategoryPickerPage(CategoryPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        viewModel.CompleteAsync = CompleteAsync;
    }

    public async Task<string?> ShowAsync(INavigation owner, string kind, string? selected)
    {
        _owner = owner;
        _viewModel.Kind = kind;
        _viewModel.SelectedId = selected;
        _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var window = new NavigationPage(this);
        window.SetDynamicResource(NavigationPage.BarBackgroundColorProperty, "Surface");
        window.SetDynamicResource(NavigationPage.BarTextColorProperty, "OnSurface");
        var host = Shell.Current.Window;
        void Popped(object? sender, ModalPoppedEventArgs args)
        {
            if (ReferenceEquals(args.Modal, window) && !_choosing) _result.TrySetResult(null);
        }
        host.ModalPopped += Popped;
        try
        {
            await owner.PushModalAsync(window, Motion.AnimationsEnabled);
            return await _result.Task;
        }
        finally { host.ModalPopped -= Popped; }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Initialize();
    }

    private async Task CompleteAsync(string id)
    {
        if (_closing) return;
        _closing = _choosing = true;
        if (_owner is not null) await _owner.PopModalAsync(Motion.AnimationsEnabled);
        else await Navigation.PopAsync(false);
        _result?.TrySetResult(id); // Deliver after the parent has returned, not while the modal is closing.
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        if (_closing) return;
        _closing = true;
        if (_owner is not null) await _owner.PopModalAsync(Motion.AnimationsEnabled);
        else await Navigation.PopAsync(false);
        _result?.TrySetResult(null);
    }
}
