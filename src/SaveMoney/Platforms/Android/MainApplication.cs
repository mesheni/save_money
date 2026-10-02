using Android.App;
using Android.Runtime;

namespace SaveMoney;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
		// Android-аналог WinUI-логгера (Platforms/Windows/App.xaml.cs): необработанные
		// исключения .NET пишем в файл, чтобы краш с устройства можно было диагностировать
		// без подключённого logcat — путь попадает в лог и в AppDataDirectory.
		AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
		{
			try
			{
				var path = Path.Combine(FileSystem.AppDataDirectory, "savemoney-crash.log");
				File.AppendAllText(path,
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {e.Exception}\n{e.Exception.StackTrace}\n\n");
			}
			catch
			{
				// логирование не должно ронять приложение повторно
			}

			e.Handled = false;
		};

		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			try
			{
				var path = Path.Combine(FileSystem.AppDataDirectory, "savemoney-crash.log");
				File.AppendAllText(path,
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {e.ExceptionObject}\n\n");
			}
			catch
			{
			}
		};

		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			try
			{
				var path = Path.Combine(FileSystem.AppDataDirectory, "savemoney-crash.log");
				File.AppendAllText(path,
					$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] UNOBSERVED {e.Exception}\n\n");
			}
			catch
			{
			}
		};
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
