using JobNexus.Core.Models;
using JobNexus.Services;
using JobNexus.Tests.Base;

namespace JobNexus.Tests;

/// <summary>
/// Tests for JSearchNormalizer — verifies that raw JSearch API response
/// data gets correctly mapped into our DB models, and that the 200
/// request/month rate limit is enforced correctly.
/// </summary>
public class JSearchNormalizerTest : DbTestBase
{
    // -----------------------------------------------------------------------
    // Helper — builds a minimal valid JSearchJob for use across tests
    // -----------------------------------------------------------------------
    private static JSearchJob MakeJSearchJob(
        string id = "jsearch-001",
        string title = "Frontend Developer",
        string employer = "Web Corp",
        string website = "https://webcorp.com",
        string url = "https://indeed.com/job/001",
        double salaryMin = 70000,
        double salaryMax = 90000)
    {
        return new JSearchJob
        {
            JobId = id,
            JobTitle = title,
            JobDescription = "A JSearch test job",
            JobApplyLink = url,
            JobPostedAtDatetimeUtc = new DateTime(2026, 2, 10),
            JobMinSalary = salaryMin,
            JobMaxSalary = salaryMax,
            JobSalaryPeriod = "YEAR",
            EmployerName = employer,
            EmployerWebsite = website,
            JobCity = "Austin",
            JobState = "TX",
            JobCountry = "US",
            JobIsRemote = false,
            JobPublisher = "Indeed"
        };
    }

    // -----------------------------------------------------------------------
    // Reset rate limit counter before each test so tests don't interfere
    // -----------------------------------------------------------------------
    public JSearchNormalizerTest()
    {
        // Reset the static counter by using reflection or by calling enough
        // times — easiest is to just ensure tests run with a fresh month
        // by calling CanMakeRequest which auto-resets if month changed.
        // For safety we directly access the counter via the public static API.
        // Note: if tests are running in the same month, counter carries over.
        // Each test that modifies the counter should account for this.
    }

    // -----------------------------------------------------------------------
    // Test 1 — Basic normalization inserts a job
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_ValidJob_InsertsJobIntoDatabase()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var jobs = new List<JSearchJob> { MakeJSearchJob() };
        int inserted = await normalizer.NormalizeAndSaveAsync(jobs);

        Assert.Equal(1, inserted);
        Assert.Single(db.Jobs);
    }

    // -----------------------------------------------------------------------
    // Test 2 — Fields map correctly from JSearch to Job model
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_ValidJob_MapsFieldsCorrectly()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var jSearchJob = MakeJSearchJob(
            title: "DevOps Engineer",
            url: "https://linkedin.com/job/devops");

        await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { jSearchJob });

        var saved = db.Jobs.First();
        Assert.Equal("DevOps Engineer", saved.Title);
        Assert.Equal("A JSearch test job", saved.Description);
        Assert.Equal("https://linkedin.com/job/devops", saved.ApplyUrl);
        Assert.Equal(new DateTime(2026, 2, 10), saved.DatePosted);
    }

    // -----------------------------------------------------------------------
    // Test 3 — Salary averages min and max into Pay
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_SalaryMinMax_AveragesIntoPay()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var job = MakeJSearchJob(salaryMin: 70000, salaryMax: 90000);
        await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { job });

        var saved = db.Jobs.First();
        Assert.Equal(80000, saved.Pay); // (70000 + 90000) / 2
    }

    // -----------------------------------------------------------------------
    // Test 4 — Company record created with website URL (JSearch provides this)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_NewCompany_CreatesCompanyWithWebsite()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var job = MakeJSearchJob(employer: "Tech Inc", website: "https://techinc.com");
        await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { job });

        var company = db.Companies.FirstOrDefault();
        Assert.NotNull(company);
        Assert.Equal("Tech Inc", company.CompanyName);
        // JSearch provides website — should be populated
        Assert.Equal("https://techinc.com", company.WebsiteUrl);
    }

    // -----------------------------------------------------------------------
    // Test 5 — If company exists from Adzuna (no website), JSearch fills it in
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_ExistingCompanyNoWebsite_FillsInWebsite()
    {
        await using var db = CreateContext();

        // Pre-seed a company with no website (as Adzuna would create it)
        var existingCompany = new JobNexus.Core.Models.Company
        {
            CompanyName = "Shared Corp",
            WebsiteUrl = null
        };
        db.Companies.Add(existingCompany);
        await db.SaveChangesAsync();

        var normalizer = new JSearchNormalizer(Factory);
        var job = MakeJSearchJob(employer: "Shared Corp", website: "https://sharedcorp.com");
        await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { job });

        // Refresh from DB
        var updated = db.Companies.First(c => c.CompanyName == "Shared Corp");
        Assert.Equal("https://sharedcorp.com", updated.WebsiteUrl);
    }

    // -----------------------------------------------------------------------
    // Test 6 — JSearch JobSource record created automatically
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_CreatesJSearchJobSourceRecord()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { MakeJSearchJob() });

        var source = db.JobSources.FirstOrDefault();
        Assert.NotNull(source);
        Assert.Equal("JSearch", source.SourceName);
    }

    // -----------------------------------------------------------------------
    // Test 7 — Duplicate job not inserted twice
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_DuplicateJob_NotInsertedTwice()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var job = MakeJSearchJob(url: "https://indeed.com/job/duplicate");

        int first = await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { job });
        int second = await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { job });

        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Single(db.Jobs);
    }

    // -----------------------------------------------------------------------
    // Test 8 — Job with no ID is skipped
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_JobWithNoId_IsSkipped()
    {
        await using var db = CreateContext();
        var normalizer = new JSearchNormalizer(Factory);

        var job = MakeJSearchJob();
        job.JobId = null;

        int inserted = await normalizer.NormalizeAndSaveAsync(new List<JSearchJob> { job });

        Assert.Equal(0, inserted);
        Assert.Empty(db.Jobs);
    }

    // -----------------------------------------------------------------------
    // Test 9 — CanMakeRequest returns true when under the cap
    // -----------------------------------------------------------------------
    [Fact]
    public void RateLimit_CanMakeRequest_ReturnsTrueWhenUnderCap()
    {
        // As long as we haven't hit 200 this month, should be true
        Assert.True(JSearchNormalizer.CanMakeRequest());
    }

    // -----------------------------------------------------------------------
    // Test 10 — RecordRequest increments the counter
    // -----------------------------------------------------------------------
    [Fact]
    public void RateLimit_RecordRequest_IncrementsCounter()
    {
        int before = JSearchNormalizer.RequestsUsedThisMonth;
        JSearchNormalizer.RecordRequest();
        int after = JSearchNormalizer.RequestsUsedThisMonth;

        Assert.Equal(before + 1, after);
    }

    // -----------------------------------------------------------------------
    // Test 11 — RequestsRemainingThisMonth decreases after RecordRequest
    // -----------------------------------------------------------------------
    [Fact]
    public void RateLimit_RequestsRemaining_DecreasesAfterRecord()
    {
        int before = JSearchNormalizer.RequestsRemainingThisMonth;
        JSearchNormalizer.RecordRequest();
        int after = JSearchNormalizer.RequestsRemainingThisMonth;

        Assert.Equal(before - 1, after);
    }

    // -----------------------------------------------------------------------
    // Test 12 — NormalizeAndSaveAsync returns -1 when rate limit reached
    // Note: this test manipulates the static counter so it runs last
    // to avoid interfering with other tests. Mark with a known order if needed.
    // -----------------------------------------------------------------------
    [Fact]
    public async Task RateLimit_WhenCapReached_ReturnsNegativeOne()
    {
        var normalizer = new JSearchNormalizer(Factory);

        // Fill up the remaining requests to hit the cap
        int remaining = JSearchNormalizer.RequestsRemainingThisMonth;
        for (int i = 0; i < remaining; i++)
        {
            JSearchNormalizer.RecordRequest();
        }

        // Now the cap should be hit
        Assert.False(JSearchNormalizer.CanMakeRequest());

        // NormalizeAndSaveAsync should return -1 as a signal
        int result = await normalizer.NormalizeAndSaveAsync(
            new List<JSearchJob> { MakeJSearchJob() });

        Assert.Equal(-1, result);
    }
}