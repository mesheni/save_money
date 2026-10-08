using Microsoft.Maui.Controls;
using SaveMoney.Core.Services;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class QuickAddPage : ContentPage
{
    private readonly QuickAddViewModel _viewModel;
    private bool _balanceAnimated;
    private bool _toastVisible;

    public QuickAddPage(QuickAddViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
        viewModel.AlertAsync = async (title, message) => await DisplayAlertAsync(title, message, "ОК");
        viewModel.CloseAsync = async () => await Navigation.PopAsync();
        viewModel.SavedAsync = ShowSavedToastAsync;

        // Отклик на нажатие клавиш и сегментов — пульс по моушн-токенам Vibrant.
        // SaveButton лежит внутри грида Keypad и подписан ниже: двойная подписка
        // давала бы два конкурирующих ScaleTo на каждое сохранение.
        foreach (var key in Keypad.Children.OfType<Button>().Where(b => !ReferenceEquals(b, SaveButton)))
        {
            key.Clicked += async (sender, _) => await PulseAsync(sender);
        }

        foreach (var button in SegGrid.Children.OfType<Button>())
        {
            button.Clicked += async (sender, _) => await PulseAsync(sender);
        }

        SaveButton.Clicked += async (sender, _) => await PulseAsync(sender);
    }

    private static async Task PulseAsync(object? sender)
    {
        if (sender is View view)
        {
            await view.PulseAsync();
        }
    }

    private async void OnAppearing(object? sender, EventArgs e)
    {
        await _viewModel.InitializeAsync();

        // Count-up баланса только при первом показе экрана за сессию страницы.
        // Кадры пишем в свойство VM: прямой set Label.Text стирает one-way binding,
        // и баланс потом «замерзает» до перезапуска приложения.
        if (!_balanceAnimated && Math.Abs(_viewModel.BalanceValue) > 0.001)
        {
            _balanceAnimated = true;
            await BalanceLabel.CountUpAsync(
                0,
                _viewModel.BalanceValue,
                value => _viewModel.BalanceText = MoneyFormat.Rubles((long)Math.Round(value * 100)));

            // Пока шла анимация, баланс мог обновиться (мессенджер/MaterializeDue):
            // финальный кадр — всегда актуальное значение из VM.
            _viewModel.BalanceText = MoneyFormat.Rubles((long)Math.Round(_viewModel.BalanceValue * 100));
        }
    }

    private async Task ShowSavedToastAsync()
    {
        if (_toastVisible)
        {
            return;
        }

        _toastVisible = true;
        SavedToast.IsVisible = true;
        SavedToast.Opacity = 0;
        SavedToast.TranslationY = 10;

        await Task.WhenAll(
            SavedToast.FadeTo(1, Motion.Micro, Motion.Vibrant),
            SavedToast.TranslateTo(0, 0, Motion.Normal, Motion.Vibrant));
        await Task.Delay(1400);
        await SavedToast.FadeTo(0, Motion.Normal, Motion.Vibrant);
        SavedToast.IsVisible = false;
        _toastVisible = false;
    }
}
