using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;

namespace SaveMoney.ViewModels;

public partial class SettingsViewModel(AppDatabase db, CsvExportService csv) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly CsvExportService _csv = csv;
    private bool _loaded;

    public IReadOnlyList<string> ThemeOptions { get; } = ["Системная", "Светлая", "Тёмная"];

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial string AboutText { get; set; } =
        $"SaveMoney {AppInfo.VersionString}\n\n" +
        "Ввод и данные бесплатны навсегда. Экспорт CSV — всегда бесплатный. " +
        "Данные не блокируются никогда, даже после отписки.";

    public Func<string, string, Task>? AlertAsync { get; set; }

    public Task InitializeAsync()
    {
        if (_loaded)
        {
            return Task.CompletedTask;
        }

        _loaded = true;
        ThemeIndex = _db.Settings.Get(SettingKeys.Theme) switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0,
        };

        return Task.CompletedTask;
    }

    partial void OnThemeIndexChanged(int value)
    {
        if (!_loaded)
        {
            return;
        }

        var themeValue = value switch
        {
            1 => "light",
            2 => "dark",
            _ => "system",
        };

        if (Application.Current is { } app)
        {
            app.UserAppTheme = themeValue switch
            {
                "light" => AppTheme.Light,
                "dark" => AppTheme.Dark,
                _ => AppTheme.Unspecified,
            };
        }

        _db.Settings.Set(SettingKeys.Theme, themeValue);
    }

    [RelayCommand]
    private async Task ExportAllCsvAsync()
    {
        await ExportCsvAsync(0, DateTimeOffset.Now.AddDays(1).ToUnixTimeSeconds());
    }

    [RelayCommand]
    private async Task BackupDatabaseAsync()
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory,
                $"savemoney-backup-{DateTime.Now:yyyyMMdd_HHmm}.db");
            _db.Connection.Execute($"VACUUM INTO '{path.Replace("'", "''")}'");

            await Share.RequestAsync(new ShareFileRequest
            {
                Title = Path.GetFileName(path),
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            await AlertAsync?.Invoke("Ошибка копии", ex.Message)!;
        }
    }

    private async Task ExportCsvAsync(long fromUnix, long toUnix)
    {
        try
        {
            var path = _csv.ExportTransactions(fromUnix, toUnix, Path.Combine(FileSystem.AppDataDirectory, "exports"));
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = Path.GetFileName(path),
                File = new ShareFile(path),
            });
        }
        catch (Exception ex)
        {
            await AlertAsync?.Invoke("Ошибка экспорта", ex.Message)!;
        }
    }
}
