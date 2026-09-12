using SQLite;

namespace SaveMoney.Core.Models;

/// <summary>Ключи настроек приложения (хранятся в settings).</summary>
public static class SettingKeys
{
    public const string DefaultCurrency = "default_currency";
    public const string PaydayDay = "payday_day";
    public const string CycleAnchorUnix = "cycle_anchor_unix";
    public const string CycleDays = "cycle_days";
    public const string Theme = "theme";
    public const string IsPro = "is_pro";
}

/// <summary>Простое key-value хранилище настроек (не синхронизируется).</summary>
[Table("settings")]
public class Setting
{
    [PrimaryKey, Column("key"), MaxLength(100)]
    public string Key { get; set; } = "";

    [Column("value")]
    public string? Value { get; set; }
}
