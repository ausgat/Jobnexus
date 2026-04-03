using Microsoft.Extensions.DependencyInjection;  
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JobNexus.Data;
using Microsoft.EntityFrameworkCore;

namespace JobNexus.Services;

public class JobSyncService : BackgroundService
{
    private readonly ILogger<JobSyncService> _logger;
    private readonly IServiceProvider _services;

    // private readonly TimeSpan _interval = TimeSpan.FromSeconds(30); //for testing
     private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    // Constructor only assigns fields — nothing else
    public JobSyncService(ILogger<JobSyncService> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }

    // This is the method BackgroundService calls when the app starts
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobSyncService starting.");

        // Run once immediately on startup, then on interval
        await RunSync(stoppingToken);

        using PeriodicTimer timer = new(_interval);  

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunSync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("JobSyncService stopping.");
        }
    }

    private async Task RunSync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job sync cycle starting at {Time}", DateTimeOffset.Now);

        try
        {
            // Create a DI scope to access DbContextFactory (scoped service)
            using var scope = _services.CreateScope();
            var dbFactory = scope.ServiceProvider
                .GetRequiredService<IDbContextFactory<JobNexusContext>>();

            // TODO: Replace with Nick's API client once implemented
            // var jobs = await NickApiClient.FetchJobAsync(stoppingToken);

            // TODO: Replace with Cody's uploader once implemented
            // await using var db = await dbFactory.CreateDbContextAsync(stoppingToken);
            // await CodyDbUploader.UploadJobAsync(db, jobs, stoppingToken);

            _logger.LogInformation("Job sync cycle complete.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job sync cycle failed.");  
        }
    }
}