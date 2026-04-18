using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using JobNexus.Data;
using JobNexus.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace JobNexus.Services;

/// <summary>
/// Background service that runs on a schedule to fetch job data from
/// the Adzuna API, normalize it, and save it to the JobNexus database.
///
/// Data flow per sync cycle:
///   FetchAdzunaJobsAsync() → AdzunaNormalizer → DB
/// </summary>
public class JobSyncService : BackgroundService
{
    private readonly ILogger<JobSyncService> _logger;
    private readonly IServiceProvider _services;

    // Shared HttpClient — reused across requests rather than created per call
    private static readonly HttpClient _httpClient = new();

    // JSON options matching Nick's deserialization settings
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Switch to TimeSpan.FromSeconds(30) for testing, then back before committing
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

        using var scope = _services.CreateScope();
        var dbFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<JobNexusContext>>();

        await RunAdzunaSync(dbFactory, stoppingToken);

        _logger.LogInformation("Job sync cycle complete.");
    }

    private async Task RunAdzunaSync(
        IDbContextFactory<JobNexusContext> dbFactory,
        CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Adzuna sync starting.");

            var adzunaResponse = await FetchAdzunaJobsAsync(stoppingToken);

            if (adzunaResponse?.Results == null)
            {
                _logger.LogWarning("Adzuna returned no results.");
                return;
            }

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

    /// <summary>
    /// Fetches jobs from the Adzuna API.
    /// Adapted from Nick's Adzuna() method in Program.cs.
    ///
    /// TODO: Move app_id and app_key to appsettings.json — they should not
    /// be hardcoded in source code. Nick flagged this in his original code.
    /// TODO: Make the search query and region configurable via appsettings.
    /// </summary>
    private async Task<AdzunaResponse?> FetchAdzunaJobsAsync(CancellationToken stoppingToken)
    {
        string url = "https://api.adzuna.com/v1/api/jobs/gb/search/1" +
                     "?app_id=2002d204" +
                     "&app_key=3d3ee187014376c4588679c98790e9bc" +
                     "&results_per_page=20" +
                     "&what=javascript%20developer" +
                     "&content-type=application/json";

        // Nick converts to bytes before deserializing because Adzuna requires it
        var response = await _httpClient.GetAsync(url, stoppingToken);
        response.EnsureSuccessStatusCode();

        byte[] data = await response.Content.ReadAsByteArrayAsync(stoppingToken);
        string responseBody = Encoding.UTF8.GetString(data);

        return JsonSerializer.Deserialize<AdzunaResponse>(responseBody, _jsonOptions);
    }
}