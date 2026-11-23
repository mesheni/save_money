using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using SaveMoney.Core.Services;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class QuickAddPage : ContentPage
{
    private readonly QuickAddViewModel _viewModel;
    private bool _toastVisible;

    public QuickAddPage(QuickAddViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await SaveMoney.Helpers.MonetDialogs.AlertAsync(title, message, "ОК");
        viewModel.PickAsync = async (title, options) =>
        {
            var picked = await SaveMoney.Helpers.MonetDialogs.ChooseAsync(title, "Отмена", null, options);
            return picked == "Отмена" ? null : picked;
        };
        viewModel.CloseAsync = async () => await Navigation.PopAsync(false);
        viewModel.SavedAsync = ShowSavedToastAsync;
        viewModel.InvalidAmountAsync = ShowInvalidAmountAsync;

        viewModel.PickCategoryAsync = async (kind, selected) =>
        {
            var picker = MauiProgram.Services.GetRequiredService<CategoryPickerPage>();
            return await picker.ShowAsync(Navigation, kind, selected);
        };
    }

    private bool? _landscape;

    private void OnLayoutSizeChanged(object? sender, EventArgs e)
    {
        if (InputLayout is null || KeypadPanel is null || InputScroll is null || SavedToast is null) return;
#if ANDROID
        var largeText = global::Android.App.Application.Context.Resources?.Configuration?.FontScale > 1.4f;
#else
        var largeText = false;
#endif
        if (KeypadDateLabel is not null) KeypadDateLabel.IsVisible = InputLayout.Width >= 340 && !largeText;
        var landscape = InputLayout.Width >= 600 && InputLayout.Width > InputLayout.Height;
        if (_landscape == landscape) return;
        _landscape = landscape;
        InputLayout.RowDefinitions.Clear();
        InputLayout.ColumnDefinitions.Clear();
        InputLayout.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        InputLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        if (landscape)
            InputLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        else
            InputLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(KeypadPanel, landscape ? 0 : 1);
        Grid.SetColumn(KeypadPanel, landscape ? 1 : 0);
        Grid.SetRowSpan(SavedToast, landscape ? 1 : 2);
        Grid.SetColumnSpan(SavedToast, landscape ? 2 : 1);
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();

        // Навбар скрыт на вкладке ввода (режим виден в сегменте, место дорого),
        // но при правке транзакции нужен back — включаем только там.
        Shell.SetNavBarIsVisible(this, _viewModel.IsEditing || _viewModel.IsStandaloneRepayment);

    }

    private async void OnPickDateClicked(object? sender, EventArgs e)
    {
#if ANDROID
        if (await DateDialog.PickAsync(_viewModel.Date) is { } selected)
            _viewModel.Date = selected;
#else
        DatePickerControl.Focus();
#endif
    }

    private async Task ShowSavedToastAsync()
    {
        if (_toastVisible)
        {
            return;
        }

        _toastVisible = true;
        SavedToast.IsVisible = true;
        SavedCheck.Scale = Motion.AnimationsEnabled ? 0 : 1;
        _ = BalanceLabel.PulseAsync(1.08);
        await SavedToast.PopInAsync(rise: 24, fromScale: 0.86);
        await SavedCheck.ScaleToAsync(1, 260, Easing.SpringOut);
        await Task.Delay(1100);
        await SavedToast.PopOutAsync();
        SavedToast.IsVisible = false;
        SavedToast.TranslationY = 0;
        _toastVisible = false;
    }

    /// <summary>Пустая сумма: покачивание и тактильный отклик вместо модального окна.</summary>
    private async Task ShowInvalidAmountAsync()
    {
        const string message = "Введите сумму больше нуля";
        SemanticScreenReader.Default.Announce(message);
#if ANDROID
        // Без разрешения VIBRATE: системный отклик представления.
        (AmountLabel.Handler?.PlatformView as Android.Views.View)?.PerformHapticFeedback(
            OperatingSystem.IsAndroidVersionAtLeast(30) ? Android.Views.FeedbackConstants.Reject : Android.Views.FeedbackConstants.LongPress);
#endif

        if (!Motion.AnimationsEnabled)
        {
            await MonetDialogs.AlertAsync("Ошибка", message, "ОК");
            return;
        }

        await AmountLabel.ShakeAsync(12);
    }
}
