using JobNexus.Core.Models;
using JobNexus.Core.Search;
using JobNexus.Services;
using JobNexus.Tests.Base;

namespace JobNexus.Tests;

/// <summary>
/// Extended tests for SearchService — builds on the existing SearchTest.cs
/// already in the repo. These tests focus specifically on the features
/// we added: Company and Source navigation property loading, source filtering,
/// and date filtering.
///
/// NOTE: The existing SearchTest.cs already covers basic keyword search
/// and company filtering — these tests cover the new additions only.
/// </summary>
public class SearchServiceExtendedTest : DbTestBase
{
    // -----------------------------------------------------------------------
    // Helper — seeds the DB with jobs from both sources for filter testing
    // -----------------------------------------------------------------------
    private async Task SeedJobsWithSources(JobNexusContext db)
    {
        var adzunaSource = new JobSource { SourceName = "Adzuna" };
        var jSearchSource = new JobSource { SourceName = "JSearch" };
        var company = new Company { CompanyName = "Test Corp", Industry = "IT" };

        db.JobSources.AddRange(adzunaSource, jSearchSource);
        db.Companies.Add(company);

        db.Jobs.AddRange(
            new Job
            {
                Title = "Adzuna Job",
                Description = "From Adzuna",
                Source = adzunaSource,
                Company = company,
                DatePosted = new DateTime(2026, 1, 1),
                Pay = 60000
            },
            new Job
            {
                Title = "JSearch Job",
                Description = "From JSearch",
                Source = jSearchSource,
                Company = company,
                DatePosted = new DateTime(2026, 2, 1),
                Pay = 80000
            },
            new Job
            {
                Title = "Another Adzuna Job",
                Description = "Also from Adzuna",
                Source = adzunaSource,
                Company = company,
                DatePosted = new DateTime(2026, 3, 1),
                Pay = 70000
            }
        );

        await db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Test 1 — Empty keyword search returns all jobs
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_EmptyKeyword_ReturnsAllJobs()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(new SearchQuery { Keywords = [""] });

        Assert.Equal(3, results.Count);
    }

    // -----------------------------------------------------------------------
    // Test 2 — Results include Company navigation property
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_Results_IncludeCompanyNavigationProperty()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(new SearchQuery { Keywords = [""] });

        // Every result should have Company loaded — not null
        Assert.All(results, job => Assert.NotNull(job.Company));
        Assert.All(results, job => Assert.Equal("Test Corp", job.Company!.CompanyName));
    }

    // -----------------------------------------------------------------------
    // Test 3 — Results include Source navigation property
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_Results_IncludeSourceNavigationProperty()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(new SearchQuery { Keywords = [""] });

        // Every result should have Source loaded — not null
        Assert.All(results, job => Assert.NotNull(job.Source));
    }

    // -----------------------------------------------------------------------
    // Test 4 — Date filter excludes jobs before the cutoff
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_DatePostedAfter_FiltersOldJobs()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);

        // Only jobs posted after Feb 1 2026 should come back
        var results = await search.SearchAsync(new SearchQuery
        {
            Keywords = [""],
            DatePostedAfter = new DateTime(2026, 2, 1)
        });

        // Should get Feb 1 and Mar 1 jobs — not Jan 1
        Assert.Equal(2, results.Count);
        Assert.All(results, job =>
            Assert.True(job.DatePosted >= new DateTime(2026, 2, 1)));
    }

    // -----------------------------------------------------------------------
    // Test 5 — Results ordered by DatePosted descending (newest first)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_Results_OrderedByDatePostedDescending()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(new SearchQuery { Keywords = [""] });

        // First result should be the most recently posted
        Assert.Equal(new DateTime(2026, 3, 1), results[0].DatePosted);
        Assert.Equal(new DateTime(2026, 2, 1), results[1].DatePosted);
        Assert.Equal(new DateTime(2026, 1, 1), results[2].DatePosted);
    }

    // -----------------------------------------------------------------------
    // Test 6 — Keyword matches title
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_KeywordMatchesTitle_ReturnsCorrectJob()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(new SearchQuery { Keywords = ["JSearch"] });

        Assert.Single(results);
        Assert.Equal("JSearch Job", results[0].Title);
    }

    // -----------------------------------------------------------------------
    // Test 7 — Keyword matches description
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_KeywordMatchesDescription_ReturnsCorrectJob()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);

        // "Also" only appears in the description of "Another Adzuna Job"
        var results = await search.SearchAsync(new SearchQuery { Keywords = ["Also"] });

        Assert.Single(results);
        Assert.Equal("Another Adzuna Job", results[0].Title);
    }

    // -----------------------------------------------------------------------
    // Test 8 — Multiple keywords return union of matches (OR behavior)
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_MultipleKeywords_ReturnsUnionOfMatches()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);

        // "JSearch" matches one job, "Another" matches one job — 2 total
        var results = await search.SearchAsync(
            new SearchQuery { Keywords = ["JSearch", "Another"] });

        Assert.Equal(2, results.Count);
    }

    // -----------------------------------------------------------------------
    // Test 9 — No results returns empty list not null
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyList()
    {
        await using var db = CreateContext();
        await SeedJobsWithSources(db);

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(
            new SearchQuery { Keywords = ["xyzzynonexistent"] });

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    // -----------------------------------------------------------------------
    // Test 10 — Company filter returns only jobs from that company
    // -----------------------------------------------------------------------
    [Fact]
    public async Task Search_CompanyFilter_ReturnsOnlyMatchingCompany()
    {
        await using var db = CreateContext();

        var companyA = new Company { CompanyName = "Alpha Inc" };
        var companyB = new Company { CompanyName = "Beta LLC" };
        db.Companies.AddRange(companyA, companyB);
        db.Jobs.AddRange(
            new Job { Title = "Alpha Job", Company = companyA },
            new Job { Title = "Beta Job", Company = companyB }
        );
        await db.SaveChangesAsync();

        var search = new SearchService(Factory);
        var results = await search.SearchAsync(
            new SearchQuery { Keywords = [""], Company = "Alpha Inc" });

        Assert.Single(results);
        Assert.Equal("Alpha Job", results[0].Title);
    }
}