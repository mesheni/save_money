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
	/// Тап по кнопке виджета открывает экран ввода с суммой. При холодном старте Shell
	/// ещё не готов — навигация откладывается до прогрева интерфейса.
	/// </summary>
	private void HandleWidgetIntent(Intent? intent)
	{
		var amount = intent?.GetStringExtra(ExtraQuickAmount);
		if (string.IsNullOrEmpty(amount))
		{
			return;
		}

		intent?.RemoveExtra(ExtraQuickAmount);

		_ = MainThread.InvokeOnMainThreadAsync(async () =>
		{
			await Task.Delay(600);
			if (Shell.Current is not null)
			{
				await Shell.Current.GoToAsync($"transaction?amount={Uri.EscapeDataString(amount)}");
			}
		});
	}
}
