using SaveMoney.Core.Models;
using SQLite;

namespace SaveMoney.Core.Repositories;

public class SettingRepository(SQLiteConnection db)
{
    private readonly SQLiteConnection _db = db;

    public string? Get(string key) => _db.Find<Setting>(key)?.Value;

    public void Set(string key, string? value)
    {
        var setting = _db.Find<Setting>(key);
        if (setting is null)
        {
            _db.Insert(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
            _db.Update(setting);
        }
    }

    public int GetInt(string key, int fallback)
    {
        var raw = Get(key);
        return int.TryParse(raw, out var value) ? value : fallback;
    }

    public void SetInt(string key, int value) => Set(key, value.ToString());

    public long GetLong(string key, long fallback)
    {
        var raw = Get(key);
        return long.TryParse(raw, out var value) ? value : fallback;
    }

    public void SetLong(string key, long value) => Set(key, value.ToString());

    public bool GetBool(string key, bool fallback)
    {
        var raw = Get(key);
        return bool.TryParse(raw, out var value) ? value : fallback;
    }

    public void SetBool(string key, bool value) => Set(key, value ? "true" : "false");
}
