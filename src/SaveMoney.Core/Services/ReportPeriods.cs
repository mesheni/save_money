namespace SaveMoney.Core.Services;

/// <summary>
/// Скользящие периоды «Неделя / Месяц / 3 месяца», выровненные по суткам:
/// с полуночи первого дня до конца сегодняшнего (конец — не включая).
/// Раньше начало бралось «сейчас минус N» с текущим временем суток, и утренние
/// операции первого дня выпадали из отчёта.
/// </summary>
public static class ReportPeriods
{
    /// <summary>Последние <paramref name="days"/> дней, включая сегодня.</summary>
    public static (DateTime From, DateTime To) LastDays(DateTime now, int days)
    {
        var to = now.Date.AddDays(1);
        return (to.AddDays(-Math.Max(1, days)), to);
    }

    /// <summary>Последние <paramref name="months"/> месяцев, включая сегодня (с «того же числа» +1 день).</summary>
    public static (DateTime From, DateTime To) LastMonths(DateTime now, int months)
    {
        var to = now.Date.AddDays(1);
        return (to.AddMonths(-Math.Max(1, months)), to);
    }
}
