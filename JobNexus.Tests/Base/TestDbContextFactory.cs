using JobNexus.Data;
using Microsoft.EntityFrameworkCore;

namespace JobNexus.Tests.Base;

public class TestDbContextFactory(DbContextOptions<JobNexusContext> options) : IDbContextFactory<JobNexusContext>
{
    private readonly DbContextOptions<JobNexusContext> _options = options;

    public JobNexusContext CreateDbContext() => new JobNexusContext(_options);

    public Task<JobNexusContext> CreateDbContextAsync() => Task.FromResult(new  JobNexusContext(_options));
}