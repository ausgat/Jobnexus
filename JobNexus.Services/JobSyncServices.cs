using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JobNexus.Data;
using JobNexus.Core.Models;
using Microsoft.EntityFrameworkCore;
 
namespace JobNexus.Services;
 
/// <summary>
/// Background service that runs on a schedule to fetch job data from
/// both the Adzuna API and JSearch API, normalize it, and save it to
/// the JobNexus database.
///
/// Data flow per sync cycle:
///   1. Fetch from Adzuna (Nick's code) → AdzunaNormalizer → DB
///   2. Fetch from JSearch (Nick's code) → JSearchNormalizer → DB
///      JSearch is skipped automatically if the 200 request/month cap is hit.
/// </summary>
public class JobSyncService : BackgroundService
{
    private readonly ILogger<JobSyncService> _logger;
    private readonly IServiceProvider _services;
 
    // Switch to TimeSpan.FromSeconds(30) for testing, then back to hours before committing
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    // private readonly TimeSpan _interval = TimeSpan.FromSeconds(30); // for testing
 
    public JobSyncService(ILogger<JobSyncService> logger, IServiceProvider services)
    {
        _logger = logger;
        _services = services;
    }
 
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobSyncService starting.");
 
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
 
        using var scope = _services.CreateScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<JobNexusContext>>();
 
        await RunAdzunaSync(dbFactory, stoppingToken);
        await RunJSearchSync(dbFactory, stoppingToken);
 
        _logger.LogInformation("Job sync cycle complete.");
    }
 
    private async Task RunAdzunaSync(
        IDbContextFactory<JobNexusContext> dbFactory,
        CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Adzuna sync starting.");
 
            // TODO (Nick): Replace with your Adzuna fetch call.
            // Expected signature: Task<AdzunaResponse> FetchAdzunaJobsAsync(CancellationToken)
            AdzunaResponse adzunaResponse = new(); // PLACEHOLDER
 
            var normalizer = new AdzunaNormalizer(dbFactory);
            int inserted = await normalizer.NormalizeAndSaveAsync(
                adzunaResponse.Results, stoppingToken);
 
            _logger.LogInformation("Adzuna sync complete. {Count} new jobs inserted.", inserted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Adzuna sync failed.");
        }
    }
 
    private async Task RunJSearchSync(
        IDbContextFactory<JobNexusContext> dbFactory,
        CancellationToken stoppingToken)
    {
        try
        {
            if (!JSearchNormalizer.CanMakeRequest())
            {
                _logger.LogWarning(
                    "JSearch sync skipped — monthly cap of 200 reached. Resets next month.");
                return;
            }
 
            _logger.LogInformation(
                "JSearch sync starting. {Remaining} requests remaining this month.",
                JSearchNormalizer.RequestsRemainingThisMonth);
 
            // TODO (Nick): Replace with your JSearch fetch call.
            // IMPORTANT: Call JSearchNormalizer.RecordRequest() right after a successful fetch.
            // Expected signature: Task<JSearchResponse> FetchJSearchJobsAsync(CancellationToken)
            JSearchResponse jSearchResponse = new(); // PLACEHOLDER
 
            // TODO (Nick): Uncomment once your fetch is wired in:
            // JSearchNormalizer.RecordRequest();
 
            var normalizer = new JSearchNormalizer(dbFactory);
            int inserted = await normalizer.NormalizeAndSaveAsync(
                jSearchResponse.Data, stoppingToken);
 
            if (inserted == -1)
            {
                _logger.LogWarning("JSearch sync aborted — rate limit hit inside normalizer.");
                return;
            }
 
            _logger.LogInformation(
                "JSearch sync complete. {Count} new jobs inserted. {Remaining} requests remaining this month.",
                inserted,
                JSearchNormalizer.RequestsRemainingThisMonth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSearch sync failed.");
        }
    }
}
 