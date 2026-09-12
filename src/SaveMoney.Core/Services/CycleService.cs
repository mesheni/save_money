using SaveMoney.Core.Database;
using SaveMoney.Core.Models;

namespace SaveMoney.Core.Services;

/// <summary>Текущий период пользовательского цикла (сменный график, полмесяца, месяц).</summary>
public class CycleInfo
{
    public required DateTime Start { get; init; }

    /// <summary>Не включая (начало следующего периода).</summary>
    public required DateTime End { get; init; }

    public required int TotalDays { get; init; }

    public int DaysLeft => Math.Max(0, (End.Date - DateTime.Now.Date).Days);

    public string Display => $"{Start:dd.MM} – {End.AddDays(-1):dd.MM}";
}

/// <summary>
/// Настраиваемый цикл: якорная дата + длина в днях (settings: cycle_anchor_unix, cycle_days).
/// Отдельно — день зарплаты (payday_day): «до зарплаты» считается по календарю, а не по циклу.
/// </summary>
public sealed class CycleService(AppDatabase database)
{
    private readonly AppDatabase _db = database;

    public (DateTime Anchor, int Days) GetSettings()
    {
        var anchorUnix = _db.Settings.GetLong(SettingKeys.CycleAnchorUnix, 0);
        var now = DateTime.Now;
        var anchor = anchorUnix > 0
            ? DateTimeOffset.FromUnixTimeSeconds(anchorUnix).LocalDateTime.Date
            : new DateTime(now.Year, now.Month, 1);
        var days = Math.Max(1, _db.Settings.GetInt(SettingKeys.CycleDays, 30));
        return (anchor, days);
    }

    public void SetSettings(DateTime anchor, int days)
    {
        _db.Settings.SetLong(SettingKeys.CycleAnchorUnix, new DateTimeOffset(anchor.Date, TimeZoneInfo.Local.GetUtcOffset(anchor.Date)).ToUnixTimeSeconds());
        _db.Settings.SetInt(SettingKeys.CycleDays, Math.Max(1, days));
    }

    public CycleInfo GetCurrentCycle()
    {
        var (anchor, days) = GetSettings();
        var today = DateTime.Now.Date;

        var start = anchor;
        while (start > today)
        {
            start = start.AddDays(-days);
        }

        while (start.AddDays(days) <= today)
        {
            start = start.AddDays(days);
        }

        return new CycleInfo { Start = start, End = start.AddDays(days), TotalDays = days };
    }

    public int GetPaydayDay() =>
        Math.Clamp(_db.Settings.GetInt(SettingKeys.PaydayDay, 5), 1, 28);

    public void SetPaydayDay(int day) =>
        _db.Settings.SetInt(SettingKeys.PaydayDay, Math.Clamp(day, 1, 28));

    /// <summary>Ближайшая дата зарплаты после текущего момента.</summary>
    public DateTime GetNextPayday(DateTime now)
    {
        var day = GetPaydayDay();
        var candidate = MakePayday(now.Year, now.Month, day);
        if (candidate <= now)
        {
            var nextMonth = candidate.AddMonths(1);
            candidate = MakePayday(nextMonth.Year, nextMonth.Month, day);
        }

        return candidate;
    }

    public int GetDaysUntilPayday(DateTime now) =>
        (GetNextPayday(now).Date - now.Date).Days;

    private static DateTime MakePayday(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));
}
