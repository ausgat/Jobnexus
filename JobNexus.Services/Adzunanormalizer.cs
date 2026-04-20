using JobNexus.Core.Models;
using JobNexus.Data;
using Microsoft.EntityFrameworkCore;

namespace JobNexus.Services;

/// <summary>
/// Responsible for taking raw Adzuna API job data and normalizing it
/// into JobNexus database models (Job, Company, JobSource).
///
/// This is the class Cody's upload code should call after Nick's API
/// fetch returns an AdzunaResponse. The general flow is:
///
///   1. Nick fetches AdzunaResponse from the API
///   2. AdzunaNormalizer.NormalizeAndSaveAsync() is called with those results
///   3. This class maps each AdzunaJob into our Job/Company/JobSource models
///   4. Records are upserted into the database (insert if new, skip if exists)
///
/// TEAM NOTE: This class handles the normalization and DB write.
/// Nick's job is to fetch and return AdzunaResponse.
/// Cody's job is to wire this normalizer into the DB pipeline.
/// </summary>
public class AdzunaNormalizer
{
    private readonly IDbContextFactory<JobNexusContext> _dbFactory;

    // The source name we use to identify Adzuna in the JobSource table
    // This is inserted once and reused for all Adzuna jobs
    private const string AdzunaSourceName = "Adzuna";

    public AdzunaNormalizer(IDbContextFactory<JobNexusContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// Takes a list of raw Adzuna jobs, normalizes them, and saves to the database.
    /// Handles Company and JobSource upserts automatically.
    /// Returns the number of new jobs inserted.
    /// </summary>
    public async Task<int> NormalizeAndSaveAsync(
        List<AdzunaJob> adzunaJobs,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _dbFactory.CreateDbContextAsync(cancellationToken);

        // Step 1 — Ensure the Adzuna JobSource record exists in the DB
        // This only needs to happen once but we check every time to be safe
        var source = await GetOrCreateJobSourceAsync(context, cancellationToken);

        int insertedCount = 0;

        foreach (var adzunaJob in adzunaJobs)
        {
            // Skip any jobs with no ID — we can't deduplicate without it
            if (string.IsNullOrEmpty(adzunaJob.Id))
            {
                continue;
            }

            // Step 2 — Check if this job already exists in our DB
            // We use the ApplyUrl (which contains the Adzuna ID) as the dedup key
            // -----------------------------------------------------------------------
            // TODO: If Nick's API fetch stores the Adzuna ID somewhere separately,
            // consider adding an AdzunaJobId column to the Job table for cleaner
            // deduplication instead of matching on ApplyUrl.
            // -----------------------------------------------------------------------
            var applyUrl = adzunaJob.RedirectUrl;
            bool jobExists = await context.Jobs
                .AnyAsync(j => j.ApplyUrl == adzunaJob.RedirectUrl 
                && j.Title == adzunaJob.Title, cancellationToken);

            if (jobExists)
            {
                // Job already in our DB — skip to avoid duplicates
                continue;
            }

            // Step 3 — Get or create the Company record for this job
            var company = await GetOrCreateCompanyAsync(
                context,
                adzunaJob.Company?.DisplayName,
                adzunaJob.Category?.Label,
                cancellationToken);

            // Step 4 — Map the Adzuna job fields to our Job model
            var job = MapToJob(adzunaJob, source, company);

            context.Jobs.Add(job);
            insertedCount++;
        }

        // Step 5 — Save all new records in one batch
        await context.SaveChangesAsync(cancellationToken);

        return insertedCount;
    }

    /// <summary>
    /// Maps a single AdzunaJob to our JobNexus Job model.
    /// This is where the field-by-field translation happens.
    /// </summary>
    private static Job MapToJob(AdzunaJob adzunaJob, JobSource source, Company? company)
    {
        return new Job
        {
            // Map title directly
            Title = adzunaJob.Title,

            // Adzuna only provides a snippet of the description
            // TODO: If full descriptions become available, update this field
            Description = adzunaJob.Description,

            // Use the Adzuna redirect URL as the apply link
            ApplyUrl = adzunaJob.RedirectUrl,

            // Use the posted date from Adzuna
            DatePosted = adzunaJob.Created,

            // -----------------------------------------------------------------------
            // Salary normalization:
            // Adzuna provides salary_min and salary_max separately as doubles.
            // Our DB stores Pay as a single int — we take the average of min/max
            // if both are available, otherwise whichever one exists.
            // TODO: Consider expanding the Job model to store salary_min and
            // salary_max separately for better filtering capabilities.
            // -----------------------------------------------------------------------
            Pay = NormalizeSalary(adzunaJob.SalaryMin, adzunaJob.SalaryMax),

            // Link to the JobSource record for Adzuna
            SourceId = source.SourceId,

            // Link to the Company record (may be null if company name was missing)
            CompanyId = company?.CompanyId,
        };
    }

    /// <summary>
    /// Converts Adzuna's salary_min and salary_max (doubles) into a single int
    /// for our Pay field. Returns null if no salary data is available.
    /// </summary>
    private static int? NormalizeSalary(double? salaryMin, double? salaryMax)
    {
        if (salaryMin.HasValue && salaryMax.HasValue)
        {
            // Average of min and max
            return (int)((salaryMin.Value + salaryMax.Value) / 2);
        }
        if (salaryMin.HasValue)
        {
            return (int)salaryMin.Value;
        }
        if (salaryMax.HasValue)
        {
            return (int)salaryMax.Value;
        }

        // No salary data available
        return null;
    }

    /// <summary>
    /// Finds an existing JobSource for Adzuna or creates one if it doesn't exist.
    /// This ensures we only ever have one Adzuna source row in the DB.
    /// </summary>
    private static async Task<JobSource> GetOrCreateJobSourceAsync(
        JobNexusContext context,
        CancellationToken cancellationToken)
    {
        var source = await context.JobSources
            .FirstOrDefaultAsync(s => s.SourceName == AdzunaSourceName, cancellationToken);

        if (source is null)
        {
            source = new JobSource { SourceName = AdzunaSourceName };
            context.JobSources.Add(source);
            await context.SaveChangesAsync(cancellationToken);
        }

        return source;
    }

    /// <summary>
    /// Finds an existing Company by name or creates a new one.
    /// Returns null if no company name was provided by Adzuna.
    /// 
    /// NOTE: Adzuna only gives us the company display name — no website or
    /// full industry data. The Industry field is populated from the job's
    /// category label (e.g. "IT Jobs") as the closest available equivalent.
    /// TODO: If a separate company data source becomes available, enrich these
    /// records with website URLs and more accurate industry classifications.
    /// </summary>
    private static async Task<Company?> GetOrCreateCompanyAsync(
        JobNexusContext context,
        string? companyName,
        string? industryLabel,
        CancellationToken cancellationToken)
    {
        // If Adzuna didn't provide a company name, we can't create a record
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

                // Use category label as a rough industry approximation
                // e.g. "IT Jobs" → Industry = "IT Jobs"
                Industry = industryLabel,

                // TODO: Website URL not provided by Adzuna — leave null for now
                // Could be populated later via a separate enrichment step
                WebsiteUrl = null,
            };
            context.Companies.Add(company);
            await context.SaveChangesAsync(cancellationToken);
        }

        return company;
    }
}