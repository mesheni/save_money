using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

/// <summary>Заглушка экрана быстрого ввода (Фаза 1). Сейчас показывает общий баланс — вертикальный срез БД.</summary>
public partial class QuickAddViewModel(BalanceService balanceService) : ObservableObject
{
    [ObservableProperty]
    public partial string BalanceText { get; private set; } = "…";

    [RelayCommand]
    private void LoadBalance()
    {
        BalanceText = MoneyFormat.Rubles(balanceService.GetTotalBalance());
    }
}
