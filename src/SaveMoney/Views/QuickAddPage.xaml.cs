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
        viewModel.AlertAsync = async (title, message) => await SaveMoney.Helpers.MonetDialogs.AlertAsync(title, message, "ОК");
        viewModel.PickAsync = async (title, options) =>
        {
            var picked = await SaveMoney.Helpers.MonetDialogs.ChooseAsync(title, "Отмена", null, options);
            return picked == "Отмена" ? null : picked;
        };
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

        // Навбар скрыт на вкладке ввода (режим виден в сегменте, место дорого),
        // но при правке транзакции нужен back — включаем только там.
        Shell.SetNavBarIsVisible(this, _viewModel.IsEditing || _viewModel.IsStandaloneRepayment);

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
        if (!Motion.AnimationsEnabled)
        {
            SavedToast.Opacity = 1;
            SavedToast.TranslationY = 0;
            await Task.Delay(1400);
            SavedToast.IsVisible = false;
            _toastVisible = false;
            return;
        }
        SavedToast.Opacity = 0;
        SavedToast.TranslationY = 10;

        await Task.WhenAll(
            SavedToast.FadeToAsync(1, Motion.Micro, Motion.Vibrant),
            SavedToast.TranslateToAsync(0, 0, Motion.Normal, Motion.Vibrant));
        await Task.Delay(1400);
        await SavedToast.FadeToAsync(0, Motion.Normal, Motion.Vibrant);
        SavedToast.IsVisible = false;
        _toastVisible = false;
    }
}
