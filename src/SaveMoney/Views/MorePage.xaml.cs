using Microsoft.Maui.Controls;
using SaveMoney.Helpers;
using SaveMoney.ViewModels;

namespace SaveMoney.Views;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _viewModel;

    public MorePage(MoreViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Повторное переключение на вкладку «Ещё» показывало последний открытый
        // раздел (Shell хранит навстек вкладки): сбрасываем его до корня. Возврат
        // с push-страницы сюда уже приходит с пустым стеком — не трогаем.
        // Поп сразу из Appearing Shell игнорирует посреди переключения — даём
        // переходу доиграть и проверяем ещё раз.
        if (Navigation.NavigationStack.Count > 1)
        {
            await Task.Delay(150);
            if (Navigation.NavigationStack.Count > 1)
            {
                await Navigation.PopToRootAsync(animated: false);
            }
        }

        // Живые подписи: балансы/долги/лимиты могли измениться с прошлого показа.
        _viewModel.RefreshSubtitles();
    }
}
