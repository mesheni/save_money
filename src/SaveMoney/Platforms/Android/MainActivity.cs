using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

namespace SaveMoney;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
	/// <summary>Ключ extra с суммой, переданной из виджета быстрого ввода.</summary>
	public const string ExtraQuickAmount = "quick_amount";

	protected override void OnCreate(Bundle? savedInstanceState)
	{
		base.OnCreate(savedInstanceState);
		HandleWidgetIntent(Intent);
	}

	protected override void OnNewIntent(Intent? intent)
	{
		base.OnNewIntent(intent);
		HandleWidgetIntent(intent);
	}

	/// <summary>
	/// Тап по кнопке виджета открывает экран ввода с суммой (пустая сумма — кнопка «✎»,
	/// просто открывает ввод). При холодном старте Shell ещё не готов — ждём его прогрева.
	/// </summary>
	private void HandleWidgetIntent(Intent? intent)
	{
		// extra есть только у интентов виджета; пустая строка означает «без предзаполненной суммы»
		var amount = intent?.GetStringExtra(ExtraQuickAmount);
		if (amount is null)
		{
			return;
		}

		intent?.RemoveExtra(ExtraQuickAmount);

		_ = MainThread.InvokeOnMainThreadAsync(async () =>
		{
			// Фиксированная задержка ненадёжна: на медленном холодном старте 600 мс не хватало
			// и тап молча терялся. Ждём появления Shell, но не дольше ~5 секунд.
			for (var attempt = 0; attempt < 100 && Shell.Current is null; attempt++)
			{
				await Task.Delay(50);
			}

			if (Shell.Current is null)
			{
				return;
			}

			var route = string.IsNullOrEmpty(amount)
				? "transaction?source=widget"
				: $"transaction?amount={Uri.EscapeDataString(amount)}&source=widget";
			await Shell.Current.GoToAsync(route);
		});
	}
}
