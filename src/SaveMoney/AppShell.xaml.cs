using Microsoft.Maui.Controls;
using SaveMoney.Views;

namespace SaveMoney;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

#if ANDROID
		// Полоска активной вкладки (макет). Без механизма Effects: EffectsFactory
		// в DI MAUI 10 не зарегистрирован и валил приложение при attach Shell.
		Platforms.Android.TabBarIndicator.Attach(this);
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
}
