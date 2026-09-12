using SaveMoney.Core.Database;

namespace SaveMoney.Tests;

public sealed class TestDatabase : IDisposable
{
    public AppDatabase Db { get; }

    public TestDatabase()
    {
        var path = Path.Combine(Path.GetTempPath(), $"savemoney-test-{Guid.NewGuid():N}.db");
        Db = new AppDatabase(path);
    }

    public void Dispose()
    {
        var path = Db.Connection.DatabasePath;
        Db.Dispose();
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
