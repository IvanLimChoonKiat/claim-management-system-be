using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace ClaimsManagement.Tests.Api;

/// <summary>
/// One isolated SQLite file per factory instance (per test class, since xUnit creates a new
/// instance per IClassFixture consumer) - never touches the real claims.db, per
/// docs/TESTING.md "Test isolation". Deleted on dispose.
/// </summary>
public sealed class ClaimsApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"claims-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:ClaimsDatabase", $"Data Source={_dbPath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        // Release pooled connections before deleting the file, otherwise SQLite can still hold a
        // handle open briefly after the host shuts down and File.Delete throws IOException.
        SqliteConnection.ClearAllPools();

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp file; harmless if it lingers until the OS reclaims %TEMP%.
        }
    }
}
