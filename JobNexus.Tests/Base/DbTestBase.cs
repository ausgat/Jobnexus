using JobNexus.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace JobNexus.Tests.Base;

public class DbTestBase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<JobNexusContext> _options;
    protected readonly TestDbContextFactory Factory;

    protected DbTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        
        _options = new DbContextOptionsBuilder<JobNexusContext>()
            .UseSqlite(_connection)
            .Options;
        
        using var db = new JobNexusContext(_options);
        db.Database.EnsureCreated();
        
        Factory = new TestDbContextFactory(_options);
    }

    protected JobNexusContext CreateContext() => new(_options);
    
    public void Dispose()
    {
        _connection.Close();
        _connection.Dispose();
    }
}