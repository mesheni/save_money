using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SaveMoney.Core.Database;
using SaveMoney.Core.Models;
using SaveMoney.Core.Services;
using SaveMoney.Helpers;

namespace SaveMoney.ViewModels;

public partial class SettingsViewModel(AppDatabase db, CsvExportService csv) : ObservableObject
{
    private readonly AppDatabase _db = db;
    private readonly CsvExportService _csv = csv;
    private bool _loaded;

    public IReadOnlyList<string> ThemeOptions { get; } = ["Системная", "Светлая", "Тёмная"];
    public IReadOnlyList<string> AccentOptions { get; } = ThemePalette.Options.Select(option => option.Name).ToArray();
    public Color SelectedAccentPreview => ThemePalette.Get("PrimaryContainer");
    public IReadOnlyList<string> ColorSourceOptions { get; } = ThemePalette.SupportsDeviceColors
        ? ["Цвета устройства", "Своя палитра"] : ["Своя палитра"];
    public bool IsManualPalette => !ThemePalette.SupportsDeviceColors || ColorSourceIndex == 1;
    public string PaletteHint => ThemePalette.SupportsDeviceColors
        ? "Цвета устройства следуют системной палитре Android. Своя палитра сохраняет выбранный оттенок."
        : "На этом устройстве используется встроенная палитра Material You.";

    [ObservableProperty]
    public partial int ColorSourceIndex { get; set; }

    partial void OnColorSourceIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsManualPalette));
        if (!_loaded) return;
        var source = !IsManualPalette ? MonetScheme.Device : MonetScheme.Manual;
        _db.Settings.Set(SettingKeys.ColorSource, source);
        ThemePalette.SetSource(source);
        OnPropertyChanged(nameof(SelectedAccentPreview));
    }

    [ObservableProperty]
    public partial int ThemeIndex { get; set; }

    [ObservableProperty]
    public partial int AccentIndex { get; set; }

    /// <summary>Ввод за 2 касания: тап по категории с введённой суммой сразу сохраняет операцию.</summary>
    [ObservableProperty]
    public partial bool QuickSaveOnCategory { get; set; }

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

        ThemeIndex = _db.Settings.Get(SettingKeys.Theme) switch
        {
            "light" => 1,
            "dark" => 2,
            _ => 0,
        };
        var savedAccent = _db.Settings.Get(SettingKeys.Accent);
        AccentIndex = Math.Max(0, ThemePalette.Options.ToList().FindIndex(option => option.Key == savedAccent));
        ColorSourceIndex = ThemePalette.SupportsDeviceColors && ThemePalette.Source == MonetScheme.Manual ? 1 : 0;
        QuickSaveOnCategory = _db.Settings.GetBool(SettingKeys.QuickSaveOnCategory, false);
        _loaded = true;

        return Task.CompletedTask;
    }

    partial void OnQuickSaveOnCategoryChanged(bool value)
    {
        if (!_loaded)
        {
            return;
        }

        _db.Settings.SetBool(SettingKeys.QuickSaveOnCategory, value);
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
        ThemePalette.Apply(ThemePalette.SelectedKey, Application.Current?.RequestedTheme ?? AppTheme.Light);
        OnPropertyChanged(nameof(SelectedAccentPreview));
    }

    partial void OnAccentIndexChanged(int value)
    {
        OnPropertyChanged(nameof(SelectedAccentPreview));
        if (!_loaded || value < 0 || value >= ThemePalette.Options.Count)
            return;

        var key = ThemePalette.Options[value].Key;
        _db.Settings.Set(SettingKeys.Accent, key);
        ThemePalette.Apply(key, Application.Current?.RequestedTheme ?? AppTheme.Light);
        OnPropertyChanged(nameof(SelectedAccentPreview));
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
            // Секунды в имени: VACUUM INTO падает, если файл уже существует,
            // а две копии в одну минуту — обычный случай.
            var backupDirectory = Path.Combine(FileSystem.AppDataDirectory, "backups");
            Directory.CreateDirectory(backupDirectory);
            var path = Path.Combine(backupDirectory,
                $"savemoney-backup-{DateTime.Now:yyyyMMdd_HHmmss}.db");
            // VACUUM большой базы занимает секунды — не замораживаем UI-поток.
            await Task.Run(() => _db.Connection.Execute($"VACUUM INTO '{path.Replace("'", "''")}'"));

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
            var path = await Task.Run(() =>
                _csv.ExportTransactions(fromUnix, toUnix, Path.Combine(FileSystem.AppDataDirectory, "exports")));
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
