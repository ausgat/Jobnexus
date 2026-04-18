using JobNexus.Core.Models;
using JobNexus.Services;
using JobNexus.Tests.Base;
using Microsoft.Extensions.Logging;

namespace JobNexus.Tests;

/// <summary>
/// Tests for JobSyncService — verifies that the sync cycle orchestrates
/// both Adzuna and JSearch normalizers correctly, handles errors gracefully,
/// and respects the JSearch rate limit.
///
/// NOTE: Since JobSyncService depends on Nick's API fetch code (which is
/// still placeholder), these tests focus on the parts we control:
/// the normalizer integration and rate limit behavior.
/// </summary>
public class JobSyncServiceTest : DbTestBase
{
    // -----------------------------------------------------------------------
    // Test 1 — Adzuna normalizer inserts jobs into DB correctly
    // (simulates what RunAdzunaSync does once Nick's code is wired in)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task AdzunaSync_WithValidJobs_InsertsIntoDatabase()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var jobs = new List<AdzunaJob>
        {
            new AdzunaJob
            {
                Id = "sync-test-001",
                Title = "Sync Test Job",
                Description = "Testing sync",
                RedirectUrl = "https://adzuna.com/sync/001",
                Created = DateTime.UtcNow,
                SalaryMin = 50000,
                SalaryMax = 70000,
                Company = new AdzunaCompany { DisplayName = "Sync Corp" },
                Category = new AdzunaCategory { Label = "IT Jobs" }
            }
        };

        int inserted = await normalizer.NormalizeAndSaveAsync(jobs);

        Assert.Equal(1, inserted);
        Assert.Single(db.Jobs);
        Assert.Equal("Sync Test Job", db.Jobs.First().Title);
    }

    // -----------------------------------------------------------------------
    // Test 2 — JSearch normalizer inserts jobs into DB correctly
    // -----------------------------------------------------------------------
    [Fact]
    public async Task JSearchSync_WithValidJobs_InsertsIntoDatabase()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var jobs = new List<JSearchJob>
        {
            new JSearchJob
            {
                JobId = "jsync-test-001",
                JobTitle = "JSearch Sync Test",
                JobDescription = "Testing JSearch sync",
                JobApplyLink = "https://indeed.com/jsync/001",
                JobPostedAtDatetimeUtc = DateTime.UtcNow,
                JobMinSalary = 60000,
                JobMaxSalary = 80000,
                EmployerName = "JSync Corp",
                EmployerWebsite = "https://jsync.com"
            }
        };

        int inserted = await normalizer.NormalizeAndSaveAsync(jobs);

        Assert.Equal(1, inserted);
        Assert.Single(db.Jobs);
        Assert.Equal("JSearch Sync Test", db.Jobs.First().Title);
    }

    // -----------------------------------------------------------------------
    // Test 3 — Both sources write to the same DB without conflict
    // -----------------------------------------------------------------------
    [Fact]
    public async Task BothSources_WriteToSameDatabase_NoConflict()
    {
        await using var db = CreateContext();

        var adzunaNormalizer = new AdzunaNormalizer(Factory);
        var jSearchNormalizer = new JSearchNormalizer(Factory);

        var adzunaJobs = new List<AdzunaJob>
        {
            new AdzunaJob
            {
                Id = "a-001",
                Title = "Adzuna Developer",
                RedirectUrl = "https://adzuna.com/a001",
                Company = new AdzunaCompany { DisplayName = "Corp A" },
                Category = new AdzunaCategory { Label = "IT" }
            }
        };

        var jSearchJobs = new List<JSearchJob>
        {
            new JSearchJob
            {
                JobId = "j-001",
                JobTitle = "JSearch Developer",
                JobApplyLink = "https://indeed.com/j001",
                EmployerName = "Corp B"
            }
        };

        await adzunaNormalizer.NormalizeAndSaveAsync(adzunaJobs);
        await jSearchNormalizer.NormalizeAndSaveAsync(jSearchJobs);

        // Both jobs should be in the DB
        Assert.Equal(2, db.Jobs.Count());

        // Both sources should be recorded
        Assert.Equal(2, db.JobSources.Count());
        Assert.Contains(db.JobSources, s => s.SourceName == "Adzuna");
        Assert.Contains(db.JobSources, s => s.SourceName == "JSearch");
    }

    // -----------------------------------------------------------------------
    // Test 4 — Same company from both sources only creates one Company record
    // -----------------------------------------------------------------------
    [Fact]
    public async Task BothSources_SameCompanyName_OnlyOneCompanyRecord()
    {
        await using var db = CreateContext();

        var adzunaNormalizer = new AdzunaNormalizer(Factory);
        var jSearchNormalizer = new JSearchNormalizer(Factory);

        // Both APIs return a job from "Google"
        var adzunaJobs = new List<AdzunaJob>
        {
            new AdzunaJob
            {
                Id = "g-001",
                Title = "Adzuna Google Job",
                RedirectUrl = "https://adzuna.com/g001",
                Company = new AdzunaCompany { DisplayName = "Google" },
                Category = new AdzunaCategory { Label = "IT" }
            }
        };

        var jSearchJobs = new List<JSearchJob>
        {
            new JSearchJob
            {
                JobId = "g-002",
                JobTitle = "JSearch Google Job",
                JobApplyLink = "https://indeed.com/g002",
                EmployerName = "Google",
                EmployerWebsite = "https://google.com"
            }
        };

        await adzunaNormalizer.NormalizeAndSaveAsync(adzunaJobs);
        await jSearchNormalizer.NormalizeAndSaveAsync(jSearchJobs);

        // Only one Google company record
        Assert.Single(db.Companies.Where(c => c.CompanyName == "Google").ToList());

        // JSearch filled in the website URL that Adzuna couldn't provide
        var google = db.Companies.First(c => c.CompanyName == "Google");
        Assert.Equal("https://google.com", google.WebsiteUrl);
    }

    // -----------------------------------------------------------------------
    // Test 5 — JSearch sync is skipped when rate limit is hit
    // -----------------------------------------------------------------------
    [Fact]
    public async Task JSearchSync_WhenRateLimitHit_ReturnsNegativeOne()
    {
        var normalizer = new JSearchNormalizer(Factory);

        // Exhaust remaining requests
        int remaining = JSearchNormalizer.RequestsRemainingThisMonth;
        for (int i = 0; i < remaining; i++)
            JSearchNormalizer.RecordRequest();

        // Attempting to normalize should return -1 (rate limit signal)
        int result = await normalizer.NormalizeAndSaveAsync(new List<JSearchJob>
        {
            new JSearchJob
            {
                JobId = "rate-test",
                JobTitle = "Should Not Insert",
                JobApplyLink = "https://nowhere.com"
            }
        });

        Assert.Equal(-1, result);
    }

    // -----------------------------------------------------------------------
    // Test 6 — Empty Adzuna response inserts nothing and doesn't crash
    // -----------------------------------------------------------------------
    [Fact]
    public async Task AdzunaSync_EmptyResponse_DoesNotCrash()
    {
        var normalizer = new AdzunaNormalizer(Factory);
        int inserted = await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob>());
        Assert.Equal(0, inserted);
    }

    // -----------------------------------------------------------------------
    // Test 7 — Empty JSearch response inserts nothing and doesn't crash
    // -----------------------------------------------------------------------
    [Fact]
    public async Task JSearchSync_EmptyResponse_DoesNotCrash()
    {
        var normalizer = new JSearchNormalizer(Factory);
        int inserted = await normalizer.NormalizeAndSaveAsync(new List<JSearchJob>());
        Assert.Equal(0, inserted);
    }
}
