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
/// Runs multiple search queries per cycle to maximize job coverage across
/// Texas and different job categories.
/// </summary>
public class JobSyncService : BackgroundService
{
    private readonly ILogger<JobSyncService> _logger;
    private readonly IServiceProvider _services;

    private static readonly HttpClient _httpClient = new();
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // Switch to TimeSpan.FromSeconds(30) for testing, then back before committing
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);
    // private readonly TimeSpan _interval = TimeSpan.FromSeconds(30); // for testing

    // -----------------------------------------------------------------------
    // Search queries — each combination of keyword + location will be
    // searched separately per sync cycle. Deduplication in AdzunaNormalizer
    // automatically handles any overlapping jobs across queries.
    // Add more entries here to expand coverage.
    // -----------------------------------------------------------------------
    private static readonly List<(string What, string Where)> _adzunaQueries = new()
    {
        ("software developer", "Texas"),
        ("software engineer", "Texas"),
        ("web developer", "Texas"),
        ("data analyst", "Texas"),
        ("cybersecurity", "Texas"),
        ("software developer", "Houston"),
        ("software developer", "Dallas"),
        ("software developer", "Austin"),
        ("software developer", "San Antonio"),
    };

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

        _logger.LogInformation("Job sync cycle complete.");
    }

    private async Task RunAdzunaSync(
        IDbContextFactory<JobNexusContext> dbFactory,
        CancellationToken stoppingToken)
    {
        int totalInserted = 0;

        // Run each search query in sequence
        foreach (var (what, where) in _adzunaQueries)
        {
            try
            {
                _logger.LogInformation(
                    "Adzuna sync starting — '{What}' in '{Where}'.", what, where);

                var response = await FetchAdzunaJobsAsync(what, where, stoppingToken);

                if (response?.Results == null || response.Results.Count == 0)
                {
                    _logger.LogWarning(
                        "Adzuna returned no results for '{What}' in '{Where}'.", what, where);
                    continue;
                }

                var normalizer = new AdzunaNormalizer(dbFactory);
                int inserted = await normalizer.NormalizeAndSaveAsync(
                    response.Results, stoppingToken);

                totalInserted += inserted;

                _logger.LogInformation(
                    "Adzuna '{What}' in '{Where}' complete. {Count} new jobs inserted.",
                    what, where, inserted);

                // Small delay between requests to be respectful to the API
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Adzuna sync failed for '{What}' in '{Where}'.", what, where);
                // Continue to next query even if this one failed
            }
        }

        _logger.LogInformation(
            "Adzuna sync cycle complete. {Total} total new jobs inserted.", totalInserted);
    }

    /// <summary>
    /// Fetches jobs from the Adzuna API for a given keyword and location.
    /// TODO: Move app_id and app_key to appsettings.json before deployment.
    /// </summary>
    private async Task<AdzunaResponse?> FetchAdzunaJobsAsync(
        string what,
        string where,
        CancellationToken stoppingToken)
    {
        string encodedWhat = Uri.EscapeDataString(what);
        string encodedWhere = Uri.EscapeDataString(where);

        string url = "https://api.adzuna.com/v1/api/jobs/us/search/1" +
                     "?app_id=2002d204" +
                     "&app_key=3d3ee187014376c4588679c98790e9bc" +
                     $"&what={encodedWhat}" +
                     $"&where={encodedWhere}" +
                     "&results_per_page=20" +
                     "&content-type=application/json";

        var response = await _httpClient.GetAsync(url, stoppingToken);
        response.EnsureSuccessStatusCode();

        byte[] data = await response.Content.ReadAsByteArrayAsync(stoppingToken);
        string responseBody = Encoding.UTF8.GetString(data);

        return JsonSerializer.Deserialize<AdzunaResponse>(responseBody, _jsonOptions);
    }
}