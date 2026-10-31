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

	// Вкладка при переключении всегда открывается в корне: Shell хранит навстек
	// каждой вкладки, из-за чего «Ещё» показывало последний открытый раздел
	// (Счета и т.п.). Сброс — только при смене вкладки/секции (не при push/pop).
	protected override void OnNavigated(ShellNavigatedEventArgs args)
	{
		base.OnNavigated(args);

		Helpers.ThemePalette.RefreshPlatform();

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
