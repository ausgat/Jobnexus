using JobNexus.Core.Models;
using JobNexus.Data;
using Microsoft.EntityFrameworkCore;
 
namespace JobNexus.Services;
 
/// <summary>
/// Responsible for taking raw JSearch API job data and normalizing it
/// into JobNexus database models (Job, Company, JobSource).
///
/// IMPORTANT — RATE LIMIT:
/// JSearch has a hard cap of 200 requests per month on the free tier.
/// This class tracks how many requests have been made this month via
/// a static counter and will refuse to proceed if the cap is reached.
///
/// Each call to NormalizeAndSaveAsync counts as consuming one "pull"
/// from that budget — the actual HTTP request count is managed by
/// Nick's fetch code, but this class enforces the monthly cap.
///
/// TODO: If the team upgrades to a paid JSearch plan, update
/// JSearchMonthlyRequestCap below to match the new limit.
/// </summary>
public class JSearchNormalizer
{
    private readonly IDbContextFactory<JobNexusContext> _dbFactory;
 
    // The source name we use to identify JSearch in the JobSource table
    private const string JSearchSourceName = "JSearch";
 
    // -----------------------------------------------------------------------
    // RATE LIMIT CONFIGURATION
    // Hard cap of 200 requests per month on the free tier.
    // This is tracked in-memory — it resets when the app restarts.
    // TODO: Persist this counter to the database or appsettings.json so it
    // survives app restarts and gives an accurate monthly count.
    // -----------------------------------------------------------------------
    private const int JSearchMonthlyRequestCap = 200;
 
    // Tracks how many JSearch pulls have been made this month
    // Static so it persists across multiple sync cycles in the same app session
    private static int _requestsThisMonth = 0;
 
    // Tracks which month the counter was last reset
    // Used to auto-reset the counter at the start of a new month
    private static int _lastResetMonth = DateTime.UtcNow.Month;
 
    public JSearchNormalizer(IDbContextFactory<JobNexusContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }
 
    /// <summary>
    /// Returns how many JSearch requests have been used this month.
    /// Useful for logging or displaying remaining quota.
    /// </summary>
    public static int RequestsUsedThisMonth => _requestsThisMonth;
 
    /// <summary>
    /// Returns how many JSearch requests remain this month.
    /// </summary>
    public static int RequestsRemainingThisMonth =>
        JSearchMonthlyRequestCap - _requestsThisMonth;
 
    /// <summary>
    /// Returns true if we still have requests available this month.
    /// Nick's fetch code should call this before making an API request.
    /// </summary>
    public static bool CanMakeRequest()
    {
        ResetCounterIfNewMonth();
        return _requestsThisMonth < JSearchMonthlyRequestCap;
    }
 
    /// <summary>
    /// Call this AFTER Nick successfully makes a JSearch API request
    /// to increment the monthly counter.
    /// </summary>
    public static void RecordRequest()
    {
        ResetCounterIfNewMonth();
        _requestsThisMonth++;
    }
 
    /// <summary>
    /// Resets the monthly counter if we've rolled into a new calendar month.
    /// </summary>
    private static void ResetCounterIfNewMonth()
    {
        int currentMonth = DateTime.UtcNow.Month;
        if (currentMonth != _lastResetMonth)
        {
            _requestsThisMonth = 0;
            _lastResetMonth = currentMonth;
        }
    }
 
    /// <summary>
    /// Takes a list of raw JSearch jobs, normalizes them, and saves to the database.
    /// Handles Company and JobSource upserts automatically.
    /// Returns the number of new jobs inserted, or -1 if the rate limit was hit.
    /// </summary>
    public async Task<int> NormalizeAndSaveAsync(
        List<JSearchJob> jSearchJobs,
        CancellationToken cancellationToken = default)
    {
        // -----------------------------------------------------------------------
        // RATE LIMIT CHECK — refuse to proceed if monthly cap is reached
        // This is a safety net in case Nick's fetch code doesn't check first.
        // -----------------------------------------------------------------------
        if (!CanMakeRequest())
        {
            // Return -1 as a signal to the caller that the rate limit was hit
            // The caller (JobSyncService) should log this and skip JSearch for
            // the rest of the month
            return -1;
        }
 
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);
 
        // Step 1 — Ensure the JSearch JobSource record exists in the DB
        var source = await GetOrCreateJobSourceAsync(context, cancellationToken);
 
        int insertedCount = 0;
 
        foreach (var jSearchJob in jSearchJobs)
        {
            // Skip jobs with no ID — can't deduplicate without it
            if (string.IsNullOrEmpty(jSearchJob.JobId))
            {
                continue;
            }
 
            // Step 2 — Check if this job already exists using the apply link as dedup key
            // -----------------------------------------------------------------------
            // TODO: Same note as AdzunaNormalizer — if a dedicated external ID column
            // is added to the Job table, use that for cleaner deduplication.
            // -----------------------------------------------------------------------
            bool jobExists = await context.Jobs
                .AnyAsync(j => j.ApplyUrl == jSearchJob.JobApplyLink, cancellationToken);
 
            if (jobExists)
            {
                continue;
            }
 
            // Step 3 — Get or create the Company record
            var company = await GetOrCreateCompanyAsync(
                context,
                jSearchJob.EmployerName,
                jSearchJob.EmployerWebsite,
                cancellationToken);
 
            // Step 4 — Map JSearch fields to our Job model
            var job = MapToJob(jSearchJob, source, company);
 
            context.Jobs.Add(job);
            insertedCount++;
        }
 
        // Step 5 — Save all new records in one batch
        await context.SaveChangesAsync(cancellationToken);
 
        return insertedCount;
    }
 
    /// <summary>
    /// Maps a single JSearchJob to our JobNexus Job model.
    /// </summary>
    private static Job MapToJob(JSearchJob jSearchJob, JobSource source, Company? company)
    {
        return new Job
        {
            Title = jSearchJob.JobTitle,
 
            // JSearch provides the full description unlike Adzuna which only gives a snippet
            Description = jSearchJob.JobDescription,
 
            ApplyUrl = jSearchJob.JobApplyLink,
 
            DatePosted = jSearchJob.JobPostedAtDatetimeUtc,
 
            // -----------------------------------------------------------------------
            // Salary normalization:
            // JSearch provides salary_period (YEAR/MONTH/HOUR) along with min/max.
            // We currently just average min/max like Adzuna — we don't convert
            // hourly/monthly to annual yet.
            // TODO: Normalize all salaries to annual equivalent for consistent
            // comparison across job listings. Multiply hourly by ~2080 (40hr x 52wk)
            // or monthly by 12 before averaging.
            // -----------------------------------------------------------------------
            Pay = NormalizeSalary(jSearchJob.JobMinSalary, jSearchJob.JobMaxSalary),
 
            SourceId = source.SourceId,
            CompanyId = company?.CompanyId,
        };
    }
 
    /// <summary>
    /// Averages min/max salary into a single int for the Pay field.
    /// Returns null if no salary data available.
    /// </summary>
    private static int? NormalizeSalary(double? salaryMin, double? salaryMax)
    {
        if (salaryMin.HasValue && salaryMax.HasValue)
            return (int)((salaryMin.Value + salaryMax.Value) / 2);
        if (salaryMin.HasValue)
            return (int)salaryMin.Value;
        if (salaryMax.HasValue)
            return (int)salaryMax.Value;
        return null;
    }
 
    /// <summary>
    /// Finds an existing JobSource for JSearch or creates one if it doesn't exist.
    /// </summary>
    private static async Task<JobSource> GetOrCreateJobSourceAsync(
        JobNexusContext context,
        CancellationToken cancellationToken)
    {
        var source = await context.JobSources
            .FirstOrDefaultAsync(s => s.SourceName == JSearchSourceName, cancellationToken);
 
        if (source is null)
        {
            source = new JobSource { SourceName = JSearchSourceName };
            context.JobSources.Add(source);
            await context.SaveChangesAsync(cancellationToken);
        }
 
        return source;
    }
 
    /// <summary>
    /// Finds an existing Company by name or creates a new one.
    /// JSearch provides employer_website which Adzuna does not — so company
    /// records from JSearch will be more complete.
    /// </summary>
    private static async Task<Company?> GetOrCreateCompanyAsync(
        JobNexusContext context,
        string? companyName,
        string? websiteUrl,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            return null;
        }
 
        var company = await context.Companies
            .FirstOrDefaultAsync(c => c.CompanyName == companyName, cancellationToken);
 
        if (company is null)
        {
            company = new Company
            {
                CompanyName = companyName,
                // JSearch actually provides the website URL — Adzuna does not
                WebsiteUrl = websiteUrl,
                // TODO: JSearch doesn't provide an industry label directly.
                // Could be inferred from job titles or left null.
                Industry = null,
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync(cancellationToken);
        }
        else if (company.WebsiteUrl is null && websiteUrl is not null)
        {
            // If this company already exists from an Adzuna pull (which has no URL),
            // take the opportunity to fill in the website URL from JSearch
            company.WebsiteUrl = websiteUrl;
            await context.SaveChangesAsync(cancellationToken);
        }
 
        return company;
    }
}