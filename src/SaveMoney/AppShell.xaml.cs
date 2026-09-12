using Microsoft.Maui.Controls;
using SaveMoney.Views;

namespace SaveMoney;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("transaction", typeof(QuickAddPage));
		Routing.RegisterRoute("accounts", typeof(AccountsPage));
		Routing.RegisterRoute("account", typeof(AccountEditPage));
		Routing.RegisterRoute("categories", typeof(CategoriesPage));
		Routing.RegisterRoute("category", typeof(CategoryEditPage));
	}
}
