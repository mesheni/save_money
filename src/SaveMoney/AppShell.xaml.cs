using Microsoft.Maui.Controls;
using SaveMoney.Views;

namespace SaveMoney;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

#if ANDROID
        Loaded += (_, _) => Helpers.ThemePalette.RefreshPlatform();
        HandlerChanged += (_, _) => Dispatcher.Dispatch(Helpers.ThemePalette.RefreshPlatform);
#endif

		Routing.RegisterRoute("transaction", typeof(QuickAddPage));
		Routing.RegisterRoute("categorypicker", typeof(CategoryPickerPage));
		Routing.RegisterRoute("accounts", typeof(AccountsPage));
		Routing.RegisterRoute("account", typeof(AccountEditPage));
		Routing.RegisterRoute("categories", typeof(CategoriesPage));
		Routing.RegisterRoute("category", typeof(CategoryEditPage));
		Routing.RegisterRoute("debts", typeof(DebtsPage));
		Routing.RegisterRoute("debtnew", typeof(DebtEditPage));
		Routing.RegisterRoute("debt", typeof(DebtDetailPage));
		Routing.RegisterRoute("budgets", typeof(BudgetsPage));
		Routing.RegisterRoute("budget", typeof(BudgetEditPage));
		Routing.RegisterRoute("payday", typeof(PaydayPage));
		Routing.RegisterRoute("recurring", typeof(RecurringPage));
		Routing.RegisterRoute("recurringnew", typeof(RecurringEditPage));
		Routing.RegisterRoute("settings", typeof(SettingsPage));
	}

    private DateTime _lastBackOnInput = DateTime.MinValue;

    /// <summary>
    /// «Назад» на корне вкладки: с «Истории»/«Отчётов»/«Ещё» — на «Ввод», а с «Ввода» —
    /// выход только по второму нажатию за 2 секунды. Раньше любое «Назад» на корне
    /// молча закрывало приложение.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (Navigation.ModalStack.Count > 0 || (CurrentItem?.CurrentItem?.Stack.Count ?? 1) > 1)
        {
            return base.OnBackButtonPressed();
        }

        if (CurrentPage is not QuickAddPage)
        {
            _ = GoToAsync("//quickadd");
            return true;
        }

        var now = DateTime.UtcNow;
        if (now - _lastBackOnInput < TimeSpan.FromSeconds(2))
        {
            return base.OnBackButtonPressed();
        }

        _lastBackOnInput = now;
#if ANDROID
        global::Android.Widget.Toast.MakeText(global::Android.App.Application.Context,
            "Нажмите «Назад» ещё раз, чтобы выйти", global::Android.Widget.ToastLength.Short)?.Show();
#endif
        return true;
    }

    protected override void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
#if ANDROID
        if (!args.Cancelled) Platforms.Android.ScreenMotion.Navigating(this, args);
#endif
    }

	// Вкладка при переключении всегда открывается в корне: Shell хранит навстек
	// каждой вкладки, из-за чего «Ещё» показывало последний открытый раздел
	// (Счета и т.п.). Сброс — только при смене вкладки/секции (не при push/pop).
	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);

        Helpers.ThemePalette.RefreshPlatform();
#if ANDROID
        // Shell применяет оформление страницы с навбаром уже после OnNavigated и
        // заливает фон окна своим цветом (синяя полоса под прозрачной статус-строкой
        // Android 15). Перекрашиваем ещё раз, когда Shell закончил.
        Dispatcher.Dispatch(Helpers.ThemePalette.RefreshPlatform);
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), Helpers.ThemePalette.RefreshPlatform);
        Platforms.Android.ScreenMotion.Navigated(this);
#endif

		// Открытие раздела из «Ещё» — это НЕ push-стек, а Shell URI-состояние
		// («//more/accounts»): Shell восстанавливает его при каждом возврате на
		// вкладку, поэтому «Ещё» показывало последний открытый раздел. Лечение:
		// при смене секции на URI вкладки «Ещё» глубже корня — перейти на «//more».
		var isSectionOrItemChange = args.Source is ShellNavigationSource.ShellSectionChanged
			or ShellNavigationSource.ShellItemChanged;

		if (isSectionOrItemChange
			&& args.Current?.Location?.OriginalString?.StartsWith("//more/") == true)
		{
			_ = Shell.Current.GoToAsync("//more");
		}
	}
}
