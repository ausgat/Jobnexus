using JobNexus.Core.Models;
using JobNexus.Services;
using JobNexus.Tests.Base;

namespace JobNexus.Tests;

/// <summary>
/// Tests for AdzunaNormalizer — verifies that raw Adzuna API response
/// data gets correctly mapped into our Job, Company, and JobSource models.
/// Uses the same in-memory SQLite pattern as the rest of the test suite.
/// </summary>
public class AdzunaNormalizerTest : DbTestBase
{
    // -----------------------------------------------------------------------
    // Helper — builds a minimal valid AdzunaJob for use across tests
    // -----------------------------------------------------------------------
    private static AdzunaJob MakeAdzunaJob(
        string id = "adzuna-001",
        string title = "Software Engineer",
        string company = "Test Corp",
        string category = "IT Jobs",
        string url = "https://adzuna.com/job/001",
        double salaryMin = 60000,
        double salaryMax = 80000)
    {
        return new AdzunaJob
        {
            Id = id,
            Title = title,
            Description = "A test job description",
            RedirectUrl = url,
            Created = new DateTime(2026, 1, 15),
            SalaryMin = salaryMin,
            SalaryMax = salaryMax,
            SalaryIsPredicted = 0,
            Company = new AdzunaCompany { DisplayName = company },
            Category = new AdzunaCategory { Label = category, Tag = "it-jobs" },
            Location = new AdzunaLocation { DisplayName = "Dallas, TX" }
        };
    }

    // -----------------------------------------------------------------------
    // Test 1 — Basic normalization inserts a job into the DB
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_ValidJob_InsertsJobIntoDatabase()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var jobs = new List<AdzunaJob> { MakeAdzunaJob() };

        int inserted = await normalizer.NormalizeAndSaveAsync(jobs);

        // Should have inserted exactly 1 job
        Assert.Equal(1, inserted);
        Assert.Single(db.Jobs);
    }

    // -----------------------------------------------------------------------
    // Test 2 — Title, Description, ApplyUrl, DatePosted map correctly
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_ValidJob_MapsFieldsCorrectly()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var adzunaJob = MakeAdzunaJob(
            title: "Backend Developer",
            url: "https://adzuna.com/job/999");

        await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { adzunaJob });

        var saved = db.Jobs.First();
        Assert.Equal("Backend Developer", saved.Title);
        Assert.Equal("A test job description", saved.Description);
        Assert.Equal("https://adzuna.com/job/999", saved.ApplyUrl);
        Assert.Equal(new DateTime(2026, 1, 15), saved.DatePosted);
    }

    // -----------------------------------------------------------------------
    // Test 3 — Salary averages min and max into Pay field
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_SalaryMinMax_AveragesIntoPay()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var job = MakeAdzunaJob(salaryMin: 60000, salaryMax: 80000);
        await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });

        var saved = db.Jobs.First();
        Assert.Equal(70000, saved.Pay); // (60000 + 80000) / 2
    }

    // -----------------------------------------------------------------------
    // Test 4 — Only salary min provided still sets Pay
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_OnlySalaryMin_SetsPay()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var job = MakeAdzunaJob(salaryMin: 50000, salaryMax: 0);
        job.SalaryMax = null;
        await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });

        var saved = db.Jobs.First();
        Assert.Equal(50000, saved.Pay);
    }

    // -----------------------------------------------------------------------
    // Test 5 — No salary data results in null Pay
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_NoSalary_PayIsNull()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var job = MakeAdzunaJob();
        job.SalaryMin = null;
        job.SalaryMax = null;
        await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });

        var saved = db.Jobs.First();
        Assert.Null(saved.Pay);
    }

    // -----------------------------------------------------------------------
    // Test 6 — Company record is created from Adzuna company name
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_NewCompany_CreatesCompanyRecord()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var job = MakeAdzunaJob(company: "Acme Corp", category: "IT Jobs");
        await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });

        var company = db.Companies.FirstOrDefault();
        Assert.NotNull(company);
        Assert.Equal("Acme Corp", company.CompanyName);
        Assert.Equal("IT Jobs", company.Industry);
        // Adzuna doesn't provide website — should be null
        Assert.Null(company.WebsiteUrl);
    }

    // -----------------------------------------------------------------------
    // Test 7 — Same company across two jobs only creates one Company record
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_TwoJobsSameCompany_OnlyOneCompanyRecord()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var jobs = new List<AdzunaJob>
        {
            MakeAdzunaJob(id: "001", url: "https://adzuna.com/1", company: "Same Corp"),
            MakeAdzunaJob(id: "002", url: "https://adzuna.com/2", company: "Same Corp")
        };

        await normalizer.NormalizeAndSaveAsync(jobs);

        Assert.Equal(2, db.Jobs.Count());
        Assert.Single(db.Companies); // Only one company record
    }

    // -----------------------------------------------------------------------
    // Test 8 — Adzuna JobSource record is created automatically
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_CreatesAdzunaJobSourceRecord()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { MakeAdzunaJob() });

        var source = db.JobSources.FirstOrDefault();
        Assert.NotNull(source);
        Assert.Equal("Adzuna", source.SourceName);
    }

    // -----------------------------------------------------------------------
    // Test 9 — Duplicate job (same ApplyUrl) is not inserted twice
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_DuplicateJob_NotInsertedTwice()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var job = MakeAdzunaJob(url: "https://adzuna.com/job/duplicate");

        // First insert
        int firstCount = await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });
        // Second insert with same URL
        int secondCount = await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });

        Assert.Equal(1, firstCount);
        Assert.Equal(0, secondCount); // Nothing inserted second time
        Assert.Single(db.Jobs);       // Still only one job in DB
    }

    // -----------------------------------------------------------------------
    // Test 10 — Job with no ID is skipped
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_JobWithNoId_IsSkipped()
    {
        await using var db = CreateContext();
        var normalizer = new AdzunaNormalizer(Factory);

        var job = MakeAdzunaJob();
        job.Id = null; // No ID

        int inserted = await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob> { job });

        Assert.Equal(0, inserted);
        Assert.Empty(db.Jobs);
    }

    // -----------------------------------------------------------------------
    // Test 11 — Empty list returns 0 inserted
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Normalize_EmptyList_ReturnsZero()
    {
        var normalizer = new AdzunaNormalizer(Factory);
        int inserted = await normalizer.NormalizeAndSaveAsync(new List<AdzunaJob>());
        Assert.Equal(0, inserted);
    }
}